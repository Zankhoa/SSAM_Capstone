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
