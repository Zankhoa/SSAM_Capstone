using System.Text.Json;
using System.Text.Json.Serialization;
using SSAM_Server.DTOs.Common;

namespace SSAM_Server.DTOs.Documents;

public class DocumentVersionRefDto
{
    [JsonPropertyName("versionId")]
    public long VersionId { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

public class DocumentNodeDto
{
    [JsonPropertyName("documentId")]
    public long DocumentId { get; set; }

    [JsonPropertyName("projectId")]
    public long ProjectId { get; set; }

    [JsonPropertyName("parentId")]
    public long? ParentId { get; set; }

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("docType")]
    public string? DocType { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }

    [JsonPropertyName("currentVersion")]
    public DocumentVersionRefDto? CurrentVersion { get; set; }

    [JsonPropertyName("isBaselined")]
    public bool IsBaselined { get; set; }

    [JsonPropertyName("baselineVersionId")]
    public long? BaselineVersionId { get; set; }

    [JsonPropertyName("reqCount")]
    public int ReqCount { get; set; }

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;
}

public class DocumentVersionSummaryDto
{
    [JsonPropertyName("versionId")]
    public long VersionId { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public UserRefDto Author { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;

    [JsonPropertyName("updatedBy")]
    public UserRefDto UpdatedBy { get; set; } = new();

    [JsonPropertyName("submittedAt")]
    public string? SubmittedAt { get; set; }

    [JsonPropertyName("reviewedBy")]
    public UserRefDto? ReviewedBy { get; set; }

    [JsonPropertyName("reviewedAt")]
    public string? ReviewedAt { get; set; }

    [JsonPropertyName("reviewNote")]
    public string? ReviewNote { get; set; }

    [JsonPropertyName("changeSummary")]
    public string? ChangeSummary { get; set; }
}

public class DocumentVersionDto
{
    [JsonPropertyName("versionId")]
    public long VersionId { get; set; }

    [JsonPropertyName("documentId")]
    public long DocumentId { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("subtitle")]
    public string? Subtitle { get; set; }

    [JsonPropertyName("changeSummary")]
    public string? ChangeSummary => ReviewNote ?? Subtitle;

    [JsonPropertyName("content")]
    public JsonElement Content { get; set; }

    [JsonPropertyName("baseVersionId")]
    public long? BaseVersionId { get; set; }

    [JsonPropertyName("rowVersion")]
    public int RowVersion { get; set; }

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;

    [JsonPropertyName("updatedBy")]
    public UserRefDto UpdatedBy { get; set; } = new();

    [JsonPropertyName("author")]
    public UserRefDto Author { get; set; } = new();

    [JsonPropertyName("submittedBy")]
    public UserRefDto? SubmittedBy { get; set; }

    [JsonPropertyName("submittedAt")]
    public string? SubmittedAt { get; set; }

    [JsonPropertyName("reviewedBy")]
    public UserRefDto? ReviewedBy { get; set; }

    [JsonPropertyName("reviewedAt")]
    public string? ReviewedAt { get; set; }

    [JsonPropertyName("reviewNote")]
    public string? ReviewNote { get; set; }
}

public class DocumentPermissionsDto
{
    [JsonPropertyName("edit")]
    public bool Edit { get; set; }

    [JsonPropertyName("approve")]
    public bool Approve { get; set; }

    [JsonPropertyName("revise")]
    public bool Revise { get; set; }

    [JsonPropertyName("delete")]
    public bool Delete { get; set; }
}

public class DocumentDetailDto
{
    [JsonPropertyName("node")]
    public DocumentNodeDto Node { get; set; } = new();

    [JsonPropertyName("versions")]
    public List<DocumentVersionSummaryDto> Versions { get; set; } = new();

    [JsonPropertyName("current")]
    public DocumentVersionDto Current { get; set; } = new();

    [JsonPropertyName("can")]
    public DocumentPermissionsDto Can { get; set; } = new();
}

public class AcceptanceCriterionDto
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("checked")]
    public bool Checked { get; set; } = true;
}

public class RequirementItemDto
{
    [JsonPropertyName("requirementId")]
    public long RequirementId { get; set; }

    [JsonPropertyName("documentId")]
    public long DocumentId { get; set; }

    [JsonPropertyName("versionId")]
    public long VersionId { get; set; }

    [JsonPropertyName("reqUid")]
    public string ReqUid { get; set; } = string.Empty;

    [JsonPropertyName("reqKey")]
    public string ReqKey { get; set; } = string.Empty;

    [JsonPropertyName("anchorId")]
    public string AnchorId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "MEDIUM";

    [JsonPropertyName("freshness")]
    public string Freshness { get; set; } = "FRESH";

    [JsonPropertyName("changedSinceBaseline")]
    public bool ChangedSinceBaseline { get; set; }

    [JsonPropertyName("bodyPreview")]
    public string BodyPreview { get; set; } = string.Empty;

    [JsonPropertyName("bodyLength")]
    public int BodyLength { get; set; }

    [JsonPropertyName("acceptanceCriteria")]
    public List<AcceptanceCriterionDto> AcceptanceCriteria { get; set; } = new();

    [JsonPropertyName("hasTable")]
    public bool HasTable { get; set; }

    [JsonPropertyName("hasDiagram")]
    public bool HasDiagram { get; set; }

    [JsonPropertyName("hasFigma")]
    public bool HasFigma { get; set; }

    [JsonPropertyName("contentHash")]
    public string ContentHash { get; set; } = string.Empty;

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }
}

public class RequirementSnapshotDto
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("bodyPreview")]
    public string BodyPreview { get; set; } = string.Empty;

    [JsonPropertyName("acceptanceCriteria")]
    public List<string> AcceptanceCriteria { get; set; } = new();
}

public class RequirementDiffItemDto
{
    [JsonPropertyName("reqUid")]
    public string ReqUid { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("change")]
    public string Change { get; set; } = string.Empty;

    [JsonPropertyName("before")]
    public RequirementSnapshotDto? Before { get; set; }

    [JsonPropertyName("after")]
    public RequirementSnapshotDto? After { get; set; }
}

public class DocumentDiffSummaryDto
{
    [JsonPropertyName("added")]
    public int Added { get; set; }

    [JsonPropertyName("modified")]
    public int Modified { get; set; }

    [JsonPropertyName("removed")]
    public int Removed { get; set; }

    [JsonPropertyName("unchanged")]
    public int Unchanged { get; set; }
}

public class DocumentDiffDto
{
    [JsonPropertyName("fromVersion")]
    public DocumentVersionRefDto FromVersion { get; set; } = new();

    [JsonPropertyName("toVersion")]
    public DocumentVersionRefDto ToVersion { get; set; } = new();

    [JsonPropertyName("summary")]
    public DocumentDiffSummaryDto Summary { get; set; } = new();

    [JsonPropertyName("items")]
    public List<RequirementDiffItemDto> Items { get; set; } = new();

    [JsonPropertyName("textBefore")]
    public string TextBefore { get; set; } = string.Empty;

    [JsonPropertyName("textAfter")]
    public string TextAfter { get; set; } = string.Empty;
}

public class DocumentCommentDto
{
    [JsonPropertyName("commentId")]
    public long CommentId { get; set; }

    [JsonPropertyName("documentId")]
    public long DocumentId { get; set; }

    [JsonPropertyName("versionId")]
    public long VersionId { get; set; }

    [JsonPropertyName("parentId")]
    public long? ParentId { get; set; }

    [JsonPropertyName("anchorId")]
    public string? AnchorId { get; set; }

    [JsonPropertyName("quote")]
    public string? Quote { get; set; }

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    [JsonPropertyName("mentions")]
    public List<UserRefDto> Mentions { get; set; } = new();

    [JsonPropertyName("author")]
    public UserRefDto Author { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("resolvedBy")]
    public UserRefDto? ResolvedBy { get; set; }

    [JsonPropertyName("resolvedAt")]
    public string? ResolvedAt { get; set; }

    [JsonPropertyName("replies")]
    public List<DocumentCommentDto> Replies { get; set; } = new();
}

public class DocumentActivityDto
{
    [JsonPropertyName("activityId")]
    public long ActivityId { get; set; }

    [JsonPropertyName("documentId")]
    public long DocumentId { get; set; }

    [JsonPropertyName("versionId")]
    public long? VersionId { get; set; }

    [JsonPropertyName("versionLabel")]
    public string? VersionLabel { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("actor")]
    public UserRefDto Actor { get; set; } = new();

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
}

public class StringOrLinesJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return reader.GetString() ?? string.Empty;

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var items = new List<string>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    var s = reader.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        items.Add(s.Trim());
                }
            }
            return string.Join("\n", items);
        }

        return string.Empty;
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value ?? string.Empty);
    }
}

public class NormalizedFieldsDto
{
    [JsonPropertyName("scope")]
    [JsonConverter(typeof(StringOrLinesJsonConverter))]
    public string Scope { get; set; } = string.Empty;

    [JsonPropertyName("actors")]
    [JsonConverter(typeof(StringOrLinesJsonConverter))]
    public string Actors { get; set; } = string.Empty;

    [JsonPropertyName("mainFlows")]
    [JsonConverter(typeof(StringOrLinesJsonConverter))]
    public string MainFlows { get; set; } = string.Empty;

    [JsonPropertyName("businessRules")]
    [JsonConverter(typeof(StringOrLinesJsonConverter))]
    public string BusinessRules { get; set; } = string.Empty;
}

public class ExtractedRequirementDto
{
    [JsonPropertyName("tempId")]
    public string TempId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "MEDIUM";

    [JsonPropertyName("acceptanceCriteria")]
    public List<string> AcceptanceCriteria { get; set; } = new();

    [JsonPropertyName("sourceLines")]
    public List<int> SourceLines { get; set; } = new();
}

public class GeneratedItemDto
{
    [JsonPropertyName("reqUid")]
    public string ReqUid { get; set; } = string.Empty;

    [JsonPropertyName("tempId")]
    public string TempId => ReqUid;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "MEDIUM";

    [JsonPropertyName("acceptanceCriteria")]
    public List<string> AcceptanceCriteria { get; set; } = new();

    [JsonPropertyName("sourceLines")]
    public List<int> SourceLines { get; set; } = new();
}

public class DocumentGenerationDto
{
    [JsonPropertyName("rawNotes")]
    public string RawNotes { get; set; } = string.Empty;

    [JsonPropertyName("fields")]
    public NormalizedFieldsDto Fields { get; set; } = new();

    [JsonPropertyName("items")]
    public List<GeneratedItemDto> Items { get; set; } = new();

    [JsonPropertyName("createdBy")]
    public UserRefDto CreatedBy { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
}

public class DiagramProposalDto
{
    [JsonPropertyName("mermaid")]
    public string Mermaid { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "flowchart";

    [JsonPropertyName("notes")]
    public List<string> Notes { get; set; } = new();
}

// --- Request DTOs ---

public class CreateDocumentNodeRequest
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("docType")]
    public string? DocType { get; set; }

    [JsonPropertyName("parentId")]
    public long? ParentId { get; set; }

    [JsonPropertyName("subtitle")]
    public string? Subtitle { get; set; }

    [JsonPropertyName("templateId")]
    public string? TemplateId { get; set; }

    [JsonPropertyName("content")]
    public JsonElement? Content { get; set; }
}

public class UpdateDocumentNodeRequest
{
    private long? _parentId;

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("parentId")]
    public long? ParentId
    {
        get => _parentId;
        set
        {
            _parentId = value;
            ParentIdSpecified = true;
        }
    }

    [JsonIgnore]
    public bool ParentIdSpecified { get; private set; }

    [JsonPropertyName("sortOrder")]
    public int? SortOrder { get; set; }
}

public class SaveDocumentContentRequest
{
    private string? _subtitle;

    [JsonPropertyName("content")]
    public JsonElement Content { get; set; }

    [JsonPropertyName("subtitle")]
    public string? Subtitle
    {
        get => _subtitle;
        set
        {
            _subtitle = value;
            SubtitleSpecified = true;
        }
    }

    [JsonIgnore]
    public bool SubtitleSpecified { get; private set; }

    [JsonPropertyName("rowVersion")]
    public int RowVersion { get; set; }
}

public class SaveContentResultDto
{
    [JsonPropertyName("version")]
    public DocumentVersionDto Version { get; set; } = new();

    [JsonPropertyName("requirements")]
    public List<RequirementItemDto> Requirements { get; set; } = new();
}

public class ApproveDocumentRequest
{
    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

public class RequestChangesRequest
{
    [JsonPropertyName("note")]
    public string Note { get; set; } = string.Empty;
}

public class CreateDocumentCommentRequest
{
    [JsonPropertyName("versionId")]
    public long VersionId { get; set; }

    [JsonPropertyName("anchorId")]
    public string? AnchorId { get; set; }

    [JsonPropertyName("quote")]
    public string? Quote { get; set; }

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    [JsonPropertyName("parentId")]
    public long? ParentId { get; set; }

    [JsonPropertyName("mentionUserIds")]
    public List<long>? MentionUserIds { get; set; }
}

public class NormalizeNotesRequest
{
    [JsonPropertyName("rawNotes")]
    public string RawNotes { get; set; } = string.Empty;
}

public class ExtractRequirementsRequest
{
    [JsonPropertyName("fields")]
    public NormalizedFieldsDto Fields { get; set; } = new();

    [JsonPropertyName("rawNotes")]
    public string? RawNotes { get; set; }
}

public class GenerateDocumentRequest
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("docType")]
    public string DocType { get; set; } = string.Empty;

    [JsonPropertyName("parentId")]
    public long? ParentId { get; set; }

    [JsonPropertyName("subtitle")]
    public string? Subtitle { get; set; }

    [JsonPropertyName("rawNotes")]
    public string RawNotes { get; set; } = string.Empty;

    [JsonPropertyName("fields")]
    public NormalizedFieldsDto Fields { get; set; } = new();

    [JsonPropertyName("items")]
    public List<ExtractedRequirementDto> Items { get; set; } = new();
}

public class GenerateDiagramRequest
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("documentId")]
    public long? DocumentId { get; set; }

    [JsonPropertyName("anchorId")]
    public string? AnchorId { get; set; }
}

public class CreateRevisionRequest
{
    [JsonPropertyName("bump")]
    public string? Bump { get; set; }

    [JsonPropertyName("changeSummary")]
    public string? ChangeSummary { get; set; }
}

public class UpdateRequirementRequest
{
    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "MEDIUM";
}
