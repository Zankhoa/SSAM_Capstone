namespace SSAM_Server.Models;

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
