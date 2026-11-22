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

    // UTC001: Create company successfully with full valid information and update the creator phone when empty.
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

    // UTC002: Create company successfully with only required fields; optional values remain empty or null.
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

    // UTC003: Do not overwrite the creator phone when the creator already has one.
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

    // UTC004: An unauthenticated user returns Unauthorized.
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

    // UTC005: An active company already exists -> Returns Conflict and stops before creator checks.
    [Fact]
    public async Task CreateCompanyAsync_WhenActiveCompanyAlreadyExists_ReturnsConflict()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        CompanyRepositoryMock
            .Setup(r => r.AnyActiveCompanyExistsAsync(It.IsAny<CancellationToken>()))
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
        Assert.Equal("COMPANY_ALREADY_EXISTS", result.ErrorCode);
        Assert.Equal("Hệ thống đã có công ty, không thể tạo thêm công ty mới.", result.ErrorMessage);
        CompanyRepositoryMock.Verify(
            r => r.HasAnyActiveCompanyAsync(It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // UTC006: The creator already belongs to an active company -> Returns Conflict.
    [Fact]
    public async Task CreateCompanyAsync_WhenCreatorAlreadyBelongsToCompany_ReturnsConflict()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        CompanyRepositoryMock
            .Setup(r => r.AnyActiveCompanyExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        CompanyRepositoryMock
            .Setup(r => r.HasAnyActiveCompanyAsync(1, null, It.IsAny<CancellationToken>()))
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
        Assert.Equal("ALREADY_IN_COMPANY", result.ErrorCode);
        Assert.Equal("Tài khoản này đã thuộc một công ty, không thể tạo thêm công ty khác.", result.ErrorMessage);
        CompanyRepositoryMock.Verify(
            r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // UTC007: An empty or whitespace company name returns a validation error.
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

    // UTC008 + UTC009: An empty or invalid workspace slug returns a validation error.
    [Theory]
    [InlineData("   ", "Workspace slug là bắt buộc.")]                                              // UTC008: Whitespace-only slug.
    [InlineData("SSAM_Tech!", "Workspace slug chỉ được chứa chữ thường, số và dấu gạch ngang.")]   // UTC009: Uppercase and invalid characters.
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

    // UTC010 + UTC011: A public email domain returns a validation error.
    [Theory]
    [InlineData("gmail.com")]       // UTC010: Lowercase public domain.
    [InlineData("  OUTLOOK.COM  ")] // UTC011: Uppercase public domain with spaces.
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

    // UTC012 + UTC013: An invalid admin phone format returns a validation error.
    [Theory]
    [InlineData("1234567890")]  // UTC012: Does not start with 0 or +84.
    [InlineData("09123456")]    // UTC013: Too short.
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

    // UTC014: Multiple invalid fields return all four validation errors.
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

    // UTC015: An existing workspace slug returns Conflict.
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

    // UTC016 + UTC017: Valid admin-phone boundary formats return success.
    [Theory]
    [InlineData("0912345678")]      // UTC016: 0 followed by 9 digits.
    [InlineData("+849123456789")]   // UTC017: +84 followed by 9 digits.
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

    // UTC018: An admin phone beyond the supported boundary returns a validation error.
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

    // UTC019 + UTC020: Name boundary lengths 1 and 255 return Created.
    [Theory]
    [InlineData(1)]     // UTC019: Name contains 1 character.
    [InlineData(255)]   // UTC020: Name contains 255 characters.
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

    // UTC021: A 256-character name propagates the database update exception.
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

    // UTC022 + UTC023: Workspace-slug boundary lengths 1 and 50 return Created.
    [Theory]
    [InlineData(1)]     // UTC022: Workspace slug contains 1 character.
    [InlineData(50)]    // UTC023: Workspace slug contains 50 characters.
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

    // UTC024: A 51-character workspace slug propagates the database update exception.
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

    // UTC025 + UTC026: Email-domain boundary lengths 1 and 255 return Created.
    [Theory]
    [InlineData(1)]     // UTC025: Email domain contains 1 character.
    [InlineData(255)]   // UTC026: Email domain contains 255 characters.
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

    // UTC027: A 256-character email domain propagates the database update exception.
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

    // UTC028 + UTC029: Tax-code boundary lengths 1 and 100 return Created.
    [Theory]
    [InlineData(1)]     // UTC028: Tax code contains 1 character.
    [InlineData(100)]   // UTC029: Tax code contains 100 characters.
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

    // UTC030: A 101-character tax code propagates the database update exception.
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