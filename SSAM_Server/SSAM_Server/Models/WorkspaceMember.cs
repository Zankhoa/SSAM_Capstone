namespace SSAM_Server.Models;

public class WorkspaceMember
{
    public long Id { get; set; }
    public long WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public long? RoleId { get; set; }
    public CompanyRole? Role { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

