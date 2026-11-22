using Microsoft.AspNetCore.Http;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Projects;
using SSAM_Server.Interfaces.Security;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.ProjectServiceTests;

public class UpdateMemberRoleAsyncTests : ProjectServiceTestBase
{
    private (ProjectAccessContext Access, ProjectMember Member) SetupValidRoleUpdate(int managerCount = 2)
    {
        var access = SetupProjectAccess(permissions: new[] { PermissionKeys.ProjectMemberManage });
        var member = new ProjectMember
        {
            ProjectId = 200,
            UserId = 20,
            RoleId = 51,
            Role = new CompanyRole
            {
                Id = 51,
                CompanyId = 10,
                Code = CompanyRoleCodes.ProjectManager,
                Scope = RoleScopes.Project,
                IsActive = true
            }
        };
        ProjectRepositoryMock.Setup(r => r.GetActiveMemberAsync(200, 20, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        ProjectRepositoryMock.Setup(r => r.CountActiveProjectManagersAsync(200, It.IsAny<CancellationToken>())).ReturnsAsync(managerCount);
        ProjectRepositoryMock.Setup(r => r.GetMemberDtoAsync(200, 100, 20, It.IsAny<CancellationToken>())).ReturnsAsync(CreateMemberDto(20, 52));
        return (access, member);
    }

    // UTC001: A manager can assign another active project role.
    [Fact]
    public async Task UpdateMemberRoleAsync_WithValidRole_ReturnsUpdatedMember()
    {
        SetupValidRoleUpdate();

        var result = await ProjectService.UpdateMemberRoleAsync(200, 20, new UpdateProjectMemberRoleRequestDto { RoleId = 52 }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(52, result.Data!.Role.RoleId);
        ProjectRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC002: An unauthenticated caller cannot change a project member role.
    [Fact]
    public async Task UpdateMemberRoleAsync_WhenUserUnauthenticated_ReturnsUnauthorized()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns((long?)null);

        var result = await ProjectService.UpdateMemberRoleAsync(200, 20, new UpdateProjectMemberRoleRequestDto { RoleId = 52 }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
    }

    // UTC003: Updating a missing active member returns not found.
    [Fact]
    public async Task UpdateMemberRoleAsync_WhenMemberIsMissing_ReturnsNotFound()
    {
        SetupProjectAccess(permissions: new[] { PermissionKeys.ProjectMemberManage });
        ProjectRepositoryMock.Setup(r => r.GetActiveMemberAsync(200, 20, It.IsAny<CancellationToken>())).ReturnsAsync((ProjectMember?)null);

        var result = await ProjectService.UpdateMemberRoleAsync(200, 20, new UpdateProjectMemberRoleRequestDto { RoleId = 52 }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    // UTC004: Removing the last project manager role is rejected.
    [Fact]
    public async Task UpdateMemberRoleAsync_WhenRemovingLastManagerRole_ReturnsConflict()
    {
        SetupValidRoleUpdate(managerCount: 1);

        var result = await ProjectService.UpdateMemberRoleAsync(200, 20, new UpdateProjectMemberRoleRequestDto { RoleId = 52 }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("LAST_PROJECT_MANAGER", result.ErrorCode);
    }

    // UTC005: An archived project cannot change member roles.
    [Fact]
    public async Task UpdateMemberRoleAsync_WhenProjectArchived_ReturnsConflict()
    {
        SetupProjectAccess(permissions: new[] { PermissionKeys.ProjectMemberManage }, projectActive: false);

        var result = await ProjectService.UpdateMemberRoleAsync(200, 20,
            new UpdateProjectMemberRoleRequestDto { RoleId = 52 }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("PROJECT_ARCHIVED", result.ErrorCode);
    }

    // UTC006: A missing replacement role returns not found.
    [Fact]
    public async Task UpdateMemberRoleAsync_WhenReplacementRoleIsMissing_ReturnsNotFound()
    {
        SetupValidRoleUpdate();
        CompanyRepositoryMock.Setup(r => r.GetRoleByIdAsync(10, 52, It.IsAny<CancellationToken>())).ReturnsAsync((CompanyRole?)null);

        var result = await ProjectService.UpdateMemberRoleAsync(200, 20,
            new UpdateProjectMemberRoleRequestDto { RoleId = 52 }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    // UTC007: A replacement role outside project scope is rejected.
    [Fact]
    public async Task UpdateMemberRoleAsync_WhenReplacementRoleHasWrongScope_ReturnsBadRequest()
    {
        SetupValidRoleUpdate();
        CompanyRepositoryMock.Setup(r => r.GetRoleByIdAsync(10, 52, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyRole { Id = 52, CompanyId = 10, Scope = RoleScopes.Workspace, IsActive = true });

        var result = await ProjectService.UpdateMemberRoleAsync(200, 20,
            new UpdateProjectMemberRoleRequestDto { RoleId = 52 }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("ROLE_SCOPE_MISMATCH", result.ErrorCode);
    }
}
