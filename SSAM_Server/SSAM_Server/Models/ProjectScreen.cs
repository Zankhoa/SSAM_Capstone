namespace SSAM_Server.Models;

public class ProjectScreen : IHasUpdatedAt
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Kind { get; set; } = "SCREEN"; // SCREEN | LAYOUT
    public string? FigmaUrl { get; set; }

    public string? ImageFileName { get; set; }
    public string? ImageStoredName { get; set; }
    public string? ImageContentType { get; set; }

    public long? DocumentId { get; set; }
    public DocumentNode? Document { get; set; }

    public string? Notes { get; set; }

    public long CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
