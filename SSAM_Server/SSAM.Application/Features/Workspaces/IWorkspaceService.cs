using SSAM.Application.Features.Workspaces.DTOs;

namespace SSAM.Application.Features.Workspaces;

public interface IWorkspaceService
{
    Task<WorkspaceSummaryDto> CreateWorkspaceAsync(CreateWorkspaceRequestDto request, long creatorUserId, CancellationToken cancellationToken = default);
}
