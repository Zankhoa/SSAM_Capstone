using Microsoft.AspNetCore.Http;
using Moq;
using SSAM_Server.Models;

namespace SSAM_Server.UnitTests.Services.AuthServiceTests;

public class VerifyEmailAsyncTests : AuthServiceTestBase
{
    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: Verify email succeeds with a valid, unexpired, and unused token.
    [Fact]
    public async Task VerifyEmailAsync_WithValidToken_UpdatesAccountAndReturnsOk()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var token = "valid_verification_token";
        var tokenEntity = new EmailVerificationToken
        {
            Id = 1,
            AccountId = 10,
            Token = token,
            ExpiresAt = now.AddHours(24),
            UsedAt = null,
            CreatedAt = now.AddMinutes(-5)
        };
        var account = new Account
        {
            Id = 10,
            Email = "user1@ssam.com",
            EmailVerified = false,
            IsActive = true
        };

        AuthRepositoryMock
            .Setup(r => r.GetEmailVerificationTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenEntity);

        AuthRepositoryMock
            .Setup(r => r.GetAccountByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await AuthService.VerifyEmailAsync(token, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Data);

        Assert.Equal(now, tokenEntity.UsedAt);
        Assert.True(account.EmailVerified);
        Assert.Equal(now, account.UpdatedAt);

        AuthRepositoryMock.Verify(r => r.UpdateEmailVerificationToken(tokenEntity), Times.Once);
        AuthRepositoryMock.Verify(r => r.UpdateAccount(account), Times.Once);
        AuthRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC002: A missing token returns BadRequest with TOKEN_INVALID.
    [Theory]
    [InlineData("non_existent_token")]  // UTC002: Non-existent token.
    public async Task VerifyEmailAsync_WhenTokenNotFound_ReturnsBadRequest(string invalidToken)
    {
        AuthRepositoryMock
            .Setup(r => r.GetEmailVerificationTokenAsync(invalidToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailVerificationToken?)null);

        var result = await AuthService.VerifyEmailAsync(invalidToken, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("TOKEN_INVALID", result.ErrorCode);
        Assert.Equal("The verification token is invalid.", result.ErrorMessage);

        AuthRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC003: An expired token returns BadRequest with TOKEN_EXPIRED.
    [Fact]
    public async Task VerifyEmailAsync_WhenTokenExpired_ReturnsBadRequest()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var token = "expired_token";
        var tokenEntity = new EmailVerificationToken
        {
            Id = 1,
            AccountId = 10,
            Token = token,
            ExpiresAt = now.AddMinutes(-1),
            UsedAt = null
        };

        AuthRepositoryMock
            .Setup(r => r.GetEmailVerificationTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenEntity);

        var result = await AuthService.VerifyEmailAsync(token, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("TOKEN_EXPIRED", result.ErrorCode);
        Assert.Equal("The verification token has expired.", result.ErrorMessage);

        AuthRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC004: An already-used token returns BadRequest with TOKEN_INVALID.
    [Fact]
    public async Task VerifyEmailAsync_WhenTokenAlreadyUsed_ReturnsBadRequest()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var token = "used_token";
        var tokenEntity = new EmailVerificationToken
        {
            Id = 1,
            AccountId = 10,
            Token = token,
            ExpiresAt = now.AddHours(12),
            UsedAt = now.AddMinutes(-10)
        };

        AuthRepositoryMock
            .Setup(r => r.GetEmailVerificationTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenEntity);

        var result = await AuthService.VerifyEmailAsync(token, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("TOKEN_INVALID", result.ErrorCode);
        Assert.Equal("The verification token has already been used.", result.ErrorMessage);

        AuthRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC005: A valid token with no associated account is marked used and returns Ok.
    [Fact]
    public async Task VerifyEmailAsync_WhenAccountNotFound_MarksTokenUsedAndReturnsOk()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var token = "valid_token_missing_account";
        var tokenEntity = new EmailVerificationToken
        {
            Id = 1,
            AccountId = 999,
            Token = token,
            ExpiresAt = now.AddHours(12),
            UsedAt = null
        };

        AuthRepositoryMock
            .Setup(r => r.GetEmailVerificationTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenEntity);

        AuthRepositoryMock
            .Setup(r => r.GetAccountByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        var result = await AuthService.VerifyEmailAsync(token, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(now, tokenEntity.UsedAt);

        AuthRepositoryMock.Verify(r => r.UpdateEmailVerificationToken(tokenEntity), Times.Once);
        AuthRepositoryMock.Verify(r => r.UpdateAccount(It.IsAny<Account>()), Times.Never);
        AuthRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // =========================================================================
    // 3. BOUNDARY CASES (Expiration timestamp: ExpiresAt <= now)
    // =========================================================================

    // UTC006: A token expiring at the current timestamp returns BadRequest with TOKEN_EXPIRED.
    [Fact]
    public async Task VerifyEmailAsync_WhenTokenExpiresExactlyNow_ReturnsBadRequest()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var token = "boundary_expired_now_token";
        var tokenEntity = new EmailVerificationToken
        {
            Id = 1,
            AccountId = 10,
            Token = token,
            ExpiresAt = now,
            UsedAt = null
        };

        AuthRepositoryMock
            .Setup(r => r.GetEmailVerificationTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenEntity);

        var result = await AuthService.VerifyEmailAsync(token, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("TOKEN_EXPIRED", result.ErrorCode);
        Assert.Equal("The verification token has expired.", result.ErrorMessage);
    }

    // UTC007: A token with one second remaining before expiration returns Ok.
    [Fact]
    public async Task VerifyEmailAsync_WhenTokenHasOneSecondRemaining_ReturnsOk()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var token = "boundary_valid_1s_token";
        var tokenEntity = new EmailVerificationToken
        {
            Id = 1,
            AccountId = 10,
            Token = token,
            ExpiresAt = now.AddSeconds(1),
            UsedAt = null
        };
        var account = new Account
        {
            Id = 10,
            Email = "user1@ssam.com",
            EmailVerified = false,
            IsActive = true
        };

        AuthRepositoryMock
            .Setup(r => r.GetEmailVerificationTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenEntity);

        AuthRepositoryMock
            .Setup(r => r.GetAccountByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await AuthService.VerifyEmailAsync(token, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.True(account.EmailVerified);
    }
}