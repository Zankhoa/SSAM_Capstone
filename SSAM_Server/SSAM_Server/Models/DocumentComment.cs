namespace SSAM_Server.Models;

public class DocumentComment
{
    public long Id { get; set; }
    public long DocumentId { get; set; }
    public DocumentNode Document { get; set; } = null!;

    public long VersionId { get; set; }
    public DocumentVersion Version { get; set; } = null!;

    public long? ParentId { get; set; }
    public DocumentComment? Parent { get; set; }
    public ICollection<DocumentComment> Replies { get; set; } = new List<DocumentComment>();

    public string? AnchorId { get; set; }
    public string? Quote { get; set; }
    public string Body { get; set; } = string.Empty;

    public long AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public long? ResolvedById { get; set; }
    public User? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public ICollection<DocumentCommentMention> Mentions { get; set; } = new List<DocumentCommentMention>();
}
