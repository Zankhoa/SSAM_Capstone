namespace SSAM.Application.Features.Emails;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string fullName, string token, CancellationToken cancellationToken = default);
    Task SendPasswordResetEmailAsync(string toEmail, string fullName, string token, CancellationToken cancellationToken = default);
    Task SendInvitationEmailAsync(string toEmail, string companyName, string token, CancellationToken cancellationToken = default);
}
