using SSAM_Server.Common;
using SSAM_Server.DTOs.Auth;

namespace SSAM_Server.Interfaces.Services;

public interface IAuthService
{
    Task<ServiceResult<RegisterResponseDto>> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<AuthSessionDto>> LoginAsync(LoginRequestDto request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
    Task<ServiceResult<AuthSessionDto>> GoogleLoginAsync(GoogleLoginRequestDto request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
    Task<ServiceResult<TokenRefreshResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
    Task<ServiceResult> LogoutAsync(LogoutRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<AuthMeResponseDto>> GetMeAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<object>> VerifyEmailAsync(string token, CancellationToken cancellationToken = default);
    Task<ServiceResult> ResendEmailVerificationAsync(string email, CancellationToken cancellationToken = default);
    Task<ServiceResult> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);
    Task<ServiceResult<object>> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default);
}
