using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Projects;

namespace SSAM_Server.Interfaces.Services;

public interface IProjectService
{
    Task<ServiceResult<ItemsDto<ProjectDto>>> ListWorkspaceProjectsAsync(long workspaceId, string? status, string? search, CancellationToken ct = default);
    Task<ServiceResult<ItemsDto<ProjectDto>>> ListProjectsAsync(long? companyId, string? status, string? search, CancellationToken ct = default);
    Task<ServiceResult<ProjectKeyAvailabilityDto>> CheckKeyAvailabilityAsync(long workspaceId, string? key, CancellationToken ct = default);
    Task<ServiceResult<ProjectDto>> CreateProjectAsync(long workspaceId, CreateProjectRequestDto request, CancellationToken ct = default);
    Task<ServiceResult<ProjectDto>> GetProjectAsync(long projectId, CancellationToken ct = default);
    Task<ServiceResult<ProjectDto>> UpdateProjectAsync(long projectId, UpdateProjectRequestDto request, CancellationToken ct = default);
    Task<ServiceResult<ProjectDto>> ArchiveProjectAsync(long projectId, CancellationToken ct = default);
    Task<ServiceResult<ProjectDto>> RestoreProjectAsync(long projectId, CancellationToken ct = default);

    Task<ServiceResult<ItemsDto<ProjectMemberDto>>> ListMembersAsync(long projectId, CancellationToken ct = default);
    Task<ServiceResult<ItemsDto<ProjectMemberCandidateDto>>> ListMemberCandidatesAsync(long projectId, string? search, CancellationToken ct = default);
    Task<ServiceResult<ItemsDto<ProjectMemberDto>>> AddMembersAsync(long projectId, AddProjectMembersRequestDto request, CancellationToken ct = default);
    Task<ServiceResult<ProjectMemberDto>> UpdateMemberRoleAsync(long projectId, long targetUserId, UpdateProjectMemberRoleRequestDto request, CancellationToken ct = default);
    Task<ServiceResult> RemoveMemberAsync(long projectId, long targetUserId, CancellationToken ct = default);

    Task<ServiceResult<WorkflowDto>> GetWorkflowAsync(long projectId, CancellationToken ct = default);
    Task<ServiceResult<PhaseDto>> CreatePhaseAsync(long projectId, CreatePhaseRequestDto request, CancellationToken ct = default);
    Task<ServiceResult<WorkflowDto>> UpdatePhaseAsync(long projectId, long phaseId, UpdatePhaseRequestDto request, CancellationToken ct = default);
    Task<ServiceResult<PhaseDto>> UpdatePhaseWorkflowAsync(long projectId, long phaseId, UpdatePhaseWorkflowRequestDto request, CancellationToken ct = default);
    Task<ServiceResult<WorkflowDto>> CompletePhaseAsync(long projectId, long phaseId, CompletePhaseRequestDto? request, CancellationToken ct = default);
    Task<ServiceResult<WorkflowDto>> ReopenPhaseAsync(long projectId, long phaseId, ReopenPhaseRequestDto? request, CancellationToken ct = default);
}
