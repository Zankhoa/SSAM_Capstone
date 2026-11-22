using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Documents;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.DocumentServiceTests;

public class CreateDocumentNodeAsyncTests : DocumentServiceTestBase
{
    private const long ProjectId = 10;
    private const long UserId = 1;

    private void SetupAuthorizedUser(long projectId = ProjectId, long userId = UserId)
    {
        CurrentUserMock.Setup(u => u.UserId).Returns(userId);
        DocumentTestData.GuardAllow(AccessGuardMock, projectId, PermissionKeys.DocumentCreate);
    }

    private static CreateDocumentNodeRequest PageRequest(string docType = "SRS", string title = "Payment SRS", long? parentId = null) => new()
    {
        Kind = "PAGE",
        Title = title,
        DocType = docType,
        ParentId = parentId
    };

    private static CreateDocumentNodeRequest FolderRequest(string title = "Specs", long? parentId = null) => new()
    {
        Kind = "FOLDER",
        Title = title,
        ParentId = parentId
    };

    private async Task AssertNothingPersistedAsync()
    {
        Assert.Empty(await Db.DocumentNodes.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.DocumentVersions.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.RequirementItems.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Create a root FOLDER with a valid title -> 201 Created, folder persisted without version/activity
    [Fact]
    public async Task CreateDocumentNodeAsync_WithRootFolder_ReturnsCreatedAndPersistsFolder()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, FolderRequest("  Specs  "), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        var dto = Assert.IsType<DocumentNodeDto>(result.Data);
        Assert.True(dto.DocumentId > 0);
        Assert.Equal(ProjectId, dto.ProjectId);
        Assert.Null(dto.ParentId);
        Assert.Equal(DocumentNodeKinds.Folder, dto.Kind);
        Assert.Equal("Specs", dto.Title);
        Assert.Null(dto.DocType);
        Assert.Null(dto.Code);
        Assert.Equal(0, dto.SortOrder);
        Assert.Null(dto.CurrentVersion);
        Assert.False(dto.IsBaselined);
        Assert.Null(dto.BaselineVersionId);
        Assert.Equal(0, dto.ReqCount);
        Assert.Empty(dto.RequiredApprovers);

        var node = await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(dto.DocumentId, node.Id);
        Assert.Equal(DocumentNodeKinds.Folder, node.Kind);
        Assert.Equal("Specs", node.Title);
        Assert.Equal(UserId, node.CreatedById);
        Assert.Equal(now, node.CreatedAt);
        Assert.Equal(now, node.UpdatedAt);
        Assert.False(node.IsDeleted);
        Assert.Null(node.CurrentVersionId);
        Assert.Empty(await Db.DocumentVersions.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));

        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.DocumentCreate, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Create FOLDER with lowercase/padded kind and a docType value -> kind normalized, docType ignored (null)
    [Fact]
    public async Task CreateDocumentNodeAsync_WithLowercaseFolderKindAndDocType_NormalizesKindAndIgnoresDocType()
    {
        SetupAuthorizedUser();
        var request = new CreateDocumentNodeRequest { Kind = "  folder ", Title = "Archive", DocType = "INVALID_TYPE" };

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var dto = Assert.IsType<DocumentNodeDto>(result.Data);
        Assert.Equal(DocumentNodeKinds.Folder, dto.Kind);
        Assert.Null(dto.DocType);
        var node = await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Null(node.DocType);
        Assert.Null(node.Code);
    }

    // UTC03: Create FOLDER inside an existing folder with siblings -> SortOrder = max active sibling + 1 (deleted siblings ignored)
    [Fact]
    public async Task CreateDocumentNodeAsync_WithFolderUnderFolderWithSiblings_UsesNextSortOrderIgnoringDeleted()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        DocumentTestData.AddNode(Db, now, 1, ProjectId, DocumentNodeKinds.Folder, "Parent");
        DocumentTestData.AddNode(Db, now, 2, ProjectId, DocumentNodeKinds.Folder, "A", parentId: 1, sortOrder: 0);
        DocumentTestData.AddNode(Db, now, 3, ProjectId, DocumentNodeKinds.Folder, "B", parentId: 1, sortOrder: 4);
        DocumentTestData.AddNode(Db, now, 4, ProjectId, DocumentNodeKinds.Folder, "Deleted", parentId: 1, sortOrder: 9, isDeleted: true);

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, FolderRequest("C", parentId: 1), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var dto = Assert.IsType<DocumentNodeDto>(result.Data);
        Assert.Equal(1, dto.ParentId);
        Assert.Equal(5, dto.SortOrder);
        var created = await Db.DocumentNodes.SingleAsync(n => n.Title == "C", TestContext.Current.CancellationToken);
        Assert.Equal(1, created.ParentId);
        Assert.Equal(5, created.SortOrder);
    }

    // UTC04: Create SRS PAGE without content -> template content, REQ-001 allocated, code SRS-01, version 1.0 DRAFT, activity CREATED
    [Fact]
    public async Task CreateDocumentNodeAsync_WithSrsPageWithoutContent_CreatesTemplateDocumentWithVersionAndActivity()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        var request = PageRequest("SRS", "  Payment SRS  ");
        request.Subtitle = "  Phase 1  ";

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        var dto = Assert.IsType<DocumentNodeDto>(result.Data);
        Assert.Equal(DocumentNodeKinds.Page, dto.Kind);
        Assert.Equal("Payment SRS", dto.Title);
        Assert.Equal(DocumentTypes.Srs, dto.DocType);
        Assert.Equal("SRS-01", dto.Code);
        Assert.Equal(0, dto.SortOrder);
        Assert.Equal(1, dto.ReqCount);
        Assert.NotNull(dto.CurrentVersion);
        Assert.Equal("1.0", dto.CurrentVersion!.Label);
        Assert.Equal(DocumentVersionStatuses.Draft, dto.CurrentVersion.Status);
        Assert.False(dto.IsBaselined);

        var node = await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken);
        var version = await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(version.Id, node.CurrentVersionId);
        Assert.Equal(dto.CurrentVersion.VersionId, version.Id);
        Assert.Equal(node.Id, version.DocumentId);
        Assert.Equal("1.0", version.Label);
        Assert.Equal(DocumentVersionStatuses.Draft, version.Status);
        Assert.Equal("Phase 1", version.Subtitle);
        Assert.Equal(1, version.RowVersion);
        Assert.Equal(UserId, version.AuthorId);
        Assert.Equal(UserId, version.UpdatedById);
        Assert.Equal(now, version.CreatedAt);
        Assert.Contains("Payment SRS", version.ContentText);

        var req = await Db.RequirementItems.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("REQ-001", req.ReqUid);
        Assert.Equal(node.Id, req.DocumentId);
        Assert.Equal(version.Id, req.VersionId);
        Assert.Equal(0, req.SortOrder);
        Assert.False(string.IsNullOrWhiteSpace(req.ContentHash));

        var activity = await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DocumentActivityActions.Created, activity.Action);
        Assert.Equal(node.Id, activity.DocumentId);
        Assert.Equal(version.Id, activity.VersionId);
        Assert.Equal(UserId, activity.ActorId);
        Assert.Equal("Khởi tạo tài liệu SRS-01 v1.0", activity.Note);
        Assert.Equal(now, activity.CreatedAt);
    }

    // UTC05: Create PAGE with lowercase padded docType " brd " -> docType normalized to BRD, code BRD-01, sample REQ allocated
    [Fact]
    public async Task CreateDocumentNodeAsync_WithLowercasePaddedDocType_NormalizesDocTypeAndAllocatesBrdCode()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, PageRequest(" brd "), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var dto = Assert.IsType<DocumentNodeDto>(result.Data);
        Assert.Equal(DocumentTypes.Brd, dto.DocType);
        Assert.Equal("BRD-01", dto.Code);
        Assert.Equal(1, dto.ReqCount);
        Assert.Equal("BRD-01", (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).Code);
    }

    // UTC06: Create URD PAGE -> code URD-01 and sample REQ allocated (URD uses the generic requirement template)
    [Fact]
    public async Task CreateDocumentNodeAsync_WithUrdPage_AllocatesUrdCodeAndSampleRequirement()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, PageRequest("URD"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var dto = Assert.IsType<DocumentNodeDto>(result.Data);
        Assert.Equal("URD-01", dto.Code);
        Assert.Equal(1, dto.ReqCount);
        Assert.Equal("REQ-001", (await Db.RequirementItems.SingleAsync(TestContext.Current.CancellationToken)).ReqUid);
    }

    // UTC07-UTC10: Create PAGE for non-requirement doc types -> prefix by doc type, overview template without requirements
    [Theory]
    [InlineData("ADR", "ADR-01")] // UTC07
    [InlineData("UI_SPEC", "UI_SPEC-01")] // UTC08
    [InlineData("TEST_PLAN", "TEST_PLAN-01")] // UTC09
    [InlineData("OTHER", "DOC-01")] // UTC10: OTHER uses the DOC prefix
    public async Task CreateDocumentNodeAsync_WithNonRequirementDocType_CreatesOverviewTemplateWithoutRequirements(string docType, string expectedCode)
    {
        SetupAuthorizedUser();

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, PageRequest(docType, "Overview Doc"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        var dto = Assert.IsType<DocumentNodeDto>(result.Data);
        Assert.Equal(docType, dto.DocType);
        Assert.Equal(expectedCode, dto.Code);
        Assert.Equal(0, dto.ReqCount);
        Assert.Empty(await Db.RequirementItems.ToListAsync(TestContext.Current.CancellationToken));
        Assert.DoesNotContain(await Db.ProjectSequences.ToListAsync(TestContext.Current.CancellationToken), s => s.Scope == SequenceScopes.Req);
        var version = await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Overview Doc", version.ContentText);
    }

    // UTC11: Create a second SRS page in the same project -> sequence increments (SRS-02) and REQ-002 allocated
    [Fact]
    public async Task CreateDocumentNodeAsync_WithSecondSrsPage_IncrementsDocumentCodeAndReqSequence()
    {
        SetupAuthorizedUser();
        await DocumentService.CreateDocumentNodeAsync(ProjectId, PageRequest("SRS", "First"), TestContext.Current.CancellationToken);

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, PageRequest("SRS", "Second"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var dto = Assert.IsType<DocumentNodeDto>(result.Data);
        Assert.Equal("SRS-02", dto.Code);
        Assert.Equal(1, dto.SortOrder);
        var reqUids = await Db.RequirementItems.Select(r => r.ReqUid).OrderBy(x => x).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "REQ-001", "REQ-002" }, reqUids);
    }

    // UTC12: Create PAGE with custom JSON content containing two REQ headings -> requirements extracted, no sample REQ allocated
    [Fact]
    public async Task CreateDocumentNodeAsync_WithCustomContent_ExtractsRequirementsAndSkipsTemplate()
    {
        SetupAuthorizedUser();
        var request = PageRequest("SRS", "Custom SRS");
        request.Content = DocumentTestData.DocWithReqs(("REQ-010", "Login"), ("REQ-011", "Logout"));

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var dto = Assert.IsType<DocumentNodeDto>(result.Data);
        Assert.Equal(2, dto.ReqCount);
        var reqs = await Db.RequirementItems.OrderBy(r => r.SortOrder).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "REQ-010", "REQ-011" }, reqs.Select(r => r.ReqUid).ToArray());
        Assert.Equal(new[] { "Login", "Logout" }, reqs.Select(r => r.Title).ToArray());
        Assert.Equal(new[] { 0, 1 }, reqs.Select(r => r.SortOrder).ToArray());
        Assert.DoesNotContain(await Db.ProjectSequences.ToListAsync(TestContext.Current.CancellationToken), s => s.Scope == SequenceScopes.Req);
        var version = await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Body of REQ-010", version.ContentText);
    }

    // UTC13: Create PAGE with non-object content (array) -> falls back to the template content
    [Fact]
    public async Task CreateDocumentNodeAsync_WithNonObjectContent_FallsBackToTemplate()
    {
        SetupAuthorizedUser();
        var request = PageRequest("SRS", "Array Content");
        request.Content = JsonDocument.Parse("[1,2,3]").RootElement.Clone();

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var dto = Assert.IsType<DocumentNodeDto>(result.Data);
        Assert.Equal(1, dto.ReqCount);
        Assert.Equal("REQ-001", (await Db.RequirementItems.SingleAsync(TestContext.Current.CancellationToken)).ReqUid);
    }

    // UTC14: Create PAGE under a PAGE parent with a code -> child code is "<parentCode>.1", then ".2" for the next child
    [Fact]
    public async Task CreateDocumentNodeAsync_WithChildPageUnderPageParent_AllocatesHierarchicalCode()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        DocumentTestData.AddNode(Db, now, 1, ProjectId, DocumentNodeKinds.Page, "Parent SRS", DocumentTypes.Srs, "SRS-07");

        var first = await DocumentService.CreateDocumentNodeAsync(ProjectId, PageRequest("SRS", "Child 1", 1), TestContext.Current.CancellationToken);
        var second = await DocumentService.CreateDocumentNodeAsync(ProjectId, PageRequest("SRS", "Child 2", 1), TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("SRS-07.1", Assert.IsType<DocumentNodeDto>(first.Data).Code);
        var secondDto = Assert.IsType<DocumentNodeDto>(second.Data);
        Assert.Equal("SRS-07.2", secondDto.Code);
        Assert.Equal(1, secondDto.ParentId);
        Assert.Equal(1, secondDto.SortOrder);
    }

    // UTC15: Create PAGE under a FOLDER parent -> uses the project sequence code (not hierarchical)
    [Fact]
    public async Task CreateDocumentNodeAsync_WithPageUnderFolder_UsesSequenceCode()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        DocumentTestData.AddNode(Db, now, 1, ProjectId, DocumentNodeKinds.Folder, "Folder", code: "IGNORED-01");

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, PageRequest("ADR", "Decision", 1), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var dto = Assert.IsType<DocumentNodeDto>(result.Data);
        Assert.Equal("ADR-01", dto.Code);
        Assert.Equal(1, dto.ParentId);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC16: Unauthenticated caller -> guard failure is propagated (401), nothing persisted
    [Fact]
    public async Task CreateDocumentNodeAsync_WhenUnauthenticated_ReturnsUnauthorizedAndPersistsNothing()
    {
        DocumentTestData.GuardUnauthenticated(AccessGuardMock, ProjectId, PermissionKeys.DocumentCreate);

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, PageRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Null(result.Data);
        await AssertNothingPersistedAsync();
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.DocumentCreate, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC17: Caller lacks document create permission -> 403 FORBIDDEN with required permission, nothing persisted
    [Fact]
    public async Task CreateDocumentNodeAsync_WhenPermissionDenied_ReturnsForbiddenAndPersistsNothing()
    {
        DocumentTestData.GuardDeny(AccessGuardMock, ProjectId, PermissionKeys.DocumentCreate);

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, PageRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.DocumentCreate, result.RequiredPermission);
        await AssertNothingPersistedAsync();
        CurrentUserMock.VerifyGet(u => u.UserId, Times.Never);
    }

    // UTC18: Invalid kind "DOC" -> 400 VALIDATION_ERROR with kind error
    [Fact]
    public async Task CreateDocumentNodeAsync_WithInvalidKind_ReturnsKindValidationError()
    {
        SetupAuthorizedUser();
        var request = new CreateDocumentNodeRequest { Kind = "DOC", Title = "Title", DocType = "SRS" };

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.NotNull(result.Errors);
        Assert.Equal(new[] { "kind" }, result.Errors!.Keys.ToArray());
        Assert.Equal("Kind must be FOLDER or PAGE.", result.Errors["kind"][0]);
        await AssertNothingPersistedAsync();
    }

    // UTC19: Empty kind -> 400 kind validation error
    [Fact]
    public async Task CreateDocumentNodeAsync_WithEmptyKind_ReturnsKindValidationError()
    {
        SetupAuthorizedUser();
        var request = new CreateDocumentNodeRequest { Kind = "", Title = "Title" };

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.True(result.Errors!.ContainsKey("kind"));
        Assert.False(result.Errors.ContainsKey("title"));
        await AssertNothingPersistedAsync();
    }

    // UTC20: Empty title -> 400 title validation error
    [Fact]
    public async Task CreateDocumentNodeAsync_WithEmptyTitle_ReturnsTitleValidationError()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, PageRequest(title: ""), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal(new[] { "title" }, result.Errors!.Keys.ToArray());
        Assert.Equal("Title is required and must be at most 150 characters.", result.Errors["title"][0]);
        await AssertNothingPersistedAsync();
    }

    // UTC21: Whitespace-only title -> 400 title validation error
    [Fact]
    public async Task CreateDocumentNodeAsync_WithWhitespaceTitle_ReturnsTitleValidationError()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, FolderRequest("     "), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.True(result.Errors!.ContainsKey("title"));
        await AssertNothingPersistedAsync();
    }

    // UTC22: PAGE without docType -> 400 docType validation error
    [Fact]
    public async Task CreateDocumentNodeAsync_WithPageMissingDocType_ReturnsDocTypeValidationError()
    {
        SetupAuthorizedUser();
        var request = new CreateDocumentNodeRequest { Kind = "PAGE", Title = "Title", DocType = null };

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal(new[] { "docType" }, result.Errors!.Keys.ToArray());
        await AssertNothingPersistedAsync();
    }

    // UTC23: PAGE with an unknown docType -> 400 docType validation error
    [Fact]
    public async Task CreateDocumentNodeAsync_WithPageInvalidDocType_ReturnsDocTypeValidationError()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, PageRequest("XYZ"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal(new[] { "docType" }, result.Errors!.Keys.ToArray());
        Assert.StartsWith("Valid docType", result.Errors["docType"][0]);
        await AssertNothingPersistedAsync();
    }

    // UTC24: Subtitle longer than 300 characters -> 400 subtitle validation error
    [Fact]
    public async Task CreateDocumentNodeAsync_WithTooLongSubtitle_ReturnsSubtitleValidationError()
    {
        SetupAuthorizedUser();
        var request = PageRequest();
        request.Subtitle = new string('s', 301);

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal(new[] { "subtitle" }, result.Errors!.Keys.ToArray());
        Assert.Equal("Subtitle must be at most 300 characters.", result.Errors["subtitle"][0]);
        await AssertNothingPersistedAsync();
    }

    // UTC25: Invalid kind, empty title and long subtitle together -> all errors reported at once
    [Fact]
    public async Task CreateDocumentNodeAsync_WithMultipleInvalidFields_ReturnsAllValidationErrors()
    {
        SetupAuthorizedUser();
        var request = new CreateDocumentNodeRequest { Kind = "BAD", Title = " ", Subtitle = new string('s', 400) };

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal(new[] { "kind", "subtitle", "title" }, result.Errors!.Keys.OrderBy(k => k).ToArray());
        await AssertNothingPersistedAsync();
    }

    // UTC26: Parent id does not exist -> 400 INVALID_PARENT
    [Fact]
    public async Task CreateDocumentNodeAsync_WithNonExistentParent_ReturnsInvalidParent()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, PageRequest(parentId: 999), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("INVALID_PARENT", result.ErrorCode);
        await AssertNothingPersistedAsync();
    }

    // UTC27: Parent belongs to another project -> 400 INVALID_PARENT
    [Fact]
    public async Task CreateDocumentNodeAsync_WithParentInAnotherProject_ReturnsInvalidParent()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        DocumentTestData.AddNode(Db, now, 1, projectId: 99, kind: DocumentNodeKinds.Folder, title: "Other project folder");

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, PageRequest(parentId: 1), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_PARENT", result.ErrorCode);
        Assert.Single(await Db.DocumentNodes.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC28: Parent folder is soft-deleted -> 400 INVALID_PARENT
    [Fact]
    public async Task CreateDocumentNodeAsync_WithDeletedParent_ReturnsInvalidParent()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        DocumentTestData.AddNode(Db, now, 1, ProjectId, DocumentNodeKinds.Folder, "Deleted folder", isDeleted: true);

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, FolderRequest(parentId: 1), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("INVALID_PARENT", result.ErrorCode);
        Assert.Single(await Db.DocumentNodes.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC29: Creating a FOLDER under a PAGE parent -> 400 INVALID_PARENT (folders must live in folders)
    [Fact]
    public async Task CreateDocumentNodeAsync_WithFolderUnderPageParent_ReturnsInvalidParent()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        DocumentTestData.AddNode(Db, now, 1, ProjectId, DocumentNodeKinds.Page, "Page parent", DocumentTypes.Srs, "SRS-01");

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, FolderRequest(parentId: 1), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("INVALID_PARENT", result.ErrorCode);
        Assert.Single(await Db.DocumentNodes.ToListAsync(TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC30: Title of exactly 1 character (min) -> created
    [Fact]
    public async Task CreateDocumentNodeAsync_WithTitleOfOneCharacter_ReturnsCreated()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, FolderRequest("A"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("A", Assert.IsType<DocumentNodeDto>(result.Data).Title);
    }

    // UTC31: Title of exactly 150 characters (max) -> created and stored unchanged
    [Fact]
    public async Task CreateDocumentNodeAsync_WithTitleOf150Characters_ReturnsCreated()
    {
        SetupAuthorizedUser();
        var title = new string('T', 150);

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, PageRequest("ADR", title), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(title, Assert.IsType<DocumentNodeDto>(result.Data).Title);
        Assert.Equal(title, (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).Title);
    }

    // UTC32: Title of 151 characters (max+1) -> 400 title validation error
    [Fact]
    public async Task CreateDocumentNodeAsync_WithTitleOf151Characters_ReturnsTitleValidationError()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, FolderRequest(new string('T', 151)), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.True(result.Errors!.ContainsKey("title"));
        await AssertNothingPersistedAsync();
    }

    // UTC33: Title of 150 characters surrounded by spaces (trimmed length = 150) -> created
    [Fact]
    public async Task CreateDocumentNodeAsync_WithPaddedTitleTrimmedTo150Characters_ReturnsCreated()
    {
        SetupAuthorizedUser();
        var core = new string('T', 150);

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, FolderRequest($"   {core}   "), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(core, Assert.IsType<DocumentNodeDto>(result.Data).Title);
    }

    // UTC34: Subtitle of exactly 300 characters (max) -> created and stored
    [Fact]
    public async Task CreateDocumentNodeAsync_WithSubtitleOf300Characters_ReturnsCreated()
    {
        SetupAuthorizedUser();
        var request = PageRequest("ADR", "Sub max");
        request.Subtitle = new string('s', 300);

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new string('s', 300), (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Subtitle);
    }

    // UTC35: Subtitle of 301 characters (max+1) on a FOLDER -> 400 subtitle validation error (checked for every kind)
    [Fact]
    public async Task CreateDocumentNodeAsync_WithFolderSubtitleOf301Characters_ReturnsSubtitleValidationError()
    {
        SetupAuthorizedUser();
        var request = FolderRequest();
        request.Subtitle = new string('s', 301);

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(new[] { "subtitle" }, result.Errors!.Keys.ToArray());
        await AssertNothingPersistedAsync();
    }

    // UTC36: Null subtitle on a PAGE -> created with null subtitle
    [Fact]
    public async Task CreateDocumentNodeAsync_WithNullSubtitle_StoresNullSubtitle()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.CreateDocumentNodeAsync(ProjectId, PageRequest("ADR"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null((await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Subtitle);
    }
}
