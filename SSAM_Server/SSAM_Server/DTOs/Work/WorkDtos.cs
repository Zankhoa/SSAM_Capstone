using System.Text.Json.Serialization;
using SSAM_Server.DTOs.Common;

namespace SSAM_Server.DTOs.Work;

public class DocumentWorkItemDto
{
    [JsonPropertyName("documentId")]
    public long DocumentId { get; set; }

    [JsonPropertyName("projectId")]
    public long ProjectId { get; set; }

    [JsonPropertyName("projectKey")]
    public string ProjectKey { get; set; } = string.Empty;

    [JsonPropertyName("projectName")]
    public string ProjectName { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("docType")]
    public string DocType { get; set; } = string.Empty;

    [JsonPropertyName("versionId")]
    public long VersionId { get; set; }

    [JsonPropertyName("versionLabel")]
    public string VersionLabel { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("reqCount")]
    public int ReqCount { get; set; }

    [JsonPropertyName("unresolvedCommentCount")]
    public int UnresolvedCommentCount { get; set; }

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;

    [JsonPropertyName("at")]
    public string At => UpdatedAt;

    [JsonPropertyName("urgency")]
    public int Urgency { get; set; } = 0;

    [JsonPropertyName("actor")]
    public UserRefDto Actor { get; set; } = new();

    [JsonPropertyName("author")]
    public UserRefDto Author => Actor;

    [JsonPropertyName("submittedBy")]
    public UserRefDto? SubmittedBy => Actor;

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("changeSummary")]
    public string? ChangeSummary => Note;
}

public class StaleRequirementItemDto
{
    [JsonPropertyName("requirementId")]
    public long RequirementId { get; set; }

    [JsonPropertyName("reqUid")]
    public string ReqUid { get; set; } = string.Empty;

    [JsonPropertyName("reqKey")]
    public string ReqKey => ReqUid.StartsWith("REQ-", StringComparison.OrdinalIgnoreCase) ? ReqUid : $"REQ-{RequirementId}";

    [JsonPropertyName("reqTitle")]
    public string ReqTitle { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title => ReqTitle;

    [JsonPropertyName("anchorId")]
    public string AnchorId { get; set; } = string.Empty;

    [JsonPropertyName("documentId")]
    public long DocumentId { get; set; }

    [JsonPropertyName("docCode")]
    public string? DocCode { get; set; }

    [JsonPropertyName("code")]
    public string? Code => DocCode;

    [JsonPropertyName("docTitle")]
    public string DocTitle { get; set; } = string.Empty;

    [JsonPropertyName("documentTitle")]
    public string DocumentTitle => DocTitle;

    [JsonPropertyName("versionId")]
    public long VersionId { get; set; }

    [JsonPropertyName("versionLabel")]
    public string VersionLabel { get; set; } = string.Empty;

    [JsonPropertyName("projectId")]
    public long ProjectId { get; set; }

    [JsonPropertyName("projectKey")]
    public string ProjectKey { get; set; } = string.Empty;

    [JsonPropertyName("projectName")]
    public string ProjectName { get; set; } = string.Empty;

    [JsonPropertyName("changeKind")]
    public string ChangeKind { get; set; } = string.Empty;

    [JsonPropertyName("beforePreview")]
    public string? BeforePreview { get; set; }

    [JsonPropertyName("afterPreview")]
    public string AfterPreview { get; set; } = string.Empty;

    [JsonPropertyName("changedBy")]
    public UserRefDto ChangedBy { get; set; } = new();

    [JsonPropertyName("changedAt")]
    public string ChangedAt { get; set; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt => ChangedAt;
}

public class DocumentWorkEventDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("documentId")]
    public long DocumentId { get; set; }

    [JsonPropertyName("projectId")]
    public long ProjectId { get; set; }

    [JsonPropertyName("projectKey")]
    public string ProjectKey { get; set; } = string.Empty;

    [JsonPropertyName("projectName")]
    public string ProjectName { get; set; } = string.Empty;

    [JsonPropertyName("docCode")]
    public string? DocCode { get; set; }

    [JsonPropertyName("code")]
    public string? Code => DocCode;

    [JsonPropertyName("docTitle")]
    public string DocTitle { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title => DocTitle;

    [JsonPropertyName("versionId")]
    public long? VersionId { get; set; }

    [JsonPropertyName("versionLabel")]
    public string? VersionLabel { get; set; }

    [JsonPropertyName("anchorId")]
    public string? AnchorId { get; set; }

    [JsonPropertyName("commentId")]
    public long? CommentId { get; set; }

    [JsonPropertyName("actor")]
    public UserRefDto Actor { get; set; } = new();

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("excerpt")]
    public string? Excerpt { get; set; }

    [JsonPropertyName("note")]
    public string? Note => Excerpt ?? Summary;

    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
}

public class ProjectDocumentStatsDto
{
    [JsonPropertyName("projectId")]
    public long ProjectId { get; set; }

    [JsonPropertyName("projectKey")]
    public string ProjectKey { get; set; } = string.Empty;

    [JsonPropertyName("projectName")]
    public string ProjectName { get; set; } = string.Empty;

    [JsonPropertyName("totalDocs")]
    public int TotalDocs { get; set; }

    [JsonPropertyName("pages")]
    public int Pages => TotalDocs;

    [JsonPropertyName("baselinedDocs")]
    public int BaselinedDocs { get; set; }

    [JsonPropertyName("approved")]
    public int Approved => BaselinedDocs;

    [JsonPropertyName("inReviewDocs")]
    public int InReviewDocs { get; set; }

    [JsonPropertyName("inReview")]
    public int InReview => InReviewDocs;

    [JsonPropertyName("draftDocs")]
    public int DraftDocs { get; set; }

    [JsonPropertyName("draft")]
    public int Draft => DraftDocs + ChangesRequestedDocs;

    [JsonPropertyName("changesRequestedDocs")]
    public int ChangesRequestedDocs { get; set; }

    [JsonPropertyName("totalReqs")]
    public int TotalReqs { get; set; }

    [JsonPropertyName("reqs")]
    public int Reqs => TotalReqs;

    [JsonPropertyName("staleReqs")]
    public int StaleReqs { get; set; }

    [JsonPropertyName("unresolvedComments")]
    public int UnresolvedComments { get; set; }
}

public class MyDocumentWorkDto
{
    [JsonPropertyName("reviews")]
    public List<DocumentWorkItemDto> Reviews => PendingApprovals;

    [JsonPropertyName("drafts")]
    public List<DocumentWorkItemDto> Drafts => MyDrafts;

    [JsonPropertyName("staleReqs")]
    public List<StaleRequirementItemDto> StaleReqs => StaleRequirements;

    [JsonPropertyName("activity")]
    public List<DocumentWorkEventDto> Activity => RecentEvents;
    [JsonPropertyName("pendingApprovals")]
    public List<DocumentWorkItemDto> PendingApprovals { get; set; } = new();

    [JsonPropertyName("changesRequested")]
    public List<DocumentWorkItemDto> ChangesRequested { get; set; } = new();

    [JsonPropertyName("myDrafts")]
    public List<DocumentWorkItemDto> MyDrafts { get; set; } = new();

    [JsonPropertyName("staleRequirements")]
    public List<StaleRequirementItemDto> StaleRequirements { get; set; } = new();

    [JsonPropertyName("mentionsAndComments")]
    public List<DocumentWorkEventDto> MentionsAndComments { get; set; } = new();

    [JsonPropertyName("recentEvents")]
    public List<DocumentWorkEventDto> RecentEvents { get; set; } = new();

    [JsonPropertyName("projectStats")]
    public List<ProjectDocumentStatsDto> ProjectStats { get; set; } = new();
}

public class WorkItemDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = string.Empty;

    [JsonPropertyName("roleHint")]
    public string RoleHint { get; set; } = string.Empty;

    [JsonPropertyName("projectId")]
    public long ProjectId { get; set; }

    [JsonPropertyName("projectKey")]
    public string ProjectKey { get; set; } = string.Empty;

    [JsonPropertyName("projectName")]
    public string ProjectName { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("subtitle")]
    public string Subtitle { get; set; } = string.Empty;

    [JsonPropertyName("badge")]
    public string? Badge { get; set; }

    [JsonPropertyName("ctaLabel")]
    public string CtaLabel { get; set; } = string.Empty;

    [JsonPropertyName("href")]
    public string Href { get; set; } = string.Empty;

    [JsonPropertyName("actor")]
    public UserRefDto? Actor { get; set; }

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;

    [JsonPropertyName("at")]
    public string At => UpdatedAt;

    [JsonPropertyName("urgency")]
    public int Urgency { get; set; } = 0;

    [JsonPropertyName("documentId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? DocumentId { get; set; }

    [JsonPropertyName("versionId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? VersionId { get; set; }

    [JsonPropertyName("anchorId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AnchorId { get; set; }

    [JsonPropertyName("reqUid")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReqUid { get; set; }

    [JsonPropertyName("issueId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? IssueId { get; set; }

    [JsonPropertyName("issueKey")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IssueKey { get; set; }

    [JsonPropertyName("runId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? RunId { get; set; }

    [JsonPropertyName("cycleId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? CycleId { get; set; }
}
