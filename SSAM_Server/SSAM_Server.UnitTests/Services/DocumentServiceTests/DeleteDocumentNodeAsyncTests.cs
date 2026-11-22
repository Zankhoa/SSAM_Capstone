using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.DocumentServiceTests;

public class DeleteDocumentNodeAsyncTests : DocumentServiceTestBase
{
    private const long ProjectId = 10;
    private const long UserId = 1;

    private void SetupAuthorizedUser()
    {
        CurrentUserMock.Setup(u => u.UserId).Returns(UserId);
        DocumentTestData.GuardAllow(AccessGuardMock, ProjectId, PermissionKeys.DocumentDelete);
    }

    private DocumentNode AddFolder(long id, long? parentId = null, long projectId = ProjectId, bool isDeleted = false) =>
        DocumentTestData.AddNode(Db, Clock.GetUtcNow().UtcDateTime, id, projectId, DocumentNodeKinds.Folder, $"Folder {id}", parentId: parentId, isDeleted: isDeleted);

    private DocumentNode AddPage(long id, long? parentId = null, long projectId = ProjectId, bool isDeleted = false) =>
        DocumentTestData.AddNode(Db, Clock.GetUtcNow().UtcDateTime, id, projectId, DocumentNodeKinds.Page, $"Page {id}", DocumentTypes.Srs, $"SRS-{id}", parentId, isDeleted: isDeleted);

    private async Task SetBaselineAsync(DocumentNode node, long versionId)
    {
        node.BaselineVersionId = versionId;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Delete a single PAGE (not baselined) -> 204, soft-deleted, DELETED activity linked to current version
    [Fact]
    public async Task DeleteDocumentNodeAsync_WithPage_SoftDeletesAndRecordsActivity()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        DocumentTestData.AddPageWithVersion(Db, now, 100, 1000, ProjectId);

        var result = await DocumentService.DeleteDocumentNodeAsync(100, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status204NoContent, result.StatusCode);
        var node = await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken);
        Assert.True(node.IsDeleted);
        Assert.Equal(now, node.UpdatedAt);
        var activity = await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DocumentActivityActions.Deleted, activity.Action);
        Assert.Equal(100, activity.DocumentId);
        Assert.Equal(1000, activity.VersionId);
        Assert.Equal(UserId, activity.ActorId);
        Assert.Equal("Xoá tài liệu", activity.Note);
        Assert.Equal(now, activity.CreatedAt);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.DocumentDelete, It.IsAny<CancellationToken>()), Times.Once);
        // The version itself is retained
        Assert.Single(await Db.DocumentVersions.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC02: Delete an empty FOLDER -> 204, soft-deleted, no activity (folders are not audited)
    [Fact]
    public async Task DeleteDocumentNodeAsync_WithEmptyFolder_SoftDeletesWithoutActivity()
    {
        SetupAuthorizedUser();
        AddFolder(1);

        var result = await DocumentService.DeleteDocumentNodeAsync(1, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status204NoContent, result.StatusCode);
        Assert.True((await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).IsDeleted);
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC03: Delete a FOLDER with nested folders and pages -> whole subtree deleted, one activity per page, others untouched
    [Fact]
    public async Task DeleteDocumentNodeAsync_WithFolderSubtree_DeletesAllDescendantsAndLeavesOthers()
    {
        SetupAuthorizedUser();
        AddFolder(1);
        AddFolder(2, parentId: 1);
        AddPage(3, parentId: 1);
        AddPage(4, parentId: 2);
        AddFolder(5);                  // sibling root folder
        AddPage(6, parentId: 5);       // child of sibling
        AddPage(7, projectId: 99);     // other project

        var result = await DocumentService.DeleteDocumentNodeAsync(1, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status204NoContent, result.StatusCode);
        var nodes = await Db.DocumentNodes.ToDictionaryAsync(n => n.Id, TestContext.Current.CancellationToken);
        Assert.All(new long[] { 1, 2, 3, 4 }, id => Assert.True(nodes[id].IsDeleted));
        Assert.All(new long[] { 5, 6, 7 }, id => Assert.False(nodes[id].IsDeleted));
        var activities = await Db.DocumentActivities.OrderBy(a => a.DocumentId).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new long[] { 3, 4 }, activities.Select(a => a.DocumentId).ToArray());
        Assert.All(activities, a => Assert.Equal(DocumentActivityActions.Deleted, a.Action));
    }

    // UTC04: Delete a PAGE that has child pages -> page and its children are deleted
    [Fact]
    public async Task DeleteDocumentNodeAsync_WithPageHavingChildPages_DeletesChildrenToo()
    {
        SetupAuthorizedUser();
        AddPage(1);
        AddPage(2, parentId: 1);
        AddPage(3, parentId: 2);

        var result = await DocumentService.DeleteDocumentNodeAsync(1, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.All(await Db.DocumentNodes.ToListAsync(TestContext.Current.CancellationToken), n => Assert.True(n.IsDeleted));
        Assert.Equal(3, await Db.DocumentActivities.CountAsync(TestContext.Current.CancellationToken));
    }

    // UTC05: Already soft-deleted descendants are ignored (not re-audited) while active ones are deleted
    [Fact]
    public async Task DeleteDocumentNodeAsync_WithAlreadyDeletedDescendant_DoesNotAuditItAgain()
    {
        SetupAuthorizedUser();
        AddFolder(1);
        AddPage(2, parentId: 1);
        AddPage(3, parentId: 1, isDeleted: true);

        var result = await DocumentService.DeleteDocumentNodeAsync(1, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var activity = await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, activity.DocumentId);
    }

    // UTC06: Baselined document in a different subtree does not block deleting this subtree
    [Fact]
    public async Task DeleteDocumentNodeAsync_WithBaselinedNodeOutsideSubtree_StillDeletes()
    {
        SetupAuthorizedUser();
        AddFolder(1);
        AddPage(2, parentId: 1);
        var other = AddPage(3);
        await SetBaselineAsync(other, 555);

        var result = await DocumentService.DeleteDocumentNodeAsync(1, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True((await Db.DocumentNodes.SingleAsync(n => n.Id == 2, TestContext.Current.CancellationToken)).IsDeleted);
        Assert.False((await Db.DocumentNodes.SingleAsync(n => n.Id == 3, TestContext.Current.CancellationToken)).IsDeleted);
    }

    // UTC07: Page with a null current version -> activity VersionId is null
    [Fact]
    public async Task DeleteDocumentNodeAsync_WithPageWithoutCurrentVersion_RecordsActivityWithNullVersion()
    {
        SetupAuthorizedUser();
        AddPage(1);

        var result = await DocumentService.DeleteDocumentNodeAsync(1, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null((await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken)).VersionId);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC08: Document does not exist -> 404 DOCUMENT_NOT_FOUND, guard not consulted
    [Fact]
    public async Task DeleteDocumentNodeAsync_WithNonExistentDocument_ReturnsNotFound()
    {
        var result = await DocumentService.DeleteDocumentNodeAsync(999, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC09: Document is already soft-deleted -> 404 DOCUMENT_NOT_FOUND, no new activity
    [Fact]
    public async Task DeleteDocumentNodeAsync_WithAlreadyDeletedDocument_ReturnsNotFound()
    {
        SetupAuthorizedUser();
        AddPage(1, isDeleted: true);

        var result = await DocumentService.DeleteDocumentNodeAsync(1, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC10: Unauthenticated caller -> 401, nothing deleted
    [Fact]
    public async Task DeleteDocumentNodeAsync_WhenUnauthenticated_ReturnsUnauthorizedAndKeepsNode()
    {
        DocumentTestData.GuardUnauthenticated(AccessGuardMock, ProjectId, PermissionKeys.DocumentDelete);
        AddPage(1);

        var result = await DocumentService.DeleteDocumentNodeAsync(1, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.False((await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).IsDeleted);
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC11: Permission denied -> 403 FORBIDDEN with required permission, nothing deleted
    [Fact]
    public async Task DeleteDocumentNodeAsync_WhenPermissionDenied_ReturnsForbiddenAndKeepsNode()
    {
        DocumentTestData.GuardDeny(AccessGuardMock, ProjectId, PermissionKeys.DocumentDelete);
        AddPage(1);

        var result = await DocumentService.DeleteDocumentNodeAsync(1, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.DocumentDelete, result.RequiredPermission);
        Assert.False((await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).IsDeleted);
    }

    // UTC12: Deleting a baselined PAGE -> 409 BASELINED_DOCUMENT_CANNOT_DELETE, nothing changed
    [Fact]
    public async Task DeleteDocumentNodeAsync_WithBaselinedPage_ReturnsConflict()
    {
        SetupAuthorizedUser();
        var page = AddPage(1);
        await SetBaselineAsync(page, 1000);

        var result = await DocumentService.DeleteDocumentNodeAsync(1, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("BASELINED_DOCUMENT_CANNOT_DELETE", result.ErrorCode);
        Assert.False((await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).IsDeleted);
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC13: Deleting a folder whose descendant page is baselined -> 409, whole subtree kept
    [Fact]
    public async Task DeleteDocumentNodeAsync_WithBaselinedDescendant_ReturnsConflictAndKeepsSubtree()
    {
        SetupAuthorizedUser();
        AddFolder(1);
        AddPage(2, parentId: 1);
        var baselined = AddPage(3, parentId: 1);
        await SetBaselineAsync(baselined, 1000);

        var result = await DocumentService.DeleteDocumentNodeAsync(1, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("BASELINED_DOCUMENT_CANNOT_DELETE", result.ErrorCode);
        Assert.All(await Db.DocumentNodes.ToListAsync(TestContext.Current.CancellationToken), n => Assert.False(n.IsDeleted));
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC14: Deleting a deeply nested chain (4 levels) from the top -> every level deleted
    [Fact]
    public async Task DeleteDocumentNodeAsync_WithDeepFolderChain_DeletesEveryLevel()
    {
        SetupAuthorizedUser();
        AddFolder(1);
        AddFolder(2, parentId: 1);
        AddFolder(3, parentId: 2);
        AddPage(4, parentId: 3);

        var result = await DocumentService.DeleteDocumentNodeAsync(1, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.All(await Db.DocumentNodes.ToListAsync(TestContext.Current.CancellationToken), n => Assert.True(n.IsDeleted));
        Assert.Single(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC15: Deleting a leaf in the middle of a tree -> only the leaf is deleted, parent and siblings kept
    [Fact]
    public async Task DeleteDocumentNodeAsync_WithLeafNode_DeletesOnlyLeaf()
    {
        SetupAuthorizedUser();
        AddFolder(1);
        AddPage(2, parentId: 1);
        AddPage(3, parentId: 1);

        var result = await DocumentService.DeleteDocumentNodeAsync(3, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var nodes = await Db.DocumentNodes.ToDictionaryAsync(n => n.Id, TestContext.Current.CancellationToken);
        Assert.False(nodes[1].IsDeleted);
        Assert.False(nodes[2].IsDeleted);
        Assert.True(nodes[3].IsDeleted);
    }
}
