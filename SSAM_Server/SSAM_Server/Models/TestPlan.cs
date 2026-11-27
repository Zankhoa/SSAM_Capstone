namespace SSAM_Server.Models;

public static class TestPlanStatuses
{
    public const string Open = "OPEN";
    public const string HandedOver = "HANDED_OVER";

    public static bool IsValid(string? status) =>
        status is Open or HandedOver;
}

public class TestPlan : IHasUpdatedAt
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Status { get; set; } = TestPlanStatuses.Open;

    public long? HandoverById { get; set; }
    public User? HandoverBy { get; set; }
    public DateTime? HandoverAt { get; set; }
    public string? HandoverNote { get; set; }

    public string? SheetLayout { get; set; }

    public long CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TestEnvironment> Environments { get; set; } = new List<TestEnvironment>();
    public ICollection<TestModule> Modules { get; set; } = new List<TestModule>();
    public ICollection<TestCase> TestCases { get; set; } = new List<TestCase>();
    public ICollection<TestActivity> Activities { get; set; } = new List<TestActivity>();
}
