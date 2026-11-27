namespace SSAM_Server.Models;

public static class TestRetestReasons
{
    public const string BugFixed = "BUG_FIXED";
    public const string ReqChanged = "REQ_CHANGED";
    public const string DesignChanged = "DESIGN_CHANGED";
    public const string Regression = "REGRESSION";
    public const string Manual = "REGRESSION";

    public static bool IsValid(string? reason) =>
        reason is BugFixed or ReqChanged or DesignChanged or Regression or "MANUAL";
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
