namespace SSAM_Server.Models;

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
