using System.Text.Json;
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
    private ProjectAccessContext SetupValidCreate(
        bool workspaceActive = true,
        long? companyId = 10,
        IEnumerable<string>? workspacePermissions = null)
    {
        var access = SetupProjectAccess(
            permissions: new[] { PermissionKeys.ProjectCreate, PermissionKeys.ProjectUpdate },
            workspaceActive: workspaceActive,
            projectActive: true);
        access.Workspace.CompanyId = companyId;

        PermissionServiceMock
            .Setup(p => p.RequireWorkspaceAsync(1, 100, PermissionKeys.ProjectCreate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<WorkspaceAccessContext>.Ok(new WorkspaceAccessContext(
                access.Workspace,
                new HashSet<string>(workspacePermissions ?? new[] { PermissionKeys.ProjectCreate }),
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
        CompanyRepositoryMock
            .Setup(c => c.GetRoleByIdAsync(10, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long _, long roleId, CancellationToken _) => new CompanyRole
            {
                Id = roleId,
                CompanyId = 10,
                Code = CompanyRoleCodes.Developer,
                Name = "Developer",
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
            .Callback<ProjectPhase, CancellationToken>((phase, _) => phase.Id++)
            .Returns(Task.CompletedTask);
        ProjectRepositoryMock
            .Setup(r => r.WritePhaseWorkflowAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<IReadOnlyList<StatusInputDto>>(), It.IsAny<IReadOnlyList<TransitionInputDto>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        ProjectRepositoryMock
            .Setup(r => r.GetByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(access.Workspace.Projects.FirstOrDefault());
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

    private static CreateProjectRequestDto ValidRequest() => new()
    {
        Name = "  New Project  ",
        Key = " eng02 ",
        Description = "  Description  ",
        SdlcMethod = SdlcMethods.Scrum,
        AiEnabled = true,
        Phases = new List<CreateProjectPhaseInputDto> { ValidPhase() }
    };

    private static CreateProjectPhaseInputDto ValidPhase(string name = "Planning") => new()
    {
        Name = name,
        Statuses = new List<StatusInputDto>
        {
            new() { Key = "todo", Name = "To do", Category = StatusCategories.Todo, IsInitial = true },
            new() { Key = "done", Name = "Done", Category = StatusCategories.Done }
        },
        Transitions = new List<TransitionInputDto>
        {
            new()
            {
                From = JsonDocument.Parse("\"todo\"").RootElement,
                To = JsonDocument.Parse("\"done\"").RootElement,
                AllowedRoleIds = new List<long> { 51 }
            }
        }
    };

    private async Task<ServiceResult<ProjectDto>> CreateAsync(CreateProjectRequestDto request)
        => await ProjectService.CreateProjectAsync(100, request, TestContext.Current.CancellationToken);

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: A valid request creates a normalized project, phase, workflow, and manager membership.
    [Fact]
    public async Task CreateProjectAsync_WithValidRequest_CreatesProjectPhaseWorkflowAndManager()
    {
        SetupValidCreate();
        var projects = new List<Project>();
        var phases = new List<ProjectPhase>();
        var members = new List<ProjectMember>();
        ProjectRepositoryMock.Setup(r => r.AddProjectAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .Callback<Project, CancellationToken>((project, _) => { project.Id = 200; projects.Add(project); })
            .Returns(Task.CompletedTask);
        ProjectRepositoryMock.Setup(r => r.AddPhaseAsync(It.IsAny<ProjectPhase>(), It.IsAny<CancellationToken>()))
            .Callback<ProjectPhase, CancellationToken>((phase, _) => { phase.Id = 300; phases.Add(phase); })
            .Returns(Task.CompletedTask);
        ProjectRepositoryMock.Setup(r => r.AddProjectMemberAsync(It.IsAny<ProjectMember>(), It.IsAny<CancellationToken>()))
            .Callback<ProjectMember, CancellationToken>((member, _) => members.Add(member))
            .Returns(Task.CompletedTask);

        var result = await CreateAsync(ValidRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Single(projects);
        Assert.Equal("New Project", projects[0].Name);
        Assert.Equal("ENG02", projects[0].Key);
        Assert.Equal("Description", projects[0].Description);
        Assert.True(projects[0].AiEnabled);
        Assert.Single(phases);
        Assert.Equal("Planning", phases[0].Name);
        Assert.Equal("ACTIVE", phases[0].LifecycleStatus);
        Assert.Single(members);
        Assert.Equal(1, members[0].UserId);
        Assert.Equal(51, members[0].RoleId);
        ProjectRepositoryMock.Verify(r => r.WritePhaseWorkflowAsync(200, 300, It.IsAny<IReadOnlyList<StatusInputDto>>(), It.IsAny<IReadOnlyList<TransitionInputDto>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC002: A request with AI explicitly disabled preserves that value and creates multiple ordered phases.
    [Fact]
    public async Task CreateProjectAsync_WithAiDisabledAndMultiplePhases_PersistsPhaseOrder()
    {
        SetupValidCreate();
        var projects = new List<Project>();
        var phases = new List<ProjectPhase>();
        ProjectRepositoryMock.Setup(r => r.AddProjectAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .Callback<Project, CancellationToken>((project, _) => { project.Id = 200; projects.Add(project); })
            .Returns(Task.CompletedTask);
        ProjectRepositoryMock.Setup(r => r.AddPhaseAsync(It.IsAny<ProjectPhase>(), It.IsAny<CancellationToken>()))
            .Callback<ProjectPhase, CancellationToken>((phase, _) => { phase.Id = 300 + phases.Count; phases.Add(phase); })
            .Returns(Task.CompletedTask);

        var request = ValidRequest();
        request.AiEnabled = false;
        request.Phases = new List<CreateProjectPhaseInputDto> { ValidPhase("Planning"), ValidPhase("Execution") };

        var result = await CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.False(projects.Single().AiEnabled);
        Assert.Equal(2, phases.Count);
        Assert.Equal(1, phases[0].Position);
        Assert.Equal("ACTIVE", phases[0].LifecycleStatus);
        Assert.Equal(2, phases[1].Position);
        Assert.Equal("UPCOMING", phases[1].LifecycleStatus);
    }

    // UTC003: A member who belongs to the workspace is added once with the requested project role.
    [Fact]
    public async Task CreateProjectAsync_WithWorkspaceMember_AddsRequestedProjectMember()
    {
        SetupValidCreate();
        WorkspaceRepositoryMock.Setup(r => r.IsMemberAsync(100, 20, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var members = new List<ProjectMember>();
        ProjectRepositoryMock.Setup(r => r.AddProjectMemberAsync(It.IsAny<ProjectMember>(), It.IsAny<CancellationToken>()))
            .Callback<ProjectMember, CancellationToken>((member, _) => members.Add(member))
            .Returns(Task.CompletedTask);

        var request = ValidRequest();
        request.Members = new List<ProjectMemberInputDto> { new() { UserId = 20, RoleId = 52 } };

        var result = await CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, members.Count);
        Assert.Contains(members, member => member.UserId == 20 && member.RoleId == 52);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC004: An unauthenticated caller cannot create a project.
    [Fact]
    public async Task CreateProjectAsync_WhenUserUnauthenticated_ReturnsUnauthorized()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns((long?)null);

        var result = await CreateAsync(ValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
    }

    // UTC005: A failed workspace permission check is returned without creating a project.
    [Fact]
    public async Task CreateProjectAsync_WhenWorkspacePermissionDenied_ReturnsForbidden()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock.Setup(p => p.RequireWorkspaceAsync(1, 100, PermissionKeys.ProjectCreate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<WorkspaceAccessContext>.ForbiddenPermission(PermissionKeys.ProjectCreate));

        var result = await CreateAsync(ValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal(PermissionKeys.ProjectCreate, result.RequiredPermission);
        ProjectRepositoryMock.Verify(r => r.AddProjectAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC006: An archived workspace is read-only and rejects project creation.
    [Fact]
    public async Task CreateProjectAsync_WhenWorkspaceArchived_ReturnsConflict()
    {
        SetupValidCreate(workspaceActive: false);

        var result = await CreateAsync(ValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("WORKSPACE_ARCHIVED", result.ErrorCode);
    }

    // UTC007: A personal workspace cannot contain projects.
    [Fact]
    public async Task CreateProjectAsync_WhenWorkspaceIsPersonal_ReturnsBadRequest()
    {
        SetupValidCreate(companyId: null);

        var result = await CreateAsync(ValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("PERSONAL_WORKSPACE", result.ErrorCode);
    }

    // UTC008: Invalid project fields return all relevant validation errors before persistence.
    [Fact]
    public async Task CreateProjectAsync_WhenProjectFieldsAreInvalid_ReturnsAllValidationErrors()
    {
        SetupValidCreate();
        var request = ValidRequest();
        request.Name = "A";
        request.Key = "bad-key";
        request.Description = new string('D', 501);
        request.SdlcMethod = "INVALID";
        request.Phases = new List<CreateProjectPhaseInputDto>();

        var result = await CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("name", result.Errors!.Keys);
        Assert.Contains("key", result.Errors.Keys);
        Assert.Contains("description", result.Errors.Keys);
        Assert.Contains("sdlcMethod", result.Errors.Keys);
        Assert.Contains("phases", result.Errors.Keys);
        ProjectRepositoryMock.Verify(r => r.AddProjectAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC009: Duplicate phase names are rejected case-insensitively.
    [Fact]
    public async Task CreateProjectAsync_WhenPhaseNamesAreDuplicated_ReturnsValidationError()
    {
        SetupValidCreate();
        var request = ValidRequest();
        request.Phases = new List<CreateProjectPhaseInputDto> { ValidPhase("Planning"), ValidPhase(" planning ") };

        var result = await CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("phases", result.Errors!.Keys);
        ProjectRepositoryMock.Verify(r => r.AddProjectAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC010: Invalid phase exit criteria are rejected with an indexed validation error.
    [Fact]
    public async Task CreateProjectAsync_WhenPhaseExitCriteriaIsInvalid_ReturnsValidationError()
    {
        SetupValidCreate();
        var request = ValidRequest();
        request.Phases[0].ExitCriteria = new List<string> { "UNKNOWN_CRITERION" };

        var result = await CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("phases[0].exitCriteria", result.Errors!.Keys);
    }

    // UTC011: A duplicate project key returns a conflict.
    [Fact]
    public async Task CreateProjectAsync_WhenKeyIsTaken_ReturnsConflict()
    {
        SetupValidCreate();
        ProjectRepositoryMock.Setup(r => r.KeyExistsAsync(100, "ENG02", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateAsync(ValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("PROJECT_KEY_TAKEN", result.ErrorCode);
    }

    // UTC012: A transition role outside the project scope is rejected.
    [Fact]
    public async Task CreateProjectAsync_WhenTransitionRoleHasWrongScope_ReturnsBadRequest()
    {
        SetupValidCreate();
        CompanyRepositoryMock.Setup(c => c.GetRoleByIdAsync(10, 51, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyRole { Id = 51, CompanyId = 10, Scope = RoleScopes.Workspace, IsActive = true });

        var result = await CreateAsync(ValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("ROLE_SCOPE_MISMATCH", result.ErrorCode);
    }

    // UTC013: An inactive transition role is rejected.
    [Fact]
    public async Task CreateProjectAsync_WhenTransitionRoleIsInactive_ReturnsBadRequest()
    {
        SetupValidCreate();
        CompanyRepositoryMock.Setup(c => c.GetRoleByIdAsync(10, 51, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyRole { Id = 51, CompanyId = 10, Scope = RoleScopes.Project, IsActive = false });

        var result = await CreateAsync(ValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("ROLE_INACTIVE", result.ErrorCode);
    }

    // UTC014: A missing transition role is rejected.
    [Fact]
    public async Task CreateProjectAsync_WhenTransitionRoleIsMissing_ReturnsNotFound()
    {
        SetupValidCreate();
        CompanyRepositoryMock.Setup(c => c.GetRoleByIdAsync(10, 51, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyRole?)null);

        var result = await CreateAsync(ValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    // UTC015: A workflow with no status is rejected.
    [Fact]
    public async Task CreateProjectAsync_WhenPhaseHasNoStatuses_ReturnsWorkflowError()
    {
        SetupValidCreate();
        var request = ValidRequest();
        request.Phases[0].Statuses = new List<StatusInputDto>();
        request.Phases[0].Transitions = new List<TransitionInputDto>();

        var result = await CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        var workflowIssues = Assert.IsAssignableFrom<IEnumerable<WorkflowIssueDto>>(result.WorkflowIssues);
        Assert.Contains(workflowIssues, issue => issue.Code == "NO_STATUS");
    }

    // UTC016: A workspace member with a missing project role is rejected.
    [Fact]
    public async Task CreateProjectAsync_WhenMemberIsNotWorkspaceMember_ReturnsBadRequest()
    {
        SetupValidCreate();
        WorkspaceRepositoryMock.Setup(r => r.IsMemberAsync(100, 20, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var request = ValidRequest();
        request.Members = new List<ProjectMemberInputDto> { new() { UserId = 20, RoleId = 52 } };

        var result = await CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("NOT_WORKSPACE_MEMBER", result.ErrorCode);
    }

    // UTC017: An inactive project role for a member is rejected.
    [Fact]
    public async Task CreateProjectAsync_WhenMemberRoleIsInactive_ReturnsBadRequest()
    {
        SetupValidCreate();
        WorkspaceRepositoryMock.Setup(r => r.IsMemberAsync(100, 20, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        CompanyRepositoryMock.Setup(c => c.GetRoleByIdAsync(10, 52, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyRole { Id = 52, CompanyId = 10, Scope = RoleScopes.Project, IsActive = false });
        var request = ValidRequest();
        request.Members = new List<ProjectMemberInputDto> { new() { UserId = 20, RoleId = 52 } };

        var result = await CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("ROLE_INACTIVE", result.ErrorCode);
    }

    // UTC018: Duplicate members are validated but persisted only once.
    [Fact]
    public async Task CreateProjectAsync_WithDuplicateMembers_PersistsEachUserOnce()
    {
        SetupValidCreate();
        WorkspaceRepositoryMock.Setup(r => r.IsMemberAsync(100, 20, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var members = new List<ProjectMember>();
        ProjectRepositoryMock.Setup(r => r.AddProjectMemberAsync(It.IsAny<ProjectMember>(), It.IsAny<CancellationToken>()))
            .Callback<ProjectMember, CancellationToken>((member, _) => members.Add(member))
            .Returns(Task.CompletedTask);
        var request = ValidRequest();
        request.Members = new List<ProjectMemberInputDto>
        {
            new() { UserId = 20, RoleId = 52 },
            new() { UserId = 20, RoleId = 52 }
        };

        var result = await CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Single(members, member => member.UserId == 20);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC019: The minimum valid project name length is accepted.
    [Fact]
    public async Task CreateProjectAsync_WithMinimumNameLength_ReturnsCreated()
    {
        SetupValidCreate();
        var request = ValidRequest();
        request.Name = "AB";

        var result = await CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
    }

    // UTC020: The maximum valid project name length is accepted.
    [Fact]
    public async Task CreateProjectAsync_WithMaximumNameLength_ReturnsCreated()
    {
        SetupValidCreate();
        var request = ValidRequest();
        request.Name = new string('A', 100);

        var result = await CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
    }

    // UTC021: A project name longer than 100 characters is rejected.
    [Fact]
    public async Task CreateProjectAsync_WhenNameExceedsMaximumLength_ReturnsValidationError()
    {
        SetupValidCreate();
        var request = ValidRequest();
        request.Name = new string('A', 101);

        var result = await CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("name", result.Errors!.Keys);
    }

    // UTC022: The minimum valid project key length is accepted.
    [Fact]
    public async Task CreateProjectAsync_WithMinimumKeyLength_ReturnsCreated()
    {
        SetupValidCreate();
        var request = ValidRequest();
        request.Key = "AB";

        var result = await CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
    }

    // UTC023: The maximum valid project key length is accepted.
    [Fact]
    public async Task CreateProjectAsync_WithMaximumKeyLength_ReturnsCreated()
    {
        SetupValidCreate();
        var request = ValidRequest();
        request.Key = "ABCDEFGHIJ";

        var result = await CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
    }

    // UTC024: A project key longer than 10 characters is rejected.
    [Fact]
    public async Task CreateProjectAsync_WhenKeyExceedsMaximumLength_ReturnsValidationError()
    {
        SetupValidCreate();
        var request = ValidRequest();
        request.Key = "ABCDEFGHIJK";

        var result = await CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("key", result.Errors!.Keys);
    }

    // UTC025: The maximum valid description length is accepted.
    [Fact]
    public async Task CreateProjectAsync_WithMaximumDescriptionLength_ReturnsCreated()
    {
        SetupValidCreate();
        var request = ValidRequest();
        request.Description = new string('D', 500);

        var result = await CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
    }

    // UTC026: A description longer than 500 characters is rejected.
    [Fact]
    public async Task CreateProjectAsync_WhenDescriptionExceedsMaximumLength_ReturnsValidationError()
    {
        SetupValidCreate();
        var request = ValidRequest();
        request.Description = new string('D', 501);

        var result = await CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("description", result.Errors!.Keys);
    }

    // UTC027: The maximum of 10 phases is accepted.
    [Fact]
    public async Task CreateProjectAsync_WithTenPhases_ReturnsCreated()
    {
        SetupValidCreate();
        var request = ValidRequest();
        request.Phases = Enumerable.Range(1, 10).Select(i => ValidPhase($"Phase {i}")).ToList();

        var result = await CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
    }

    // UTC028: More than 10 phases is rejected.
    [Fact]
    public async Task CreateProjectAsync_WithElevenPhases_ReturnsValidationError()
    {
        SetupValidCreate();
        var request = ValidRequest();
        request.Phases = Enumerable.Range(1, 11).Select(i => ValidPhase($"Phase {i}")).ToList();

        var result = await CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("phases", result.Errors!.Keys);
    }
}
