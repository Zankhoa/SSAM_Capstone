namespace SSAM_Server.Models;

public class WorkflowTransitionRole
{
    public long TransitionId { get; set; }
    public WorkflowTransition Transition { get; set; } = null!;

    public long RoleId { get; set; }
    public CompanyRole Role { get; set; } = null!;
}
