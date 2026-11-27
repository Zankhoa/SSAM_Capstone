namespace SSAM_Server.Models;

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
