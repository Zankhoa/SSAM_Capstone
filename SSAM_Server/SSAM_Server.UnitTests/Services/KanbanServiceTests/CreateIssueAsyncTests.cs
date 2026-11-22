using Microsoft.AspNetCore.Http;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.Models;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class CreateIssueAsyncTests : KanbanServiceTestBase
{
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
    }

    // UTC002: Invalid issue fields are rejected before persistence.
    [Fact]
    public async Task CreateIssueAsync_WhenRequestIsInvalid_ReturnsValidationError()
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
        Assert.Empty(Db.Issues);
    }

    // UTC003: A status from another project cannot be used.
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
        Assert.Equal("STATUS_NOT_FOUND", result.ErrorCode);
    }
}
