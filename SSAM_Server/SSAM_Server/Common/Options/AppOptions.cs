using System.ComponentModel.DataAnnotations;

namespace SSAM_Server.Common.Options;

public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    [Required]
    [MinLength(32)]
    public string Secret { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = "SSAM_Server";

    [Required]
    public string Audience { get; set; } = "SSAM_Client";

    [Range(1, 1440)]
    public int ExpiryMinutes { get; set; } = 15;

    [Range(1, 365)]
    public int RefreshTokenExpiryDays { get; set; } = 7;
}

public class ClientAppOptions
{
    public const string SectionName = "ClientApp";

    public string FrontendBaseUrl { get; set; } = "http://localhost:5174";
}

public class SmtpSettings
{
    public const string SectionName = "SmtpSettings";

    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = "SSAM Platform";
    public string Password { get; set; } = string.Empty;
    public string FrontendBaseUrl { get; set; } = "http://localhost:5174";
    public bool LogLinks { get; set; } = false;
}
