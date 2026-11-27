namespace SSAM_Server.Models;

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
    public string? Actor { get; set; }
    public string? Preconditions { get; set; }
    public string Constraints { get; set; } = "[]";
    public string AcceptanceCriteria { get; set; } = "[]";
    public bool HasTable { get; set; } = false;
    public bool HasDiagram { get; set; } = false;
    public bool HasFigma { get; set; } = false;
    public string ContentHash { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
}
