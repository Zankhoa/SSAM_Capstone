namespace SSAM_Server.Models;

public class MembershipRequest
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public long? UserId { get; set; }
    public User? User { get; set; }

    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // pending | approved | rejected
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
