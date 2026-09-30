using SSAM.Domain.Entities.Companies;
using SSAM.Domain.Entities.Identity;

namespace SSAM.Application.Common.Interfaces.Repositories;

public interface IUnitOfWork : IDisposable
{
    IGenericRepository<T> Repository<T>() where T : class;

    // Explicit convenience repositories
    IGenericRepository<Account> Accounts { get; }
    IGenericRepository<User> Users { get; }
    IGenericRepository<Company> Companies { get; }
    IGenericRepository<CompanyMember> CompanyMembers { get; }
    IGenericRepository<CompanyRole> CompanyRoles { get; }
    IGenericRepository<ExternalLogin> ExternalLogins { get; }
    IGenericRepository<EmailVerificationToken> EmailVerificationTokens { get; }
    IGenericRepository<PasswordResetToken> PasswordResetTokens { get; }
    IGenericRepository<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
