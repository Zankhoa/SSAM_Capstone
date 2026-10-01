using SSAM.Domain.Entities.Identity;

namespace SSAM.Application.Common.Interfaces.Security;

public interface IJwtTokenGenerator
{
    (string Token, int ExpiresIn) GenerateAccessToken(Account account, User user, IEnumerable<string>? roles = null);
    string GenerateRefreshToken();
    string HashRefreshToken(string refreshToken);
}
