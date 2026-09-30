namespace SSAM.Domain.Entities.Identity;

public class ExternalLogin
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public string Provider { get; set; } = string.Empty;
    public string ProviderUserId { get; set; } = string.Empty;
    public string? EmailAtProvider { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
