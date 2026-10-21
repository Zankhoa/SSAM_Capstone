using System.Text.Json.Serialization;
using SSAM_Server.DTOs.Common;

namespace SSAM_Server.DTOs.Workspaces;

public class WorkspaceDto
{
    [JsonPropertyName("workspaceId")]
    public long WorkspaceId { get; set; }

    // Included for backward compatibility with onboarding screens that read workspaceName
    [JsonPropertyName("workspaceName")]
    public string WorkspaceName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("companyId")]
    public long? CompanyId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "ACTIVE";

    [JsonPropertyName("myRole")]
    public RoleRefDto? MyRole { get; set; }

    [JsonPropertyName("myPermissions")]
    public List<string> MyPermissions { get; set; } = new();

    [JsonPropertyName("projectCount")]
    public int ProjectCount { get; set; }

    [JsonPropertyName("memberCount")]
    public int MemberCount { get; set; }

    [JsonPropertyName("createdBy")]
    public UserRefDto CreatedBy { get; set; } = null!;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("archivedAt")]
    public DateTime? ArchivedAt { get; set; }
}

public class CreateWorkspaceRequestDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("companyId")]
    public long? CompanyId { get; set; }
}

public class UpdateWorkspaceRequestDto
{
    private string? _description;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("description")]
    public string? Description
    {
        get => _description;
        set
        {
            _description = value;
            DescriptionSpecified = true;
        }
    }

    [JsonIgnore]
    public bool DescriptionSpecified { get; private set; }
}

public class WorkspaceCodeAvailabilityDto
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("available")]
    public bool Available { get; set; }

    [JsonPropertyName("suggestion")]
    public string? Suggestion { get; set; }
}

public class WorkspaceMemberDto : UserRefDto
{
    [JsonPropertyName("role")]
    public RoleRefDto Role { get; set; } = null!;

    [JsonPropertyName("companyRole")]
    public RoleRefDto CompanyRole { get; set; } = null!;

    [JsonPropertyName("joinedAt")]
    public DateTime JoinedAt { get; set; }
}

public class AddWorkspaceMemberItemDto
{
    [JsonPropertyName("userId")]
    public long UserId { get; set; }

    [JsonPropertyName("roleId")]
    public long? RoleId { get; set; }
}

public class AddWorkspaceMembersRequestDto
{
    [JsonPropertyName("members")]
    public List<AddWorkspaceMemberItemDto> Members { get; set; } = new();
}

public class UpdateWorkspaceMemberRoleRequestDto
{
    [JsonPropertyName("roleId")]
    public long RoleId { get; set; }
}
