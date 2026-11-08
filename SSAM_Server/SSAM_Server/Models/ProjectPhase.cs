namespace SSAM_Server.Models;

public class ProjectPhase : IHasUpdatedAt
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Position { get; set; }
    public bool IsActive { get; set; } = true;
    public string ExitCriteria { get; set; } = "[]";
    public string LifecycleStatus { get; set; } = "UPCOMING";
    public DateTime? CompletedAt { get; set; }
    public long? CompletedById { get; set; }
    public User? CompletedBy { get; set; }
    public string? OverrideReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WorkflowStatus> Statuses { get; set; } = new List<WorkflowStatus>();
    public ICollection<WorkflowTransition> Transitions { get; set; } = new List<WorkflowTransition>();
    public ICollection<ProjectPhaseLog> Logs { get; set; } = new List<ProjectPhaseLog>();
}

public class ProjectPhaseLog
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public long PhaseId { get; set; }
    public string Action { get; set; } = null!; // COMPLETE | OVERRIDE_COMPLETE | REOPEN
    public long ActorId { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string? UnmetCriteria { get; set; }
    public string? Reason { get; set; }

    public Project Project { get; set; } = null!;
    public ProjectPhase Phase { get; set; } = null!;
    public User Actor { get; set; } = null!;
}
