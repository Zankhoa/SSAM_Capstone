using SSAM.Domain.Entities.Identity;

namespace SSAM.Domain.Entities.Workspaces;

public class WorkspaceMember
{
    public long Id { get; set; }
    public long WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public long? RoleId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
