namespace SSAM_Server.Configurations;

public class AiOptions
{
    public const string SectionName = "AI";

    public string Provider { get; set; } = "Anthropic";
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string DefaultModel { get; set; } = "claude-sonnet-5-5";
}