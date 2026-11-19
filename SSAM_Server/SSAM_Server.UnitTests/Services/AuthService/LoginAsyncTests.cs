using Microsoft.AspNetCore.Http;
using Moq;
using SSAM_Server.DTOs.Auth;
using SSAM_Server.Models;

namespace SSAM_Server.UnitTests.Services.AuthService;

public class LoginAsyncTests : AuthServiceTestBase
{
    // UTC01 + UTC02: Login successful.
    [Theory]
    [InlineData("user1@ssam.com", "123@123", 1, 1)] // UTC01
    [InlineData("user2@ssam.com", "234@234", 2, 3)] // UTC02
    public async Task LoginAsync_WithValidCredentials_ReturnsOkWithSession(string email, string password, int accountId, int userId)
    {
        var request = new LoginRequestDto { Email = email, Password = password };
        var account = new Account
        {
            Id = accountId,
            Email = request.Email,
            Password = "hashed_password",
            IsActive = true,
            EmailVerified = true
        };
        var user = new User
        {
            Id = userId,
            AccountId = accountId,
            FullName = $"Phạm Công Minh",
            Email = request.Email
        };

        AuthRepositoryMock
            .Setup(r => r.GetAccountByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        PasswordHasherMock
            .Setup(p => p.VerifyPassword(request.Password, account.Password))
            .Returns(true);

        AuthRepositoryMock
            .Setup(r => r.GetUserByAccountIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await AuthService.LoginAsync(request, null, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(user.Id, result.Data.User.Id);
        Assert.Equal(user.Email, result.Data.User.Email);

        AuthRepositoryMock.Verify(r => r.AddRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
        AuthRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC03: Email does not exist
    [Fact]
    public async Task LoginAsync_WhenEmailNotFound_ReturnsUnauthorized()
    {
        var request = new LoginRequestDto { Email = "user0@ssam.com", Password = "123@123" };
        AuthRepositoryMock
            .Setup(r => r.GetAccountByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        var result = await AuthService.LoginAsync(request, null, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("Invalid email or password.", result.ErrorMessage);
    }

    // UTC04 + UTC05: Email exists but password is invalid.
    [Theory]
    [InlineData("")]            // UTC04: Empty password
    [InlineData("234@234")]     // UTC05: Incorrect password
    public async Task LoginAsync_WhenInvalidPassword_ReturnsUnauthorized(string password)
    {
        var request = new LoginRequestDto { Email = "user1@ssam.com", Password = password };
        var account = new Account
        {
            Id = 1,
            Email = request.Email,
            Password = "hashed_password",
            IsActive = true,
            EmailVerified = true
        };

        AuthRepositoryMock
            .Setup(r => r.GetAccountByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        PasswordHasherMock
            .Setup(p => p.VerifyPassword(request.Password, account.Password))
            .Returns(false);

        var result = await AuthService.LoginAsync(request, null, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("Invalid email or password.", result.ErrorMessage);
    }

    // UTC06: Email is not verified
    [Fact]
    public async Task LoginAsync_WhenEmailNotVerified_ReturnsForbidden()
    {
        var request = new LoginRequestDto { Email = "user1@ssam.com", Password = "123@123" };

        var account = new Account
        {
            Id = 1,
            Email = request.Email,
            Password = "hashed_password",
            IsActive = true,
            EmailVerified = false
        };

        AuthRepositoryMock
            .Setup(r => r.GetAccountByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        PasswordHasherMock
            .Setup(p => p.VerifyPassword(request.Password, account.Password))
            .Returns(true);

        var result = await AuthService.LoginAsync(request, null, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("EMAIL_NOT_VERIFIED", result.ErrorCode);
        Assert.Equal("Your email address has not been verified.", result.ErrorMessage);
    }

    // UTC07: Account is not active
    [Fact]
    public async Task LoginAsync_WhenAccountNotActive_ReturnsForbidden()
    {
        var request = new LoginRequestDto { Email = "user1@ssam.com", Password = "123@123" };

        var account = new Account
        {
            Id = 1,
            Email = request.Email,
            Password = "hashed_password",
            IsActive = false,
            EmailVerified = true
        };

        AuthRepositoryMock
            .Setup(r => r.GetAccountByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        PasswordHasherMock
            .Setup(p => p.VerifyPassword(request.Password, account.Password))
            .Returns(true);

        var result = await AuthService.LoginAsync(request, null, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("ACCOUNT_DISABLED", result.ErrorCode);
        Assert.Equal("Your account has been disabled.", result.ErrorMessage);
    }

    // UTC08: Account doesn't have user information
    [Fact]
    public async Task LoginAsync_WhenUserNotFound_ReturnsNotFound()
    {
        var request = new LoginRequestDto { Email = "user1@ssam.com", Password = "123@123" };

        var account = new Account
        {
            Id = 1,
            Email = request.Email,
            Password = "hashed_password",
            IsActive = true,
            EmailVerified = true
        };

        AuthRepositoryMock
            .Setup(r => r.GetAccountByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        PasswordHasherMock
            .Setup(p => p.VerifyPassword(request.Password, account.Password))
            .Returns(true);

        AuthRepositoryMock
            .Setup(r => r.GetUserByAccountIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await AuthService.LoginAsync(request, null, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("User information could not be found.", result.ErrorMessage);
    }

}