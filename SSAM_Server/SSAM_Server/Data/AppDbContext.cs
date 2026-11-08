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
    public DbSet<ProjectPhaseLog> ProjectPhaseLogs => Set<ProjectPhaseLog>();
    public DbSet<WorkflowStatus> WorkflowStatuses => Set<WorkflowStatus>();
    public DbSet<WorkflowTransition> WorkflowTransitions => Set<WorkflowTransition>();
    public DbSet<WorkflowTransitionRole> WorkflowTransitionRoles => Set<WorkflowTransitionRole>();
    public DbSet<ProjectSequence> ProjectSequences => Set<ProjectSequence>();

    // Module A — Documents & Requirements
    public DbSet<DocumentNode> DocumentNodes => Set<DocumentNode>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<RequirementItem> RequirementItems => Set<RequirementItem>();
    public DbSet<DocumentComment> DocumentComments => Set<DocumentComment>();
    public DbSet<DocumentCommentMention> DocumentCommentMentions => Set<DocumentCommentMention>();
    public DbSet<DocumentActivity> DocumentActivities => Set<DocumentActivity>();
    public DbSet<DocumentGeneration> DocumentGenerations => Set<DocumentGeneration>();

    // Module C — Kanban, Issues & Sprints
    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<IssueRequirementLink> IssueRequirementLinks => Set<IssueRequirementLink>();
    public DbSet<BoardWipLimit> BoardWipLimits => Set<BoardWipLimit>();
    public DbSet<IssueComment> IssueComments => Set<IssueComment>();
    public DbSet<IssueCommentMention> IssueCommentMentions => Set<IssueCommentMention>();
    public DbSet<IssueActivity> IssueActivities => Set<IssueActivity>();

    // Module D — Test Cases & Test Plans (v2)
    public DbSet<TestPlan> TestPlans => Set<TestPlan>();
    public DbSet<TestEnvironment> TestEnvironments => Set<TestEnvironment>();
    public DbSet<TestBuild> TestBuilds => Set<TestBuild>();
    public DbSet<TestModule> TestModules => Set<TestModule>();
    public DbSet<TestSection> TestSections => Set<TestSection>();
    public DbSet<TestAssignment> TestAssignments => Set<TestAssignment>();
    public DbSet<TestCase> TestCases => Set<TestCase>();
    public DbSet<TestCaseRequirementLink> TestCaseRequirementLinks => Set<TestCaseRequirementLink>();
    public DbSet<TestResult> TestResults => Set<TestResult>();
    public DbSet<TestRetestFlag> TestRetestFlags => Set<TestRetestFlag>();
    public DbSet<TestActivity> TestActivities => Set<TestActivity>();

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
        var isRetry = false;
        return await strategy.ExecuteAsync(async ct =>
        {
            if (isRetry)
            {
                ChangeTracker.Clear();
            }
            isRetry = true;

            await using var transaction = await Database.BeginTransactionAsync(ct);
            var result = await work(ct);

            if (result is ServiceResult { IsSuccess: false })
            {
                await transaction.RollbackAsync(ct);
                ChangeTracker.Clear();
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

    public async Task<int> NextSequenceValueAsync(long projectId, string scope, CancellationToken ct = default)
    {
        var seq = await ProjectSequences
            .FirstOrDefaultAsync(ps => ps.ProjectId == projectId && ps.Scope == scope, ct);
        if (seq == null)
        {
            seq = new ProjectSequence
            {
                ProjectId = projectId,
                Scope = scope,
                LastValue = 1
            };
            await ProjectSequences.AddAsync(seq, ct);
            await SaveChangesAsync(ct);
            return 1;
        }

        seq.LastValue += 1;
        await SaveChangesAsync(ct);
        return seq.LastValue;
    }

    public async Task<string> AllocateDocumentCodeAsync(long projectId, string docType, CancellationToken ct = default)
    {
        var scope = SequenceScopes.ForDocType(docType);
        var n = await NextSequenceValueAsync(projectId, scope, ct);
        var prefix = docType == DocumentTypes.Other ? "DOC" : docType;
        return $"{prefix}-{n:D2}";
    }

    public async Task<string> AllocateIssueKeyAsync(long projectId, string projectKey, CancellationToken ct = default)
    {
        var n = await NextSequenceValueAsync(projectId, SequenceScopes.Issue, ct);
        return $"{projectKey}-{n}";
    }

    public async Task<string> AllocateTestCaseKeyAsync(long projectId, CancellationToken ct = default)
    {
        var n = await NextSequenceValueAsync(projectId, SequenceScopes.TestCase, ct);
        return $"TC-{n}";
    }

    public async Task<string> AllocateReqUidAsync(long projectId, CancellationToken ct = default)
    {
        var n = await NextSequenceValueAsync(projectId, SequenceScopes.Req, ct);
        return $"REQ-{n:D3}";
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
