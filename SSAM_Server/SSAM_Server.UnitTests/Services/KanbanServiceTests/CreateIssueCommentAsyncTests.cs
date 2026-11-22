using Microsoft.AspNetCore.Http;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.Models;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class CreateIssueCommentAsyncTests : KanbanServiceTestBase
{
    // UTC001: A valid comment is persisted with the current user as author.
    [Fact]
    public async Task CreateIssueCommentAsync_WithValidBody_ReturnsCreatedComment()
    {
        SeedWorkflow();
        var issue = SeedIssue();

        var result = await KanbanService.CreateIssueCommentAsync(issue.Id, new CreateIssueCommentRequest
        {
            Body = "  Looks good.  "
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        var comment = Assert.Single(Db.IssueComments);
        Assert.Equal("Looks good.", comment.Body);
        Assert.Equal(1, comment.AuthorId);
    }

    // UTC002: An empty comment body is rejected.
    [Fact]
    public async Task CreateIssueCommentAsync_WhenBodyIsEmpty_ReturnsValidationError()
    {
        SeedWorkflow();
        var issue = SeedIssue();

        var result = await KanbanService.CreateIssueCommentAsync(issue.Id, new CreateIssueCommentRequest { Body = "   " }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
    }

    // UTC003: A reply to a reply is rejected.
    [Fact]
    public async Task CreateIssueCommentAsync_WhenParentIsAlreadyReply_ReturnsBadRequest()
    {
        SeedWorkflow();
        var issue = SeedIssue();
        var parent = new IssueComment { Id = 600, IssueId = issue.Id, Body = "Parent", AuthorId = 1 };
        var reply = new IssueComment { Id = 601, IssueId = issue.Id, ParentId = parent.Id, Body = "Reply", AuthorId = 1 };
        Db.IssueComments.AddRange(parent, reply);
        await Db.SaveChangesAsync();

        var result = await KanbanService.CreateIssueCommentAsync(issue.Id, new CreateIssueCommentRequest { Body = "Nested", ParentId = reply.Id }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("COMMENT_NESTING_LIMIT", result.ErrorCode);
    }
}
