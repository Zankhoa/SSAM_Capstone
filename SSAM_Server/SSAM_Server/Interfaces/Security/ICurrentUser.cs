namespace SSAM_Server.Interfaces.Security;

public interface ICurrentUser
{
    long? UserIdOrNull { get; }
    long UserId { get; }
    bool IsAuthenticated => UserIdOrNull.HasValue;
}
