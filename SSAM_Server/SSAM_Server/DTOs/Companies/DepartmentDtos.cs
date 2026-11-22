using System.Text.Json.Serialization;
using SSAM_Server.DTOs.Common;

namespace SSAM_Server.DTOs.Companies;

public class DepartmentRefDto
{
    [JsonPropertyName("departmentId")]
    public long DepartmentId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string? Code { get; set; }
}

public class DepartmentDto : DepartmentRefDto
{
    [JsonPropertyName("companyId")]
    public long CompanyId { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("memberCount")]
    public int MemberCount { get; set; }

    [JsonPropertyName("members")]
    public List<UserRefDto> Members { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public class CreateDepartmentRequestDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class UpdateDepartmentRequestDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("isActive")]
    public bool? IsActive { get; set; }
}

public class AssignDepartmentMemberRequestDto
{
    [JsonPropertyName("userId")]
    public long UserId { get; set; }
}
