namespace SSAM_Server.Models;

public class Company : IHasUpdatedAt
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? EmailDomain { get; set; }
    public bool DomainAutoJoinEnabled { get; set; } = false;
    public string TaxCode { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public string CompanySize { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<CompanyMember> Members { get; set; } = new List<CompanyMember>();
    public ICollection<CompanyRole> Roles { get; set; } = new List<CompanyRole>();
}
