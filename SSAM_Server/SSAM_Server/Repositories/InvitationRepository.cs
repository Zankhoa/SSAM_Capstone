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

    public Task<CompanyInvitation?> GetInvitationWithDetailsAsync(string token, CancellationToken ct = default)
        => _db.CompanyInvitations
            .AsNoTracking()
            .Include(m => m.Company)
            .Include(m => m.Workspace)
            .Include(m => m.Role)
            .FirstOrDefaultAsync(m => m.Token == token, ct);

    public Task<CompanyInvitation?> GetByTokenAsync(string token, CancellationToken ct = default)
        => _db.CompanyInvitations.FirstOrDefaultAsync(m => m.Token == token, ct);

    public async Task AddInvitationAsync(CompanyInvitation invite, CancellationToken ct = default)
        => await _db.CompanyInvitations.AddAsync(invite, ct);

    public void UpdateInvitation(CompanyInvitation invite)
    {
        if (_db.Entry(invite).State == EntityState.Detached)
            _db.CompanyInvitations.Update(invite);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work, CancellationToken ct = default)
        => _db.ExecuteInTransactionAsync(work, ct);
}
