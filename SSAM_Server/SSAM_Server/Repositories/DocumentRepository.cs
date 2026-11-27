using SSAM_Server.Data;
using SSAM_Server.Interfaces.Repositories;
using SSAM_Server.Models;

namespace SSAM_Server.Repositories;

public class DocumentRepository : RepositoryBase, IDocumentRepository
{
    public DocumentRepository(AppDbContext db) : base(db) { }

    public IQueryable<Project> Projects => _db.Projects;
    public IQueryable<ProjectMember> ProjectMembers => _db.ProjectMembers;
    public IQueryable<User> Users => _db.Users;
    public IQueryable<DocumentNode> DocumentNodes => _db.DocumentNodes;
    public IQueryable<DocumentVersion> DocumentVersions => _db.DocumentVersions;
    public IQueryable<DocumentVersionReqChange> DocumentVersionReqChanges => _db.DocumentVersionReqChanges;
    public IQueryable<RequirementItem> RequirementItems => _db.RequirementItems;
    public IQueryable<DocumentComment> DocumentComments => _db.DocumentComments;
    public IQueryable<DocumentCommentMention> DocumentCommentMentions => _db.DocumentCommentMentions;
    public IQueryable<DocumentActivity> DocumentActivities => _db.DocumentActivities;
    public IQueryable<DocumentGeneration> DocumentGenerations => _db.DocumentGenerations;

    public Task<string> AllocateDocumentCodeAsync(long projectId, string docType, long? rootDocId = null, CancellationToken ct = default)
        => _db.AllocateDocumentCodeAsync(projectId, docType, rootDocId, ct);

    public Task<string> AllocateReqUidAsync(long projectId, CancellationToken ct = default)
        => _db.AllocateReqUidAsync(projectId, ct);
}
