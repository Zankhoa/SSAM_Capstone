using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SSAM_Server.Common.Options;
using SSAM_Server.Models;

namespace SSAM_Server.Security;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _settings;
    private readonly TimeProvider _clock;
    private readonly SigningCredentials _credentials;

    public JwtTokenGenerator(IOptions<JwtSettings> settings, TimeProvider clock)
    {
        _settings = settings.Value;
        _clock = clock;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public (string Token, int ExpiresIn) GenerateAccessToken(Account account, User user, IEnumerable<string>? roles = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("accountId", account.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, account.Email),
            new("fullName", user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (roles != null)
        {
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        }

        var expiresInSeconds = _settings.ExpiryMinutes * 60;
        var now = _clock.GetUtcNow().UtcDateTime;

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = now,
            Expires = now.AddSeconds(expiresInSeconds),
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            SigningCredentials = _credentials
        };

        var handler = new JwtSecurityTokenHandler();
        return (handler.WriteToken(handler.CreateToken(tokenDescriptor)), expiresInSeconds);
    }

    public string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    public string HashRefreshToken(string refreshToken)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
    }
}
