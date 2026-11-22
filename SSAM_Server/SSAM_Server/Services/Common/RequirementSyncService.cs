using Microsoft.EntityFrameworkCore;
using SSAM_Server.Data;
using SSAM_Server.Interfaces.Services;
using SSAM_Server.Models;

namespace SSAM_Server.Services.Common;

/// <summary>
/// Handles cross-module synchronization when document requirements change or are confirmed fresh.
/// </summary>
public class RequirementSyncService : IRequirementSyncService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;

    public RequirementSyncService(AppDbContext db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task PropagateBaselineChangesAsync(
        DocumentNode node,
        DocumentVersion approvedVersion,
        IReadOnlyList<RequirementItem> newReqs,
        long actorUserId,
        DateTime now,
        CancellationToken ct = default)
    {
        var documentId = node.Id;
        var newReqByUid = newReqs.ToDictionary(r => r.ReqUid, StringComparer.OrdinalIgnoreCase);

        var issueLinks = await _db.IssueRequirementLinks
            .Include(l => l.Issue)
            .Where(l => l.DocumentId == documentId)
            .ToListAsync(ct);

        foreach (var link in issueLinks)
        {
            var isStale = !newReqByUid.TryGetValue(link.ReqUid, out var newReq) ||
                          !string.Equals(link.ReqHash, newReq.ContentHash, StringComparison.OrdinalIgnoreCase);

            if (isStale)
            {
                link.ChangedVersionLabel = approvedVersion.Label;
                if (!link.Issue.IsDeleted)
                {
                    link.Issue.ReqChanged = true;
                    link.Issue.UpdatedAt = now;
                    _db.IssueActivities.Add(new IssueActivity
                    {
                        IssueId = link.IssueId,
                        Action = IssueActivityActions.ReqChanged,
                        ActorId = actorUserId,
                        Note = $"{link.ReqUid} cập nhật ở {node.Code ?? node.Title} v{approvedVersion.Label}",
                        CreatedAt = now
                    });

                    if (link.Issue.AssigneeId.HasValue && link.Issue.AssigneeId.Value != actorUserId)
                    {
                        await _notifications.NotifyAsync(
                            link.Issue.AssigneeId.Value,
                            "ISSUE_REQ_CHANGED",
                            $"Cảnh báo yêu cầu thay đổi: {link.Issue.Key}",
                            $"{link.ReqUid} đã cập nhật ở {node.Code ?? node.Title} v{approvedVersion.Label}",
                            $"/projects/{node.ProjectId}/kanban?issue={Uri.EscapeDataString(link.Issue.Key)}",
                            ct);
                    }
                }
            }
        }

        var tcLinks = await _db.TestCaseRequirementLinks
            .Include(l => l.TestCase)
                .ThenInclude(tc => tc.Plan)
            .Where(l => l.DocumentId == documentId)
            .ToListAsync(ct);

        var docKeyPrefix = !string.IsNullOrWhiteSpace(node.Code) ? node.Code : $"DOC-{node.Id}";
        var affectedCases = new List<(TestCase Case, string ReqKey)>();
        foreach (var link in tcLinks)
        {
            var isStale = !newReqByUid.TryGetValue(link.ReqUid, out var newReq) ||
                          !string.Equals(link.ReqHash, newReq.ContentHash, StringComparison.OrdinalIgnoreCase);

            if (isStale)
            {
                link.ChangedVersionLabel = approvedVersion.Label;
                if (link.TestCase.Status == TestCaseStatuses.Active &&
                    link.TestCase.Plan.Status == TestPlanStatuses.Open)
                {
                    link.TestCase.UpdatedAt = now;
                    affectedCases.Add((link.TestCase, $"{docKeyPrefix}:{link.ReqUid}"));
                }
            }
        }

        if (affectedCases.Count > 0)
        {
            var affectedCaseIds = affectedCases.Select(x => x.Case.Id).Distinct().ToList();
            var testedPairs = await _db.TestResults
                .AsNoTracking()
                .Include(r => r.Environment)
                .Where(r => affectedCaseIds.Contains(r.TestCaseId) &&
                            r.Result != TestResultValues.Untest &&
                            !r.Environment.IsDeleted)
                .Select(r => new { r.TestCaseId, r.EnvId })
                .Distinct()
                .ToListAsync(ct);

            var envsByCase = testedPairs
                .GroupBy(p => p.TestCaseId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.EnvId).ToList());

            var existingFlags = await _db.TestRetestFlags
                .Where(f => affectedCaseIds.Contains(f.TestCaseId))
                .ToListAsync(ct);
            var flagMap = existingFlags.ToDictionary(f => (f.TestCaseId, f.EnvId));

            foreach (var (tc, reqKey) in affectedCases)
            {
                if (!envsByCase.TryGetValue(tc.Id, out var envIds) || envIds.Count == 0)
                    continue;

                foreach (var envId in envIds)
                {
                    if (flagMap.TryGetValue((tc.Id, envId), out var existingFlag))
                    {
                        existingFlag.Reason = TestRetestReasons.ReqChanged;
                        existingFlag.ReqKey = reqKey;
                        existingFlag.BugIssueId = null;
                        existingFlag.Build = null;
                        existingFlag.At = now;
                    }
                    else
                    {
                        var newFlag = new TestRetestFlag
                        {
                            TestCaseId = tc.Id,
                            EnvId = envId,
                            Reason = TestRetestReasons.ReqChanged,
                            ReqKey = reqKey,
                            BugIssueId = null,
                            Build = null,
                            At = now
                        };
                        _db.TestRetestFlags.Add(newFlag);
                        flagMap[(tc.Id, envId)] = newFlag;
                    }
                }

                _db.TestActivities.Add(new TestActivity
                {
                    PlanId = tc.PlanId,
                    Type = TestActivityTypes.Req,
                    At = now,
                    ActorId = null,
                    ModuleId = tc.ModuleId,
                    TestCaseId = tc.Id,
                    Text = $"{reqKey} đổi ở bản {approvedVersion.Label} → cần test lại"
                });
            }

            var moduleIds = affectedCases.Select(x => x.Case.ModuleId).Distinct().ToList();
            var assignedUserIds = await _db.TestAssignments
                .AsNoTracking()
                .Where(a => moduleIds.Contains(a.ModuleId))
                .Select(a => a.AssigneeId)
                .Distinct()
                .ToListAsync(ct);

            if (assignedUserIds.Count == 0)
            {
                assignedUserIds = await _db.ProjectMembers
                    .AsNoTracking()
                    .Where(pm => pm.ProjectId == node.ProjectId && pm.IsActive)
                    .Select(pm => pm.UserId)
                    .Distinct()
                    .ToListAsync(ct);
            }

            var sampleCodes = string.Join(", ", affectedCases.Select(x => x.Case.Code).Distinct().Take(3));
            var msg = $"{affectedCases.Select(x => x.Case.Id).Distinct().Count()} test case bị ảnh hưởng ({sampleCodes}) do {node.Code ?? node.Title} v{approvedVersion.Label} thay đổi.";
            foreach (var uid in assignedUserIds)
            {
                if (uid == actorUserId) continue;
                await _notifications.NotifyAsync(
                    uid,
                    "TESTCASE_REQ_CHANGED",
                    $"Cảnh báo tài liệu ảnh hưởng Test Case",
                    msg,
                    $"/projects/{node.ProjectId}/tests",
                    ct);
            }
        }
    }

    public async Task ConfirmRequirementFreshAsync(
        long documentId,
        string normalizedReqUid,
        string latestContentHash,
        DateTime now,
        CancellationToken ct = default)
    {
        var issueLinks = await _db.IssueRequirementLinks
            .Include(l => l.Issue)
                .ThenInclude(i => i.RequirementLinks)
            .Where(l => l.DocumentId == documentId && l.ReqUid == normalizedReqUid)
            .ToListAsync(ct);

        foreach (var link in issueLinks)
        {
            link.ReqHash = latestContentHash;
            link.ChangedVersionLabel = null;
            link.Issue.ReqChanged = link.Issue.RequirementLinks.Any(rl => rl.ChangedVersionLabel != null);
            link.Issue.UpdatedAt = now;
        }

        var tcLinks = await _db.TestCaseRequirementLinks
            .Include(l => l.TestCase)
                .ThenInclude(tc => tc.RequirementLinks)
            .Where(l => l.DocumentId == documentId && l.ReqUid == normalizedReqUid)
            .ToListAsync(ct);

        var clearedCaseIds = new List<long>();
        foreach (var link in tcLinks)
        {
            link.ReqHash = latestContentHash;
            link.ChangedVersionLabel = null;
            link.TestCase.UpdatedAt = now;
            if (!link.TestCase.RequirementLinks.Any(rl => rl.ChangedVersionLabel != null))
            {
                clearedCaseIds.Add(link.TestCaseId);
            }
        }

        if (clearedCaseIds.Count > 0)
        {
            var reqFlags = await _db.TestRetestFlags
                .Where(f => clearedCaseIds.Contains(f.TestCaseId) && f.Reason == TestRetestReasons.ReqChanged)
                .ToListAsync(ct);
            if (reqFlags.Count > 0)
            {
                _db.TestRetestFlags.RemoveRange(reqFlags);
            }
        }
    }

    public async Task<HashSet<string>> GetStaleRequirementUidsAsync(long documentId, CancellationToken ct = default)
    {
        var staleUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var issueStale = await _db.IssueRequirementLinks
            .AsNoTracking()
            .Where(l => l.DocumentId == documentId && l.ChangedVersionLabel != null)
            .Select(l => l.ReqUid)
            .ToListAsync(ct);
        foreach (var u in issueStale) staleUids.Add(u);

        var tcStale = await _db.TestCaseRequirementLinks
            .AsNoTracking()
            .Where(l => l.DocumentId == documentId && l.ChangedVersionLabel != null)
            .Select(l => l.ReqUid)
            .ToListAsync(ct);
        foreach (var u in tcStale) staleUids.Add(u);

        return staleUids;
    }
}
