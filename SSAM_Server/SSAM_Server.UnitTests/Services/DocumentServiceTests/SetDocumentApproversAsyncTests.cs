using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Documents;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.DocumentServiceTests;

public class SetDocumentApproversAsyncTests : DocumentServiceTestBase
{
    private const long ProjectId = 10;
    private const long DocumentId = 100;

    private void SetupAuthorizedUser()
    {
        CurrentUserMock.Setup(u => u.UserId).Returns(1);
        DocumentTestData.GuardAllow(AccessGuardMock, ProjectId, PermissionKeys.DocumentUpdate);
    }

    /// <summary>Seeds a page (with version) plus users 3, 4, 5 that are members of the project.</summary>
    private DocumentNode SetupPageWithMembers(string requiredApprovers = "[]")
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var (node, _) = DocumentTestData.AddPageWithVersion(Db, now, DocumentId, 1000, ProjectId, requiredApprovers: requiredApprovers);
        foreach (var id in new long[] { 2, 3, 4, 5 })
        {
            if (!Db.Users.Any(u => u.Id == id)) DocumentTestData.AddUser(Db, id);
            DocumentTestData.AddProjectMember(Db, ProjectId, id);
        }
        return node;
    }

    private async Task<List<long>> StoredApproversAsync()
    {
        var node = await Db.DocumentNodes.AsNoTracking().SingleAsync(n => n.Id == DocumentId, TestContext.Current.CancellationToken);
        return JsonSerializer.Deserialize<List<long>>(node.RequiredApprovers)!;
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Set two valid project members as approvers via ApproverIds -> 200, ids persisted in order, DTO lists user refs
    [Fact]
    public async Task SetDocumentApproversAsync_WithValidApproverIds_PersistsApproversAndReturnsUserRefs()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        SetupPageWithMembers();

        var result = await DocumentService.SetDocumentApproversAsync(DocumentId, new UpdateDocumentApproversRequest { ApproverIds = new List<long> { 3, 2 } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(DocumentId, result.Data.DocumentId);
        Assert.Equal(ProjectId, result.Data.ProjectId);
        Assert.Equal(new long[] { 3, 2 }, result.Data.RequiredApprovers.Select(a => a.UserId).ToArray());
        Assert.Equal("User 3", result.Data.RequiredApprovers[0].FullName);
        Assert.Equal("user3@ssam.vn", result.Data.RequiredApprovers[0].Email);
        Assert.Equal(new long[] { 3, 2 }, await StoredApproversAsync());
        Assert.Equal(now, (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).UpdatedAt);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.DocumentUpdate, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: ApproverIds null but UserIds provided -> UserIds used as fallback
    [Fact]
    public async Task SetDocumentApproversAsync_WithOnlyUserIds_UsesUserIdsFallback()
    {
        SetupAuthorizedUser();
        SetupPageWithMembers();

        var result = await DocumentService.SetDocumentApproversAsync(DocumentId, new UpdateDocumentApproversRequest { UserIds = new List<long> { 4 } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new long[] { 4 }, result.Data!.RequiredApprovers.Select(a => a.UserId).ToArray());
        Assert.Equal(new long[] { 4 }, await StoredApproversAsync());
    }

    // UTC03: Both ApproverIds and UserIds provided -> ApproverIds wins
    [Fact]
    public async Task SetDocumentApproversAsync_WithBothLists_PrefersApproverIds()
    {
        SetupAuthorizedUser();
        SetupPageWithMembers();

        var result = await DocumentService.SetDocumentApproversAsync(DocumentId, new UpdateDocumentApproversRequest { ApproverIds = new List<long> { 2 }, UserIds = new List<long> { 3 } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new long[] { 2 }, await StoredApproversAsync());
    }

    // UTC04: Empty ApproverIds on a document with approvers -> approvers cleared ("[]")
    [Fact]
    public async Task SetDocumentApproversAsync_WithEmptyList_ClearsApprovers()
    {
        SetupAuthorizedUser();
        SetupPageWithMembers("[2,3]");

        var result = await DocumentService.SetDocumentApproversAsync(DocumentId, new UpdateDocumentApproversRequest { ApproverIds = new List<long>() }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.RequiredApprovers);
        Assert.Equal("[]", (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).RequiredApprovers);
    }

    // UTC05: Both lists null -> treated as empty, approvers cleared
    [Fact]
    public async Task SetDocumentApproversAsync_WithBothListsNull_ClearsApprovers()
    {
        SetupAuthorizedUser();
        SetupPageWithMembers("[2]");

        var result = await DocumentService.SetDocumentApproversAsync(DocumentId, new UpdateDocumentApproversRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.RequiredApprovers);
        Assert.Equal("[]", (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).RequiredApprovers);
    }

    // UTC06: Duplicates and non-positive ids are dropped before validation -> only distinct positive ids stored
    [Fact]
    public async Task SetDocumentApproversAsync_WithDuplicatesAndNonPositiveIds_NormalizesList()
    {
        SetupAuthorizedUser();
        SetupPageWithMembers();

        var result = await DocumentService.SetDocumentApproversAsync(DocumentId, new UpdateDocumentApproversRequest { ApproverIds = new List<long> { 0, -5, 2, 2, 3, 2 } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new long[] { 2, 3 }, await StoredApproversAsync());
        Assert.Equal(new long[] { 2, 3 }, result.Data!.RequiredApprovers.Select(a => a.UserId).ToArray());
    }

    // UTC07: Only non-positive ids supplied -> normalized to an empty list without member lookup errors
    [Fact]
    public async Task SetDocumentApproversAsync_WithOnlyNonPositiveIds_StoresEmptyList()
    {
        SetupAuthorizedUser();
        SetupPageWithMembers("[2]");

        var result = await DocumentService.SetDocumentApproversAsync(DocumentId, new UpdateDocumentApproversRequest { ApproverIds = new List<long> { 0, -1 } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("[]", (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).RequiredApprovers);
    }

    // UTC08: Page with a current version and requirements -> DTO has req count and version ref
    [Fact]
    public async Task SetDocumentApproversAsync_WithPageHavingRequirements_ReturnsReqCountAndVersion()
    {
        SetupAuthorizedUser();
        SetupPageWithMembers();
        DocumentTestData.AddRequirement(Db, DocumentId, 1000, "REQ-001");
        DocumentTestData.AddRequirement(Db, DocumentId, 1000, "REQ-002", sortOrder: 1);

        var result = await DocumentService.SetDocumentApproversAsync(DocumentId, new UpdateDocumentApproversRequest { ApproverIds = new List<long> { 2 } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.ReqCount);
        Assert.Equal(1000, result.Data.CurrentVersion!.VersionId);
        Assert.Equal("SRS-01", result.Data.Code);
    }

    // UTC09: Inactive project member is still accepted (no IsActive filter is applied)
    [Fact]
    public async Task SetDocumentApproversAsync_WithInactiveMember_StillAccepted()
    {
        SetupAuthorizedUser();
        SetupPageWithMembers();
        var member = await Db.ProjectMembers.SingleAsync(m => m.UserId == 5, TestContext.Current.CancellationToken);
        member.IsActive = false;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DocumentService.SetDocumentApproversAsync(DocumentId, new UpdateDocumentApproversRequest { ApproverIds = new List<long> { 5 } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new long[] { 5 }, await StoredApproversAsync());
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC10: Document does not exist -> 404 DOCUMENT_NOT_FOUND, guard not consulted
    [Fact]
    public async Task SetDocumentApproversAsync_WithNonExistentDocument_ReturnsNotFound()
    {
        var result = await DocumentService.SetDocumentApproversAsync(999, new UpdateDocumentApproversRequest { ApproverIds = new List<long> { 2 } }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC11: Document is soft-deleted -> 404 DOCUMENT_NOT_FOUND
    [Fact]
    public async Task SetDocumentApproversAsync_WithDeletedDocument_ReturnsNotFound()
    {
        SetupAuthorizedUser();
        var node = SetupPageWithMembers();
        node.IsDeleted = true;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DocumentService.SetDocumentApproversAsync(DocumentId, new UpdateDocumentApproversRequest { ApproverIds = new List<long> { 2 } }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
    }

    // UTC12: Unauthenticated caller -> 401, approvers unchanged
    [Fact]
    public async Task SetDocumentApproversAsync_WhenUnauthenticated_ReturnsUnauthorizedAndKeepsApprovers()
    {
        DocumentTestData.GuardUnauthenticated(AccessGuardMock, ProjectId, PermissionKeys.DocumentUpdate);
        SetupPageWithMembers("[2]");

        var result = await DocumentService.SetDocumentApproversAsync(DocumentId, new UpdateDocumentApproversRequest { ApproverIds = new List<long> { 3 } }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal(new long[] { 2 }, await StoredApproversAsync());
    }

    // UTC13: Permission denied -> 403 FORBIDDEN with required permission, approvers unchanged
    [Fact]
    public async Task SetDocumentApproversAsync_WhenPermissionDenied_ReturnsForbiddenAndKeepsApprovers()
    {
        DocumentTestData.GuardDeny(AccessGuardMock, ProjectId, PermissionKeys.DocumentUpdate);
        SetupPageWithMembers("[2]");

        var result = await DocumentService.SetDocumentApproversAsync(DocumentId, new UpdateDocumentApproversRequest { ApproverIds = new List<long> { 3 } }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.DocumentUpdate, result.RequiredPermission);
        Assert.Equal(new long[] { 2 }, await StoredApproversAsync());
    }

    // UTC14: Target is a FOLDER -> 400 INVALID_DOCUMENT_KIND
    [Fact]
    public async Task SetDocumentApproversAsync_WithFolder_ReturnsInvalidDocumentKind()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        DocumentTestData.AddNode(Db, now, 1, ProjectId, DocumentNodeKinds.Folder, "Folder");

        var result = await DocumentService.SetDocumentApproversAsync(1, new UpdateDocumentApproversRequest { ApproverIds = new List<long> { 2 } }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("INVALID_DOCUMENT_KIND", result.ErrorCode);
        Assert.Equal("[]", (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).RequiredApprovers);
    }

    // UTC15: Approver is not a member of the project -> 400 VALIDATION_ERROR listing the invalid ids, nothing saved
    [Fact]
    public async Task SetDocumentApproversAsync_WithNonMemberApprover_ReturnsValidationError()
    {
        SetupAuthorizedUser();
        SetupPageWithMembers("[2]");

        var result = await DocumentService.SetDocumentApproversAsync(DocumentId, new UpdateDocumentApproversRequest { ApproverIds = new List<long> { 777 } }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal("Người dùng không thuộc dự án: 777", result.Errors!["approverIds"][0]);
        Assert.Equal(new long[] { 2 }, await StoredApproversAsync());
    }

    // UTC16: Mix of valid and invalid approvers -> all rejected, only invalid ids listed, nothing saved
    [Fact]
    public async Task SetDocumentApproversAsync_WithMixOfValidAndInvalidApprovers_RejectsWholeRequest()
    {
        SetupAuthorizedUser();
        SetupPageWithMembers("[]");

        var result = await DocumentService.SetDocumentApproversAsync(DocumentId, new UpdateDocumentApproversRequest { ApproverIds = new List<long> { 2, 777, 888 } }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal("Người dùng không thuộc dự án: 777, 888", result.Errors!["approverIds"][0]);
        Assert.Equal("[]", (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).RequiredApprovers);
    }

    // UTC17: Approver is a member of a different project only -> rejected as not in this project
    [Fact]
    public async Task SetDocumentApproversAsync_WithMemberOfAnotherProject_ReturnsValidationError()
    {
        SetupAuthorizedUser();
        SetupPageWithMembers();
        DocumentTestData.AddUser(Db, 50);
        DocumentTestData.AddProjectMember(Db, 99, 50);

        var result = await DocumentService.SetDocumentApproversAsync(DocumentId, new UpdateDocumentApproversRequest { ApproverIds = new List<long> { 50 } }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal("Người dùng không thuộc dự án: 50", result.Errors!["approverIds"][0]);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC18: Exactly one approver (min non-empty) -> saved
    [Fact]
    public async Task SetDocumentApproversAsync_WithSingleApprover_Saves()
    {
        SetupAuthorizedUser();
        SetupPageWithMembers();

        var result = await DocumentService.SetDocumentApproversAsync(DocumentId, new UpdateDocumentApproversRequest { ApproverIds = new List<long> { 2 } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.RequiredApprovers);
        Assert.Equal("[2]", (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).RequiredApprovers);
    }

    // UTC19: Smallest positive id (1) is treated as a regular id (member required)
    [Fact]
    public async Task SetDocumentApproversAsync_WithIdOne_RequiresMembership()
    {
        SetupAuthorizedUser();
        SetupPageWithMembers();

        var result = await DocumentService.SetDocumentApproversAsync(DocumentId, new UpdateDocumentApproversRequest { ApproverIds = new List<long> { 1 } }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("Người dùng không thuộc dự án: 1", result.Errors!["approverIds"][0]);
    }

    // UTC20: Many approvers (50 members) -> all saved
    [Fact]
    public async Task SetDocumentApproversAsync_WithFiftyApprovers_SavesAll()
    {
        SetupAuthorizedUser();
        SetupPageWithMembers();
        var ids = new List<long>();
        for (long id = 100; id < 150; id++)
        {
            DocumentTestData.AddUser(Db, id);
            DocumentTestData.AddProjectMember(Db, ProjectId, id);
            ids.Add(id);
        }

        var result = await DocumentService.SetDocumentApproversAsync(DocumentId, new UpdateDocumentApproversRequest { ApproverIds = ids }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(50, result.Data!.RequiredApprovers.Count);
        Assert.Equal(ids, await StoredApproversAsync());
    }

    // UTC21: Member without a user row -> id persisted but missing from the DTO approver refs
    [Fact]
    public async Task SetDocumentApproversAsync_WithMemberWithoutUserRecord_PersistsIdButOmitsRef()
    {
        SetupAuthorizedUser();
        SetupPageWithMembers();
        DocumentTestData.AddProjectMember(Db, ProjectId, 888);

        var result = await DocumentService.SetDocumentApproversAsync(DocumentId, new UpdateDocumentApproversRequest { ApproverIds = new List<long> { 2, 888 } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new long[] { 2, 888 }, await StoredApproversAsync());
        Assert.Equal(new long[] { 2 }, result.Data!.RequiredApprovers.Select(a => a.UserId).ToArray());
    }
}
