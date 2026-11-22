using Microsoft.AspNetCore.Http;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class CreateIssueAsyncTests : KanbanServiceTestBase
{
    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: A valid issue is created in the requested workflow status.
    [Fact]
    public async Task CreateIssueAsync_WithValidRequest_ReturnsCreatedIssue()
    {
        var (_, _, todo, _) = SeedWorkflow();

        var result = await KanbanService.CreateIssueAsync(100, new CreateIssueRequest
        {
            Type = IssueTypes.Task,
            Title = "  New issue  ",
            Description = "Description",
            StatusId = todo.Id,
            Priority = IssuePriorities.High,
            Points = 5,
            Labels = new List<string> { "backend" },
            DueDate = "2026-12-01"
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        var issue = Assert.Single(Db.Issues.Where(i => i.ProjectId == 100));
        Assert.Equal("New issue", issue.Title);
        Assert.Equal(IssuePriorities.High, issue.Priority);
        Assert.Equal(new DateOnly(2026, 12, 1), issue.DueDate);
        Assert.Equal("[\"backend\"]", issue.Labels);
        Assert.Single(Db.IssueActivities.Where(a => a.IssueId == issue.Id));
    }

    // UTC002: Omitting optional values selects the initial workflow status and defaults.
    [Fact]
    public async Task CreateIssueAsync_WithoutOptionalValues_UsesInitialStatusAndDefaults()
    {
        var (_, _, todo, _) = SeedWorkflow();

        var result = await KanbanService.CreateIssueAsync(100, new CreateIssueRequest
        {
            Title = "Defaulted issue"
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var issue = Assert.Single(Db.Issues.Where(i => i.ProjectId == 100));
        Assert.Equal(todo.Id, issue.StatusId);
        Assert.Equal(IssueTypes.Task, issue.Type);
        Assert.Equal(IssuePriorities.Medium, issue.Priority);
        Assert.Equal("[]", issue.Labels);
    }

    // UTC003: A valid sprint, parent issue, and requirement reference are persisted together.
    [Fact]
    public async Task CreateIssueAsync_WithSprintParentAndRequirement_CreatesCompleteIssueGraph()
    {
        var (_, _, todo, _) = SeedWorkflow();
        var sprint = SeedSprint();
        var parent = SeedIssue();
        var document = SeedRequirementDocument();

        var result = await KanbanService.CreateIssueAsync(100, new CreateIssueRequest
        {
            Title = "Child issue",
            StatusId = todo.Id,
            SprintId = sprint.Id,
            ParentId = parent.Id,
            RequirementRefs = new List<RequirementRefInputDto>
            {
                new() { DocumentId = document.Id, ReqUid = " req-001 " }
            }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var issue = Assert.Single(Db.Issues.Where(i => i.Title == "Child issue"));
        Assert.Equal(sprint.Id, issue.SprintId);
        Assert.Equal(parent.Id, issue.ParentId);
        var link = Assert.Single(Db.IssueRequirementLinks);
        Assert.Equal(issue.Id, link.IssueId);
        Assert.Equal("REQ-001", link.ReqUid);
    }

    // UTC004: A DONE status sets the resolved timestamp during creation.
    [Fact]
    public async Task CreateIssueAsync_WithDoneStatus_SetsResolvedAt()
    {
        var (_, _, _, done) = SeedWorkflow();

        var result = await KanbanService.CreateIssueAsync(100, new CreateIssueRequest
        {
            Title = "Completed issue",
            StatusId = done.Id
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var issue = Assert.Single(Db.Issues.Where(i => i.Title == "Completed issue"));
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, issue.ResolvedAt);
    }

    // UTC005: Duplicate requirement references create only one link.
    [Fact]
    public async Task CreateIssueAsync_WithDuplicateRequirementRefs_CreatesOneLink()
    {
        SeedWorkflow();
        var document = SeedRequirementDocument();

        var result = await KanbanService.CreateIssueAsync(100, new CreateIssueRequest
        {
            Title = "Linked issue",
            RequirementRefs = new List<RequirementRefInputDto>
            {
                new() { DocumentId = document.Id, ReqUid = "REQ-001" },
                new() { DocumentId = document.Id, ReqUid = " req-001 " }
            }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(Db.IssueRequirementLinks);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC006: An access failure prevents issue creation.
    [Fact]
    public async Task CreateIssueAsync_WhenPermissionIsDenied_ReturnsForbidden()
    {
        AccessGuardMock.Setup(g => g.EnsureProjectPermissionAsync(100, PermissionKeys.IssueCreate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Forbidden("FORBIDDEN", "Permission denied."));

        var result = await KanbanService.CreateIssueAsync(100, new CreateIssueRequest { Title = "Issue" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Empty(Db.Issues);
    }

    // UTC007: Invalid scalar fields are rejected before persistence.
    [Fact]
    public async Task CreateIssueAsync_WhenScalarFieldsAreInvalid_ReturnsValidationErrors()
    {
        SeedWorkflow();

        var result = await KanbanService.CreateIssueAsync(100, new CreateIssueRequest
        {
            Type = "INVALID",
            Title = "",
            Priority = "INVALID",
            Points = 101,
            DueDate = "not-a-date"
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("type", result.Errors!.Keys);
        Assert.Contains("title", result.Errors.Keys);
        Assert.Contains("priority", result.Errors.Keys);
        Assert.Contains("points", result.Errors.Keys);
        Assert.Contains("dueDate", result.Errors.Keys);
        Assert.Empty(Db.Issues);
    }

    // UTC008: A status from another project cannot be used.
    [Fact]
    public async Task CreateIssueAsync_WhenStatusDoesNotBelongToProject_ReturnsNotFound()
    {
        SeedWorkflow();

        var result = await KanbanService.CreateIssueAsync(100, new CreateIssueRequest
        {
            Title = "Issue",
            StatusId = 9999
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("STATUS_NOT_FOUND", result.ErrorCode);
    }

    // UTC009: A sprint from another project cannot be assigned.
    [Fact]
    public async Task CreateIssueAsync_WhenSprintDoesNotBelongToProject_ReturnsNotFound()
    {
        SeedWorkflow();
        SeedSprint(projectId: 200);

        var result = await KanbanService.CreateIssueAsync(100, new CreateIssueRequest
        {
            Title = "Issue",
            SprintId = 400
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("SPRINT_NOT_FOUND", result.ErrorCode);
    }

    // UTC010: A missing parent issue cannot be assigned.
    [Fact]
    public async Task CreateIssueAsync_WhenParentIsMissing_ReturnsNotFound()
    {
        SeedWorkflow();

        var result = await KanbanService.CreateIssueAsync(100, new CreateIssueRequest
        {
            Title = "Subtask",
            ParentId = 9999
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("ISSUE_NOT_FOUND", result.ErrorCode);
    }

    // UTC011: A subtask cannot contain another subtask.
    [Fact]
    public async Task CreateIssueAsync_WhenParentIsAlreadySubtask_ReturnsBadRequest()
    {
        SeedWorkflow();
        var parent = SeedIssue();
        var child = SeedIssue(201, title: "Existing subtask");
        child.ParentId = parent.Id;
        await Db.SaveChangesAsync();

        var result = await KanbanService.CreateIssueAsync(100, new CreateIssueRequest
        {
            Title = "Nested subtask",
            ParentId = child.Id
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("SUBTASK_DEPTH_EXCEEDED", result.ErrorCode);
    }

    // UTC012: Points below the supported range are rejected.
    [Fact]
    public async Task CreateIssueAsync_WithNegativePoints_ReturnsValidationError()
    {
        SeedWorkflow();

        var result = await KanbanService.CreateIssueAsync(100, new CreateIssueRequest { Title = "Issue", Points = -1 }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Contains("points", result.Errors!.Keys);
    }

    // UTC013: Points above the supported range are rejected.
    [Fact]
    public async Task CreateIssueAsync_WithPointsAboveMaximum_ReturnsValidationError()
    {
        SeedWorkflow();

        var result = await KanbanService.CreateIssueAsync(100, new CreateIssueRequest { Title = "Issue", Points = 101 }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Contains("points", result.Errors!.Keys);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC014: A title of exactly 200 characters is accepted.
    [Fact]
    public async Task CreateIssueAsync_WithTitleAtMaximumLength_ReturnsCreatedIssue()
    {
        SeedWorkflow();

        var result = await KanbanService.CreateIssueAsync(100, new CreateIssueRequest
        {
            Title = new string('T', 200)
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, Assert.Single(Db.Issues).Title.Length);
    }

    // UTC015: A title of 201 characters is rejected.
    [Fact]
    public async Task CreateIssueAsync_WithTitleAboveMaximumLength_ReturnsValidationError()
    {
        SeedWorkflow();

        var result = await KanbanService.CreateIssueAsync(100, new CreateIssueRequest
        {
            Title = new string('T', 201)
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Contains("title", result.Errors!.Keys);
    }

    // UTC016: Points at both supported endpoints are accepted.
    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task CreateIssueAsync_WithPointsAtBoundary_ReturnsCreatedIssue(int points)
    {
        SeedWorkflow();

        var result = await KanbanService.CreateIssueAsync(100, new CreateIssueRequest
        {
            Title = $"Points {points}",
            Points = points
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(points, Assert.Single(Db.Issues).Points);
    }

    // UTC017: A description of exactly 10000 characters is accepted.
    [Fact]
    public async Task CreateIssueAsync_WithDescriptionAtMaximumLength_ReturnsCreatedIssue()
    {
        SeedWorkflow();

        var result = await KanbanService.CreateIssueAsync(100, new CreateIssueRequest
        {
            Title = "Issue",
            Description = new string('D', 10000)
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(10000, Assert.Single(Db.Issues).Description.Length);
    }

    // UTC018: A description over 10000 characters is rejected.
    [Fact]
    public async Task CreateIssueAsync_WithDescriptionAboveMaximumLength_ReturnsValidationError()
    {
        SeedWorkflow();

        var result = await KanbanService.CreateIssueAsync(100, new CreateIssueRequest
        {
            Title = "Issue",
            Description = new string('D', 10001)
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Contains("description", result.Errors!.Keys);
    }

    // UTC019: A valid ISO date at the parsing boundary is accepted.
    [Fact]
    public async Task CreateIssueAsync_WithMinimumDateValue_ReturnsCreatedIssue()
    {
        SeedWorkflow();

        var result = await KanbanService.CreateIssueAsync(100, new CreateIssueRequest
        {
            Title = "Issue",
            DueDate = "0001-01-01"
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(1, 1, 1), Assert.Single(Db.Issues).DueDate);
    }
}
