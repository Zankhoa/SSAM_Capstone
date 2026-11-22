using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Documents;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.DocumentServiceTests;

public class CreateDocumentCommentAsyncTests : DocumentServiceTestBase
{
    private const long ProjectId = 10;
    private const long DocumentId = 100;
    private const long VersionId = 1000;
    private const long UserId = 2;

    private void SetupAuthorizedUser()
    {
        CurrentUserMock.Setup(u => u.UserId).Returns(UserId);
        DocumentTestData.GuardAllow(AccessGuardMock, ProjectId, PermissionKeys.DocumentComment);
    }

    private (DocumentNode Node, DocumentVersion Version) SetupPage(string status = DocumentVersionStatuses.Draft)
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        return DocumentTestData.AddPageWithVersion(Db, now, DocumentId, VersionId, ProjectId, status);
    }

    private DocumentComment AddComment(long id, long? parentId = null, long documentId = DocumentId, long versionId = VersionId)
    {
        var comment = new DocumentComment
        {
            Id = id,
            DocumentId = documentId,
            VersionId = versionId,
            ParentId = parentId,
            Body = $"Existing {id}",
            AuthorId = 1,
            CreatedAt = Clock.GetUtcNow().UtcDateTime
        };
        Db.DocumentComments.Add(comment);
        Db.SaveChanges();
        return comment;
    }

    private static CreateDocumentCommentRequest Request(string body = "Looks good", long versionId = VersionId) => new()
    {
        VersionId = versionId,
        Body = body
    };

    private async Task AssertNothingPersistedAsync()
    {
        Assert.Empty(await Db.DocumentComments.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.DocumentCommentMentions.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Create a root comment with anchor and quote -> 201, DTO fully mapped, comment and COMMENTED activity persisted
    [Fact]
    public async Task CreateDocumentCommentAsync_WithRootCommentAnchorAndQuote_ReturnsCreated()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        SetupPage();
        var request = new CreateDocumentCommentRequest { VersionId = VersionId, Body = "  Please clarify  ", AnchorId = "  anchor-1  ", Quote = "  quoted text  " };

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.CommentId > 0);
        Assert.Equal(DocumentId, result.Data.DocumentId);
        Assert.Equal(VersionId, result.Data.VersionId);
        Assert.Null(result.Data.ParentId);
        Assert.Equal("anchor-1", result.Data.AnchorId);
        Assert.Equal("quoted text", result.Data.Quote);
        Assert.Equal("Please clarify", result.Data.Body);
        Assert.Equal(UserId, result.Data.Author.UserId);
        Assert.Equal("Reviewer Two", result.Data.Author.FullName);
        Assert.Equal("2026-11-21T10:00:00.000Z", result.Data.CreatedAt);
        Assert.Null(result.Data.ResolvedBy);
        Assert.Null(result.Data.ResolvedAt);
        Assert.Empty(result.Data.Mentions);
        Assert.Empty(result.Data.Replies);

        var comment = await Db.DocumentComments.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(result.Data.CommentId, comment.Id);
        Assert.Equal(DocumentId, comment.DocumentId);
        Assert.Equal(VersionId, comment.VersionId);
        Assert.Equal("anchor-1", comment.AnchorId);
        Assert.Equal("quoted text", comment.Quote);
        Assert.Equal("Please clarify", comment.Body);
        Assert.Equal(UserId, comment.AuthorId);
        Assert.Equal(now, comment.CreatedAt);
        Assert.Null(comment.ResolvedAt);

        var activity = await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DocumentActivityActions.Commented, activity.Action);
        Assert.Equal(DocumentId, activity.DocumentId);
        Assert.Equal(VersionId, activity.VersionId);
        Assert.Equal(UserId, activity.ActorId);
        Assert.Equal("Please clarify", activity.Note);
        Assert.Equal(now, activity.CreatedAt);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.DocumentComment, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Blank anchor and quote (whitespace) -> stored as null
    [Fact]
    public async Task CreateDocumentCommentAsync_WithWhitespaceAnchorAndQuote_StoresNulls()
    {
        SetupAuthorizedUser();
        SetupPage();
        var request = new CreateDocumentCommentRequest { VersionId = VersionId, Body = "Body", AnchorId = "   ", Quote = "\t " };

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.AnchorId);
        Assert.Null(result.Data.Quote);
        var comment = await Db.DocumentComments.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Null(comment.AnchorId);
        Assert.Null(comment.Quote);
    }

    // UTC03: Null anchor and quote -> stored as null
    [Fact]
    public async Task CreateDocumentCommentAsync_WithNullAnchorAndQuote_StoresNulls()
    {
        SetupAuthorizedUser();
        SetupPage();

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, Request(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.AnchorId);
        Assert.Null(result.Data.Quote);
    }

    // UTC04: Reply to an existing root comment -> ParentId set
    [Fact]
    public async Task CreateDocumentCommentAsync_WithReplyToRootComment_SetsParent()
    {
        SetupAuthorizedUser();
        SetupPage();
        AddComment(1);
        var request = Request("I agree");
        request.ParentId = 1;

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(1, result.Data!.ParentId);
        var reply = await Db.DocumentComments.SingleAsync(c => c.Id != 1, TestContext.Current.CancellationToken);
        Assert.Equal(1, reply.ParentId);
        Assert.Equal("I agree", reply.Body);
    }

    // UTC05: Mentions of project members are stored and returned; non-members, duplicates ignored
    [Fact]
    public async Task CreateDocumentCommentAsync_WithMentions_StoresOnlyDistinctProjectMembers()
    {
        SetupAuthorizedUser();
        SetupPage();
        DocumentTestData.AddUser(Db, 3);
        DocumentTestData.AddUser(Db, 4);
        DocumentTestData.AddUser(Db, 99);
        DocumentTestData.AddProjectMember(Db, ProjectId, 3);
        DocumentTestData.AddProjectMember(Db, ProjectId, 4);
        DocumentTestData.AddProjectMember(Db, 77, 99); // member of another project only
        var request = Request("Ping");
        request.MentionUserIds = new List<long> { 3, 3, 4, 99, 12345 };

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new long[] { 3, 4 }, result.Data!.Mentions.Select(m => m.UserId).OrderBy(x => x).ToArray());
        Assert.Equal("User 3", result.Data.Mentions.Single(m => m.UserId == 3).FullName);
        var mentions = await Db.DocumentCommentMentions.OrderBy(m => m.UserId).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new long[] { 3, 4 }, mentions.Select(m => m.UserId).ToArray());
        Assert.All(mentions, m => Assert.Equal(result.Data.CommentId, m.CommentId));
    }

    // UTC06: Empty and null mention lists -> no mentions
    [Fact]
    public async Task CreateDocumentCommentAsync_WithEmptyMentionList_StoresNoMentions()
    {
        SetupAuthorizedUser();
        SetupPage();
        var request = Request();
        request.MentionUserIds = new List<long>();

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Mentions);
        Assert.Empty(await Db.DocumentCommentMentions.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC07: Only non-member mentions -> comment still created, no mention rows
    [Fact]
    public async Task CreateDocumentCommentAsync_WithOnlyNonMemberMentions_CreatesCommentWithoutMentions()
    {
        SetupAuthorizedUser();
        SetupPage();
        var request = Request();
        request.MentionUserIds = new List<long> { 555 };

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Mentions);
        Assert.Empty(await Db.DocumentCommentMentions.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await Db.DocumentComments.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC08: Commenting on a locked (APPROVED) version -> allowed (no status check)
    [Fact]
    public async Task CreateDocumentCommentAsync_OnApprovedVersion_Succeeds()
    {
        SetupAuthorizedUser();
        SetupPage(DocumentVersionStatuses.Approved);

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, Request(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
    }

    // UTC09: Long body (150 chars) -> activity note truncated to 120 chars plus an ellipsis, comment body kept in full
    [Fact]
    public async Task CreateDocumentCommentAsync_WithLongBody_TruncatesActivityNote()
    {
        SetupAuthorizedUser();
        SetupPage();
        var body = new string('b', 150);

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, Request(body), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(body, result.Data!.Body);
        Assert.Equal(new string('b', 120) + "…", (await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken)).Note);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC10: Document does not exist -> 404 DOCUMENT_NOT_FOUND, guard not consulted
    [Fact]
    public async Task CreateDocumentCommentAsync_WithNonExistentDocument_ReturnsNotFound()
    {
        var result = await DocumentService.CreateDocumentCommentAsync(999, Request(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC11: Document is soft-deleted -> 404 DOCUMENT_NOT_FOUND
    [Fact]
    public async Task CreateDocumentCommentAsync_WithDeletedDocument_ReturnsNotFound()
    {
        SetupAuthorizedUser();
        var (node, _) = SetupPage();
        node.IsDeleted = true;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, Request(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
    }

    // UTC12: Unauthenticated caller -> 401, nothing persisted
    [Fact]
    public async Task CreateDocumentCommentAsync_WhenUnauthenticated_ReturnsUnauthorizedAndPersistsNothing()
    {
        DocumentTestData.GuardUnauthenticated(AccessGuardMock, ProjectId, PermissionKeys.DocumentComment);
        SetupPage();

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, Request(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        await AssertNothingPersistedAsync();
    }

    // UTC13: Permission denied -> 403 FORBIDDEN with required permission (comment.write), nothing persisted
    [Fact]
    public async Task CreateDocumentCommentAsync_WhenPermissionDenied_ReturnsForbiddenAndPersistsNothing()
    {
        DocumentTestData.GuardDeny(AccessGuardMock, ProjectId, PermissionKeys.DocumentComment);
        SetupPage();

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, Request(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.CommentWrite, result.RequiredPermission);
        await AssertNothingPersistedAsync();
    }

    // UTC14: Empty body -> 400 VALIDATION_ERROR (body)
    [Fact]
    public async Task CreateDocumentCommentAsync_WithEmptyBody_ReturnsValidationError()
    {
        SetupAuthorizedUser();
        SetupPage();

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, Request(""), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal("Comment body is required and must be at most 2000 characters.", result.Errors!["body"][0]);
        await AssertNothingPersistedAsync();
    }

    // UTC15: Whitespace-only body -> 400 VALIDATION_ERROR (body)
    [Fact]
    public async Task CreateDocumentCommentAsync_WithWhitespaceBody_ReturnsValidationError()
    {
        SetupAuthorizedUser();
        SetupPage();

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, Request("  \n\t "), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.True(result.Errors!.ContainsKey("body"));
        await AssertNothingPersistedAsync();
    }

    // UTC16: Body longer than 2000 characters -> 400 VALIDATION_ERROR (body)
    [Fact]
    public async Task CreateDocumentCommentAsync_WithTooLongBody_ReturnsValidationError()
    {
        SetupAuthorizedUser();
        SetupPage();

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, Request(new string('b', 2001)), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.True(result.Errors!.ContainsKey("body"));
        await AssertNothingPersistedAsync();
    }

    // UTC17: Version does not exist -> 404 VERSION_NOT_FOUND
    [Fact]
    public async Task CreateDocumentCommentAsync_WithNonExistentVersion_ReturnsVersionNotFound()
    {
        SetupAuthorizedUser();
        SetupPage();

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, Request(versionId: 9999), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("VERSION_NOT_FOUND", result.ErrorCode);
        await AssertNothingPersistedAsync();
    }

    // UTC18: Version belongs to another document -> 404 VERSION_NOT_FOUND
    [Fact]
    public async Task CreateDocumentCommentAsync_WithVersionOfAnotherDocument_ReturnsVersionNotFound()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        SetupPage();
        DocumentTestData.AddPageWithVersion(Db, now, 200, 2000, ProjectId, title: "Other", code: "SRS-02");

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, Request(versionId: 2000), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VERSION_NOT_FOUND", result.ErrorCode);
        await AssertNothingPersistedAsync();
    }

    // UTC19: Parent comment does not exist -> 404 COMMENT_NOT_FOUND
    [Fact]
    public async Task CreateDocumentCommentAsync_WithNonExistentParent_ReturnsCommentNotFound()
    {
        SetupAuthorizedUser();
        SetupPage();
        var request = Request();
        request.ParentId = 999;

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("COMMENT_NOT_FOUND", result.ErrorCode);
        await AssertNothingPersistedAsync();
    }

    // UTC20: Parent comment belongs to another document -> 404 COMMENT_NOT_FOUND
    [Fact]
    public async Task CreateDocumentCommentAsync_WithParentOfAnotherDocument_ReturnsCommentNotFound()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        SetupPage();
        DocumentTestData.AddPageWithVersion(Db, now, 200, 2000, ProjectId, title: "Other", code: "SRS-02");
        AddComment(1, documentId: 200, versionId: 2000);
        var request = Request();
        request.ParentId = 1;

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("COMMENT_NOT_FOUND", result.ErrorCode);
        Assert.Single(await Db.DocumentComments.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC21: Reply to a reply (nesting > 1 level) -> 400 COMMENT_NESTING_LIMIT
    [Fact]
    public async Task CreateDocumentCommentAsync_WithReplyToReply_ReturnsNestingLimit()
    {
        SetupAuthorizedUser();
        SetupPage();
        AddComment(1);
        AddComment(2, parentId: 1);
        var request = Request();
        request.ParentId = 2;

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("COMMENT_NESTING_LIMIT", result.ErrorCode);
        Assert.Equal(2, await Db.DocumentComments.CountAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC22: Invalid body is reported before a missing version (validation precedes lookup)
    [Fact]
    public async Task CreateDocumentCommentAsync_WithInvalidBodyAndMissingVersion_ReturnsValidationErrorFirst()
    {
        SetupAuthorizedUser();
        SetupPage();

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, Request("", versionId: 9999), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC23: Body of exactly 1 character (min) -> created
    [Fact]
    public async Task CreateDocumentCommentAsync_WithBodyOfOneCharacter_Succeeds()
    {
        SetupAuthorizedUser();
        SetupPage();

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, Request("x"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("x", result.Data!.Body);
        Assert.Equal("x", (await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken)).Note);
    }

    // UTC24: Body of exactly 2000 characters (max) -> created
    [Fact]
    public async Task CreateDocumentCommentAsync_WithBodyOf2000Characters_Succeeds()
    {
        SetupAuthorizedUser();
        SetupPage();
        var body = new string('b', 2000);

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, Request(body), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(body, (await Db.DocumentComments.SingleAsync(TestContext.Current.CancellationToken)).Body);
    }

    // UTC25: Body of 2001 characters (max+1) -> validation error
    [Fact]
    public async Task CreateDocumentCommentAsync_WithBodyOf2001Characters_ReturnsValidationError()
    {
        SetupAuthorizedUser();
        SetupPage();

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, Request(new string('b', 2001)), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    // UTC26: Body of 2000 characters padded with spaces (trimmed length 2000) -> created with trimmed body
    [Fact]
    public async Task CreateDocumentCommentAsync_WithPaddedBodyTrimmedTo2000Characters_Succeeds()
    {
        SetupAuthorizedUser();
        SetupPage();
        var body = new string('b', 2000);

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, Request($"   {body}   "), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(body, result.Data!.Body);
    }

    // UTC27: Body of exactly 120 characters -> activity note is the full body without ellipsis
    [Fact]
    public async Task CreateDocumentCommentAsync_WithBodyOf120Characters_ActivityNoteHasNoEllipsis()
    {
        SetupAuthorizedUser();
        SetupPage();
        var body = new string('b', 120);

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, Request(body), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(body, (await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken)).Note);
    }

    // UTC28: Body of 121 characters -> activity note truncated to 120 plus ellipsis
    [Fact]
    public async Task CreateDocumentCommentAsync_WithBodyOf121Characters_ActivityNoteIsTruncated()
    {
        SetupAuthorizedUser();
        SetupPage();

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, Request(new string('b', 121)), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new string('b', 120) + "…", (await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken)).Note);
    }

    // UTC29: Quote of exactly 300 characters (max) -> stored unchanged
    [Fact]
    public async Task CreateDocumentCommentAsync_WithQuoteOf300Characters_StoresFullQuote()
    {
        SetupAuthorizedUser();
        SetupPage();
        var request = Request();
        request.Quote = new string('q', 300);

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new string('q', 300), result.Data!.Quote);
        Assert.Equal(new string('q', 300), (await Db.DocumentComments.SingleAsync(TestContext.Current.CancellationToken)).Quote);
    }

    // UTC30: Quote of 301 characters (max+1) -> truncated to 300 characters (not rejected)
    [Fact]
    public async Task CreateDocumentCommentAsync_WithQuoteOf301Characters_TruncatesTo300()
    {
        SetupAuthorizedUser();
        SetupPage();
        var request = Request();
        request.Quote = new string('q', 301);

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new string('q', 300), result.Data!.Quote);
        Assert.Equal(300, (await Db.DocumentComments.SingleAsync(TestContext.Current.CancellationToken)).Quote!.Length);
    }

    // UTC31: Quote of 299 characters (max-1) -> stored unchanged
    [Fact]
    public async Task CreateDocumentCommentAsync_WithQuoteOf299Characters_StoresFullQuote()
    {
        SetupAuthorizedUser();
        SetupPage();
        var request = Request();
        request.Quote = new string('q', 299);

        var result = await DocumentService.CreateDocumentCommentAsync(DocumentId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(299, result.Data!.Quote!.Length);
    }
}
