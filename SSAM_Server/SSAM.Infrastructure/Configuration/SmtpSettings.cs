namespace SSAM.Infrastructure.Configuration;

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
}
