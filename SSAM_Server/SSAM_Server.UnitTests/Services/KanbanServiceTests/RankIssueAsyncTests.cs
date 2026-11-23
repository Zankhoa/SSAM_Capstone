using Microsoft.AspNetCore.Http;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class RankIssueAsyncTests : KanbanServiceTestBase
{
    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: Ranking an issue before a target sibling rebalances column ranks cleanly with step 1000.
    [Fact]
    public async Task RankIssueAsync_WithBeforeIssueId_MovesIssueBeforeTargetAndRebalancesRanks()
    {
        var (_, _, todo, _) = SeedWorkflow();
        var (first, second, third) = SeedColumnIssues(statusId: todo.Id);

        var result = await KanbanService.RankIssueAsync(third.Id, new RankIssueRequest
        {
            BeforeIssueId = second.Id
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal("00002000", result.Data!.Rank);

        Assert.Equal("00001000", Db.Issues.Single(i => i.Id == first.Id).Rank);
        Assert.Equal("00002000", Db.Issues.Single(i => i.Id == third.Id).Rank);
        Assert.Equal("00003000", Db.Issues.Single(i => i.Id == second.Id).Rank);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, Db.Issues.Single(i => i.Id == third.Id).UpdatedAt);
        Assert.Empty(Db.IssueActivities.Where(a => a.IssueId == third.Id));
    }

    // UTC002: Ranking an issue after a target sibling places it immediately after that sibling.
    [Fact]
    public async Task RankIssueAsync_WithAfterIssueId_MovesIssueAfterTargetAndRebalancesRanks()
    {
        var (_, _, todo, _) = SeedWorkflow();
        var (first, second, third) = SeedColumnIssues(statusId: todo.Id);

        var result = await KanbanService.RankIssueAsync(first.Id, new RankIssueRequest
        {
            AfterIssueId = second.Id
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("00001000", Db.Issues.Single(i => i.Id == second.Id).Rank);
        Assert.Equal("00002000", Db.Issues.Single(i => i.Id == first.Id).Rank);
        Assert.Equal("00003000", Db.Issues.Single(i => i.Id == third.Id).Rank);
    }

    // UTC003: Omitting BeforeIssueId and AfterIssueId places the issue at the tail of the column.
    [Fact]
    public async Task RankIssueAsync_WithoutAnchorIds_PlacesIssueAtTail()
    {
        var (_, _, todo, _) = SeedWorkflow();
        var (first, second, third) = SeedColumnIssues(statusId: todo.Id);

        var result = await KanbanService.RankIssueAsync(first.Id, new RankIssueRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("00001000", Db.Issues.Single(i => i.Id == second.Id).Rank);
        Assert.Equal("00002000", Db.Issues.Single(i => i.Id == third.Id).Rank);
        Assert.Equal("00003000", Db.Issues.Single(i => i.Id == first.Id).Rank);
    }

    // UTC004: Moving an issue into a sprint updates SprintId, logs SprintChanged activity, and ranks within the sprint.
    [Fact]
    public async Task RankIssueAsync_WhenMovingIntoSprint_UpdatesSprintLogsActivityAndRanksInSprint()
    {
        var (_, _, todo, _) = SeedWorkflow();
        var sprint = SeedSprint(sprintId: 400);
        var sprintIssue = SeedIssue(issueId: 201, statusId: todo.Id, title: "Sprint Issue");
        sprintIssue.SprintId = sprint.Id;
        sprintIssue.Rank = "00001000";

        var backlogIssue = SeedIssue(issueId: 202, statusId: todo.Id, title: "Backlog Issue");
        backlogIssue.SprintId = null;
        backlogIssue.Rank = "00005000";
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.RankIssueAsync(backlogIssue.Id, new RankIssueRequest
        {
            SprintId = sprint.Id,
            BeforeIssueId = sprintIssue.Id
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var updated = Db.Issues.Single(i => i.Id == backlogIssue.Id);
        Assert.Equal(sprint.Id, updated.SprintId);
        Assert.Equal("00001000", updated.Rank);
        Assert.Equal("00002000", Db.Issues.Single(i => i.Id == sprintIssue.Id).Rank);

        var activity = Assert.Single(Db.IssueActivities.Where(a => a.IssueId == backlogIssue.Id));
        Assert.Equal(IssueActivityActions.SprintChanged, activity.Action);
        Assert.Equal("Chuyển vào Sprint 1", activity.Note);
    }

    // UTC005: Setting SprintId to null moves the issue back to Backlog and logs SprintChanged activity.
    [Fact]
    public async Task RankIssueAsync_WhenMovingToBacklog_ClearsSprintIdAndLogsActivity()
    {
        var (_, _, todo, _) = SeedWorkflow();
        var sprint = SeedSprint(sprintId: 400);
        var issue = SeedIssue(issueId: 201, statusId: todo.Id);
        issue.SprintId = sprint.Id;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.RankIssueAsync(issue.Id, new RankIssueRequest
        {
            SprintId = null
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(Db.Issues.Single(i => i.Id == issue.Id).SprintId);

        var activity = Assert.Single(Db.IssueActivities.Where(a => a.IssueId == issue.Id));
        Assert.Equal(IssueActivityActions.SprintChanged, activity.Action);
        Assert.Equal("Chuyển về Backlog", activity.Note);
    }

    // UTC006: Specifying the same SprintId the issue already belongs to does not create a duplicate SprintChanged activity.
    [Fact]
    public async Task RankIssueAsync_WhenSprintIdIsUnchanged_DoesNotLogSprintChangedActivity()
    {
        var (_, _, todo, _) = SeedWorkflow();
        var sprint = SeedSprint(sprintId: 400);
        var issue = SeedIssue(issueId: 201, statusId: todo.Id);
        issue.SprintId = sprint.Id;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.RankIssueAsync(issue.Id, new RankIssueRequest
        {
            SprintId = sprint.Id
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(Db.IssueActivities.Where(a => a.IssueId == issue.Id));
    }

    // UTC007: Caller lacking IssueUpdate falls back to IssueTransition permission and succeeds.
    [Fact]
    public async Task RankIssueAsync_WhenIssueUpdateDenied_FallsBackToIssueTransitionPermission()
    {
        var (_, _, todo, _) = SeedWorkflow();
        var issue = SeedIssue(issueId: 201, statusId: todo.Id);

        AccessGuardMock
            .Setup(g => g.EnsureProjectPermissionAsync(100, PermissionKeys.IssueUpdate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Forbidden("FORBIDDEN", "No update permission."));
        AccessGuardMock
            .Setup(g => g.EnsureProjectPermissionAsync(100, PermissionKeys.IssueTransition, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Ok());

        var result = await KanbanService.RankIssueAsync(issue.Id, new RankIssueRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        AccessGuardMock.Verify(
            g => g.EnsureProjectPermissionAsync(100, PermissionKeys.IssueTransition, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC008: Ranking a non-existent or soft-deleted issue returns NotFound.
    [Theory]
    [InlineData(false)] // Missing issue
    [InlineData(true)]  // Soft-deleted issue
    public async Task RankIssueAsync_WhenIssueMissingOrDeleted_ReturnsNotFound(bool seedDeleted)
    {
        var (_, _, todo, _) = SeedWorkflow();
        long targetId = 9999;
        if (seedDeleted)
        {
            var deleted = SeedIssue(issueId: 201, statusId: todo.Id);
            deleted.IsDeleted = true;
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
            targetId = deleted.Id;
        }

        var result = await KanbanService.RankIssueAsync(targetId, new RankIssueRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("ISSUE_NOT_FOUND", result.ErrorCode);
    }

    // UTC009: Caller lacking both IssueUpdate and IssueTransition permissions returns Forbidden.
    [Fact]
    public async Task RankIssueAsync_WhenBothUpdateAndTransitionPermissionsDenied_ReturnsForbidden()
    {
        var (_, _, todo, _) = SeedWorkflow();
        var issue = SeedIssue(issueId: 201, statusId: todo.Id);

        AccessGuardMock
            .Setup(g => g.EnsureProjectPermissionAsync(100, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Forbidden("FORBIDDEN", "Permission denied."));

        var result = await KanbanService.RankIssueAsync(issue.Id, new RankIssueRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    // UTC010: Moving an issue to a missing sprint or a sprint from another project returns NotFound.
    [Fact]
    public async Task RankIssueAsync_WhenTargetSprintNotInProject_ReturnsNotFound()
    {
        var (_, _, todo, _) = SeedWorkflow();
        var issue = SeedIssue(issueId: 201, statusId: todo.Id);
        var foreignSprint = SeedSprint(sprintId: 401, projectId: 999);

        var result = await KanbanService.RankIssueAsync(issue.Id, new RankIssueRequest
        {
            SprintId = foreignSprint.Id
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("SPRINT_NOT_FOUND", result.ErrorCode);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC011: Ranking the sole issue in a column assigns the initial base rank "00001000".
    [Fact]
    public async Task RankIssueAsync_WhenColumnHasOnlyMovingIssue_AssignsBaseRank00001000()
    {
        var (_, _, todo, _) = SeedWorkflow();
        var soleIssue = SeedIssue(issueId: 201, statusId: todo.Id);
        soleIssue.Rank = "00009000";
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.RankIssueAsync(soleIssue.Id, new RankIssueRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("00001000", Db.Issues.Single(i => i.Id == soleIssue.Id).Rank);
    }

    // UTC012: BeforeIssueId pointing to the first issue places the moving issue at head ("00001000").
    [Fact]
    public async Task RankIssueAsync_WithBeforeFirstIssueId_PlacesIssueAtHead()
    {
        var (_, _, todo, _) = SeedWorkflow();
        var (first, second, third) = SeedColumnIssues(statusId: todo.Id);

        var result = await KanbanService.RankIssueAsync(third.Id, new RankIssueRequest
        {
            BeforeIssueId = first.Id
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("00001000", Db.Issues.Single(i => i.Id == third.Id).Rank);
        Assert.Equal("00002000", Db.Issues.Single(i => i.Id == first.Id).Rank);
        Assert.Equal("00003000", Db.Issues.Single(i => i.Id == second.Id).Rank);
    }

    // UTC013: AfterIssueId pointing to the last issue places the moving issue at the tail.
    [Fact]
    public async Task RankIssueAsync_WithAfterLastIssueId_PlacesIssueAtTail()
    {
        var (_, _, todo, _) = SeedWorkflow();
        var (first, second, third) = SeedColumnIssues(statusId: todo.Id);

        var result = await KanbanService.RankIssueAsync(first.Id, new RankIssueRequest
        {
            AfterIssueId = third.Id
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("00001000", Db.Issues.Single(i => i.Id == second.Id).Rank);
        Assert.Equal("00002000", Db.Issues.Single(i => i.Id == third.Id).Rank);
        Assert.Equal("00003000", Db.Issues.Single(i => i.Id == first.Id).Rank);
    }

    // UTC014: When both BeforeIssueId and AfterIssueId are provided, BeforeIssueId takes precedence.
    [Fact]
    public async Task RankIssueAsync_WhenBothBeforeAndAfterProvided_PrioritizesBeforeIssueId()
    {
        var (_, _, todo, _) = SeedWorkflow();
        var (first, second, third) = SeedColumnIssues(statusId: todo.Id);

        var result = await KanbanService.RankIssueAsync(third.Id, new RankIssueRequest
        {
            BeforeIssueId = first.Id,
            AfterIssueId = second.Id
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("00001000", Db.Issues.Single(i => i.Id == third.Id).Rank);
        Assert.Equal("00002000", Db.Issues.Single(i => i.Id == first.Id).Rank);
        Assert.Equal("00003000", Db.Issues.Single(i => i.Id == second.Id).Rank);
    }

    // UTC015: Unknown, self, or cross-column anchor IDs fall back gracefully to placing the issue at the tail.
    [Theory]
    [InlineData(9999)] // Non-existent anchor ID
    [InlineData(201)]  // Self ID (excluded from siblings)
    public async Task RankIssueAsync_WhenAnchorIdIsNotInSiblings_FallsBackToTail(long invalidAnchorId)
    {
        var (_, _, todo, _) = SeedWorkflow();
        var (first, second, third) = SeedColumnIssues(statusId: todo.Id);

        var result = await KanbanService.RankIssueAsync(first.Id, new RankIssueRequest
        {
            BeforeIssueId = invalidAnchorId
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("00001000", Db.Issues.Single(i => i.Id == second.Id).Rank);
        Assert.Equal("00002000", Db.Issues.Single(i => i.Id == third.Id).Rank);
        Assert.Equal("00003000", Db.Issues.Single(i => i.Id == first.Id).Rank);
    }
}