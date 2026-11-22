using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Documents;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.DocumentServiceTests;

public class ApproveVersionAsyncTests : DocumentServiceTestBase
{
    private const long ProjectId = 10;
    private const long DocumentId = 100;
    private const long VersionId = 1000;
    private const long SubmitterId = 1;
    private const long ApproverId = 2;

    private void SetupAuthorizedApprover(IReadOnlyCollection<string>? permissions = null)
    {
        CurrentUserMock.Setup(u => u.UserId).Returns(ApproverId);
        DocumentTestData.GuardAllow(AccessGuardMock, ProjectId, PermissionKeys.DocumentApprove);
        DocumentTestData.GuardAllow(AccessGuardMock, ProjectId, PermissionKeys.DocumentView);
        PermissionServiceMock
            .Setup(p => p.GetProjectPermissionsAsync(ApproverId, ProjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions ?? new[] { PermissionKeys.DocumentApprove, PermissionKeys.DocumentEdit });
    }

    /// <summary>Seeds a page whose current version is IN_REVIEW, submitted by user 1, with REQ-001 and REQ-002.</summary>
    private (DocumentNode Node, DocumentVersion Version) SetupInReviewVersion(string status = DocumentVersionStatuses.InReview, string requiredApprovers = "[]")
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var seeded = DocumentTestData.AddPageWithVersion(Db, now, DocumentId, VersionId, ProjectId, status, requiredApprovers: requiredApprovers, rowVersion: 6, submittedById: SubmitterId);
        DocumentTestData.AddRequirement(Db, DocumentId, VersionId, "REQ-001", contentHash: "new-1");
        DocumentTestData.AddRequirement(Db, DocumentId, VersionId, "REQ-002", contentHash: "same", sortOrder: 1);
        return seeded;
    }

    private void AddComment(long id, long? parentId = null, bool resolved = false, long versionId = VersionId, long documentId = DocumentId)
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        Db.DocumentComments.Add(new DocumentComment
        {
            Id = id,
            DocumentId = documentId,
            VersionId = versionId,
            ParentId = parentId,
            Body = $"Comment {id}",
            AuthorId = SubmitterId,
            CreatedAt = now,
            ResolvedById = resolved ? ApproverId : null,
            ResolvedAt = resolved ? now : null
        });
        Db.SaveChanges();
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Approve an IN_REVIEW version first time with a note -> APPROVED baseline, reviewer recorded, node baseline/current set, activity uses the note
    [Fact]
    public async Task ApproveVersionAsync_WithNoteAndNoPreviousBaseline_ApprovesAndSetsBaseline()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedApprover();
        SetupInReviewVersion();

        var result = await DocumentService.ApproveVersionAsync(DocumentId, VersionId, new ApproveDocumentRequest { Note = "  LGTM  " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(DocumentVersionStatuses.Approved, result.Data.Current.Status);
        Assert.Equal(7, result.Data.Current.RowVersion);
        Assert.Equal(ApproverId, result.Data.Current.ReviewedBy!.UserId);
        Assert.Equal("LGTM", result.Data.Current.ReviewNote);
        Assert.Equal("2026-11-21T10:00:00.000Z", result.Data.Current.ReviewedAt);
        Assert.Equal(SubmitterId, result.Data.Current.SubmittedBy!.UserId);
        Assert.True(result.Data.Node.IsBaselined);
        Assert.Equal(VersionId, result.Data.Node.BaselineVersionId);
        Assert.Equal(VersionId, result.Data.Node.CurrentVersion!.VersionId);
        Assert.Equal(DocumentVersionStatuses.Approved, result.Data.Node.CurrentVersion.Status);
        Assert.False(result.Data.Can.Approve);
        Assert.False(result.Data.Can.Edit);

        var version = await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DocumentVersionStatuses.Approved, version.Status);
        Assert.Equal(ApproverId, version.ReviewedById);
        Assert.Equal(now, version.ReviewedAt);
        Assert.Equal("LGTM", version.ReviewNote);
        Assert.Equal(ApproverId, version.UpdatedById);
        Assert.Equal(now, version.UpdatedAt);
        Assert.Equal(7, version.RowVersion);

        var node = await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(VersionId, node.BaselineVersionId);
        Assert.Equal(VersionId, node.CurrentVersionId);
        Assert.Equal(now, node.UpdatedAt);

        var activity = await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DocumentActivityActions.Approved, activity.Action);
        Assert.Equal(DocumentId, activity.DocumentId);
        Assert.Equal(VersionId, activity.VersionId);
        Assert.Equal(ApproverId, activity.ActorId);
        Assert.Equal("LGTM", activity.Note);
        Assert.Equal(now, activity.CreatedAt);

        RequirementSyncMock.Verify(s => s.PropagateBaselineChangesAsync(
            It.Is<DocumentNode>(n => n.Id == DocumentId),
            It.Is<DocumentVersion>(v => v.Id == VersionId),
            It.Is<IReadOnlyList<RequirementItem>>(r => r.Count == 2),
            ApproverId,
            now,
            It.IsAny<CancellationToken>()), Times.Once);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.DocumentApprove, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Approve without note and without a previous baseline -> ReviewNote null, default activity note "Phê duyệt baseline v1.0"
    [Fact]
    public async Task ApproveVersionAsync_WithoutNoteAndNoPreviousBaseline_UsesDefaultActivityNote()
    {
        SetupAuthorizedApprover();
        SetupInReviewVersion();

        var result = await DocumentService.ApproveVersionAsync(DocumentId, VersionId, new ApproveDocumentRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.Current.ReviewNote);
        Assert.Null((await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).ReviewNote);
        Assert.Equal("Phê duyệt baseline v1.0", (await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken)).Note);
    }

    // UTC03: Whitespace-only note -> treated as no note
    [Fact]
    public async Task ApproveVersionAsync_WithWhitespaceNote_TreatsNoteAsEmpty()
    {
        SetupAuthorizedApprover();
        SetupInReviewVersion();

        var result = await DocumentService.ApproveVersionAsync(DocumentId, VersionId, new ApproveDocumentRequest { Note = "   " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null((await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).ReviewNote);
        Assert.Equal("Phê duyệt baseline v1.0", (await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken)).Note);
    }

    // UTC04: Approve when a previous baseline exists and no note -> previous baseline SUPERSEDED, activity note summarises requirement diff
    [Fact]
    public async Task ApproveVersionAsync_WithPreviousBaselineAndNoNote_SupersedesPreviousAndSummarisesDiff()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedApprover();
        var (node, _) = SetupInReviewVersion();
        Db.DocumentVersions.Add(new DocumentVersion { Id = 900, DocumentId = DocumentId, Label = "0.9", Status = DocumentVersionStatuses.Approved, AuthorId = 1, UpdatedById = 1, CreatedAt = now.AddDays(-5), UpdatedAt = now.AddDays(-5) });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        DocumentTestData.AddRequirement(Db, DocumentId, 900, "REQ-001", contentHash: "old-1");
        DocumentTestData.AddRequirement(Db, DocumentId, 900, "REQ-002", contentHash: "same", sortOrder: 1);
        DocumentTestData.AddRequirement(Db, DocumentId, 900, "REQ-003", contentHash: "gone", sortOrder: 2);
        node.BaselineVersionId = 900;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DocumentService.ApproveVersionAsync(DocumentId, VersionId, new ApproveDocumentRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var previous = await Db.DocumentVersions.SingleAsync(v => v.Id == 900, TestContext.Current.CancellationToken);
        Assert.Equal(DocumentVersionStatuses.Superseded, previous.Status);
        Assert.Equal(now, previous.UpdatedAt);
        Assert.Equal(DocumentVersionStatuses.Approved, (await Db.DocumentVersions.SingleAsync(v => v.Id == VersionId, TestContext.Current.CancellationToken)).Status);
        var storedNode = await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(VersionId, storedNode.BaselineVersionId);
        Assert.Equal(VersionId, storedNode.CurrentVersionId);
        Assert.Equal("Sửa REQ-001 · Xoá REQ-003", (await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken)).Note);
        Assert.Equal(2, result.Data!.Versions.Count);
    }

    // UTC05: Approve with a previous baseline and an explicit note -> the note wins over the diff summary
    [Fact]
    public async Task ApproveVersionAsync_WithPreviousBaselineAndNote_UsesNoteForActivity()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedApprover();
        var (node, _) = SetupInReviewVersion();
        Db.DocumentVersions.Add(new DocumentVersion { Id = 900, DocumentId = DocumentId, Label = "0.9", Status = DocumentVersionStatuses.Approved, AuthorId = 1, UpdatedById = 1, CreatedAt = now, UpdatedAt = now });
        node.BaselineVersionId = 900;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DocumentService.ApproveVersionAsync(DocumentId, VersionId, new ApproveDocumentRequest { Note = "Approved with minor edits" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Approved with minor edits", (await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken)).Note);
        Assert.Equal(DocumentVersionStatuses.Superseded, (await Db.DocumentVersions.SingleAsync(v => v.Id == 900, TestContext.Current.CancellationToken)).Status);
    }

    // UTC06: Previous baseline id points to a missing version -> no crash, version still approved
    [Fact]
    public async Task ApproveVersionAsync_WithDanglingPreviousBaseline_StillApproves()
    {
        SetupAuthorizedApprover();
        var (node, _) = SetupInReviewVersion();
        node.BaselineVersionId = 12345;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DocumentService.ApproveVersionAsync(DocumentId, VersionId, new ApproveDocumentRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(VersionId, (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).BaselineVersionId);
        Assert.Equal(DocumentVersionStatuses.Approved, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Status);
    }

    // UTC07: Baseline already equals the approved version -> not superseded; note is the diff against an empty set ("Thêm ...")
    [Fact]
    public async Task ApproveVersionAsync_WhenBaselineAlreadyEqualsVersion_DoesNotSupersedeItself()
    {
        SetupAuthorizedApprover();
        var (node, _) = SetupInReviewVersion();
        node.BaselineVersionId = VersionId;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DocumentService.ApproveVersionAsync(DocumentId, VersionId, new ApproveDocumentRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentVersionStatuses.Approved, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Status);
        Assert.Equal("Thêm REQ-001, REQ-002", (await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken)).Note);
    }

    // UTC08: Approver is listed among the required approvers -> approved
    [Fact]
    public async Task ApproveVersionAsync_WhenCallerIsRequiredApprover_Approves()
    {
        SetupAuthorizedApprover();
        SetupInReviewVersion(requiredApprovers: "[2,3]");
        DocumentTestData.AddUser(Db, 3);

        var result = await DocumentService.ApproveVersionAsync(DocumentId, VersionId, new ApproveDocumentRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Node.RequiredApprovers.Count);
    }

    // UTC09: Only resolved root comments and unresolved replies exist -> not blocking, approved
    [Fact]
    public async Task ApproveVersionAsync_WithResolvedRootsAndOpenReplies_Approves()
    {
        SetupAuthorizedApprover();
        SetupInReviewVersion();
        AddComment(1, resolved: true);
        AddComment(2, parentId: 1, resolved: false);

        var result = await DocumentService.ApproveVersionAsync(DocumentId, VersionId, new ApproveDocumentRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentVersionStatuses.Approved, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Status);
    }

    // UTC10: Open comment threads on another version or document do not block approval
    [Fact]
    public async Task ApproveVersionAsync_WithOpenCommentsOnOtherVersionOrDocument_Approves()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedApprover();
        SetupInReviewVersion();
        DocumentTestData.AddPageWithVersion(Db, now, 200, 2000, ProjectId, title: "Other", code: "SRS-02");
        AddComment(1, versionId: 5555);
        AddComment(2, versionId: 2000, documentId: 200);

        var result = await DocumentService.ApproveVersionAsync(DocumentId, VersionId, new ApproveDocumentRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC11: Document does not exist -> 404 DOCUMENT_NOT_FOUND, guard not consulted
    [Fact]
    public async Task ApproveVersionAsync_WithNonExistentDocument_ReturnsNotFound()
    {
        var result = await DocumentService.ApproveVersionAsync(999, VersionId, new ApproveDocumentRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC12: Document is soft-deleted -> 404 DOCUMENT_NOT_FOUND
    [Fact]
    public async Task ApproveVersionAsync_WithDeletedDocument_ReturnsNotFound()
    {
        SetupAuthorizedApprover();
        var (node, _) = SetupInReviewVersion();
        node.IsDeleted = true;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DocumentService.ApproveVersionAsync(DocumentId, VersionId, new ApproveDocumentRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
    }

    // UTC13: Unauthenticated caller -> 401, version stays IN_REVIEW
    [Fact]
    public async Task ApproveVersionAsync_WhenUnauthenticated_ReturnsUnauthorizedAndKeepsState()
    {
        DocumentTestData.GuardUnauthenticated(AccessGuardMock, ProjectId, PermissionKeys.DocumentApprove);
        SetupInReviewVersion();

        var result = await DocumentService.ApproveVersionAsync(DocumentId, VersionId, new ApproveDocumentRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal(DocumentVersionStatuses.InReview, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Status);
        RequirementSyncMock.Verify(s => s.PropagateBaselineChangesAsync(It.IsAny<DocumentNode>(), It.IsAny<DocumentVersion>(), It.IsAny<IReadOnlyList<RequirementItem>>(), It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC14: Permission denied -> 403 FORBIDDEN with required permission, nothing changed
    [Fact]
    public async Task ApproveVersionAsync_WhenPermissionDenied_ReturnsForbiddenAndKeepsState()
    {
        DocumentTestData.GuardDeny(AccessGuardMock, ProjectId, PermissionKeys.DocumentApprove);
        SetupInReviewVersion();

        var result = await DocumentService.ApproveVersionAsync(DocumentId, VersionId, new ApproveDocumentRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.DocumentApprove, result.RequiredPermission);
        Assert.Equal(DocumentVersionStatuses.InReview, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Status);
    }

    // UTC15: Version does not exist -> 404 VERSION_NOT_FOUND
    [Fact]
    public async Task ApproveVersionAsync_WithNonExistentVersion_ReturnsVersionNotFound()
    {
        SetupAuthorizedApprover();
        SetupInReviewVersion();

        var result = await DocumentService.ApproveVersionAsync(DocumentId, 9999, new ApproveDocumentRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("VERSION_NOT_FOUND", result.ErrorCode);
    }

    // UTC16: Version belongs to another document -> 404 VERSION_NOT_FOUND
    [Fact]
    public async Task ApproveVersionAsync_WithVersionOfAnotherDocument_ReturnsVersionNotFound()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedApprover();
        SetupInReviewVersion();
        DocumentTestData.AddPageWithVersion(Db, now, 200, 2000, ProjectId, DocumentVersionStatuses.InReview, title: "Other", code: "SRS-02");

        var result = await DocumentService.ApproveVersionAsync(DocumentId, 2000, new ApproveDocumentRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VERSION_NOT_FOUND", result.ErrorCode);
        Assert.Equal(DocumentVersionStatuses.InReview, (await Db.DocumentVersions.SingleAsync(v => v.Id == 2000, TestContext.Current.CancellationToken)).Status);
    }

    // UTC17-UTC20: Version not IN_REVIEW (DRAFT/CHANGES_REQUESTED/APPROVED/SUPERSEDED) -> 409 INVALID_VERSION_STATE
    [Theory]
    [InlineData(DocumentVersionStatuses.Draft)] // UTC17
    [InlineData(DocumentVersionStatuses.ChangesRequested)] // UTC18
    [InlineData(DocumentVersionStatuses.Approved)] // UTC19
    [InlineData(DocumentVersionStatuses.Superseded)] // UTC20
    public async Task ApproveVersionAsync_WithVersionNotInReview_ReturnsInvalidVersionState(string status)
    {
        SetupAuthorizedApprover();
        SetupInReviewVersion(status);

        var result = await DocumentService.ApproveVersionAsync(DocumentId, VersionId, new ApproveDocumentRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("INVALID_VERSION_STATE", result.ErrorCode);
        Assert.Equal(status, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Status);
        Assert.Null((await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).BaselineVersionId);
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC21: Submitter tries to approve their own version -> 409 SELF_APPROVAL_NOT_ALLOWED
    [Fact]
    public async Task ApproveVersionAsync_WhenCallerIsSubmitter_ReturnsSelfApprovalNotAllowed()
    {
        SetupAuthorizedApprover();
        CurrentUserMock.Setup(u => u.UserId).Returns(SubmitterId);
        SetupInReviewVersion();

        var result = await DocumentService.ApproveVersionAsync(DocumentId, VersionId, new ApproveDocumentRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("SELF_APPROVAL_NOT_ALLOWED", result.ErrorCode);
        Assert.Equal(DocumentVersionStatuses.InReview, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Status);
    }

    // UTC22: Required approvers are configured and caller is not one of them -> 403 NOT_REQUIRED_APPROVER
    [Fact]
    public async Task ApproveVersionAsync_WhenCallerNotInRequiredApprovers_ReturnsNotRequiredApprover()
    {
        SetupAuthorizedApprover();
        SetupInReviewVersion(requiredApprovers: "[3,4]");

        var result = await DocumentService.ApproveVersionAsync(DocumentId, VersionId, new ApproveDocumentRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("NOT_REQUIRED_APPROVER", result.ErrorCode);
        Assert.Equal(DocumentVersionStatuses.InReview, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Status);
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC23: Two open root comment threads -> 409 OPEN_COMMENTS with the count in errors
    [Fact]
    public async Task ApproveVersionAsync_WithOpenCommentThreads_ReturnsOpenCommentsConflict()
    {
        SetupAuthorizedApprover();
        SetupInReviewVersion();
        AddComment(1);
        AddComment(2);
        AddComment(3, resolved: true);

        var result = await DocumentService.ApproveVersionAsync(DocumentId, VersionId, new ApproveDocumentRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("OPEN_COMMENTS", result.ErrorCode);
        Assert.Equal("Còn 2 chuỗi bình luận chưa giải quyết.", result.ErrorMessage);
        Assert.Equal(new[] { "2" }, result.Errors!["openComments"]);
        Assert.Equal(DocumentVersionStatuses.InReview, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Status);
        RequirementSyncMock.Verify(s => s.PropagateBaselineChangesAsync(It.IsAny<DocumentNode>(), It.IsAny<DocumentVersion>(), It.IsAny<IReadOnlyList<RequirementItem>>(), It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC24: Self-approval is reported before required-approver mismatch (check order)
    [Fact]
    public async Task ApproveVersionAsync_WhenSubmitterNotInApproverList_ReturnsSelfApprovalFirst()
    {
        SetupAuthorizedApprover();
        CurrentUserMock.Setup(u => u.UserId).Returns(SubmitterId);
        SetupInReviewVersion(requiredApprovers: "[3]");

        var result = await DocumentService.ApproveVersionAsync(DocumentId, VersionId, new ApproveDocumentRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("SELF_APPROVAL_NOT_ALLOWED", result.ErrorCode);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC25: Exactly one open comment thread (min blocking) -> 409 OPEN_COMMENTS with count 1
    [Fact]
    public async Task ApproveVersionAsync_WithSingleOpenComment_ReturnsOpenCommentsConflict()
    {
        SetupAuthorizedApprover();
        SetupInReviewVersion();
        AddComment(1);

        var result = await DocumentService.ApproveVersionAsync(DocumentId, VersionId, new ApproveDocumentRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("OPEN_COMMENTS", result.ErrorCode);
        Assert.Equal(new[] { "1" }, result.Errors!["openComments"]);
    }

    // UTC26: Zero comments -> approved
    [Fact]
    public async Task ApproveVersionAsync_WithNoComments_Approves()
    {
        SetupAuthorizedApprover();
        SetupInReviewVersion();

        var result = await DocumentService.ApproveVersionAsync(DocumentId, VersionId, new ApproveDocumentRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
    }

    // UTC27: Required approvers is the caller alone (and caller is not the submitter) -> approved
    [Fact]
    public async Task ApproveVersionAsync_WhenCallerIsSoleRequiredApprover_Approves()
    {
        SetupAuthorizedApprover();
        SetupInReviewVersion(requiredApprovers: "[2]");

        var result = await DocumentService.ApproveVersionAsync(DocumentId, VersionId, new ApproveDocumentRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ApproverId, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).ReviewedById);
    }

    // UTC28: Version without any requirements -> still approved, sync receives an empty requirement list
    [Fact]
    public async Task ApproveVersionAsync_WithNoRequirements_ApprovesAndPropagatesEmptyList()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedApprover();
        DocumentTestData.AddPageWithVersion(Db, now, DocumentId, VersionId, ProjectId, DocumentVersionStatuses.InReview, submittedById: SubmitterId);

        var result = await DocumentService.ApproveVersionAsync(DocumentId, VersionId, new ApproveDocumentRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        RequirementSyncMock.Verify(s => s.PropagateBaselineChangesAsync(
            It.IsAny<DocumentNode>(),
            It.IsAny<DocumentVersion>(),
            It.Is<IReadOnlyList<RequirementItem>>(r => r.Count == 0),
            ApproverId,
            now,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
