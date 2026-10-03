using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using SSAM.Application.Common.Interfaces.Services;

namespace SSAM.Infrastructure.Email;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task SendEmailVerificationAsync(string toEmail, string token, CancellationToken cancellationToken = default)
    {
        var link = $"{FrontendBaseUrl}/verify-email?token={token}";
        return SendAsync(
            toEmail,
            "Xác thực email SSAM của bạn",
            $"<p>Chào bạn,</p><p>Bấm vào link bên dưới để xác thực email và hoàn tất đăng ký SSAM:</p><p><a href=\"{link}\">{link}</a></p><p>Link có hiệu lực trong 24 giờ.</p>",
            cancellationToken);
    }

    public Task SendPasswordResetAsync(string toEmail, string token, CancellationToken cancellationToken = default)
    {
        var link = $"{FrontendBaseUrl}/reset-password?token={token}";
        return SendAsync(
            toEmail,
            "Đặt lại mật khẩu SSAM",
            $"<p>Chào bạn,</p><p>Bấm vào link bên dưới để đặt lại mật khẩu:</p><p><a href=\"{link}\">{link}</a></p><p>Nếu bạn không yêu cầu việc này, hãy bỏ qua email này. Link có hiệu lực trong 2 giờ.</p>",
            cancellationToken);
    }

    private string FrontendBaseUrl => (_configuration["App:FrontendBaseUrl"] ?? "http://127.0.0.1:5174").TrimEnd('/');

    private async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var host = _configuration["Smtp:Host"];
        var username = _configuration["Smtp:Username"];
        var password = _configuration["Smtp:Password"];
        var fromName = _configuration["Smtp:FromName"] ?? "SSAM";
        var port = int.TryParse(_configuration["Smtp:Port"], out var p) ? p : 587;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("Smtp chưa được cấu hình đầy đủ — bỏ qua gửi email cho {Email}.", toEmail);
            return;
        }

        var fromEmail = _configuration["Smtp:FromEmail"] ?? username;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls, cancellationToken);
            await client.AuthenticateAsync(username, password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            _logger.LogInformation("Đã gửi email tới {Email}: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            // Gửi email thất bại không nên làm hỏng luồng nghiệp vụ chính (register/forgot-password đã
            // commit DB rồi) — chỉ log lại để debug, không throw ra ngoài AuthService.
            _logger.LogError(ex, "Gửi email tới {Email} thất bại.", toEmail);
        }
    }
}
