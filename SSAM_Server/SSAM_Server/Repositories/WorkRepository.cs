using SSAM_Server.Data;
using SSAM_Server.Interfaces.Repositories;
using SSAM_Server.Models;

namespace SSAM_Server.Repositories;

public class WorkRepository : RepositoryBase, IWorkRepository
{
    public WorkRepository(AppDbContext db) : base(db) { }

    public IQueryable<Project> Projects => _db.Projects;
    public IQueryable<ProjectMember> ProjectMembers => _db.ProjectMembers;
    public IQueryable<DocumentNode> DocumentNodes => _db.DocumentNodes;
    public IQueryable<DocumentComment> DocumentComments => _db.DocumentComments;
    public IQueryable<DocumentActivity> DocumentActivities => _db.DocumentActivities;
    public IQueryable<RequirementItem> RequirementItems => _db.RequirementItems;
    public IQueryable<Issue> Issues => _db.Issues;
    public IQueryable<TestPlan> TestPlans => _db.TestPlans;
    public IQueryable<TestCase> TestCases => _db.TestCases;
    public IQueryable<TestCaseRequirementLink> TestCaseRequirementLinks => _db.TestCaseRequirementLinks;
}
