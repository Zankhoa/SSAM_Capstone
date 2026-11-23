using Microsoft.AspNetCore.Http;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class UpdateIssueRequirementsAsyncTests : KanbanServiceTestBase
{
    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: Linking valid requirements replaces old links, clears ReqChanged, updates timestamp, and logs activity.
    [Fact]
    public async Task UpdateIssueRequirementsAsync_WithValidRefs_ReplacesLinksAndRecordsActivity()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);
        issue.ReqChanged = true;
        Db.IssueRequirementLinks.Add(new IssueRequirementLink
        {
            IssueId = issue.Id,
            DocumentId = 999,
            ReqUid = "REQ-OLD",
            ReqHash = "old-hash",
            LinkedAt = Clock.GetUtcNow().UtcDateTime.AddDays(-1),
            LinkedById = 1
        });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var doc1 = SeedRequirementDocument(documentId: 500, versionId: 501, reqUid: "REQ-001");
        var doc2 = SeedRequirementDocument(documentId: 510, versionId: 511, reqUid: "REQ-002");

        var result = await KanbanService.UpdateIssueRequirementsAsync(issue.Id, new UpdateIssueRequirementsRequest
        {
            Refs = new List<RequirementRefInputDto>
            {
                new() { DocumentId = doc1.Id, ReqUid = " req-001 " },
                new() { DocumentId = doc2.Id, ReqUid = "REQ-002" }
            }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);

        var updated = Db.Issues.Single(i => i.Id == issue.Id);
        Assert.False(updated.ReqChanged);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, updated.UpdatedAt);

        var links = Db.IssueRequirementLinks.Where(l => l.IssueId == issue.Id).OrderBy(l => l.ReqUid).ToList();
        Assert.Equal(2, links.Count);
        Assert.Equal("REQ-001", links[0].ReqUid);
        Assert.Equal("REQ-002", links[1].ReqUid);
        Assert.DoesNotContain(links, l => l.ReqUid == "REQ-OLD");

        var activity = Assert.Single(Db.IssueActivities.Where(a => a.IssueId == issue.Id));
        Assert.Equal(IssueActivityActions.ReqLinked, activity.Action);
        Assert.Equal("Liên kết 2 yêu cầu ( REQ-001 , REQ-002)", activity.Note);
    }

    // UTC002: Passing an empty Refs list unlinks all existing requirements and logs unlink activity.
    [Fact]
    public async Task UpdateIssueRequirementsAsync_WithEmptyRefs_RemovesAllLinksAndLogsUnlinkActivity()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);
        issue.ReqChanged = true;
        var doc = SeedRequirementDocument(documentId: 500, versionId: 501, reqUid: "REQ-001");
        Db.IssueRequirementLinks.Add(new IssueRequirementLink
        {
            IssueId = issue.Id,
            DocumentId = doc.Id,
            ReqUid = "REQ-001",
            ReqHash = "hash-1",
            LinkedAt = Clock.GetUtcNow().UtcDateTime,
            LinkedById = 1
        });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.UpdateIssueRequirementsAsync(issue.Id, new UpdateIssueRequirementsRequest
        {
            Refs = new List<RequirementRefInputDto>()
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(Db.IssueRequirementLinks.Where(l => l.IssueId == issue.Id));
        Assert.False(Db.Issues.Single(i => i.Id == issue.Id).ReqChanged);

        var activity = Assert.Single(Db.IssueActivities.Where(a => a.IssueId == issue.Id));
        Assert.Equal(IssueActivityActions.ReqLinked, activity.Action);
        Assert.Equal("Gỡ liên kết yêu cầu", activity.Note);
    }

    // UTC003: Duplicate requirement references (case-insensitive / whitespace) persist only one IssueRequirementLink.
    [Fact]
    public async Task UpdateIssueRequirementsAsync_WithDuplicateRefs_DeduplicatesPersistedLinks()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);
        var doc = SeedRequirementDocument(documentId: 500, versionId: 501, reqUid: "REQ-001");

        var result = await KanbanService.UpdateIssueRequirementsAsync(issue.Id, new UpdateIssueRequirementsRequest
        {
            Refs = new List<RequirementRefInputDto>
            {
                new() { DocumentId = doc.Id, ReqUid = "REQ-001" },
                new() { DocumentId = doc.Id, ReqUid = "  req-001  " }
            }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var link = Assert.Single(Db.IssueRequirementLinks.Where(l => l.IssueId == issue.Id));
        Assert.Equal("REQ-001", link.ReqUid);
    }

    // UTC004: Resolves requirements from BaselineVersionId when present, or falls back to CurrentVersionId when null.
    [Fact]
    public async Task UpdateIssueRequirementsAsync_ResolvesFromBaselineOrFallsBackToCurrentVersion()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);

        // Doc 1: BaselineVersionId = 501 ("REQ-BASE"), CurrentVersionId = 511
        var baselineDoc = SeedRequirementDocument(documentId: 500, versionId: 501, reqUid: "REQ-BASE");
        // Doc 2: BaselineVersionId = null, CurrentVersionId = 511 ("REQ-CURR")
        var draftDoc = SeedRequirementDocument(documentId: 510, versionId: 511, reqUid: "REQ-CURR");
        baselineDoc.CurrentVersionId = 511;
        draftDoc.BaselineVersionId = null;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.UpdateIssueRequirementsAsync(issue.Id, new UpdateIssueRequirementsRequest
        {
            Refs = new List<RequirementRefInputDto>
            {
                new() { DocumentId = baselineDoc.Id, ReqUid = "REQ-BASE" },
                new() { DocumentId = draftDoc.Id, ReqUid = "REQ-CURR" }
            }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, Db.IssueRequirementLinks.Count(l => l.IssueId == issue.Id));
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC005: Missing or soft-deleted issue returns NotFound ("ISSUE_NOT_FOUND").
    [Theory]
    [InlineData(false)] // Issue does not exist
    [InlineData(true)]  // Issue is soft-deleted
    public async Task UpdateIssueRequirementsAsync_WhenIssueMissingOrDeleted_ReturnsNotFound(bool seedDeleted)
    {
        SeedWorkflow();
        long targetId = 9999;
        if (seedDeleted)
        {
            var deleted = SeedIssue(issueId: 200);
            deleted.IsDeleted = true;
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
            targetId = deleted.Id;
        }

        var result = await KanbanService.UpdateIssueRequirementsAsync(targetId, new UpdateIssueRequirementsRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("ISSUE_NOT_FOUND", result.ErrorCode);
    }

    // UTC006: Caller lacking IssueUpdate permission returns Forbidden without altering links.
    [Fact]
    public async Task UpdateIssueRequirementsAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);
        AccessGuardMock
            .Setup(g => g.EnsureProjectPermissionAsync(100, PermissionKeys.IssueUpdate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Forbidden("FORBIDDEN", "Permission denied."));

        var result = await KanbanService.UpdateIssueRequirementsAsync(issue.Id, new UpdateIssueRequirementsRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    // UTC007: Missing, soft-deleted, or cross-project document returns NotFound ("DOCUMENT_NOT_FOUND").
    [Theory]
    [InlineData("missing")]
    [InlineData("deleted")]
    [InlineData("other_project")]
    public async Task UpdateIssueRequirementsAsync_WhenDocumentInvalid_ReturnsNotFound(string scenario)
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);
        long docId = 9999;

        if (scenario == "deleted")
        {
            var doc = SeedRequirementDocument(documentId: 500, versionId: 501, reqUid: "REQ-001");
            doc.IsDeleted = true;
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
            docId = doc.Id;
        }
        else if (scenario == "other_project")
        {
            var doc = SeedRequirementDocument(documentId: 500, versionId: 501, reqUid: "REQ-001");
            doc.ProjectId = 999;
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
            docId = doc.Id;
        }

        var result = await KanbanService.UpdateIssueRequirementsAsync(issue.Id, new UpdateIssueRequirementsRequest
        {
            Refs = new List<RequirementRefInputDto>
            {
                new() { DocumentId = docId, ReqUid = "REQ-001" }
            }
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
    }

    // UTC008: Non-existent ReqUid or document without any version returns NotFound ("REQUIREMENT_NOT_FOUND").
    [Theory]
    [InlineData(false)] // Unknown ReqUid in valid version
    [InlineData(true)]  // Both BaselineVersionId and CurrentVersionId are null
    public async Task UpdateIssueRequirementsAsync_WhenRequirementOrVersionMissing_ReturnsNotFound(bool clearVersions)
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);
        var doc = SeedRequirementDocument(documentId: 500, versionId: 501, reqUid: "REQ-001");
        if (clearVersions)
        {
            doc.BaselineVersionId = null;
            doc.CurrentVersionId = null;
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var result = await KanbanService.UpdateIssueRequirementsAsync(issue.Id, new UpdateIssueRequirementsRequest
        {
            Refs = new List<RequirementRefInputDto>
            {
                new() { DocumentId = doc.Id, ReqUid = clearVersions ? "REQ-001" : "REQ-404" }
            }
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("REQUIREMENT_NOT_FOUND", result.ErrorCode);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC009: Linking a single requirement reference (minimum non-empty batch = 1) succeeds.
    [Fact]
    public async Task UpdateIssueRequirementsAsync_WithSingleRef_CreatesOneLink()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);
        var doc = SeedRequirementDocument(documentId: 500, versionId: 501, reqUid: "REQ-001");

        var result = await KanbanService.UpdateIssueRequirementsAsync(issue.Id, new UpdateIssueRequirementsRequest
        {
            Refs = new List<RequirementRefInputDto>
            {
                new() { DocumentId = doc.Id, ReqUid = "REQ-001" }
            }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(Db.IssueRequirementLinks.Where(l => l.IssueId == issue.Id));
    }

    // UTC010: Re-linking a requirement whose ChangedVersionLabel was set clears the stale version label and ReqChanged flag.
    [Fact]
    public async Task UpdateIssueRequirementsAsync_WhenRelinkingStaleRequirement_ClearsChangedVersionLabel()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);
        issue.ReqChanged = true;
        var doc = SeedRequirementDocument(documentId: 500, versionId: 501, reqUid: "REQ-001");
        Db.IssueRequirementLinks.Add(new IssueRequirementLink
        {
            IssueId = issue.Id,
            DocumentId = doc.Id,
            ReqUid = "REQ-001",
            ReqHash = "outdated-hash",
            ChangedVersionLabel = "2.0",
            LinkedAt = Clock.GetUtcNow().UtcDateTime.AddHours(-5),
            LinkedById = 1
        });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.UpdateIssueRequirementsAsync(issue.Id, new UpdateIssueRequirementsRequest
        {
            Refs = new List<RequirementRefInputDto>
            {
                new() { DocumentId = doc.Id, ReqUid = "REQ-001" }
            }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var link = Assert.Single(Db.IssueRequirementLinks.Where(l => l.IssueId == issue.Id));
        Assert.Null(link.ChangedVersionLabel);
        Assert.Equal("hash-1", link.ReqHash);
        Assert.False(Db.Issues.Single(i => i.Id == issue.Id).ReqChanged);
    }
}