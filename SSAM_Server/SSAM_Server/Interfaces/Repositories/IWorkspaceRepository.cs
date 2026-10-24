using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Workspaces;
using SSAM_Server.Models;

namespace SSAM_Server.Interfaces.Repositories;

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(long workspaceId, CancellationToken ct = default);
    Task<List<Workspace>> ListByCompanyOrPersonalAsync(long? companyId, long userId, CancellationToken ct = default);
    Task<bool> PersonalWorkspaceExistsAsync(long userId, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(long companyId, string code, long? excludeWorkspaceId = null, CancellationToken ct = default);
    Task AddWorkspaceAsync(Workspace workspace, CancellationToken ct = default);

    Task<bool> IsMemberAsync(long workspaceId, long userId, CancellationToken ct = default);
    Task<WorkspaceMember?> GetActiveMemberAsync(long workspaceId, long userId, CancellationToken ct = default);
    Task<WorkspaceMember?> GetMemberRecordAsync(long workspaceId, long userId, CancellationToken ct = default);
    Task AddMemberAsync(WorkspaceMember member, CancellationToken ct = default);
    Task<int> CountActiveWorkspaceAdminsAsync(long workspaceId, CancellationToken ct = default);

    Task<List<WorkspaceMemberDto>> ListMembersAsync(long workspaceId, long companyId, long? roleId, string? search, CancellationToken ct = default);
    Task<WorkspaceMemberDto?> GetMemberDtoAsync(long workspaceId, long companyId, long userId, CancellationToken ct = default);
    Task<List<UserRefDto>> ListMemberCandidatesAsync(long workspaceId, long companyId, string? search, CancellationToken ct = default);

    Task<string?> FindSoleProjectManagerProjectKeyAsync(long workspaceId, long userId, CancellationToken ct = default);
    Task DeactivateProjectMembershipsInWorkspaceAsync(long workspaceId, long userId, CancellationToken ct = default);

    Task<WorkspaceDto> BuildWorkspaceDtoAsync(Workspace ws, long viewerId, RoleRefDto? myRole, IEnumerable<string> myPermissions, CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work, CancellationToken ct = default);
}
