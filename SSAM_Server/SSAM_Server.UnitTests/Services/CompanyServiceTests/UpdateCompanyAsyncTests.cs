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

    // UTC001: Update the company successfully with valid fields, trimming inputs and lowercasing EmailDomain.
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

    // UTC002: Null or whitespace EmailDomain is normalized to null and the update succeeds.
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

    // UTC003: An unauthenticated user returns Unauthorized.
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

    // UTC004: A user without CompanyUpdate permission returns Forbidden.
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

    // UTC005: An empty or whitespace company name returns a validation error.
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

    // UTC006: An empty or whitespace tax code returns a validation error.
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

    // UTC007: An empty or whitespace address returns a validation error.
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

    // UTC008 + UTC009: An invalid EmailDomain format returns a validation error.
    [Theory]
    [InlineData("ssam")]          // UTC008: Missing the top-level-domain dot.
    [InlineData("@ssam.vn")]      // UTC009: Contains an '@' character.
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

    // UTC010: Multiple invalid fields return all four validation errors.
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

    // UTC011 + UTC012: A missing or inactive company returns NotFound.
    [Theory]
    [InlineData(false)] // UTC011: Company is not found.
    [InlineData(true)]  // UTC012: Company exists but is inactive.
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

    // UTC013 + UTC014: Name boundary lengths 1 and 200 return Ok.
    [Theory]
    [InlineData(1)]     // UTC013: Name contains 1 character.
    [InlineData(200)]   // UTC014: Name contains 200 characters at the service upper boundary.
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

    // UTC015: A 201-character name returns a validation error.
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

    // UTC016 + UTC017: Tax-code boundary lengths 1 and 100 return Ok.
    [Theory]
    [InlineData(1)]     // UTC016: Tax code contains 1 character.
    [InlineData(100)]   // UTC017: Tax code contains 100 characters at the database upper boundary.
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

    // UTC018: A 101-character tax code propagates the database update exception.
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

    // UTC019 + UTC020: Email-domain boundary lengths 3 and 255 return Ok.
    [Theory]
    [InlineData(3)]     // UTC019: Minimum valid domain length ("a.b").
    [InlineData(255)]   // UTC020: Maximum database domain length.
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

    // UTC021: A 256-character email domain propagates the database update exception.
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