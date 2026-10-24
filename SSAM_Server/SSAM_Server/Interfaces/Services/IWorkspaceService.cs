using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Workspaces;

namespace SSAM_Server.Interfaces.Services;

public interface IWorkspaceService
{
    Task<ServiceResult<ItemsDto<WorkspaceDto>>> ListWorkspacesAsync(long? companyId, string? status, string? search, CancellationToken cancellationToken = default);
    Task<ServiceResult<WorkspaceDto>> GetWorkspaceAsync(long workspaceId, CancellationToken cancellationToken = default);
    Task<ServiceResult<WorkspaceCodeAvailabilityDto>> CheckCodeAvailabilityAsync(long companyId, string? code, CancellationToken cancellationToken = default);
    Task<ServiceResult<WorkspaceDto>> CreateWorkspaceAsync(CreateWorkspaceRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<WorkspaceDto>> UpdateWorkspaceAsync(long workspaceId, UpdateWorkspaceRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<WorkspaceDto>> ArchiveWorkspaceAsync(long workspaceId, CancellationToken cancellationToken = default);
    Task<ServiceResult<WorkspaceDto>> RestoreWorkspaceAsync(long workspaceId, CancellationToken cancellationToken = default);

    Task<ServiceResult<ItemsDto<WorkspaceMemberDto>>> ListMembersAsync(long workspaceId, long? roleId, string? search, CancellationToken cancellationToken = default);
    Task<ServiceResult<ItemsDto<UserRefDto>>> ListMemberCandidatesAsync(long workspaceId, string? search, CancellationToken cancellationToken = default);
    Task<ServiceResult<ItemsDto<WorkspaceMemberDto>>> AddMembersAsync(long workspaceId, AddWorkspaceMembersRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<WorkspaceMemberDto>> UpdateMemberRoleAsync(long workspaceId, long targetUserId, UpdateWorkspaceMemberRoleRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult> RemoveMemberAsync(long workspaceId, long targetUserId, CancellationToken cancellationToken = default);
}
