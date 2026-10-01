using SSAM.Domain.Entities.Companies;

namespace SSAM.Domain.Entities.Workspaces;

public class Workspace
{
    public long Id { get; set; }
    public long? CompanyId { get; set; }
    public Company? Company { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPersonal { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WorkspaceMember> Members { get; set; } = new List<WorkspaceMember>();
}
