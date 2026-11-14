using System.Text.Json.Serialization;

namespace SSAM_Server.DTOs.Common;

public class VersionConflictCurrentDto
{
    [JsonPropertyName("rowVersion")]
    public int RowVersion { get; set; }

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;

    [JsonPropertyName("updatedBy")]
    public UserLiteDto UpdatedBy { get; set; } = new();
}
