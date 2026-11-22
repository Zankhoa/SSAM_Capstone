using System.Text.Json.Serialization;

namespace SSAM_Server.DTOs.Designs;

public class ResolveDesignRequest
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public class ResolveDesignResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("thumbnailUrl")]
    public string? ThumbnailUrl { get; set; }
}

public class FigmaWebhookPayload
{
    [JsonPropertyName("event_type")]
    public string? EventType { get; set; }

    [JsonPropertyName("file_key")]
    public string? FileKey { get; set; }

    [JsonPropertyName("file_name")]
    public string? FileName { get; set; }

    [JsonPropertyName("passcode")]
    public string? Passcode { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }
}

public class FigmaCheckUpdatesResponse
{
    [JsonPropertyName("checkedCount")]
    public int CheckedCount { get; set; }

    [JsonPropertyName("changedFiles")]
    public List<string> ChangedFiles { get; set; } = new();

    [JsonPropertyName("flaggedTestCases")]
    public int FlaggedTestCases { get; set; }
}
