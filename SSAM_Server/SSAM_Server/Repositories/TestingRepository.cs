using SSAM_Server.Data;
using SSAM_Server.Interfaces.Repositories;
using SSAM_Server.Models;

namespace SSAM_Server.Repositories;

public class TestingRepository : RepositoryBase, ITestingRepository
{
    public TestingRepository(AppDbContext db) : base(db) { }

    public IQueryable<Project> Projects => _db.Projects;
    public IQueryable<ProjectMember> ProjectMembers => _db.ProjectMembers;
    public IQueryable<User> Users => _db.Users;
    public IQueryable<WorkflowStatus> WorkflowStatuses => _db.WorkflowStatuses;
    public IQueryable<Issue> Issues => _db.Issues;
    public IQueryable<IssueActivity> IssueActivities => _db.IssueActivities;
    public IQueryable<DocumentNode> DocumentNodes => _db.DocumentNodes;
    public IQueryable<DocumentVersion> DocumentVersions => _db.DocumentVersions;
    public IQueryable<RequirementItem> RequirementItems => _db.RequirementItems;
    public IQueryable<TestPlan> TestPlans => _db.TestPlans;
    public IQueryable<TestEnvironment> TestEnvironments => _db.TestEnvironments;
    public IQueryable<TestBuild> TestBuilds => _db.TestBuilds;
    public IQueryable<TestModule> TestModules => _db.TestModules;
    public IQueryable<TestSection> TestSections => _db.TestSections;
    public IQueryable<TestAssignment> TestAssignments => _db.TestAssignments;
    public IQueryable<TestCase> TestCases => _db.TestCases;
    public IQueryable<TestCaseRequirementLink> TestCaseRequirementLinks => _db.TestCaseRequirementLinks;
    public IQueryable<TestResult> TestResults => _db.TestResults;
    public IQueryable<TestRetestFlag> TestRetestFlags => _db.TestRetestFlags;
    public IQueryable<TestActivity> TestActivities => _db.TestActivities;
}
