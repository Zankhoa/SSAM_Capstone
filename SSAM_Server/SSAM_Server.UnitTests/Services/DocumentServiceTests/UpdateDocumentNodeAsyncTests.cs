using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Documents;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.DocumentServiceTests;

public class UpdateDocumentNodeAsyncTests : DocumentServiceTestBase
{
    private const long ProjectId = 10;
    private const long UserId = 1;

    private void SetupAuthorizedUser()
    {
        CurrentUserMock.Setup(u => u.UserId).Returns(UserId);
        DocumentTestData.GuardAllow(AccessGuardMock, ProjectId, PermissionKeys.DocumentUpdate);
    }

    private DocumentNode SetupPage(long id = 100, string title = "Old Title", long? parentId = null, int sortOrder = 0)
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        return DocumentTestData.AddNode(Db, now, id, ProjectId, DocumentNodeKinds.Page, title, DocumentTypes.Srs, $"SRS-{id}", parentId, sortOrder);
    }

    private DocumentNode SetupFolder(long id, string title = "Folder", long? parentId = null, bool isDeleted = false, long projectId = ProjectId)
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        return DocumentTestData.AddNode(Db, now, id, projectId, DocumentNodeKinds.Folder, title, parentId: parentId, isDeleted: isDeleted);
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Rename a PAGE with a new trimmed title -> 200, title saved, RENAMED activity recorded
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithNewTitleOnPage_RenamesAndRecordsActivity()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        SetupPage(100, "Old Title");

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { Title = "  New Title  " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(100, result.Data.DocumentId);
        Assert.Equal(ProjectId, result.Data.ProjectId);
        Assert.Equal("New Title", result.Data.Title);
        Assert.Equal(DocumentNodeKinds.Page, result.Data.Kind);
        Assert.Equal("SRS-100", result.Data.Code);
        Assert.Equal(0, result.Data.ReqCount);

        var node = await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("New Title", node.Title);
        Assert.Equal(now, node.UpdatedAt);
        var activity = await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DocumentActivityActions.Renamed, activity.Action);
        Assert.Equal(100, activity.DocumentId);
        Assert.Equal(UserId, activity.ActorId);
        Assert.Equal("Đổi tên từ \"Old Title\" sang \"New Title\"", activity.Note);
        Assert.Equal(now, activity.CreatedAt);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.DocumentUpdate, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Rename a FOLDER -> title saved, no activity (activities exist only for pages)
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithNewTitleOnFolder_RenamesWithoutActivity()
    {
        SetupAuthorizedUser();
        SetupFolder(1, "Old Folder");

        var result = await DocumentService.UpdateDocumentNodeAsync(1, new UpdateDocumentNodeRequest { Title = "New Folder" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("New Folder", result.Data!.Title);
        Assert.Equal(DocumentNodeKinds.Folder, result.Data.Kind);
        Assert.Equal("New Folder", (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).Title);
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC03: Same title as current (ordinal equal) -> success, no RENAMED activity
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithUnchangedTitle_DoesNotRecordActivity()
    {
        SetupAuthorizedUser();
        SetupPage(100, "Same Title");

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { Title = "  Same Title " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Same Title", result.Data!.Title);
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC04: Title differing only by case -> treated as a rename (ordinal comparison)
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithTitleDifferingOnlyByCase_RecordsRename()
    {
        SetupAuthorizedUser();
        SetupPage(100, "my doc");

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { Title = "My Doc" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("My Doc", (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).Title);
        Assert.Single(await Db.DocumentActivities.Where(a => a.Action == DocumentActivityActions.Renamed).ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC05: Move a PAGE into a folder -> ParentId updated, MOVED activity recorded
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithMovePageToFolder_UpdatesParentAndRecordsActivity()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        SetupFolder(1, "Target");
        SetupPage(100);

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { ParentId = 1 }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.ParentId);
        Assert.Equal("Old Title", result.Data.Title);
        Assert.Equal(1, (await Db.DocumentNodes.SingleAsync(n => n.Id == 100, TestContext.Current.CancellationToken)).ParentId);
        var activity = await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DocumentActivityActions.Moved, activity.Action);
        Assert.Equal(100, activity.DocumentId);
        Assert.Equal(UserId, activity.ActorId);
        Assert.Equal("Di chuyển vị trí tài liệu trong cây thư mục", activity.Note);
        Assert.Equal(now, activity.CreatedAt);
    }

    // UTC06: Move a PAGE back to root (explicit null ParentId) -> ParentId cleared, MOVED activity recorded
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithMovePageToRoot_ClearsParent()
    {
        SetupAuthorizedUser();
        SetupFolder(1, "Source");
        SetupPage(100, parentId: 1);

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { ParentId = null }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.ParentId);
        Assert.Null((await Db.DocumentNodes.SingleAsync(n => n.Id == 100, TestContext.Current.CancellationToken)).ParentId);
        Assert.Single(await Db.DocumentActivities.Where(a => a.Action == DocumentActivityActions.Moved).ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC07: Move a FOLDER into another folder -> ParentId updated, no activity
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithMoveFolderToFolder_UpdatesParentWithoutActivity()
    {
        SetupAuthorizedUser();
        SetupFolder(1, "Target");
        SetupFolder(2, "Moving");

        var result = await DocumentService.UpdateDocumentNodeAsync(2, new UpdateDocumentNodeRequest { ParentId = 1 }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.ParentId);
        Assert.Equal(1, (await Db.DocumentNodes.SingleAsync(n => n.Id == 2, TestContext.Current.CancellationToken)).ParentId);
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC08: Move a PAGE under another PAGE (child page) -> allowed
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithMovePageUnderPage_UpdatesParent()
    {
        SetupAuthorizedUser();
        SetupPage(100, "Parent page");
        SetupPage(101, "Child page");

        var result = await DocumentService.UpdateDocumentNodeAsync(101, new UpdateDocumentNodeRequest { ParentId = 100 }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Data!.ParentId);
        Assert.Equal(100, (await Db.DocumentNodes.SingleAsync(n => n.Id == 101, TestContext.Current.CancellationToken)).ParentId);
    }

    // UTC09: ParentId equal to the current parent -> no change and no MOVED activity
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithSameParent_DoesNotRecordMove()
    {
        SetupAuthorizedUser();
        SetupFolder(1, "Same parent");
        SetupPage(100, parentId: 1);

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { ParentId = 1 }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.ParentId);
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC10: Request without ParentId specified -> parent untouched
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithParentNotSpecified_KeepsExistingParent()
    {
        SetupAuthorizedUser();
        SetupFolder(1, "Parent");
        SetupPage(100, parentId: 1);

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { SortOrder = 3 }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.ParentId);
        Assert.Equal(3, result.Data.SortOrder);
        Assert.Equal(1, (await Db.DocumentNodes.SingleAsync(n => n.Id == 100, TestContext.Current.CancellationToken)).ParentId);
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC11: Update only SortOrder -> SortOrder saved, title unchanged
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithSortOrderOnly_UpdatesSortOrder()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        SetupPage(100, sortOrder: 2);

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { SortOrder = 7 }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Data!.SortOrder);
        var node = await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(7, node.SortOrder);
        Assert.Equal("Old Title", node.Title);
        Assert.Equal(now, node.UpdatedAt);
    }

    // UTC12: Empty request (all null, parent not specified) -> success, nothing changed except UpdatedAt
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithEmptyRequest_OnlyTouchesUpdatedAt()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        SetupPage(100, "Keep", sortOrder: 4);

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var node = await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Keep", node.Title);
        Assert.Equal(4, node.SortOrder);
        Assert.Null(node.ParentId);
        Assert.Equal(now, node.UpdatedAt);
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC13: Title + parent + sort order together -> all applied, two activities (RENAMED and MOVED)
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithTitleParentAndSortOrder_AppliesAllChanges()
    {
        SetupAuthorizedUser();
        SetupFolder(1, "Target");
        SetupPage(100, "Old Title");

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { Title = "Renamed", ParentId = 1, SortOrder = 9 }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Renamed", result.Data!.Title);
        Assert.Equal(1, result.Data.ParentId);
        Assert.Equal(9, result.Data.SortOrder);
        var activities = await Db.DocumentActivities.OrderBy(a => a.Id).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { DocumentActivityActions.Renamed, DocumentActivityActions.Moved }, activities.Select(a => a.Action).ToArray());
    }

    // UTC14: Page with a current version, requirements and required approvers -> DTO carries version ref, req count and approver refs
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithPageHavingVersionAndApprovers_ReturnsEnrichedDto()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        var (_, version) = DocumentTestData.AddPageWithVersion(Db, now, 100, 1000, ProjectId, requiredApprovers: "[2]", code: "SRS-01");
        DocumentTestData.AddRequirement(Db, 100, version.Id, "REQ-001");
        DocumentTestData.AddRequirement(Db, 100, version.Id, "REQ-002", sortOrder: 1);

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { Title = "Renamed" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.ReqCount);
        Assert.NotNull(result.Data.CurrentVersion);
        Assert.Equal(1000, result.Data.CurrentVersion!.VersionId);
        Assert.Equal("1.0", result.Data.CurrentVersion.Label);
        Assert.Equal(DocumentVersionStatuses.Draft, result.Data.CurrentVersion.Status);
        var approver = Assert.Single(result.Data.RequiredApprovers);
        Assert.Equal(2, approver.UserId);
        Assert.Equal("Reviewer Two", approver.FullName);
        Assert.Equal("user2@ssam.vn", approver.Email);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"), result.Data.UpdatedAt);
        var activity = await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1000, activity.VersionId);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC15: Document does not exist -> 404 DOCUMENT_NOT_FOUND, guard never consulted
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithNonExistentDocument_ReturnsNotFound()
    {
        var result = await DocumentService.UpdateDocumentNodeAsync(999, new UpdateDocumentNodeRequest { Title = "x" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC16: Document is soft-deleted -> 404 DOCUMENT_NOT_FOUND
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithDeletedDocument_ReturnsNotFound()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        DocumentTestData.AddNode(Db, now, 100, ProjectId, title: "Deleted", isDeleted: true);

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { Title = "x" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
        Assert.Equal("Deleted", (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).Title);
    }

    // UTC17: Unauthenticated caller -> 401 from guard, node unchanged
    [Fact]
    public async Task UpdateDocumentNodeAsync_WhenUnauthenticated_ReturnsUnauthorizedAndKeepsNode()
    {
        DocumentTestData.GuardUnauthenticated(AccessGuardMock, ProjectId, PermissionKeys.DocumentUpdate);
        SetupPage(100, "Old Title");

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { Title = "New" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("Old Title", (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).Title);
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC18: Permission denied -> 403 FORBIDDEN with required permission, node unchanged
    [Fact]
    public async Task UpdateDocumentNodeAsync_WhenPermissionDenied_ReturnsForbiddenAndKeepsNode()
    {
        DocumentTestData.GuardDeny(AccessGuardMock, ProjectId, PermissionKeys.DocumentUpdate);
        SetupPage(100, "Old Title");

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { Title = "New" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.DocumentUpdate, result.RequiredPermission);
        Assert.Equal("Old Title", (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).Title);
    }

    // UTC19: Empty title -> 400 VALIDATION_ERROR (title), node unchanged
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithEmptyTitle_ReturnsValidationError()
    {
        SetupAuthorizedUser();
        SetupPage(100, "Old Title");

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { Title = "" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal("Title must be between 1 and 150 characters.", result.Errors!["title"][0]);
        Assert.Equal("Old Title", (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).Title);
    }

    // UTC20: Whitespace-only title -> 400 VALIDATION_ERROR (title)
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithWhitespaceTitle_ReturnsValidationError()
    {
        SetupAuthorizedUser();
        SetupPage(100, "Old Title");

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { Title = "    " }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.True(result.Errors!.ContainsKey("title"));
        Assert.Equal("Old Title", (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).Title);
    }

    // UTC21: Moving a node into itself -> 400 INVALID_PARENT
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithParentEqualToSelf_ReturnsInvalidParent()
    {
        SetupAuthorizedUser();
        SetupFolder(1, "Self");

        var result = await DocumentService.UpdateDocumentNodeAsync(1, new UpdateDocumentNodeRequest { ParentId = 1 }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("INVALID_PARENT", result.ErrorCode);
        Assert.Null((await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).ParentId);
    }

    // UTC22: Target parent does not exist -> 400 INVALID_PARENT, no move activity
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithNonExistentParent_ReturnsInvalidParent()
    {
        SetupAuthorizedUser();
        SetupPage(100);

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { ParentId = 999 }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_PARENT", result.ErrorCode);
        Assert.Null((await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).ParentId);
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC23: Target parent is in another project -> 400 INVALID_PARENT
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithParentInAnotherProject_ReturnsInvalidParent()
    {
        SetupAuthorizedUser();
        SetupFolder(1, "Foreign", projectId: 99);
        SetupPage(100);

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { ParentId = 1 }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_PARENT", result.ErrorCode);
        Assert.Null((await Db.DocumentNodes.SingleAsync(n => n.Id == 100, TestContext.Current.CancellationToken)).ParentId);
    }

    // UTC24: Target parent is soft-deleted -> 400 INVALID_PARENT
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithDeletedParent_ReturnsInvalidParent()
    {
        SetupAuthorizedUser();
        SetupFolder(1, "Deleted", isDeleted: true);
        SetupPage(100);

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { ParentId = 1 }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_PARENT", result.ErrorCode);
        Assert.Null((await Db.DocumentNodes.SingleAsync(n => n.Id == 100, TestContext.Current.CancellationToken)).ParentId);
    }

    // UTC25: Moving a FOLDER under a PAGE -> 400 INVALID_PARENT
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithFolderMovedUnderPage_ReturnsInvalidParent()
    {
        SetupAuthorizedUser();
        SetupPage(100, "Page");
        SetupFolder(1, "Folder");

        var result = await DocumentService.UpdateDocumentNodeAsync(1, new UpdateDocumentNodeRequest { ParentId = 100 }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("INVALID_PARENT", result.ErrorCode);
        Assert.Null((await Db.DocumentNodes.SingleAsync(n => n.Id == 1, TestContext.Current.CancellationToken)).ParentId);
    }

    // UTC26: Moving a folder into its own descendant -> 400 INVALID_PARENT (cycle)
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithFolderMovedIntoDescendant_ReturnsInvalidParentCycle()
    {
        SetupAuthorizedUser();
        SetupFolder(1, "Root");
        SetupFolder(2, "Child", parentId: 1);
        SetupFolder(3, "Grandchild", parentId: 2);

        var result = await DocumentService.UpdateDocumentNodeAsync(1, new UpdateDocumentNodeRequest { ParentId = 3 }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("INVALID_PARENT", result.ErrorCode);
        Assert.Equal("Cannot move a folder into one of its descendants.", result.ErrorMessage);
        Assert.Null((await Db.DocumentNodes.SingleAsync(n => n.Id == 1, TestContext.Current.CancellationToken)).ParentId);
    }

    // UTC27: Moving a PAGE under its own child page -> 400 INVALID_PARENT (cycle)
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithPageMovedUnderOwnChildPage_ReturnsInvalidParentCycle()
    {
        SetupAuthorizedUser();
        SetupPage(100, "Parent page");
        SetupPage(101, "Child page", parentId: 100);

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { ParentId = 101 }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_PARENT", result.ErrorCode);
        Assert.Null((await Db.DocumentNodes.SingleAsync(n => n.Id == 100, TestContext.Current.CancellationToken)).ParentId);
    }

    // UTC28: Valid new title but invalid parent -> error returned and the rename is not persisted
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithValidTitleButInvalidParent_DoesNotPersistRename()
    {
        SetupAuthorizedUser();
        SetupPage(100, "Old Title");

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { Title = "New Title", ParentId = 999 }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_PARENT", result.ErrorCode);
        Db.ChangeTracker.Clear();
        Assert.Equal("Old Title", (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).Title);
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC29: Title of 1 character (min) -> renamed
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithTitleOfOneCharacter_Renames()
    {
        SetupAuthorizedUser();
        SetupPage(100);

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { Title = "A" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("A", result.Data!.Title);
    }

    // UTC30: Title of exactly 150 characters (max) -> renamed
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithTitleOf150Characters_Renames()
    {
        SetupAuthorizedUser();
        SetupPage(100);
        var title = new string('T', 150);

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { Title = title }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(title, (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).Title);
    }

    // UTC31: Title of 151 characters (max+1) -> 400 validation error, title unchanged
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithTitleOf151Characters_ReturnsValidationError()
    {
        SetupAuthorizedUser();
        SetupPage(100, "Old Title");

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { Title = new string('T', 151) }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.True(result.Errors!.ContainsKey("title"));
        Assert.Equal("Old Title", (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).Title);
    }

    // UTC32: SortOrder of 0 (min typical) -> applied
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithSortOrderZero_Applies()
    {
        SetupAuthorizedUser();
        SetupPage(100, sortOrder: 5);

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { SortOrder = 0 }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).SortOrder);
    }

    // UTC33: Negative SortOrder (no lower bound enforced) -> applied as given
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithNegativeSortOrder_AppliesWithoutValidation()
    {
        SetupAuthorizedUser();
        SetupPage(100, sortOrder: 5);

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { SortOrder = -1 }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(-1, (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).SortOrder);
    }

    // UTC34: SortOrder of int.MaxValue -> applied
    [Fact]
    public async Task UpdateDocumentNodeAsync_WithMaxIntSortOrder_Applies()
    {
        SetupAuthorizedUser();
        SetupPage(100);

        var result = await DocumentService.UpdateDocumentNodeAsync(100, new UpdateDocumentNodeRequest { SortOrder = int.MaxValue }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(int.MaxValue, result.Data!.SortOrder);
    }
}
