namespace SSAM_Server.Common.Options;

public class FigmaOptions
{
    public const string SectionName = "Figma";

    public string? AccessToken { get; set; }
    public string? WebhookPasscode { get; set; }
    public int PollingIntervalSeconds { get; set; } = 300;
    public int TimeoutSeconds { get; set; } = 10;
}
