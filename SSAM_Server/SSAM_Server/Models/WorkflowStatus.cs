namespace SSAM_Server.Models;

public static class StatusCategories
{
    public const string Todo = "TODO";
    public const string InProgress = "IN_PROGRESS";
    public const string Done = "DONE";

    public static bool IsValid(string? category) =>
        category is Todo or InProgress or Done;
}

public class WorkflowStatus
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public long PhaseId { get; set; }
    public ProjectPhase Phase { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = StatusCategories.Todo;
    public string? Color { get; set; }
    public string? Description { get; set; }
    public bool IsInitial { get; set; } = false;
    public int Position { get; set; }
    public bool IsActive { get; set; } = true;
}
