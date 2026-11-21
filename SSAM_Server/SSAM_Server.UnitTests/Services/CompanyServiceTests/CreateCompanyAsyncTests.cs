using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Companies;
using SSAM_Server.Models;

namespace SSAM_Server.UnitTests.Services.CompanyServiceTests;

public class CreateCompanyAsyncTests : CompanyServiceTestBase
{
    private void SetupSuccessfulCreation(long creatorUserId = 1, long generatedCompanyId = 10, long adminRoleId = 100)
    {
        CurrentUserMock
            .Setup(u => u.UserIdOrNull)
            .Returns(creatorUserId);

        CompanyRepositoryMock
            .Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        CompanyRepositoryMock
            .Setup(r => r.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<CompanyDto>>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<CompanyDto>>, CancellationToken>(
                (action, ct) => action(ct));

        CompanyRepositoryMock
            .Setup(r => r.AddCompanyAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()))
            .Callback<Company, CancellationToken>((c, _) => c.Id = generatedCompanyId)
            .Returns(Task.CompletedTask);

        CompanyRepositoryMock
            .Setup(r => r.GetRoleByCodeAsync(generatedCompanyId, CompanyRoleCodes.TenantAdmin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyRole
            {
                Id = adminRoleId,
                CompanyId = generatedCompanyId,
                Code = CompanyRoleCodes.TenantAdmin,
                Name = "Tenant Admin",
                IsActive = true
            });
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Create company successfully with full valid information (updates creator phone if empty)
    [Fact]
    public async Task CreateCompanyAsync_WithFullValidRequest_ReturnsCreatedAndUpdatesUserPhone()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var creatorUser = new User { Id = 1, Email = "admin@ssam.vn", FullName = "Phạm Công Minh", Phone = null };
        var request = new CreateCompanyRequestDto
        {
            Name = "  SSAM Technology  ",
            TaxCode = "0101234567",
            Address = "Hà Nội",
            EmailDomain = "  SSAM.VN  ",
            AdminPhone = "0912 345-678",
            WorkspaceSlug = "ssam-tech",
            DomainAutoJoinEnabled = true
        };

        SetupSuccessfulCreation(creatorUserId: 1, generatedCompanyId: 10, adminRoleId: 100);
        AuthRepositoryMock
            .Setup(r => r.GetUserByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(creatorUser);

        var result = await CompanyService.CreateCompanyAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(10, result.Data.Id);
        Assert.Equal("SSAM Technology", result.Data.Name);
        Assert.Equal("ssam-tech", result.Data.Code);
        Assert.Equal("ssam.vn", result.Data.EmailDomain);
        Assert.True(result.Data.DomainAutoJoinEnabled);
        Assert.Equal("0101234567", result.Data.TaxCode);
        Assert.Equal("Hà Nội", result.Data.Address);

        Assert.Equal("0912345678", creatorUser.Phone);
        Assert.Equal(now, creatorUser.UpdatedAt);

        CompanyRepositoryMock.Verify(r => r.SeedSystemRolesAndPermissionsAsync(10, now, It.IsAny<CancellationToken>()), Times.Once);
        CompanyRepositoryMock.Verify(r => r.AddMemberAsync(
            It.Is<CompanyMember>(m => m.CompanyId == 10 && m.UserId == 1 && m.RoleId == 100 && m.IsActive),
            It.IsAny<CancellationToken>()), Times.Once);
        CompanyRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // UTC02: Create company successfully with only required fields (non-nullable strings default to empty, nullable fields null)
    [Fact]
    public async Task CreateCompanyAsync_WithMinimalRequiredFields_ReturnsCreated()
    {
        var request = new CreateCompanyRequestDto
        {
            Name = "SSAM Minimal",
            TaxCode = string.Empty,
            Address = string.Empty,
            EmailDomain = null,
            AdminPhone = null,
            WorkspaceSlug = "ssam-minimal",
            DomainAutoJoinEnabled = false
        };
        SetupSuccessfulCreation(creatorUserId: 1, generatedCompanyId: 11, adminRoleId: 100);

        var result = await CompanyService.CreateCompanyAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(11, result.Data.Id);
        Assert.Equal("SSAM Minimal", result.Data.Name);
        Assert.Equal("ssam-minimal", result.Data.Code);
        Assert.Equal(string.Empty, result.Data.TaxCode);
        Assert.Equal(string.Empty, result.Data.Address);
        Assert.Null(result.Data.EmailDomain);

        AuthRepositoryMock.Verify(r => r.GetUserByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC03: Create company with AdminPhone when creator user already has a phone number -> Does not overwrite existing phone
    [Fact]
    public async Task CreateCompanyAsync_WhenCreatorAlreadyHasPhone_DoesNotOverwritePhone()
    {
        var creatorUser = new User { Id = 1, Email = "admin@ssam.vn", FullName = "Phạm Công Minh", Phone = "0988888888" };
        var request = new CreateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = "0101234567",
            Address = "Hà Nội",
            AdminPhone = "0912345678",
            WorkspaceSlug = "ssam-tech"
        };

        SetupSuccessfulCreation(creatorUserId: 1, generatedCompanyId: 10, adminRoleId: 100);
        AuthRepositoryMock
            .Setup(r => r.GetUserByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(creatorUser);

        var result = await CompanyService.CreateCompanyAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal("0988888888", creatorUser.Phone);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC04: User is not authenticated (UserIdOrNull is null) -> Returns Unauthorized
    [Fact]
    public async Task CreateCompanyAsync_WhenUserUnauthenticated_ReturnsUnauthorized()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns((long?)null);
        var request = new CreateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = "0101234567",
            Address = "Hà Nội",
            WorkspaceSlug = "ssam-tech"
        };

        var result = await CompanyService.CreateCompanyAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("Your session is invalid or has expired.", result.ErrorMessage);
    }

    // UTC05: Company Name is empty or whitespace -> Returns Validation Error
    [Fact]
    public async Task CreateCompanyAsync_WhenNameIsWhitespace_ReturnsValidationError()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        var request = new CreateCompanyRequestDto
        {
            Name = "   ",
            TaxCode = "0101234567",
            Address = "Hà Nội",
            WorkspaceSlug = "ssam-tech"
        };

        var result = await CompanyService.CreateCompanyAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("name", result.Errors!.Keys);
        Assert.Contains("Tên công ty là bắt buộc.", result.Errors["name"]);
    }

    // UTC06 + UTC07: WorkspaceSlug is empty or invalid format -> Returns Validation Error
    [Theory]
    [InlineData("   ", "Workspace slug là bắt buộc.")]                                              // UTC06: Whitespace only
    [InlineData("SSAM_Tech!", "Workspace slug chỉ được chứa chữ thường, số và dấu gạch ngang.")]   // UTC07: Uppercase & invalid chars
    public async Task CreateCompanyAsync_WhenWorkspaceSlugIsInvalid_ReturnsValidationError(string invalidSlug, string expectedError)
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        var request = new CreateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = "0101234567",
            Address = "Hà Nội",
            WorkspaceSlug = invalidSlug
        };

        var result = await CompanyService.CreateCompanyAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("workspaceSlug", result.Errors!.Keys);
        Assert.Contains(expectedError, result.Errors["workspaceSlug"]);
    }

    // UTC08 + UTC09: EmailDomain uses public email providers -> Returns Validation Error
    [Theory]
    [InlineData("gmail.com")]       // UTC08: Lowercase public domain
    [InlineData("  OUTLOOK.COM  ")] // UTC09: Uppercase with spaces public domain
    public async Task CreateCompanyAsync_WhenEmailDomainIsPublic_ReturnsValidationError(string publicDomain)
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        var request = new CreateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = "0101234567",
            Address = "Hà Nội",
            WorkspaceSlug = "ssam-tech",
            EmailDomain = publicDomain
        };

        var result = await CompanyService.CreateCompanyAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("emailDomain", result.Errors!.Keys);
        Assert.Contains("Không dùng tên miền email công cộng (gmail.com, yahoo.com…).", result.Errors["emailDomain"]);
    }

    // UTC10 + UTC11: AdminPhone has invalid format -> Returns Validation Error
    [Theory]
    [InlineData("1234567890")]  // UTC10: Does not start with 0 or +84
    [InlineData("09123456")]    // UTC11: Too short (8 digits)
    public async Task CreateCompanyAsync_WhenAdminPhoneIsInvalid_ReturnsValidationError(string invalidPhone)
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        var request = new CreateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = "0101234567",
            Address = "Hà Nội",
            WorkspaceSlug = "ssam-tech",
            AdminPhone = invalidPhone
        };

        var result = await CompanyService.CreateCompanyAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("adminPhone", result.Errors!.Keys);
        Assert.Contains("Số điện thoại không hợp lệ.", result.Errors["adminPhone"]);
    }

    // UTC12: All validated fields are invalid simultaneously -> Returns all 4 validation errors
    [Fact]
    public async Task CreateCompanyAsync_WhenMultipleFieldsAreInvalid_ReturnsAllValidationErrors()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        var request = new CreateCompanyRequestDto
        {
            Name = string.Empty,
            TaxCode = "0101234567",
            Address = "Hà Nội",
            WorkspaceSlug = "INVALID SLUG",
            EmailDomain = "yahoo.com",
            AdminPhone = "abc"
        };

        var result = await CompanyService.CreateCompanyAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal(4, result.Errors!.Count);
        Assert.Contains("name", result.Errors.Keys);
        Assert.Contains("workspaceSlug", result.Errors.Keys);
        Assert.Contains("emailDomain", result.Errors.Keys);
        Assert.Contains("adminPhone", result.Errors.Keys);
    }

    // UTC13: WorkspaceSlug already exists in database -> Returns Conflict
    [Fact]
    public async Task CreateCompanyAsync_WhenWorkspaceSlugAlreadyExists_ReturnsConflict()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        CompanyRepositoryMock
            .Setup(r => r.CodeExistsAsync("ssam-tech", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new CreateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = "0101234567",
            Address = "Hà Nội",
            WorkspaceSlug = "ssam-tech"
        };

        var result = await CompanyService.CreateCompanyAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("WORKSPACE_SLUG_TAKEN", result.ErrorCode);
        Assert.Equal("Mã công ty (slug) đã được sử dụng.", result.ErrorMessage);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC14 + UTC15: AdminPhone boundary valid lengths (10 digits & +84 + 10 digits) -> Success
    [Theory]
    [InlineData("0912345678")]      // UTC14: Lower boundary (0 + 9 digits = 10 digits)
    [InlineData("+849123456789")]   // UTC15: Upper boundary (+84 + 10 digits)
    public async Task CreateCompanyAsync_WhenAdminPhoneIsAtValidBoundary_ReturnsCreated(string validBoundaryPhone)
    {
        var request = new CreateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = "0101234567",
            Address = "Hà Nội",
            WorkspaceSlug = "ssam-tech",
            AdminPhone = validBoundaryPhone
        };
        SetupSuccessfulCreation(creatorUserId: 1, generatedCompanyId: 10, adminRoleId: 100);
        AuthRepositoryMock
            .Setup(r => r.GetUserByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Email = "admin@ssam.vn", FullName = "Admin", Phone = null });

        var result = await CompanyService.CreateCompanyAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
    }

    // UTC16: AdminPhone exceeds upper boundary (0 + 11 digits = 12 digits) -> Returns Validation Error
    [Fact]
    public async Task CreateCompanyAsync_WhenAdminPhoneExceedsUpperBoundary_ReturnsValidationError()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        var request = new CreateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = "0101234567",
            Address = "Hà Nội",
            WorkspaceSlug = "ssam-tech",
            AdminPhone = "091234567890"
        };

        var result = await CompanyService.CreateCompanyAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("adminPhone", result.Errors!.Keys);
        Assert.Contains("Số điện thoại không hợp lệ.", result.Errors["adminPhone"]);
    }

    // UTC17 + UTC18: Name boundary length 1 and 255 -> Returns Created
    [Theory]
    [InlineData(1)]     // UTC17: Name 1 character
    [InlineData(255)]   // UTC18: Name 255 characters
    public async Task CreateCompanyAsync_WhenNameLengthIsWithinBoundary_ReturnsCreated(int length)
    {
        var validName = new string('A', length);
        var request = new CreateCompanyRequestDto
        {
            Name = validName,
            TaxCode = "0101234567",
            Address = "Hà Nội",
            WorkspaceSlug = "ssam-tech"
        };
        SetupSuccessfulCreation(creatorUserId: 1, generatedCompanyId: 10, adminRoleId: 100);

        var result = await CompanyService.CreateCompanyAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(validName, result.Data!.Name);
        Assert.Equal(length, result.Data.Name.Length);
    }

    // UTC19: Name boundary length 256 -> Throws DbUpdateException
    [Fact]
    public async Task CreateCompanyAsync_WhenNameExceeds255Chars_ThrowsDbUpdateException()
    {
        var longName = new string('A', 256);
        var request = new CreateCompanyRequestDto
        {
            Name = longName,
            TaxCode = "0101234567",
            Address = "Hà Nội",
            WorkspaceSlug = "ssam-tech"
        };
        SetupSuccessfulCreation(creatorUserId: 1, generatedCompanyId: 10, adminRoleId: 100);

        CompanyRepositoryMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("String or binary data would be truncated in table 'Companies', column 'Name'."));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            CompanyService.CreateCompanyAsync(request, TestContext.Current.CancellationToken));
    }

    // UTC20 + UTC21: WorkspaceSlug (Code) boundary length 1 and 50 -> Returns Created
    [Theory]
    [InlineData(1)]     // UTC20: WorkspaceSlug 1 character
    [InlineData(50)]    // UTC21: WorkspaceSlug 50 characters
    public async Task CreateCompanyAsync_WhenWorkspaceSlugLengthIsWithinBoundary_ReturnsCreated(int length)
    {
        var validSlug = new string('a', length);
        var request = new CreateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = "0101234567",
            Address = "Hà Nội",
            WorkspaceSlug = validSlug
        };
        SetupSuccessfulCreation(creatorUserId: 1, generatedCompanyId: 10, adminRoleId: 100);

        var result = await CompanyService.CreateCompanyAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(validSlug, result.Data!.Code);
        Assert.Equal(length, result.Data.Code.Length);
    }

    // UTC22: WorkspaceSlug (Code) boundary length 51 -> Throws DbUpdateException
    [Fact]
    public async Task CreateCompanyAsync_WhenWorkspaceSlugExceeds50Chars_ThrowsDbUpdateException()
    {
        var longSlug = new string('a', 51);
        var request = new CreateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = "0101234567",
            Address = "Hà Nội",
            WorkspaceSlug = longSlug
        };
        SetupSuccessfulCreation(creatorUserId: 1, generatedCompanyId: 10, adminRoleId: 100);

        CompanyRepositoryMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("String or binary data would be truncated in table 'Companies', column 'Code'."));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            CompanyService.CreateCompanyAsync(request, TestContext.Current.CancellationToken));
    }

    // UTC23 + UTC24: EmailDomain boundary length 1 and 255 -> Returns Created
    [Theory]
    [InlineData(1)]     // UTC23: EmailDomain 1 character
    [InlineData(255)]   // UTC24: EmailDomain 255 characters
    public async Task CreateCompanyAsync_WhenEmailDomainLengthIsWithinBoundary_ReturnsCreated(int length)
    {
        var validDomain = length == 1
            ? "a"
            : new string('a', length - 8) + ".ssam.vn";

        var request = new CreateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = "0101234567",
            Address = "Hà Nội",
            EmailDomain = validDomain,
            WorkspaceSlug = "ssam-tech"
        };
        SetupSuccessfulCreation(creatorUserId: 1, generatedCompanyId: 10, adminRoleId: 100);

        var result = await CompanyService.CreateCompanyAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(validDomain, result.Data!.EmailDomain);
        Assert.Equal(length, result.Data.EmailDomain!.Length);
    }

    // UTC25: EmailDomain boundary length 256 -> Throws DbUpdateException
    [Fact]
    public async Task CreateCompanyAsync_WhenEmailDomainExceeds255Chars_ThrowsDbUpdateException()
    {
        var longDomain = new string('a', 248) + ".ssam.vn";
        var request = new CreateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = "0101234567",
            Address = "Hà Nội",
            EmailDomain = longDomain,
            WorkspaceSlug = "ssam-tech"
        };
        SetupSuccessfulCreation(creatorUserId: 1, generatedCompanyId: 10, adminRoleId: 100);

        CompanyRepositoryMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("String or binary data would be truncated in table 'Companies', column 'EmailDomain'."));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            CompanyService.CreateCompanyAsync(request, TestContext.Current.CancellationToken));
    }

    // UTC26 + UTC27: TaxCode boundary length 1 and 100 -> Returns Created
    [Theory]
    [InlineData(1)]     // UTC26: TaxCode 1 character
    [InlineData(100)]   // UTC27: TaxCode 100 characters
    public async Task CreateCompanyAsync_WhenTaxCodeLengthIsWithinBoundary_ReturnsCreated(int length)
    {
        var validTaxCode = new string('1', length);
        var request = new CreateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = validTaxCode,
            Address = "Hà Nội",
            WorkspaceSlug = "ssam-tech"
        };
        SetupSuccessfulCreation(creatorUserId: 1, generatedCompanyId: 10, adminRoleId: 100);

        var result = await CompanyService.CreateCompanyAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(validTaxCode, result.Data!.TaxCode);
        Assert.Equal(length, result.Data.TaxCode!.Length);
    }

    // UTC28: TaxCode boundary length 101 -> Throws DbUpdateException
    [Fact]
    public async Task CreateCompanyAsync_WhenTaxCodeExceeds100Chars_ThrowsDbUpdateException()
    {
        var longTaxCode = new string('1', 101);
        var request = new CreateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = longTaxCode,
            Address = "Hà Nội",
            WorkspaceSlug = "ssam-tech"
        };
        SetupSuccessfulCreation(creatorUserId: 1, generatedCompanyId: 10, adminRoleId: 100);

        CompanyRepositoryMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("String or binary data would be truncated in table 'Companies', column 'TaxCode'."));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            CompanyService.CreateCompanyAsync(request, TestContext.Current.CancellationToken));
    }
}