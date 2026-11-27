using SSAM_Server.Models;

namespace SSAM_Server.Interfaces.Repositories;

public interface IDocumentRepository : IRepositoryBase
{
    IQueryable<Project> Projects { get; }
    IQueryable<ProjectMember> ProjectMembers { get; }
    IQueryable<User> Users { get; }
    IQueryable<DocumentNode> DocumentNodes { get; }
    IQueryable<DocumentVersion> DocumentVersions { get; }
    IQueryable<DocumentVersionReqChange> DocumentVersionReqChanges { get; }
    IQueryable<RequirementItem> RequirementItems { get; }
    IQueryable<DocumentComment> DocumentComments { get; }
    IQueryable<DocumentCommentMention> DocumentCommentMentions { get; }
    IQueryable<DocumentActivity> DocumentActivities { get; }
    IQueryable<DocumentGeneration> DocumentGenerations { get; }

    Task<string> AllocateDocumentCodeAsync(long projectId, string docType, long? rootDocId = null, CancellationToken ct = default);
    Task<string> AllocateReqUidAsync(long projectId, CancellationToken ct = default);
}
