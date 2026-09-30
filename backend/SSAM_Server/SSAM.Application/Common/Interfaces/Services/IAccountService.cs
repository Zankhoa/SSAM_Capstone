using SSAM.Application.DTOs.Auth;

namespace SSAM.Application.Common.Interfaces.Services;

public interface IAccountService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<bool> VerifyEmailAsync(string token, CancellationToken cancellationToken = default);
}
