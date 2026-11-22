using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Documents;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.DocumentServiceTests;

public class ExtractRequirementsAsyncTests : DocumentServiceTestBase
{
    private const long ProjectId = 10;

    private void SetupAuthorizedUser(bool aiEnabled = true, bool projectExists = true)
    {
        CurrentUserMock.Setup(u => u.UserId).Returns(1);
        DocumentTestData.GuardAllow(AccessGuardMock, ProjectId, PermissionKeys.DocumentCreate);
        DocumentTestData.GuardAllow(AccessGuardMock, ProjectId, PermissionKeys.AiUse);
        if (projectExists)
        {
            Db.Projects.Add(new Project { Id = ProjectId, WorkspaceId = 1, Key = "SSAM", Name = "SSAM", AiEnabled = aiEnabled, CreatedBy = 1 });
            Db.SaveChanges();
        }
    }

    private static List<ExtractedRequirementDto> SampleDrafts() => new()
    {
        new ExtractedRequirementDto { TempId = "t1", Title = "Login", Description = "User can log in", Priority = "HIGH", AcceptanceCriteria = new List<string> { "Valid creds succeed" }, Confidence = 0.9, IsAiGenerated = true },
        new ExtractedRequirementDto { TempId = "t2", Title = "Logout", Description = "User can log out", Priority = "LOW", Confidence = 0.5, IsAiGenerated = true }
    };

    private static ExtractRequirementsRequest FieldsRequest() => new()
    {
        Fields = new NormalizedFieldsDto { Scope = "Auth module", Actors = "User", MainFlows = "• Login", BusinessRules = "• Lock after 5 tries" }
    };

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: AI returns drafts for normalized fields -> 200 with the AI items, service called with the fields and notes
    [Fact]
    public async Task ExtractRequirementsAsync_WithFieldsAndAiSuccess_ReturnsAiItems()
    {
        SetupAuthorizedUser();
        var drafts = SampleDrafts();
        AiServiceMock
            .Setup(a => a.GenerateRequirementDraftsAsync(It.IsAny<NormalizedFieldsDto>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(drafts);
        var request = FieldsRequest();
        request.RawNotes = "meeting notes";

        var result = await DocumentService.ExtractRequirementsAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Items.Count);
        Assert.Equal(new[] { "t1", "t2" }, result.Data.Items.Select(i => i.TempId).ToArray());
        Assert.Equal("Login", result.Data.Items[0].Title);
        Assert.Equal("HIGH", result.Data.Items[0].Priority);
        Assert.Equal(new[] { "Valid creds succeed" }, result.Data.Items[0].AcceptanceCriteria.ToArray());
        Assert.Equal(0.9, result.Data.Items[0].Confidence);
        Assert.True(result.Data.Items[0].IsAiGenerated);
        AiServiceMock.Verify(a => a.GenerateRequirementDraftsAsync(
            It.Is<NormalizedFieldsDto>(f => f.Scope == "Auth module" && f.Actors == "User" && f.MainFlows == "• Login" && f.BusinessRules == "• Lock after 5 tries"),
            "meeting notes",
            It.IsAny<CancellationToken>()), Times.Once);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.DocumentCreate, It.IsAny<CancellationToken>()), Times.Once);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.AiUse, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Only raw notes provided (empty fields) -> accepted and AI invoked
    [Fact]
    public async Task ExtractRequirementsAsync_WithOnlyRawNotes_CallsAi()
    {
        SetupAuthorizedUser();
        AiServiceMock
            .Setup(a => a.GenerateRequirementDraftsAsync(It.IsAny<NormalizedFieldsDto>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleDrafts());

        var result = await DocumentService.ExtractRequirementsAsync(ProjectId, new ExtractRequirementsRequest { RawNotes = "Some raw notes" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Items.Count);
        AiServiceMock.Verify(a => a.GenerateRequirementDraftsAsync(It.IsAny<NormalizedFieldsDto>(), "Some raw notes", It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC03: AI returns an empty list -> 200 with an empty items collection
    [Fact]
    public async Task ExtractRequirementsAsync_WhenAiReturnsEmptyList_ReturnsEmptyItems()
    {
        SetupAuthorizedUser();
        AiServiceMock
            .Setup(a => a.GenerateRequirementDraftsAsync(It.IsAny<NormalizedFieldsDto>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExtractedRequirementDto>());

        var result = await DocumentService.ExtractRequirementsAsync(ProjectId, FieldsRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data.Items);
        AiServiceMock.Verify(a => a.GenerateRequirementDraftsAsync(It.IsAny<NormalizedFieldsDto>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC04: Extraction is read-only -> no documents, generations or activities are persisted
    [Fact]
    public async Task ExtractRequirementsAsync_OnSuccess_PersistsNothing()
    {
        SetupAuthorizedUser();
        AiServiceMock
            .Setup(a => a.GenerateRequirementDraftsAsync(It.IsAny<NormalizedFieldsDto>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleDrafts());

        var result = await DocumentService.ExtractRequirementsAsync(ProjectId, FieldsRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(await Db.DocumentNodes.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.DocumentGenerations.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC05: Unauthenticated caller (document permission check) -> 401, AI never called and AI permission not checked
    [Fact]
    public async Task ExtractRequirementsAsync_WhenUnauthenticated_ReturnsUnauthorizedAndSkipsAi()
    {
        DocumentTestData.GuardUnauthenticated(AccessGuardMock, ProjectId, PermissionKeys.DocumentCreate);

        var result = await DocumentService.ExtractRequirementsAsync(ProjectId, FieldsRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Null(result.Data);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.AiUse, It.IsAny<CancellationToken>()), Times.Never);
        AiServiceMock.Verify(a => a.GenerateRequirementDraftsAsync(It.IsAny<NormalizedFieldsDto>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC06: Missing document create permission -> 403 FORBIDDEN, AI never called
    [Fact]
    public async Task ExtractRequirementsAsync_WhenDocumentPermissionDenied_ReturnsForbiddenAndSkipsAi()
    {
        DocumentTestData.GuardDeny(AccessGuardMock, ProjectId, PermissionKeys.DocumentCreate);

        var result = await DocumentService.ExtractRequirementsAsync(ProjectId, FieldsRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.DocumentCreate, result.RequiredPermission);
        AiServiceMock.Verify(a => a.GenerateRequirementDraftsAsync(It.IsAny<NormalizedFieldsDto>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC07: Missing AI-use permission -> 403 FORBIDDEN (ai.use), AI never called
    [Fact]
    public async Task ExtractRequirementsAsync_WhenAiPermissionDenied_ReturnsForbiddenAndSkipsAi()
    {
        DocumentTestData.GuardAllow(AccessGuardMock, ProjectId, PermissionKeys.DocumentCreate);
        DocumentTestData.GuardDeny(AccessGuardMock, ProjectId, PermissionKeys.AiUse);

        var result = await DocumentService.ExtractRequirementsAsync(ProjectId, FieldsRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.AiUse, result.RequiredPermission);
        AiServiceMock.Verify(a => a.GenerateRequirementDraftsAsync(It.IsAny<NormalizedFieldsDto>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC08: Project has AI disabled -> 403 AI_DISABLED, AI never called
    [Fact]
    public async Task ExtractRequirementsAsync_WhenProjectAiDisabled_ReturnsAiDisabled()
    {
        SetupAuthorizedUser(aiEnabled: false);

        var result = await DocumentService.ExtractRequirementsAsync(ProjectId, FieldsRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("AI_DISABLED", result.ErrorCode);
        Assert.Equal("AI features are disabled for this project.", result.ErrorMessage);
        AiServiceMock.Verify(a => a.GenerateRequirementDraftsAsync(It.IsAny<NormalizedFieldsDto>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC09: Project row does not exist -> treated as AI disabled (403 AI_DISABLED)
    [Fact]
    public async Task ExtractRequirementsAsync_WhenProjectRowMissing_ReturnsAiDisabled()
    {
        SetupAuthorizedUser(projectExists: false);

        var result = await DocumentService.ExtractRequirementsAsync(ProjectId, FieldsRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("AI_DISABLED", result.ErrorCode);
    }

    // UTC10: Neither fields nor raw notes provided -> 400 VALIDATION_ERROR (fields), AI never called
    [Fact]
    public async Task ExtractRequirementsAsync_WithNoFieldsAndNoNotes_ReturnsValidationError()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.ExtractRequirementsAsync(ProjectId, new ExtractRequirementsRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal("Provide normalized fields or raw notes to generate requirements.", result.Errors!["fields"][0]);
        AiServiceMock.Verify(a => a.GenerateRequirementDraftsAsync(It.IsAny<NormalizedFieldsDto>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC11: Fields null (defensive) and raw notes whitespace -> 400 VALIDATION_ERROR
    [Fact]
    public async Task ExtractRequirementsAsync_WithNullFieldsAndWhitespaceNotes_ReturnsValidationError()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.ExtractRequirementsAsync(ProjectId, new ExtractRequirementsRequest { Fields = null!, RawNotes = "   " }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.True(result.Errors!.ContainsKey("fields"));
    }

    // UTC12: Whitespace-only fields and null notes -> 400 VALIDATION_ERROR
    [Fact]
    public async Task ExtractRequirementsAsync_WithWhitespaceFields_ReturnsValidationError()
    {
        SetupAuthorizedUser();
        var request = new ExtractRequirementsRequest { Fields = new NormalizedFieldsDto { Scope = " ", Actors = "\t", MainFlows = "\n", BusinessRules = "  " }, RawNotes = null };

        var result = await DocumentService.ExtractRequirementsAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    // UTC13: AI service throws -> exception propagates (no error handling in the service)
    [Fact]
    public async Task ExtractRequirementsAsync_WhenAiThrows_PropagatesException()
    {
        SetupAuthorizedUser();
        AiServiceMock
            .Setup(a => a.GenerateRequirementDraftsAsync(It.IsAny<NormalizedFieldsDto>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("AI provider unavailable"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DocumentService.ExtractRequirementsAsync(ProjectId, FieldsRequest(), TestContext.Current.CancellationToken));

        Assert.Equal("AI provider unavailable", ex.Message);
        AiServiceMock.Verify(a => a.GenerateRequirementDraftsAsync(It.IsAny<NormalizedFieldsDto>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(await Db.DocumentNodes.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC14: AI call cancelled -> OperationCanceledException propagates
    [Fact]
    public async Task ExtractRequirementsAsync_WhenAiCancelled_PropagatesCancellation()
    {
        SetupAuthorizedUser();
        AiServiceMock
            .Setup(a => a.GenerateRequirementDraftsAsync(It.IsAny<NormalizedFieldsDto>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            DocumentService.ExtractRequirementsAsync(ProjectId, FieldsRequest(), TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC15-UTC18: Exactly one non-empty normalized field (and no notes) is enough to call the AI
    [Theory]
    [InlineData("Scope", "Only scope")] // UTC15
    [InlineData("Actors", "Only actors")] // UTC16
    [InlineData("MainFlows", "Only flows")] // UTC17
    [InlineData("BusinessRules", "Only rules")] // UTC18
    public async Task ExtractRequirementsAsync_WithSingleNonEmptyField_CallsAi(string fieldName, string value)
    {
        SetupAuthorizedUser();
        AiServiceMock
            .Setup(a => a.GenerateRequirementDraftsAsync(It.IsAny<NormalizedFieldsDto>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleDrafts());
        var fields = new NormalizedFieldsDto();
        typeof(NormalizedFieldsDto).GetProperty(fieldName)!.SetValue(fields, value);

        var result = await DocumentService.ExtractRequirementsAsync(ProjectId, new ExtractRequirementsRequest { Fields = fields }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Items.Count);
        AiServiceMock.Verify(a => a.GenerateRequirementDraftsAsync(It.Is<NormalizedFieldsDto>(f => ReferenceEquals(f, fields)), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC19: Single character raw notes (no minimum length enforced here) -> AI invoked
    [Fact]
    public async Task ExtractRequirementsAsync_WithSingleCharacterRawNotes_CallsAi()
    {
        SetupAuthorizedUser();
        AiServiceMock
            .Setup(a => a.GenerateRequirementDraftsAsync(It.IsAny<NormalizedFieldsDto>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExtractedRequirementDto>());

        var result = await DocumentService.ExtractRequirementsAsync(ProjectId, new ExtractRequirementsRequest { RawNotes = "x" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Items);
        AiServiceMock.Verify(a => a.GenerateRequirementDraftsAsync(It.IsAny<NormalizedFieldsDto>(), "x", It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC20: Large AI result (200 drafts) -> all returned in order
    [Fact]
    public async Task ExtractRequirementsAsync_WithLargeAiResult_ReturnsAllItemsInOrder()
    {
        SetupAuthorizedUser();
        var drafts = Enumerable.Range(1, 200).Select(i => new ExtractedRequirementDto { TempId = $"t{i}", Title = $"R{i}" }).ToList();
        AiServiceMock
            .Setup(a => a.GenerateRequirementDraftsAsync(It.IsAny<NormalizedFieldsDto>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(drafts);

        var result = await DocumentService.ExtractRequirementsAsync(ProjectId, FieldsRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.Data!.Items.Count);
        Assert.Equal("t1", result.Data.Items[0].TempId);
        Assert.Equal("t200", result.Data.Items[199].TempId);
    }
}
