namespace SSAM.Application.Common.Interfaces.Services;

public interface IEmailSender
{
    Task SendEmailVerificationAsync(string toEmail, string token, CancellationToken cancellationToken = default);
    Task SendPasswordResetAsync(string toEmail, string token, CancellationToken cancellationToken = default);
}
