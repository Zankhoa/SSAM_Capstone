namespace SSAM_Server.Models;

public class WorkspaceDepartment
{
    public long WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    public long DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
