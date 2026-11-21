using System.Text.Json.Serialization;
using SSAM_Server.DTOs.Companies;

namespace SSAM_Server.DTOs.Common;

public class UserRefDto
{
    [JsonPropertyName("userId")]
    public long UserId { get; set; }

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("avatarUrl")]
    public string? AvatarUrl { get; set; }

    [JsonPropertyName("department")]
    public DepartmentRefDto? Department { get; set; }
}
