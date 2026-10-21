namespace SSAM_Server.Models;

public static class SdlcMethods
{
    public const string Scrum = "SCRUM";
    public const string Kanban = "KANBAN";
    public const string Custom = "CUSTOM";

    public static bool IsValid(string? method) =>
        method is Scrum or Kanban or Custom;
}

public class Project : IHasUpdatedAt
{
    public long Id { get; set; }
    public long WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SdlcMethod { get; set; } = SdlcMethods.Scrum;
    public bool AiEnabled { get; set; } = true;

    public long CreatedBy { get; set; }
    public User Creator { get; set; } = null!;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }

    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
    public ICollection<ProjectPhase> Phases { get; set; } = new List<ProjectPhase>();
    public ICollection<WorkflowStatus> WorkflowStatuses { get; set; } = new List<WorkflowStatus>();
    public ICollection<WorkflowTransition> WorkflowTransitions { get; set; } = new List<WorkflowTransition>();
}
