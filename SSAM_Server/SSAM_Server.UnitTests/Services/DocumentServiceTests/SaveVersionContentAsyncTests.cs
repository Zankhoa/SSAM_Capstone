using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Documents;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.DocumentServiceTests;

public class SaveVersionContentAsyncTests : DocumentServiceTestBase
{
    private const long ProjectId = 10;
    private const long DocumentId = 100;
    private const long VersionId = 1000;
    private const long UserId = 2;

    private (DocumentNode Node, DocumentVersion Version) SetupDraftVersion(
        string status = DocumentVersionStatuses.Draft,
        int rowVersion = 3,
        string? subtitle = "Old subtitle")
    {
        CurrentUserMock.Setup(u => u.UserId).Returns(UserId);
        DocumentTestData.GuardAllow(AccessGuardMock, ProjectId, PermissionKeys.DocumentUpdate);
        var now = Clock.GetUtcNow().UtcDateTime;
        var seeded = DocumentTestData.AddPageWithVersion(Db, now, DocumentId, VersionId, ProjectId, status, rowVersion: rowVersion);
        seeded.Version.Subtitle = subtitle;
        Db.SaveChanges();
        return seeded;
    }

    private static SaveDocumentContentRequest Request(JsonElement? content = null, int rowVersion = 3) => new()
    {
        Content = content ?? DocumentTestData.DocWithReqs(("REQ-001", "Login")),
        RowVersion = rowVersion
    };

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Save content with two new requirements on a DRAFT -> 200, content/text/rowVersion updated, requirements stored, CONTENT_SAVED activity
    [Fact]
    public async Task SaveVersionContentAsync_WithNewRequirementsOnDraft_SavesContentAndRequirements()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupDraftVersion();
        var request = Request(DocumentTestData.DocWithReqs(("REQ-001", "Login"), ("REQ-002", "Logout")), rowVersion: 3);

        var result = await DocumentService.SaveVersionContentAsync(DocumentId, VersionId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(VersionId, result.Data.Version.VersionId);
        Assert.Equal(DocumentId, result.Data.Version.DocumentId);
        Assert.Equal("1.0", result.Data.Version.Label);
        Assert.Equal(DocumentVersionStatuses.Draft, result.Data.Version.Status);
        Assert.Equal(4, result.Data.Version.RowVersion);
        Assert.Equal(UserId, result.Data.Version.UpdatedBy.UserId);
        Assert.Equal("Reviewer Two", result.Data.Version.UpdatedBy.FullName);
        Assert.Equal(1, result.Data.Version.Author.UserId);
        Assert.Equal("2026-11-21T10:00:00.000Z", result.Data.Version.UpdatedAt);
        Assert.Equal(JsonValueKind.Object, result.Data.Version.Content.ValueKind);

        Assert.Equal(2, result.Data.Requirements.Count);
        Assert.Equal(new[] { "REQ-001", "REQ-002" }, result.Data.Requirements.Select(r => r.ReqUid).ToArray());
        Assert.Equal(new[] { "Login", "Logout" }, result.Data.Requirements.Select(r => r.Title).ToArray());
        Assert.Equal(new[] { 0, 1 }, result.Data.Requirements.Select(r => r.SortOrder).ToArray());

        var version = await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(4, version.RowVersion);
        Assert.Equal(UserId, version.UpdatedById);
        Assert.Equal(now, version.UpdatedAt);
        Assert.Contains("REQ-001", version.Content);
        Assert.Contains("Body of REQ-002", version.ContentText);

        var reqs = await Db.RequirementItems.OrderBy(r => r.SortOrder).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "REQ-001", "REQ-002" }, reqs.Select(r => r.ReqUid).ToArray());
        Assert.All(reqs, r =>
        {
            Assert.Equal(DocumentId, r.DocumentId);
            Assert.Equal(VersionId, r.VersionId);
            Assert.False(string.IsNullOrWhiteSpace(r.ContentHash));
        });

        var activity = await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DocumentActivityActions.ContentSaved, activity.Action);
        Assert.Equal(DocumentId, activity.DocumentId);
        Assert.Equal(VersionId, activity.VersionId);
        Assert.Equal(UserId, activity.ActorId);
        Assert.Equal("Thêm REQ-001, REQ-002", activity.Note);
        Assert.Equal(now, activity.CreatedAt);

        var node = await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(now, node.UpdatedAt);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.DocumentUpdate, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Save on a CHANGES_REQUESTED version -> allowed (still editable) and status unchanged
    [Fact]
    public async Task SaveVersionContentAsync_OnChangesRequestedVersion_Saves()
    {
        SetupDraftVersion(DocumentVersionStatuses.ChangesRequested);

        var result = await DocumentService.SaveVersionContentAsync(DocumentId, VersionId, Request(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentVersionStatuses.ChangesRequested, result.Data!.Version.Status);
        var version = await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DocumentVersionStatuses.ChangesRequested, version.Status);
        Assert.Equal(4, version.RowVersion);
    }

    // UTC03: Replacing existing requirements -> old rows removed, modified/added/removed summarised in the activity note
    [Fact]
    public async Task SaveVersionContentAsync_WithModifiedAddedAndRemovedRequirements_ReplacesRequirementsAndSummarisesChanges()
    {
        SetupDraftVersion();
        DocumentTestData.AddRequirement(Db, DocumentId, VersionId, "REQ-001", "Old login", "old-hash");
        DocumentTestData.AddRequirement(Db, DocumentId, VersionId, "REQ-002", "Removed", "hash-2", 1);

        var request = Request(DocumentTestData.DocWithReqs(("REQ-001", "Login"), ("REQ-003", "New")));
        var result = await DocumentService.SaveVersionContentAsync(DocumentId, VersionId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var reqs = await Db.RequirementItems.OrderBy(r => r.SortOrder).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "REQ-001", "REQ-003" }, reqs.Select(r => r.ReqUid).ToArray());
        Assert.Equal("Login", reqs[0].Title);
        var activity = await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Sửa REQ-001 · Thêm REQ-003 · Xoá REQ-002", activity.Note);
    }

    // UTC04: Saving content that results in no requirement change (no requirements before/after) -> no activity written but version still updated
    [Fact]
    public async Task SaveVersionContentAsync_WithNoRequirementChanges_DoesNotRecordActivity()
    {
        SetupDraftVersion();
        var request = Request(JsonDocument.Parse("{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"Just text\"}]}]}").RootElement.Clone());

        var result = await DocumentService.SaveVersionContentAsync(DocumentId, VersionId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Requirements);
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
        var version = await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(4, version.RowVersion);
        Assert.Equal("Just text", version.ContentText);
    }

    // UTC05: Subtitle specified with surrounding spaces -> trimmed and stored
    [Fact]
    public async Task SaveVersionContentAsync_WithSubtitleSpecified_TrimsAndStoresSubtitle()
    {
        SetupDraftVersion();
        var request = Request();
        request.Subtitle = "  New subtitle  ";

        var result = await DocumentService.SaveVersionContentAsync(DocumentId, VersionId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("New subtitle", result.Data!.Version.Subtitle);
        Assert.Equal("New subtitle", (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Subtitle);
    }

    // UTC06: Subtitle explicitly set to null -> subtitle cleared
    [Fact]
    public async Task SaveVersionContentAsync_WithSubtitleExplicitlyNull_ClearsSubtitle()
    {
        SetupDraftVersion(subtitle: "Existing");
        var request = Request();
        request.Subtitle = null;

        var result = await DocumentService.SaveVersionContentAsync(DocumentId, VersionId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.Version.Subtitle);
        Assert.Null((await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Subtitle);
    }

    // UTC07: Subtitle not specified -> existing subtitle preserved
    [Fact]
    public async Task SaveVersionContentAsync_WithSubtitleNotSpecified_KeepsExistingSubtitle()
    {
        SetupDraftVersion(subtitle: "Existing");

        var result = await DocumentService.SaveVersionContentAsync(DocumentId, VersionId, Request(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Existing", result.Data!.Version.Subtitle);
        Assert.Equal("Existing", (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Subtitle);
    }

    // UTC08: Content that cannot be read (default JsonElement) -> falls back to an empty doc and removes all requirements
    [Fact]
    public async Task SaveVersionContentAsync_WithUnreadableContent_StoresEmptyDocumentAndRemovesRequirements()
    {
        SetupDraftVersion();
        DocumentTestData.AddRequirement(Db, DocumentId, VersionId, "REQ-001");

        var request = new SaveDocumentContentRequest { Content = default, RowVersion = 3 };
        var result = await DocumentService.SaveVersionContentAsync(DocumentId, VersionId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Requirements);
        Assert.Empty(await Db.RequirementItems.ToListAsync(TestContext.Current.CancellationToken));
        var version = await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("{\"type\":\"doc\",\"content\":[]}", version.Content);
        Assert.Equal(string.Empty, version.ContentText);
        var activity = await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Xoá REQ-001", activity.Note);
    }

    // UTC09: Requirements of other versions of the same document are untouched
    [Fact]
    public async Task SaveVersionContentAsync_WithOtherVersionRequirements_LeavesThemUntouched()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupDraftVersion();
        Db.DocumentVersions.Add(new DocumentVersion { Id = 2000, DocumentId = DocumentId, Label = "0.9", Status = DocumentVersionStatuses.Superseded, AuthorId = 1, UpdatedById = 1, CreatedAt = now, UpdatedAt = now });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        DocumentTestData.AddRequirement(Db, DocumentId, 2000, "REQ-900");

        var result = await DocumentService.SaveVersionContentAsync(DocumentId, VersionId, Request(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var other = await Db.RequirementItems.Where(r => r.VersionId == 2000).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal("REQ-900", Assert.Single(other).ReqUid);
        Assert.Single(await Db.RequirementItems.Where(r => r.VersionId == VersionId).ToListAsync(TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC10: Document does not exist -> 404 DOCUMENT_NOT_FOUND, guard not consulted
    [Fact]
    public async Task SaveVersionContentAsync_WithNonExistentDocument_ReturnsNotFound()
    {
        var result = await DocumentService.SaveVersionContentAsync(999, VersionId, Request(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC11: Document is soft-deleted -> 404 DOCUMENT_NOT_FOUND
    [Fact]
    public async Task SaveVersionContentAsync_WithDeletedDocument_ReturnsNotFound()
    {
        SetupDraftVersion();
        var node = await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken);
        node.IsDeleted = true;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DocumentService.SaveVersionContentAsync(DocumentId, VersionId, Request(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
    }

    // UTC12: Unauthenticated caller -> 401, version unchanged
    [Fact]
    public async Task SaveVersionContentAsync_WhenUnauthenticated_ReturnsUnauthorizedAndKeepsVersion()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        DocumentTestData.GuardUnauthenticated(AccessGuardMock, ProjectId, PermissionKeys.DocumentUpdate);
        DocumentTestData.AddPageWithVersion(Db, now, DocumentId, VersionId, ProjectId);

        var result = await DocumentService.SaveVersionContentAsync(DocumentId, VersionId, Request(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal(1, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).RowVersion);
        Assert.Empty(await Db.RequirementItems.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC13: Permission denied -> 403 FORBIDDEN with required permission, version unchanged
    [Fact]
    public async Task SaveVersionContentAsync_WhenPermissionDenied_ReturnsForbiddenAndKeepsVersion()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        DocumentTestData.GuardDeny(AccessGuardMock, ProjectId, PermissionKeys.DocumentUpdate);
        DocumentTestData.AddPageWithVersion(Db, now, DocumentId, VersionId, ProjectId);

        var result = await DocumentService.SaveVersionContentAsync(DocumentId, VersionId, Request(rowVersion: 1), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.DocumentUpdate, result.RequiredPermission);
        Assert.Equal(1, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).RowVersion);
    }

    // UTC14: Version id does not exist -> 404 VERSION_NOT_FOUND
    [Fact]
    public async Task SaveVersionContentAsync_WithNonExistentVersion_ReturnsVersionNotFound()
    {
        SetupDraftVersion();

        var result = await DocumentService.SaveVersionContentAsync(DocumentId, 9999, Request(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("VERSION_NOT_FOUND", result.ErrorCode);
    }

    // UTC15: Version belongs to a different document -> 404 VERSION_NOT_FOUND
    [Fact]
    public async Task SaveVersionContentAsync_WithVersionOfAnotherDocument_ReturnsVersionNotFound()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupDraftVersion();
        DocumentTestData.AddPageWithVersion(Db, now, 200, 2000, ProjectId, title: "Other", code: "SRS-02");

        var result = await DocumentService.SaveVersionContentAsync(DocumentId, 2000, Request(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VERSION_NOT_FOUND", result.ErrorCode);
        Assert.Equal(1, (await Db.DocumentVersions.SingleAsync(v => v.Id == 2000, TestContext.Current.CancellationToken)).RowVersion);
    }

    // UTC16-UTC18: Version is locked (IN_REVIEW / APPROVED / SUPERSEDED) -> 409 VERSION_LOCKED, nothing changed
    [Theory]
    [InlineData(DocumentVersionStatuses.InReview)] // UTC16
    [InlineData(DocumentVersionStatuses.Approved)] // UTC17
    [InlineData(DocumentVersionStatuses.Superseded)] // UTC18
    public async Task SaveVersionContentAsync_WithLockedVersion_ReturnsVersionLocked(string status)
    {
        SetupDraftVersion(status);

        var result = await DocumentService.SaveVersionContentAsync(DocumentId, VersionId, Request(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("VERSION_LOCKED", result.ErrorCode);
        var version = await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, version.RowVersion);
        Assert.Equal("{\"type\":\"doc\",\"content\":[]}", version.Content);
        Assert.Empty(await Db.RequirementItems.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC19: Stale RowVersion -> 409 VERSION_CONFLICT carrying the current rowVersion, updatedAt and updatedBy
    [Fact]
    public async Task SaveVersionContentAsync_WithStaleRowVersion_ReturnsVersionConflictWithCurrentState()
    {
        SetupDraftVersion(rowVersion: 5);

        var result = await DocumentService.SaveVersionContentAsync(DocumentId, VersionId, Request(rowVersion: 4), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("VERSION_CONFLICT", result.ErrorCode);
        var current = Assert.IsType<VersionConflictCurrentDto>(result.Current);
        Assert.Equal(5, current.RowVersion);
        Assert.Equal("2026-11-21T10:00:00.000Z", current.UpdatedAt);
        Assert.Equal(1, current.UpdatedBy.UserId);
        Assert.Equal("Author One", current.UpdatedBy.FullName);
        var version = await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(5, version.RowVersion);
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC20: RowVersion ahead of the stored one -> also a 409 VERSION_CONFLICT
    [Fact]
    public async Task SaveVersionContentAsync_WithRowVersionAheadOfStored_ReturnsVersionConflict()
    {
        SetupDraftVersion(rowVersion: 3);

        var result = await DocumentService.SaveVersionContentAsync(DocumentId, VersionId, Request(rowVersion: 4), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VERSION_CONFLICT", result.ErrorCode);
    }

    // UTC21: Subtitle longer than 300 characters -> 400 VALIDATION_ERROR (subtitle), nothing saved
    [Fact]
    public async Task SaveVersionContentAsync_WithTooLongSubtitle_ReturnsValidationError()
    {
        SetupDraftVersion(subtitle: "Existing");
        var request = Request();
        request.Subtitle = new string('s', 301);

        var result = await DocumentService.SaveVersionContentAsync(DocumentId, VersionId, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal("Subtitle must be at most 300 characters.", result.Errors!["subtitle"][0]);
        var version = await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Existing", version.Subtitle);
        Assert.Equal(3, version.RowVersion);
    }

    // UTC22: Locked status is reported before a stale RowVersion (status check precedes row-version check)
    [Fact]
    public async Task SaveVersionContentAsync_WithLockedVersionAndStaleRowVersion_ReturnsVersionLockedFirst()
    {
        SetupDraftVersion(DocumentVersionStatuses.Approved, rowVersion: 5);

        var result = await DocumentService.SaveVersionContentAsync(DocumentId, VersionId, Request(rowVersion: 1), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VERSION_LOCKED", result.ErrorCode);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC23: Subtitle of exactly 300 characters (max) -> saved
    [Fact]
    public async Task SaveVersionContentAsync_WithSubtitleOf300Characters_Saves()
    {
        SetupDraftVersion();
        var request = Request();
        request.Subtitle = new string('s', 300);

        var result = await DocumentService.SaveVersionContentAsync(DocumentId, VersionId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new string('s', 300), (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Subtitle);
    }

    // UTC24: Subtitle of 301 characters (max+1) -> validation error (covered with padding: untrimmed length is checked)
    [Fact]
    public async Task SaveVersionContentAsync_WithSubtitleOf301CharactersPaddedWithSpaces_ReturnsValidationError()
    {
        SetupDraftVersion();
        var request = Request();
        request.Subtitle = " " + new string('s', 300);

        var result = await DocumentService.SaveVersionContentAsync(DocumentId, VersionId, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    // UTC25: Empty subtitle string -> accepted and stored as empty string
    [Fact]
    public async Task SaveVersionContentAsync_WithEmptySubtitle_StoresEmptyString()
    {
        SetupDraftVersion();
        var request = Request();
        request.Subtitle = "";

        var result = await DocumentService.SaveVersionContentAsync(DocumentId, VersionId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Subtitle);
    }

    // UTC26: RowVersion equal to the stored value (exact match) -> accepted and incremented by exactly one
    [Fact]
    public async Task SaveVersionContentAsync_WithMatchingRowVersion_IncrementsRowVersionByOne()
    {
        SetupDraftVersion(rowVersion: 1);

        var result = await DocumentService.SaveVersionContentAsync(DocumentId, VersionId, Request(rowVersion: 1), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Version.RowVersion);
        Assert.Equal(2, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).RowVersion);
    }

    // UTC27: RowVersion 0 against stored 1 -> 409 VERSION_CONFLICT
    [Fact]
    public async Task SaveVersionContentAsync_WithRowVersionZero_ReturnsVersionConflict()
    {
        SetupDraftVersion(rowVersion: 1);

        var result = await DocumentService.SaveVersionContentAsync(DocumentId, VersionId, Request(rowVersion: 0), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VERSION_CONFLICT", result.ErrorCode);
        Assert.Equal(1, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).RowVersion);
    }
}
