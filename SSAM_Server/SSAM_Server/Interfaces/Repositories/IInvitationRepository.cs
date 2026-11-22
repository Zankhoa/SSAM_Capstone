using SSAM_Server.Models;

namespace SSAM_Server.Interfaces.Repositories;

public interface IInvitationRepository
{
    Task<CompanyInvitation?> GetInvitationWithDetailsAsync(string token, CancellationToken ct = default);
    Task<CompanyInvitation?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task AddInvitationAsync(CompanyInvitation invite, CancellationToken ct = default);
    void UpdateInvitation(CompanyInvitation invite);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work, CancellationToken ct = default);
}
