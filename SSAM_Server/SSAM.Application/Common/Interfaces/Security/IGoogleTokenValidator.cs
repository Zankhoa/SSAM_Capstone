namespace SSAM.Application.Common.Interfaces.Security;

public record GoogleTokenPayload(string Subject, string Email, string FullName, bool EmailVerified);

public interface IGoogleTokenValidator
{
    // Trả null nếu idToken không hợp lệ/hết hạn/audience sai — caller tự quyết định throw lỗi gì.
    Task<GoogleTokenPayload?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
