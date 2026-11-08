namespace SSAM_Server.Models;

public static class TestCaseTypes
{
    public const string Functional = "FUNCTIONAL";
    public const string Integration = "INTEGRATION";
    public const string Security = "SECURITY";
    public const string E2E = "E2E";
    public const string Unit = "UNIT";
    public const string Regression = "REGRESSION";

    public static bool IsValid(string? type) =>
        type is Functional or Integration or Security or E2E or Unit or Regression;
}

public static class TestCasePriorities
{
    public const string High = "HIGH";
    public const string Medium = "MEDIUM";
    public const string Low = "LOW";

    public static bool IsValid(string? priority) =>
        priority is High or Medium or Low;
}

public static class TestCycleStatuses
{
    public const string Open = "OPEN";
    public const string Closed = "CLOSED";

    public static bool IsValid(string? status) =>
        status is Open or Closed;
}

public static class TestRunResults
{
    public const string NotRun = "NOT_RUN";
    public const string Pass = "PASS";
    public const string Fail = "FAIL";
    public const string Blocked = "BLOCKED";

    public static bool IsValid(string? result) =>
        result is NotRun or Pass or Fail or Blocked;
}

public class TestFolder
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public long? ParentId { get; set; }
    public TestFolder? Parent { get; set; }
    public ICollection<TestFolder> Children { get; set; } = new List<TestFolder>();

    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TestCase> TestCases { get; set; } = new List<TestCase>();
}

public class TestCase : IHasUpdatedAt
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = TestCaseTypes.Functional;
    public string Priority { get; set; } = TestCasePriorities.Medium;
    public string Preconditions { get; set; } = string.Empty;
    public string Steps { get; set; } = "[]";
    public int StepCount { get; set; } = 0;

    public long? FolderId { get; set; }
    public TestFolder? Folder { get; set; }

    public bool NeedsReview { get; set; } = false;
    public string? LastResult { get; set; }
    public DateTime? LastRunAt { get; set; }
    public long? LastRunById { get; set; }
    public User? LastRunBy { get; set; }

    public long CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;

    public ICollection<TestCaseRequirementLink> RequirementLinks { get; set; } = new List<TestCaseRequirementLink>();
    public ICollection<TestCaseIssueLink> IssueLinks { get; set; } = new List<TestCaseIssueLink>();
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

public class TestCaseIssueLink
{
    public long TestCaseId { get; set; }
    public TestCase TestCase { get; set; } = null!;

    public long IssueId { get; set; }
    public Issue Issue { get; set; } = null!;

    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;
}

public class TestCycle : IHasUpdatedAt
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public long? SprintId { get; set; }
    public Sprint? Sprint { get; set; }

    public string Status { get; set; } = TestCycleStatuses.Open;

    public long CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TestRun> Runs { get; set; } = new List<TestRun>();
}

public class TestRun
{
    public long Id { get; set; }
    public long CycleId { get; set; }
    public TestCycle Cycle { get; set; } = null!;

    public long TestCaseId { get; set; }
    public TestCase TestCase { get; set; } = null!;

    public long? AssigneeId { get; set; }
    public User? Assignee { get; set; }

    public string Result { get; set; } = TestRunResults.NotRun;
    public string? Note { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public long? ExecutedById { get; set; }
    public User? ExecutedBy { get; set; }

    public bool NeedsRerun { get; set; } = false;
    public byte[] RowVer { get; set; } = Array.Empty<byte>();

    public ICollection<TestRunDefect> Defects { get; set; } = new List<TestRunDefect>();
    public ICollection<TestRunHistory> History { get; set; } = new List<TestRunHistory>();
}

public class TestRunDefect
{
    public long RunId { get; set; }
    public TestRun Run { get; set; } = null!;

    public long IssueId { get; set; }
    public Issue Issue { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class TestRunHistory
{
    public long Id { get; set; }
    public long RunId { get; set; }
    public TestRun Run { get; set; } = null!;

    public long TestCaseId { get; set; }
    public TestCase TestCase { get; set; } = null!;

    public string Result { get; set; } = string.Empty;
    public string? Note { get; set; }

    public long? DefectIssueId { get; set; }
    public Issue? DefectIssue { get; set; }

    public long ExecutedById { get; set; }
    public User ExecutedBy { get; set; } = null!;

    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}
