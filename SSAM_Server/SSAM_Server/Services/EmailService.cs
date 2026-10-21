using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using SSAM_Server.Common.Options;

namespace SSAM_Server.Services;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string fullName, string token, CancellationToken cancellationToken = default);
    Task SendPasswordResetEmailAsync(string toEmail, string fullName, string token, CancellationToken cancellationToken = default);
    Task SendInvitationEmailAsync(string toEmail, string companyName, string token, CancellationToken cancellationToken = default);
}

public sealed record EmailJob(string Description, Func<IEmailService, CancellationToken, Task> Send);

public sealed class EmailQueue
{
    private readonly Channel<EmailJob> _channel = Channel.CreateBounded<EmailJob>(
        new BoundedChannelOptions(1000)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });

    public bool TryEnqueue(EmailJob job) => _channel.Writer.TryWrite(job);

    public IAsyncEnumerable<EmailJob> ReadAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}

public sealed class QueuedEmailService : IEmailService
{
    private readonly EmailQueue _queue;
    private readonly ILogger<QueuedEmailService> _logger;

    public QueuedEmailService(EmailQueue queue, ILogger<QueuedEmailService> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    public Task SendVerificationEmailAsync(string toEmail, string fullName, string token, CancellationToken cancellationToken = default)
        => Enqueue("verification", toEmail, (svc, ct) => svc.SendVerificationEmailAsync(toEmail, fullName, token, ct));

    public Task SendPasswordResetEmailAsync(string toEmail, string fullName, string token, CancellationToken cancellationToken = default)
        => Enqueue("password-reset", toEmail, (svc, ct) => svc.SendPasswordResetEmailAsync(toEmail, fullName, token, ct));

    public Task SendInvitationEmailAsync(string toEmail, string companyName, string token, CancellationToken cancellationToken = default)
        => Enqueue("invitation", toEmail, (svc, ct) => svc.SendInvitationEmailAsync(toEmail, companyName, token, ct));

    private Task Enqueue(string kind, string toEmail, Func<IEmailService, CancellationToken, Task> send)
    {
        if (!_queue.TryEnqueue(new EmailJob($"{kind} email", send)))
        {
            _logger.LogError("Email queue is full; dropped {Kind} email for {Email}.", kind, toEmail);
        }
        return Task.CompletedTask;
    }
}

public sealed class EmailDispatcherService : BackgroundService
{
    private static readonly TimeSpan[] RetryDelays =
    {
        TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)
    };

    private readonly EmailQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailDispatcherService> _logger;

    public EmailDispatcherService(EmailQueue queue, IServiceScopeFactory scopeFactory, ILogger<EmailDispatcherService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var job in _queue.ReadAllAsync(stoppingToken))
            {
                await ProcessAsync(job, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private async Task ProcessAsync(EmailJob job, CancellationToken stoppingToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var smtp = scope.ServiceProvider.GetRequiredService<SmtpEmailService>();
                await job.Send(smtp, stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt >= RetryDelays.Length)
                {
                    _logger.LogError(ex, "Giving up on {Job} after {Attempts} attempts.", job.Description, attempt + 1);
                    return;
                }

                _logger.LogWarning(ex, "Sending {Job} failed (attempt {Attempt}); retrying in {Delay}.",
                    job.Description, attempt + 1, RetryDelays[attempt]);
                await Task.Delay(RetryDelays[attempt], stoppingToken);
            }
        }
    }
}

public class SmtpEmailService : IEmailService
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<SmtpSettings> settings, ILogger<SmtpEmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendVerificationEmailAsync(string toEmail, string fullName, string token, CancellationToken cancellationToken = default)
    {
        var baseUrl = (_settings.FrontendBaseUrl ?? "http://localhost:5174").TrimEnd('/');
        var verifyUrl = $"{baseUrl}/verify-email?token={token}";

        LogLink("VERIFY EMAIL", toEmail, verifyUrl);

        var subject = "[SSAM] Xác thực địa chỉ email tài khoản của bạn";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f4f5f7; margin: 0; padding: 24px; }}
        .card {{ max-width: 520px; margin: 0 auto; background: #ffffff; border-radius: 12px; padding: 32px; box-shadow: 0 4px 12px rgba(0,0,0,0.06); }}
        .title {{ font-size: 20px; font-weight: 700; color: #111827; margin-bottom: 12px; }}
        .text {{ font-size: 15px; color: #374151; line-height: 1.6; margin-bottom: 24px; }}
        .button {{ display: inline-block; background-color: #2563eb; color: #ffffff !important; padding: 12px 28px; font-size: 15px; font-weight: 600; text-decoration: none; border-radius: 8px; text-align: center; }}
        .footer {{ font-size: 13px; color: #6b7280; margin-top: 32px; border-top: 1px solid #e5e7eb; padding-top: 16px; word-break: break-all; }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='title'>Xác thực tài khoản SSAM</div>
        <p class='text'>Xin chào <strong>{WebUtility.HtmlEncode(fullName)}</strong>,<br/><br/>
        Cảm ơn bạn đã đăng ký tài khoản trên nền tảng SSAM. Vui lòng bấm vào nút bên dưới để hoàn tất việc xác thực địa chỉ email:</p>
        <div style='text-align: center; margin: 28px 0;'>
            <a href='{verifyUrl}' class='button'>Xác thực email ngay</a>
        </div>
        <p class='text'>Liên kết này sẽ có hiệu lực trong vòng 24 giờ. Nếu bạn không tạo tài khoản này, vui lòng bỏ qua email.</p>
        <div class='footer'>
            Hoặc bạn có thể dán đường dẫn sau vào trình duyệt:<br/>
            <a href='{verifyUrl}' style='color: #2563eb;'>{verifyUrl}</a>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string fullName, string token, CancellationToken cancellationToken = default)
    {
        var baseUrl = (_settings.FrontendBaseUrl ?? "http://localhost:5174").TrimEnd('/');
        var resetUrl = $"{baseUrl}/reset-password?token={token}";

        LogLink("PASSWORD RESET", toEmail, resetUrl);

        var subject = "[SSAM] Yêu cầu đặt lại mật khẩu";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f4f5f7; margin: 0; padding: 24px; }}
        .card {{ max-width: 520px; margin: 0 auto; background: #ffffff; border-radius: 12px; padding: 32px; box-shadow: 0 4px 12px rgba(0,0,0,0.06); }}
        .title {{ font-size: 20px; font-weight: 700; color: #111827; margin-bottom: 12px; }}
        .text {{ font-size: 15px; color: #374151; line-height: 1.6; margin-bottom: 24px; }}
        .button {{ display: inline-block; background-color: #ef4444; color: #ffffff !important; padding: 12px 28px; font-size: 15px; font-weight: 600; text-decoration: none; border-radius: 8px; text-align: center; }}
        .footer {{ font-size: 13px; color: #6b7280; margin-top: 32px; border-top: 1px solid #e5e7eb; padding-top: 16px; word-break: break-all; }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='title'>Đặt lại mật khẩu SSAM</div>
        <p class='text'>Xin chào <strong>{WebUtility.HtmlEncode(fullName)}</strong>,<br/><br/>
        Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Bấm vào nút bên dưới để tiến hành thiết lập mật khẩu mới:</p>
        <div style='text-align: center; margin: 28px 0;'>
            <a href='{resetUrl}' class='button'>Đặt lại mật khẩu</a>
        </div>
        <p class='text'>Liên kết này sẽ hết hạn sau 2 giờ. Nếu bạn không yêu cầu hành động này, vui lòng bỏ qua email.</p>
        <div class='footer'>
            Hoặc dán link sau vào trình duyệt:<br/>
            <a href='{resetUrl}' style='color: #ef4444;'>{resetUrl}</a>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    public async Task SendInvitationEmailAsync(string toEmail, string companyName, string token, CancellationToken cancellationToken = default)
    {
        var baseUrl = (_settings.FrontendBaseUrl ?? "http://localhost:5174").TrimEnd('/');
        var inviteUrl = $"{baseUrl}/invite/{token}";

        LogLink("INVITATION", toEmail, inviteUrl);

        var subject = $"[SSAM] Lời mời tham gia tổ chức {companyName}";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f4f5f7; margin: 0; padding: 24px; }}
        .card {{ max-width: 520px; margin: 0 auto; background: #ffffff; border-radius: 12px; padding: 32px; box-shadow: 0 4px 12px rgba(0,0,0,0.06); }}
        .title {{ font-size: 20px; font-weight: 700; color: #111827; margin-bottom: 12px; }}
        .text {{ font-size: 15px; color: #374151; line-height: 1.6; margin-bottom: 24px; }}
        .button {{ display: inline-block; background-color: #10b981; color: #ffffff !important; padding: 12px 28px; font-size: 15px; font-weight: 600; text-decoration: none; border-radius: 8px; text-align: center; }}
        .footer {{ font-size: 13px; color: #6b7280; margin-top: 32px; border-top: 1px solid #e5e7eb; padding-top: 16px; word-break: break-all; }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='title'>Lời mời gia nhập {WebUtility.HtmlEncode(companyName)}</div>
        <p class='text'>Bạn đã được mời tham gia tổ chức <strong>{WebUtility.HtmlEncode(companyName)}</strong> trên nền tảng SSAM.<br/><br/>
        Bấm nút bên dưới để chấp nhận lời mời:</p>
        <div style='text-align: center; margin: 28px 0;'>
            <a href='{inviteUrl}' class='button'>Chấp nhận lời mời</a>
        </div>
        <div class='footer'>
            Hoặc dán liên kết sau vào trình duyệt:<br/>
            <a href='{inviteUrl}' style='color: #10b981;'>{inviteUrl}</a>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    private void LogLink(string kind, string toEmail, string url)
    {
        if (_settings.LogLinks)
        {
            _logger.LogInformation("[{Kind} LINK] for {Email}: {Url}", kind, toEmail, url);
        }
    }

    private async Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.SenderEmail) || string.IsNullOrWhiteSpace(_settings.Password))
        {
            _logger.LogWarning("[SMTP WARNING] SenderEmail hoặc Password chưa được cấu hình trong appsettings.json. Bỏ qua việc gửi mail qua Internet.");
            return;
        }

        try
        {
            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                Credentials = new NetworkCredential(_settings.SenderEmail, _settings.Password)
            };

            using var message = new MailMessage
            {
                From = new MailAddress(_settings.SenderEmail, _settings.SenderName, Encoding.UTF8),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };

            message.To.Add(toEmail);

            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("[SMTP SUCCESS] Đã gửi email thành công tới {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SMTP ERROR] Không thể gửi email tới {Email}. Nguyên nhân: {Message}", toEmail, ex.Message);
            throw;
        }
    }
}
