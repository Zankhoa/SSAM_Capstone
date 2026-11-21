using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Companies;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.CompanyServiceTests;

public class UpdateCompanyAsyncTests : CompanyServiceTestBase
{
    private Company SetupValidAuthorizationAndCompany(long userId = 1, long companyId = 10)
    {
        CurrentUserMock
            .Setup(u => u.UserIdOrNull)
            .Returns(userId);

        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(userId, companyId, PermissionKeys.CompanyUpdate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.CompanyUpdate }));

        var existingCompany = new Company
        {
            Id = companyId,
            Name = "Old Company Name",
            Code = "ssam-tech",
            TaxCode = "0100000000",
            Address = "Old Address",
            EmailDomain = "old.ssam.vn",
            DomainAutoJoinEnabled = true,
            LogoUrl = "https://cdn.ssam.vn/logo.png",
            IsActive = true
        };

        CompanyRepositoryMock
            .Setup(r => r.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCompany);

        return existingCompany;
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Update company successfully with full valid fields (trims inputs & lowercases EmailDomain)
    [Fact]
    public async Task UpdateCompanyAsync_WithFullValidRequest_ReturnsOkAndUpdatesCompany()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var company = SetupValidAuthorizationAndCompany(userId: 1, companyId: 10);
        var request = new UpdateCompanyRequestDto
        {
            Name = "  SSAM Technology JSC  ",
            TaxCode = "  0101234567  ",
            Address = "  Tầng 5, Tòa nhà ABC, Hà Nội  ",
            EmailDomain = "  SSAM.COM.VN  "
        };

        var result = await CompanyService.UpdateCompanyAsync(10, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(10, result.Data.Id);
        Assert.Equal("SSAM Technology JSC", result.Data.Name);
        Assert.Equal("0101234567", result.Data.TaxCode);
        Assert.Equal("Tầng 5, Tòa nhà ABC, Hà Nội", result.Data.Address);
        Assert.Equal("ssam.com.vn", result.Data.EmailDomain);

        Assert.Equal("SSAM Technology JSC", company.Name);
        Assert.Equal("0101234567", company.TaxCode);
        Assert.Equal("Tầng 5, Tòa nhà ABC, Hà Nội", company.Address);
        Assert.Equal("ssam.com.vn", company.EmailDomain);
        Assert.Equal(now, company.UpdatedAt);

        CompanyRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Update company successfully when optional EmailDomain is null or whitespace -> Sets EmailDomain to null
    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task UpdateCompanyAsync_WhenEmailDomainIsNullOrWhitespace_SetsEmailDomainNullAndReturnsOk(string? emailDomain)
    {
        var company = SetupValidAuthorizationAndCompany(userId: 1, companyId: 10);
        var request = new UpdateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = "0101234567",
            Address = "Hà Nội",
            EmailDomain = emailDomain
        };

        var result = await CompanyService.UpdateCompanyAsync(10, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Null(result.Data!.EmailDomain);
        Assert.Null(company.EmailDomain);

        CompanyRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC03: User is not authenticated (UserIdOrNull is null) -> Returns Unauthorized
    [Fact]
    public async Task UpdateCompanyAsync_WhenUserUnauthenticated_ReturnsUnauthorized()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns((long?)null);
        var request = new UpdateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = "0101234567",
            Address = "Hà Nội"
        };

        var result = await CompanyService.UpdateCompanyAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("Your session is invalid or has expired.", result.ErrorMessage);
    }

    // UTC04: User lacks CompanyUpdate permission -> Returns Forbidden
    [Fact]
    public async Task UpdateCompanyAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.CompanyUpdate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Forbidden("FORBIDDEN", "Bạn không có quyền cập nhật thông tin công ty."));

        var request = new UpdateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = "0101234567",
            Address = "Hà Nội"
        };

        var result = await CompanyService.UpdateCompanyAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    // UTC05: Company Name is empty or whitespace -> Returns Validation Error
    [Fact]
    public async Task UpdateCompanyAsync_WhenNameIsWhitespace_ReturnsValidationError()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.CompanyUpdate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.CompanyUpdate }));

        var request = new UpdateCompanyRequestDto
        {
            Name = "   ",
            TaxCode = "0101234567",
            Address = "Hà Nội"
        };

        var result = await CompanyService.UpdateCompanyAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("name", result.Errors!.Keys);
        Assert.Contains("Tên công ty là bắt buộc.", result.Errors["name"]);
    }

    // UTC06: TaxCode is empty or whitespace -> Returns Validation Error
    [Fact]
    public async Task UpdateCompanyAsync_WhenTaxCodeIsWhitespace_ReturnsValidationError()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.CompanyUpdate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.CompanyUpdate }));

        var request = new UpdateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = "   ",
            Address = "Hà Nội"
        };

        var result = await CompanyService.UpdateCompanyAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("taxCode", result.Errors!.Keys);
        Assert.Contains("Mã số thuế là bắt buộc.", result.Errors["taxCode"]);
    }

    // UTC07: Address is empty or whitespace -> Returns Validation Error
    [Fact]
    public async Task UpdateCompanyAsync_WhenAddressIsWhitespace_ReturnsValidationError()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.CompanyUpdate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.CompanyUpdate }));

        var request = new UpdateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = "0101234567",
            Address = "   "
        };

        var result = await CompanyService.UpdateCompanyAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("address", result.Errors!.Keys);
        Assert.Contains("Địa chỉ công ty là bắt buộc.", result.Errors["address"]);
    }

    // UTC08 + UTC09: EmailDomain has invalid format -> Returns Validation Error
    [Theory]
    [InlineData("ssam")]          // UTC08: Missing TLD dot
    [InlineData("@ssam.vn")]      // UTC09: Contains '@' character
    public async Task UpdateCompanyAsync_WhenEmailDomainFormatIsInvalid_ReturnsValidationError(string invalidDomain)
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.CompanyUpdate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.CompanyUpdate }));

        var request = new UpdateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = "0101234567",
            Address = "Hà Nội",
            EmailDomain = invalidDomain
        };

        var result = await CompanyService.UpdateCompanyAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("emailDomain", result.Errors!.Keys);
        Assert.Contains("Tên miền email không đúng định dạng.", result.Errors["emailDomain"]);
    }

    // UTC10: All 4 fields are invalid simultaneously -> Returns all 4 validation errors
    [Fact]
    public async Task UpdateCompanyAsync_WhenAllFieldsAreInvalid_ReturnsAllValidationErrors()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.CompanyUpdate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.CompanyUpdate }));

        var request = new UpdateCompanyRequestDto
        {
            Name = string.Empty,
            TaxCode = string.Empty,
            Address = string.Empty,
            EmailDomain = "invalid_domain"
        };

        var result = await CompanyService.UpdateCompanyAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal(4, result.Errors!.Count);
        Assert.Contains("name", result.Errors.Keys);
        Assert.Contains("taxCode", result.Errors.Keys);
        Assert.Contains("address", result.Errors.Keys);
        Assert.Contains("emailDomain", result.Errors.Keys);
    }

    // UTC11 + UTC12: Company does not exist or is inactive -> Returns NotFound
    [Theory]
    [InlineData(false)] // UTC11: Company not found (null)
    [InlineData(true)]  // UTC12: Company exists but IsActive = false
    public async Task UpdateCompanyAsync_WhenCompanyNotFoundOrInactive_ReturnsNotFound(bool companyExistsAsInactive)
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.CompanyUpdate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.CompanyUpdate }));

        Company? company = companyExistsAsInactive
            ? new Company { Id = 10, Name = "Inactive Corp", IsActive = false }
            : null;

        CompanyRepositoryMock
            .Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var request = new UpdateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = "0101234567",
            Address = "Hà Nội"
        };

        var result = await CompanyService.UpdateCompanyAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("Không tìm thấy công ty.", result.ErrorMessage);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC13 + UTC14: Name boundary length 1 and 200 -> Returns Ok
    [Theory]
    [InlineData(1)]     // UTC13: Name 1 character
    [InlineData(200)]   // UTC14: Name 200 characters (service upper boundary)
    public async Task UpdateCompanyAsync_WhenNameLengthIsWithinBoundary_ReturnsOk(int length)
    {
        SetupValidAuthorizationAndCompany(userId: 1, companyId: 10);
        var validName = new string('A', length);
        var request = new UpdateCompanyRequestDto
        {
            Name = validName,
            TaxCode = "0101234567",
            Address = "Hà Nội"
        };

        var result = await CompanyService.UpdateCompanyAsync(10, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(validName, result.Data!.Name);
        Assert.Equal(length, result.Data.Name.Length);
    }

    // UTC15: Name boundary length 201 -> Returns Validation Error
    [Fact]
    public async Task UpdateCompanyAsync_WhenNameExceeds200Chars_ReturnsValidationError()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.CompanyUpdate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.CompanyUpdate }));

        var request = new UpdateCompanyRequestDto
        {
            Name = new string('A', 201),
            TaxCode = "0101234567",
            Address = "Hà Nội"
        };

        var result = await CompanyService.UpdateCompanyAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("name", result.Errors!.Keys);
        Assert.Contains("Tên công ty không được vượt quá 200 ký tự.", result.Errors["name"]);
    }

    // UTC16 + UTC17: TaxCode boundary length 1 and 100 -> Returns Ok
    [Theory]
    [InlineData(1)]     // UTC16: TaxCode 1 character
    [InlineData(100)]   // UTC17: TaxCode 100 characters (DB upper boundary)
    public async Task UpdateCompanyAsync_WhenTaxCodeLengthIsWithinBoundary_ReturnsOk(int length)
    {
        SetupValidAuthorizationAndCompany(userId: 1, companyId: 10);
        var validTaxCode = new string('1', length);
        var request = new UpdateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = validTaxCode,
            Address = "Hà Nội"
        };

        var result = await CompanyService.UpdateCompanyAsync(10, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(validTaxCode, result.Data!.TaxCode);
        Assert.Equal(length, result.Data.TaxCode!.Length);
    }

    // UTC18: TaxCode boundary length 101 -> Throws DbUpdateException
    [Fact]
    public async Task UpdateCompanyAsync_WhenTaxCodeExceeds100Chars_ThrowsDbUpdateException()
    {
        SetupValidAuthorizationAndCompany(userId: 1, companyId: 10);
        var request = new UpdateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = new string('1', 101),
            Address = "Hà Nội"
        };

        CompanyRepositoryMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("String or binary data would be truncated in table 'Companies', column 'TaxCode'."));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            CompanyService.UpdateCompanyAsync(10, request, TestContext.Current.CancellationToken));
    }

    // UTC19 + UTC20: EmailDomain boundary length 3 ("a.b") and 255 -> Returns Ok
    [Theory]
    [InlineData(3)]     // UTC19: Minimum valid domain length ("a.b")
    [InlineData(255)]   // UTC20: Maximum DB domain length (255 characters)
    public async Task UpdateCompanyAsync_WhenEmailDomainLengthIsWithinBoundary_ReturnsOk(int length)
    {
        SetupValidAuthorizationAndCompany(userId: 1, companyId: 10);
        var validDomain = length == 3
            ? "a.b"
            : new string('a', length - 8) + ".ssam.vn";

        var request = new UpdateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = "0101234567",
            Address = "Hà Nội",
            EmailDomain = validDomain
        };

        var result = await CompanyService.UpdateCompanyAsync(10, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(validDomain, result.Data!.EmailDomain);
        Assert.Equal(length, result.Data.EmailDomain!.Length);
    }

    // UTC21: EmailDomain boundary length 256 -> Throws DbUpdateException
    [Fact]
    public async Task UpdateCompanyAsync_WhenEmailDomainExceeds255Chars_ThrowsDbUpdateException()
    {
        SetupValidAuthorizationAndCompany(userId: 1, companyId: 10);
        var longDomain = new string('a', 248) + ".ssam.vn"; // 256 characters
        var request = new UpdateCompanyRequestDto
        {
            Name = "SSAM Tech",
            TaxCode = "0101234567",
            Address = "Hà Nội",
            EmailDomain = longDomain
        };

        CompanyRepositoryMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("String or binary data would be truncated in table 'Companies', column 'EmailDomain'."));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            CompanyService.UpdateCompanyAsync(10, request, TestContext.Current.CancellationToken));
    }
}