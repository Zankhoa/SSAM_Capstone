using SSAM_Server.Data;
using SSAM_Server.Interfaces.Repositories;
using SSAM_Server.Models;

namespace SSAM_Server.Repositories;

public class KanbanRepository : RepositoryBase, IKanbanRepository
{
    public KanbanRepository(AppDbContext db) : base(db) { }

    public IQueryable<Project> Projects => _db.Projects;
    public IQueryable<ProjectMember> ProjectMembers => _db.ProjectMembers;
    public IQueryable<CompanyMember> CompanyMembers => _db.CompanyMembers;
    public IQueryable<Department> Departments => _db.Departments;
    public IQueryable<User> Users => _db.Users;
    public IQueryable<WorkflowStatus> WorkflowStatuses => _db.WorkflowStatuses;
    public IQueryable<WorkflowTransition> WorkflowTransitions => _db.WorkflowTransitions;
    public IQueryable<Sprint> Sprints => _db.Sprints;
    public IQueryable<BoardWipLimit> BoardWipLimits => _db.BoardWipLimits;
    public IQueryable<Issue> Issues => _db.Issues;
    public IQueryable<IssueComment> IssueComments => _db.IssueComments;
    public IQueryable<IssueCommentMention> IssueCommentMentions => _db.IssueCommentMentions;
    public IQueryable<IssueActivity> IssueActivities => _db.IssueActivities;
    public IQueryable<IssueAttachment> IssueAttachments => _db.IssueAttachments;
    public IQueryable<IssueRequirementLink> IssueRequirementLinks => _db.IssueRequirementLinks;
    public IQueryable<DocumentNode> DocumentNodes => _db.DocumentNodes;
    public IQueryable<RequirementItem> RequirementItems => _db.RequirementItems;
    public IQueryable<TestCase> TestCases => _db.TestCases;

    public Task<string> AllocateIssueKeyAsync(long projectId, string projectKey, CancellationToken ct = default)
        => _db.AllocateIssueKeyAsync(projectId, projectKey, ct);
}
