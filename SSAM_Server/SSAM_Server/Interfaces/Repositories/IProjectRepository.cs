using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Projects;
using SSAM_Server.Models;

namespace SSAM_Server.Interfaces.Repositories;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(long projectId, CancellationToken ct = default);
    Task<List<Project>> ListByWorkspaceIdsAsync(IEnumerable<long> workspaceIds, CancellationToken ct = default);
    Task<bool> KeyExistsAsync(long workspaceId, string key, CancellationToken ct = default);
    Task AddProjectAsync(Project project, CancellationToken ct = default);

    Task<bool> IsActiveMemberAsync(long projectId, long userId, CancellationToken ct = default);
    Task<ProjectMember?> GetActiveMemberAsync(long projectId, long userId, CancellationToken ct = default);
    Task<ProjectMember?> GetMemberRecordAsync(long projectId, long userId, CancellationToken ct = default);
    Task AddProjectMemberAsync(ProjectMember member, CancellationToken ct = default);
    Task<int> CountActiveProjectManagersAsync(long projectId, CancellationToken ct = default);

    Task<List<ProjectMemberDto>> ListMembersAsync(long projectId, long workspaceId, CancellationToken ct = default);
    Task<ProjectMemberDto?> GetMemberDtoAsync(long projectId, long workspaceId, long userId, CancellationToken ct = default);
    Task<List<ProjectMemberCandidateDto>> ListMemberCandidatesAsync(long projectId, long workspaceId, string? search, CancellationToken ct = default);

    Task<List<ProjectPhase>> ListPhasesByProjectAsync(long projectId, CancellationToken ct = default);
    Task<ProjectPhase?> GetPhaseByIdAsync(long projectId, long phaseId, CancellationToken ct = default);
    Task AddPhaseAsync(ProjectPhase phase, CancellationToken ct = default);
    Task<List<WorkflowStatus>> ListStatusesByPhaseAsync(long phaseId, CancellationToken ct = default);
    Task<List<long>> GetStatusIdsWithIssuesAsync(IEnumerable<long> statusIds, CancellationToken ct = default);
    Task WritePhaseWorkflowAsync(long projectId, long phaseId, IReadOnlyList<StatusInputDto> statuses, IReadOnlyList<TransitionInputDto> transitions, CancellationToken ct = default);

    Task<List<long>> GetActiveProjectRoleIdsAsync(long companyId, CancellationToken ct = default);
    Task<ProjectDto> BuildProjectDtoAsync(Project project, Workspace ws, long viewerId, RoleRefDto? myRole, IEnumerable<string> myPermissions, CancellationToken ct = default);
    Task<PhaseDto> BuildPhaseDtoAsync(long phaseId, CancellationToken ct = default);
    Task<WorkflowDto> BuildWorkflowDtoAsync(Project project, CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work, CancellationToken ct = default);
}
