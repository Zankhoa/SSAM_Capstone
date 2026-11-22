using Microsoft.AspNetCore.Http;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class BoardAndIssueQueryTests : KanbanServiceTestBase
{
    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: The board returns ordered workflow columns and an active sprint.
    [Fact]
    public async Task GetBoardAsync_WithWorkflowAndActiveSprint_ReturnsColumnsAndSprint()
    {
        var (_, _, todo, done) = SeedWorkflow();
        SeedSprint(400, SprintStatuses.Active);
        Db.BoardWipLimits.Add(new BoardWipLimit { ProjectId = 100, StatusId = todo.Id, WipLimit = 3 });
        await Db.SaveChangesAsync();

        var result = await KanbanService.GetBoardAsync(100, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(new[] { todo.Id, done.Id }, result.Data!.Columns.Select(c => c.StatusId));
        Assert.Equal(3, result.Data.Columns[0].WipLimit);
        Assert.NotNull(result.Data.ActiveSprint);
        Assert.Equal(400, result.Data.ActiveSprint!.SprintId);
    }

    // UTC002: Issue listing applies backlog, type, status, parent, search, and requirement filters.
    [Fact]
    public async Task GetProjectIssuesAsync_WithFilters_ReturnsMatchingIssue()
    {
        var (_, _, todo, _) = SeedWorkflow();
        var matching = SeedIssue(200, todo.Id, title: "Matching issue");
        matching.Type = IssueTypes.Bug;
        matching.Rank = "000100";
        var other = SeedIssue(201, todo.Id, title: "Other issue");
        other.Type = IssueTypes.Task;
        other.Rank = "000200";
        var document = SeedRequirementDocument();
        Db.IssueRequirementLinks.Add(new IssueRequirementLink
        {
            IssueId = matching.Id,
            DocumentId = document.Id,
            ReqUid = "REQ-001",
            ReqHash = "hash-1",
            LinkedById = 1
        });
        await Db.SaveChangesAsync();

        var result = await KanbanService.GetProjectIssuesAsync(100, new IssueListQuery
        {
            SprintId = "backlog",
            Type = " bug ",
            StatusId = todo.Id,
            ParentId = "root",
            Search = "Matching"
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(matching.Id, item.IssueId);
    }

    // UTC003: Issue detail includes subtasks and allowed transitions.
    [Fact]
    public async Task GetIssueDetailAsync_WithSubtaskAndTransition_ReturnsDetailGraph()
    {
        var (_, _, todo, done) = SeedWorkflow();
        var issue = SeedIssue(statusId: todo.Id);
        SeedIssue(201, done.Id, title: "Subtask").ParentId = issue.Id;
        Db.WorkflowTransitions.Add(new WorkflowTransition
        {
            Id = 40,
            ProjectId = 100,
            FromStatusId = todo.Id,
            ToStatusId = done.Id,
            RequireComment = false
        });
        await Db.SaveChangesAsync();

        var result = await KanbanService.GetIssueDetailAsync(issue.Id, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(issue.Id, result.Data!.IssueId);
        Assert.Single(result.Data.Subtasks);
        Assert.Single(result.Data.AllowedTransitions);
        Assert.Equal(done.Id, result.Data.AllowedTransitions[0].ToStatusId);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC004: A missing issue returns not found for detail.
    [Fact]
    public async Task GetIssueDetailAsync_WhenIssueIsMissing_ReturnsNotFound()
    {
        var result = await KanbanService.GetIssueDetailAsync(9999, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("ISSUE_NOT_FOUND", result.ErrorCode);
    }

    // UTC005: A denied view permission blocks board access.
    [Fact]
    public async Task GetBoardAsync_WhenPermissionIsDenied_ReturnsForbidden()
    {
        AccessGuardMock.Setup(g => g.EnsureProjectPermissionAsync(100, PermissionKeys.IssueView, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Forbidden("FORBIDDEN", "Permission denied."));

        var result = await KanbanService.GetBoardAsync(100, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }
}
