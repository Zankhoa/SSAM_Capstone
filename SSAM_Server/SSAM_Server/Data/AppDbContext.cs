using Microsoft.EntityFrameworkCore;
using SSAM_Server.Common;
using SSAM_Server.Models;

namespace SSAM_Server.Data;

public class AppDbContext : DbContext
{
    private readonly TimeProvider _clock;

    public AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider? clock = null) : base(options)
    {
        _clock = clock ?? TimeProvider.System;
    }

    // Identity & Company Auth Tables
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyMember> CompanyMembers => Set<CompanyMember>();
    public DbSet<CompanyRole> CompanyRoles => Set<CompanyRole>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Onboarding & Workspaces
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
    public DbSet<MembershipRequest> MembershipRequests => Set<MembershipRequest>();

    // RBAC & Projects & Workflow
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<ProjectPhase> ProjectPhases => Set<ProjectPhase>();
    public DbSet<WorkflowStatus> WorkflowStatuses => Set<WorkflowStatus>();
    public DbSet<WorkflowTransition> WorkflowTransitions => Set<WorkflowTransition>();
    public DbSet<WorkflowTransitionRole> WorkflowTransitionRoles => Set<WorkflowTransitionRole>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        TouchUpdatedAt();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        TouchUpdatedAt();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken = default)
    {
        if (Database.CurrentTransaction != null)
        {
            return await work(cancellationToken);
        }

        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async ct =>
        {
            ChangeTracker.Clear();

            await using var transaction = await Database.BeginTransactionAsync(ct);
            var result = await work(ct);

            if (result is ServiceResult { IsSuccess: false })
            {
                await transaction.RollbackAsync(ct);
                return result;
            }

            await transaction.CommitAsync(ct);
            return result;
        }, cancellationToken);
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default)
    {
        await ExecuteInTransactionAsync<object?>(async ct =>
        {
            await work(ct);
            return null;
        }, cancellationToken);
    }

    private void TouchUpdatedAt()
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        foreach (var entry in ChangeTracker.Entries<IHasUpdatedAt>().Where(e => e.State == EntityState.Modified))
        {
            entry.Entity.UpdatedAt = now;
        }
    }
}
