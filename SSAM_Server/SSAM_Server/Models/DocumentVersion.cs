namespace SSAM_Server.Models;

public static class DocumentVersionStatuses
{
    public const string Draft = "DRAFT";
    public const string InReview = "IN_REVIEW";
    public const string ChangesRequested = "CHANGES_REQUESTED";
    public const string Approved = "APPROVED";
    public const string Superseded = "SUPERSEDED";

    public static bool IsValid(string? status) =>
        status is Draft or InReview or ChangesRequested or Approved or Superseded;
}

public class DocumentVersion : IHasUpdatedAt
{
    public long Id { get; set; }
    public long DocumentId { get; set; }
    public DocumentNode Document { get; set; } = null!;

    public string Label { get; set; } = "1.0";
    public string Status { get; set; } = DocumentVersionStatuses.Draft;
    public string? Subtitle { get; set; }
    public string Content { get; set; } = "{}";
    public string ContentText { get; set; } = string.Empty;

    public long? BaseVersionId { get; set; }
    public DocumentVersion? BaseVersion { get; set; }

    public int RowVersion { get; set; } = 1;

    public long UpdatedById { get; set; }
    public User UpdatedBy { get; set; } = null!;

    public long AuthorId { get; set; }
    public User Author { get; set; } = null!;

    public long? SubmittedById { get; set; }
    public User? SubmittedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }

    public long? ReviewedById { get; set; }
    public User? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RequirementItem> Requirements { get; set; } = new List<RequirementItem>();
}
