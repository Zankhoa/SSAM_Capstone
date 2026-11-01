namespace SSAM_Server.Models;

public static class DocumentNodeKinds
{
    public const string Folder = "FOLDER";
    public const string Page = "PAGE";

    public static bool IsValid(string? kind) =>
        kind is Folder or Page;
}

public static class DocumentTypes
{
    public const string Urd = "URD";
    public const string Brd = "BRD";
    public const string Srs = "SRS";
    public const string Adr = "ADR";
    public const string UiSpec = "UI_SPEC";
    public const string TestPlan = "TEST_PLAN";
    public const string Other = "OTHER";

    public static bool IsValid(string? docType) =>
        docType is Urd or Brd or Srs or Adr or UiSpec or TestPlan or Other;
}

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

public class DocumentNode : IHasUpdatedAt
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public long? ParentId { get; set; }
    public DocumentNode? Parent { get; set; }
    public ICollection<DocumentNode> Children { get; set; } = new List<DocumentNode>();

    public string Kind { get; set; } = DocumentNodeKinds.Page;
    public string Title { get; set; } = string.Empty;
    public string? DocType { get; set; }
    public string? Code { get; set; }
    public int SortOrder { get; set; } = 0;

    public long? CurrentVersionId { get; set; }
    public DocumentVersion? CurrentVersion { get; set; }

    public long? BaselineVersionId { get; set; }
    public DocumentVersion? BaselineVersion { get; set; }

    public long CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();
    public ICollection<DocumentComment> Comments { get; set; } = new List<DocumentComment>();
    public ICollection<DocumentActivity> Activities { get; set; } = new List<DocumentActivity>();
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

public class RequirementItem
{
    public long Id { get; set; }
    public long DocumentId { get; set; }
    public DocumentNode Document { get; set; } = null!;

    public long VersionId { get; set; }
    public DocumentVersion Version { get; set; } = null!;

    public string ReqUid { get; set; } = string.Empty;
    public string AnchorId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string BodyPreview { get; set; } = string.Empty;
    public int BodyLength { get; set; } = 0;
    public string AcceptanceCriteria { get; set; } = "[]";
    public bool HasTable { get; set; } = false;
    public bool HasDiagram { get; set; } = false;
    public bool HasFigma { get; set; } = false;
    public string ContentHash { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
}

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

public class DocumentCommentMention
{
    public long CommentId { get; set; }
    public DocumentComment Comment { get; set; } = null!;

    public long UserId { get; set; }
    public User User { get; set; } = null!;
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
