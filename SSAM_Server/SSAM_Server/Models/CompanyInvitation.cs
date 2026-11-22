namespace SSAM_Server.Models;

public class CompanyInvitation
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public string Email { get; set; } = string.Empty;
    public string? RoleName { get; set; }

    public long? RoleId { get; set; }
    public CompanyRole? Role { get; set; }

    public long? WorkspaceId { get; set; }
    public Workspace? Workspace { get; set; }

    public long? WorkspaceRoleId { get; set; }
    public CompanyRole? WorkspaceRole { get; set; }

    public long? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public string Token { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // pending | accepted | expired
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
