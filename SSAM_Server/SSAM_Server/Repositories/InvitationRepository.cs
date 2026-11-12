using Microsoft.EntityFrameworkCore;
using SSAM_Server.Data;
using SSAM_Server.Models;

namespace SSAM_Server.Repositories;

public class InvitationRepository : IInvitationRepository
{
    private readonly AppDbContext _db;

    public InvitationRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<MembershipRequest?> GetInvitationWithDetailsAsync(string token, CancellationToken ct = default)
        => _db.MembershipRequests
            .AsNoTracking()
            .Include(m => m.Company)
            .Include(m => m.Workspace)
            .Include(m => m.Role)
            .FirstOrDefaultAsync(m => m.Token == token && m.Source == "admin_invite", ct);

    public Task<MembershipRequest?> GetByTokenAsync(string token, CancellationToken ct = default)
        => _db.MembershipRequests.FirstOrDefaultAsync(m => m.Token == token && m.Source == "admin_invite", ct);

    public async Task AddInvitationAsync(MembershipRequest invite, CancellationToken ct = default)
        => await _db.MembershipRequests.AddAsync(invite, ct);

    public void UpdateInvitation(MembershipRequest invite)
    {
        if (_db.Entry(invite).State == EntityState.Detached)
            _db.MembershipRequests.Update(invite);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work, CancellationToken ct = default)
        => _db.ExecuteInTransactionAsync(work, ct);
}
