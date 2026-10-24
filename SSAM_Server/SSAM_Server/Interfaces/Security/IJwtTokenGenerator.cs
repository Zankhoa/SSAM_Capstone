using SSAM_Server.Models;

namespace SSAM_Server.Interfaces.Security;

public interface IJwtTokenGenerator
{
    (string Token, int ExpiresIn) GenerateAccessToken(Account account, User user, IEnumerable<string>? roles = null);
    string GenerateRefreshToken();
    string HashRefreshToken(string refreshToken);
}
