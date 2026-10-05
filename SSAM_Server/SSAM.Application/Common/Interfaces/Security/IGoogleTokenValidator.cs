namespace SSAM.Application.Common.Interfaces.Security;

public record GoogleTokenPayload(string Subject, string Email, string FullName, bool EmailVerified);

public interface IGoogleTokenValidator
{
    Task<GoogleTokenPayload?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
