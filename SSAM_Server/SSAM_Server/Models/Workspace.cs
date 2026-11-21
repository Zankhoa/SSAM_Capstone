namespace SSAM_Server.Models;

public class Workspace : IHasUpdatedAt
{
    public long Id { get; set; }
    public long? CompanyId { get; set; }
    public Company? Company { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }

    public long CreatedBy { get; set; }
    public User Creator { get; set; } = null!;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }

    public ICollection<WorkspaceMember> Members { get; set; } = new List<WorkspaceMember>();
    public ICollection<WorkspaceDepartment> WorkspaceDepartments { get; set; } = new List<WorkspaceDepartment>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}

