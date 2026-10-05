using SSAM.Application.Common.Exceptions;
using SSAM.Application.Common.Interfaces.Repositories;
using SSAM.Application.Features.Workspaces.DTOs;
using SSAM.Domain.Entities.Workspaces;

namespace SSAM.Application.Features.Workspaces;

public class WorkspaceService : IWorkspaceService
{
    private readonly IUnitOfWork _unitOfWork;

    public WorkspaceService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<WorkspaceSummaryDto> CreateWorkspaceAsync(CreateWorkspaceRequestDto request, long creatorUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw AppException.Validation(new Dictionary<string, string[]>
            {
                { "name", new[] { "Tên Workspace là bắt buộc." } }
            });
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var workspace = new Workspace
            {
                Name = request.Name,
                Description = request.Description,
                CompanyId = request.CompanyId,
                IsPersonal = request.CompanyId == null,
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Workspaces.AddAsync(workspace, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var member = new WorkspaceMember
            {
                WorkspaceId = workspace.Id,
                UserId = creatorUserId,
                RoleId = null, // null for personal or default
                JoinedAt = DateTime.UtcNow
            };
            await _unitOfWork.WorkspaceMembers.AddAsync(member, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new WorkspaceSummaryDto
            {
                WorkspaceId = workspace.Id,
                WorkspaceName = workspace.Name,
                CompanyId = workspace.CompanyId
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
