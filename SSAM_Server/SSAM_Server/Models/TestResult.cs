namespace SSAM_Server.Models;

public static class TestResultValues
{
    public const string Pass = "PASS";
    public const string Fail = "FAIL";
    public const string Na = "NA";
    public const string Untest = "UNTEST";

    public static bool IsValid(string? result) =>
        result is Pass or Fail or Na or Untest;
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
