namespace SSAM_Server.Interfaces.Security;

public record GoogleTokenPayload(string Subject, string Email, string FullName, bool EmailVerified);
