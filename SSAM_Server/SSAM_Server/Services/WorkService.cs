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
        long? companyId = null,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserIdOrNull is null)
            return ServiceResult<MyDocumentWorkDto>.Unauthorized();

        var myId = _currentUser.UserId;

        var accessibleProjects = await GetAccessibleProjectsAsync(myId, projectId, companyId, ct);
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
        long? companyId = null,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserIdOrNull is null)
            return ServiceResult<ItemsDto<WorkItemDto>>.Unauthorized();

        var myId = _currentUser.UserId;

        var docWorkRes = await GetMyDocumentWorkAsync(projectId, companyId, ct);
        if (!docWorkRes.IsSuccess) return docWorkRes.Cast<ItemsDto<WorkItemDto>>();
        var docWork = docWorkRes.Data!;

        var accessibleProjects = await GetAccessibleProjectsAsync(myId, projectId, companyId, ct);
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
                Urgency = 82,
                RoleHint = "PM",
                ProjectId = d.ProjectId,
                ProjectKey = d.ProjectKey,
                ProjectName = d.ProjectName,
                Code = d.Code,
                VersionLabel = d.VersionLabel,
                Note = d.Note,
                Count = d.ReqCount,
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
                Urgency = 92,
                RoleHint = "BA",
                ProjectId = d.ProjectId,
                ProjectKey = d.ProjectKey,
                ProjectName = d.ProjectName,
                Code = d.Code,
                VersionLabel = d.VersionLabel,
                Note = d.Note,
                Count = d.ReqCount,
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
                Urgency = m.Kind == "MENTION" ? 72 : 65,
                RoleHint = "ALL",
                ProjectId = m.ProjectId,
                ProjectKey = m.ProjectKey,
                ProjectName = m.ProjectName,
                Code = m.DocCode,
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
                AnchorId = m.AnchorId,
                Comment = new WorkItemCommentDto
                {
                    CommentId = m.CommentId ?? 0,
                    Body = m.Excerpt ?? m.Summary,
                    AnchorId = m.AnchorId,
                    Author = m.Actor
                }
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
                Urgency = 75,
                RoleHint = "QA",
                ProjectId = r.ProjectId,
                ProjectKey = r.ProjectKey,
                ProjectName = r.ProjectName,
                Code = r.ReqUid,
                VersionLabel = r.VersionLabel,
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
                ReqUid = r.ReqUid,
                Requirement = new WorkItemRequirementDto
                {
                    RequirementId = r.RequirementId > 0 ? r.RequirementId : null,
                    ReqUid = r.ReqUid,
                    ReqKey = r.ReqKey,
                    Title = r.ReqTitle,
                    AnchorId = r.AnchorId,
                    ChangeKind = r.ChangeKind,
                    BeforePreview = r.BeforePreview,
                    AfterPreview = r.AfterPreview
                }
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
                Urgency = 42,
                RoleHint = "BA",
                ProjectId = d.ProjectId,
                ProjectKey = d.ProjectKey,
                ProjectName = d.ProjectName,
                Code = d.Code,
                VersionLabel = d.VersionLabel,
                Count = d.ReqCount,
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

                // 2. Issue & Test v2 work items (matching issueWorkRules.ts)
        if (projectIds.Count > 0)
        {
            var myIssues = await _db.Issues
                .AsNoTracking()
                .Include(i => i.Status)
                .Include(i => i.Assignee)
                .Include(i => i.Reporter)
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
                var dueDateStr = iss.DueDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

                if (iss.ReqChanged)
                {
                    var changedReqUids = iss.RequirementLinks
                        .Where(rl => rl.ChangedVersionLabel != null)
                        .Select(rl => rl.ReqUid)
                        .ToList();
                    if (changedReqUids.Count == 0)
                        changedReqUids = iss.RequirementLinks.Select(rl => rl.ReqUid).ToList();

                    items.Add(new WorkItemDto
                    {
                        Id = $"issue-req-changed-{iss.Id}",
                        Source = "ISSUE",
                        Kind = "ISSUE_REQ_CHANGED",
                        Priority = "HIGH",
                        Urgency = 84,
                        RoleHint = roleHint,
                        ProjectId = iss.ProjectId,
                        ProjectKey = pInfo.Project.Key,
                        ProjectName = pInfo.Project.Name,
                        Code = iss.Key,
                        Title = $"{iss.Key} · {iss.Title}",
                        Subtitle = $"Yêu cầu {(changedReqUids.Count > 0 ? string.Join(", ", changedReqUids) : "liên kết")} vừa cập nhật — kiểm tra lại phạm vi trước khi làm tiếp",
                        Note = $"Yêu cầu {(changedReqUids.Count > 0 ? string.Join(", ", changedReqUids) : "liên kết")} vừa cập nhật — kiểm tra lại phạm vi trước khi làm tiếp",
                        Count = changedReqUids.Count,
                        Badge = "REQ vừa đổi",
                        CtaLabel = "Xem & xác nhận",
                        Href = $"/projects/{iss.ProjectId}/kanban?issue={iss.Key}",
                        Actor = MapUserRef(iss.Reporter),
                        UpdatedAt = FormatUtc(iss.UpdatedAt),
                        IssueId = iss.Id,
                        IssueKey = iss.Key,
                        Issue = new WorkItemIssueDto
                        {
                            IssueId = iss.Id,
                            Key = iss.Key,
                            Title = iss.Title,
                            Type = iss.Type,
                            StatusName = iss.Status.Name,
                            StatusCategory = iss.Status.Category,
                            StatusColor = iss.Status.Color,
                            Priority = iss.Priority,
                            Points = iss.Points,
                            DueDate = dueDateStr,
                            ReqChanged = iss.ReqChanged,
                            ChangedReqUids = changedReqUids
                        }
                    });
                }

                var priority = (iss.Priority == IssuePriorities.Highest || isOverdue)
                    ? "URGENT"
                    : iss.Status.Category == "IN_PROGRESS"
                        ? "HIGH"
                        : "NORMAL";
                var baseUrgency = iss.Status.Category == "IN_PROGRESS" ? 76 : 62;
                var prioBoost = iss.Priority == IssuePriorities.Highest ? 12 : iss.Priority == IssuePriorities.High ? 6 : 0;
                var dueBoost = isOverdue ? 10 : 0;

                items.Add(new WorkItemDto
                {
                    Id = $"issue-work-{iss.Id}",
                    Source = "ISSUE",
                    Kind = iss.Status.Category == "IN_PROGRESS" ? "ISSUE_IN_PROGRESS" : "ISSUE_TODO",
                    Priority = priority,
                    Urgency = Math.Min(94, baseUrgency + prioBoost + dueBoost),
                    RoleHint = roleHint,
                    ProjectId = iss.ProjectId,
                    ProjectKey = pInfo.Project.Key,
                    ProjectName = pInfo.Project.Name,
                    Code = iss.Key,
                    Title = $"{iss.Key} · {iss.Title}",
                    Subtitle = $"{iss.Status.Name}" +
                               (iss.Points.HasValue ? $" · {iss.Points.Value}pt" : "") +
                               (dueDateStr != null ? $" · hạn {dueDateStr}" : ""),
                    Note = $"{iss.Status.Name}" +
                           (iss.Points.HasValue ? $" · {iss.Points.Value}pt" : "") +
                           (dueDateStr != null ? $" · hạn {dueDateStr}" : "") +
                           (isOverdue ? " (sắp/quá hạn)" : ""),
                    Badge = isOverdue ? "Sắp/quá hạn" : iss.Status.Name,
                    CtaLabel = iss.Status.Category == "IN_PROGRESS" ? "Tiếp tục làm" : "Bắt đầu",
                    Href = $"/projects/{iss.ProjectId}/kanban?issue={iss.Key}",
                    Actor = iss.Assignee != null ? MapUserRef(iss.Assignee) : MapUserRef(iss.Reporter),
                    UpdatedAt = FormatUtc(iss.UpdatedAt),
                    IssueId = iss.Id,
                    IssueKey = iss.Key,
                    Issue = new WorkItemIssueDto
                    {
                        IssueId = iss.Id,
                        Key = iss.Key,
                        Title = iss.Title,
                        Type = iss.Type,
                        StatusName = iss.Status.Name,
                        StatusCategory = iss.Status.Category,
                        StatusColor = iss.Status.Color,
                        Priority = iss.Priority,
                        Points = iss.Points,
                        DueDate = dueDateStr,
                        ReqChanged = iss.ReqChanged
                    }
                });
            }

            // Test v2 work items (TEST_RERUN, TEST_NOT_RUN, TEST_FAILED_MINE, REQ_WITHOUT_TEST)
            var openPlans = await _db.TestPlans
                .AsNoTracking()
                .Include(p => p.CreatedBy)
                .Include(p => p.Environments.Where(e => !e.IsDeleted))
                    .ThenInclude(e => e.Builds)
                .Include(p => p.Modules)
                    .ThenInclude(m => m.Assignments)
                        .ThenInclude(a => a.Assignee)
                .Where(p => projectIds.Contains(p.ProjectId) && p.Status == TestPlanStatuses.Open)
                .ToListAsync(ct);

            var openPlanIds = openPlans.Select(p => p.Id).ToList();
            var activeCases = openPlanIds.Count == 0
                ? new List<TestCase>()
                : await _db.TestCases
                    .AsNoTracking()
                    .Include(tc => tc.CreatedBy)
                    .Include(tc => tc.Results)
                    .Include(tc => tc.RetestFlags)
                        .ThenInclude(f => f.BugIssue)
                    .Include(tc => tc.RequirementLinks)
                    .Where(tc => openPlanIds.Contains(tc.PlanId) && tc.Status == TestCaseStatuses.Active)
                    .OrderBy(tc => tc.Position)
                    .ThenBy(tc => tc.Id)
                    .ToListAsync(ct);

            var planMap = openPlans.ToDictionary(p => p.Id);

            foreach (var tc in activeCases)
            {
                if (!planMap.TryGetValue(tc.PlanId, out var plan)) continue;
                var pInfo = projectMap[tc.ProjectId];
                var mod = plan.Modules.FirstOrDefault(m => m.Id == tc.ModuleId);
                var activeEnvs = plan.Environments.Where(e => !e.IsDeleted).OrderBy(e => e.Position).ThenBy(e => e.Id).ToList();

                foreach (var env in activeEnvs)
                {
                    var assignment = mod?.Assignments.FirstOrDefault(a => a.EnvId == env.Id);
                    var isMyCell = assignment?.AssigneeId == myId || (assignment == null && pInfo.CanExecuteTests);
                    if (!isMyCell) continue;

                    var flag = tc.RetestFlags.FirstOrDefault(f => f.EnvId == env.Id);
                    var envResults = tc.Results
                        .Where(r => r.EnvId == env.Id)
                        .OrderByDescending(r => r.At)
                        .ThenByDescending(r => r.Id)
                        .ToList();
                    var latestRec = envResults.FirstOrDefault();
                    var status = flag != null ? "RETEST" : (latestRec?.Result ?? TestResultValues.Untest);
                    if (status is not ("RETEST" or TestResultValues.Untest)) continue;

                    var needsRerun = status == "RETEST";
                    var currentBuild = env.Builds.OrderByDescending(b => b.DeployedAt).ThenByDescending(b => b.Id).FirstOrDefault()?.Version ?? "v1.0.0";
                    var cycleLabel = $"{plan.Name} · {env.Name} ({currentBuild})";
                    var reasonNote = flag != null
                        ? (flag.Reason switch
                        {
                            TestRetestReasons.BugFixed => $"Bug {flag.BugIssue?.Key ?? ""} vừa sửa ở bản {flag.Build ?? currentBuild} — cần xác nhận lại",
                            TestRetestReasons.ReqChanged => $"{flag.ReqKey ?? "Yêu cầu"} vừa đổi — cần chạy lại kịch bản",
                            _ => $"Yêu cầu chạy hồi quy trên {env.Name}"
                        })
                        : $"{cycleLabel} · Chưa chạy";

                    var lastRes = latestRec == null || latestRec.Result == TestResultValues.Untest
                        ? "NOT_RUN"
                        : (latestRec.Result == TestResultValues.Na ? "BLOCKED" : latestRec.Result);

                    var runSyntheticId = tc.Id * 100 + env.Id;
                    var actorRef = assignment != null ? MapUserRef(assignment.Assignee) : MapUserRef(tc.CreatedBy);
                    var atStr = flag != null ? FormatUtc(flag.At) : (latestRec != null ? FormatUtc(latestRec.At) : FormatUtc(tc.UpdatedAt));

                    items.Add(new WorkItemDto
                    {
                        Id = $"test-run-{tc.Id}-{env.Id}",
                        Source = "TEST",
                        Kind = needsRerun ? "TEST_RERUN" : "TEST_NOT_RUN",
                        Priority = needsRerun ? "HIGH" : "NORMAL",
                        Urgency = needsRerun ? 86 : 66,
                        RoleHint = "QA",
                        ProjectId = pInfo.Project.Id,
                        ProjectKey = pInfo.Project.Key,
                        ProjectName = pInfo.Project.Name,
                        Code = tc.Code,
                        Title = $"{tc.Code} · {tc.Title}",
                        Subtitle = reasonNote,
                        Note = reasonNote,
                        Badge = needsRerun ? "Chạy lại" : cycleLabel,
                        CtaLabel = "Chạy test",
                        Href = $"/projects/{pInfo.Project.Id}/tests?plan={plan.Id}&env={env.Id}&case={tc.Id}",
                        Actor = actorRef,
                        UpdatedAt = atStr,
                        RunId = runSyntheticId,
                        CycleId = plan.Id,
                        TestRun = new WorkItemTestRunDto
                        {
                            RunId = tc.Id,
                            CycleId = plan.Id,
                            TestCaseId = tc.Id,
                            TestCaseKey = mod != null ? $"{mod.ShortCode}·{tc.Code}" : tc.Code,
                            Title = tc.Title,
                            CycleName = $"{plan.Name} · {env.Name}",
                            LastResult = lastRes,
                            NeedsRerun = needsRerun,
                            PlanId = plan.Id,
                            EnvId = env.Id,
                            ModuleId = tc.ModuleId
                        }
                    });
                }
            }

            // TEST_FAILED_MINE: my open issues that have a linked test case currently FAIL
            var myIssueIds = myIssues.Select(i => i.Id).ToList();
            if (myIssueIds.Count > 0)
            {
                var linkedFailCases = activeCases
                    .Where(tc => tc.BugIssueId.HasValue && myIssueIds.Contains(tc.BugIssueId.Value))
                    .ToList();

                foreach (var iss in myIssues)
                {
                    var pInfo = projectMap[iss.ProjectId];
                    var dueDateStr = iss.DueDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                    TestCase? failCase = null;
                    long? failEnvId = null;

                    foreach (var tc in linkedFailCases.Where(c => c.BugIssueId == iss.Id))
                    {
                        if (!planMap.TryGetValue(tc.PlanId, out var plan)) continue;
                        foreach (var env in plan.Environments.Where(e => !e.IsDeleted))
                        {
                            var hasFlag = tc.RetestFlags.Any(f => f.EnvId == env.Id);
                            var latestRec = tc.Results
                                .Where(r => r.EnvId == env.Id)
                                .OrderByDescending(r => r.At)
                                .ThenByDescending(r => r.Id)
                                .FirstOrDefault();
                            var st = hasFlag ? "RETEST" : (latestRec?.Result ?? TestResultValues.Untest);
                            if (st == TestResultValues.Fail)
                            {
                                failCase = tc;
                                failEnvId = env.Id;
                                break;
                            }
                        }
                        if (failCase != null) break;
                    }

                    if (failCase != null && planMap.TryGetValue(failCase.PlanId, out var failPlan))
                    {
                        items.Add(new WorkItemDto
                        {
                            Id = $"test-fail-mine-{iss.Id}-{failCase.Id}",
                            Source = "ISSUE",
                            Kind = "TEST_FAILED_MINE",
                            Priority = "URGENT",
                            Urgency = 88,
                            RoleHint = "DEV",
                            ProjectId = iss.ProjectId,
                            ProjectKey = pInfo.Project.Key,
                            ProjectName = pInfo.Project.Name,
                            Code = iss.Key,
                            Title = $"{iss.Key} · {iss.Title}",
                            Subtitle = $"Kịch bản {failCase.Code} ({failCase.Title}) báo FAIL — cần kiểm tra & sửa lỗi",
                            Note = $"Kịch bản {failCase.Code} ({failCase.Title}) báo FAIL — cần kiểm tra & sửa lỗi",
                            Badge = "Test FAIL",
                            CtaLabel = "Xem lỗi",
                            Href = $"/projects/{iss.ProjectId}/kanban?issue={iss.Key}",
                            Actor = MapUserRef(failCase.CreatedBy),
                            UpdatedAt = FormatUtc(failCase.UpdatedAt),
                            IssueId = iss.Id,
                            IssueKey = iss.Key,
                            Issue = new WorkItemIssueDto
                            {
                                IssueId = iss.Id,
                                Key = iss.Key,
                                Title = iss.Title,
                                Type = iss.Type,
                                StatusName = iss.Status.Name,
                                StatusCategory = iss.Status.Category,
                                StatusColor = iss.Status.Color,
                                Priority = iss.Priority,
                                Points = iss.Points,
                                DueDate = dueDateStr,
                                ReqChanged = iss.ReqChanged
                            },
                            TestRun = new WorkItemTestRunDto
                            {
                                RunId = failCase.Id,
                                CycleId = failCase.PlanId,
                                TestCaseId = failCase.Id,
                                TestCaseKey = failCase.Code,
                                Title = failCase.Title,
                                CycleName = failPlan.Name,
                                LastResult = "FAIL",
                                NeedsRerun = false,
                                PlanId = failCase.PlanId,
                                EnvId = failEnvId,
                                ModuleId = failCase.ModuleId
                            }
                        });
                    }
                }
            }

            // REQ_WITHOUT_TEST: for projects where user has testcase.manage
            var manageProjectIds = accessibleProjects
                .Where(p => p.CanManageTests)
                .Select(p => p.Project.Id)
                .ToList();

            if (manageProjectIds.Count > 0)
            {
                var baselinedDocs = await _db.DocumentNodes
                    .AsNoTracking()
                    .Include(d => d.CurrentVersion)
                        .ThenInclude(v => v!.UpdatedBy)
                    .Where(d => manageProjectIds.Contains(d.ProjectId) &&
                                !d.IsDeleted &&
                                d.Kind == DocumentNodeKinds.Page &&
                                d.BaselineVersionId != null)
                    .ToListAsync(ct);

                var baseVerIds = baselinedDocs.Select(d => d.BaselineVersionId!.Value).Distinct().ToList();
                var baseReqs = baseVerIds.Count == 0
                    ? new List<RequirementItem>()
                    : await _db.RequirementItems
                        .AsNoTracking()
                        .Where(r => baseVerIds.Contains(r.VersionId))
                        .OrderBy(r => r.SortOrder)
                        .ToListAsync(ct);

                var coveredLinks = await _db.TestCaseRequirementLinks
                    .AsNoTracking()
                    .Where(l => manageProjectIds.Contains(l.TestCase.ProjectId) && l.TestCase.Status == TestCaseStatuses.Active)
                    .Select(l => new { l.DocumentId, l.ReqUid })
                    .ToListAsync(ct);
                var coveredSet = coveredLinks
                    .Select(l => (l.DocumentId, l.ReqUid.ToUpperInvariant()))
                    .ToHashSet();

                var docById = baselinedDocs.ToDictionary(d => d.Id);
                foreach (var req in baseReqs)
                {
                    if (coveredSet.Contains((req.DocumentId, req.ReqUid.ToUpperInvariant())))
                        continue;
                    if (!docById.TryGetValue(req.DocumentId, out var doc))
                        continue;

                    var pInfo = projectMap[doc.ProjectId];
                    var ver = doc.CurrentVersion;
                    if (ver == null) continue;

                    items.Add(new WorkItemDto
                    {
                        Id = $"req-no-test-{doc.Id}-{req.ReqUid}",
                        Source = "DOCUMENT",
                        Kind = "REQ_WITHOUT_TEST",
                        Priority = "NORMAL",
                        Urgency = 58,
                        RoleHint = "QA",
                        ProjectId = pInfo.Project.Id,
                        ProjectKey = pInfo.Project.Key,
                        ProjectName = pInfo.Project.Name,
                        Code = req.ReqUid,
                        Title = $"{req.ReqUid} · {req.Title}",
                        Subtitle = $"Chưa có kịch bản kiểm thử nào gắn với {req.ReqUid} trong {doc.Code ?? doc.Title}",
                        Note = $"Chưa có kịch bản kiểm thử nào gắn với {req.ReqUid} trong {doc.Code ?? doc.Title}",
                        Badge = "Chưa có test",
                        CtaLabel = "Viết test",
                        Href = $"/projects/{pInfo.Project.Id}/tests",
                        Actor = MapUserRef(ver.UpdatedBy),
                        UpdatedAt = FormatUtc(ver.UpdatedAt),
                        DocumentId = doc.Id,
                        VersionId = ver.Id,
                        VersionLabel = ver.Label,
                        AnchorId = req.AnchorId,
                        ReqUid = req.ReqUid,
                        Requirement = new WorkItemRequirementDto
                        {
                            ReqUid = req.ReqUid,
                            ReqKey = $"{(doc.Code ?? $"DOC-{doc.Id}")}:{req.ReqUid}",
                            Title = req.Title,
                            AnchorId = req.AnchorId,
                            ChangeKind = "ADDED",
                            AfterPreview = req.BodyPreview
                        }
                    });
                }
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
        bool CanExecuteTests,
        bool CanManageTests
    );

    private async Task<List<AccessibleProjectInfo>> GetAccessibleProjectsAsync(
        long userId,
        long? filterProjectId,
        long? filterCompanyId,
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

        if (filterCompanyId.HasValue)
        {
            query = query.Where(p => p.Workspace.CompanyId == filterCompanyId.Value);
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
                CanExecuteTests: perms.Contains(PermissionKeys.TestcaseExecute, StringComparer.OrdinalIgnoreCase),
                CanManageTests: perms.Contains(PermissionKeys.TestCaseManage, StringComparer.OrdinalIgnoreCase)
            ));
        }

        return result;
    }
}
