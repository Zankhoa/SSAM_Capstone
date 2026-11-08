namespace SSAM_Server.Models;

public static class TestPlanStatuses
{
    public const string Open = "OPEN";
    public const string HandedOver = "HANDED_OVER";

    public static bool IsValid(string? status) =>
        status is Open or HandedOver;
}

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
            "HIGH" or "P0" => High,
            "LOW" or "P2" or "P3" => Low,
            "MEDIUM" or "P1" => Medium,
            _ => Medium
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

public static class TestResultValues
{
    public const string Pass = "PASS";
    public const string Fail = "FAIL";
    public const string Na = "NA";
    public const string Untest = "UNTEST";

    public static bool IsValid(string? result) =>
        result is Pass or Fail or Na or Untest;
}

public static class TestRetestReasons
{
    public const string BugFixed = "BUG_FIXED";
    public const string ReqChanged = "REQ_CHANGED";
    public const string Regression = "REGRESSION";
    public const string Manual = "REGRESSION";

    public static bool IsValid(string? reason) =>
        reason is BugFixed or ReqChanged or Regression or "MANUAL";
}

public static class TestActivityTypes
{
    public const string Result = "RESULT";
    public const string Assign = "ASSIGN";
    public const string Bug = "BUG";
    public const string Req = "REQ";
    public const string TestCase = "TESTCASE";
    public const string Build = "BUILD";
    public const string Setup = "SETUP";
    public const string Handover = "HANDOVER";

    public const string Deploy = "BUILD";
    public const string Case = "TESTCASE";
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

public class TestBuild
{
    public long Id { get; set; }
    public long EnvId { get; set; }
    public TestEnvironment Environment { get; set; } = null!;

    public string Version { get; set; } = string.Empty;
    public DateTime DeployedAt { get; set; } = DateTime.UtcNow;
    public long DeployedById { get; set; }
    public User DeployedBy { get; set; } = null!;
    public string FixedIssueIds { get; set; } = "[]";
}

public class TestModule
{
    public long Id { get; set; }
    public long PlanId { get; set; }
    public TestPlan Plan { get; set; } = null!;

    public long? DocumentId { get; set; }
    public DocumentNode? Document { get; set; }

    public string Name { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public int Position { get; set; } = 0;
    public int CaseSeq { get; set; } = 0;

    public ICollection<TestSection> Sections { get; set; } = new List<TestSection>();
    public ICollection<TestAssignment> Assignments { get; set; } = new List<TestAssignment>();
    public ICollection<TestCase> TestCases { get; set; } = new List<TestCase>();
}

public class TestSection
{
    public long Id { get; set; }
    public long ModuleId { get; set; }
    public TestModule Module { get; set; } = null!;

    public long? ParentId { get; set; }
    public TestSection? Parent { get; set; }
    public ICollection<TestSection> Children { get; set; } = new List<TestSection>();

    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? AnchorId { get; set; }
    public int Position { get; set; } = 0;

    public ICollection<TestCase> TestCases { get; set; } = new List<TestCase>();
}

public class TestAssignment
{
    public long ModuleId { get; set; }
    public TestModule Module { get; set; } = null!;

    public long EnvId { get; set; }
    public TestEnvironment Environment { get; set; } = null!;

    public long AssigneeId { get; set; }
    public User Assignee { get; set; } = null!;
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
    public string Status { get; set; } = TestCaseStatuses.Active;
    public int Position { get; set; } = 0;

    public long? BugIssueId { get; set; }
    public Issue? BugIssue { get; set; }

    public long CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TestCaseRequirementLink> RequirementLinks { get; set; } = new List<TestCaseRequirementLink>();
    public ICollection<TestResult> Results { get; set; } = new List<TestResult>();
    public ICollection<TestRetestFlag> RetestFlags { get; set; } = new List<TestRetestFlag>();
}

public class TestCaseRequirementLink
{
    public long TestCaseId { get; set; }
    public TestCase TestCase { get; set; } = null!;

    public long DocumentId { get; set; }
    public DocumentNode Document { get; set; } = null!;

    public string ReqUid { get; set; } = string.Empty;
    public string ReqHash { get; set; } = string.Empty;
    public string? ChangedVersionLabel { get; set; }
    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;
}

public class TestResult
{
    public long Id { get; set; }
    public long TestCaseId { get; set; }
    public TestCase TestCase { get; set; } = null!;

    public long EnvId { get; set; }
    public TestEnvironment Environment { get; set; } = null!;

    public string Build { get; set; } = string.Empty;
    public string Result { get; set; } = TestResultValues.Untest;

    public long ById { get; set; }
    public User By { get; set; } = null!;

    public DateTime At { get; set; } = DateTime.UtcNow;

    public long? BehalfOfId { get; set; }
    public User? BehalfOf { get; set; }
}

public class TestRetestFlag
{
    public long TestCaseId { get; set; }
    public TestCase TestCase { get; set; } = null!;

    public long EnvId { get; set; }
    public TestEnvironment Environment { get; set; } = null!;

    public string Reason { get; set; } = TestRetestReasons.Manual;

    public long? BugIssueId { get; set; }
    public Issue? BugIssue { get; set; }

    public string? Build { get; set; }
    public string? ReqKey { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public class TestActivity
{
    public long Id { get; set; }
    public long PlanId { get; set; }
    public TestPlan Plan { get; set; } = null!;

    public string Type { get; set; } = TestActivityTypes.Result;
    public DateTime At { get; set; } = DateTime.UtcNow;

    public long? ActorId { get; set; }
    public User? Actor { get; set; }

    public long? EnvId { get; set; }
    public TestEnvironment? Environment { get; set; }

    public long? ModuleId { get; set; }
    public TestModule? Module { get; set; }

    public long? TestCaseId { get; set; }
    public TestCase? TestCase { get; set; }

    public string? FromStatus { get; set; }
    public string? ToResult { get; set; }
    public string? Build { get; set; }

    public long? BehalfOfId { get; set; }
    public User? BehalfOf { get; set; }

    public string? Text { get; set; }
}
