using SSAM.Application.DTOs.Companies;
using SSAM.Application.DTOs.Invitations;

namespace SSAM.Application.Common.Interfaces.Services;

public interface ICompanyService
{
    Task<CompanyMatchResponseDto> MatchDomainAsync(string email, CancellationToken cancellationToken = default);
    Task<CompanyDto> CreateCompanyAsync(CreateCompanyRequestDto request, long creatorUserId, CancellationToken cancellationToken = default);
    Task<CompanyJoinResponseDto> JoinCompanyAsync(long companyId, long userId, CancellationToken cancellationToken = default);
    Task<JoinRequestResponseDto> CreateJoinRequestAsync(long companyId, long userId, CancellationToken cancellationToken = default);
    Task<JoinRequestStatusResponseDto> GetJoinRequestStatusAsync(long companyId, long requestId, CancellationToken cancellationToken = default);
    Task<CreateInvitationResponseDto> CreateInvitationAsync(long companyId, CreateInvitationRequestDto request, CancellationToken cancellationToken = default);
    Task<InvitationDetailsResponseDto> GetInvitationDetailsAsync(string token, CancellationToken cancellationToken = default);
    Task<AcceptInvitationResponseDto> AcceptInvitationAsync(string token, AcceptInvitationRequestDto? request, long? currentUserId, CancellationToken cancellationToken = default);
}
