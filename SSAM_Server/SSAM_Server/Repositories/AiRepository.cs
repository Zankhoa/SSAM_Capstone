using SSAM_Server.Data;
using SSAM_Server.Interfaces.Repositories;
using SSAM_Server.Models;

namespace SSAM_Server.Repositories;

public class AiRepository : RepositoryBase, IAiRepository
{
    public AiRepository(AppDbContext db) : base(db) { }

    public IQueryable<AiConversation> AiConversations => _db.AiConversations;
    public IQueryable<AiConversationMessage> AiConversationMessages => _db.AiConversationMessages;
    public IQueryable<Project> Projects => _db.Projects;
    public IQueryable<DocumentNode> DocumentNodes => _db.DocumentNodes;
    public IQueryable<DocumentVersion> DocumentVersions => _db.DocumentVersions;
    public IQueryable<RequirementItem> RequirementItems => _db.RequirementItems;
    public IQueryable<Issue> Issues => _db.Issues;
    public IQueryable<IssueRequirementLink> IssueRequirementLinks => _db.IssueRequirementLinks;
    public IQueryable<TestPlan> TestPlans => _db.TestPlans;
    public IQueryable<TestCase> TestCases => _db.TestCases;
    public IQueryable<TestCaseRequirementLink> TestCaseRequirementLinks => _db.TestCaseRequirementLinks;
    public IQueryable<TestResult> TestResults => _db.TestResults;
}
