namespace SSAM_Server.Models;

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
