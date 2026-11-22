using Microsoft.AspNetCore.Http;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.Models;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class TransitionIssueAsyncTests : KanbanServiceTestBase
{
    // UTC001: An issue transitions through an allowed workflow transition.
    [Fact]
    public async Task TransitionIssueAsync_WithAllowedTransition_UpdatesStatus()
    {
        var (_, _, todo, done) = SeedWorkflow();
        var issue = SeedIssue(statusId: todo.Id);
        Db.WorkflowTransitions.Add(new WorkflowTransition
        {
            Id = 40,
            ProjectId = 100,
            FromStatusId = todo.Id,
            ToStatusId = done.Id,
            RequireComment = false
        });
        await Db.SaveChangesAsync();

        var result = await KanbanService.TransitionIssueAsync(issue.Id, new TransitionIssueRequest { ToStatusId = done.Id }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(done.Id, (await Db.Issues.FindAsync(issue.Id))!.StatusId);
    }

    // UTC002: A missing target status returns not found.
    [Fact]
    public async Task TransitionIssueAsync_WhenTargetStatusIsMissing_ReturnsNotFound()
    {
        SeedWorkflow();
        var issue = SeedIssue();

        var result = await KanbanService.TransitionIssueAsync(issue.Id, new TransitionIssueRequest { ToStatusId = 9999 }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("STATUS_NOT_FOUND", result.ErrorCode);
    }

    // UTC003: A transition without an allowed workflow edge returns conflict.
    [Fact]
    public async Task TransitionIssueAsync_WhenTransitionIsNotConfigured_ReturnsConflict()
    {
        var (_, _, _, done) = SeedWorkflow();
        var issue = SeedIssue();

        var result = await KanbanService.TransitionIssueAsync(issue.Id, new TransitionIssueRequest { ToStatusId = done.Id }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("TRANSITION_NOT_ALLOWED", result.ErrorCode);
    }
}
