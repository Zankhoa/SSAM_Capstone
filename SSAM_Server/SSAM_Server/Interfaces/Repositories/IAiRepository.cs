using SSAM_Server.Models;

namespace SSAM_Server.Interfaces.Repositories;

public interface IAiRepository : IRepositoryBase
{
    IQueryable<AiConversation> AiConversations { get; }
    IQueryable<AiConversationMessage> AiConversationMessages { get; }
    IQueryable<Project> Projects { get; }
    IQueryable<DocumentNode> DocumentNodes { get; }
    IQueryable<DocumentVersion> DocumentVersions { get; }
    IQueryable<RequirementItem> RequirementItems { get; }
    IQueryable<Issue> Issues { get; }
    IQueryable<IssueRequirementLink> IssueRequirementLinks { get; }
    IQueryable<TestPlan> TestPlans { get; }
    IQueryable<TestCase> TestCases { get; }
    IQueryable<TestCaseRequirementLink> TestCaseRequirementLinks { get; }
    IQueryable<TestResult> TestResults { get; }
}
