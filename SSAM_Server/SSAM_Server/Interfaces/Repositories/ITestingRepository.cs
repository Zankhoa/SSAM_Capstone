using SSAM_Server.Models;

namespace SSAM_Server.Interfaces.Repositories;

public interface ITestingRepository : IRepositoryBase
{
    IQueryable<Project> Projects { get; }
    IQueryable<ProjectMember> ProjectMembers { get; }
    IQueryable<User> Users { get; }
    IQueryable<WorkflowStatus> WorkflowStatuses { get; }
    IQueryable<Issue> Issues { get; }
    IQueryable<IssueActivity> IssueActivities { get; }
    IQueryable<DocumentNode> DocumentNodes { get; }
    IQueryable<DocumentVersion> DocumentVersions { get; }
    IQueryable<RequirementItem> RequirementItems { get; }
    IQueryable<TestPlan> TestPlans { get; }
    IQueryable<TestEnvironment> TestEnvironments { get; }
    IQueryable<TestBuild> TestBuilds { get; }
    IQueryable<TestModule> TestModules { get; }
    IQueryable<TestSection> TestSections { get; }
    IQueryable<TestAssignment> TestAssignments { get; }
    IQueryable<TestCase> TestCases { get; }
    IQueryable<TestCaseRequirementLink> TestCaseRequirementLinks { get; }
    IQueryable<TestResult> TestResults { get; }
    IQueryable<TestRetestFlag> TestRetestFlags { get; }
    IQueryable<TestActivity> TestActivities { get; }
}
