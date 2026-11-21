using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Auth;
using SSAM_Server.Models;

namespace SSAM_Server.UnitTests.Services.AuthServiceTests;

public class RegisterAsyncTests : AuthServiceTestBase
{
    private void SetupSuccessfulRegistration(long generatedAccountId = 1)
    {
        AuthRepositoryMock
            .Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        PasswordHasherMock
            .Setup(p => p.HashPassword(It.IsAny<string>()))
            .Returns("hashed_password");

        AuthRepositoryMock
            .Setup(r => r.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<(Account, string)>>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<(Account, string)>>, CancellationToken>(
                (action, ct) => action(ct));

        AuthRepositoryMock
            .Setup(r => r.AddAccountAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .Callback<Account, CancellationToken>((acc, _) => acc.Id = generatedAccountId)
            .Returns(Task.CompletedTask);
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Register successful with valid FullName, Email and Password
    [Fact]
    public async Task RegisterAsync_WithValidCredentials_ReturnsCreated()
    {
        var request = new RegisterRequestDto
        {
            FullName = "Phạm Công Minh",
            Email = "user1@ssam.com",
            Password = "Password@123"
        };
        SetupSuccessfulRegistration(generatedAccountId: 1);

        var result = await AuthService.RegisterAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.AccountId);
        Assert.Equal("user1@ssam.com", result.Data.Email);
        Assert.False(result.Data.EmailVerified);

        AuthRepositoryMock.Verify(r => r.AddAccountAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Once);
        AuthRepositoryMock.Verify(r => r.AddUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        AuthRepositoryMock.Verify(r => r.AddEmailVerificationTokenAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<CancellationToken>()), Times.Once);
        AuthRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        EmailServiceMock.Verify(e => e.SendVerificationEmailAsync("user1@ssam.com", "Phạm Công Minh", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC02: FullName contains only whitespace
    [Fact]
    public async Task RegisterAsync_WhenFullNameIsWhitespace_ReturnsValidationError()
    {
        var request = new RegisterRequestDto
        {
            FullName = "   ",
            Email = "user1@ssam.com",
            Password = "Password@123"
        };

        var result = await AuthService.RegisterAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("fullName", result.Errors!.Keys);
        Assert.Contains("Field fullname is required.", result.Errors["fullName"]);
    }

    // UTC03 + UTC04: Invalid Email (Whitespace only / Missing '@')
    [Theory]
    [InlineData("   ")]             // UTC03: Email contains only whitespace
    [InlineData("user1ssam.com")]   // UTC04: Email missing '@' character
    public async Task RegisterAsync_WhenEmailIsInvalid_ReturnsValidationError(string invalidEmail)
    {
        var request = new RegisterRequestDto
        {
            FullName = "Phạm Công Minh",
            Email = invalidEmail,
            Password = "Password@123"
        };

        var result = await AuthService.RegisterAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("email", result.Errors!.Keys);
        Assert.Contains("Email format is invalid.", result.Errors["email"]);
    }

    // UTC05: Email already exists in database
    [Fact]
    public async Task RegisterAsync_WhenEmailAlreadyExists_ReturnsConflict()
    {
        var request = new RegisterRequestDto
        {
            FullName = "Phạm Công Minh",
            Email = "user1@ssam.com",
            Password = "Password@123"
        };

        AuthRepositoryMock
            .Setup(r => r.EmailExistsAsync("user1@ssam.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await AuthService.RegisterAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("EMAIL_ALREADY_EXISTS", result.ErrorCode);
        Assert.Equal("Email already exists.", result.ErrorMessage);
    }

    // UTC06 -> UTC12: Invalid Password rules
    [Theory]
    [InlineData("        ", "Password must contain at least 8 characters.")]                    // UTC06: Password contains only whitespace
    [InlineData("Pa@1", "Password must contain at least 8 characters.")]                        // UTC07: Password less than 8 characters (4 chars)
    [InlineData("Password@abc", "Password must contain at least one digit.")]                   // UTC08: Password >= 8 chars missing digit
    [InlineData("password@123", "Password must contain both uppercase and lowercase letters.")] // UTC09: Password >= 8 chars missing uppercase
    [InlineData("PASSWORD@123", "Password must contain both uppercase and lowercase letters.")] // UTC10: Password >= 8 chars missing lowercase
    [InlineData("Password1234", "Password must contain at least one special character.")]       // UTC11: Password >= 8 chars missing special char
    public async Task RegisterAsync_WhenPasswordIsInvalid_ReturnsValidationError(string invalidPassword, string expectedError)
    {
        var request = new RegisterRequestDto
        {
            FullName = "Phạm Công Minh",
            Email = "user1@ssam.com",
            Password = invalidPassword
        };

        var result = await AuthService.RegisterAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("password", result.Errors!.Keys);
        Assert.Contains(expectedError, result.Errors["password"]);
    }

    // UTC12: Password >= 8 chars with lowercase only -> Returns all 3 password rule errors
    [Fact]
    public async Task RegisterAsync_WhenPasswordHasLowercaseOnly_ReturnsAllThreePasswordValidationErrors()
    {
        var request = new RegisterRequestDto
        {
            FullName = "Phạm Công Minh",
            Email = "user1@ssam.com",
            Password = "passwordonly"
        };

        var result = await AuthService.RegisterAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("password", result.Errors!.Keys);
        Assert.Equal(3, result.Errors["password"].Length);
        Assert.Contains("Password must contain at least one digit.", result.Errors["password"]);
        Assert.Contains("Password must contain both uppercase and lowercase letters.", result.Errors["password"]);
        Assert.Contains("Password must contain at least one special character.", result.Errors["password"]);
    }

    // UTC13: All 3 fields contain only whitespace
    [Fact]
    public async Task RegisterAsync_WhenAllFieldsAreWhitespace_ReturnsAllValidationErrors()
    {
        var request = new RegisterRequestDto
        {
            FullName = "   ",
            Email = "   ",
            Password = "   "
        };

        var result = await AuthService.RegisterAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal(3, result.Errors!.Count);
        Assert.Contains("fullName", result.Errors.Keys);
        Assert.Contains("email", result.Errors.Keys);
        Assert.Contains("password", result.Errors.Keys);
    }

    // =========================================================================
    // 3. BOUNDARY CASES (FullName & Email length: 1, 255, 256)
    // =========================================================================

    // UTC14 + UTC15: FullName boundary length 1 and 255 (trimmed) -> Success
    [Theory]
    [InlineData(1)]     // UTC14: FullName 1 character
    [InlineData(255)]   // UTC15: FullName 255 characters
    public async Task RegisterAsync_WhenFullNameLengthIsWithinBoundary_ReturnsCreated(int length)
    {
        var validFullName = new string('A', length);
        var request = new RegisterRequestDto
        {
            FullName = validFullName,
            Email = "user1@ssam.com",
            Password = "Password@123"
        };
        SetupSuccessfulRegistration(generatedAccountId: 1);

        var result = await AuthService.RegisterAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        AuthRepositoryMock.Verify(r => r.AddUserAsync(
            It.Is<User>(u => u.FullName == validFullName && u.FullName.Length == length),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC16: FullName boundary length 256 -> Throws DB Exception
    [Fact]
    public async Task RegisterAsync_WhenFullNameExceeds255Chars_ThrowsDbUpdateException()
    {
        var longFullName = new string('A', 256);
        var request = new RegisterRequestDto
        {
            FullName = longFullName,
            Email = "user1@ssam.com",
            Password = "Password@123"
        };
        SetupSuccessfulRegistration(generatedAccountId: 1);

        AuthRepositoryMock
            .SetupSequence(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .ThrowsAsync(new DbUpdateException("String or binary data would be truncated in table 'Users', column 'FullName'."));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            AuthService.RegisterAsync(request, TestContext.Current.CancellationToken));

        EmailServiceMock.Verify(
            e => e.SendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // UTC17 + UTC18: Email boundary length 1 ("@") and 255 (with "@") -> Success
    [Theory]
    [InlineData(1)]     // UTC17: Email 1 character ("@")
    [InlineData(255)]   // UTC18: Email 255 characters containing "@"
    public async Task RegisterAsync_WhenEmailLengthIsWithinBoundary_ReturnsCreated(int length)
    {
        var validEmail = length == 1
            ? "@"
            : new string('a', length - 9) + "@ssam.com";

        var request = new RegisterRequestDto
        {
            FullName = "Phạm Công Minh",
            Email = validEmail,
            Password = "Password@123"
        };
        SetupSuccessfulRegistration(generatedAccountId: 1);

        var result = await AuthService.RegisterAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(validEmail, result.Data!.Email);
        Assert.Equal(length, result.Data.Email.Length);
    }

    // UTC19: Email boundary length 256 (with "@") -> Throws DB Exception
    [Fact]
    public async Task RegisterAsync_WhenEmailExceeds255Chars_ThrowsDbUpdateException()
    {
        var longEmail = new string('a', 247) + "@ssam.com";
        var request = new RegisterRequestDto
        {
            FullName = "Phạm Công Minh",
            Email = longEmail,
            Password = "Password@123"
        };
        SetupSuccessfulRegistration(generatedAccountId: 1);

        AuthRepositoryMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("String or binary data would be truncated in table 'Accounts', column 'Email'."));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            AuthService.RegisterAsync(request, TestContext.Current.CancellationToken));

        EmailServiceMock.Verify(
            e => e.SendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // UTC20: Password boundary length 8 (valid rules) -> Success
    [Fact]
    public async Task RegisterAsync_WhenPasswordLengthIs8AndValid_ReturnsCreated()
    {
        var request = new RegisterRequestDto
        {
            FullName = "Phạm Công Minh",
            Email = "user1@ssam.com",
            Password = "Pass@123"
        };
        SetupSuccessfulRegistration(generatedAccountId: 1);

        var result = await AuthService.RegisterAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.AccountId);
    }

    // UTC21: Password boundary length 7 (valid rules but too short) -> Validation Error
    [Fact]
    public async Task RegisterAsync_WhenPasswordLengthIs7_ReturnsValidationError()
    {
        var request = new RegisterRequestDto
        {
            FullName = "Phạm Công Minh",
            Email = "user1@ssam.com",
            Password = "Pas@123"
        };

        var result = await AuthService.RegisterAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("password", result.Errors!.Keys);
        Assert.Single(result.Errors["password"]);
        Assert.Contains("Password must contain at least 8 characters.", result.Errors["password"]);
    }
}