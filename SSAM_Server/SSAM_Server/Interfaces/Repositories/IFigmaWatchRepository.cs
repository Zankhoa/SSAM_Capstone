using SSAM_Server.Models;

namespace SSAM_Server.Interfaces.Repositories;

public interface IFigmaWatchRepository : IRepositoryBase
{
    IQueryable<FigmaFileWatch> FigmaFileWatches { get; }
    IQueryable<TestCaseFigmaLink> TestCaseFigmaLinks { get; }
    IQueryable<TestCase> TestCases { get; }
    IQueryable<TestCaseRequirementLink> TestCaseRequirementLinks { get; }
    IQueryable<TestResult> TestResults { get; }
    IQueryable<TestRetestFlag> TestRetestFlags { get; }
    IQueryable<TestAssignment> TestAssignments { get; }
    IQueryable<TestActivity> TestActivities { get; }
    IQueryable<DocumentNode> DocumentNodes { get; }
    IQueryable<ProjectMember> ProjectMembers { get; }
}
