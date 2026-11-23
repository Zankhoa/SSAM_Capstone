using Microsoft.AspNetCore.Http;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class CreateIssueCommentAsyncTests : KanbanServiceTestBase
{
    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: Valid root comment trims whitespace, persists comment and activity, and returns 201 Created.
    [Fact]
    public async Task CreateIssueCommentAsync_WithValidRootComment_CreatesCommentAndActivity()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);

        var result = await KanbanService.CreateIssueCommentAsync(issue.Id, new CreateIssueCommentRequest
        {
            Body = "  Looks good to me!  "
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(issue.Id, result.Data!.IssueId);
        Assert.Null(result.Data.ParentId);
        Assert.Equal("Looks good to me!", result.Data.Body);
        Assert.Equal(1, result.Data.Author.UserId);

        var persisted = Assert.Single(Db.IssueComments);
        Assert.Equal("Looks good to me!", persisted.Body);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, persisted.CreatedAt);

        var activity = Assert.Single(Db.IssueActivities);
        Assert.Equal(IssueActivityActions.Commented, activity.Action);
        Assert.Equal("Looks good to me!", activity.Note);
    }

    // UTC002: Valid reply referencing a top-level comment on the same issue creates a 1-level nested reply.
    [Fact]
    public async Task CreateIssueCommentAsync_WithValidParentComment_CreatesReply()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);
        var rootComment = new IssueComment
        {
            Id = 800,
            IssueId = issue.Id,
            ParentId = null,
            Body = "Parent discussion",
            AuthorId = 1,
            CreatedAt = Clock.GetUtcNow().UtcDateTime.AddMinutes(-5)
        };
        Db.IssueComments.Add(rootComment);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.CreateIssueCommentAsync(issue.Id, new CreateIssueCommentRequest
        {
            ParentId = rootComment.Id,
            Body = "Replying to parent"
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(rootComment.Id, result.Data!.ParentId);
        Assert.Equal("Replying to parent", result.Data.Body);
    }

    // UTC003: MentionUserIds persists distinct mentions only for members belonging to the issue's project.
    [Fact]
    public async Task CreateIssueCommentAsync_WithMentionUserIds_PersistsOnlyDistinctProjectMemberMentions()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);
        var memberUser = SeedUser(userId: 2, fullName: "Project Member", email: "member@example.com");
        var outsiderUser = SeedUser(userId: 3, fullName: "Outsider User", email: "outsider@example.com");
        SeedProjectMember(userId: memberUser.User.Id, roleId: 51, projectId: 100, memberId: 701);

        var result = await KanbanService.CreateIssueCommentAsync(issue.Id, new CreateIssueCommentRequest
        {
            Body = "Tagging @member and @outsider",
            MentionUserIds = new List<long> { memberUser.User.Id, memberUser.User.Id, outsiderUser.User.Id, 9999 }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);

        var mentionDto = Assert.Single(result.Data!.Mentions);
        Assert.Equal(memberUser.User.Id, mentionDto.UserId);

        var persistedMention = Assert.Single(Db.IssueCommentMentions);
        Assert.Equal(memberUser.User.Id, persistedMention.UserId);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC004: Missing or soft-deleted issue returns NotFound ("ISSUE_NOT_FOUND").
    [Theory]
    [InlineData(false)] // Issue ID does not exist
    [InlineData(true)]  // Issue is soft-deleted
    public async Task CreateIssueCommentAsync_WhenIssueMissingOrDeleted_ReturnsNotFound(bool seedDeleted)
    {
        SeedWorkflow();
        long targetIssueId = 9999;
        if (seedDeleted)
        {
            var deleted = SeedIssue(issueId: 200);
            deleted.IsDeleted = true;
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
            targetIssueId = deleted.Id;
        }

        var result = await KanbanService.CreateIssueCommentAsync(targetIssueId, new CreateIssueCommentRequest
        {
            Body = "Comment on missing issue"
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("ISSUE_NOT_FOUND", result.ErrorCode);
    }

    // UTC005: Caller lacking IssueComment permission returns Forbidden without persisting comment.
    [Fact]
    public async Task CreateIssueCommentAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);
        AccessGuardMock
            .Setup(g => g.EnsureProjectPermissionAsync(100, PermissionKeys.IssueComment, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Forbidden("FORBIDDEN", "Permission denied."));

        var result = await KanbanService.CreateIssueCommentAsync(issue.Id, new CreateIssueCommentRequest
        {
            Body = "Unauthorized comment"
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Empty(Db.IssueComments);
    }

    // UTC006: Empty, whitespace-only, or >2000-character Body returns ValidationFailure on "body".
    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData(null)]
    public async Task CreateIssueCommentAsync_WithBlankOrTooLongBody_ReturnsValidationFailure(string? rawBody)
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);
        var body = rawBody ?? new string('X', 2001);

        var result = await KanbanService.CreateIssueCommentAsync(issue.Id, new CreateIssueCommentRequest
        {
            Body = body
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("body", result.Errors!.Keys);
        Assert.Empty(Db.IssueComments);
    }

    // UTC007: ParentId pointing to a non-existent comment or a comment on another issue returns NotFound ("COMMENT_NOT_FOUND").
    [Theory]
    [InlineData(false)] // Parent comment does not exist
    [InlineData(true)]  // Parent comment belongs to another issue
    public async Task CreateIssueCommentAsync_WhenParentCommentNotFoundOnIssue_ReturnsNotFound(bool seedOnOtherIssue)
    {
        SeedWorkflow();
        var issue1 = SeedIssue(issueId: 200);
        long parentId = 9999;

        if (seedOnOtherIssue)
        {
            var issue2 = SeedIssue(issueId: 201);
            var otherComment = new IssueComment
            {
                Id = 801,
                IssueId = issue2.Id,
                Body = "Comment on another issue",
                AuthorId = 1,
                CreatedAt = Clock.GetUtcNow().UtcDateTime
            };
            Db.IssueComments.Add(otherComment);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
            parentId = otherComment.Id;
        }

        var result = await KanbanService.CreateIssueCommentAsync(issue1.Id, new CreateIssueCommentRequest
        {
            ParentId = parentId,
            Body = "Invalid reply"
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("COMMENT_NOT_FOUND", result.ErrorCode);
    }

    // UTC008: Replying to a comment that is already a reply (ParentId != null) returns BadRequest ("COMMENT_NESTING_LIMIT").
    [Fact]
    public async Task CreateIssueCommentAsync_WhenReplyingToNestedReply_ReturnsBadRequest()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);
        var root = new IssueComment
        {
            Id = 800,
            IssueId = issue.Id,
            ParentId = null,
            Body = "Root",
            AuthorId = 1,
            CreatedAt = Clock.GetUtcNow().UtcDateTime.AddMinutes(-2)
        };
        var firstReply = new IssueComment
        {
            Id = 801,
            IssueId = issue.Id,
            ParentId = root.Id,
            Body = "Level 1 reply",
            AuthorId = 1,
            CreatedAt = Clock.GetUtcNow().UtcDateTime.AddMinutes(-1)
        };
        Db.IssueComments.AddRange(root, firstReply);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.CreateIssueCommentAsync(issue.Id, new CreateIssueCommentRequest
        {
            ParentId = firstReply.Id,
            Body = "Level 2 reply"
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("COMMENT_NESTING_LIMIT", result.ErrorCode);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC009: Body with exact minimum length (1 character) succeeds.
    [Fact]
    public async Task CreateIssueCommentAsync_WithMinimumLengthBody_Succeeds()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);

        var result = await KanbanService.CreateIssueCommentAsync(issue.Id, new CreateIssueCommentRequest
        {
            Body = "A"
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal("A", result.Data!.Body);
    }

    // UTC010: Body with exact maximum length (2000 characters) succeeds and truncates Activity.Note to 120 chars + ellipsis.
    [Fact]
    public async Task CreateIssueCommentAsync_WithMaximumLengthBody_SucceedsAndTruncatesActivityNote()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);
        var maxBody = new string('B', 2000);

        var result = await KanbanService.CreateIssueCommentAsync(issue.Id, new CreateIssueCommentRequest
        {
            Body = maxBody
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2000, result.Data!.Body.Length);

        var activity = Assert.Single(Db.IssueActivities);
        Assert.Equal(new string('B', 120) + "…", activity.Note);
    }

    // UTC011: Body of exactly 120 characters stores full text in Activity.Note without truncation ellipsis.
    [Fact]
    public async Task CreateIssueCommentAsync_With120CharBody_StoresFullActivityNoteWithoutEllipsis()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);
        var exact120Body = new string('C', 120);

        var result = await KanbanService.CreateIssueCommentAsync(issue.Id, new CreateIssueCommentRequest
        {
            Body = exact120Body
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var activity = Assert.Single(Db.IssueActivities);
        Assert.Equal(exact120Body, activity.Note);
        Assert.DoesNotContain("…", activity.Note);
    }
}