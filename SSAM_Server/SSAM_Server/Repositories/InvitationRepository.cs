using Microsoft.EntityFrameworkCore;
using SSAM_Server.Data;
using SSAM_Server.DTOs.Invitations;
using SSAM_Server.Models;

namespace SSAM_Server.Repositories;

public class InvitationRepository : IInvitationRepository
{
    private readonly AppDbContext _db;

    public InvitationRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<InvitationDetailsResponseDto> GetDetailsAsync(string token, DateTime utcNow, CancellationToken ct = default)
    {
        var invite = await _db.MembershipRequests
            .AsNoTracking()
            .Where(m => m.Token == token && m.Source == "admin_invite")
            .Select(m => new
            {
                m.Status,
                m.ExpiresAt,
                m.Email,
                RoleName = m.Role != null ? m.Role.Name : m.RoleName,
                CompanyName = m.Company.Name,
                WorkspaceName = m.Workspace != null ? m.Workspace.Name : null
            })
            .FirstOrDefaultAsync(ct);

        if (invite == null)
            return new InvitationDetailsResponseDto { Valid = false, Reason = "NOT_FOUND" };
        if (invite.Status == "accepted")
            return new InvitationDetailsResponseDto { Valid = false, Reason = "ALREADY_USED" };
        if (invite.Status != "pending")
            return new InvitationDetailsResponseDto { Valid = false, Reason = "NOT_FOUND" };
        if (invite.ExpiresAt != null && invite.ExpiresAt <= utcNow)
            return new InvitationDetailsResponseDto { Valid = false, Reason = "EXPIRED" };

        return new InvitationDetailsResponseDto
        {
            Valid = true,
            Email = invite.Email,
            CompanyName = invite.CompanyName,
            WorkspaceName = invite.WorkspaceName,
            RoleName = invite.RoleName,
            ExpiresAt = invite.ExpiresAt
        };
    }

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
