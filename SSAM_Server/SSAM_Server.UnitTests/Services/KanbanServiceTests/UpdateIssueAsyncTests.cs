using Microsoft.AspNetCore.Http;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.Models;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class UpdateIssueAsyncTests : KanbanServiceTestBase
{
    // UTC001: Updating valid issue fields persists normalized values.
    [Fact]
    public async Task UpdateIssueAsync_WithValidRequest_ReturnsUpdatedIssue()
    {
        SeedWorkflow();
        var issue = SeedIssue();

        var result = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest
        {
            Title = "  Updated title  ",
            Priority = IssuePriorities.High,
            Points = 8,
            DueDate = "2026-12-10"
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        var saved = await Db.Issues.FindAsync(issue.Id);
        Assert.Equal("Updated title", saved!.Title);
        Assert.Equal(IssuePriorities.High, saved.Priority);
        Assert.Equal(8, saved.Points);
    }

    // UTC002: Invalid update values are rejected.
    [Fact]
    public async Task UpdateIssueAsync_WhenPriorityIsInvalid_ReturnsValidationError()
    {
        SeedWorkflow();
        var issue = SeedIssue();

        var result = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest { Priority = "INVALID" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
    }

    // UTC003: Updating a missing issue returns not found.
    [Fact]
    public async Task UpdateIssueAsync_WhenIssueIsMissing_ReturnsNotFound()
    {
        SeedWorkflow();

        var result = await KanbanService.UpdateIssueAsync(9999, new UpdateIssueRequest { Title = "Updated" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("ISSUE_NOT_FOUND", result.ErrorCode);
    }
}
