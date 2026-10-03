using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SSAM.Application.Common.Interfaces.Services;

namespace SSAM.Infrastructure.Email;

// Chưa có SMTP thật (chưa có creds) — in link ra console/log để test luồng local.
// Thay bằng implementation SMTP (MailKit...) khi có thông tin SMTP server thật.
public class ConsoleEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConsoleEmailSender> _logger;

    public ConsoleEmailSender(IConfiguration configuration, ILogger<ConsoleEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task SendEmailVerificationAsync(string toEmail, string token, CancellationToken cancellationToken = default)
    {
        var link = $"{FrontendBaseUrl}/verify-email?token={token}";
        _logger.LogWarning("[DEV EMAIL] Xác thực email cho {Email}: {Link}", toEmail, link);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string toEmail, string token, CancellationToken cancellationToken = default)
    {
        var link = $"{FrontendBaseUrl}/reset-password?token={token}";
        _logger.LogWarning("[DEV EMAIL] Đặt lại mật khẩu cho {Email}: {Link}", toEmail, link);
        return Task.CompletedTask;
    }

    private string FrontendBaseUrl => (_configuration["App:FrontendBaseUrl"] ?? "http://127.0.0.1:5174").TrimEnd('/');
}
