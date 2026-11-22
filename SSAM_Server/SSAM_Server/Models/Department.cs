namespace SSAM_Server.Models;

public class Department : IHasUpdatedAt
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CompanyMember> Members { get; set; } = new List<CompanyMember>();
}
