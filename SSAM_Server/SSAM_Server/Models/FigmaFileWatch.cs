namespace SSAM_Server.Models;

public class FigmaFileWatch
{
    public long Id { get; set; }
    public string FileKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? LastVersion { get; set; }
    public DateTime? LastModified { get; set; }
    public DateTime? LastCheckedAt { get; set; }
    public DateTime? LastChangedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
