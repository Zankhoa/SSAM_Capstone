using SSAM_Server.Data;
using SSAM_Server.Interfaces.Repositories;
using SSAM_Server.Models;

namespace SSAM_Server.Repositories;

public class FigmaWatchRepository : RepositoryBase, IFigmaWatchRepository
{
    public FigmaWatchRepository(AppDbContext db) : base(db) { }

    public IQueryable<FigmaFileWatch> FigmaFileWatches => _db.FigmaFileWatches;
    public IQueryable<TestCaseFigmaLink> TestCaseFigmaLinks => _db.TestCaseFigmaLinks;
    public IQueryable<TestCase> TestCases => _db.TestCases;
    public IQueryable<TestCaseRequirementLink> TestCaseRequirementLinks => _db.TestCaseRequirementLinks;
    public IQueryable<TestResult> TestResults => _db.TestResults;
    public IQueryable<TestRetestFlag> TestRetestFlags => _db.TestRetestFlags;
    public IQueryable<TestAssignment> TestAssignments => _db.TestAssignments;
    public IQueryable<TestActivity> TestActivities => _db.TestActivities;
    public IQueryable<DocumentNode> DocumentNodes => _db.DocumentNodes;
    public IQueryable<ProjectMember> ProjectMembers => _db.ProjectMembers;
}
