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

    // UTC001: Registration succeeds and sends an email-verification message when all credentials are valid.
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

    // UTC002: Registration trims surrounding whitespace before checking, storing, and emailing the user data.
    [Fact]
    public async Task RegisterAsync_TrimsFullNameAndEmailBeforePersistingAndSendingEmail()
    {
        var request = new RegisterRequestDto
        {
            FullName = "  Phạm Công Minh  ",
            Email = "  user1@ssam.com  ",
            Password = "Password@123"
        };
        SetupSuccessfulRegistration(generatedAccountId: 1);

        var result = await AuthService.RegisterAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("user1@ssam.com", result.Data!.Email);
        AuthRepositoryMock.Verify(r => r.EmailExistsAsync("user1@ssam.com", It.IsAny<CancellationToken>()), Times.Once);
        AuthRepositoryMock.Verify(r => r.AddUserAsync(
            It.Is<User>(u => u.Email == "user1@ssam.com" && u.FullName == "Phạm Công Minh"),
            It.IsAny<CancellationToken>()), Times.Once);
        EmailServiceMock.Verify(e => e.SendVerificationEmailAsync(
            "user1@ssam.com", "Phạm Công Minh", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC003: FullName contains only whitespace.
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

    // UTC004 + UTC005: Invalid email (whitespace only / missing '@').
    [Theory]
    [InlineData("   ")]             // UTC004: Email contains only whitespace.
    [InlineData("user1ssam.com")]   // UTC005: Email missing '@' character.
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

    // UTC006: Email already exists in the database.
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

    // Invalid password rules are covered by the ordered cases below.
    [Theory]
    [InlineData("        ", "Password must contain at least 8 characters.")]                    // UTC007: Password contains only whitespace.
    [InlineData("Pa@1", "Password must contain at least 8 characters.")]                        // UTC008: Password is shorter than 8 characters.
    [InlineData("Password@abc", "Password must contain at least one digit.")]                   // UTC009: Password has no digit.
    [InlineData("password@123", "Password must contain both uppercase and lowercase letters.")] // UTC010: Password has no uppercase letter.
    [InlineData("PASSWORD@123", "Password must contain both uppercase and lowercase letters.")] // UTC011: Password has no lowercase letter.
    [InlineData("Password1234", "Password must contain at least one special character.")]       // UTC012: Password has no special character.
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

    // UTC013: A lowercase-only password returns all three password-rule errors.
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

    // UTC014: All three registration fields contain only whitespace.
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

    // UTC014 + UTC015: FullName boundary lengths 1 and 255 return success.
    [Theory]
    [InlineData(1)]     // UTC014: FullName contains 1 character.
    [InlineData(255)]   // UTC015: FullName contains 255 characters.
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

    // UTC016: A 256-character full name propagates the database update exception.
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

    // UTC017 + UTC018: Email boundary lengths 1 and 255 return success.
    [Theory]
    [InlineData(1)]     // UTC017: Email contains one character ("@").
    [InlineData(255)]   // UTC018: Email contains 255 characters including "@".
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

    // UTC019: A 256-character email propagates the database update exception.
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

    // UTC020: An eight-character password satisfying all rules returns success.
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

    // UTC021: A seven-character password returns a validation error.
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