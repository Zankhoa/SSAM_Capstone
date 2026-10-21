namespace SSAM_Server.Models;

public class MembershipRequest
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public long? UserId { get; set; }
    public User? User { get; set; }

    public string Email { get; set; } = string.Empty;
    public string Source { get; set; } = "join_request"; // join_request | admin_invite
    public string? RoleName { get; set; }

    public long? RoleId { get; set; }
    public CompanyRole? Role { get; set; }

    public long? WorkspaceId { get; set; }
    public Workspace? Workspace { get; set; }

    public long? WorkspaceRoleId { get; set; }
    public CompanyRole? WorkspaceRole { get; set; }

    public string? Token { get; set; }
    public string Status { get; set; } = "pending"; // pending | approved | rejected | accepted | expired
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

