namespace SSAM_Server.Models;

public class WorkflowTransition
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public long PhaseId { get; set; }
    public ProjectPhase Phase { get; set; } = null!;

    public long FromStatusId { get; set; }
    public WorkflowStatus FromStatus { get; set; } = null!;

    public long ToStatusId { get; set; }
    public WorkflowStatus ToStatus { get; set; } = null!;

    public bool RequireComment { get; set; } = false;
    public string? CommentHint { get; set; }
    public string? Condition { get; set; }

    public ICollection<WorkflowTransitionRole> AllowedRoles { get; set; } = new List<WorkflowTransitionRole>();
}
