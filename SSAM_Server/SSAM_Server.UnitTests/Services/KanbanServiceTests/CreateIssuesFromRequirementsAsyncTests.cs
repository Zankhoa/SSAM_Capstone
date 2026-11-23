using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class CreateIssuesFromRequirementsAsyncTests : KanbanServiceTestBase
{
    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: Creating issues from valid Items creates issues with defaults, requirement links, and activities.
    [Fact]
    public async Task CreateIssuesFromRequirementsAsync_WithValidItems_CreatesIssuesLinksAndActivities()
    {
        var (_, _, todo, _) = SeedWorkflow();
        var doc1 = SeedRequirementDocument(documentId: 500, versionId: 501, reqUid: "REQ-001");
        var doc2 = SeedRequirementDocument(documentId: 510, versionId: 511, reqUid: "REQ-002");

        var result = await KanbanService.CreateIssuesFromRequirementsAsync(100, new CreateIssuesFromRequirementsRequest
        {
            Items = new List<CreateIssueFromRequirementItemDto>
            {
                new() { DocumentId = doc1.Id, ReqUid = " req-001 " },
                new() { DocumentId = doc2.Id, ReqUid = "REQ-002", Type = "bug", Priority = "high", Points = 5 }
            }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(2, result.Data!.Items.Count);

        var issues = Db.Issues.Where(i => i.ProjectId == 100).OrderBy(i => i.Id).ToList();
        Assert.Equal(2, issues.Count);

        Assert.Equal(IssueTypes.Story, issues[0].Type);
        Assert.Equal(IssuePriorities.Medium, issues[0].Priority);
        Assert.Equal(todo.Id, issues[0].StatusId);
        Assert.Equal("REQ-001: User authentication", issues[0].Title);
        Assert.Equal("The user can authenticate.", issues[0].Description);
        Assert.Equal("00001000", issues[0].Rank);

        Assert.Equal(IssueTypes.Bug, issues[1].Type);
        Assert.Equal(IssuePriorities.High, issues[1].Priority);
        Assert.Equal(5, issues[1].Points);
        Assert.Equal("REQ-002: User authentication", issues[1].Title);
        Assert.Equal("00002000", issues[1].Rank);

        Assert.Equal(2, Db.IssueRequirementLinks.Count());
        Assert.Equal(2, Db.IssueActivities.Count(a => a.Action == IssueActivityActions.Created));
    }

    // UTC002: Legacy Refs payload is converted into Items and inherits request-level Type and SprintId.
    [Fact]
    public async Task CreateIssuesFromRequirementsAsync_WithRefsPayload_MapsRequestTypeAndSprintId()
    {
        SeedWorkflow();
        var sprint = SeedSprint(sprintId: 400);
        var doc = SeedRequirementDocument();

        var result = await KanbanService.CreateIssuesFromRequirementsAsync(100, new CreateIssuesFromRequirementsRequest
        {
            Type = "task",
            SprintId = sprint.Id,
            Refs = new List<RequirementRefInputDto>
            {
                new() { DocumentId = doc.Id, ReqUid = "REQ-001" }
            }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);

        var created = Assert.Single(Db.Issues.Where(i => i.ProjectId == 100));
        Assert.Equal(IssueTypes.Task, created.Type);
        Assert.Equal(sprint.Id, created.SprintId);
    }

    // UTC003: Setting CreateSubtasks = true generates child Task issues for each non-empty acceptance criterion.
    [Fact]
    public async Task CreateIssuesFromRequirementsAsync_WithCreateSubtasksTrue_CreatesSubtasksFromAcceptanceCriteria()
    {
        var (_, _, todo, _) = SeedWorkflow();
        var sprint = SeedSprint(sprintId: 400);
        var doc = SeedRequirementDocument();

        var seededReq = Db.RequirementItems.Single(r => r.DocumentId == doc.Id);
        seededReq.AcceptanceCriteria = JsonSerializer.Serialize(new[] { "AC 1: Validate input", "   ", "AC 2: Persist record" });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.CreateIssuesFromRequirementsAsync(100, new CreateIssuesFromRequirementsRequest
        {
            Items = new List<CreateIssueFromRequirementItemDto>
            {
                new()
                {
                    DocumentId = doc.Id,
                    ReqUid = "REQ-001",
                    Priority = IssuePriorities.High,
                    SprintId = sprint.Id,
                    AssigneeId = 1,
                    CreateSubtasks = true
                }
            }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var parent = Assert.Single(Db.Issues.Where(i => i.ParentId == null));
        var subtasks = Db.Issues.Where(i => i.ParentId == parent.Id).OrderBy(i => i.Id).ToList();

        Assert.Equal(2, subtasks.Count);
        Assert.Equal("AC 1: Validate input", subtasks[0].Title);
        Assert.Equal("AC 2: Persist record", subtasks[1].Title);
        Assert.All(subtasks, s =>
        {
            Assert.Equal(IssueTypes.Task, s.Type);
            Assert.Equal(IssuePriorities.High, s.Priority);
            Assert.Equal(todo.Id, s.StatusId);
            Assert.Equal(sprint.Id, s.SprintId);
            Assert.Equal(1, s.AssigneeId);
        });
    }

    // UTC004: BaselineVersionId takes precedence over CurrentVersionId when resolving RequirementItems.
    [Fact]
    public async Task CreateIssuesFromRequirementsAsync_WhenDocumentHasBaselineVersion_ResolvesFromBaselineVersion()
    {
        SeedWorkflow();
        var baselineDoc = SeedRequirementDocument(documentId: 500, versionId: 501, reqUid: "REQ-BASE");
        SeedRequirementDocument(documentId: 510, versionId: 511, reqUid: "REQ-CURR");

        // Point baselineDoc's CurrentVersionId to 511 while keeping BaselineVersionId = 501
        baselineDoc.CurrentVersionId = 511;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.CreateIssuesFromRequirementsAsync(100, new CreateIssuesFromRequirementsRequest
        {
            Items = new List<CreateIssueFromRequirementItemDto>
            {
                new() { DocumentId = baselineDoc.Id, ReqUid = "REQ-BASE" }
            }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var created = Assert.Single(Db.Issues.Where(i => i.ProjectId == 100));
        Assert.Equal("REQ-BASE: User authentication", created.Title);
    }

    // UTC005: Unrecognized Type or Priority values fall back gracefully to STORY and MEDIUM.
    [Fact]
    public async Task CreateIssuesFromRequirementsAsync_WithInvalidTypeOrPriority_FallsBackToStoryAndMedium()
    {
        SeedWorkflow();
        var doc = SeedRequirementDocument();

        var result = await KanbanService.CreateIssuesFromRequirementsAsync(100, new CreateIssuesFromRequirementsRequest
        {
            Items = new List<CreateIssueFromRequirementItemDto>
            {
                new()
                {
                    DocumentId = doc.Id,
                    ReqUid = "REQ-001",
                    Type = "UNKNOWN_TYPE",
                    Priority = "UNKNOWN_PRIORITY"
                }
            }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var created = Assert.Single(Db.Issues.Where(i => i.ProjectId == 100));
        Assert.Equal(IssueTypes.Story, created.Type);
        Assert.Equal(IssuePriorities.Medium, created.Priority);
    }

    // UTC006: Document without Code uses Document.Title in the created IssueActivity note.
    [Fact]
    public async Task CreateIssuesFromRequirementsAsync_WhenDocumentCodeIsNull_UsesDocumentTitleInActivityNote()
    {
        SeedWorkflow();
        var doc = SeedRequirementDocument();
        doc.Code = null;
        doc.Title = "System Specification";
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.CreateIssuesFromRequirementsAsync(100, new CreateIssuesFromRequirementsRequest
        {
            Items = new List<CreateIssueFromRequirementItemDto>
            {
                new() { DocumentId = doc.Id, ReqUid = "REQ-001" }
            }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var activity = Assert.Single(Db.IssueActivities);
        Assert.Equal("Tạo từ yêu cầu REQ-001 (System Specification)", activity.Note);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC007: Caller lacking IssueCreate permission returns Forbidden without persisting issues.
    [Fact]
    public async Task CreateIssuesFromRequirementsAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        SeedWorkflow();
        var doc = SeedRequirementDocument();
        AccessGuardMock
            .Setup(g => g.EnsureProjectPermissionAsync(100, PermissionKeys.IssueCreate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Forbidden("FORBIDDEN", "Permission denied."));

        var result = await KanbanService.CreateIssuesFromRequirementsAsync(100, new CreateIssuesFromRequirementsRequest
        {
            Items = new List<CreateIssueFromRequirementItemDto>
            {
                new() { DocumentId = doc.Id, ReqUid = "REQ-001" }
            }
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Empty(Db.Issues);
    }

    // UTC008: Empty Items and empty/null Refs return ValidationFailure on "items".
    [Fact]
    public async Task CreateIssuesFromRequirementsAsync_WhenItemsAndRefsAreEmpty_ReturnsValidationFailure()
    {
        SeedWorkflow();

        var result = await KanbanService.CreateIssuesFromRequirementsAsync(100, new CreateIssuesFromRequirementsRequest
        {
            Items = new List<CreateIssueFromRequirementItemDto>(),
            Refs = null
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("items", result.Errors!.Keys);
    }

    // UTC009: Project with no active workflow statuses returns NotFound ("STATUS_NOT_FOUND").
    [Fact]
    public async Task CreateIssuesFromRequirementsAsync_WhenProjectHasNoActiveStatuses_ReturnsNotFound()
    {
        SeedProject();
        var doc = SeedRequirementDocument();

        var result = await KanbanService.CreateIssuesFromRequirementsAsync(100, new CreateIssuesFromRequirementsRequest
        {
            Items = new List<CreateIssueFromRequirementItemDto>
            {
                new() { DocumentId = doc.Id, ReqUid = "REQ-001" }
            }
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("STATUS_NOT_FOUND", result.ErrorCode);
    }

    // UTC010: Missing or soft-deleted DocumentNode returns NotFound ("DOCUMENT_NOT_FOUND").
    [Theory]
    [InlineData(false)] // Document ID does not exist
    [InlineData(true)]  // Document is soft-deleted
    public async Task CreateIssuesFromRequirementsAsync_WhenDocumentMissingOrDeleted_ReturnsNotFound(bool seedDeleted)
    {
        SeedWorkflow();
        long docId = 9999;
        if (seedDeleted)
        {
            var doc = SeedRequirementDocument();
            doc.IsDeleted = true;
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
            docId = doc.Id;
        }

        var result = await KanbanService.CreateIssuesFromRequirementsAsync(100, new CreateIssuesFromRequirementsRequest
        {
            Items = new List<CreateIssueFromRequirementItemDto>
            {
                new() { DocumentId = docId, ReqUid = "REQ-001" }
            }
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
    }

    // UTC011: Document belonging to another project returns NotFound ("DOCUMENT_NOT_FOUND").
    [Fact]
    public async Task CreateIssuesFromRequirementsAsync_WhenDocumentBelongsToAnotherProject_ReturnsNotFound()
    {
        SeedWorkflow();
        var doc = SeedRequirementDocument();
        doc.ProjectId = 999;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.CreateIssuesFromRequirementsAsync(100, new CreateIssuesFromRequirementsRequest
        {
            Items = new List<CreateIssueFromRequirementItemDto>
            {
                new() { DocumentId = doc.Id, ReqUid = "REQ-001" }
            }
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
    }

    // UTC012: Missing RequirementItem or Document without any version returns NotFound ("REQUIREMENT_NOT_FOUND").
    [Theory]
    [InlineData(false)] // ReqUid does not exist in version
    [InlineData(true)]  // Both BaselineVersionId and CurrentVersionId are null
    public async Task CreateIssuesFromRequirementsAsync_WhenRequirementOrVersionMissing_ReturnsNotFound(bool clearVersions)
    {
        SeedWorkflow();
        var doc = SeedRequirementDocument();
        if (clearVersions)
        {
            doc.BaselineVersionId = null;
            doc.CurrentVersionId = null;
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var result = await KanbanService.CreateIssuesFromRequirementsAsync(100, new CreateIssuesFromRequirementsRequest
        {
            Items = new List<CreateIssueFromRequirementItemDto>
            {
                new() { DocumentId = doc.Id, ReqUid = clearVersions ? "REQ-001" : "REQ-UNKNOWN" }
            }
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("REQUIREMENT_NOT_FOUND", result.ErrorCode);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC013: Single item batch (minimum valid count = 1) succeeds and returns 1 created issue.
    [Fact]
    public async Task CreateIssuesFromRequirementsAsync_WithSingleItem_CreatesOneIssue()
    {
        SeedWorkflow();
        var doc = SeedRequirementDocument();

        var result = await KanbanService.CreateIssuesFromRequirementsAsync(100, new CreateIssuesFromRequirementsRequest
        {
            Items = new List<CreateIssueFromRequirementItemDto>
            {
                new() { DocumentId = doc.Id, ReqUid = "REQ-001" }
            }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Items);
    }

    // UTC014: Subtask title derived from an Acceptance Criterion > 200 chars is truncated to 200 characters.
    [Fact]
    public async Task CreateIssuesFromRequirementsAsync_WithLongAcceptanceCriterion_TruncatesSubtaskTitleTo200Chars()
    {
        SeedWorkflow();
        var doc = SeedRequirementDocument();
        var seededReq = Db.RequirementItems.Single(r => r.DocumentId == doc.Id);
        seededReq.AcceptanceCriteria = JsonSerializer.Serialize(new[] { new string('A', 250) });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.CreateIssuesFromRequirementsAsync(100, new CreateIssuesFromRequirementsRequest
        {
            Items = new List<CreateIssueFromRequirementItemDto>
            {
                new() { DocumentId = doc.Id, ReqUid = "REQ-001", CreateSubtasks = true }
            }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var subtask = Assert.Single(Db.Issues.Where(i => i.ParentId != null));
        Assert.Equal(200, subtask.Title.Length);
        Assert.Equal(new string('A', 200), subtask.Title);
    }

    // UTC015: CreateSubtasks = true with empty or malformed AcceptanceCriteria JSON creates parent issue without subtasks.
    [Theory]
    [InlineData("[]")]
    [InlineData("not-valid-json")]
    public async Task CreateIssuesFromRequirementsAsync_WithEmptyOrInvalidAcceptanceCriteria_CreatesNoSubtasks(string rawAc)
    {
        SeedWorkflow();
        var doc = SeedRequirementDocument();
        var seededReq = Db.RequirementItems.Single(r => r.DocumentId == doc.Id);
        seededReq.AcceptanceCriteria = rawAc;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.CreateIssuesFromRequirementsAsync(100, new CreateIssuesFromRequirementsRequest
        {
            Items = new List<CreateIssueFromRequirementItemDto>
            {
                new() { DocumentId = doc.Id, ReqUid = "REQ-001", CreateSubtasks = true }
            }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(Db.Issues.Where(i => i.ParentId == null));
        Assert.Empty(Db.Issues.Where(i => i.ParentId != null));
    }
}