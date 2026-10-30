using System.Security.Claims;

namespace SSAM_Server.Security;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    public long? UserIdOrNull
    {
        get
        {
            var principal = _accessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true) return null;

            var claim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("sub")?.Value;
            return long.TryParse(claim, out var id) ? id : null;
        }
    }

    public long UserId => UserIdOrNull ?? 0;
    public bool IsAuthenticated => UserIdOrNull.HasValue;
}
