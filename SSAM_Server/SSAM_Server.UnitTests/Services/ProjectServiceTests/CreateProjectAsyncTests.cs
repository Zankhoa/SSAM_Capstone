using Microsoft.AspNetCore.Http;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Projects;
using SSAM_Server.Interfaces.Security;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.ProjectServiceTests;

public class CreateProjectAsyncTests : ProjectServiceTestBase
{
    private ProjectAccessContext SetupValidCreate()
    {
        var access = SetupProjectAccess(permissions: new[] { PermissionKeys.ProjectCreate, PermissionKeys.ProjectUpdate });
        PermissionServiceMock
            .Setup(p => p.RequireWorkspaceAsync(1, 100, PermissionKeys.ProjectCreate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<WorkspaceAccessContext>.Ok(new WorkspaceAccessContext(
                access.Workspace,
                new HashSet<string> { PermissionKeys.ProjectCreate },
                access.MyRole!)));

        CompanyRepositoryMock
            .Setup(c => c.GetRoleByCodeAsync(10, CompanyRoleCodes.ProjectManager, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyRole
            {
                Id = 51,
                CompanyId = 10,
                Code = CompanyRoleCodes.ProjectManager,
                Name = "Project Manager",
                Scope = RoleScopes.Project,
                IsActive = true
            });
        ProjectRepositoryMock
            .Setup(r => r.KeyExistsAsync(100, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        ProjectRepositoryMock
            .Setup(r => r.AddProjectAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .Callback<Project, CancellationToken>((project, _) => project.Id = 200)
            .Returns(Task.CompletedTask);
        ProjectRepositoryMock
            .Setup(r => r.AddPhaseAsync(It.IsAny<ProjectPhase>(), It.IsAny<CancellationToken>()))
            .Callback<ProjectPhase, CancellationToken>((phase, _) => phase.Id = 300)
            .Returns(Task.CompletedTask);
        WorkspaceRepositoryMock
            .Setup(r => r.GetByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(access.Workspace);
        PermissionServiceMock
            .Setup(p => p.GetProjectAccessAsync(1, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(access);
        ProjectRepositoryMock
            .Setup(r => r.BuildProjectDtoAsync(It.IsAny<Project>(), It.IsAny<Workspace>(), 1, It.IsAny<RoleRefDto?>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectDto { ProjectId = 200, WorkspaceId = 100, Key = "ENG02" });
        ProjectRepositoryMock
            .Setup(r => r.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<ServiceResult<ProjectDto>>>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<ServiceResult<ProjectDto>>>, CancellationToken>(
                (action, ct) => action(ct));
        return access;
    }

    // UTC001: A valid project request creates the project, initial phase, manager membership, and response DTO.
    [Fact]
    public async Task CreateProjectAsync_WithValidRequest_ReturnsCreatedAndPersistsProject()
    {
        var access = SetupValidCreate();
        var createdProjects = new List<Project>();
        var addedMembers = new List<ProjectMember>();
        ProjectRepositoryMock.Setup(r => r.AddProjectAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .Callback<Project, CancellationToken>((project, _) => { project.Id = 200; createdProjects.Add(project); })
            .Returns(Task.CompletedTask);
        ProjectRepositoryMock.Setup(r => r.AddProjectMemberAsync(It.IsAny<ProjectMember>(), It.IsAny<CancellationToken>()))
            .Callback<ProjectMember, CancellationToken>((member, _) => addedMembers.Add(member))
            .Returns(Task.CompletedTask);

        var result = await ProjectService.CreateProjectAsync(100, new CreateProjectRequestDto
        {
            Name = "  New Project  ",
            Key = " eng02 ",
            Description = "  Description  ",
            SdlcMethod = SdlcMethods.Scrum,
            Phases = new List<CreateProjectPhaseInputDto>
            {
                new()
                {
                    Name = "Planning",
                    Statuses = new List<StatusInputDto>
                    {
                        new() { Key = "todo", Name = "To do", Category = StatusCategories.Todo, IsInitial = true },
                        new() { Key = "done", Name = "Done", Category = StatusCategories.Done }
                    },
                    Transitions = new List<TransitionInputDto>
                    {
                        new()
                        {
                            From = System.Text.Json.JsonDocument.Parse("\"todo\"").RootElement,
                            To = System.Text.Json.JsonDocument.Parse("\"done\"").RootElement,
                            AllowedRoleIds = new List<long> { 51 }
                        }
                    }
                }
            }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Single(createdProjects);
        Assert.Equal("New Project", createdProjects[0].Name);
        Assert.Equal("ENG02", createdProjects[0].Key);
        Assert.Equal(1, addedMembers.Count);
        Assert.Equal(1, addedMembers[0].UserId);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, createdProjects[0].CreatedAt);
    }

    // UTC002: An unauthenticated caller cannot create a project.
    [Fact]
    public async Task CreateProjectAsync_WhenUserUnauthenticated_ReturnsUnauthorized()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns((long?)null);

        var result = await ProjectService.CreateProjectAsync(100, new CreateProjectRequestDto(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
    }

    // UTC003: Invalid project fields are rejected before repository persistence.
    [Fact]
    public async Task CreateProjectAsync_WhenRequestIsInvalid_ReturnsValidationError()
    {
        SetupValidCreate();

        var result = await ProjectService.CreateProjectAsync(100, new CreateProjectRequestDto
        {
            Name = "A",
            Key = "bad-key",
            SdlcMethod = "INVALID"
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        ProjectRepositoryMock.Verify(r => r.AddProjectAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC004: A duplicate project key returns a conflict.
    [Fact]
    public async Task CreateProjectAsync_WhenKeyIsTaken_ReturnsConflict()
    {
        SetupValidCreate();
        ProjectRepositoryMock.Setup(r => r.KeyExistsAsync(100, "ENG02", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await ProjectService.CreateProjectAsync(100, new CreateProjectRequestDto
        {
            Name = "New Project",
            Key = "ENG02",
            SdlcMethod = SdlcMethods.Scrum,
            Phases = new List<CreateProjectPhaseInputDto> { new() { Name = "Planning" } }
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
    }
}
