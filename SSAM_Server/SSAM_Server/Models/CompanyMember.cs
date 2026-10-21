namespace SSAM_Server.Models;

public class CompanyMember
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public long RoleId { get; set; }
    public CompanyRole Role { get; set; } = null!;

    public bool IsActive { get; set; } = true;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
