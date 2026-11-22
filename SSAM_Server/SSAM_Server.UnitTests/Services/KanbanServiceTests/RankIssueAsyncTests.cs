using Microsoft.AspNetCore.Http;
using SSAM_Server.DTOs.Kanban;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class RankIssueAsyncTests : KanbanServiceTestBase
{
    // UTC001: Ranking an issue without neighboring issues keeps the operation successful.
    [Fact]
    public async Task RankIssueAsync_WithValidRequest_ReturnsSuccess()
    {
        var (_, _, todo, _) = SeedWorkflow();
        var issue = SeedIssue(statusId: todo.Id);

        var result = await KanbanService.RankIssueAsync(issue.Id, new RankIssueRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
    }

    // UTC002: Ranking a missing issue returns not found.
    [Fact]
    public async Task RankIssueAsync_WhenIssueIsMissing_ReturnsNotFound()
    {
        var result = await KanbanService.RankIssueAsync(9999, new RankIssueRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("ISSUE_NOT_FOUND", result.ErrorCode);
    }
}
