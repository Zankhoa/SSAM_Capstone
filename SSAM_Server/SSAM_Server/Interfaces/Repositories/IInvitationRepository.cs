using SSAM_Server.Models;

namespace SSAM_Server.Interfaces.Repositories;

public interface IInvitationRepository
{
    Task<MembershipRequest?> GetInvitationWithDetailsAsync(string token, CancellationToken ct = default);
    Task<MembershipRequest?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task AddInvitationAsync(MembershipRequest invite, CancellationToken ct = default);
    void UpdateInvitation(MembershipRequest invite);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work, CancellationToken ct = default);
}
