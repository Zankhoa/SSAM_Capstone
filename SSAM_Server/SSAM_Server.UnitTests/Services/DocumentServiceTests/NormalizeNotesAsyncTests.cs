using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Documents;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.DocumentServiceTests;

public class NormalizeNotesAsyncTests : DocumentServiceTestBase
{
    private const long ProjectId = 10;

    private void SetupAuthorizedUser()
    {
        CurrentUserMock.Setup(u => u.UserId).Returns(1);
        DocumentTestData.GuardAllow(AccessGuardMock, ProjectId, PermissionKeys.DocumentCreate);
    }


    private void VerifyNoSideEffects()
    {
        AiServiceMock.VerifyNoOtherCalls();
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Structured notes with labelled sections -> 200, every field mapped from its section
    [Fact]
    public async Task NormalizeNotesAsync_WithLabelledSections_ReturnsMappedFields()
    {
        SetupAuthorizedUser();
        var raw = "Phạm vi: Quản lý đơn hàng\nTác nhân: Khách hàng, Admin\nLuồng: Tạo đơn\n- Thanh toán\nQuy tắc: Số lượng không âm";

        var result = await DocumentService.NormalizeNotesAsync(ProjectId, new NormalizeNotesRequest { RawNotes = raw }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("Quản lý đơn hàng", result.Data.Scope);
        Assert.Equal("Khách hàng, Admin", result.Data.Actors);
        Assert.Equal("• Tạo đơn\n• Thanh toán", result.Data.MainFlows);
        Assert.Equal("• Số lượng không âm", result.Data.BusinessRules);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.DocumentCreate, It.IsAny<CancellationToken>()), Times.Once);
        VerifyNoSideEffects();
    }

    // UTC02: Free text without section labels -> first line becomes scope and actors/flows/rules stay empty
    [Fact]
    public async Task NormalizeNotesAsync_WithFreeText_LeavesOtherFieldsEmpty()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.NormalizeNotesAsync(ProjectId, new NormalizeNotesRequest { RawNotes = "Hệ thống quản lý thư viện số" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Hệ thống quản lý thư viện số", result.Data!.Scope);
        Assert.Equal(string.Empty, result.Data.Actors);
        Assert.Equal(string.Empty, result.Data.MainFlows);
        Assert.Equal(string.Empty, result.Data.BusinessRules);
    }

    // UTC03: Notes are trimmed before validation and processing
    [Fact]
    public async Task NormalizeNotesAsync_WithSurroundingWhitespace_TrimsBeforeProcessing()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.NormalizeNotesAsync(ProjectId, new NormalizeNotesRequest { RawNotes = "   Scope: Billing module   \n\n  " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Billing module", result.Data!.Scope);
    }

    // UTC04: Normalizing persists nothing and never calls the AI service
    [Fact]
    public async Task NormalizeNotesAsync_WithValidNotes_PersistsNothingAndSkipsAi()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.NormalizeNotesAsync(ProjectId, new NormalizeNotesRequest { RawNotes = "Scope: Reporting module" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(await Db.DocumentNodes.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.DocumentGenerations.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
        VerifyNoSideEffects();
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC05: Unauthenticated caller -> 401 from guard, no data returned
    [Fact]
    public async Task NormalizeNotesAsync_WhenUnauthenticated_ReturnsUnauthorized()
    {
        DocumentTestData.GuardUnauthenticated(AccessGuardMock, ProjectId, PermissionKeys.DocumentCreate);

        var result = await DocumentService.NormalizeNotesAsync(ProjectId, new NormalizeNotesRequest { RawNotes = "Scope: Billing module" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Null(result.Data);
    }

    // UTC06: Permission denied -> 403 FORBIDDEN with required permission
    [Fact]
    public async Task NormalizeNotesAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        DocumentTestData.GuardDeny(AccessGuardMock, ProjectId, PermissionKeys.DocumentCreate);

        var result = await DocumentService.NormalizeNotesAsync(ProjectId, new NormalizeNotesRequest { RawNotes = "Scope: Billing module" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.DocumentCreate, result.RequiredPermission);
        Assert.Null(result.Data);
    }

    // UTC07: Empty notes -> 400 VALIDATION_ERROR (rawNotes)
    [Fact]
    public async Task NormalizeNotesAsync_WithEmptyNotes_ReturnsValidationError()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.NormalizeNotesAsync(ProjectId, new NormalizeNotesRequest { RawNotes = "" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal("rawNotes must be between 10 and 20,000 characters.", result.Errors!["rawNotes"][0]);
        Assert.Null(result.Data);
    }

    // UTC08: Null notes (defensive) -> 400 VALIDATION_ERROR (rawNotes)
    [Fact]
    public async Task NormalizeNotesAsync_WithNullNotes_ReturnsValidationError()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.NormalizeNotesAsync(ProjectId, new NormalizeNotesRequest { RawNotes = null! }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.True(result.Errors!.ContainsKey("rawNotes"));
    }

    // UTC09: Whitespace-only notes -> 400 VALIDATION_ERROR (rawNotes)
    [Fact]
    public async Task NormalizeNotesAsync_WithWhitespaceOnlyNotes_ReturnsValidationError()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.NormalizeNotesAsync(ProjectId, new NormalizeNotesRequest { RawNotes = new string(' ', 50) }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.True(result.Errors!.ContainsKey("rawNotes"));
    }

    // UTC10: Notes shorter than 10 characters -> 400 VALIDATION_ERROR (rawNotes)
    [Fact]
    public async Task NormalizeNotesAsync_WithTooShortNotes_ReturnsValidationError()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.NormalizeNotesAsync(ProjectId, new NormalizeNotesRequest { RawNotes = "too short" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.True(result.Errors!.ContainsKey("rawNotes"));
    }

    // UTC11: Notes longer than 20,000 characters -> 400 VALIDATION_ERROR (rawNotes)
    [Fact]
    public async Task NormalizeNotesAsync_WithTooLongNotes_ReturnsValidationError()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.NormalizeNotesAsync(ProjectId, new NormalizeNotesRequest { RawNotes = new string('a', 20001) }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.True(result.Errors!.ContainsKey("rawNotes"));
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC12: Notes of exactly 9 characters (min-1) -> validation error
    [Fact]
    public async Task NormalizeNotesAsync_WithNineCharacters_ReturnsValidationError()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.NormalizeNotesAsync(ProjectId, new NormalizeNotesRequest { RawNotes = "123456789" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    // UTC13: Notes of exactly 10 characters (min) -> success, scope is the notes themselves
    [Fact]
    public async Task NormalizeNotesAsync_WithTenCharacters_Succeeds()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.NormalizeNotesAsync(ProjectId, new NormalizeNotesRequest { RawNotes = "abcdefghij" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("abcdefghij", result.Data!.Scope);
        Assert.Equal(string.Empty, result.Data.Actors);
    }

    // UTC14: Notes of 10 characters surrounded by whitespace (trimmed length 10) -> success
    [Fact]
    public async Task NormalizeNotesAsync_WithPaddedTenCharacters_Succeeds()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.NormalizeNotesAsync(ProjectId, new NormalizeNotesRequest { RawNotes = "   abcdefghij   " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("abcdefghij", result.Data!.Scope);
    }

    // UTC15: 9 characters padded with whitespace to 20 (trimmed length 9) -> validation error
    [Fact]
    public async Task NormalizeNotesAsync_WithPaddedNineCharacters_ReturnsValidationError()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.NormalizeNotesAsync(ProjectId, new NormalizeNotesRequest { RawNotes = "     123456789      " }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    // UTC16: Notes of exactly 20,000 characters (max) -> success
    [Fact]
    public async Task NormalizeNotesAsync_With20000Characters_Succeeds()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.NormalizeNotesAsync(ProjectId, new NormalizeNotesRequest { RawNotes = new string('a', 20000) }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(20000, result.Data!.Scope.Length);
    }

    // UTC17: Notes of 20,000 characters padded with whitespace (trimmed length 20,000) -> success
    [Fact]
    public async Task NormalizeNotesAsync_WithPaddedMaxLength_Succeeds()
    {
        SetupAuthorizedUser();

        var result = await DocumentService.NormalizeNotesAsync(ProjectId, new NormalizeNotesRequest { RawNotes = "  " + new string('a', 20000) + "  " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
    }
}
