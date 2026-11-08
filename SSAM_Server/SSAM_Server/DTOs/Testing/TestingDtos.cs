using System.Text.Json.Serialization;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Kanban;

namespace SSAM_Server.DTOs.Testing;

public class TestTreeNodeDto
{
    [JsonPropertyName("id")]
    public string Id => NodeId;

    [JsonPropertyName("nodeId")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("parentId")]
    public string? ParentId { get; set; }

    [JsonPropertyName("folderId")]
    public long? FolderId { get; set; }

    [JsonPropertyName("documentId")]
    public long? DocumentId { get; set; }

    [JsonPropertyName("docCode")]
    public string? DocCode { get; set; }

    [JsonPropertyName("reqUid")]
    public string? ReqUid { get; set; }

    [JsonPropertyName("requirement")]
    public TestCaseRequirementRefDto? Requirement { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("testCount")]
    public int TestCount => Count;

    [JsonPropertyName("passCount")]
    public int PassCount { get; set; }

    [JsonPropertyName("failCount")]
    public int FailCount { get; set; }

    [JsonPropertyName("needsReviewCount")]
    public int NeedsReviewCount { get; set; }
}

public class TestFolderDto
{
    [JsonPropertyName("id")]
    public string Id => $"folder-{FolderId}";

    [JsonPropertyName("nodeId")]
    public string NodeId => $"folder-{FolderId}";

    [JsonPropertyName("kind")]
    public string Kind => "FOLDER";

    [JsonPropertyName("label")]
    public string Label => Name;

    [JsonPropertyName("folderId")]
    public long FolderId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("parentId")]
    public long? ParentId { get; set; }

    [JsonPropertyName("requirement")]
    public TestCaseRequirementRefDto? Requirement => null;

    [JsonPropertyName("testCount")]
    public int TestCount => 0;

    [JsonPropertyName("needsReviewCount")]
    public int NeedsReviewCount => 0;
}

public class TestStepDto
{
    [JsonPropertyName("stepNumber")]
    public int StepNumber { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("testData")]
    public string? TestData { get; set; }

    [JsonPropertyName("expected")]
    public string Expected { get; set; } = string.Empty;
}

public class TestCaseRequirementRefDto
{
    [JsonPropertyName("documentId")]
    public long DocumentId { get; set; }

    [JsonPropertyName("docCode")]
    public string? DocCode { get; set; }

    [JsonPropertyName("documentCode")]
    public string DocumentCode => DocCode ?? DocTitle;

    [JsonPropertyName("docTitle")]
    public string DocTitle { get; set; } = string.Empty;

    [JsonPropertyName("reqUid")]
    public string ReqUid { get; set; } = string.Empty;

    [JsonPropertyName("reqKey")]
    public string ReqKey => ReqUid.StartsWith("REQ-", StringComparison.OrdinalIgnoreCase) && ReqUid.Length <= 8
        ? ReqUid.ToUpperInvariant()
        : (ReqUid.Length > 8 ? ReqUid[..8].ToUpperInvariant() : ReqUid.ToUpperInvariant());

    [JsonPropertyName("reqTitle")]
    public string ReqTitle { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title => ReqTitle;

    [JsonPropertyName("changed")]
    public bool Changed { get; set; }

    [JsonPropertyName("freshness")]
    public string Freshness => Changed ? "STALE" : "FRESH";

    [JsonPropertyName("changedVersionLabel")]
    public string? ChangedVersionLabel { get; set; }

    [JsonPropertyName("changedNote")]
    public string? ChangedNote => ChangedVersionLabel != null ? $"Đổi trong bản v{ChangedVersionLabel}" : null;
}

public class TestCaseIssueRefDto
{
    [JsonPropertyName("issueId")]
    public long IssueId { get; set; }

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("statusCategory")]
    public string StatusCategory { get; set; } = string.Empty;
}

public class TestCaseSummaryDto
{
    [JsonPropertyName("testCaseId")]
    public long TestCaseId { get; set; }

    [JsonPropertyName("projectId")]
    public long ProjectId { get; set; }

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = string.Empty;

    [JsonPropertyName("folderId")]
    public long? FolderId { get; set; }

    [JsonPropertyName("stepCount")]
    public int StepCount { get; set; }

    [JsonPropertyName("requirements")]
    public List<TestCaseRequirementRefDto> Requirements { get; set; } = new();

    [JsonPropertyName("linkedIssues")]
    public List<TestCaseIssueRefDto> LinkedIssues { get; set; } = new();

    [JsonPropertyName("issues")]
    public List<TestCaseIssueRefDto> Issues => LinkedIssues;

    [JsonPropertyName("needsReview")]
    public bool NeedsReview { get; set; }

    [JsonPropertyName("lastResult")]
    public string? LastResult { get; set; }

    [JsonPropertyName("lastRunAt")]
    public string? LastRunAt { get; set; }

    [JsonPropertyName("lastRunBy")]
    public UserRefDto? LastRunBy { get; set; }

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;
}

public class DefectIssueLiteDto
{
    [JsonPropertyName("issueId")]
    public long IssueId { get; set; }

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;
}

public class TestRunHistoryItemDto
{
    [JsonPropertyName("runId")]
    public long RunId { get; set; }

    [JsonPropertyName("cycleId")]
    public long CycleId { get; set; }

    [JsonPropertyName("cycleName")]
    public string CycleName { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public string Result { get; set; } = string.Empty;

    [JsonPropertyName("executedBy")]
    public UserRefDto ExecutedBy { get; set; } = new();

    [JsonPropertyName("executedAt")]
    public string ExecutedAt { get; set; } = string.Empty;

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("defectIssue")]
    public DefectIssueLiteDto? DefectIssue { get; set; }

    [JsonPropertyName("defects")]
    public List<DefectIssueLiteDto> Defects => DefectIssue != null ? new List<DefectIssueLiteDto> { DefectIssue } : new List<DefectIssueLiteDto>();
}

public class TestCasePermissionsDto
{
    [JsonPropertyName("edit")]
    public bool Edit { get; set; }

    [JsonPropertyName("delete")]
    public bool Delete { get; set; }

    [JsonPropertyName("execute")]
    public bool Execute { get; set; }
}

public class TestCaseDetailDto : TestCaseSummaryDto
{
    [JsonPropertyName("preconditions")]
    public string Preconditions { get; set; } = string.Empty;

    [JsonPropertyName("steps")]
    public List<TestStepDto> Steps { get; set; } = new();

    [JsonPropertyName("createdBy")]
    public UserRefDto CreatedBy { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("runHistory")]
    public List<TestRunHistoryItemDto> RunHistory { get; set; } = new();

    [JsonPropertyName("history")]
    public List<TestRunHistoryItemDto> History => RunHistory;

    [JsonPropertyName("can")]
    public TestCasePermissionsDto Can { get; set; } = new();
}

public class SuggestedTestCaseDto
{
    [JsonPropertyName("tempId")]
    public string TempId { get; set; } = string.Empty;

    [JsonPropertyName("reqUid")]
    public string ReqUid { get; set; } = string.Empty;

    [JsonPropertyName("documentId")]
    public long DocumentId { get; set; }

    [JsonPropertyName("requirementRef")]
    public RequirementRefInputDto RequirementRef => new() { DocumentId = DocumentId, ReqUid = ReqUid };

    [JsonPropertyName("fromCriterion")]
    public string? FromCriterion { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = string.Empty;

    [JsonPropertyName("preconditions")]
    public string Preconditions { get; set; } = string.Empty;

    [JsonPropertyName("steps")]
    public List<TestStepDto> Steps { get; set; } = new();
}

public class TestCycleStatsDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("pass")]
    public int Pass { get; set; }

    [JsonPropertyName("fail")]
    public int Fail { get; set; }

    [JsonPropertyName("blocked")]
    public int Blocked { get; set; }

    [JsonPropertyName("notRun")]
    public int NotRun { get; set; }

    [JsonPropertyName("needsRerun")]
    public int NeedsRerun { get; set; }
}

public class TestCycleDto
{
    [JsonPropertyName("cycleId")]
    public long CycleId { get; set; }

    [JsonPropertyName("projectId")]
    public long ProjectId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("sprintId")]
    public long? SprintId { get; set; }

    [JsonPropertyName("sprintName")]
    public string? SprintName { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("stats")]
    public TestCycleStatsDto Stats { get; set; } = new();

    [JsonPropertyName("counts")]
    public TestCycleStatsDto Counts => Stats;

    [JsonPropertyName("createdBy")]
    public UserRefDto CreatedBy { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
}

public class TestRunReqLiteDto
{
    [JsonPropertyName("docCode")]
    public string? DocCode { get; set; }

    [JsonPropertyName("reqUid")]
    public string ReqUid { get; set; } = string.Empty;

    [JsonPropertyName("reqTitle")]
    public string ReqTitle { get; set; } = string.Empty;
}

public class TestRunDefectRefDto
{
    [JsonPropertyName("issueId")]
    public long IssueId { get; set; }

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("statusId")]
    public long StatusId { get; set; }

    [JsonPropertyName("statusCategory")]
    public string StatusCategory { get; set; } = string.Empty;
}

public class TestRunDto
{
    [JsonPropertyName("runId")]
    public long RunId { get; set; }

    [JsonPropertyName("cycleId")]
    public long CycleId { get; set; }

    [JsonPropertyName("testCaseId")]
    public long TestCaseId { get; set; }

    [JsonPropertyName("testCase")]
    public TestCaseSummaryDto TestCase { get; set; } = new();

    [JsonPropertyName("testCaseKey")]
    public string TestCaseKey { get; set; } = string.Empty;

    [JsonPropertyName("testCaseTitle")]
    public string TestCaseTitle { get; set; } = string.Empty;

    [JsonPropertyName("testCasePriority")]
    public string TestCasePriority { get; set; } = string.Empty;

    [JsonPropertyName("testCaseType")]
    public string TestCaseType { get; set; } = string.Empty;

    [JsonPropertyName("stepCount")]
    public int StepCount { get; set; }

    [JsonPropertyName("requirements")]
    public List<TestRunReqLiteDto> Requirements { get; set; } = new();

    [JsonPropertyName("assignee")]
    public UserRefDto? Assignee { get; set; }

    [JsonPropertyName("result")]
    public string Result { get; set; } = string.Empty;

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("executedAt")]
    public string? ExecutedAt { get; set; }

    [JsonPropertyName("executedBy")]
    public UserRefDto? ExecutedBy { get; set; }

    [JsonPropertyName("defects")]
    public List<TestRunDefectRefDto> Defects { get; set; } = new();

    [JsonPropertyName("needsRerun")]
    public bool NeedsRerun { get; set; }
}

// --- Request DTOs ---

public class CreateTestFolderRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("parentId")]
    public long? ParentId { get; set; }
}

public class TestStepInputDto
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("testData")]
    public string? TestData { get; set; }

    [JsonPropertyName("expected")]
    public string Expected { get; set; } = string.Empty;
}

public class CreateTestCaseRequest
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("priority")]
    public string? Priority { get; set; }

    [JsonPropertyName("preconditions")]
    public string? Preconditions { get; set; }

    [JsonPropertyName("steps")]
    public List<TestStepInputDto> Steps { get; set; } = new();

    [JsonPropertyName("folderId")]
    public long? FolderId { get; set; }

    [JsonPropertyName("requirementRefs")]
    public List<RequirementRefInputDto>? RequirementRefs { get; set; }

    [JsonPropertyName("linkedIssueIds")]
    public List<long>? LinkedIssueIds { get; set; }

    [JsonPropertyName("issueIds")]
    public List<long>? IssueIds { get; set; }
}

public class UpdateTestCaseRequest
{
    private long? _folderId;

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("priority")]
    public string? Priority { get; set; }

    [JsonPropertyName("preconditions")]
    public string? Preconditions { get; set; }

    [JsonPropertyName("steps")]
    public List<TestStepInputDto>? Steps { get; set; }

    [JsonPropertyName("folderId")]
    public long? FolderId
    {
        get => _folderId;
        set { _folderId = value; FolderIdSpecified = true; }
    }
    [JsonIgnore] public bool FolderIdSpecified { get; private set; }

    [JsonPropertyName("requirementRefs")]
    public List<RequirementRefInputDto>? RequirementRefs { get; set; }

    [JsonPropertyName("linkedIssueIds")]
    public List<long>? LinkedIssueIds { get; set; }

    [JsonPropertyName("issueIds")]
    public List<long>? IssueIds { get; set; }

    [JsonPropertyName("markReviewed")]
    public bool? MarkReviewed { get; set; }

    [JsonPropertyName("needsReview")]
    public bool? NeedsReview { get; set; }
}

public class SuggestTestCasesRequest
{
    [JsonPropertyName("refs")]
    public List<RequirementRefInputDto> Refs { get; set; } = new();
}

public class TestCycleFilterInputDto
{
    [JsonPropertyName("testCaseIds")]
    public List<long>? TestCaseIds { get; set; }

    [JsonPropertyName("reqUids")]
    public List<string>? ReqUids { get; set; }

    [JsonPropertyName("folderId")]
    public long? FolderId { get; set; }

    [JsonPropertyName("defaultAssigneeId")]
    public long? DefaultAssigneeId { get; set; }
}

public class CreateTestCycleRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("sprintId")]
    public long? SprintId { get; set; }

    [JsonPropertyName("testCaseIds")]
    public List<long>? TestCaseIds { get; set; }

    [JsonPropertyName("defaultAssigneeId")]
    public long? DefaultAssigneeId { get; set; }

    [JsonPropertyName("filter")]
    public TestCycleFilterInputDto? Filter { get; set; }
}

public class UpdateTestCycleRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("addTestCaseIds")]
    public List<long>? AddTestCaseIds { get; set; }
}

public class UpdateTestRunRequest
{
    [JsonPropertyName("assigneeId")]
    public long? AssigneeId { get; set; }
}

public class CreateDefectInlineInputDto
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("priority")]
    public string? Priority { get; set; }

    [JsonPropertyName("assigneeId")]
    public long? AssigneeId { get; set; }

    [JsonPropertyName("sprintId")]
    public long? SprintId { get; set; }
}

public class RecordTestRunResultRequest
{
    [JsonPropertyName("result")]
    public string Result { get; set; } = string.Empty;

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("defectIssueIds")]
    public List<long>? DefectIssueIds { get; set; }

    [JsonPropertyName("createDefect")]
    public CreateDefectInlineInputDto? CreateDefect { get; set; }
}

public class TestCaseListQuery
{
    public string? ReqUid { get; set; }
    public long? DocumentId { get; set; }
    public long? FolderId { get; set; }
    public bool? Unlinked { get; set; }
    public bool? NeedsReview { get; set; }
    public string? LastResult { get; set; }
    public long? IssueId { get; set; }
    public string? Search { get; set; }
}

public class TestRunListQuery
{
    public string? AssigneeId { get; set; }
    public string? Result { get; set; }
    public bool? NeedsRerun { get; set; }
}
