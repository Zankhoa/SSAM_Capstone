using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SSAM_Server.Common;
using SSAM_Server.Data;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.DTOs.Testing;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.Services;

public class TestingService : ITestingService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionService _permissions;
    private readonly TimeProvider _clock;

    public TestingService(
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

    public async Task<ServiceResult<ItemsDto<TestTreeNodeDto>>> GetTestTreeAsync(
        long projectId,
        CancellationToken ct = default)
    {
        var auth = await EnsureProjectPermissionAsync(projectId, PermissionKeys.TestcaseView, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<TestTreeNodeDto>>();

        var folders = await _db.TestFolders
            .AsNoTracking()
            .Where(f => f.ProjectId == projectId)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Id)
            .ToListAsync(ct);

        var testCases = await _db.TestCases
            .AsNoTracking()
            .Include(tc => tc.RequirementLinks)
            .Where(tc => tc.ProjectId == projectId && !tc.IsDeleted)
            .ToListAsync(ct);

        var docs = await _db.DocumentNodes
            .AsNoTracking()
            .Where(d => d.ProjectId == projectId && !d.IsDeleted && d.Kind == DocumentNodeKinds.Page)
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Id)
            .ToListAsync(ct);

        var targetVerIds = docs
            .Select(d => d.BaselineVersionId ?? d.CurrentVersionId)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();

        var reqItems = targetVerIds.Count == 0
            ? new List<RequirementItem>()
            : await _db.RequirementItems
                .AsNoTracking()
                .Where(r => targetVerIds.Contains(r.VersionId))
                .OrderBy(r => r.SortOrder)
                .ToListAsync(ct);

        var nodes = new List<TestTreeNodeDto>();

        // 1. Folders
        foreach (var f in folders)
        {
            var inFolder = testCases.Where(tc => tc.FolderId == f.Id).ToList();
            nodes.Add(new TestTreeNodeDto
            {
                NodeId = $"folder-{f.Id}",
                Kind = "FOLDER",
                Label = f.Name,
                ParentId = f.ParentId.HasValue ? $"folder-{f.ParentId.Value}" : null,
                FolderId = f.Id,
                Count = inFolder.Count,
                PassCount = inFolder.Count(tc => tc.LastResult == TestRunResults.Pass),
                FailCount = inFolder.Count(tc => tc.LastResult == TestRunResults.Fail),
                NeedsReviewCount = inFolder.Count(tc => tc.NeedsReview)
            });
        }

        // 2. Documents & Requirements
        var docByVer = docs
            .Where(d => (d.BaselineVersionId ?? d.CurrentVersionId).HasValue)
            .ToDictionary(d => (d.BaselineVersionId ?? d.CurrentVersionId)!.Value);

        var reqsByVer = reqItems.GroupBy(r => r.VersionId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var kvp in docByVer)
        {
            var verId = kvp.Key;
            var doc = kvp.Value;
            if (!reqsByVer.TryGetValue(verId, out var docReqs) || docReqs.Count == 0) continue;

            var docMatchingCases = testCases
                .Where(tc => tc.RequirementLinks.Any(rl => rl.DocumentId == doc.Id))
                .ToList();

            var docNodeId = $"doc-{doc.Id}";
            nodes.Add(new TestTreeNodeDto
            {
                NodeId = docNodeId,
                Kind = "DOCUMENT",
                Label = $"{doc.Code} · {doc.Title}",
                ParentId = null,
                DocumentId = doc.Id,
                DocCode = doc.Code,
                Count = docMatchingCases.Count,
                PassCount = docMatchingCases.Count(tc => tc.LastResult == TestRunResults.Pass),
                FailCount = docMatchingCases.Count(tc => tc.LastResult == TestRunResults.Fail),
                NeedsReviewCount = docMatchingCases.Count(tc => tc.NeedsReview)
            });

            foreach (var req in docReqs)
            {
                var matchingCases = testCases
                    .Where(tc => tc.RequirementLinks.Any(rl => rl.DocumentId == doc.Id && rl.ReqUid.Equals(req.ReqUid, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                nodes.Add(new TestTreeNodeDto
                {
                    NodeId = $"req-{doc.Id}-{req.ReqUid}",
                    Kind = "REQUIREMENT",
                    Label = $"{req.ReqUid} · {req.Title}",
                    ParentId = docNodeId,
                    DocumentId = doc.Id,
                    DocCode = doc.Code,
                    ReqUid = req.ReqUid,
                    Requirement = new TestCaseRequirementRefDto
                    {
                        DocumentId = doc.Id,
                        DocCode = doc.Code,
                        DocTitle = doc.Title,
                        ReqUid = req.ReqUid,
                        ReqTitle = req.Title,
                        Changed = matchingCases.Any(tc => tc.RequirementLinks.Any(rl => rl.DocumentId == doc.Id && rl.ReqUid.Equals(req.ReqUid, StringComparison.OrdinalIgnoreCase) && rl.ChangedVersionLabel != null))
                    },
                    Count = matchingCases.Count,
                    PassCount = matchingCases.Count(tc => tc.LastResult == TestRunResults.Pass),
                    FailCount = matchingCases.Count(tc => tc.LastResult == TestRunResults.Fail),
                    NeedsReviewCount = matchingCases.Count(tc => tc.NeedsReview)
                });
            }
        }

        // 3. Unlinked node
        var unlinkedCases = testCases.Where(tc => tc.RequirementLinks.Count == 0).ToList();
        nodes.Add(new TestTreeNodeDto
        {
            NodeId = "unlinked",
            Kind = "UNLINKED",
            Label = "Chưa gắn yêu cầu",
            ParentId = null,
            Count = unlinkedCases.Count,
            PassCount = unlinkedCases.Count(tc => tc.LastResult == TestRunResults.Pass),
            FailCount = unlinkedCases.Count(tc => tc.LastResult == TestRunResults.Fail),
            NeedsReviewCount = unlinkedCases.Count(tc => tc.NeedsReview)
        });

        return ServiceResult<ItemsDto<TestTreeNodeDto>>.Success(new ItemsDto<TestTreeNodeDto>(nodes));
    }

    public async Task<ServiceResult<TestFolderDto>> CreateTestFolderAsync(
        long projectId,
        CreateTestFolderRequest request,
        CancellationToken ct = default)
    {
        var auth = await EnsureProjectPermissionAsync(projectId, PermissionKeys.TestcaseCreate, ct);
        if (!auth.IsSuccess) return auth.Cast<TestFolderDto>();

        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
        {
            return ServiceResult<TestFolderDto>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["name"] = new[] { "Folder name is required and must be at most 100 characters." }
            });
        }

        if (request.ParentId.HasValue)
        {
            var parentExists = await _db.TestFolders
                .AsNoTracking()
                .AnyAsync(f => f.Id == request.ParentId.Value && f.ProjectId == projectId, ct);
            if (!parentExists)
                return ServiceResult<TestFolderDto>.NotFound("FOLDER_NOT_FOUND", "Parent test folder not found.");
        }

        var maxSort = await _db.TestFolders
            .Where(f => f.ProjectId == projectId && f.ParentId == request.ParentId)
            .Select(f => (int?)f.SortOrder)
            .MaxAsync(ct) ?? -1;

        var folder = new TestFolder
        {
            ProjectId = projectId,
            ParentId = request.ParentId,
            Name = name,
            SortOrder = maxSort + 1,
            CreatedAt = _clock.GetUtcNow().UtcDateTime
        };

        _db.TestFolders.Add(folder);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<TestFolderDto>.Created(new TestFolderDto
        {
            FolderId = folder.Id,
            Name = folder.Name,
            ParentId = folder.ParentId
        });
    }

    public async Task<ServiceResult<ItemsDto<TestCaseSummaryDto>>> GetProjectTestCasesAsync(
        long projectId,
        TestCaseListQuery query,
        CancellationToken ct = default)
    {
        var auth = await EnsureProjectPermissionAsync(projectId, PermissionKeys.TestcaseView, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<TestCaseSummaryDto>>();

        var q = _db.TestCases
            .AsNoTracking()
            .Include(tc => tc.LastRunBy)
            .Include(tc => tc.RequirementLinks)
            .Include(tc => tc.IssueLinks)
            .Where(tc => tc.ProjectId == projectId && !tc.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.ReqUid))
        {
            var reqUpper = query.ReqUid.Trim().ToUpperInvariant();
            if (query.DocumentId.HasValue)
            {
                var docId = query.DocumentId.Value;
                q = q.Where(tc => tc.RequirementLinks.Any(rl => rl.DocumentId == docId && rl.ReqUid == reqUpper));
            }
            else
            {
                q = q.Where(tc => tc.RequirementLinks.Any(rl => rl.ReqUid == reqUpper));
            }
        }
        else if (query.DocumentId.HasValue)
        {
            var docId = query.DocumentId.Value;
            q = q.Where(tc => tc.RequirementLinks.Any(rl => rl.DocumentId == docId));
        }

        if (query.FolderId.HasValue)
        {
            q = q.Where(tc => tc.FolderId == query.FolderId.Value);
        }

        if (query.Unlinked == true)
        {
            q = q.Where(tc => !tc.RequirementLinks.Any());
        }

        if (query.NeedsReview.HasValue)
        {
            q = q.Where(tc => tc.NeedsReview == query.NeedsReview.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.LastResult))
        {
            var lr = query.LastResult.Trim().ToUpperInvariant();
            if (lr == TestRunResults.NotRun)
                q = q.Where(tc => tc.LastResult == null || tc.LastResult == TestRunResults.NotRun);
            else
                q = q.Where(tc => tc.LastResult == lr);
        }

        if (query.IssueId.HasValue)
        {
            var issueId = query.IssueId.Value;
            var issueReqLinks = await _db.IssueRequirementLinks
                .AsNoTracking()
                .Where(rl => rl.IssueId == issueId)
                .Select(rl => new { rl.DocumentId, rl.ReqUid })
                .ToListAsync(ct);

            var reqUids = issueReqLinks.Select(x => x.ReqUid).Distinct().ToList();
            q = q.Where(tc => tc.IssueLinks.Any(il => il.IssueId == issueId) ||
                              (reqUids.Count > 0 && tc.RequirementLinks.Any(rl => reqUids.Contains(rl.ReqUid))));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(tc => tc.Key.Contains(s) || tc.Title.Contains(s) || tc.Preconditions.Contains(s));
        }

        var testCases = await q.OrderBy(tc => tc.Id).ToListAsync(ct);
        var dtos = await BuildTestCaseSummaryDtosAsync(testCases, ct);

        return ServiceResult<ItemsDto<TestCaseSummaryDto>>.Success(new ItemsDto<TestCaseSummaryDto>(dtos));
    }

    public async Task<ServiceResult<TestCaseDetailDto>> CreateTestCaseAsync(
        long projectId,
        CreateTestCaseRequest request,
        CancellationToken ct = default)
    {
        var auth = await EnsureProjectPermissionAsync(projectId, PermissionKeys.TestcaseCreate, ct);
        if (!auth.IsSuccess) return auth.Cast<TestCaseDetailDto>();

        var errors = new Dictionary<string, string[]>();
        var title = request.Title?.Trim() ?? string.Empty;
        var type = request.Type?.Trim().ToUpperInvariant() ?? TestCaseTypes.Functional;
        var priority = request.Priority?.Trim().ToUpperInvariant() ?? TestCasePriorities.Medium;

        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
            errors["title"] = new[] { "Title is required and must be at most 200 characters." };
        if (!TestCaseTypes.IsValid(type))
            errors["type"] = new[] { "Invalid test case type." };
        if (!TestCasePriorities.IsValid(priority))
            errors["priority"] = new[] { "Priority must be HIGH, MEDIUM, or LOW." };
        if (request.Steps == null || request.Steps.Count == 0)
            errors["steps"] = new[] { "At least 1 test step is required." };

        if (errors.Count > 0)
            return ServiceResult<TestCaseDetailDto>.ValidationFailure(errors);

        if (request.FolderId.HasValue)
        {
            var folderExists = await _db.TestFolders
                .AsNoTracking()
                .AnyAsync(f => f.Id == request.FolderId.Value && f.ProjectId == projectId, ct);
            if (!folderExists)
                return ServiceResult<TestCaseDetailDto>.NotFound("FOLDER_NOT_FOUND", "Test folder not found.");
        }

        var numberedSteps = NormalizeSteps(request.Steps!);
        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;

        return await _db.ExecuteInTransactionAsync(async innerCt =>
        {
            var key = await _db.AllocateTestCaseKeyAsync(projectId, innerCt);

            var tc = new TestCase
            {
                ProjectId = projectId,
                Key = key,
                Title = title,
                Type = type,
                Priority = priority,
                Preconditions = request.Preconditions?.Trim() ?? string.Empty,
                Steps = JsonSerializer.Serialize(numberedSteps),
                StepCount = numberedSteps.Count,
                FolderId = request.FolderId,
                NeedsReview = false,
                CreatedById = userId,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.TestCases.Add(tc);
            await _db.SaveChangesAsync(innerCt);

            if (request.RequirementRefs != null && request.RequirementRefs.Count > 0)
            {
                var err = await AttachTestCaseRequirementRefsAsync(tc, request.RequirementRefs, now, innerCt);
                if (!err.IsSuccess) return err.Cast<TestCaseDetailDto>();
            }

            var issueIdsToLink = request.LinkedIssueIds ?? request.IssueIds;
            if (issueIdsToLink != null && issueIdsToLink.Count > 0)
            {
                var validIssueIds = await _db.Issues
                    .AsNoTracking()
                    .Where(i => i.ProjectId == projectId && !i.IsDeleted && issueIdsToLink.Distinct().Contains(i.Id))
                    .Select(i => i.Id)
                    .ToListAsync(innerCt);

                foreach (var iid in validIssueIds)
                {
                    _db.TestCaseIssueLinks.Add(new TestCaseIssueLink
                    {
                        TestCaseId = tc.Id,
                        IssueId = iid,
                        LinkedAt = now
                    });
                }
            }

            await _db.SaveChangesAsync(innerCt);

            var detailRes = await GetTestCaseDetailAsync(tc.Id, innerCt);
            return ServiceResult<TestCaseDetailDto>.Created(detailRes.Data!);
        }, ct);
    }

    public async Task<ServiceResult<TestCaseDetailDto>> GetTestCaseDetailAsync(
        long testCaseId,
        CancellationToken ct = default)
    {
        var tc = await _db.TestCases
            .AsNoTracking()
            .Include(t => t.CreatedBy)
            .Include(t => t.LastRunBy)
            .Include(t => t.RequirementLinks)
            .Include(t => t.IssueLinks)
            .FirstOrDefaultAsync(t => t.Id == testCaseId && !t.IsDeleted, ct);
        if (tc == null)
            return ServiceResult<TestCaseDetailDto>.NotFound("TESTCASE_NOT_FOUND", "Test case not found.");

        var auth = await EnsureProjectPermissionAsync(tc.ProjectId, PermissionKeys.TestcaseView, ct);
        if (!auth.IsSuccess) return auth.Cast<TestCaseDetailDto>();

        var summaries = await BuildTestCaseSummaryDtosAsync(new List<TestCase> { tc }, ct);
        var summary = summaries[0];

        var historyRows = await _db.TestRunHistory
            .AsNoTracking()
            .Include(h => h.Run)
                .ThenInclude(r => r.Cycle)
            .Include(h => h.ExecutedBy)
            .Include(h => h.DefectIssue)
            .Where(h => h.TestCaseId == testCaseId)
            .OrderByDescending(h => h.ExecutedAt)
            .ThenByDescending(h => h.Id)
            .ToListAsync(ct);

        var historyDtos = historyRows.Select(h => new TestRunHistoryItemDto
        {
            RunId = h.RunId,
            CycleId = h.Run.CycleId,
            CycleName = h.Run.Cycle.Name,
            Result = h.Result,
            ExecutedBy = MapUserRef(h.ExecutedBy),
            ExecutedAt = FormatUtc(h.ExecutedAt),
            Note = h.Note,
            DefectIssue = h.DefectIssue != null
                ? new DefectIssueLiteDto { IssueId = h.DefectIssue.Id, Key = h.DefectIssue.Key }
                : null
        }).ToList();

        var userId = _currentUser.UserId;
        var userPerms = await _permissions.GetProjectPermissionsAsync(userId, tc.ProjectId, ct);
        var permSet = new HashSet<string>(userPerms, StringComparer.OrdinalIgnoreCase);

        var detail = new TestCaseDetailDto
        {
            TestCaseId = summary.TestCaseId,
            ProjectId = summary.ProjectId,
            Key = summary.Key,
            Title = summary.Title,
            Type = summary.Type,
            Priority = summary.Priority,
            FolderId = summary.FolderId,
            Preconditions = tc.Preconditions,
            Steps = SafeDeserialize<List<TestStepDto>>(tc.Steps) ?? new List<TestStepDto>(),
            StepCount = summary.StepCount,
            Requirements = summary.Requirements,
            LinkedIssues = summary.LinkedIssues,
            NeedsReview = summary.NeedsReview,
            LastResult = summary.LastResult,
            LastRunAt = summary.LastRunAt,
            LastRunBy = summary.LastRunBy,
            CreatedBy = MapUserRef(tc.CreatedBy),
            CreatedAt = FormatUtc(tc.CreatedAt),
            UpdatedAt = summary.UpdatedAt,
            RunHistory = historyDtos,
            Can = new TestCasePermissionsDto
            {
                Edit = permSet.Contains(PermissionKeys.TestcaseUpdate),
                Delete = permSet.Contains(PermissionKeys.TestcaseDelete),
                Execute = permSet.Contains(PermissionKeys.TestcaseExecute)
            }
        };

        return ServiceResult<TestCaseDetailDto>.Success(detail);
    }

    public async Task<ServiceResult<TestCaseDetailDto>> UpdateTestCaseAsync(
        long testCaseId,
        UpdateTestCaseRequest request,
        CancellationToken ct = default)
    {
        var tc = await _db.TestCases
            .Include(t => t.RequirementLinks)
            .Include(t => t.IssueLinks)
            .FirstOrDefaultAsync(t => t.Id == testCaseId && !t.IsDeleted, ct);
        if (tc == null)
            return ServiceResult<TestCaseDetailDto>.NotFound("TESTCASE_NOT_FOUND", "Test case not found.");

        var auth = await EnsureProjectPermissionAsync(tc.ProjectId, PermissionKeys.TestcaseUpdate, ct);
        if (!auth.IsSuccess) return auth.Cast<TestCaseDetailDto>();

        var now = _clock.GetUtcNow().UtcDateTime;

        if (request.Title != null)
        {
            var t = request.Title.Trim();
            if (string.IsNullOrWhiteSpace(t) || t.Length > 200)
                return ServiceResult<TestCaseDetailDto>.ValidationFailure(new Dictionary<string, string[]> { ["title"] = new[] { "Title must be 1..200 chars." } });
            tc.Title = t;
        }

        if (request.Type != null)
        {
            var t = request.Type.Trim().ToUpperInvariant();
            if (!TestCaseTypes.IsValid(t))
                return ServiceResult<TestCaseDetailDto>.ValidationFailure(new Dictionary<string, string[]> { ["type"] = new[] { "Invalid test case type." } });
            tc.Type = t;
        }

        if (request.Priority != null)
        {
            var p = request.Priority.Trim().ToUpperInvariant();
            if (!TestCasePriorities.IsValid(p))
                return ServiceResult<TestCaseDetailDto>.ValidationFailure(new Dictionary<string, string[]> { ["priority"] = new[] { "Invalid priority." } });
            tc.Priority = p;
        }

        if (request.Preconditions != null)
        {
            tc.Preconditions = request.Preconditions.Trim();
        }

        if (request.FolderIdSpecified)
        {
            if (request.FolderId.HasValue)
            {
                var folderExists = await _db.TestFolders
                    .AsNoTracking()
                    .AnyAsync(f => f.Id == request.FolderId.Value && f.ProjectId == tc.ProjectId, ct);
                if (!folderExists)
                    return ServiceResult<TestCaseDetailDto>.NotFound("FOLDER_NOT_FOUND", "Test folder not found.");
            }
            tc.FolderId = request.FolderId;
        }

        if (request.Steps != null)
        {
            if (request.Steps.Count == 0)
                return ServiceResult<TestCaseDetailDto>.ValidationFailure(new Dictionary<string, string[]> { ["steps"] = new[] { "At least 1 step is required." } });
            var numbered = NormalizeSteps(request.Steps);
            tc.Steps = JsonSerializer.Serialize(numbered);
            tc.StepCount = numbered.Count;
        }

        if (request.RequirementRefs != null)
        {
            _db.TestCaseRequirementLinks.RemoveRange(tc.RequirementLinks);
            tc.RequirementLinks.Clear();
            var err = await AttachTestCaseRequirementRefsAsync(tc, request.RequirementRefs, now, ct);
            if (!err.IsSuccess) return err.Cast<TestCaseDetailDto>();
            tc.NeedsReview = false;
        }
        else if (request.MarkReviewed == true || request.NeedsReview == false || request.Steps != null)
        {
            tc.NeedsReview = false;
            foreach (var link in tc.RequirementLinks)
            {
                var doc = await _db.DocumentNodes.AsNoTracking().FirstOrDefaultAsync(d => d.Id == link.DocumentId, ct);
                var verId = doc?.BaselineVersionId ?? doc?.CurrentVersionId;
                if (verId.HasValue)
                {
                    var req = await _db.RequirementItems
                        .AsNoTracking()
                        .FirstOrDefaultAsync(r => r.VersionId == verId.Value && r.ReqUid == link.ReqUid, ct);
                    if (req != null) link.ReqHash = req.ContentHash;
                }
                link.ChangedVersionLabel = null;
            }
        }

        var issueIdsToUpdate = request.LinkedIssueIds ?? request.IssueIds;
        if (issueIdsToUpdate != null)
        {
            _db.TestCaseIssueLinks.RemoveRange(tc.IssueLinks);
            tc.IssueLinks.Clear();

            var validIssueIds = await _db.Issues
                .AsNoTracking()
                .Where(i => i.ProjectId == tc.ProjectId && !i.IsDeleted && issueIdsToUpdate.Distinct().Contains(i.Id))
                .Select(i => i.Id)
                .ToListAsync(ct);

            foreach (var iid in validIssueIds)
            {
                _db.TestCaseIssueLinks.Add(new TestCaseIssueLink
                {
                    TestCaseId = tc.Id,
                    IssueId = iid,
                    LinkedAt = now
                });
            }
        }

        tc.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        return await GetTestCaseDetailAsync(tc.Id, ct);
    }

    public async Task<ServiceResult> DeleteTestCaseAsync(long testCaseId, CancellationToken ct = default)
    {
        var tc = await _db.TestCases.FirstOrDefaultAsync(t => t.Id == testCaseId && !t.IsDeleted, ct);
        if (tc == null)
            return ServiceResult.NotFound("TESTCASE_NOT_FOUND", "Test case not found.");

        var auth = await EnsureProjectPermissionAsync(tc.ProjectId, PermissionKeys.TestcaseDelete, ct);
        if (!auth.IsSuccess) return auth;

        tc.IsDeleted = true;
        tc.UpdatedAt = _clock.GetUtcNow().UtcDateTime;

        var pendingRuns = await _db.TestRuns
            .Include(r => r.Cycle)
            .Where(r => r.TestCaseId == testCaseId && r.Cycle.Status == TestCycleStatuses.Open && r.Result == TestRunResults.NotRun)
            .ToListAsync(ct);

        if (pendingRuns.Count > 0)
        {
            _db.TestRuns.RemoveRange(pendingRuns);
        }

        await _db.SaveChangesAsync(ct);
        return ServiceResult.NoContent();
    }

    public async Task<ServiceResult<ItemsDto<SuggestedTestCaseDto>>> SuggestTestCasesAsync(
        long projectId,
        SuggestTestCasesRequest request,
        CancellationToken ct = default)
    {
        var auth = await EnsureProjectPermissionAsync(projectId, PermissionKeys.TestcaseCreate, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<SuggestedTestCaseDto>>();

        if (request.Refs == null || request.Refs.Count == 0)
        {
            return ServiceResult<ItemsDto<SuggestedTestCaseDto>>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["refs"] = new[] { "At least 1 requirement reference is required." }
            });
        }

        var suggestions = new List<SuggestedTestCaseDto>();
        int idx = 1;

        foreach (var r in request.Refs)
        {
            var doc = await _db.DocumentNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == r.DocumentId && d.ProjectId == projectId && !d.IsDeleted, ct);
            if (doc == null) continue;

            var verId = doc.BaselineVersionId ?? doc.CurrentVersionId;
            if (!verId.HasValue) continue;

            var reqUidUpper = r.ReqUid.Trim().ToUpperInvariant();
            var req = await _db.RequirementItems
                .AsNoTracking()
                .FirstOrDefaultAsync(ri => ri.VersionId == verId.Value && ri.ReqUid == reqUidUpper, ct);
            if (req == null) continue;

            var acList = SafeDeserialize<List<string>>(req.AcceptanceCriteria) ?? new List<string>();
            var firstAc = acList.FirstOrDefault() ?? $"Hệ thống xử lý thành công yêu cầu {req.Title}.";
            var secondAc = acList.Skip(1).FirstOrDefault() ?? "Hiển thị thông báo lỗi khi dữ liệu không hợp lệ.";

            // 1. Happy path
            suggestions.Add(new SuggestedTestCaseDto
            {
                TempId = $"sug-{idx++}",
                ReqUid = req.ReqUid,
                DocumentId = doc.Id,
                Title = $"[{req.ReqUid}] Kiểm tra luồng chính: {req.Title}",
                Type = TestCaseTypes.Functional,
                Priority = TestCasePriorities.High,
                Preconditions = "Người dùng đã đăng nhập và có quyền truy cập chức năng.",
                FromCriterion = firstAc,
                Steps = new List<TestStepDto>
                {
                    new() { StepNumber = 1, Action = $"Truy cập màn hình chức năng của {req.Title}", TestData = null, Expected = "Hiển thị đầy đủ các trường thông tin hợp lệ." },
                    new() { StepNumber = 2, Action = "Nhập dữ liệu hợp lệ và thực hiện thao tác chính", TestData = "Dữ liệu chuẩn theo đặc tả", Expected = firstAc }
                }
            });

            // 2. Negative / Validation path
            suggestions.Add(new SuggestedTestCaseDto
            {
                TempId = $"sug-{idx++}",
                ReqUid = req.ReqUid,
                DocumentId = doc.Id,
                Title = $"[{req.ReqUid}] Kiểm tra dữ liệu không hợp lệ / bắt buộc: {req.Title}",
                Type = TestCaseTypes.Functional,
                Priority = TestCasePriorities.Medium,
                Preconditions = "Người dùng đang ở màn hình thao tác chức năng.",
                FromCriterion = secondAc,
                Steps = new List<TestStepDto>
                {
                    new() { StepNumber = 1, Action = "Bỏ trống hoặc nhập sai định dạng các trường bắt buộc", TestData = "Trường bắt buộc = rỗng", Expected = secondAc },
                    new() { StepNumber = 2, Action = "Kiểm tra dữ liệu không bị lưu sai vào hệ thống", TestData = null, Expected = "Trạng thái dữ liệu giữ nguyên, không phát sinh bản ghi lỗi." }
                }
            });
        }

        return ServiceResult<ItemsDto<SuggestedTestCaseDto>>.Success(new ItemsDto<SuggestedTestCaseDto>(suggestions));
    }

    public async Task<ServiceResult<ItemsDto<TestCycleDto>>> GetProjectTestCyclesAsync(
        long projectId,
        string? status,
        CancellationToken ct = default)
    {
        var auth = await EnsureProjectPermissionAsync(projectId, PermissionKeys.TestcaseView, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<TestCycleDto>>();

        var q = _db.TestCycles
            .AsNoTracking()
            .Include(c => c.Sprint)
            .Include(c => c.CreatedBy)
            .Include(c => c.Runs)
            .Where(c => c.ProjectId == projectId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var stUpper = status.Trim().ToUpperInvariant();
            q = q.Where(c => c.Status == stUpper);
        }

        var cycles = await q
            .OrderByDescending(c => c.Status == TestCycleStatuses.Open)
            .ThenByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        var dtos = cycles.Select(MapTestCycleDto).ToList();
        return ServiceResult<ItemsDto<TestCycleDto>>.Success(new ItemsDto<TestCycleDto>(dtos));
    }

    public async Task<ServiceResult<TestCycleDto>> CreateTestCycleAsync(
        long projectId,
        CreateTestCycleRequest request,
        CancellationToken ct = default)
    {
        var auth = await EnsureProjectPermissionAsync(projectId, PermissionKeys.TestcaseCreate, ct);
        if (!auth.IsSuccess)
        {
            auth = await EnsureProjectPermissionAsync(projectId, PermissionKeys.TestcaseExecute, ct);
        }
        if (!auth.IsSuccess) return auth.Cast<TestCycleDto>();

        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name) || name.Length > 150)
        {
            return ServiceResult<TestCycleDto>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["name"] = new[] { "Cycle name is required and must be at most 150 characters." }
            });
        }

        var tcQuery = _db.TestCases
            .AsNoTracking()
            .Include(tc => tc.RequirementLinks)
            .Where(tc => tc.ProjectId == projectId && !tc.IsDeleted);

        var f = request.Filter;
        var explicitTestCaseIds = f?.TestCaseIds ?? request.TestCaseIds;
        if (explicitTestCaseIds != null && explicitTestCaseIds.Count > 0)
        {
            tcQuery = tcQuery.Where(tc => explicitTestCaseIds.Contains(tc.Id));
        }
        if (f != null)
        {
            if (f.ReqUids != null && f.ReqUids.Count > 0)
            {
                var upperUids = f.ReqUids.Select(u => u.Trim().ToUpperInvariant()).ToList();
                tcQuery = tcQuery.Where(tc => tc.RequirementLinks.Any(rl => upperUids.Contains(rl.ReqUid)));
            }
            if (f.FolderId.HasValue)
            {
                tcQuery = tcQuery.Where(tc => tc.FolderId == f.FolderId.Value);
            }
        }

        var selectedIds = await tcQuery.OrderBy(tc => tc.Id).Select(tc => tc.Id).ToListAsync(ct);
        if (selectedIds.Count == 0)
        {
            return ServiceResult<TestCycleDto>.BadRequest(
                "EMPTY_TEST_CYCLE",
                "Không tìm thấy test case nào thoả mãn bộ lọc để đưa vào chu kỳ kiểm thử.");
        }

        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;

        var cycle = new TestCycle
        {
            ProjectId = projectId,
            Name = name,
            SprintId = request.SprintId,
            Status = TestCycleStatuses.Open,
            CreatedById = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.TestCycles.Add(cycle);
        await _db.SaveChangesAsync(ct);

        var defaultAssigneeId = f?.DefaultAssigneeId ?? request.DefaultAssigneeId;
        foreach (var tcId in selectedIds)
        {
            _db.TestRuns.Add(new TestRun
            {
                CycleId = cycle.Id,
                TestCaseId = tcId,
                AssigneeId = defaultAssigneeId,
                Result = TestRunResults.NotRun,
                NeedsRerun = false
            });
        }

        await _db.SaveChangesAsync(ct);

        var loaded = await _db.TestCycles
            .AsNoTracking()
            .Include(c => c.Sprint)
            .Include(c => c.CreatedBy)
            .Include(c => c.Runs)
            .FirstAsync(c => c.Id == cycle.Id, ct);

        return ServiceResult<TestCycleDto>.Created(MapTestCycleDto(loaded));
    }

    public async Task<ServiceResult<TestCycleDto>> UpdateTestCycleAsync(
        long cycleId,
        UpdateTestCycleRequest request,
        CancellationToken ct = default)
    {
        var cycle = await _db.TestCycles
            .Include(c => c.Runs)
            .FirstOrDefaultAsync(c => c.Id == cycleId, ct);
        if (cycle == null)
            return ServiceResult<TestCycleDto>.NotFound("CYCLE_NOT_FOUND", "Test cycle not found.");

        var auth = await EnsureProjectPermissionAsync(cycle.ProjectId, PermissionKeys.TestcaseUpdate, ct);
        if (!auth.IsSuccess)
        {
            auth = await EnsureProjectPermissionAsync(cycle.ProjectId, PermissionKeys.TestcaseExecute, ct);
        }
        if (!auth.IsSuccess) return auth.Cast<TestCycleDto>();

        if (request.Name != null)
        {
            var n = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(n) || n.Length > 150)
                return ServiceResult<TestCycleDto>.ValidationFailure(new Dictionary<string, string[]> { ["name"] = new[] { "Name must be 1..150 chars." } });
            cycle.Name = n;
        }

        if (request.Status != null)
        {
            var st = request.Status.Trim().ToUpperInvariant();
            if (!TestCycleStatuses.IsValid(st))
                return ServiceResult<TestCycleDto>.ValidationFailure(new Dictionary<string, string[]> { ["status"] = new[] { "Status must be OPEN or CLOSED." } });
            cycle.Status = st;
        }

        if (request.AddTestCaseIds != null && request.AddTestCaseIds.Count > 0)
        {
            var existingTcIds = cycle.Runs.Select(r => r.TestCaseId).ToHashSet();
            var validNewTcIds = await _db.TestCases
                .AsNoTracking()
                .Where(tc => tc.ProjectId == cycle.ProjectId && !tc.IsDeleted && request.AddTestCaseIds.Distinct().Contains(tc.Id))
                .Select(tc => tc.Id)
                .ToListAsync(ct);

            foreach (var tcId in validNewTcIds.Where(id => !existingTcIds.Contains(id)))
            {
                _db.TestRuns.Add(new TestRun
                {
                    CycleId = cycle.Id,
                    TestCaseId = tcId,
                    Result = TestRunResults.NotRun
                });
            }
        }

        cycle.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);

        var loaded = await _db.TestCycles
            .AsNoTracking()
            .Include(c => c.Sprint)
            .Include(c => c.CreatedBy)
            .Include(c => c.Runs)
            .FirstAsync(c => c.Id == cycle.Id, ct);

        return ServiceResult<TestCycleDto>.Success(MapTestCycleDto(loaded));
    }

    public async Task<ServiceResult<ItemsDto<TestRunDto>>> GetCycleRunsAsync(
        long cycleId,
        TestRunListQuery query,
        CancellationToken ct = default)
    {
        var cycle = await _db.TestCycles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cycleId, ct);
        if (cycle == null)
            return ServiceResult<ItemsDto<TestRunDto>>.NotFound("CYCLE_NOT_FOUND", "Test cycle not found.");

        var auth = await EnsureProjectPermissionAsync(cycle.ProjectId, PermissionKeys.TestcaseView, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<TestRunDto>>();

        var q = _db.TestRuns
            .AsNoTracking()
            .Include(r => r.TestCase)
                .ThenInclude(tc => tc.RequirementLinks)
            .Include(r => r.TestCase)
                .ThenInclude(tc => tc.IssueLinks)
            .Include(r => r.TestCase)
                .ThenInclude(tc => tc.LastRunBy)
            .Include(r => r.Assignee)
            .Include(r => r.ExecutedBy)
            .Include(r => r.Defects)
                .ThenInclude(d => d.Issue)
                    .ThenInclude(i => i.Status)
            .Where(r => r.CycleId == cycleId);

        if (!string.IsNullOrWhiteSpace(query.AssigneeId))
        {
            var aVal = query.AssigneeId.Trim();
            if (aVal.Equals("me", StringComparison.OrdinalIgnoreCase))
            {
                var myId = _currentUser.UserId;
                q = q.Where(r => r.AssigneeId == myId);
            }
            else if (long.TryParse(aVal, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedAssigneeId))
            {
                q = q.Where(r => r.AssigneeId == parsedAssigneeId);
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Result))
        {
            var resUpper = query.Result.Trim().ToUpperInvariant();
            q = q.Where(r => r.Result == resUpper);
        }

        if (query.NeedsRerun.HasValue)
        {
            q = q.Where(r => r.NeedsRerun == query.NeedsRerun.Value);
        }

        var runs = await q.OrderBy(r => r.Id).ToListAsync(ct);
        var dtos = await BuildTestRunDtosAsync(runs, ct);

        return ServiceResult<ItemsDto<TestRunDto>>.Success(new ItemsDto<TestRunDto>(dtos));
    }

    public async Task<ServiceResult<TestRunDto>> UpdateTestRunAsync(
        long runId,
        UpdateTestRunRequest request,
        CancellationToken ct = default)
    {
        var run = await _db.TestRuns
            .Include(r => r.Cycle)
            .FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run == null)
            return ServiceResult<TestRunDto>.NotFound("RUN_NOT_FOUND", "Test run not found.");

        var auth = await EnsureProjectPermissionAsync(run.Cycle.ProjectId, PermissionKeys.TestcaseExecute, ct);
        if (!auth.IsSuccess)
        {
            auth = await EnsureProjectPermissionAsync(run.Cycle.ProjectId, PermissionKeys.TestcaseUpdate, ct);
        }
        if (!auth.IsSuccess) return auth.Cast<TestRunDto>();

        run.AssigneeId = request.AssigneeId;
        await _db.SaveChangesAsync(ct);

        return await GetSingleRunDtoAsync(runId, ct);
    }

    public async Task<ServiceResult<TestRunDto>> RecordTestRunResultAsync(
        long runId,
        RecordTestRunResultRequest request,
        CancellationToken ct = default)
    {
        var run = await _db.TestRuns
            .Include(r => r.Cycle)
            .Include(r => r.TestCase)
                .ThenInclude(tc => tc.RequirementLinks)
            .Include(r => r.Defects)
            .FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run == null)
            return ServiceResult<TestRunDto>.NotFound("RUN_NOT_FOUND", "Test run not found.");

        var auth = await EnsureProjectPermissionAsync(run.Cycle.ProjectId, PermissionKeys.TestcaseExecute, ct);
        if (!auth.IsSuccess) return auth.Cast<TestRunDto>();

        if (run.Cycle.Status == TestCycleStatuses.Closed)
        {
            return ServiceResult<TestRunDto>.Conflict(
                "CYCLE_CLOSED",
                "Chu kỳ kiểm thử đã đóng, không thể ghi nhận kết quả mới.");
        }

        var resultUpper = request.Result?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!TestRunResults.IsValid(resultUpper))
        {
            return ServiceResult<TestRunDto>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["result"] = new[] { "result must be NOT_RUN, PASS, FAIL, or BLOCKED." }
            });
        }

        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;
        var projectId = run.Cycle.ProjectId;

        return await _db.ExecuteInTransactionAsync(async innerCt =>
        {
            run.Result = resultUpper;
            run.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            run.ExecutedAt = now;
            run.ExecutedById = userId;
            run.NeedsRerun = false;

            run.TestCase.LastResult = resultUpper;
            run.TestCase.LastRunAt = now;
            run.TestCase.LastRunById = userId;
            run.TestCase.UpdatedAt = now;

            long? primaryDefectIssueId = null;

            // Inline defect creation
            if (request.CreateDefect != null && !string.IsNullOrWhiteSpace(request.CreateDefect.Title))
            {
                var project = await _db.Projects.AsNoTracking().FirstAsync(p => p.Id == projectId, innerCt);
                var initialStatus = await _db.WorkflowStatuses
                    .AsNoTracking()
                    .Include(s => s.Phase)
                    .Where(s => s.ProjectId == projectId && s.IsActive)
                    .OrderByDescending(s => s.IsInitial)
                    .ThenBy(s => s.Phase.Position)
                    .ThenBy(s => s.Position)
                    .FirstAsync(innerCt);

                var activeSprintId = await _db.Sprints
                    .AsNoTracking()
                    .Where(s => s.ProjectId == projectId && s.Status == SprintStatuses.Active)
                    .Select(s => (long?)s.Id)
                    .FirstOrDefaultAsync(innerCt);

                var bugKey = await _db.AllocateIssueKeyAsync(projectId, project.Key, innerCt);
                var bugPriority = request.CreateDefect.Priority?.Trim().ToUpperInvariant() ?? IssuePriorities.High;
                if (!IssuePriorities.IsValid(bugPriority)) bugPriority = IssuePriorities.High;

                var bugSprintId = request.CreateDefect.SprintId ?? run.Cycle.SprintId ?? activeSprintId;

                var bug = new Issue
                {
                    ProjectId = projectId,
                    Key = bugKey,
                    Type = IssueTypes.Bug,
                    Title = request.CreateDefect.Title.Trim(),
                    Description = request.CreateDefect.Description?.Trim()
                                  ?? $"Phát hiện lỗi khi chạy test case {run.TestCase.Key} ({run.TestCase.Title}) trong chu kỳ {run.Cycle.Name}.\n{run.Note}",
                    StatusId = initialStatus.Id,
                    Priority = bugPriority,
                    AssigneeId = request.CreateDefect.AssigneeId,
                    ReporterId = userId,
                    SprintId = bugSprintId,
                    Rank = "00999000",
                    Labels = "[\"defect\"]",
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _db.Issues.Add(bug);
                await _db.SaveChangesAsync(innerCt);

                primaryDefectIssueId = bug.Id;

                // Copy requirement links from testCase to bug
                foreach (var rl in run.TestCase.RequirementLinks)
                {
                    _db.IssueRequirementLinks.Add(new IssueRequirementLink
                    {
                        IssueId = bug.Id,
                        DocumentId = rl.DocumentId,
                        ReqUid = rl.ReqUid,
                        ReqHash = rl.ReqHash,
                        LinkedAt = now,
                        LinkedById = userId
                    });
                }

                _db.IssueActivities.Add(new IssueActivity
                {
                    IssueId = bug.Id,
                    Action = IssueActivityActions.Created,
                    ActorId = userId,
                    Note = $"Tạo lỗi từ lần chạy kiểm thử {run.TestCase.Key}",
                    CreatedAt = now
                });

                _db.TestRunDefects.Add(new TestRunDefect
                {
                    RunId = run.Id,
                    IssueId = bug.Id,
                    CreatedAt = now
                });

                var linkExists = await _db.TestCaseIssueLinks
                    .AnyAsync(l => l.TestCaseId == run.TestCaseId && l.IssueId == bug.Id, innerCt);
                if (!linkExists)
                {
                    _db.TestCaseIssueLinks.Add(new TestCaseIssueLink
                    {
                        TestCaseId = run.TestCaseId,
                        IssueId = bug.Id,
                        LinkedAt = now
                    });
                }
            }

            if (request.DefectIssueIds != null && request.DefectIssueIds.Count > 0)
            {
                var existingDefectIds = run.Defects.Select(d => d.IssueId).ToHashSet();
                foreach (var iid in request.DefectIssueIds.Distinct())
                {
                    primaryDefectIssueId ??= iid;
                    if (!existingDefectIds.Contains(iid))
                    {
                        _db.TestRunDefects.Add(new TestRunDefect
                        {
                            RunId = run.Id,
                            IssueId = iid,
                            CreatedAt = now
                        });
                    }

                    var tcLinkExists = await _db.TestCaseIssueLinks
                        .AnyAsync(l => l.TestCaseId == run.TestCaseId && l.IssueId == iid, innerCt);
                    if (!tcLinkExists)
                    {
                        _db.TestCaseIssueLinks.Add(new TestCaseIssueLink
                        {
                            TestCaseId = run.TestCaseId,
                            IssueId = iid,
                            LinkedAt = now
                        });
                    }
                }
            }

            if (resultUpper is TestRunResults.Pass or TestRunResults.Fail or TestRunResults.Blocked)
            {
                _db.TestRunHistory.Add(new TestRunHistory
                {
                    RunId = run.Id,
                    TestCaseId = run.TestCaseId,
                    Result = resultUpper,
                    Note = run.Note,
                    DefectIssueId = primaryDefectIssueId,
                    ExecutedById = userId,
                    ExecutedAt = now
                });
            }

            await _db.SaveChangesAsync(innerCt);
            return await GetSingleRunDtoAsync(run.Id, innerCt);
        }, ct);
    }

    // --- Helpers ---

    private async Task<ServiceResult> EnsureProjectPermissionAsync(long projectId, string permission, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserIdOrNull is null)
            return ServiceResult.Unauthorized();

        var projectExists = await _db.Projects.AsNoTracking().AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
            return ServiceResult.NotFound("PROJECT_NOT_FOUND", "Project not found.");

        var hasPerm = await _permissions.HasProjectPermissionAsync(_currentUser.UserId, projectId, permission, ct);
        if (!hasPerm)
            return ServiceResult.PermissionDenied(permission);

        return ServiceResult.Success();
    }

    private async Task<ServiceResult> AttachTestCaseRequirementRefsAsync(
        TestCase tc,
        List<RequirementRefInputDto> refs,
        DateTime now,
        CancellationToken ct)
    {
        foreach (var r in refs.DistinctBy(x => (x.DocumentId, x.ReqUid.Trim().ToUpperInvariant())))
        {
            var doc = await _db.DocumentNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == r.DocumentId && d.ProjectId == tc.ProjectId && !d.IsDeleted, ct);
            if (doc == null)
                return ServiceResult.NotFound("DOCUMENT_NOT_FOUND", $"Document {r.DocumentId} not found.");

            var verId = doc.BaselineVersionId ?? doc.CurrentVersionId;
            var reqUidUpper = r.ReqUid.Trim().ToUpperInvariant();
            var req = verId.HasValue
                ? await _db.RequirementItems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ri => ri.VersionId == verId.Value && ri.ReqUid == reqUidUpper, ct)
                : null;

            if (req == null)
                return ServiceResult.NotFound("REQUIREMENT_NOT_FOUND", $"Requirement {reqUidUpper} not found.");

            _db.TestCaseRequirementLinks.Add(new TestCaseRequirementLink
            {
                TestCaseId = tc.Id,
                DocumentId = doc.Id,
                ReqUid = req.ReqUid,
                ReqHash = req.ContentHash,
                ChangedVersionLabel = null,
                LinkedAt = now
            });
        }

        return ServiceResult.Success();
    }

    private async Task<List<TestCaseSummaryDto>> BuildTestCaseSummaryDtosAsync(
        List<TestCase> testCases,
        CancellationToken ct)
    {
        if (testCases.Count == 0) return new List<TestCaseSummaryDto>();

        var docIds = testCases
            .SelectMany(tc => tc.RequirementLinks.Select(rl => rl.DocumentId))
            .Distinct()
            .ToList();

        var docs = docIds.Count == 0
            ? new Dictionary<long, DocumentNode>()
            : await _db.DocumentNodes
                .AsNoTracking()
                .Where(d => docIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, ct);

        var verIds = docs.Values
            .Select(d => d.BaselineVersionId ?? d.CurrentVersionId)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();

        var reqItems = verIds.Count == 0
            ? new List<RequirementItem>()
            : await _db.RequirementItems
                .AsNoTracking()
                .Where(r => verIds.Contains(r.VersionId))
                .ToListAsync(ct);

        var reqLookup = reqItems.ToDictionary(r => (r.VersionId, r.ReqUid.ToUpperInvariant()));

        var issueIds = testCases
            .SelectMany(tc => tc.IssueLinks.Select(il => il.IssueId))
            .Distinct()
            .ToList();

        var issues = issueIds.Count == 0
            ? new Dictionary<long, Issue>()
            : await _db.Issues
                .AsNoTracking()
                .Include(i => i.Status)
                .Where(i => issueIds.Contains(i.Id) && !i.IsDeleted)
                .ToDictionaryAsync(i => i.Id, ct);

        var dtos = new List<TestCaseSummaryDto>(testCases.Count);
        foreach (var tc in testCases)
        {
            var reqRefs = new List<TestCaseRequirementRefDto>();
            foreach (var rl in tc.RequirementLinks)
            {
                if (!docs.TryGetValue(rl.DocumentId, out var doc)) continue;
                var targetVerId = doc.BaselineVersionId ?? doc.CurrentVersionId;
                RequirementItem? reqItem = null;
                if (targetVerId.HasValue)
                {
                    reqLookup.TryGetValue((targetVerId.Value, rl.ReqUid.ToUpperInvariant()), out reqItem);
                }

                reqRefs.Add(new TestCaseRequirementRefDto
                {
                    DocumentId = rl.DocumentId,
                    DocCode = doc.Code,
                    DocTitle = doc.Title,
                    ReqUid = rl.ReqUid,
                    ReqTitle = reqItem?.Title ?? rl.ReqUid,
                    Changed = rl.ChangedVersionLabel != null,
                    ChangedVersionLabel = rl.ChangedVersionLabel
                });
            }

            var linkedIssueRefs = new List<TestCaseIssueRefDto>();
            foreach (var il in tc.IssueLinks)
            {
                if (!issues.TryGetValue(il.IssueId, out var iss)) continue;
                linkedIssueRefs.Add(new TestCaseIssueRefDto
                {
                    IssueId = iss.Id,
                    Key = iss.Key,
                    Title = iss.Title,
                    Type = iss.Type,
                    StatusCategory = iss.Status.Category
                });
            }

            dtos.Add(new TestCaseSummaryDto
            {
                TestCaseId = tc.Id,
                ProjectId = tc.ProjectId,
                Key = tc.Key,
                Title = tc.Title,
                Type = tc.Type,
                Priority = tc.Priority,
                FolderId = tc.FolderId,
                StepCount = tc.StepCount,
                Requirements = reqRefs,
                LinkedIssues = linkedIssueRefs,
                NeedsReview = tc.NeedsReview,
                LastResult = tc.LastResult,
                LastRunAt = tc.LastRunAt.HasValue ? FormatUtc(tc.LastRunAt.Value) : null,
                LastRunBy = tc.LastRunBy != null ? MapUserRef(tc.LastRunBy) : null,
                UpdatedAt = FormatUtc(tc.UpdatedAt)
            });
        }

        return dtos;
    }

    private async Task<ServiceResult<TestRunDto>> GetSingleRunDtoAsync(long runId, CancellationToken ct)
    {
        var run = await _db.TestRuns
            .AsNoTracking()
            .Include(r => r.TestCase)
                .ThenInclude(tc => tc.RequirementLinks)
            .Include(r => r.TestCase)
                .ThenInclude(tc => tc.IssueLinks)
            .Include(r => r.TestCase)
                .ThenInclude(tc => tc.LastRunBy)
            .Include(r => r.Assignee)
            .Include(r => r.ExecutedBy)
            .Include(r => r.Defects)
                .ThenInclude(d => d.Issue)
                    .ThenInclude(i => i.Status)
            .FirstAsync(r => r.Id == runId, ct);

        var dtos = await BuildTestRunDtosAsync(new List<TestRun> { run }, ct);
        return ServiceResult<TestRunDto>.Success(dtos[0]);
    }

    private async Task<List<TestRunDto>> BuildTestRunDtosAsync(List<TestRun> runs, CancellationToken ct)
    {
        if (runs.Count == 0) return new List<TestRunDto>();

        var distinctCases = runs.Select(r => r.TestCase).GroupBy(tc => tc.Id).Select(g => g.First()).ToList();
        var caseSummaries = await BuildTestCaseSummaryDtosAsync(distinctCases, ct);
        var caseSummaryMap = caseSummaries.ToDictionary(s => s.TestCaseId);

        var docIds = runs
            .SelectMany(r => r.TestCase.RequirementLinks.Select(rl => rl.DocumentId))
            .Distinct()
            .ToList();

        var docs = docIds.Count == 0
            ? new Dictionary<long, DocumentNode>()
            : await _db.DocumentNodes
                .AsNoTracking()
                .Where(d => docIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, ct);

        var verIds = docs.Values
            .Select(d => d.BaselineVersionId ?? d.CurrentVersionId)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();

        var reqItems = verIds.Count == 0
            ? new List<RequirementItem>()
            : await _db.RequirementItems
                .AsNoTracking()
                .Where(ri => verIds.Contains(ri.VersionId))
                .ToListAsync(ct);

        var reqLookup = reqItems.ToDictionary(ri => (ri.VersionId, ri.ReqUid.ToUpperInvariant()));

        var dtos = new List<TestRunDto>(runs.Count);
        foreach (var r in runs)
        {
            var reqLites = new List<TestRunReqLiteDto>();
            foreach (var rl in r.TestCase.RequirementLinks)
            {
                docs.TryGetValue(rl.DocumentId, out var doc);
                var verId = doc?.BaselineVersionId ?? doc?.CurrentVersionId;
                RequirementItem? reqItem = null;
                if (verId.HasValue)
                {
                    reqLookup.TryGetValue((verId.Value, rl.ReqUid.ToUpperInvariant()), out reqItem);
                }
                reqLites.Add(new TestRunReqLiteDto
                {
                    DocCode = doc?.Code,
                    ReqUid = rl.ReqUid,
                    ReqTitle = reqItem?.Title ?? rl.ReqUid
                });
            }

            caseSummaryMap.TryGetValue(r.TestCaseId, out var tcSummary);

            dtos.Add(new TestRunDto
            {
                RunId = r.Id,
                CycleId = r.CycleId,
                TestCaseId = r.TestCaseId,
                TestCaseKey = r.TestCase.Key,
                TestCaseTitle = r.TestCase.Title,
                TestCasePriority = r.TestCase.Priority,
                TestCaseType = r.TestCase.Type,
                StepCount = r.TestCase.StepCount,
                Requirements = reqLites,
                TestCase = tcSummary,
                Assignee = r.Assignee != null ? MapUserRef(r.Assignee) : null,
                Result = r.Result,
                Note = r.Note,
                ExecutedAt = r.ExecutedAt.HasValue ? FormatUtc(r.ExecutedAt.Value) : null,
                ExecutedBy = r.ExecutedBy != null ? MapUserRef(r.ExecutedBy) : null,
                Defects = r.Defects
                    .Where(d => !d.Issue.IsDeleted)
                    .Select(d => new TestRunDefectRefDto
                    {
                        IssueId = d.IssueId,
                        Key = d.Issue.Key,
                        Title = d.Issue.Title,
                        StatusId = d.Issue.StatusId,
                        StatusCategory = d.Issue.Status.Category
                    })
                    .ToList(),
                NeedsRerun = r.NeedsRerun
            });
        }

        return dtos;
    }

    private static TestCycleDto MapTestCycleDto(TestCycle c)
    {
        var runs = c.Runs;
        return new TestCycleDto
        {
            CycleId = c.Id,
            ProjectId = c.ProjectId,
            Name = c.Name,
            SprintId = c.SprintId,
            SprintName = c.Sprint?.Name,
            Status = c.Status,
            Stats = new TestCycleStatsDto
            {
                Total = runs.Count,
                Pass = runs.Count(r => r.Result == TestRunResults.Pass),
                Fail = runs.Count(r => r.Result == TestRunResults.Fail),
                Blocked = runs.Count(r => r.Result == TestRunResults.Blocked),
                NotRun = runs.Count(r => r.Result == TestRunResults.NotRun),
                NeedsRerun = runs.Count(r => r.NeedsRerun)
            },
            CreatedBy = MapUserRef(c.CreatedBy),
            CreatedAt = FormatUtc(c.CreatedAt)
        };
    }

    private static List<TestStepDto> NormalizeSteps(List<TestStepInputDto> inputs) =>
        inputs.Select((s, idx) => new TestStepDto
        {
            StepNumber = idx + 1,
            Action = s.Action?.Trim() ?? string.Empty,
            TestData = string.IsNullOrWhiteSpace(s.TestData) ? null : s.TestData.Trim(),
            Expected = s.Expected?.Trim() ?? string.Empty
        }).ToList();

    private static UserRefDto MapUserRef(User u) => new()
    {
        UserId = u.Id,
        FullName = u.FullName,
        Email = u.Email,
        AvatarUrl = u.AvatarUrl
    };

    private static string FormatUtc(DateTime dt) =>
        DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    private static T? SafeDeserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch { return default; }
    }
}
