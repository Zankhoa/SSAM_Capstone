namespace SSAM_Server.Models;

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
