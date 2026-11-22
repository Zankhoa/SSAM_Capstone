using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Documents;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.DocumentServiceTests;

public class SubmitVersionAsyncTests : DocumentServiceTestBase
{
    private const long ProjectId = 10;
    private const long DocumentId = 100;
    private const long VersionId = 1000;
    private const long SubmitterId = 1;

    private void SetupAuthorizedUser(IReadOnlyCollection<string>? permissions = null)
    {
        CurrentUserMock.Setup(u => u.UserId).Returns(SubmitterId);
        DocumentTestData.GuardAllow(AccessGuardMock, ProjectId, PermissionKeys.DocumentUpdate);
        DocumentTestData.GuardAllow(AccessGuardMock, ProjectId, PermissionKeys.DocumentView);
        PermissionServiceMock
            .Setup(p => p.GetProjectPermissionsAsync(SubmitterId, ProjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions ?? new[] { PermissionKeys.DocumentEdit });
    }

    /// <summary>Seeds a page with one version having REQ-001 and REQ-002.</summary>
    private (DocumentNode Node, DocumentVersion Version) SetupSubmittableVersion(
        string status = DocumentVersionStatuses.Draft,
        string contentText = "Clean text",
        string requiredApprovers = "[]",
        bool withRequirements = true)
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var seeded = DocumentTestData.AddPageWithVersion(Db, now, DocumentId, VersionId, ProjectId, status, contentText: contentText, requiredApprovers: requiredApprovers, rowVersion: 4);
        if (withRequirements)
        {
            DocumentTestData.AddRequirement(Db, DocumentId, VersionId, "REQ-001");
            DocumentTestData.AddRequirement(Db, DocumentId, VersionId, "REQ-002", sortOrder: 1);
        }
        return seeded;
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Submit a DRAFT with requirements and no request body -> 200, IN_REVIEW, submitter recorded, every REQ marked MAJOR, activity SUBMITTED
    [Fact]
    public async Task SubmitVersionAsync_WithDraftVersionAndNoRequest_MovesToInReviewWithMajorChanges()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        SetupSubmittableVersion();

        var result = await DocumentService.SubmitVersionAsync(DocumentId, VersionId, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(DocumentId, result.Data.Node.DocumentId);
        Assert.Equal(2, result.Data.Node.ReqCount);
        Assert.Equal(VersionId, result.Data.Current.VersionId);
        Assert.Equal(DocumentVersionStatuses.InReview, result.Data.Current.Status);
        Assert.Equal(5, result.Data.Current.RowVersion);
        Assert.Equal(SubmitterId, result.Data.Current.SubmittedBy!.UserId);
        Assert.Equal("2026-11-21T10:00:00.000Z", result.Data.Current.SubmittedAt);
        Assert.Null(result.Data.Current.ReviewedBy);
        Assert.Null(result.Data.Current.ReviewNote);
        Assert.False(result.Data.Can.Edit);
        Assert.False(result.Data.Can.Approve);
        Assert.Single(result.Data.Versions);

        var version = await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DocumentVersionStatuses.InReview, version.Status);
        Assert.Equal(SubmitterId, version.SubmittedById);
        Assert.Equal(now, version.SubmittedAt);
        Assert.Null(version.ReviewedById);
        Assert.Null(version.ReviewedAt);
        Assert.Null(version.ReviewNote);
        Assert.Equal(SubmitterId, version.UpdatedById);
        Assert.Equal(now, version.UpdatedAt);
        Assert.Equal(5, version.RowVersion);
        Assert.Equal(now, (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).UpdatedAt);

        var changes = await Db.DocumentVersionReqChanges.OrderBy(c => c.ReqUid).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "REQ-001", "REQ-002" }, changes.Select(c => c.ReqUid).ToArray());
        Assert.All(changes, c =>
        {
            Assert.Equal(VersionId, c.VersionId);
            Assert.Equal("MAJOR", c.Level);
        });

        var activity = await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DocumentActivityActions.Submitted, activity.Action);
        Assert.Equal(DocumentId, activity.DocumentId);
        Assert.Equal(VersionId, activity.VersionId);
        Assert.Equal(SubmitterId, activity.ActorId);
        Assert.Equal("Gửi duyệt phiên bản v1.0", activity.Note);
        Assert.Equal(now, activity.CreatedAt);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.DocumentUpdate, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Submit with minorReqUids (case/space-insensitive, blanks ignored) -> matching REQ marked MINOR, others MAJOR
    [Fact]
    public async Task SubmitVersionAsync_WithMinorReqUids_MarksThoseRequirementsMinor()
    {
        SetupAuthorizedUser();
        SetupSubmittableVersion();
        var request = new SubmitDocumentRequest { MinorReqUids = new List<string> { "  req-002 ", " ", "", "REQ-999" } };

        var result = await DocumentService.SubmitVersionAsync(DocumentId, VersionId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var changes = await Db.DocumentVersionReqChanges.OrderBy(c => c.ReqUid).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, changes.Count);
        Assert.Equal("MAJOR", changes.Single(c => c.ReqUid == "REQ-001").Level);
        Assert.Equal("MINOR", changes.Single(c => c.ReqUid == "REQ-002").Level);
    }

    // UTC03: Resubmit a CHANGES_REQUESTED version -> IN_REVIEW again, previous review data cleared, old req-changes replaced
    [Fact]
    public async Task SubmitVersionAsync_WithChangesRequestedVersion_ResubmitsAndClearsReviewData()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        var (_, version) = SetupSubmittableVersion(DocumentVersionStatuses.ChangesRequested);
        version.ReviewedById = 2;
        version.ReviewedAt = now.AddHours(-1);
        version.ReviewNote = "Please fix";
        Db.DocumentVersionReqChanges.Add(new DocumentVersionReqChange { VersionId = VersionId, ReqUid = "REQ-OLD", Level = "MINOR" });
        Db.DocumentVersionReqChanges.Add(new DocumentVersionReqChange { VersionId = VersionId, ReqUid = "REQ-001", Level = "MINOR" });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DocumentService.SubmitVersionAsync(DocumentId, VersionId, new SubmitDocumentRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var stored = await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DocumentVersionStatuses.InReview, stored.Status);
        Assert.Null(stored.ReviewedById);
        Assert.Null(stored.ReviewedAt);
        Assert.Null(stored.ReviewNote);
        var changes = await Db.DocumentVersionReqChanges.OrderBy(c => c.ReqUid).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "REQ-001", "REQ-002" }, changes.Select(c => c.ReqUid).ToArray());
        Assert.All(changes, c => Assert.Equal("MAJOR", c.Level));
    }

    // UTC04: Required approvers list contains other users -> submit allowed
    [Fact]
    public async Task SubmitVersionAsync_WithOtherRequiredApprovers_Submits()
    {
        SetupAuthorizedUser();
        SetupSubmittableVersion(requiredApprovers: "[2,3]");

        var result = await DocumentService.SubmitVersionAsync(DocumentId, VersionId, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentVersionStatuses.InReview, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Status);
    }

    // UTC05: Submitter is one of several approvers -> allowed (only the sole-approver case is blocked)
    [Fact]
    public async Task SubmitVersionAsync_WhenSubmitterIsOneOfSeveralApprovers_Submits()
    {
        SetupAuthorizedUser();
        SetupSubmittableVersion(requiredApprovers: "[1,2]");

        var result = await DocumentService.SubmitVersionAsync(DocumentId, VersionId, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
    }

    // UTC06: Malformed RequiredApprovers JSON -> treated as no approvers and submit succeeds
    [Fact]
    public async Task SubmitVersionAsync_WithMalformedRequiredApproversJson_TreatsAsNoApprovers()
    {
        SetupAuthorizedUser();
        SetupSubmittableVersion(requiredApprovers: "not-json");

        var result = await DocumentService.SubmitVersionAsync(DocumentId, VersionId, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentVersionStatuses.InReview, result.Data!.Current.Status);
    }

    // UTC07: Caller also holds approve permission -> returned Can.Approve is true (version now IN_REVIEW)
    [Fact]
    public async Task SubmitVersionAsync_WithApprovePermission_ReturnsApproveCapability()
    {
        SetupAuthorizedUser(new[] { PermissionKeys.DocumentEdit, PermissionKeys.DocumentApprove, PermissionKeys.DocumentDelete });
        SetupSubmittableVersion();

        var result = await DocumentService.SubmitVersionAsync(DocumentId, VersionId, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.Can.Approve);
        Assert.False(result.Data.Can.Edit);
        Assert.False(result.Data.Can.Revise);
        Assert.True(result.Data.Can.Delete);
    }

    // UTC08: Duplicate requirement uids (case-insensitive) in the version -> a single req-change per uid
    [Fact]
    public async Task SubmitVersionAsync_WithDuplicateRequirementUids_CreatesOneChangePerUid()
    {
        SetupAuthorizedUser();
        SetupSubmittableVersion(withRequirements: false);
        DocumentTestData.AddRequirement(Db, DocumentId, VersionId, "REQ-001");
        DocumentTestData.AddRequirement(Db, DocumentId, VersionId, "req-001", sortOrder: 1);

        var result = await DocumentService.SubmitVersionAsync(DocumentId, VersionId, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(await Db.DocumentVersionReqChanges.ToListAsync(TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC09: Document does not exist -> 404 DOCUMENT_NOT_FOUND, guard not consulted
    [Fact]
    public async Task SubmitVersionAsync_WithNonExistentDocument_ReturnsNotFound()
    {
        var result = await DocumentService.SubmitVersionAsync(999, VersionId, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC10: Document is soft-deleted -> 404 DOCUMENT_NOT_FOUND
    [Fact]
    public async Task SubmitVersionAsync_WithDeletedDocument_ReturnsNotFound()
    {
        SetupAuthorizedUser();
        var (node, _) = SetupSubmittableVersion();
        node.IsDeleted = true;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DocumentService.SubmitVersionAsync(DocumentId, VersionId, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
    }

    // UTC11: Unauthenticated caller -> 401, version stays DRAFT
    [Fact]
    public async Task SubmitVersionAsync_WhenUnauthenticated_ReturnsUnauthorizedAndKeepsDraft()
    {
        DocumentTestData.GuardUnauthenticated(AccessGuardMock, ProjectId, PermissionKeys.DocumentUpdate);
        SetupSubmittableVersion();

        var result = await DocumentService.SubmitVersionAsync(DocumentId, VersionId, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal(DocumentVersionStatuses.Draft, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Status);
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC12: Permission denied -> 403 FORBIDDEN with required permission, version stays DRAFT
    [Fact]
    public async Task SubmitVersionAsync_WhenPermissionDenied_ReturnsForbiddenAndKeepsDraft()
    {
        DocumentTestData.GuardDeny(AccessGuardMock, ProjectId, PermissionKeys.DocumentUpdate);
        SetupSubmittableVersion();

        var result = await DocumentService.SubmitVersionAsync(DocumentId, VersionId, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.DocumentUpdate, result.RequiredPermission);
        Assert.Equal(DocumentVersionStatuses.Draft, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Status);
    }

    // UTC13: Version does not exist -> 404 VERSION_NOT_FOUND
    [Fact]
    public async Task SubmitVersionAsync_WithNonExistentVersion_ReturnsVersionNotFound()
    {
        SetupAuthorizedUser();
        SetupSubmittableVersion();

        var result = await DocumentService.SubmitVersionAsync(DocumentId, 9999, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("VERSION_NOT_FOUND", result.ErrorCode);
    }

    // UTC14: Version belongs to another document -> 404 VERSION_NOT_FOUND
    [Fact]
    public async Task SubmitVersionAsync_WithVersionOfAnotherDocument_ReturnsVersionNotFound()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        SetupSubmittableVersion();
        DocumentTestData.AddPageWithVersion(Db, now, 200, 2000, ProjectId, title: "Other", code: "SRS-02");

        var result = await DocumentService.SubmitVersionAsync(DocumentId, 2000, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VERSION_NOT_FOUND", result.ErrorCode);
        Assert.Equal(DocumentVersionStatuses.Draft, (await Db.DocumentVersions.SingleAsync(v => v.Id == 2000, TestContext.Current.CancellationToken)).Status);
    }

    // UTC15-UTC17: Version not in DRAFT/CHANGES_REQUESTED (IN_REVIEW/APPROVED/SUPERSEDED) -> 409 INVALID_VERSION_STATE
    [Theory]
    [InlineData(DocumentVersionStatuses.InReview)] // UTC15
    [InlineData(DocumentVersionStatuses.Approved)] // UTC16
    [InlineData(DocumentVersionStatuses.Superseded)] // UTC17
    public async Task SubmitVersionAsync_WithNonSubmittableStatus_ReturnsInvalidVersionState(string status)
    {
        SetupAuthorizedUser();
        SetupSubmittableVersion(status);

        var result = await DocumentService.SubmitVersionAsync(DocumentId, VersionId, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("INVALID_VERSION_STATE", result.ErrorCode);
        Assert.Equal(status, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Status);
        Assert.Empty(await Db.DocumentVersionReqChanges.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC18: Version has no requirements -> 409 NO_REQUIREMENTS
    [Fact]
    public async Task SubmitVersionAsync_WithNoRequirements_ReturnsNoRequirements()
    {
        SetupAuthorizedUser();
        SetupSubmittableVersion(withRequirements: false);

        var result = await DocumentService.SubmitVersionAsync(DocumentId, VersionId, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("NO_REQUIREMENTS", result.ErrorCode);
        Assert.Equal(DocumentVersionStatuses.Draft, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Status);
    }

    // UTC19: Content still contains two "[Cần BA xác nhận]" markers -> 409 NEEDS_CONFIRMATION mentioning the count
    [Fact]
    public async Task SubmitVersionAsync_WithPendingConfirmationMarkers_ReturnsNeedsConfirmation()
    {
        SetupAuthorizedUser();
        SetupSubmittableVersion(contentText: "A [Cần BA xác nhận] B [Cần BA xác nhận] C");

        var result = await DocumentService.SubmitVersionAsync(DocumentId, VersionId, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("NEEDS_CONFIRMATION", result.ErrorCode);
        Assert.Contains("Còn 2 chỗ", result.ErrorMessage);
        Assert.Equal(DocumentVersionStatuses.Draft, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Status);
        Assert.Empty(await Db.DocumentVersionReqChanges.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC20: Submitter is the sole required approver -> 409 SOLE_APPROVER_IS_SUBMITTER
    [Fact]
    public async Task SubmitVersionAsync_WhenSubmitterIsSoleApprover_ReturnsConflict()
    {
        SetupAuthorizedUser();
        SetupSubmittableVersion(requiredApprovers: "[1]");

        var result = await DocumentService.SubmitVersionAsync(DocumentId, VersionId, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("SOLE_APPROVER_IS_SUBMITTER", result.ErrorCode);
        Assert.Equal(DocumentVersionStatuses.Draft, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Status);
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC21: NO_REQUIREMENTS is reported before NEEDS_CONFIRMATION (check order)
    [Fact]
    public async Task SubmitVersionAsync_WithNoRequirementsAndPendingMarkers_ReturnsNoRequirementsFirst()
    {
        SetupAuthorizedUser();
        SetupSubmittableVersion(contentText: "[Cần BA xác nhận]", withRequirements: false);

        var result = await DocumentService.SubmitVersionAsync(DocumentId, VersionId, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("NO_REQUIREMENTS", result.ErrorCode);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC22: Exactly one "[Cần BA xác nhận]" marker (min blocking count) -> 409 NEEDS_CONFIRMATION with count 1
    [Fact]
    public async Task SubmitVersionAsync_WithSinglePendingMarker_ReturnsNeedsConfirmation()
    {
        SetupAuthorizedUser();
        SetupSubmittableVersion(contentText: "Only one [Cần BA xác nhận] here");

        var result = await DocumentService.SubmitVersionAsync(DocumentId, VersionId, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("NEEDS_CONFIRMATION", result.ErrorCode);
        Assert.Contains("Còn 1 chỗ", result.ErrorMessage);
    }

    // UTC23: Marker text with different casing/spelling does not match -> submit succeeds
    [Fact]
    public async Task SubmitVersionAsync_WithMarkerOfDifferentCase_Submits()
    {
        SetupAuthorizedUser();
        SetupSubmittableVersion(contentText: "[cần ba xác nhận]");

        var result = await DocumentService.SubmitVersionAsync(DocumentId, VersionId, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
    }

    // UTC24: Exactly one requirement (min) -> submit succeeds with a single req-change
    [Fact]
    public async Task SubmitVersionAsync_WithSingleRequirement_Submits()
    {
        SetupAuthorizedUser();
        SetupSubmittableVersion(withRequirements: false);
        DocumentTestData.AddRequirement(Db, DocumentId, VersionId, "REQ-001");

        var result = await DocumentService.SubmitVersionAsync(DocumentId, VersionId, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(await Db.DocumentVersionReqChanges.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC25: Required approvers [1,1] (duplicates collapse to the submitter alone) -> 409 SOLE_APPROVER_IS_SUBMITTER
    [Fact]
    public async Task SubmitVersionAsync_WithDuplicateSubmitterApprovers_ReturnsSoleApproverConflict()
    {
        SetupAuthorizedUser();
        SetupSubmittableVersion(requiredApprovers: "[1,1]");

        var result = await DocumentService.SubmitVersionAsync(DocumentId, VersionId, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("SOLE_APPROVER_IS_SUBMITTER", result.ErrorCode);
    }

    // UTC26: Required approvers [0,1] (non-positive ids ignored -> submitter alone) -> 409 SOLE_APPROVER_IS_SUBMITTER
    [Fact]
    public async Task SubmitVersionAsync_WithNonPositiveApproverIdsAndSubmitter_ReturnsSoleApproverConflict()
    {
        SetupAuthorizedUser();
        SetupSubmittableVersion(requiredApprovers: "[0,1]");

        var result = await DocumentService.SubmitVersionAsync(DocumentId, VersionId, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("SOLE_APPROVER_IS_SUBMITTER", result.ErrorCode);
    }

    // UTC27: Empty minorReqUids list -> all requirements MAJOR
    [Fact]
    public async Task SubmitVersionAsync_WithEmptyMinorList_MarksAllMajor()
    {
        SetupAuthorizedUser();
        SetupSubmittableVersion();

        var result = await DocumentService.SubmitVersionAsync(DocumentId, VersionId, new SubmitDocumentRequest { MinorReqUids = new List<string>() }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.All(await Db.DocumentVersionReqChanges.ToListAsync(TestContext.Current.CancellationToken), c => Assert.Equal("MAJOR", c.Level));
    }
}
