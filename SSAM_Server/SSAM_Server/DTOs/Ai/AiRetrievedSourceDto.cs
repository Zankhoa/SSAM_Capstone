namespace SSAM_Server.DTOs.Ai;

public sealed record AiRetrievedSourceDto
{
    public string CitationKey { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public long? DocumentId { get; set; }
    public long? VersionId { get; set; }
    public long? RequirementId { get; set; }
    public string? ReqUid { get; set; }
    public string? AnchorId { get; set; }
    public long? IssueId { get; set; }
    public long? TestCaseId { get; set; }
    public long? PlanId { get; set; }
    public string? TargetReference { get; set; }
}
