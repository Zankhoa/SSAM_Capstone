using Microsoft.Extensions.Time.Testing;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Projects;
using SSAM_Server.Interfaces.Repositories;
using SSAM_Server.Interfaces.Security;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.ProjectServiceTests;

public abstract class ProjectServiceTestBase
{
    protected readonly Mock<IProjectRepository> ProjectRepositoryMock;
    protected readonly Mock<IWorkspaceRepository> WorkspaceRepositoryMock;
    protected readonly Mock<ICompanyRepository> CompanyRepositoryMock;
    protected readonly Mock<IPermissionService> PermissionServiceMock;
    protected readonly Mock<ICurrentUser> CurrentUserMock;
    protected readonly FakeTimeProvider Clock;
    protected readonly SSAM_Server.Services.ProjectService ProjectService;

    protected ProjectServiceTestBase()
    {
        ProjectRepositoryMock = new Mock<IProjectRepository>();
        WorkspaceRepositoryMock = new Mock<IWorkspaceRepository>();
        CompanyRepositoryMock = new Mock<ICompanyRepository>();
        PermissionServiceMock = new Mock<IPermissionService>();
        CurrentUserMock = new Mock<ICurrentUser>();
        Clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.Zero));

        ProjectService = new SSAM_Server.Services.ProjectService(
            ProjectRepositoryMock.Object,
            WorkspaceRepositoryMock.Object,
            CompanyRepositoryMock.Object,
            PermissionServiceMock.Object,
            CurrentUserMock.Object,
            Clock);
    }

    protected ProjectAccessContext SetupProjectAccess(
        long userId = 1,
        long projectId = 200,
        long workspaceId = 100,
        long companyId = 10,
        bool workspaceActive = true,
        bool projectActive = true,
        IEnumerable<string>? permissions = null)
    {
        CurrentUserMock
            .Setup(u => u.UserIdOrNull)
            .Returns(userId);

        var workspace = new Workspace
        {
            Id = workspaceId,
            CompanyId = companyId,
            Name = "Engineering Workspace",
            Code = "engineering",
            IsActive = workspaceActive,
            UpdatedAt = Clock.GetUtcNow().UtcDateTime.AddDays(-2)
        };

        var project = new Project
        {
            Id = projectId,
            WorkspaceId = workspaceId,
            Workspace = workspace,
            Key = "ENG01",
            Name = "Engineering Project",
            Description = "Project description",
            SdlcMethod = SdlcMethods.Scrum,
            AiEnabled = true,
            CreatedBy = userId,
            IsActive = projectActive,
            CreatedAt = Clock.GetUtcNow().UtcDateTime.AddDays(-10),
            UpdatedAt = Clock.GetUtcNow().UtcDateTime.AddDays(-2)
        };

        var permissionSet = permissions == null
            ? new HashSet<string> { PermissionKeys.ProjectMemberManage, PermissionKeys.ProjectUpdate }
            : new HashSet<string>(permissions);
        var myRole = new RoleRefDto
        {
            RoleId = 50,
            Code = CompanyRoleCodes.ProjectManager,
            Name = "Project Manager"
        };
        var access = new ProjectAccessContext(project, workspace, permissionSet, myRole);

        PermissionServiceMock
            .Setup(p => p.RequireProjectAsync(userId, projectId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long _, long _, string? requiredPermission, CancellationToken _) =>
            {
                if (requiredPermission != null && !permissionSet.Contains(requiredPermission))
                    return ServiceResult<ProjectAccessContext>.ForbiddenPermission(requiredPermission);

                return ServiceResult<ProjectAccessContext>.Ok(access);
            });

        ProjectRepositoryMock
            .Setup(r => r.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        CompanyRepositoryMock
            .Setup(r => r.GetRoleByIdAsync(companyId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long _, long roleId, CancellationToken _) => new CompanyRole
            {
                Id = roleId,
                CompanyId = companyId,
                Code = roleId == 51 ? CompanyRoleCodes.ProjectManager : CompanyRoleCodes.Developer,
                Name = roleId == 51 ? "Project Manager" : "Developer",
                Scope = RoleScopes.Project,
                IsActive = true
            });

        ProjectRepositoryMock
            .Setup(r => r.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<ServiceResult<ItemsDto<ProjectMemberDto>>>>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<ServiceResult<ItemsDto<ProjectMemberDto>>>>, CancellationToken>(
                (action, ct) => action(ct));

        return access;
    }

    protected static ProjectMemberDto CreateMemberDto(long userId, long roleId = 52) => new()
    {
        UserId = userId,
        FullName = $"User {userId}",
        Email = $"user{userId}@example.com",
        Role = new RoleRefDto
        {
            RoleId = roleId,
            Code = CompanyRoleCodes.Developer,
            Name = "Developer"
        },
        WorkspaceRole = new RoleRefDto
        {
            RoleId = 60,
            Code = CompanyRoleCodes.WorkspaceMember,
            Name = "Workspace Member"
        }
    };
}
