using System.Text.Json;
using System.Text.Json.Serialization;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Companies;
using SSAM_Server.DTOs.Projects;

namespace SSAM_Server.DTOs.Kanban;

public class BoardColumnDto
{
    [JsonPropertyName("statusId")]
    public long StatusId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("phaseId")]
    public long PhaseId { get; set; }

    [JsonPropertyName("phaseName")]
    public string PhaseName { get; set; } = string.Empty;

    [JsonPropertyName("position")]
    public int Position { get; set; }

    [JsonPropertyName("wipLimit")]
    public int? WipLimit { get; set; }

    [JsonPropertyName("isInitial")]
    public bool IsInitial { get; set; }
}

public class SprintStatsDto
{
    [JsonPropertyName("totalIssues")]
    public int TotalIssues { get; set; }

    [JsonPropertyName("doneIssues")]
    public int DoneIssues { get; set; }

    [JsonPropertyName("totalPoints")]
    public int TotalPoints { get; set; }

    [JsonPropertyName("donePoints")]
    public int DonePoints { get; set; }
}

public class SprintDto
{
    [JsonPropertyName("sprintId")]
    public long SprintId { get; set; }

    [JsonPropertyName("projectId")]
    public long ProjectId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("goal")]
    public string? Goal { get; set; }

    [JsonPropertyName("startDate")]
    public string? StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public string? EndDate { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("capacityPoints")]
    public int? CapacityPoints { get; set; }

    [JsonPropertyName("committedPoints")]
    public int CommittedPoints => Stats.TotalPoints;

    [JsonPropertyName("donePoints")]
    public int DonePoints => Stats.DonePoints;

    [JsonPropertyName("issueCount")]
    public int IssueCount => Stats.TotalIssues;

    [JsonPropertyName("stats")]
    public SprintStatsDto Stats { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("completedAt")]
    public string? CompletedAt { get; set; }
}

public class BoardDto
{
    [JsonPropertyName("columns")]
    public List<BoardColumnDto> Columns { get; set; } = new();

    [JsonPropertyName("activeSprint")]
    public SprintDto? ActiveSprint { get; set; }
}

public class IssueRequirementRefDto
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

    [JsonPropertyName("anchorId")]
    public string AnchorId { get; set; } = string.Empty;

    [JsonPropertyName("changed")]
    public bool Changed { get; set; }

    [JsonPropertyName("freshness")]
    public string Freshness => Changed ? "STALE" : "FRESH";

    [JsonPropertyName("changedVersionLabel")]
    public string? ChangedVersionLabel { get; set; }

    [JsonPropertyName("changedNote")]
    public string? ChangedNote => ChangedVersionLabel != null ? $"Đổi trong bản v{ChangedVersionLabel}" : null;
}

public class IssueTestSummaryDto
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

public class IssueSummaryDto
{
    [JsonPropertyName("issueId")]
    public long IssueId { get; set; }

    [JsonPropertyName("projectId")]
    public long ProjectId { get; set; }

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("statusId")]
    public long StatusId { get; set; }

    [JsonPropertyName("statusName")]
    public string StatusName { get; set; } = string.Empty;

    [JsonPropertyName("statusCategory")]
    public string StatusCategory { get; set; } = string.Empty;

    [JsonPropertyName("statusColor")]
    public string? StatusColor { get; set; }

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = string.Empty;

    [JsonPropertyName("points")]
    public int? Points { get; set; }

    [JsonPropertyName("assignee")]
    public UserRefDto? Assignee { get; set; }

    [JsonPropertyName("department")]
    public DepartmentRefDto? Department { get; set; }

    [JsonPropertyName("sprintId")]
    public long? SprintId { get; set; }

    [JsonPropertyName("parentId")]
    public long? ParentId { get; set; }

    [JsonPropertyName("parentKey")]
    public string? ParentKey { get; set; }

    [JsonPropertyName("rank")]
    public string Rank { get; set; } = string.Empty;

    [JsonPropertyName("labels")]
    public List<string> Labels { get; set; } = new();

    [JsonPropertyName("dueDate")]
    public string? DueDate { get; set; }

    [JsonPropertyName("reqChanged")]
    public bool ReqChanged { get; set; }

    [JsonPropertyName("requirements")]
    public List<IssueRequirementRefDto> Requirements { get; set; } = new();

    [JsonPropertyName("subtaskCount")]
    public int SubtaskCount { get; set; }

    [JsonPropertyName("subtaskDone")]
    public int SubtaskDone { get; set; }

    [JsonPropertyName("tests")]
    public IssueTestSummaryDto Tests { get; set; } = new();

    [JsonPropertyName("testSummary")]
    public IssueTestSummaryDto? TestSummary { get; set; }

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;
}

public class IssuePermissionsDto
{
    [JsonPropertyName("edit")]
    public bool Edit { get; set; }

    [JsonPropertyName("transition")]
    public bool Transition { get; set; }

    [JsonPropertyName("changeStatus")]
    public bool ChangeStatus => Transition;

    [JsonPropertyName("assign")]
    public bool Assign { get; set; }

    [JsonPropertyName("delete")]
    public bool Delete { get; set; }

    [JsonPropertyName("comment")]
    public bool Comment { get; set; }
}

public class IssueLinkedTestCaseDto
{
    [JsonPropertyName("testCaseId")]
    public long TestCaseId { get; set; }

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("lastResult")]
    public string? LastResult { get; set; }

    [JsonPropertyName("planId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? PlanId { get; set; }

    [JsonPropertyName("envId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? EnvId { get; set; }
}

public class IssueAllowedTransitionDto : WorkflowTransitionDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class IssueDetailDto : IssueSummaryDto
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("reporter")]
    public UserRefDto Reporter { get; set; } = new();

    [JsonPropertyName("subtasks")]
    public List<IssueSummaryDto> Subtasks { get; set; } = new();

    [JsonPropertyName("testCases")]
    public List<IssueLinkedTestCaseDto> TestCases { get; set; } = new();

    [JsonPropertyName("attachments")]
    public List<IssueAttachmentDto> Attachments { get; set; } = new();

    [JsonPropertyName("allowedTransitions")]
    public List<IssueAllowedTransitionDto> AllowedTransitions { get; set; } = new();

    [JsonPropertyName("resolvedAt")]
    public string? ResolvedAt { get; set; }

    [JsonPropertyName("can")]
    public IssuePermissionsDto Can { get; set; } = new();
}

public class IssueAttachmentDto
{
    [JsonPropertyName("attachmentId")]
    public long AttachmentId { get; set; }

    [JsonPropertyName("issueId")]
    public long IssueId { get; set; }

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }

    [JsonPropertyName("uploadedBy")]
    public UserRefDto UploadedBy { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
}

public class IssueCommentDto
{
    [JsonPropertyName("commentId")]
    public long CommentId { get; set; }

    [JsonPropertyName("issueId")]
    public long IssueId { get; set; }

    [JsonPropertyName("parentId")]
    public long? ParentId { get; set; }

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    [JsonPropertyName("mentions")]
    public List<UserRefDto> Mentions { get; set; } = new();

    [JsonPropertyName("author")]
    public UserRefDto Author { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("replies")]
    public List<IssueCommentDto> Replies { get; set; } = new();
}

public class IssueActivityDto
{
    [JsonPropertyName("activityId")]
    public long ActivityId { get; set; }

    [JsonPropertyName("issueId")]
    public long IssueId { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("actor")]
    public UserRefDto Actor { get; set; } = new();

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
}

// --- Request DTOs ---

public class WipLimitItemInputDto
{
    [JsonPropertyName("statusId")]
    public long StatusId { get; set; }

    [JsonPropertyName("limit")]
    public int? Limit { get; set; }
}

public class UpdateWipLimitsRequest
{
    [JsonPropertyName("limits")]
    public List<WipLimitItemInputDto> Limits { get; set; } = new();
}

public class RequirementRefInputDto
{
    [JsonPropertyName("documentId")]
    public long DocumentId { get; set; }

    [JsonPropertyName("reqUid")]
    public string ReqUid { get; set; } = string.Empty;
}

public class CreateIssueRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "TASK";

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("statusId")]
    public long? StatusId { get; set; }

    [JsonPropertyName("priority")]
    public string? Priority { get; set; }

    [JsonPropertyName("points")]
    public int? Points { get; set; }

    [JsonPropertyName("assigneeId")]
    public long? AssigneeId { get; set; }

    [JsonPropertyName("departmentId")]
    public long? DepartmentId { get; set; }

    [JsonPropertyName("sprintId")]
    public long? SprintId { get; set; }

    [JsonPropertyName("parentId")]
    public long? ParentId { get; set; }

    [JsonPropertyName("labels")]
    public List<string>? Labels { get; set; }

    [JsonPropertyName("dueDate")]
    public string? DueDate { get; set; }

    [JsonPropertyName("requirementRefs")]
    public List<RequirementRefInputDto>? RequirementRefs { get; set; }
}

public class UpdateIssueRequest
{
    private int? _points;
    private long? _assigneeId;
    private long? _departmentId;
    private long? _sprintId;
    private string? _dueDate;

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("priority")]
    public string? Priority { get; set; }

    [JsonPropertyName("points")]
    public int? Points
    {
        get => _points;
        set { _points = value; PointsSpecified = true; }
    }
    [JsonIgnore] public bool PointsSpecified { get; private set; }

    [JsonPropertyName("assigneeId")]
    public long? AssigneeId
    {
        get => _assigneeId;
        set { _assigneeId = value; AssigneeIdSpecified = true; }
    }
    [JsonIgnore] public bool AssigneeIdSpecified { get; private set; }

    [JsonPropertyName("departmentId")]
    public long? DepartmentId
    {
        get => _departmentId;
        set { _departmentId = value; DepartmentIdSpecified = true; }
    }
    [JsonIgnore] public bool DepartmentIdSpecified { get; private set; }

    [JsonPropertyName("sprintId")]
    public long? SprintId
    {
        get => _sprintId;
        set { _sprintId = value; SprintIdSpecified = true; }
    }
    [JsonIgnore] public bool SprintIdSpecified { get; private set; }

    [JsonPropertyName("labels")]
    public List<string>? Labels { get; set; }

    [JsonPropertyName("dueDate")]
    public string? DueDate
    {
        get => _dueDate;
        set { _dueDate = value; DueDateSpecified = true; }
    }
    [JsonIgnore] public bool DueDateSpecified { get; private set; }
}

public class IssueRankPositionInputDto
{
    [JsonPropertyName("beforeIssueId")]
    public long? BeforeIssueId { get; set; }

    [JsonPropertyName("afterIssueId")]
    public long? AfterIssueId { get; set; }
}

public class TransitionIssueRequest
{
    [JsonPropertyName("toStatusId")]
    public long ToStatusId { get; set; }

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    [JsonPropertyName("rank")]
    public IssueRankPositionInputDto? Rank { get; set; }

    [JsonPropertyName("rankAfterIssueId")]
    public long? RankAfterIssueId { get; set; }
}

public class RankIssueRequest
{
    private long? _sprintId;

    [JsonPropertyName("beforeIssueId")]
    public long? BeforeIssueId { get; set; }

    [JsonPropertyName("afterIssueId")]
    public long? AfterIssueId { get; set; }

    [JsonPropertyName("sprintId")]
    public long? SprintId
    {
        get => _sprintId;
        set { _sprintId = value; SprintIdSpecified = true; }
    }
    [JsonIgnore] public bool SprintIdSpecified { get; private set; }
}

public class CreateIssueFromRequirementItemDto
{
    [JsonPropertyName("documentId")]
    public long DocumentId { get; set; }

    [JsonPropertyName("reqUid")]
    public string ReqUid { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("priority")]
    public string? Priority { get; set; }

    [JsonPropertyName("points")]
    public int? Points { get; set; }

    [JsonPropertyName("sprintId")]
    public long? SprintId { get; set; }

    [JsonPropertyName("assigneeId")]
    public long? AssigneeId { get; set; }

    [JsonPropertyName("createSubtasks")]
    public bool? CreateSubtasks { get; set; }
}

public class CreateIssuesFromRequirementsRequest
{
    [JsonPropertyName("items")]
    public List<CreateIssueFromRequirementItemDto> Items { get; set; } = new();

    [JsonPropertyName("refs")]
    public List<RequirementRefInputDto>? Refs { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("sprintId")]
    public long? SprintId { get; set; }
}

public class UpdateIssueRequirementsRequest
{
    [JsonPropertyName("refs")]
    public List<RequirementRefInputDto> Refs { get; set; } = new();
}

public class CreateIssueCommentRequest
{
    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    [JsonPropertyName("parentId")]
    public long? ParentId { get; set; }

    [JsonPropertyName("mentionUserIds")]
    public List<long>? MentionUserIds { get; set; }
}

public class CreateSprintRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("goal")]
    public string? Goal { get; set; }

    [JsonPropertyName("startDate")]
    public string? StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public string? EndDate { get; set; }

    [JsonPropertyName("capacityPoints")]
    public int? CapacityPoints { get; set; }
}

public class UpdateSprintRequest
{
    private string? _goal;
    private string? _startDate;
    private string? _endDate;
    private int? _capacityPoints;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("goal")]
    public string? Goal
    {
        get => _goal;
        set { _goal = value; GoalSpecified = true; }
    }
    [JsonIgnore] public bool GoalSpecified { get; private set; }

    [JsonPropertyName("startDate")]
    public string? StartDate
    {
        get => _startDate;
        set { _startDate = value; StartDateSpecified = true; }
    }
    [JsonIgnore] public bool StartDateSpecified { get; private set; }

    [JsonPropertyName("endDate")]
    public string? EndDate
    {
        get => _endDate;
        set { _endDate = value; EndDateSpecified = true; }
    }
    [JsonIgnore] public bool EndDateSpecified { get; private set; }

    [JsonPropertyName("capacityPoints")]
    public int? CapacityPoints
    {
        get => _capacityPoints;
        set { _capacityPoints = value; CapacityPointsSpecified = true; }
    }
    [JsonIgnore] public bool CapacityPointsSpecified { get; private set; }
}

public class StartSprintRequest
{
    [JsonPropertyName("startDate")]
    public string? StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public string? EndDate { get; set; }

    [JsonPropertyName("goal")]
    public string? Goal { get; set; }
}

public class CompleteSprintRequest
{
    [JsonPropertyName("moveOpenIssuesTo")]
    public JsonElement? MoveOpenIssuesTo { get; set; }
}

public class IssueListQuery
{
    public string? SprintId { get; set; }
    public string? AssigneeId { get; set; }
    public string? Type { get; set; }
    public long? StatusId { get; set; }
    public string? ReqUid { get; set; }
    public bool? ReqChanged { get; set; }
    public string? Search { get; set; }
    public string? ParentId { get; set; }
}
