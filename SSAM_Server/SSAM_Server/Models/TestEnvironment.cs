namespace SSAM_Server.Models;

public class TestEnvironment
{
    public long Id { get; set; }
    public long PlanId { get; set; }
    public TestPlan Plan { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public int Position { get; set; } = 0;
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TestBuild> Builds { get; set; } = new List<TestBuild>();
    public ICollection<TestAssignment> Assignments { get; set; } = new List<TestAssignment>();
    public ICollection<TestResult> Results { get; set; } = new List<TestResult>();
    public ICollection<TestRetestFlag> RetestFlags { get; set; } = new List<TestRetestFlag>();
}
