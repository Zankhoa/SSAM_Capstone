using System.Text.Json.Serialization;
using SSAM_Server.Security;

namespace SSAM_Server.DTOs.Common;

public class RoleRefDto
{
    [JsonPropertyName("roleId")]
    public long RoleId { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = RoleKinds.Other;
}
