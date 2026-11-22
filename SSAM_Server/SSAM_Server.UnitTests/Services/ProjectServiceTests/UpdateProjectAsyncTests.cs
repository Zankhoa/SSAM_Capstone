using Microsoft.AspNetCore.Http;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Projects;
using SSAM_Server.Interfaces.Security;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.ProjectServiceTests;

public class UpdateProjectAsyncTests : ProjectServiceTestBase
{
    private (ProjectAccessContext Access, Project Project, ProjectDto Dto) SetupValidUpdate(
        IEnumerable<string>? permissions = null,
        bool workspaceActive = true,
        bool projectActive = true)
    {
        var access = SetupProjectAccess(
            permissions: permissions ?? new[] { PermissionKeys.ProjectUpdate },
            workspaceActive: workspaceActive,
            projectActive: projectActive);
        var dto = new ProjectDto { ProjectId = 200, WorkspaceId = 100, Name = "Updated Project" };
        ProjectRepositoryMock
            .Setup(r => r.BuildProjectDtoAsync(access.Project, access.Workspace, 1, access.MyRole, access.Permissions, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        return (access, access.Project, dto);
    }

    // UTC001: A valid update trims and persists the requested project fields.
    [Fact]
    public async Task UpdateProjectAsync_WithValidRequest_ReturnsOkAndUpdatesProject()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var (_, project, dto) = SetupValidUpdate();

        var result = await ProjectService.UpdateProjectAsync(200, new UpdateProjectRequestDto
        {
            Name = "  Updated Project  ",
            Description = "  New description  ",
            AiEnabled = false
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(dto, result.Data);
        Assert.Equal("Updated Project", project.Name);
        Assert.Equal("New description", project.Description);
        Assert.False(project.AiEnabled);
        Assert.Equal(now, project.UpdatedAt);
        ProjectRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC002: An update containing only the description preserves other fields and supports clearing it.
    [Fact]
    public async Task UpdateProjectAsync_WithDescriptionOnly_ClearsDescriptionAndPreservesOtherFields()
    {
        var (_, project, _) = SetupValidUpdate();
        var originalName = project.Name;

        var result = await ProjectService.UpdateProjectAsync(200, new UpdateProjectRequestDto
        {
            Description = "   "
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(project.Description);
        Assert.Equal(originalName, project.Name);
    }

    // UTC003: An unauthenticated caller cannot update a project.
    [Fact]
    public async Task UpdateProjectAsync_WhenUserUnauthenticated_ReturnsUnauthorized()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns((long?)null);

        var result = await ProjectService.UpdateProjectAsync(200, new UpdateProjectRequestDto(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
    }

    // UTC004: An archived project cannot be updated.
    [Fact]
    public async Task UpdateProjectAsync_WhenProjectArchived_ReturnsConflict()
    {
        SetupValidUpdate(projectActive: false);

        var result = await ProjectService.UpdateProjectAsync(200, new UpdateProjectRequestDto { Name = "Updated" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("PROJECT_ARCHIVED", result.ErrorCode);
    }

    // UTC005: A caller without project.update cannot change project configuration.
    [Fact]
    public async Task UpdateProjectAsync_WhenUpdatePermissionMissing_ReturnsForbidden()
    {
        SetupValidUpdate(permissions: Array.Empty<string>());

        var result = await ProjectService.UpdateProjectAsync(200, new UpdateProjectRequestDto { Name = "Updated" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.ProjectUpdate, result.RequiredPermission);
    }

    // UTC006: An archived workspace cannot be updated.
    [Fact]
    public async Task UpdateProjectAsync_WhenWorkspaceArchived_ReturnsConflict()
    {
        SetupValidUpdate(workspaceActive: false);

        var result = await ProjectService.UpdateProjectAsync(200, new UpdateProjectRequestDto { Name = "Updated" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("WORKSPACE_ARCHIVED", result.ErrorCode);
    }

    // UTC007: A missing tracked project returns not found without saving.
    [Fact]
    public async Task UpdateProjectAsync_WhenTrackedProjectIsMissing_ReturnsNotFound()
    {
        SetupValidUpdate();
        ProjectRepositoryMock.Setup(r => r.GetByIdAsync(200, It.IsAny<CancellationToken>())).ReturnsAsync((Project?)null);

        var result = await ProjectService.UpdateProjectAsync(200, new UpdateProjectRequestDto { Name = "Updated" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        ProjectRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC008: A name at the lower boundary below two characters returns a validation error.
    [Fact]
    public async Task UpdateProjectAsync_WhenNameIsTooShort_ReturnsValidationError()
    {
        SetupValidUpdate();

        var result = await ProjectService.UpdateProjectAsync(200, new UpdateProjectRequestDto { Name = "A" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("name", result.Errors!.Keys);
    }

    // UTC009: A description at the upper boundary above 500 characters returns a validation error.
    [Fact]
    public async Task UpdateProjectAsync_WhenDescriptionIsTooLong_ReturnsValidationError()
    {
        SetupValidUpdate();

        var result = await ProjectService.UpdateProjectAsync(200, new UpdateProjectRequestDto { Description = new string('D', 501) }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("description", result.Errors!.Keys);
    }
}
