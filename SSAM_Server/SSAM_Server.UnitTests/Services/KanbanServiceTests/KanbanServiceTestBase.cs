using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.Data;
using SSAM_Server.Interfaces.Security;
using SSAM_Server.Interfaces.Services;
using SSAM_Server.Models;
using SSAM_Server.Security;
using SSAM_Server.UnitTests.Helpers;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public abstract class KanbanServiceTestBase : IDisposable
{
    protected readonly AppDbContext Db;
    protected readonly Mock<ICurrentUser> CurrentUserMock;
    protected readonly Mock<IPermissionService> PermissionServiceMock;
    protected readonly Mock<IProjectAccessGuard> AccessGuardMock;
    protected readonly FakeTimeProvider Clock;
    protected readonly string UploadRoot;
    protected readonly SSAM_Server.Services.KanbanService KanbanService;

    protected KanbanServiceTestBase()
    {
        Clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero));
        Db = TestDbContextFactory.Create(Clock);
        var account = new Account
        {
            Id = 1,
            Email = "kanban-tests@example.com",
            EmailVerified = true,
            IsActive = true,
            CreatedAt = Clock.GetUtcNow().UtcDateTime,
            UpdatedAt = Clock.GetUtcNow().UtcDateTime
        };
        Db.Accounts.Add(account);
        Db.Users.Add(new User
        {
            Id = 1,
            AccountId = account.Id,
            Account = account,
            FullName = "Kanban Test User",
            Email = account.Email,
            IsActive = true,
            CreatedAt = Clock.GetUtcNow().UtcDateTime,
            UpdatedAt = Clock.GetUtcNow().UtcDateTime
        });
        Db.SaveChanges();
        CurrentUserMock = new Mock<ICurrentUser>();
        PermissionServiceMock = new Mock<IPermissionService>();
        AccessGuardMock = new Mock<IProjectAccessGuard>();
        UploadRoot = Path.Combine(Path.GetTempPath(), "ssam-kanban-tests", Guid.NewGuid().ToString("N"));

        CurrentUserMock.SetupGet(u => u.UserIdOrNull).Returns(1);
        CurrentUserMock.SetupGet(u => u.UserId).Returns(1);
        CurrentUserMock.SetupGet(u => u.IsAuthenticated).Returns(true);
        PermissionServiceMock
            .Setup(p => p.GetProjectPermissionsAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string>)PermissionKeys.AllProjectKeys);
        AccessGuardMock
            .Setup(g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Ok());
        AccessGuardMock
            .Setup(g => g.IsCompanyOrWorkspaceAdminAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:UploadPath"] = UploadRoot
            })
            .Build();

        KanbanService = new SSAM_Server.Services.KanbanService(
            Db,
            CurrentUserMock.Object,
            PermissionServiceMock.Object,
            Clock,
            AccessGuardMock.Object,
            new Mock<INotificationService>().Object,
            configuration);
    }

    protected Project SeedProject(long projectId = 100, long workspaceId = 10, long companyId = 1)
    {
        var workspace = new Workspace
        {
            Id = workspaceId,
            CompanyId = companyId,
            Name = "Engineering Workspace",
            Code = "engineering",
            IsActive = true,
            UpdatedAt = Clock.GetUtcNow().UtcDateTime
        };
        var project = new Project
        {
            Id = projectId,
            WorkspaceId = workspaceId,
            Workspace = workspace,
            Key = "ENG",
            Name = "Engineering Project",
            SdlcMethod = SdlcMethods.Scrum,
            CreatedBy = 1,
            IsActive = true,
            CreatedAt = Clock.GetUtcNow().UtcDateTime,
            UpdatedAt = Clock.GetUtcNow().UtcDateTime
        };
        Db.Projects.Add(project);
        Db.SaveChanges();
        return project;
    }

    protected (Project Project, ProjectPhase Phase, WorkflowStatus Todo, WorkflowStatus Done) SeedWorkflow()
    {
        var project = SeedProject();
        var phase = new ProjectPhase
        {
            Id = 20,
            ProjectId = project.Id,
            Name = "Execution",
            Position = 1,
            IsActive = true,
            LifecycleStatus = "ACTIVE",
            CreatedAt = Clock.GetUtcNow().UtcDateTime,
            UpdatedAt = Clock.GetUtcNow().UtcDateTime
        };
        var todo = new WorkflowStatus
        {
            Id = 30,
            ProjectId = project.Id,
            PhaseId = phase.Id,
            Name = "To do",
            Category = StatusCategories.Todo,
            IsInitial = true,
            Position = 1,
            IsActive = true
        };
        var done = new WorkflowStatus
        {
            Id = 31,
            ProjectId = project.Id,
            PhaseId = phase.Id,
            Name = "Done",
            Category = StatusCategories.Done,
            Position = 2,
            IsActive = true
        };
        Db.ProjectPhases.Add(phase);
        Db.WorkflowStatuses.AddRange(todo, done);
        Db.SaveChanges();
        return (project, phase, todo, done);
    }

    protected Issue SeedIssue(long issueId = 200, long? statusId = 30, long projectId = 100, string title = "Existing issue")
    {
        var status = Db.WorkflowStatuses.Find(statusId ?? 30);
        if (status == null)
        {
            SeedWorkflow();
            status = Db.WorkflowStatuses.Find(statusId ?? 30);
        }
        var issue = new Issue
        {
            Id = issueId,
            ProjectId = projectId,
            Key = $"ENG-{issueId}",
            Type = IssueTypes.Task,
            Title = title,
            Description = "Issue description",
            StatusId = status!.Id,
            Priority = IssuePriorities.Medium,
            ReporterId = 1,
            Rank = "000100",
            Labels = "[]",
            CreatedAt = Clock.GetUtcNow().UtcDateTime,
            UpdatedAt = Clock.GetUtcNow().UtcDateTime
        };
        Db.Issues.Add(issue);
        Db.SaveChanges();
        return issue;
    }

    protected Sprint SeedSprint(long sprintId = 400, string status = SprintStatuses.Planned, long projectId = 100)
    {
        var sprint = new Sprint
        {
            Id = sprintId,
            ProjectId = projectId,
            Name = "Sprint 1",
            Status = status,
            CreatedById = 1,
            CreatedAt = Clock.GetUtcNow().UtcDateTime,
            UpdatedAt = Clock.GetUtcNow().UtcDateTime
        };
        Db.Sprints.Add(sprint);
        Db.SaveChanges();
        return sprint;
    }

    protected DocumentNode SeedRequirementDocument(long documentId = 500, long versionId = 501, string reqUid = "REQ-001")
    {
        var project = Db.Projects.Find(100L) ?? SeedProject();
        var document = new DocumentNode
        {
            Id = documentId,
            ProjectId = project.Id,
            Kind = DocumentNodeKinds.Page,
            Title = "Requirements",
            Code = "REQ",
            CreatedById = 1,
            IsDeleted = false
        };
        var version = new DocumentVersion
        {
            Id = versionId,
            DocumentId = documentId,
            Label = "1.0",
            Status = DocumentVersionStatuses.Approved,
            Content = "{}",
            ContentText = "requirements",
            AuthorId = 1,
            UpdatedById = 1
        };
        document.CurrentVersionId = versionId;
        document.BaselineVersionId = versionId;
        var requirement = new RequirementItem
        {
            Id = versionId + 1,
            DocumentId = documentId,
            VersionId = versionId,
            ReqUid = reqUid,
            AnchorId = "anchor-1",
            Title = "User authentication",
            BodyPreview = "The user can authenticate.",
            ContentHash = "hash-1",
            AcceptanceCriteria = "[\"Login succeeds\"]"
        };
        Db.DocumentNodes.Add(document);
        Db.DocumentVersions.Add(version);
        Db.RequirementItems.Add(requirement);
        Db.SaveChanges();
        return document;
    }

    protected WorkflowTransition SeedTransition(
        long transitionId = 600,
        long fromStatusId = 30,
        long toStatusId = 31,
        long projectId = 100,
        long phaseId = 20,
        bool requireComment = false,
        string? commentHint = null,
        string? condition = null,
        params long[] allowedRoleIds)
    {
        var transition = new WorkflowTransition
        {
            Id = transitionId,
            ProjectId = projectId,
            PhaseId = phaseId,
            FromStatusId = fromStatusId,
            ToStatusId = toStatusId,
            RequireComment = requireComment,
            CommentHint = commentHint,
            Condition = condition
        };

        foreach (var roleId in allowedRoleIds)
        {
            var role = Db.CompanyRoles.Find(roleId);
            if (role == null)
            {
                role = new CompanyRole
                {
                    Id = roleId,
                    CompanyId = 1,
                    Code = $"ROLE_{roleId}",
                    Name = $"Role {roleId}",
                    Scope = RoleScopes.Project,
                    IsActive = true
                };
                Db.CompanyRoles.Add(role);
            }

            transition.AllowedRoles.Add(new WorkflowTransitionRole
            {
                TransitionId = transitionId,
                RoleId = roleId,
                Role = role
            });
        }

        Db.WorkflowTransitions.Add(transition);
        Db.SaveChanges();
        return transition;
    }

    protected ProjectMember SeedProjectMember(
        long userId = 1,
        long roleId = 51,
        long projectId = 100,
        long memberId = 700)
    {
        var role = Db.CompanyRoles.Find(roleId);
        if (role == null)
        {
            role = new CompanyRole
            {
                Id = roleId,
                CompanyId = 1,
                Code = $"ROLE_{roleId}",
                Name = $"Role {roleId}",
                Scope = RoleScopes.Project,
                IsActive = true
            };
            Db.CompanyRoles.Add(role);
        }

        var member = new ProjectMember
        {
            Id = memberId,
            ProjectId = projectId,
            UserId = userId,
            RoleId = roleId,
            Role = role,
            IsActive = true
        };
        Db.ProjectMembers.Add(member);
        Db.SaveChanges();
        return member;
    }

    protected (Issue First, Issue Second, Issue Third) SeedColumnIssues(long statusId = 30, long? sprintId = null)
    {
        var first = SeedIssue(issueId: 201, statusId: statusId, title: "Issue 1");
        first.SprintId = sprintId;
        first.Rank = "00001000";

        var second = SeedIssue(issueId: 202, statusId: statusId, title: "Issue 2");
        second.SprintId = sprintId;
        second.Rank = "00002000";

        var third = SeedIssue(issueId: 203, statusId: statusId, title: "Issue 3");
        third.SprintId = sprintId;
        third.Rank = "00003000";

        Db.SaveChanges();
        return (first, second, third);
    }

    protected Department SeedDepartment(long departmentId = 50, long companyId = 1, string name = "Backend Team")
    {
        var dept = new Department
        {
            Id = departmentId,
            CompanyId = companyId,
            Name = name,
            Code = "BE",
            IsActive = true,
            CreatedAt = Clock.GetUtcNow().UtcDateTime,
            UpdatedAt = Clock.GetUtcNow().UtcDateTime
        };
        Db.Departments.Add(dept);
        Db.SaveChanges();
        return dept;
    }

    protected (Account Account, User User) SeedUser(long userId = 2, string fullName = "Assignee User", string email = "assignee@example.com")
    {
        var account = new Account
        {
            Id = userId,
            Email = email,
            EmailVerified = true,
            IsActive = true,
            CreatedAt = Clock.GetUtcNow().UtcDateTime,
            UpdatedAt = Clock.GetUtcNow().UtcDateTime
        };
        var user = new User
        {
            Id = userId,
            AccountId = userId,
            Account = account,
            FullName = fullName,
            Email = email,
            IsActive = true,
            CreatedAt = Clock.GetUtcNow().UtcDateTime,
            UpdatedAt = Clock.GetUtcNow().UtcDateTime
        };
        Db.Accounts.Add(account);
        Db.Users.Add(user);
        Db.SaveChanges();
        return (account, user);
    }

    public void Dispose()
    {
        Db.Dispose();
        if (Directory.Exists(UploadRoot))
            Directory.Delete(UploadRoot, recursive: true);
    }

}
