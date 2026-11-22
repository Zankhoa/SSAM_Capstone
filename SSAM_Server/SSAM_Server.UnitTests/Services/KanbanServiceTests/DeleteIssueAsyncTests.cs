using Microsoft.AspNetCore.Http;
using SSAM_Server.Models;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class DeleteIssueAsyncTests : KanbanServiceTestBase
{
    // UTC001: Deleting an issue soft-deletes the issue and its active subtasks.
    [Fact]
    public async Task DeleteIssueAsync_WithExistingIssue_SoftDeletesIssue()
    {
        SeedWorkflow();
        var issue = SeedIssue();
        var subtask = new Issue
        {
            ProjectId = 100,
            ParentId = issue.Id,
            Key = "ENG-201",
            Title = "Subtask",
            StatusId = 30,
            ReporterId = 1,
            Rank = "000200",
            Labels = "[]"
        };
        Db.Issues.Add(subtask);
        await Db.SaveChangesAsync();

        var result = await KanbanService.DeleteIssueAsync(issue.Id, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status204NoContent, result.StatusCode);
        Assert.True((await Db.Issues.FindAsync(issue.Id))!.IsDeleted);
        Assert.True((await Db.Issues.FindAsync(subtask.Id))!.IsDeleted);
    }

    // UTC002: Deleting a missing issue returns not found.
    [Fact]
    public async Task DeleteIssueAsync_WhenIssueIsMissing_ReturnsNotFound()
    {
        var result = await KanbanService.DeleteIssueAsync(9999, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }
}
