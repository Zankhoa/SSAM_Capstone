using SSAM_Server.Models;

namespace SSAM_Server.Interfaces.Repositories;

public interface IWorkRepository : IRepositoryBase
{
    IQueryable<Project> Projects { get; }
    IQueryable<ProjectMember> ProjectMembers { get; }
    IQueryable<DocumentNode> DocumentNodes { get; }
    IQueryable<DocumentComment> DocumentComments { get; }
    IQueryable<DocumentActivity> DocumentActivities { get; }
    IQueryable<RequirementItem> RequirementItems { get; }
    IQueryable<Issue> Issues { get; }
    IQueryable<TestPlan> TestPlans { get; }
    IQueryable<TestCase> TestCases { get; }
    IQueryable<TestCaseRequirementLink> TestCaseRequirementLinks { get; }
}
