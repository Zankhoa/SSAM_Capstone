namespace SSAM_Server.Models;

public static class DocumentActivityActions
{
    public const string Created = "CREATED";
    public const string Deleted = "DELETED";
    public const string GeneratedFromNotes = "GENERATED_FROM_NOTES";
    public const string ContentSaved = "CONTENT_SAVED";
    public const string Submitted = "SUBMITTED";
    public const string Approved = "APPROVED";
    public const string ChangesRequested = "CHANGES_REQUESTED";
    public const string RevisionCreated = "REVISION_CREATED";
    public const string Renamed = "RENAMED";
    public const string Moved = "MOVED";
    public const string ReqConfirmedFresh = "REQ_CONFIRMED_FRESH";
    public const string Commented = "COMMENTED";
}

public class DocumentActivity
{
    public long Id { get; set; }
    public long DocumentId { get; set; }
    public DocumentNode Document { get; set; } = null!;

    public long? VersionId { get; set; }
    public DocumentVersion? Version { get; set; }

    public string Action { get; set; } = string.Empty;
    public long ActorId { get; set; }
    public User Actor { get; set; } = null!;

    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
