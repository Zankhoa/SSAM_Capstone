using SSAM.Application.DTOs.Auth;

namespace SSAM.Application.Common.Interfaces.Services;

public interface IAuthService
{
    Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthSessionDto> LoginAsync(LoginRequestDto request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
    Task<AuthSessionDto> GoogleLoginAsync(GoogleLoginRequestDto request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
    Task<TokenRefreshResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
    Task LogoutAsync(LogoutRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthMeResponseDto> GetMeAsync(long userId, CancellationToken cancellationToken = default);
    Task<bool> VerifyEmailAsync(string token, CancellationToken cancellationToken = default);
    Task ResendEmailVerificationAsync(string email, CancellationToken cancellationToken = default);
    Task ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default);
}
