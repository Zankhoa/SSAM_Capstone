using Microsoft.EntityFrameworkCore;
using SSAM_Server.Common;
using SSAM_Server.Data;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Work;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.Services;

public class WorkService : IWorkService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionService _permissions;
    private readonly TimeProvider _clock;

    public WorkService(
        AppDbContext db,
        ICurrentUser currentUser,
        IPermissionService permissions,
        TimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _permissions = permissions;
        _clock = clock;
    }

    public async Task<ServiceResult<MyDocumentWorkDto>> GetMyDocumentWorkAsync(
        long? projectId,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserIdOrNull is null)
            return ServiceResult<MyDocumentWorkDto>.Unauthorized();

        var myId = _currentUser.UserId;

        var accessibleProjects = await GetAccessibleProjectsAsync(myId, projectId, ct);
        if (accessibleProjects.Count == 0)
        {
            return ServiceResult<MyDocumentWorkDto>.Success(new MyDocumentWorkDto());
        }

        var projectMap = accessibleProjects.ToDictionary(p => p.Project.Id);
        var projectIds = projectMap.Keys.ToList();

        var nodes = await _db.DocumentNodes
            .AsNoTracking()
            .Include(d => d.CurrentVersion)
                .ThenInclude(v => v!.UpdatedBy)
            .Include(d => d.CurrentVersion)
                .ThenInclude(v => v!.Author)
            .Include(d => d.CurrentVersion)
                .ThenInclude(v => v!.SubmittedBy)
            .Include(d => d.CurrentVersion)
                .ThenInclude(v => v!.ReviewedBy)
            .Where(d => projectIds.Contains(d.ProjectId) && !d.IsDeleted && d.Kind == DocumentNodeKinds.Page && d.CurrentVersionId != null)
            .ToListAsync(ct);

        var currentVerIds = nodes.Select(n => n.CurrentVersionId!.Value).Distinct().ToList();
        var baselineVerIds = nodes.Where(n => n.BaselineVersionId.HasValue).Select(n => n.BaselineVersionId!.Value).Distinct().ToList();
        var allVerIds = currentVerIds.Union(baselineVerIds).Distinct().ToList();

        var allReqs = allVerIds.Count == 0
            ? new List<RequirementItem>()
            : await _db.RequirementItems
                .AsNoTracking()
                .Where(r => allVerIds.Contains(r.VersionId))
                .OrderBy(r => r.SortOrder)
                .ToListAsync(ct);

        var reqsByVer = allReqs
            .GroupBy(r => r.VersionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var docIds = nodes.Select(n => n.Id).ToList();

        var allComments = docIds.Count == 0
            ? new List<DocumentComment>()
            : await _db.DocumentComments
                .AsNoTracking()
                .Include(c => c.Author)
                .Include(c => c.Mentions)
                .Where(c => docIds.Contains(c.DocumentId))
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync(ct);

        var unresolvedCountByDocVer = allComments
            .Where(c => c.ParentId == null && c.ResolvedAt == null)
            .GroupBy(c => (c.DocumentId, c.VersionId))
            .ToDictionary(g => g.Key, g => g.Count());

        var allActivities = docIds.Count == 0
            ? new List<DocumentActivity>()
            : await _db.DocumentActivities
                .AsNoTracking()
                .Include(a => a.Actor)
                .Include(a => a.Version)
                .Where(a => docIds.Contains(a.DocumentId))
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync(ct);

        var confirmedFreshByDoc = allActivities
            .Where(a => a.Action == DocumentActivityActions.ReqConfirmedFresh && a.Note != null)
            .GroupBy(a => a.DocumentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var pendingApprovals = new List<DocumentWorkItemDto>();
        var changesRequested = new List<DocumentWorkItemDto>();
        var myDrafts = new List<DocumentWorkItemDto>();
        var staleRequirements = new List<StaleRequirementItemDto>();
        var mentionsAndComments = new List<DocumentWorkEventDto>();
        var recentEvents = new List<DocumentWorkEventDto>();
        var projectStatsMap = accessibleProjects.ToDictionary(
            p => p.Project.Id,
            p => new ProjectDocumentStatsDto
            {
                ProjectId = p.Project.Id,
                ProjectKey = p.Project.Key,
                ProjectName = p.Project.Name
            });

        var nodeMap = nodes.ToDictionary(n => n.Id);

        foreach (var node in nodes)
        {
            var pInfo = projectMap[node.ProjectId];
            var cur = node.CurrentVersion!;
            var reqList = reqsByVer.GetValueOrDefault(cur.Id) ?? new List<RequirementItem>();
            var unresolvedCount = unresolvedCountByDocVer.GetValueOrDefault((node.Id, cur.Id));

            var stats = projectStatsMap[node.ProjectId];
            stats.TotalDocs++;
            if (node.BaselineVersionId.HasValue) stats.BaselinedDocs++;
            if (cur.Status == DocumentVersionStatuses.InReview) stats.InReviewDocs++;
            if (cur.Status == DocumentVersionStatuses.Draft) stats.DraftDocs++;
            if (cur.Status == DocumentVersionStatuses.ChangesRequested) stats.ChangesRequestedDocs++;
            stats.TotalReqs += reqList.Count;
            stats.UnresolvedComments += unresolvedCount;

            DocumentWorkItemDto ToWorkItem(User actor, string? note = null) => new()
            {
                DocumentId = node.Id,
                ProjectId = pInfo.Project.Id,
                ProjectKey = pInfo.Project.Key,
                ProjectName = pInfo.Project.Name,
                Code = node.Code,
                Title = node.Title,
                DocType = node.DocType ?? DocumentTypes.Other,
                VersionId = cur.Id,
                VersionLabel = cur.Label,
                Status = cur.Status,
                ReqCount = reqList.Count,
                UnresolvedCommentCount = unresolvedCount,
                UpdatedAt = FormatUtc(cur.UpdatedAt),
                Actor = MapUserRef(actor),
                Note = note
            };

            if (cur.Status == DocumentVersionStatuses.InReview && pInfo.CanApprove)
            {
                pendingApprovals.Add(ToWorkItem(cur.SubmittedBy ?? cur.UpdatedBy));
            }

            bool isMyDoc = cur.AuthorId == myId || cur.UpdatedById == myId || cur.SubmittedById == myId;
            if (cur.Status == DocumentVersionStatuses.ChangesRequested && (isMyDoc || pInfo.CanEdit))
            {
                changesRequested.Add(ToWorkItem(cur.ReviewedBy ?? cur.UpdatedBy, cur.ReviewNote));
            }

            if (cur.Status == DocumentVersionStatuses.Draft && (isMyDoc || pInfo.CanEdit))
            {
                myDrafts.Add(ToWorkItem(cur.UpdatedBy));
            }

            // Stale requirements: compare previous baseline or baseVersion vs current baseline/approved version
            if (node.BaselineVersionId.HasValue)
            {
                var baselineVer = cur;
                var prevVerId = baselineVer.BaseVersionId;
                if (prevVerId.HasValue && prevVerId.Value != baselineVer.Id)
                {
                    var prevReqs = reqsByVer.GetValueOrDefault(prevVerId.Value) ?? new List<RequirementItem>();
                    var prevByUid = prevReqs.ToDictionary(r => r.ReqUid, StringComparer.OrdinalIgnoreCase);
                    var currReqs = reqsByVer.GetValueOrDefault(baselineVer.Id) ?? new List<RequirementItem>();

                    var confirmedActs = confirmedFreshByDoc.GetValueOrDefault(node.Id) ?? new List<DocumentActivity>();

                    foreach (var r in currReqs)
                    {
                        bool isConfirmed = confirmedActs.Any(a =>
                            a.CreatedAt >= baselineVer.UpdatedAt &&
                            a.Note != null &&
                            a.Note.Contains(r.ReqUid, StringComparison.OrdinalIgnoreCase));

                        if (isConfirmed) continue;

                        if (!prevByUid.TryGetValue(r.ReqUid, out var before))
                        {
                            stats.StaleReqs++;
                            staleRequirements.Add(new StaleRequirementItemDto
                            {
                                RequirementId = r.Id,
                                ReqUid = r.ReqUid,
                                ReqTitle = r.Title,
                                AnchorId = r.AnchorId,
                                DocumentId = node.Id,
                                DocCode = node.Code,
                                DocTitle = node.Title,
                                VersionId = baselineVer.Id,
                                VersionLabel = baselineVer.Label,
                                ProjectId = pInfo.Project.Id,
                                ProjectKey = pInfo.Project.Key,
                                ProjectName = pInfo.Project.Name,
                                ChangeKind = "ADDED",
                                BeforePreview = null,
                                AfterPreview = r.BodyPreview,
                                ChangedBy = MapUserRef(baselineVer.UpdatedBy),
                                ChangedAt = FormatUtc(baselineVer.UpdatedAt)
                            });
                        }
                        else if (!string.Equals(before.ContentHash, r.ContentHash, StringComparison.OrdinalIgnoreCase))
                        {
                            stats.StaleReqs++;
                            staleRequirements.Add(new StaleRequirementItemDto
                            {
                                RequirementId = r.Id,
                                ReqUid = r.ReqUid,
                                ReqTitle = r.Title,
                                AnchorId = r.AnchorId,
                                DocumentId = node.Id,
                                DocCode = node.Code,
                                DocTitle = node.Title,
                                VersionId = baselineVer.Id,
                                VersionLabel = baselineVer.Label,
                                ProjectId = pInfo.Project.Id,
                                ProjectKey = pInfo.Project.Key,
                                ProjectName = pInfo.Project.Name,
                                ChangeKind = "MODIFIED",
                                BeforePreview = before.BodyPreview,
                                AfterPreview = r.BodyPreview,
                                ChangedBy = MapUserRef(baselineVer.UpdatedBy),
                                ChangedAt = FormatUtc(baselineVer.UpdatedAt)
                            });
                        }
                    }
                }
            }
        }

        // Mentions & Comments
        var commentById = allComments.ToDictionary(c => c.Id);
        foreach (var c in allComments)
        {
            if (c.ResolvedAt != null) continue;
            if (!nodeMap.TryGetValue(c.DocumentId, out var node)) continue;
            var pInfo = projectMap[node.ProjectId];
            var cur = node.CurrentVersion!;

            bool isMentioned = c.Mentions.Any(m => m.UserId == myId);
            bool isReplyToMe = c.ParentId.HasValue &&
                               commentById.TryGetValue(c.ParentId.Value, out var parentComment) &&
                               parentComment.AuthorId == myId &&
                               c.AuthorId != myId;
            bool isOnMyDoc = (cur.AuthorId == myId || cur.UpdatedById == myId) && c.AuthorId != myId;

            if (!isMentioned && !isReplyToMe && !isOnMyDoc) continue;

            var kind = isMentioned ? "MENTION" : isReplyToMe ? "COMMENT_REPLY" : "COMMENT_OPEN";
            var summary = isMentioned
                ? $"{c.Author.FullName} nhắc bạn trong {node.Code ?? node.Title}"
                : isReplyToMe
                    ? $"{c.Author.FullName} trả lời bình luận của bạn"
                    : $"{c.Author.FullName} bình luận trong {node.Code ?? node.Title}";

            mentionsAndComments.Add(new DocumentWorkEventDto
            {
                Id = $"cmt-{c.Id}",
                Kind = kind,
                DocumentId = node.Id,
                ProjectId = pInfo.Project.Id,
                ProjectKey = pInfo.Project.Key,
                ProjectName = pInfo.Project.Name,
                DocCode = node.Code,
                DocTitle = node.Title,
                VersionId = c.VersionId,
                VersionLabel = cur.Label,
                AnchorId = c.AnchorId,
                CommentId = c.Id,
                Actor = MapUserRef(c.Author),
                Summary = summary,
                Excerpt = c.Body,
                CreatedAt = FormatUtc(c.CreatedAt)
            });
        }

        // Recent Events
        foreach (var a in allActivities.Take(25))
        {
            if (!nodeMap.TryGetValue(a.DocumentId, out var node)) continue;
            var pInfo = projectMap[node.ProjectId];

            var summary = a.Action switch
            {
                DocumentActivityActions.Approved => $"Phê duyệt baseline v{a.Version?.Label ?? ""}",
                DocumentActivityActions.Submitted => $"Gửi duyệt v{a.Version?.Label ?? ""}",
                DocumentActivityActions.ChangesRequested => $"Yêu cầu chỉnh sửa v{a.Version?.Label ?? ""}",
                DocumentActivityActions.RevisionCreated => $"Tạo bản nháp mới v{a.Version?.Label ?? ""}",
                DocumentActivityActions.ContentSaved => $"Cập nhật nội dung v{a.Version?.Label ?? ""}",
                DocumentActivityActions.Created => "Khởi tạo tài liệu",
                DocumentActivityActions.GeneratedFromNotes => "Sinh tài liệu từ ghi chú",
                _ => a.Action
            };

            recentEvents.Add(new DocumentWorkEventDto
            {
                Id = $"act-{a.Id}",
                Kind = a.Action,
                DocumentId = node.Id,
                ProjectId = pInfo.Project.Id,
                ProjectKey = pInfo.Project.Key,
                ProjectName = pInfo.Project.Name,
                DocCode = node.Code,
                DocTitle = node.Title,
                VersionId = a.VersionId,
                VersionLabel = a.Version?.Label,
                Actor = MapUserRef(a.Actor),
                Summary = summary,
                Excerpt = a.Note,
                CreatedAt = FormatUtc(a.CreatedAt)
            });
        }

        return ServiceResult<MyDocumentWorkDto>.Success(new MyDocumentWorkDto
        {
            PendingApprovals = pendingApprovals.OrderByDescending(x => x.UpdatedAt).ToList(),
            ChangesRequested = changesRequested.OrderByDescending(x => x.UpdatedAt).ToList(),
            MyDrafts = myDrafts.OrderByDescending(x => x.UpdatedAt).ToList(),
            StaleRequirements = staleRequirements.OrderByDescending(x => x.ChangedAt).ToList(),
            MentionsAndComments = mentionsAndComments.OrderByDescending(x => x.CreatedAt).ToList(),
            RecentEvents = recentEvents,
            ProjectStats = projectStatsMap.Values.ToList()
        });
    }

    public async Task<ServiceResult<ItemsDto<WorkItemDto>>> GetMyWorkItemsAsync(
        long? projectId,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserIdOrNull is null)
            return ServiceResult<ItemsDto<WorkItemDto>>.Unauthorized();

        var myId = _currentUser.UserId;

        var docWorkRes = await GetMyDocumentWorkAsync(projectId, ct);
        if (!docWorkRes.IsSuccess) return docWorkRes.Cast<ItemsDto<WorkItemDto>>();
        var docWork = docWorkRes.Data!;

        var accessibleProjects = await GetAccessibleProjectsAsync(myId, projectId, ct);
        var projectMap = accessibleProjects.ToDictionary(p => p.Project.Id);
        var projectIds = projectMap.Keys.ToList();

        var items = new List<WorkItemDto>();

        // 1. Document work items (matching docWorkRules.ts)
        foreach (var d in docWork.PendingApprovals)
        {
            items.Add(new WorkItemDto
            {
                Id = $"doc-approve-{d.DocumentId}-{d.VersionId}",
                Source = "DOCUMENT",
                Kind = "DOC_APPROVE",
                Priority = "URGENT",
                RoleHint = "PM",
                ProjectId = d.ProjectId,
                ProjectKey = d.ProjectKey,
                ProjectName = d.ProjectName,
                Title = $"{d.Code ?? "DOC"} · {d.Title}",
                Subtitle = $"{d.Actor.FullName} gửi duyệt v{d.VersionLabel} · {d.ReqCount} yêu cầu",
                Badge = $"v{d.VersionLabel} · Chờ duyệt",
                CtaLabel = "Xem & duyệt",
                Href = $"/projects/{d.ProjectId}/documents/{d.DocumentId}?v={d.VersionId}",
                Actor = d.Actor,
                UpdatedAt = d.UpdatedAt,
                DocumentId = d.DocumentId,
                VersionId = d.VersionId
            });
        }

        foreach (var d in docWork.ChangesRequested)
        {
            items.Add(new WorkItemDto
            {
                Id = $"doc-fix-{d.DocumentId}-{d.VersionId}",
                Source = "DOCUMENT",
                Kind = "DOC_FIX_REQUESTED",
                Priority = "URGENT",
                RoleHint = "BA",
                ProjectId = d.ProjectId,
                ProjectKey = d.ProjectKey,
                ProjectName = d.ProjectName,
                Title = $"{d.Code ?? "DOC"} · {d.Title}",
                Subtitle = d.Note != null ? $"“{d.Note}” — {d.Actor.FullName}" : $"{d.Actor.FullName} yêu cầu chỉnh sửa v{d.VersionLabel}",
                Badge = $"v{d.VersionLabel} · Cần chỉnh sửa",
                CtaLabel = "Mở sửa",
                Href = $"/projects/{d.ProjectId}/documents/{d.DocumentId}?v={d.VersionId}",
                Actor = d.Actor,
                UpdatedAt = d.UpdatedAt,
                DocumentId = d.DocumentId,
                VersionId = d.VersionId
            });
        }

        foreach (var m in docWork.MentionsAndComments)
        {
            items.Add(new WorkItemDto
            {
                Id = $"doc-mention-{m.Id}",
                Source = "DOCUMENT",
                Kind = m.Kind == "MENTION" ? "DOC_MENTION" : "DOC_COMMENT",
                Priority = m.Kind == "MENTION" ? "HIGH" : "NORMAL",
                RoleHint = "ALL",
                ProjectId = m.ProjectId,
                ProjectKey = m.ProjectKey,
                ProjectName = m.ProjectName,
                Title = m.Summary,
                Subtitle = m.Excerpt ?? $"{m.DocCode ?? "DOC"} · {m.DocTitle}",
                Badge = m.Kind == "MENTION" ? "@nhắc tới bạn" : "Bình luận mở",
                CtaLabel = "Trả lời",
                Href = $"/projects/{m.ProjectId}/documents/{m.DocumentId}" +
                       (m.VersionId.HasValue ? $"?v={m.VersionId.Value}" : "") +
                       (!string.IsNullOrEmpty(m.AnchorId) ? $"#{m.AnchorId}" : ""),
                Actor = m.Actor,
                UpdatedAt = m.CreatedAt,
                DocumentId = m.DocumentId,
                VersionId = m.VersionId,
                AnchorId = m.AnchorId
            });
        }

        foreach (var r in docWork.StaleRequirements)
        {
            items.Add(new WorkItemDto
            {
                Id = $"req-stale-{r.DocumentId}-{r.ReqUid}-{r.VersionId}",
                Source = "DOCUMENT",
                Kind = "REQ_STALE",
                Priority = "HIGH",
                RoleHint = "QA",
                ProjectId = r.ProjectId,
                ProjectKey = r.ProjectKey,
                ProjectName = r.ProjectName,
                Title = $"{r.ReqUid} — {r.ReqTitle}",
                Subtitle = $"{r.DocCode ?? r.DocTitle} v{r.VersionLabel} · {(r.ChangeKind == "ADDED" ? "Yêu cầu mới thêm" : "Nội dung vừa cập nhật")} bởi {r.ChangedBy.FullName}",
                Badge = r.ChangeKind == "ADDED" ? "REQ mới" : "REQ vừa đổi",
                CtaLabel = "Xem thay đổi",
                Href = $"/projects/{r.ProjectId}/documents/{r.DocumentId}?v={r.VersionId}#{r.AnchorId}",
                Actor = r.ChangedBy,
                UpdatedAt = r.ChangedAt,
                DocumentId = r.DocumentId,
                VersionId = r.VersionId,
                AnchorId = r.AnchorId,
                ReqUid = r.ReqUid
            });
        }

        foreach (var d in docWork.MyDrafts)
        {
            items.Add(new WorkItemDto
            {
                Id = $"doc-draft-{d.DocumentId}-{d.VersionId}",
                Source = "DOCUMENT",
                Kind = "DOC_DRAFT",
                Priority = "NORMAL",
                RoleHint = "BA",
                ProjectId = d.ProjectId,
                ProjectKey = d.ProjectKey,
                ProjectName = d.ProjectName,
                Title = $"{d.Code ?? "DOC"} · {d.Title}",
                Subtitle = $"Bản nháp v{d.VersionLabel} · {d.ReqCount} yêu cầu" +
                           (d.UnresolvedCommentCount > 0 ? $" · {d.UnresolvedCommentCount} nhận xét mở" : ""),
                Badge = $"v{d.VersionLabel} · Bản nháp",
                CtaLabel = "Tiếp tục viết",
                Href = $"/projects/{d.ProjectId}/documents/{d.DocumentId}?v={d.VersionId}",
                Actor = d.Actor,
                UpdatedAt = d.UpdatedAt,
                DocumentId = d.DocumentId,
                VersionId = d.VersionId
            });
        }

        // 2. Issue & Test Run work items (matching issueWorkRules.ts)
        if (projectIds.Count > 0)
        {
            var myIssues = await _db.Issues
                .AsNoTracking()
                .Include(i => i.Status)
                .Include(i => i.Assignee)
                .Include(i => i.RequirementLinks)
                .Where(i => projectIds.Contains(i.ProjectId) &&
                            !i.IsDeleted &&
                            i.AssigneeId == myId &&
                            i.Status.Category != "DONE")
                .ToListAsync(ct);

            var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

            foreach (var iss in myIssues)
            {
                var pInfo = projectMap[iss.ProjectId];
                var isOverdue = iss.DueDate.HasValue && iss.DueDate.Value <= today.AddDays(1);
                var roleHint = iss.Type == IssueTypes.Design ? "DESIGNER" : "DEV";

                if (iss.ReqChanged)
                {
                    var changedReqUids = iss.RequirementLinks
                        .Where(rl => rl.ChangedVersionLabel != null)
                        .Select(rl => rl.ReqUid)
                        .ToList();

                    items.Add(new WorkItemDto
                    {
                        Id = $"issue-req-changed-{iss.Id}",
                        Source = "ISSUE",
                        Kind = "ISSUE_REQ_CHANGED",
                        Priority = "HIGH",
                        RoleHint = roleHint,
                        ProjectId = iss.ProjectId,
                        ProjectKey = pInfo.Project.Key,
                        ProjectName = pInfo.Project.Name,
                        Title = $"{iss.Key} · {iss.Title}",
                        Subtitle = $"Yêu cầu {(changedReqUids.Count > 0 ? string.Join(", ", changedReqUids) : "liên kết")} vừa cập nhật — kiểm tra lại phạm vi trước khi làm tiếp",
                        Badge = "REQ vừa đổi",
                        CtaLabel = "Xem & xác nhận",
                        Href = $"/projects/{iss.ProjectId}/kanban?issue={iss.Key}",
                        Actor = iss.Assignee != null ? MapUserRef(iss.Assignee) : null,
                        UpdatedAt = FormatUtc(iss.UpdatedAt),
                        IssueId = iss.Id,
                        IssueKey = iss.Key
                    });
                }

                var priority = (iss.Priority == IssuePriorities.Highest || isOverdue)
                    ? "URGENT"
                    : iss.Status.Category == "IN_PROGRESS"
                        ? "HIGH"
                        : "NORMAL";

                items.Add(new WorkItemDto
                {
                    Id = $"issue-work-{iss.Id}",
                    Source = "ISSUE",
                    Kind = iss.Status.Category == "IN_PROGRESS" ? "ISSUE_IN_PROGRESS" : "ISSUE_TODO",
                    Priority = priority,
                    RoleHint = roleHint,
                    ProjectId = iss.ProjectId,
                    ProjectKey = pInfo.Project.Key,
                    ProjectName = pInfo.Project.Name,
                    Title = $"{iss.Key} · {iss.Title}",
                    Subtitle = $"{iss.Status.Name}" +
                               (iss.Points.HasValue ? $" · {iss.Points.Value}pt" : "") +
                               (iss.DueDate.HasValue ? $" · hạn {iss.DueDate.Value:yyyy-MM-dd}" : ""),
                    Badge = isOverdue ? "Sắp/quá hạn" : iss.Status.Name,
                    CtaLabel = iss.Status.Category == "IN_PROGRESS" ? "Tiếp tục làm" : "Bắt đầu",
                    Href = $"/projects/{iss.ProjectId}/kanban?issue={iss.Key}",
                    Actor = iss.Assignee != null ? MapUserRef(iss.Assignee) : null,
                    UpdatedAt = FormatUtc(iss.UpdatedAt),
                    IssueId = iss.Id,
                    IssueKey = iss.Key
                });
            }

            // Test Runs in OPEN cycles
            var openRuns = await _db.TestRuns
                .AsNoTracking()
                .Include(r => r.Cycle)
                .Include(r => r.TestCase)
                .Include(r => r.Assignee)
                .Where(r => projectIds.Contains(r.Cycle.ProjectId) &&
                            r.Cycle.Status == TestCycleStatuses.Open &&
                            (r.AssigneeId == myId || r.AssigneeId == null) &&
                            (r.Result == TestRunResults.NotRun || r.NeedsRerun))
                .ToListAsync(ct);

            foreach (var r in openRuns)
            {
                var pInfo = projectMap[r.Cycle.ProjectId];
                if (!pInfo.CanExecuteTests && r.AssigneeId != myId) continue;

                items.Add(new WorkItemDto
                {
                    Id = $"test-run-{r.Id}",
                    Source = "TEST",
                    Kind = r.NeedsRerun ? "TEST_RERUN" : "TEST_PENDING",
                    Priority = r.NeedsRerun ? "HIGH" : "NORMAL",
                    RoleHint = "QA",
                    ProjectId = pInfo.Project.Id,
                    ProjectKey = pInfo.Project.Key,
                    ProjectName = pInfo.Project.Name,
                    Title = $"{r.TestCase.Key} · {r.TestCase.Title}",
                    Subtitle = $"{r.Cycle.Name} · {(r.NeedsRerun ? "Cần chạy lại sau khi REQ đổi" : "Chưa chạy")}",
                    Badge = r.NeedsRerun ? "Chạy lại" : r.Cycle.Name,
                    CtaLabel = "Chạy test",
                    Href = $"/projects/{pInfo.Project.Id}/tests/cycles/{r.CycleId}?run={r.Id}",
                    Actor = r.Assignee != null ? MapUserRef(r.Assignee) : null,
                    UpdatedAt = r.ExecutedAt.HasValue ? FormatUtc(r.ExecutedAt.Value) : FormatUtc(r.Cycle.CreatedAt),
                    RunId = r.Id,
                    CycleId = r.CycleId
                });
            }
        }

        // Sort by Priority (URGENT > HIGH > NORMAL > LOW) then UpdatedAt DESC
        static int PriorityRank(string p) => p switch
        {
            "URGENT" => 0,
            "HIGH" => 1,
            "NORMAL" => 2,
            _ => 3
        };

        var sorted = items
            .OrderBy(i => PriorityRank(i.Priority))
            .ThenByDescending(i => i.UpdatedAt)
            .ToList();

        return ServiceResult<ItemsDto<WorkItemDto>>.Success(new ItemsDto<WorkItemDto>(sorted));
    }

    // --- Helpers ---

    private sealed record AccessibleProjectInfo(
        Project Project,
        bool CanEdit,
        bool CanApprove,
        bool CanExecuteTests
    );

    private async Task<List<AccessibleProjectInfo>> GetAccessibleProjectsAsync(
        long userId,
        long? filterProjectId,
        CancellationToken ct)
    {
        var memberProjectIds = await _db.ProjectMembers
            .AsNoTracking()
            .Where(pm => pm.UserId == userId)
            .Select(pm => pm.ProjectId)
            .ToListAsync(ct);

        var query = _db.Projects
            .AsNoTracking()
            .Where(p => p.IsActive && memberProjectIds.Contains(p.Id));

        if (filterProjectId.HasValue)
        {
            query = _db.Projects
                .AsNoTracking()
                .Where(p => p.Id == filterProjectId.Value && p.IsActive);
        }

        var projects = await query.OrderBy(p => p.Id).ToListAsync(ct);
        var result = new List<AccessibleProjectInfo>();

        foreach (var p in projects)
        {
            var perms = await _permissions.GetProjectPermissionsAsync(userId, p.Id, ct);
            if (!perms.Contains(PermissionKeys.ProjectView, StringComparer.OrdinalIgnoreCase) &&
                !perms.Contains(PermissionKeys.DocumentView, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(new AccessibleProjectInfo(
                Project: p,
                CanEdit: perms.Contains(PermissionKeys.DocumentUpdate, StringComparer.OrdinalIgnoreCase),
                CanApprove: perms.Contains(PermissionKeys.DocumentApprove, StringComparer.OrdinalIgnoreCase),
                CanExecuteTests: perms.Contains(PermissionKeys.TestcaseExecute, StringComparer.OrdinalIgnoreCase)
            ));
        }

        return result;
    }

    private static UserRefDto MapUserRef(User u) => new()
    {
        UserId = u.Id,
        FullName = u.FullName,
        Email = u.Email,
        AvatarUrl = u.AvatarUrl
    };

    private static string FormatUtc(DateTime dt) =>
        DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
}
