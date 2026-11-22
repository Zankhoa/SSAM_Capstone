using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Documents;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.DocumentServiceTests;

public class GenerateDocumentAsyncTests : DocumentServiceTestBase
{
    private const long ProjectId = 10;
    private const long UserId = 1;

    private void SetupAuthorizedUser()
    {
        CurrentUserMock.Setup(u => u.UserId).Returns(UserId);
        DocumentTestData.GuardAllow(AccessGuardMock, ProjectId, PermissionKeys.DocumentCreate);
        DocumentTestData.GuardAllow(AccessGuardMock, ProjectId, PermissionKeys.DocumentView);
        PermissionServiceMock
            .Setup(p => p.GetProjectPermissionsAsync(UserId, ProjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { PermissionKeys.DocumentEdit, PermissionKeys.DocumentDelete });
        DocumentTestData.AddUser(Db, UserId, "Author One");
    }

    private static ExtractedRequirementDto Item(string title, string description = "Description", string priority = "MEDIUM", string reviewStatus = ExtractedRequirementDto.ReviewAccepted) => new()
    {
        TempId = $"tmp-{title}",
        Title = title,
        Description = description,
        Priority = priority,
        AcceptanceCriteria = new List<string> { $"{title} works" },
        SourceLines = new List<int> { 1, 2 },
        Confidence = 0.8,
        IsAiGenerated = true,
        ReviewStatus = reviewStatus
    };

    private static GenerateDocumentRequest ValidRequest(params ExtractedRequirementDto[] items) => new()
    {
        Title = "Auth SRS",
        DocType = "SRS",
        RawNotes = "raw meeting notes",
        Fields = new NormalizedFieldsDto { Scope = "Auth module", Actors = "User, Admin", MainFlows = "• Login", BusinessRules = "• Lock after 5 tries" },
        Items = items.Length == 0 ? new List<ExtractedRequirementDto> { Item("Login") } : items.ToList()
    };

    private async Task AssertNothingPersistedAsync()
    {
        Assert.Empty(await Db.DocumentNodes.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.DocumentVersions.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.RequirementItems.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.DocumentGenerations.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Generate a document from two accepted items -> 201, SRS-01 page with v1.0 DRAFT, REQ-001/REQ-002 allocated, generation + activity persisted
    [Fact]
    public async Task GenerateDocumentAsync_WithAcceptedItems_CreatesDocumentRequirementsGenerationAndActivity()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        var request = ValidRequest(Item("Login", "  User logs in  ", "HIGH"), Item("Logout", "User logs out", "LOW"));
        request.Title = "  Auth SRS  ";
        request.Subtitle = "  Generated draft  ";

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(ProjectId, result.Data.Node.ProjectId);
        Assert.Equal(DocumentNodeKinds.Page, result.Data.Node.Kind);
        Assert.Equal("Auth SRS", result.Data.Node.Title);
        Assert.Equal(DocumentTypes.Srs, result.Data.Node.DocType);
        Assert.Equal("SRS-01", result.Data.Node.Code);
        Assert.Equal(0, result.Data.Node.SortOrder);
        Assert.Equal(2, result.Data.Node.ReqCount);
        Assert.Equal("1.0", result.Data.Current.Label);
        Assert.Equal(DocumentVersionStatuses.Draft, result.Data.Current.Status);
        Assert.Equal("Generated draft", result.Data.Current.Subtitle);
        Assert.Equal(1, result.Data.Current.RowVersion);
        Assert.Equal(UserId, result.Data.Current.Author.UserId);
        Assert.Single(result.Data.Versions);
        Assert.True(result.Data.Can.Edit);
        Assert.True(result.Data.Can.Delete);

        var node = await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken);
        var version = await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(version.Id, node.CurrentVersionId);
        Assert.Equal(UserId, node.CreatedById);
        Assert.Equal(now, node.CreatedAt);
        Assert.Equal("Generated draft", version.Subtitle);
        Assert.Contains("Auth module", version.ContentText);
        Assert.Contains("User logs in", version.ContentText);
        Assert.Contains("REQ-001", version.ContentText);

        var reqs = await Db.RequirementItems.OrderBy(r => r.SortOrder).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "REQ-001", "REQ-002" }, reqs.Select(r => r.ReqUid).ToArray());
        Assert.Equal(new[] { "Login", "Logout" }, reqs.Select(r => r.Title).ToArray());
        Assert.All(reqs, r =>
        {
            Assert.Equal(node.Id, r.DocumentId);
            Assert.Equal(version.Id, r.VersionId);
        });
        Assert.Contains("Login works", reqs[0].AcceptanceCriteria);

        var generation = await Db.DocumentGenerations.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ProjectId, generation.ProjectId);
        Assert.Equal(node.Id, generation.DocumentId);
        Assert.Equal("raw meeting notes", generation.RawNotes);
        Assert.Equal(UserId, generation.CreatedById);
        Assert.Equal(now, generation.CreatedAt);
        using (var fields = JsonDocument.Parse(generation.Fields))
        {
            Assert.Equal("Auth module", fields.RootElement.GetProperty("scope").GetString());
            Assert.Equal("User, Admin", fields.RootElement.GetProperty("actors").GetString());
        }
        using (var items = JsonDocument.Parse(generation.Items))
        {
            var array = items.RootElement.EnumerateArray().ToList();
            Assert.Equal(2, array.Count);
            Assert.Equal("REQ-001", array[0].GetProperty("reqUid").GetString());
            Assert.Equal("Login", array[0].GetProperty("title").GetString());
            Assert.Equal("User logs in", array[0].GetProperty("description").GetString());
            Assert.Equal("HIGH", array[0].GetProperty("priority").GetString());
            Assert.True(array[0].GetProperty("isAiGenerated").GetBoolean());
            Assert.Equal(ExtractedRequirementDto.ReviewAccepted, array[0].GetProperty("reviewStatus").GetString());
            Assert.Equal("REQ-002", array[1].GetProperty("reqUid").GetString());
        }

        var activity = await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DocumentActivityActions.GeneratedFromNotes, activity.Action);
        Assert.Equal(node.Id, activity.DocumentId);
        Assert.Equal(version.Id, activity.VersionId);
        Assert.Equal(UserId, activity.ActorId);
        Assert.Equal("Tạo từ ghi chú cuộc họp (2 yêu cầu)", activity.Note);
        Assert.Equal(now, activity.CreatedAt);

        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.DocumentCreate, It.IsAny<CancellationToken>()), Times.Once);
        AiServiceMock.VerifyNoOtherCalls();
    }

    // UTC02: Rejected items are excluded (case-insensitive) -> only accepted items generate requirements
    [Fact]
    public async Task GenerateDocumentAsync_WithRejectedItems_ExcludesThemFromDocument()
    {
        SetupAuthorizedUser();
        var request = ValidRequest(
            Item("Login"),
            Item("Skipped", reviewStatus: "rejected"),
            Item("Pending one", reviewStatus: ExtractedRequirementDto.ReviewPending),
            Item("Gone", reviewStatus: ExtractedRequirementDto.ReviewRejected));

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Node.ReqCount);
        var titles = await Db.RequirementItems.OrderBy(r => r.SortOrder).Select(r => r.Title).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "Login", "Pending one" }, titles);
        Assert.Equal("Tạo từ ghi chú cuộc họp (2 yêu cầu)", (await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken)).Note);
    }

    // UTC03: Lowercase padded docType -> normalized (brd -> BRD, code BRD-01)
    [Fact]
    public async Task GenerateDocumentAsync_WithLowercaseDocType_NormalizesDocType()
    {
        SetupAuthorizedUser();
        var request = ValidRequest();
        request.DocType = " brd ";

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentTypes.Brd, result.Data!.Node.DocType);
        Assert.Equal("BRD-01", result.Data.Node.Code);
    }

    // UTC04: OTHER docType -> code uses the DOC prefix
    [Fact]
    public async Task GenerateDocumentAsync_WithOtherDocType_UsesDocPrefix()
    {
        SetupAuthorizedUser();
        var request = ValidRequest();
        request.DocType = "OTHER";

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("DOC-01", result.Data!.Node.Code);
    }

    // UTC05: Second generated document continues the REQ and document code sequences
    [Fact]
    public async Task GenerateDocumentAsync_WhenCalledTwice_ContinuesSequences()
    {
        SetupAuthorizedUser();
        await DocumentService.GenerateDocumentAsync(ProjectId, ValidRequest(Item("A"), Item("B")), TestContext.Current.CancellationToken);

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, ValidRequest(Item("C")), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("SRS-02", result.Data!.Node.Code);
        Assert.Equal(1, result.Data.Node.SortOrder);
        var secondDocReqs = await Db.RequirementItems.Where(r => r.DocumentId == result.Data.Node.DocumentId).Select(r => r.ReqUid).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "REQ-003" }, secondDocReqs.ToArray());
    }

    // UTC06: Generate under an existing folder -> parent set, SortOrder after siblings
    [Fact]
    public async Task GenerateDocumentAsync_WithFolderParent_SetsParentAndSortOrder()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        DocumentTestData.AddNode(Db, now, 1, ProjectId, DocumentNodeKinds.Folder, "Folder");
        DocumentTestData.AddNode(Db, now, 2, ProjectId, DocumentNodeKinds.Folder, "Sibling", parentId: 1, sortOrder: 3);
        var request = ValidRequest();
        request.ParentId = 1;

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.Node.ParentId);
        Assert.Equal(4, result.Data.Node.SortOrder);
        Assert.Equal("SRS-01", result.Data.Node.Code);
    }

    // UTC07: Generate under a PAGE parent with a code -> hierarchical child code
    [Fact]
    public async Task GenerateDocumentAsync_WithPageParent_UsesHierarchicalCode()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        DocumentTestData.AddNode(Db, now, 1, ProjectId, DocumentNodeKinds.Page, "Parent", DocumentTypes.Srs, "SRS-05");
        var request = ValidRequest();
        request.ParentId = 1;

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("SRS-05.1", result.Data!.Node.Code);
        Assert.Equal(1, result.Data.Node.ParentId);
    }

    // UTC08: Item with a diagram -> requirement flagged as having a diagram, diagram stored in generation items
    [Fact]
    public async Task GenerateDocumentAsync_WithItemDiagram_FlagsRequirementHasDiagram()
    {
        SetupAuthorizedUser();
        var item = Item("Flow");
        item.Diagram = new DiagramProposalDto { Mermaid = "graph LR; Start to End", Kind = "flowchart" };

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, ValidRequest(item), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True((await Db.RequirementItems.SingleAsync(TestContext.Current.CancellationToken)).HasDiagram);
        var generation = await Db.DocumentGenerations.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Contains("graph LR; Start to End", generation.Items);
    }

    // UTC09: Null optional collections on an item (acceptance criteria, source lines) -> treated as empty
    [Fact]
    public async Task GenerateDocumentAsync_WithNullItemCollections_TreatsThemAsEmpty()
    {
        SetupAuthorizedUser();
        var item = Item("Bare");
        item.AcceptanceCriteria = null!;
        item.SourceLines = null!;

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, ValidRequest(item), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var req = await Db.RequirementItems.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("[]", req.AcceptanceCriteria);
    }

    // UTC10: Null fields and null subtitle/rawNotes -> defaults used (empty fields JSON, empty raw notes, null subtitle)
    [Fact]
    public async Task GenerateDocumentAsync_WithNullFieldsAndNotes_UsesDefaults()
    {
        SetupAuthorizedUser();
        var request = ValidRequest();
        request.Fields = null!;
        request.RawNotes = null!;
        request.Subtitle = null;

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.Current.Subtitle);
        var generation = await Db.DocumentGenerations.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(string.Empty, generation.RawNotes);
        using var fields = JsonDocument.Parse(generation.Fields);
        Assert.Equal(string.Empty, fields.RootElement.GetProperty("scope").GetString());
        Assert.Contains("I. Overall Requirements", (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).ContentText);
    }

    // UTC11: Null entries inside Items are ignored
    [Fact]
    public async Task GenerateDocumentAsync_WithNullItemEntries_IgnoresThem()
    {
        SetupAuthorizedUser();
        var request = ValidRequest(Item("Real"));
        request.Items.Add(null!);

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.Node.ReqCount);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC12: Unauthenticated caller -> 401, nothing persisted
    [Fact]
    public async Task GenerateDocumentAsync_WhenUnauthenticated_ReturnsUnauthorizedAndPersistsNothing()
    {
        DocumentTestData.GuardUnauthenticated(AccessGuardMock, ProjectId, PermissionKeys.DocumentCreate);

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, ValidRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Null(result.Data);
        await AssertNothingPersistedAsync();
    }

    // UTC13: Permission denied -> 403 FORBIDDEN with required permission, nothing persisted
    [Fact]
    public async Task GenerateDocumentAsync_WhenPermissionDenied_ReturnsForbiddenAndPersistsNothing()
    {
        DocumentTestData.GuardDeny(AccessGuardMock, ProjectId, PermissionKeys.DocumentCreate);

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, ValidRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.DocumentCreate, result.RequiredPermission);
        await AssertNothingPersistedAsync();
    }

    // UTC14: Empty title -> 400 VALIDATION_ERROR (title)
    [Fact]
    public async Task GenerateDocumentAsync_WithEmptyTitle_ReturnsTitleValidationError()
    {
        SetupAuthorizedUser();
        var request = ValidRequest();
        request.Title = "";

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal(new[] { "title" }, result.Errors!.Keys.ToArray());
        Assert.Equal("Title is required and must be at most 150 characters.", result.Errors["title"][0]);
        await AssertNothingPersistedAsync();
    }

    // UTC15: Whitespace-only title -> 400 VALIDATION_ERROR (title)
    [Fact]
    public async Task GenerateDocumentAsync_WithWhitespaceTitle_ReturnsTitleValidationError()
    {
        SetupAuthorizedUser();
        var request = ValidRequest();
        request.Title = "    ";

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.True(result.Errors!.ContainsKey("title"));
        await AssertNothingPersistedAsync();
    }

    // UTC16: Empty docType -> 400 VALIDATION_ERROR (docType)
    [Fact]
    public async Task GenerateDocumentAsync_WithEmptyDocType_ReturnsDocTypeValidationError()
    {
        SetupAuthorizedUser();
        var request = ValidRequest();
        request.DocType = "";

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(new[] { "docType" }, result.Errors!.Keys.ToArray());
        await AssertNothingPersistedAsync();
    }

    // UTC17: Unknown docType -> 400 VALIDATION_ERROR (docType)
    [Fact]
    public async Task GenerateDocumentAsync_WithInvalidDocType_ReturnsDocTypeValidationError()
    {
        SetupAuthorizedUser();
        var request = ValidRequest();
        request.DocType = "MANUAL";

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("docType must be valid (BRD, SRS, ADR, UI_SPEC, TEST_PLAN, OTHER).", result.Errors!["docType"][0]);
        await AssertNothingPersistedAsync();
    }

    // UTC18: Empty items list -> 400 VALIDATION_ERROR (items: at least 1 required)
    [Fact]
    public async Task GenerateDocumentAsync_WithEmptyItems_ReturnsItemsValidationError()
    {
        SetupAuthorizedUser();
        var request = ValidRequest();
        request.Items = new List<ExtractedRequirementDto>();

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("At least 1 requirement item is required.", result.Errors!["items"][0]);
        await AssertNothingPersistedAsync();
    }

    // UTC19: Null items list -> 400 VALIDATION_ERROR (items)
    [Fact]
    public async Task GenerateDocumentAsync_WithNullItems_ReturnsItemsValidationError()
    {
        SetupAuthorizedUser();
        var request = ValidRequest();
        request.Items = null!;

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("At least 1 requirement item is required.", result.Errors!["items"][0]);
        await AssertNothingPersistedAsync();
    }

    // UTC20: All items rejected -> 400 VALIDATION_ERROR (items: accept at least one)
    [Fact]
    public async Task GenerateDocumentAsync_WithAllItemsRejected_ReturnsAcceptAtLeastOneError()
    {
        SetupAuthorizedUser();
        var request = ValidRequest(Item("A", reviewStatus: "REJECTED"), Item("B", reviewStatus: "rejected"));

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("Accept at least one requirement before creating the document.", result.Errors!["items"][0]);
        await AssertNothingPersistedAsync();
    }

    // UTC21: Title, docType and items all invalid -> every error reported together
    [Fact]
    public async Task GenerateDocumentAsync_WithMultipleInvalidFields_ReturnsAllValidationErrors()
    {
        SetupAuthorizedUser();
        var request = new GenerateDocumentRequest { Title = "", DocType = "NOPE", Items = new List<ExtractedRequirementDto>() };

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(new[] { "docType", "items", "title" }, result.Errors!.Keys.OrderBy(k => k).ToArray());
        await AssertNothingPersistedAsync();
    }

    // UTC22: Parent does not exist -> 400 INVALID_PARENT
    [Fact]
    public async Task GenerateDocumentAsync_WithNonExistentParent_ReturnsInvalidParent()
    {
        SetupAuthorizedUser();
        var request = ValidRequest();
        request.ParentId = 999;

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("INVALID_PARENT", result.ErrorCode);
        await AssertNothingPersistedAsync();
    }

    // UTC23: Parent in another project -> 400 INVALID_PARENT
    [Fact]
    public async Task GenerateDocumentAsync_WithParentInAnotherProject_ReturnsInvalidParent()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        DocumentTestData.AddNode(Db, now, 1, projectId: 99, kind: DocumentNodeKinds.Folder, title: "Foreign");
        var request = ValidRequest();
        request.ParentId = 1;

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_PARENT", result.ErrorCode);
        Assert.Single(await Db.DocumentNodes.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.DocumentGenerations.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC24: Parent is soft-deleted -> 400 INVALID_PARENT
    [Fact]
    public async Task GenerateDocumentAsync_WithDeletedParent_ReturnsInvalidParent()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        DocumentTestData.AddNode(Db, now, 1, ProjectId, DocumentNodeKinds.Folder, "Deleted", isDeleted: true);
        var request = ValidRequest();
        request.ParentId = 1;

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_PARENT", result.ErrorCode);
        Assert.Single(await Db.DocumentNodes.ToListAsync(TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC25: Title of 1 character (min) -> created
    [Fact]
    public async Task GenerateDocumentAsync_WithTitleOfOneCharacter_Succeeds()
    {
        SetupAuthorizedUser();
        var request = ValidRequest();
        request.Title = "A";

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("A", result.Data!.Node.Title);
    }

    // UTC26: Title of exactly 150 characters (max) -> created
    [Fact]
    public async Task GenerateDocumentAsync_WithTitleOf150Characters_Succeeds()
    {
        SetupAuthorizedUser();
        var request = ValidRequest();
        request.Title = new string('T', 150);

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new string('T', 150), (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).Title);
    }

    // UTC27: Title of 151 characters (max+1) -> validation error
    [Fact]
    public async Task GenerateDocumentAsync_WithTitleOf151Characters_ReturnsTitleValidationError()
    {
        SetupAuthorizedUser();
        var request = ValidRequest();
        request.Title = new string('T', 151);

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.True(result.Errors!.ContainsKey("title"));
        await AssertNothingPersistedAsync();
    }

    // UTC28: Exactly one item (min) that is accepted -> created with one requirement
    [Fact]
    public async Task GenerateDocumentAsync_WithSingleAcceptedItem_CreatesOneRequirement()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, ValidRequest(Item("Only")), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.Node.ReqCount);
        Assert.Equal("Tạo từ ghi chú cuộc họp (1 yêu cầu)", (await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken)).Note);
    }

    // UTC29: Exactly one accepted among many rejected -> created with that single requirement
    [Fact]
    public async Task GenerateDocumentAsync_WithOneAcceptedAmongRejected_CreatesOneRequirement()
    {
        SetupAuthorizedUser();
        var request = ValidRequest(Item("Rej1", reviewStatus: "REJECTED"), Item("Keep"), Item("Rej2", reviewStatus: "REJECTED"));

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.Node.ReqCount);
        Assert.Equal("Keep", (await Db.RequirementItems.SingleAsync(TestContext.Current.CancellationToken)).Title);
    }

    // UTC30: Many items (25) -> sequential REQ uids and sort orders
    [Fact]
    public async Task GenerateDocumentAsync_WithManyItems_AllocatesSequentialRequirementUids()
    {
        SetupAuthorizedUser();
        var items = Enumerable.Range(1, 25).Select(i => Item($"Item {i}")).ToArray();

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, ValidRequest(items), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(25, result.Data!.Node.ReqCount);
        var reqs = await Db.RequirementItems.OrderBy(r => r.SortOrder).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal("REQ-001", reqs[0].ReqUid);
        Assert.Equal("REQ-025", reqs[24].ReqUid);
        Assert.Equal(Enumerable.Range(0, 25), reqs.Select(r => r.SortOrder));
    }

    // =========================================================================
    // 4. DOC-TYPE SPECIFIC TEMPLATES & SRS AUTO DIAGRAM
    // =========================================================================

    private static ExtractedRequirementDto ItemWithCriteria(string title, params string[] criteria)
    {
        var item = Item(title);
        item.AcceptanceCriteria = criteria.ToList();
        return item;
    }

    // UTC31: SRS docType -> SRS template (overall requirements, functional requirements sections) is generated
    [Fact]
    public async Task GenerateDocumentAsync_WithSrsDocType_UsesSrsTemplate()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, ValidRequest(Item("Login")), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var version = await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Contains("I. Overall Requirements", version.Content);
        Assert.Contains("III. Functional Requirements", version.Content);
        Assert.Contains("IV. Non-Functional Requirements", version.Content);
        Assert.Contains("REQ-001", version.ContentText);
        Assert.Equal(1, result.Data!.Node.ReqCount);
    }

    // UTC32: BRD docType -> BRD template (business problem, success criteria) is generated
    [Fact]
    public async Task GenerateDocumentAsync_WithBrdDocType_UsesBrdTemplate()
    {
        SetupAuthorizedUser();
        var request = ValidRequest(ItemWithCriteria("Login", "User can sign in"));
        request.DocType = "BRD";

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var version = await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Contains("1. Business Problem", version.Content);
        Assert.Contains("9. Success Criteria", version.Content);
        Assert.DoesNotContain("I. Overall Requirements", version.Content);
        Assert.Contains("Login: User can sign in", version.ContentText);
        Assert.Equal(1, result.Data!.Node.ReqCount);
    }

    // UTC33: URD docType -> URD template (introduction, acceptance criteria per requirement, priority list) is generated
    [Fact]
    public async Task GenerateDocumentAsync_WithUrdDocType_UsesUrdTemplate()
    {
        SetupAuthorizedUser();
        var request = ValidRequest(ItemWithCriteria("Login", "User can sign in"));
        request.DocType = "URD";

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var version = await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Contains("1. Introduction", version.Content);
        Assert.Contains("7. Requirement Priority", version.Content);
        Assert.Contains("8. Acceptance Criteria", version.Content);
        Assert.DoesNotContain("I. Overall Requirements", version.Content);
        Assert.Contains("User can sign in", version.ContentText);
        Assert.Equal(1, result.Data!.Node.ReqCount);
    }

    // UTC34: OTHER docType -> falls back to the generic template (no SRS/BRD/URD sections)
    [Fact]
    public async Task GenerateDocumentAsync_WithOtherDocType_UsesGenericTemplate()
    {
        SetupAuthorizedUser();
        var request = ValidRequest(Item("Login"));
        request.DocType = "OTHER";

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var version = await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("I. Overall Requirements", version.Content);
        Assert.DoesNotContain("1. Business Problem", version.Content);
        Assert.DoesNotContain("1. Introduction", version.Content);
        Assert.Contains("REQ-001", version.ContentText);
    }

    // UTC35: SRS item without diagram but with >= 2 acceptance criteria -> flowchart auto-generated and stored
    [Fact]
    public async Task GenerateDocumentAsync_WithSrsItemWithoutDiagram_AutoGeneratesFlowchart()
    {
        SetupAuthorizedUser();
        var item = ItemWithCriteria("Login", "Open the login page", "Submit valid credentials", "Redirect to dashboard");

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, ValidRequest(item), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True((await Db.RequirementItems.SingleAsync(TestContext.Current.CancellationToken)).HasDiagram);
        var generation = await Db.DocumentGenerations.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Contains("flowchart TD", generation.Items);
        var version = await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"language\":\"mermaid\"", version.Content);
        Assert.DoesNotContain("mermaidBlock", version.Content);
    }

    // UTC36: SRS item whose diagram has blank mermaid text -> replaced by an auto-generated diagram
    [Fact]
    public async Task GenerateDocumentAsync_WithSrsItemBlankDiagram_ReplacesWithGeneratedDiagram()
    {
        SetupAuthorizedUser();
        var item = ItemWithCriteria("Login", "Open the login page", "Submit valid credentials");
        item.Diagram = new DiagramProposalDto { Mermaid = "   ", Kind = "flowchart" };

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, ValidRequest(item), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True((await Db.RequirementItems.SingleAsync(TestContext.Current.CancellationToken)).HasDiagram);
        Assert.Contains("flowchart TD", (await Db.DocumentGenerations.SingleAsync(TestContext.Current.CancellationToken)).Items);
    }

    // UTC37: SRS item with an existing diagram -> the provided diagram is kept (not regenerated)
    [Fact]
    public async Task GenerateDocumentAsync_WithSrsItemExistingDiagram_KeepsProvidedDiagram()
    {
        SetupAuthorizedUser();
        var item = ItemWithCriteria("Login", "Open the login page", "Submit valid credentials");
        item.Diagram = new DiagramProposalDto { Mermaid = "graph LR; Custom to Diagram", Kind = "flowchart" };

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, ValidRequest(item), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var generation = await Db.DocumentGenerations.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Contains("graph LR; Custom to Diagram", generation.Items);
        Assert.DoesNotContain("flowchart TD", generation.Items);
    }

    // UTC38: SRS item with a single acceptance criterion (fewer than 2 steps) -> no diagram generated
    [Fact]
    public async Task GenerateDocumentAsync_WithSrsItemSingleCriterion_DoesNotGenerateDiagram()
    {
        SetupAuthorizedUser();
        var item = ItemWithCriteria("Login", "Open the login page");

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, ValidRequest(item), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False((await Db.RequirementItems.SingleAsync(TestContext.Current.CancellationToken)).HasDiagram);
    }

    // UTC39: SRS item with no acceptance criteria (null) -> no diagram generated
    [Fact]
    public async Task GenerateDocumentAsync_WithSrsItemNullCriteria_DoesNotGenerateDiagram()
    {
        SetupAuthorizedUser();
        var item = Item("Login");
        item.AcceptanceCriteria = null!;

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, ValidRequest(item), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False((await Db.RequirementItems.SingleAsync(TestContext.Current.CancellationToken)).HasDiagram);
    }

    // UTC40: BRD item without diagram -> diagram is NOT auto-generated (only SRS does that)
    [Fact]
    public async Task GenerateDocumentAsync_WithBrdItemWithoutDiagram_DoesNotAutoGenerateDiagram()
    {
        SetupAuthorizedUser();
        var request = ValidRequest(ItemWithCriteria("Login", "Open the login page", "Submit valid credentials"));
        request.DocType = "BRD";

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False((await Db.RequirementItems.SingleAsync(TestContext.Current.CancellationToken)).HasDiagram);
        Assert.DoesNotContain("flowchart TD", (await Db.DocumentGenerations.SingleAsync(TestContext.Current.CancellationToken)).Items);
    }

    // UTC41: BRD item with provided diagram -> stored as a mermaid codeBlock (no legacy mermaidBlock)
    [Fact]
    public async Task GenerateDocumentAsync_WithBrdItemDiagram_StoresMermaidCodeBlock()
    {
        SetupAuthorizedUser();
        var item = Item("Flow");
        item.Diagram = new DiagramProposalDto { Mermaid = "graph LR; Start to End", Kind = "flowchart" };
        var request = ValidRequest(item);
        request.DocType = "BRD";

        var result = await DocumentService.GenerateDocumentAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True((await Db.RequirementItems.SingleAsync(TestContext.Current.CancellationToken)).HasDiagram);
        var version = await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"language\":\"mermaid\"", version.Content);
        Assert.DoesNotContain("mermaidBlock", version.Content);
    }
}
