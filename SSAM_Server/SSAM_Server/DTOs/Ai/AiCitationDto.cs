namespace SSAM_Server.DTOs.Ai;

/// <summary>A validated reference to a project record used in an AI response.</summary>
public sealed record AiCitationDto
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public long? DocumentId { get; init; }
    public long? VersionId { get; init; }
    public long? RequirementId { get; init; }
    public string? ReqUid { get; init; }
    public string? AnchorId { get; init; }
    public long? IssueId { get; init; }
    public long? TestCaseId { get; init; }
    public long? PlanId { get; init; }
    public string? TargetReference { get; init; }
}
