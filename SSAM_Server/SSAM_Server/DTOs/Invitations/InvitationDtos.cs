using System.Text.Json.Serialization;

namespace SSAM_Server.DTOs.Invitations;

public class CreateInvitationRequestDto
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("roleId")]
    public long? RoleId { get; set; }

    [JsonPropertyName("workspaceId")]
    public long? WorkspaceId { get; set; }

    [JsonPropertyName("workspaceRoleId")]
    public long? WorkspaceRoleId { get; set; }

    [JsonPropertyName("departmentId")]
    public long? DepartmentId { get; set; }

    [JsonPropertyName("roleName")]
    public string? RoleName { get; set; }
}

public class CreateInvitationResponseDto
{
    [JsonPropertyName("invitationId")]
    public long InvitationId { get; set; }

    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("inviteUrl")]
    public string InviteUrl { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }
}

public class InvitationDetailsResponseDto
{
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; set; }

    [JsonPropertyName("email")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Email { get; set; }

    [JsonPropertyName("companyName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CompanyName { get; set; }

    [JsonPropertyName("workspaceName")]
    public string? WorkspaceName { get; set; }

    [JsonPropertyName("roleName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RoleName { get; set; }

    [JsonPropertyName("expiresAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? ExpiresAt { get; set; }
}

public class AcceptInvitationRequestDto
{
    [JsonPropertyName("fullName")]
    public string? FullName { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }
}

public class AcceptInvitationResponseDto
{
    [JsonPropertyName("accessToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refreshToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expiresIn")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ExpiresIn { get; set; }

    [JsonPropertyName("companyId")]
    public long CompanyId { get; set; }

    [JsonPropertyName("workspaceId")]
    public long? WorkspaceId { get; set; }
}
