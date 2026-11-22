using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Documents;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.DocumentServiceTests;

public class RequestChangesAsyncTests : DocumentServiceTestBase
{
    private const long ProjectId = 10;
    private const long DocumentId = 100;
    private const long VersionId = 1000;
    private const long ReviewerId = 2;

    private void SetupAuthorizedReviewer(IReadOnlyCollection<string>? permissions = null)
    {
        CurrentUserMock.Setup(u => u.UserId).Returns(ReviewerId);
        DocumentTestData.GuardAllow(AccessGuardMock, ProjectId, PermissionKeys.DocumentApprove);
        DocumentTestData.GuardAllow(AccessGuardMock, ProjectId, PermissionKeys.DocumentView);
        PermissionServiceMock
            .Setup(p => p.GetProjectPermissionsAsync(ReviewerId, ProjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions ?? new[] { PermissionKeys.DocumentApprove, PermissionKeys.DocumentEdit });
    }

    private (DocumentNode Node, DocumentVersion Version) SetupInReviewVersion(string status = DocumentVersionStatuses.InReview)
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var seeded = DocumentTestData.AddPageWithVersion(Db, now, DocumentId, VersionId, ProjectId, status, rowVersion: 6, submittedById: 1);
        DocumentTestData.AddRequirement(Db, DocumentId, VersionId, "REQ-001");
        return seeded;
    }

    private async Task AssertVersionUntouchedAsync(string expectedStatus)
    {
        var version = await Db.DocumentVersions.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expectedStatus, version.Status);
        Assert.Equal(6, version.RowVersion);
        Assert.Null(version.ReviewedById);
        Assert.Null(version.ReviewNote);
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Request changes on an IN_REVIEW version with a note -> CHANGES_REQUESTED, reviewer/note stored, activity recorded
    [Fact]
    public async Task RequestChangesAsync_WithValidNote_MovesToChangesRequested()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedReviewer();
        SetupInReviewVersion();

        var result = await DocumentService.RequestChangesAsync(DocumentId, VersionId, new RequestChangesRequest { Note = "  Please clarify REQ-001  " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(DocumentVersionStatuses.ChangesRequested, result.Data.Current.Status);
        Assert.Equal(7, result.Data.Current.RowVersion);
        Assert.Equal("Please clarify REQ-001", result.Data.Current.ReviewNote);
        Assert.Equal(ReviewerId, result.Data.Current.ReviewedBy!.UserId);
        Assert.Equal("2026-11-21T10:00:00.000Z", result.Data.Current.ReviewedAt);
        Assert.Equal(DocumentVersionStatuses.ChangesRequested, result.Data.Node.CurrentVersion!.Status);
        Assert.True(result.Data.Can.Edit);
        Assert.False(result.Data.Can.Approve);
        Assert.False(result.Data.Node.IsBaselined);

        var version = await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DocumentVersionStatuses.ChangesRequested, version.Status);
        Assert.Equal(ReviewerId, version.ReviewedById);
        Assert.Equal(now, version.ReviewedAt);
        Assert.Equal("Please clarify REQ-001", version.ReviewNote);
        Assert.Equal(ReviewerId, version.UpdatedById);
        Assert.Equal(now, version.UpdatedAt);
        Assert.Equal(7, version.RowVersion);
        Assert.Equal(1, version.SubmittedById);
        Assert.Equal(now, (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).UpdatedAt);

        var activity = await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DocumentActivityActions.ChangesRequested, activity.Action);
        Assert.Equal(DocumentId, activity.DocumentId);
        Assert.Equal(VersionId, activity.VersionId);
        Assert.Equal(ReviewerId, activity.ActorId);
        Assert.Equal("Please clarify REQ-001", activity.Note);
        Assert.Equal(now, activity.CreatedAt);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.DocumentApprove, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Reviewer is not restricted by the required approver list (current behaviour: no NOT_REQUIRED_APPROVER check)
    [Fact]
    public async Task RequestChangesAsync_WhenCallerNotInRequiredApprovers_StillRequestsChanges()
    {
        SetupAuthorizedReviewer();
        var (node, _) = SetupInReviewVersion();
        node.RequiredApprovers = "[3]";
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DocumentService.RequestChangesAsync(DocumentId, VersionId, new RequestChangesRequest { Note = "Fix it" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentVersionStatuses.ChangesRequested, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Status);
    }

    // UTC03: The submitter may request changes on their own version (no self-check in this method)
    [Fact]
    public async Task RequestChangesAsync_WhenCallerIsSubmitter_StillRequestsChanges()
    {
        SetupAuthorizedReviewer();
        CurrentUserMock.Setup(u => u.UserId).Returns(1);
        PermissionServiceMock
            .Setup(p => p.GetProjectPermissionsAsync(1, ProjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { PermissionKeys.DocumentEdit });
        SetupInReviewVersion();

        var result = await DocumentService.RequestChangesAsync(DocumentId, VersionId, new RequestChangesRequest { Note = "Self review" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).ReviewedById);
    }

    // UTC04: Existing requirements and comments are not modified by a change request
    [Fact]
    public async Task RequestChangesAsync_WithExistingRequirements_LeavesRequirementsUntouched()
    {
        SetupAuthorizedReviewer();
        SetupInReviewVersion();

        var result = await DocumentService.RequestChangesAsync(DocumentId, VersionId, new RequestChangesRequest { Note = "Note" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.Node.ReqCount);
        Assert.Single(await Db.RequirementItems.ToListAsync(TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC05: Document does not exist -> 404 DOCUMENT_NOT_FOUND, guard not consulted
    [Fact]
    public async Task RequestChangesAsync_WithNonExistentDocument_ReturnsNotFound()
    {
        var result = await DocumentService.RequestChangesAsync(999, VersionId, new RequestChangesRequest { Note = "Note" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC06: Document is soft-deleted -> 404 DOCUMENT_NOT_FOUND
    [Fact]
    public async Task RequestChangesAsync_WithDeletedDocument_ReturnsNotFound()
    {
        SetupAuthorizedReviewer();
        var (node, _) = SetupInReviewVersion();
        node.IsDeleted = true;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DocumentService.RequestChangesAsync(DocumentId, VersionId, new RequestChangesRequest { Note = "Note" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
    }

    // UTC07: Unauthenticated caller -> 401, nothing changed
    [Fact]
    public async Task RequestChangesAsync_WhenUnauthenticated_ReturnsUnauthorizedAndKeepsState()
    {
        DocumentTestData.GuardUnauthenticated(AccessGuardMock, ProjectId, PermissionKeys.DocumentApprove);
        SetupInReviewVersion();

        var result = await DocumentService.RequestChangesAsync(DocumentId, VersionId, new RequestChangesRequest { Note = "Note" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        await AssertVersionUntouchedAsync(DocumentVersionStatuses.InReview);
    }

    // UTC08: Permission denied -> 403 FORBIDDEN with required permission, nothing changed
    [Fact]
    public async Task RequestChangesAsync_WhenPermissionDenied_ReturnsForbiddenAndKeepsState()
    {
        DocumentTestData.GuardDeny(AccessGuardMock, ProjectId, PermissionKeys.DocumentApprove);
        SetupInReviewVersion();

        var result = await DocumentService.RequestChangesAsync(DocumentId, VersionId, new RequestChangesRequest { Note = "Note" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.DocumentApprove, result.RequiredPermission);
        await AssertVersionUntouchedAsync(DocumentVersionStatuses.InReview);
    }

    // UTC09: Empty note -> 400 VALIDATION_ERROR (note)
    [Fact]
    public async Task RequestChangesAsync_WithEmptyNote_ReturnsValidationError()
    {
        SetupAuthorizedReviewer();
        SetupInReviewVersion();

        var result = await DocumentService.RequestChangesAsync(DocumentId, VersionId, new RequestChangesRequest { Note = "" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal("Note is required and must be at most 1000 characters.", result.Errors!["note"][0]);
        await AssertVersionUntouchedAsync(DocumentVersionStatuses.InReview);
    }

    // UTC10: Whitespace-only note -> 400 VALIDATION_ERROR (note)
    [Fact]
    public async Task RequestChangesAsync_WithWhitespaceNote_ReturnsValidationError()
    {
        SetupAuthorizedReviewer();
        SetupInReviewVersion();

        var result = await DocumentService.RequestChangesAsync(DocumentId, VersionId, new RequestChangesRequest { Note = "   \t " }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.True(result.Errors!.ContainsKey("note"));
        await AssertVersionUntouchedAsync(DocumentVersionStatuses.InReview);
    }

    // UTC11: Note longer than 1000 characters -> 400 VALIDATION_ERROR (note)
    [Fact]
    public async Task RequestChangesAsync_WithTooLongNote_ReturnsValidationError()
    {
        SetupAuthorizedReviewer();
        SetupInReviewVersion();

        var result = await DocumentService.RequestChangesAsync(DocumentId, VersionId, new RequestChangesRequest { Note = new string('n', 1001) }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.True(result.Errors!.ContainsKey("note"));
        await AssertVersionUntouchedAsync(DocumentVersionStatuses.InReview);
    }

    // UTC12: Invalid note on a missing version -> validation error is reported before version lookup
    [Fact]
    public async Task RequestChangesAsync_WithInvalidNoteAndMissingVersion_ReturnsValidationErrorFirst()
    {
        SetupAuthorizedReviewer();
        SetupInReviewVersion();

        var result = await DocumentService.RequestChangesAsync(DocumentId, 9999, new RequestChangesRequest { Note = "" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    // UTC13: Version does not exist -> 404 VERSION_NOT_FOUND
    [Fact]
    public async Task RequestChangesAsync_WithNonExistentVersion_ReturnsVersionNotFound()
    {
        SetupAuthorizedReviewer();
        SetupInReviewVersion();

        var result = await DocumentService.RequestChangesAsync(DocumentId, 9999, new RequestChangesRequest { Note = "Note" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("VERSION_NOT_FOUND", result.ErrorCode);
    }

    // UTC14: Version belongs to another document -> 404 VERSION_NOT_FOUND
    [Fact]
    public async Task RequestChangesAsync_WithVersionOfAnotherDocument_ReturnsVersionNotFound()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedReviewer();
        SetupInReviewVersion();
        DocumentTestData.AddPageWithVersion(Db, now, 200, 2000, ProjectId, DocumentVersionStatuses.InReview, title: "Other", code: "SRS-02");

        var result = await DocumentService.RequestChangesAsync(DocumentId, 2000, new RequestChangesRequest { Note = "Note" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VERSION_NOT_FOUND", result.ErrorCode);
        Assert.Equal(DocumentVersionStatuses.InReview, (await Db.DocumentVersions.SingleAsync(v => v.Id == 2000, TestContext.Current.CancellationToken)).Status);
    }

    // UTC15-UTC18: Version not IN_REVIEW (DRAFT/CHANGES_REQUESTED/APPROVED/SUPERSEDED) -> 409 INVALID_VERSION_STATE
    [Theory]
    [InlineData(DocumentVersionStatuses.Draft)] // UTC15
    [InlineData(DocumentVersionStatuses.ChangesRequested)] // UTC16
    [InlineData(DocumentVersionStatuses.Approved)] // UTC17
    [InlineData(DocumentVersionStatuses.Superseded)] // UTC18
    public async Task RequestChangesAsync_WithVersionNotInReview_ReturnsInvalidVersionState(string status)
    {
        SetupAuthorizedReviewer();
        SetupInReviewVersion(status);

        var result = await DocumentService.RequestChangesAsync(DocumentId, VersionId, new RequestChangesRequest { Note = "Note" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("INVALID_VERSION_STATE", result.ErrorCode);
        await AssertVersionUntouchedAsync(status);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC19: Note of exactly 1 character (min) -> accepted
    [Fact]
    public async Task RequestChangesAsync_WithNoteOfOneCharacter_Succeeds()
    {
        SetupAuthorizedReviewer();
        SetupInReviewVersion();

        var result = await DocumentService.RequestChangesAsync(DocumentId, VersionId, new RequestChangesRequest { Note = "x" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("x", result.Data!.Current.ReviewNote);
    }

    // UTC20: Note of exactly 1000 characters (max) -> accepted and stored fully
    [Fact]
    public async Task RequestChangesAsync_WithNoteOf1000Characters_Succeeds()
    {
        SetupAuthorizedReviewer();
        SetupInReviewVersion();
        var note = new string('n', 1000);

        var result = await DocumentService.RequestChangesAsync(DocumentId, VersionId, new RequestChangesRequest { Note = note }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(note, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).ReviewNote);
        Assert.Equal(note, (await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken)).Note);
    }

    // UTC21: Note of 1000 characters padded with spaces (trimmed length 1000) -> accepted
    [Fact]
    public async Task RequestChangesAsync_WithPaddedNoteTrimmedTo1000Characters_Succeeds()
    {
        SetupAuthorizedReviewer();
        SetupInReviewVersion();
        var note = new string('n', 1000);

        var result = await DocumentService.RequestChangesAsync(DocumentId, VersionId, new RequestChangesRequest { Note = $"  {note}  " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(note, result.Data!.Current.ReviewNote);
    }

    // UTC22: Note of 1001 characters (max+1) -> validation error
    [Fact]
    public async Task RequestChangesAsync_WithNoteOf1001Characters_ReturnsValidationError()
    {
        SetupAuthorizedReviewer();
        SetupInReviewVersion();

        var result = await DocumentService.RequestChangesAsync(DocumentId, VersionId, new RequestChangesRequest { Note = new string('n', 1001) }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    // UTC23: Null note (defensive) -> treated as empty -> validation error
    [Fact]
    public async Task RequestChangesAsync_WithNullNote_ReturnsValidationError()
    {
        SetupAuthorizedReviewer();
        SetupInReviewVersion();

        var result = await DocumentService.RequestChangesAsync(DocumentId, VersionId, new RequestChangesRequest { Note = null! }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }
}
