namespace SSAM_Server.Models;

public static class TestCasePriorities
{
    public const string High = "HIGH";
    public const string Medium = "MEDIUM";
    public const string Low = "LOW";

    public const string P0 = "P0";
    public const string P1 = "P1";
    public const string P2 = "P2";
    public const string P3 = "P3";

    public static bool IsValid(string? priority) =>
        priority is High or Medium or Low or P0 or P1 or P2 or P3;

    public static string Normalize(string? priority) =>
        (priority?.Trim().ToUpperInvariant()) switch
        {
            "HIGH" or "P0" => P0,
            "LOW" or "P2" => P2,
            "P3" => P3,
            "MEDIUM" or "P1" => P1,
            _ => P1
        };
}

public static class TestCaseCategories
{
    public const string Functional = "FUNCTIONAL";
    public const string Boundary = "BOUNDARY";
    public const string Negative = "NEGATIVE";
    public const string Ui = "UI";
    public const string Security = "SECURITY";
    public const string Performance = "PERFORMANCE";

    public static bool IsValid(string? category) => true;
}

public static class TestCaseStatuses
{
    public const string Active = "ACTIVE";
    public const string Deleted = "DELETED";

    public static bool IsValid(string? status) =>
        status is Active or Deleted;
}

public class TestCase : IHasUpdatedAt
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public long PlanId { get; set; }
    public TestPlan Plan { get; set; } = null!;

    public long ModuleId { get; set; }
    public TestModule Module { get; set; } = null!;

    public long SectionId { get; set; }
    public TestSection Section { get; set; } = null!;

    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Preconditions { get; set; } = string.Empty;
    public string Steps { get; set; } = "[]";
    public string Actual { get; set; } = string.Empty;
    public bool AiSuggestedReqs { get; set; } = false;
    public string Priority { get; set; } = TestCasePriorities.P1;
    public string Category { get; set; } = TestCaseCategories.Functional;
    public string Browser { get; set; } = string.Empty;
    public string CustomFields { get; set; } = "{}";
    public string CellStyles { get; set; } = "{}";
    public string Status { get; set; } = TestCaseStatuses.Active;
    public int Position { get; set; } = 0;

    public long? BugIssueId { get; set; }
    public Issue? BugIssue { get; set; }

    public long CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TestCaseRequirementLink> RequirementLinks { get; set; } = new List<TestCaseRequirementLink>();
    public ICollection<TestCaseFigmaLink> FigmaLinks { get; set; } = new List<TestCaseFigmaLink>();
    public ICollection<TestResult> Results { get; set; } = new List<TestResult>();
    public ICollection<TestRetestFlag> RetestFlags { get; set; } = new List<TestRetestFlag>();
}
