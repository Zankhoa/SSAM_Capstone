namespace SSAM_Server.Models;

public class DocumentGeneration
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public long? DocumentId { get; set; }
    public DocumentNode? Document { get; set; }

    public string RawNotes { get; set; } = string.Empty;
    public string Fields { get; set; } = "{}";
    public string Items { get; set; } = "[]";

    public long CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
