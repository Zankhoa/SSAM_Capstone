using SSAM_Server.Models;

namespace SSAM_Server.Interfaces.Repositories;

public interface IKanbanRepository : IRepositoryBase
{
    IQueryable<Project> Projects { get; }
    IQueryable<ProjectMember> ProjectMembers { get; }
    IQueryable<CompanyMember> CompanyMembers { get; }
    IQueryable<Department> Departments { get; }
    IQueryable<User> Users { get; }
    IQueryable<WorkflowStatus> WorkflowStatuses { get; }
    IQueryable<WorkflowTransition> WorkflowTransitions { get; }
    IQueryable<Sprint> Sprints { get; }
    IQueryable<BoardWipLimit> BoardWipLimits { get; }
    IQueryable<Issue> Issues { get; }
    IQueryable<IssueComment> IssueComments { get; }
    IQueryable<IssueCommentMention> IssueCommentMentions { get; }
    IQueryable<IssueActivity> IssueActivities { get; }
    IQueryable<IssueAttachment> IssueAttachments { get; }
    IQueryable<IssueRequirementLink> IssueRequirementLinks { get; }
    IQueryable<DocumentNode> DocumentNodes { get; }
    IQueryable<RequirementItem> RequirementItems { get; }
    IQueryable<TestCase> TestCases { get; }

    Task<string> AllocateIssueKeyAsync(long projectId, string projectKey, CancellationToken ct = default);
}
