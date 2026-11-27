namespace SSAM_Server.Models;

public class TestAssignment
{
    public long ModuleId { get; set; }
    public TestModule Module { get; set; } = null!;

    public long EnvId { get; set; }
    public TestEnvironment Environment { get; set; } = null!;

    public long AssigneeId { get; set; }
    public User Assignee { get; set; } = null!;
}
