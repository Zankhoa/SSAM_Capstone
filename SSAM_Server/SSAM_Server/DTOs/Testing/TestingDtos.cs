using System.Text.Json;
using System.Text.Json.Serialization;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Kanban;

namespace SSAM_Server.DTOs.Testing;

public class TestPersonDto
{
    public long UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool Qa { get; set; }
}

public class TestHandoverDto
{
    public UserRefDto By { get; set; } = null!;
    public string At { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public class IssueKeyRefDto
{
    public long IssueId { get; set; }
    public string Key { get; set; } = string.Empty;
}

public class TestBugRefDto
{
    public long IssueId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Priority { get; set; } = "MEDIUM";
    public string StatusName { get; set; } = string.Empty;
    public string StatusCategory { get; set; } = "TODO";
}

public class SelectOptionDto
{
    public string OptionId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Color { get; set; }
}

public class CustomColumnDto
{
    public string ColumnId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Kind { get; set; } = "TEXT"; // "TEXT" | "MULTILINE" | "SELECT" | "NUMBER" | "DATE" | "PERSON"
    public List<SelectOptionDto>? Options { get; set; }
    public bool? Multiple { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public string? Unit { get; set; }
    public string? DefaultValue { get; set; }
    public bool? DefaultToday { get; set; }
    public int? MaxLength { get; set; }
    public string? PersonScope { get; set; } // "QA" | "ALL"
}

public class SheetLayoutDto
{
    public List<string> Order { get; set; } = new()
    {
        "code",
        "title",
        "req",
        "preconditions",
        "steps",
        "expected",
        "actual",
        "result",
        "build",
        "bug",
        "bugStatus",
        "priority",
        "category",
        "browser"
    };

    public Dictionary<string, string> Labels { get; set; } = new();
    public Dictionary<string, int> Widths { get; set; } = new();
    public List<CustomColumnDto> CustomColumns { get; set; } = new();
}

// =========================================================================
// Plan, Environment, Build, Module, Section, Assignment DTOs
// =========================================================================

public class TestBuildDto
{
    public long BuildId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string DeployedAt { get; set; } = string.Empty;
    public UserRefDto? DeployedBy { get; set; }
    public List<IssueKeyRefDto> FixedBugs { get; set; } = new();
    public int ResultCount { get; set; }
}

public class TestEnvironmentDto
{
    public long EnvId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Note { get; set; }
    public int Position { get; set; }
    public string? CurrentBuild { get; set; }
    public List<TestBuildDto> Builds { get; set; } = new();
}

public class TestSectionDto
{
    public long SectionId { get; set; }
    public long ModuleId { get; set; }
    public long? ParentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? AnchorId { get; set; }
    public bool ReqChanged { get; set; }
    public int Position { get; set; }
}

public class TestModuleDto
{
    public long ModuleId { get; set; }
    public long DocumentId { get; set; }
    public string DocumentCode { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public int Position { get; set; }
    public List<TestSectionDto> Sections { get; set; } = new();
}

public class TestAssignmentDto
{
    public long ModuleId { get; set; }
    public long EnvId { get; set; }
    public UserRefDto? Assignee { get; set; }
}

public class TestPlanDto
{
    public long PlanId { get; set; }
    public long ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string Status { get; set; } = "OPEN";
    public string CreatedAt { get; set; } = string.Empty;
    public TestHandoverDto? Handover { get; set; }
    public List<TestEnvironmentDto> Environments { get; set; } = new();
    public List<TestModuleDto> Modules { get; set; } = new();
    public List<TestAssignmentDto> Assignments { get; set; } = new();
    public SheetLayoutDto Layout { get; set; } = new();
    public List<TestPersonDto> People { get; set; } = new();
}

// =========================================================================
// Test Case, Result, Retest, Activity, Traceability DTOs
// =========================================================================

public class TestStepDto
{
    public string Action { get; set; } = string.Empty;
    public string Expected { get; set; } = string.Empty;
}

public class ReqRefDto
{
    public long DocumentId { get; set; }
    public string DocumentCode { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public string ReqUid { get; set; } = string.Empty;
    public string? ReqKey { get; set; }
    public string? Title { get; set; }
    public string? AnchorId { get; set; }
    public bool Changed { get; set; }
    public string? Freshness { get; set; }
    public string? ChangedNote { get; set; }
}

public class TestResultDto
{
    public long ResultId { get; set; }
    public long EnvId { get; set; }
    public string Build { get; set; } = string.Empty;
    public string Result { get; set; } = "UNTEST";
    public UserRefDto By { get; set; } = null!;
    public string At { get; set; } = string.Empty;
    public UserRefDto? BehalfOf { get; set; }
}

public class RetestFlagDto
{
    public long EnvId { get; set; }
    public string Reason { get; set; } = "REGRESSION";
    public IssueKeyRefDto? Bug { get; set; }
    public string? Build { get; set; }
    public string? ReqKey { get; set; }
    public string At { get; set; } = string.Empty;
}

public class TestCaseDto
{
    public long TestCaseId { get; set; }
    public long PlanId { get; set; }
    public long ModuleId { get; set; }
    public long SectionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string ModuleCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Preconditions { get; set; }
    public List<TestStepDto> Steps { get; set; } = new();
    public string? Actual { get; set; }
    public List<ReqRefDto> Requirements { get; set; } = new();
    public bool AiSuggestedReqs { get; set; }
    public string Priority { get; set; } = "MEDIUM";
    public string? Category { get; set; }
    public string? Browser { get; set; }
    public Dictionary<string, object?> CustomFields { get; set; } = new();
    public Dictionary<string, CellStyleDto> CellStyles { get; set; } = new();
    public string Status { get; set; } = "ACTIVE";
    public int Position { get; set; }
    public TestBugRefDto? Bug { get; set; }
    public List<TestResultDto> Results { get; set; } = new();
    public List<RetestFlagDto> Retest { get; set; } = new();
    public string UpdatedAt { get; set; } = string.Empty;
}

public class TestActivityDto
{
    public long ActivityId { get; set; }
    public long PlanId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string At { get; set; } = string.Empty;
    public UserRefDto Actor { get; set; } = null!;
    public long? EnvId { get; set; }
    public long? ModuleId { get; set; }
    public long? TestCaseId { get; set; }
    public string? From { get; set; }
    public string? To { get; set; }
    public string? Build { get; set; }
    public UserRefDto? BehalfOf { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class TraceCaseDto
{
    public long TestCaseId { get; set; }
    public long ModuleId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = "UNTEST";
    public bool Ai { get; set; }
}

public class TraceRowDto
{
    public long DocumentId { get; set; }
    public string DocumentCode { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public string ReqUid { get; set; } = string.Empty;
    public string ReqKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? AnchorId { get; set; }
    public bool Changed { get; set; }
    public string Status { get; set; } = "NO_TESTS";
    public List<TraceCaseDto> Cases { get; set; } = new();
    public List<IssueKeyRefDto> Bugs { get; set; } = new();
}

public class TestPlacementDto
{
    public long? ModuleId { get; set; }
    public long? SectionId { get; set; }
    public long? GroupId { get; set; }
    public long DocumentId { get; set; }
    public string? AnchorId { get; set; }
    public string SectionTitle { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class SuggestedTestCaseDesignDto
{
    public string Name { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
}

public class SuggestedTestCaseDto
{
    public string TempId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Preconditions { get; set; }
    public string Priority { get; set; } = "MEDIUM";
    public string Category { get; set; } = "Functional";
    public string Source { get; set; } = "AI";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SuggestedTestCaseDesignDto? Design { get; set; }
    public List<TestStepDto> Steps { get; set; } = new();
    public RequirementRefInputDto RequirementRef { get; set; } = new();
    public string? FromCriterion { get; set; }
    public TestPlacementDto Placement { get; set; } = new();
}

public class TestCaseGenerationContextDto
{
    [JsonPropertyName("requirementIndex")]
    public int RequirementIndex { get; set; }

    [JsonPropertyName("requirementUid")]
    public string RequirementUid { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("actor")]
    public string? Actor { get; set; }

    [JsonPropertyName("preconditions")]
    public string? Preconditions { get; set; }

    [JsonPropertyName("constraints")]
    public List<string> Constraints { get; set; } = new();

    [JsonPropertyName("acceptanceCriteria")]
    public List<string> AcceptanceCriteria { get; set; } = new();

    [JsonPropertyName("existingTestCaseTitles")]
    public List<string> ExistingTestCaseTitles { get; set; } = new();

    [JsonPropertyName("designNames")]
    public List<string> DesignNames { get; set; } = new();
}

public class AiGeneratedTestCaseDraftDto
{
    [JsonRequired]
    [JsonPropertyName("requirementIndex")]
    public int RequirementIndex { get; set; }

    [JsonPropertyName("acceptanceCriterionIndex")]
    public int? AcceptanceCriterionIndex { get; set; }

    [JsonPropertyName("designIndex")]
    public int? DesignIndex { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("preconditions")]
    public string Preconditions { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("steps")]
    public List<TestStepDto> Steps { get; set; } = new();
}

// =========================================================================
// Request & Response Payloads
// =========================================================================

public class CreateTestPlanRequest
{
    public string Name { get; set; } = string.Empty;
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public List<string>? Environments { get; set; }
}

public class UpdateTestPlanRequest
{
    public string? Name { get; set; }

    [JsonIgnore]
    public bool StartDateSpecified { get; private set; }
    private string? _startDate;
    public string? StartDate
    {
        get => _startDate;
        set
        {
            _startDate = value;
            StartDateSpecified = true;
        }
    }

    [JsonIgnore]
    public bool EndDateSpecified { get; private set; }
    private string? _endDate;
    public string? EndDate
    {
        get => _endDate;
        set
        {
            _endDate = value;
            EndDateSpecified = true;
        }
    }
}

public class CreateTestEnvironmentRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Note { get; set; }
}

public class UpdateTestEnvironmentRequest
{
    public string? Name { get; set; }
    public string? Url { get; set; }
    public string? Note { get; set; }
    public int? Position { get; set; }
}

public class DeployTestBuildRequest
{
    public string Version { get; set; } = string.Empty;
    public List<long>? FixedIssueIds { get; set; }
}

public class DeployTestBuildResponse
{
    public TestBuildDto Build { get; set; } = null!;
    public int RetestCount { get; set; }
}

public class TriggerRegressionRequest
{
    public List<long>? ModuleIds { get; set; }
}

public class TriggerRegressionResponse
{
    public int RetestCount { get; set; }
}

public class CreateTestModuleRequest
{
    public long? DocumentId { get; set; }
    public string? Name { get; set; }
    public string? ShortCode { get; set; }
    public List<string>? AnchorIds { get; set; }
}

public class UpdateTestModuleRequest
{
    public string? Name { get; set; }
    public string? ShortCode { get; set; }
    public int? Position { get; set; }
}

public class CreateTestSectionRequest
{
    public long? ParentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? AnchorId { get; set; }
}

public class UpdateTestSectionRequest
{
    public string? Title { get; set; }
    public string? AnchorId { get; set; }
    public int? Position { get; set; }
}

public class AssignmentInputItemDto
{
    public long ModuleId { get; set; }
    public long EnvId { get; set; }
    public long? AssigneeId { get; set; }
}

public class SetTestAssignmentRequest
{
    public List<AssignmentInputItemDto>? Items { get; set; }
    public long? ModuleId { get; set; }
    public long? EnvId { get; set; }
    public long? AssigneeId { get; set; }
}

public class ListTestCasesQuery
{
    public long? PlanId { get; set; }
    public long? ModuleId { get; set; }
    public long? DocumentId { get; set; }
    public string? ReqUid { get; set; }
    public string? Search { get; set; }
    public bool IncludeDeleted { get; set; }
}

public class CreateTestCaseRequest
{
    public long SectionId { get; set; }
    public string? Title { get; set; }
    public string? Preconditions { get; set; }
    public List<TestStepDto>? Steps { get; set; }
    public string? Actual { get; set; }
    public List<RequirementRefInputDto>? RequirementRefs { get; set; }
    public bool? AiSuggestedReqs { get; set; }
    public string? Priority { get; set; }
    public string? Category { get; set; }
    public string? Browser { get; set; }
    public Dictionary<string, object?>? CustomFields { get; set; }
    public long? AfterTestCaseId { get; set; }
}

public class UpdateTestCaseRequest
{
    public long? SectionId { get; set; }
    public string? Title { get; set; }

    [JsonIgnore]
    public bool PreconditionsSpecified { get; private set; }
    private string? _preconditions;
    public string? Preconditions
    {
        get => _preconditions;
        set
        {
            _preconditions = value;
            PreconditionsSpecified = true;
        }
    }

    public List<TestStepDto>? Steps { get; set; }

    [JsonIgnore]
    public bool ActualSpecified { get; private set; }
    private string? _actual;
    public string? Actual
    {
        get => _actual;
        set
        {
            _actual = value;
            ActualSpecified = true;
        }
    }

    public List<RequirementRefInputDto>? RequirementRefs { get; set; }
    public bool? AiSuggestedReqs { get; set; }
    public string? Priority { get; set; }

    [JsonIgnore]
    public bool CategorySpecified { get; private set; }
    private string? _category;
    public string? Category
    {
        get => _category;
        set
        {
            _category = value;
            CategorySpecified = true;
        }
    }

    [JsonIgnore]
    public bool BrowserSpecified { get; private set; }
    private string? _browser;
    public string? Browser
    {
        get => _browser;
        set
        {
            _browser = value;
            BrowserSpecified = true;
        }
    }

    public Dictionary<string, object?>? CustomFields { get; set; }
    public Dictionary<string, CellStyleDto?>? CellStyles { get; set; }
    public string? Status { get; set; }
    public int? Position { get; set; }
}

public class CellStyleDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? B { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? I { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? U { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? S { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Size { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Color { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Fill { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Align { get; set; }
}

public class BulkUpdateTestCaseItemDto
{
    public long TestCaseId { get; set; }
    public UpdateTestCaseRequest Changes { get; set; } = new();
}

public class BulkUpdateTestCasesRequest
{
    public List<BulkUpdateTestCaseItemDto> Items { get; set; } = new();
}

public class RecordSingleResultRequest
{
    public long EnvId { get; set; }
    public string Result { get; set; } = string.Empty;
}

public class RecordTestResultsRequest
{
    public long EnvId { get; set; }
    public List<long> TestCaseIds { get; set; } = new();
    public string Result { get; set; } = string.Empty;
}

public class RecordTestResultsResponse
{
    public List<TestCaseDto> Items { get; set; } = new();
    public List<long> NeedBug { get; set; } = new();
    public List<IssueKeyRefDto> Reopened { get; set; } = new();
}

public class ReportTestBugRequest
{
    public long EnvId { get; set; }
    public List<long> TestCaseIds { get; set; } = new();
    public string Title { get; set; } = string.Empty;
    public string? Priority { get; set; }
    public string? Description { get; set; }
}

public class ReportTestBugResponse
{
    public IssueKeyRefDto Issue { get; set; } = null!;
    public List<TestCaseDto> Items { get; set; } = new();
}

public class LinkTestBugRequest
{
    public List<long> TestCaseIds { get; set; } = new();
}

public class HandoverTestPlanRequest
{
    public string? Note { get; set; }
}

public class ListActivitiesQuery
{
    public long? TestCaseId { get; set; }
    public long? ActorId { get; set; }
    public string? Type { get; set; }
    public long? ModuleId { get; set; }
}

public class SuggestDesignSourceDto
{
    public string Kind { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Name { get; set; }
    public string? DataUrl { get; set; }
}

public class SuggestDesignItemDto
{
    public long DocumentId { get; set; }
    public string ReqUid { get; set; } = string.Empty;
    public SuggestDesignSourceDto Source { get; set; } = new();
}

public class SuggestTestCasesRequest
{
    public long? PlanId { get; set; }
    public List<RequirementRefInputDto> Refs { get; set; } = new();
    public List<SuggestDesignItemDto>? Designs { get; set; }
}

public class AiApplyItemDto
{
    public string Title { get; set; } = string.Empty;
    public string? Preconditions { get; set; }
    public string? Priority { get; set; }
    public string? Category { get; set; }
    public List<TestStepDto>? Steps { get; set; }
    public RequirementRefInputDto? RequirementRef { get; set; }
    public TestPlacementDto? Placement { get; set; }
}

public class AiApplyTestCasesRequest
{
    public List<AiApplyItemDto> Items { get; set; } = new();
}
