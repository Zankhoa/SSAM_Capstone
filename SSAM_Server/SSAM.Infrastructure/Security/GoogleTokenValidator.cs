using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SSAM.Application.Common.Interfaces.Security;

namespace SSAM.Infrastructure.Security;

public class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleTokenValidator> _logger;

    public GoogleTokenValidator(IConfiguration configuration, ILogger<GoogleTokenValidator> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<GoogleTokenPayload?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var clientId = _configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogWarning("Google:ClientId chưa được cấu hình — không thể verify Google ID token.");
            return null;
        }

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId },
            };
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            return new GoogleTokenPayload(payload.Subject, payload.Email, payload.Name ?? payload.Email, payload.EmailVerified);
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(ex, "Google ID token không hợp lệ.");
            return null;
        }
    }
}
