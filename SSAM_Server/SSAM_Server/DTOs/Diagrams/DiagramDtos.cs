using System.Text.Json.Serialization;

namespace SSAM_Server.DTOs.Diagrams;

public class RenderDiagramRequest
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "plantuml";

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;
}

public class RenderDiagramResponse
{
    [JsonPropertyName("svg")]
    public string Svg { get; set; } = string.Empty;
}
