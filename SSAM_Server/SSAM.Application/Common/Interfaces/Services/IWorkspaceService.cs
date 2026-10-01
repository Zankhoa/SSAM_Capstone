using SSAM.Application.DTOs.Workspaces;

namespace SSAM.Application.Common.Interfaces.Services;

public interface IWorkspaceService
{
    Task<WorkspaceSummaryDto> CreateWorkspaceAsync(CreateWorkspaceRequestDto request, long creatorUserId, CancellationToken cancellationToken = default);
}
