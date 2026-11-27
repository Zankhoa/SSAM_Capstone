namespace SSAM_Server.Models;

public class TestCaseFigmaLink
{
    public long TestCaseId { get; set; }
    public TestCase TestCase { get; set; } = null!;

    public string FileKey { get; set; } = string.Empty;
    public string? NodeId { get; set; }
    public string Url { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
