using SSAM_Server.Common;
using SSAM_Server.DTOs.Invitations;

namespace SSAM_Server.Interfaces.Services;

public interface IInvitationService
{
    Task<ServiceResult<CreateInvitationResponseDto>> CreateInvitationAsync(long companyId, CreateInvitationRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<InvitationDetailsResponseDto>> GetInvitationDetailsAsync(string token, CancellationToken cancellationToken = default);
    Task<ServiceResult<AcceptInvitationResponseDto>> AcceptInvitationAsync(string token, AcceptInvitationRequestDto? request, CancellationToken cancellationToken = default);
}
