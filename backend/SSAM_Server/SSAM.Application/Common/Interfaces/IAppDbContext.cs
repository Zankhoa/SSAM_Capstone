using Microsoft.EntityFrameworkCore;
using SSAM.Domain.Entities.Companies;
using SSAM.Domain.Entities.Identity;
using SSAM.Domain.Entities.Workspaces;

namespace SSAM.Application.Common.Interfaces;

public interface IAppDbContext
{
    // Identity & Company Auth Tables
    DbSet<Account> Accounts { get; }
    DbSet<User> Users { get; }
    DbSet<Company> Companies { get; }
    DbSet<CompanyMember> CompanyMembers { get; }
    DbSet<CompanyRole> CompanyRoles { get; }
    DbSet<ExternalLogin> ExternalLogins { get; }
    DbSet<EmailVerificationToken> EmailVerificationTokens { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    
    // Onboarding & Workspaces
    DbSet<Workspace> Workspaces { get; }
    DbSet<WorkspaceMember> WorkspaceMembers { get; }
    DbSet<MembershipRequest> MembershipRequests { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
