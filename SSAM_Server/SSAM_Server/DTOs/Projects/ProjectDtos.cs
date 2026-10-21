using System.Text.Json;
using System.Text.Json.Serialization;
using SSAM_Server.DTOs.Common;

namespace SSAM_Server.DTOs.Projects;

// --- SDLC Templates (§9.1) ---

public class TemplateStatusDto
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public string Color { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("isInitial")]
    public bool IsInitial { get; set; }
}

public class TemplateTransitionDto
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("allowedRoleCodes")]
    public List<string> AllowedRoleCodes { get; set; } = new();

    [JsonPropertyName("requireComment")]
    public bool RequireComment { get; set; }

    [JsonPropertyName("commentHint")]
    public string? CommentHint { get; set; }

    [JsonPropertyName("condition")]
    public string? Condition { get; set; }
}

public class TemplatePhaseDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("statuses")]
    public List<TemplateStatusDto> Statuses { get; set; } = new();

    [JsonPropertyName("transitions")]
    public List<TemplateTransitionDto> Transitions { get; set; } = new();
}

public class SdlcTemplateDto
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("phasesEditable")]
    public bool PhasesEditable { get; set; }

    [JsonPropertyName("phases")]
    public List<TemplatePhaseDto> Phases { get; set; } = new();
}

// --- Projects (§10) ---

public class ProjectDto
{
    [JsonPropertyName("projectId")]
    public long ProjectId { get; set; }

    [JsonPropertyName("workspaceId")]
    public long WorkspaceId { get; set; }

    [JsonPropertyName("workspaceName")]
    public string WorkspaceName { get; set; } = string.Empty;

    [JsonPropertyName("companyId")]
    public long? CompanyId { get; set; }

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("sdlcMethod")]
    public string SdlcMethod { get; set; } = string.Empty;

    [JsonPropertyName("aiEnabled")]
    public bool AiEnabled { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "ACTIVE";

    [JsonPropertyName("lead")]
    public UserRefDto? Lead { get; set; }

    [JsonPropertyName("myRole")]
    public RoleRefDto? MyRole { get; set; }

    [JsonPropertyName("myPermissions")]
    public List<string> MyPermissions { get; set; } = new();

    [JsonPropertyName("memberCount")]
    public int MemberCount { get; set; }

    [JsonPropertyName("phaseCount")]
    public int PhaseCount { get; set; }

    [JsonPropertyName("openIssueCount")]
    public int OpenIssueCount { get; set; }

    [JsonPropertyName("doneIssueCount")]
    public int DoneIssueCount { get; set; }

    [JsonPropertyName("documentCount")]
    public int DocumentCount { get; set; }

    [JsonPropertyName("createdBy")]
    public UserRefDto CreatedBy { get; set; } = null!;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("archivedAt")]
    public DateTime? ArchivedAt { get; set; }
}

public class ProjectKeyAvailabilityDto
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("available")]
    public bool Available { get; set; }

    [JsonPropertyName("suggestion")]
    public string? Suggestion { get; set; }
}

public class StatusInputDto
{
    [JsonPropertyName("statusId")]
    public long? StatusId { get; set; }

    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("isInitial")]
    public bool IsInitial { get; set; }

    [JsonPropertyName("isActive")]
    public bool? IsActive { get; set; }

    [JsonIgnore]
    public string EffectiveKey => StatusId.HasValue ? StatusId.Value.ToString() : (Key ?? string.Empty);
}

public class TransitionInputDto
{
    [JsonPropertyName("from")]
    public JsonElement From { get; set; }

    [JsonPropertyName("to")]
    public JsonElement To { get; set; }

    [JsonPropertyName("allowedRoleIds")]
    public List<long> AllowedRoleIds { get; set; } = new();

    [JsonPropertyName("requireComment")]
    public bool RequireComment { get; set; }

    [JsonPropertyName("commentHint")]
    public string? CommentHint { get; set; }

    [JsonPropertyName("condition")]
    public string? Condition { get; set; }

    [JsonIgnore]
    public string FromKey => ElementToKey(From);

    [JsonIgnore]
    public string ToKey => ElementToKey(To);

    private static string ElementToKey(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Number => el.GetInt64().ToString(),
        JsonValueKind.String => el.GetString() ?? string.Empty,
        _ => string.Empty
    };
}

public class CreateProjectPhaseInputDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("statuses")]
    public List<StatusInputDto> Statuses { get; set; } = new();

    [JsonPropertyName("transitions")]
    public List<TransitionInputDto> Transitions { get; set; } = new();
}

public class ProjectMemberInputDto
{
    [JsonPropertyName("userId")]
    public long UserId { get; set; }

    [JsonPropertyName("roleId")]
    public long RoleId { get; set; }
}

public class CreateProjectRequestDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("sdlcMethod")]
    public string SdlcMethod { get; set; } = string.Empty;

    [JsonPropertyName("aiEnabled")]
    public bool? AiEnabled { get; set; }

    [JsonPropertyName("phases")]
    public List<CreateProjectPhaseInputDto> Phases { get; set; } = new();

    [JsonPropertyName("members")]
    public List<ProjectMemberInputDto>? Members { get; set; }
}

public class UpdateProjectRequestDto
{
    private string? _description;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description
    {
        get => _description;
        set
        {
            _description = value;
            DescriptionSpecified = true;
        }
    }

    [JsonIgnore]
    public bool DescriptionSpecified { get; private set; }

    [JsonPropertyName("aiEnabled")]
    public bool? AiEnabled { get; set; }
}

// --- Project Members (§11) ---

public class ProjectMemberDto : UserRefDto
{
    [JsonPropertyName("role")]
    public RoleRefDto Role { get; set; } = null!;

    [JsonPropertyName("workspaceRole")]
    public RoleRefDto WorkspaceRole { get; set; } = null!;

    [JsonPropertyName("joinedAt")]
    public DateTime JoinedAt { get; set; }
}

public class ProjectMemberCandidateDto : UserRefDto
{
    [JsonPropertyName("workspaceRole")]
    public RoleRefDto WorkspaceRole { get; set; } = null!;
}

public class AddProjectMembersRequestDto
{
    [JsonPropertyName("members")]
    public List<ProjectMemberInputDto> Members { get; set; } = new();
}

public class UpdateProjectMemberRoleRequestDto
{
    [JsonPropertyName("roleId")]
    public long RoleId { get; set; }
}

// --- Workflow (§12) ---

public class WorkflowStatusDto
{
    [JsonPropertyName("statusId")]
    public long StatusId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("isInitial")]
    public bool IsInitial { get; set; }

    [JsonPropertyName("position")]
    public int Position { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("issueCount")]
    public int IssueCount { get; set; }
}

public class WorkflowTransitionDto
{
    [JsonPropertyName("transitionId")]
    public long TransitionId { get; set; }

    [JsonPropertyName("fromStatusId")]
    public long FromStatusId { get; set; }

    [JsonPropertyName("toStatusId")]
    public long ToStatusId { get; set; }

    [JsonPropertyName("allowedRoles")]
    public List<RoleRefDto> AllowedRoles { get; set; } = new();

    [JsonPropertyName("requireComment")]
    public bool RequireComment { get; set; }

    [JsonPropertyName("commentHint")]
    public string? CommentHint { get; set; }

    [JsonPropertyName("condition")]
    public string? Condition { get; set; }
}

public class PhaseDto
{
    [JsonPropertyName("phaseId")]
    public long PhaseId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("position")]
    public int Position { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("statuses")]
    public List<WorkflowStatusDto> Statuses { get; set; } = new();

    [JsonPropertyName("transitions")]
    public List<WorkflowTransitionDto> Transitions { get; set; } = new();
}

public class WorkflowDto
{
    [JsonPropertyName("projectId")]
    public long ProjectId { get; set; }

    [JsonPropertyName("sdlcMethod")]
    public string SdlcMethod { get; set; } = string.Empty;

    [JsonPropertyName("phasesEditable")]
    public bool PhasesEditable { get; set; }

    [JsonPropertyName("phases")]
    public List<PhaseDto> Phases { get; set; } = new();
}

public class CreatePhaseRequestDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class UpdatePhaseRequestDto
{
    private string? _description;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description
    {
        get => _description;
        set
        {
            _description = value;
            DescriptionSpecified = true;
        }
    }

    [JsonIgnore]
    public bool DescriptionSpecified { get; private set; }

    [JsonPropertyName("position")]
    public int? Position { get; set; }

    [JsonPropertyName("isActive")]
    public bool? IsActive { get; set; }
}

public class UpdatePhaseWorkflowRequestDto
{
    [JsonPropertyName("statuses")]
    public List<StatusInputDto> Statuses { get; set; } = new();

    [JsonPropertyName("transitions")]
    public List<TransitionInputDto> Transitions { get; set; } = new();
}

public class WorkflowIssueDto
{
    [JsonPropertyName("phaseIndex")]
    public int PhaseIndex { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("statusKey")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StatusKey { get; set; }

    [JsonPropertyName("transitionIndex")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TransitionIndex { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
