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

    public string RequiredApprovers { get; set; } = "[]";

    public long CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();
    public ICollection<DocumentComment> Comments { get; set; } = new List<DocumentComment>();
    public ICollection<DocumentActivity> Activities { get; set; } = new List<DocumentActivity>();
}
