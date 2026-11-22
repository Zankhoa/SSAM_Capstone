using Microsoft.AspNetCore.Http;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.Interfaces.Security;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.ProjectServiceTests;

public class RemoveMemberAsyncTests : ProjectServiceTestBase
{
    private (ProjectAccessContext Access, ProjectMember Member) SetupValidRemoval(
        long currentUserId = 1,
        long targetUserId = 20,
        string roleCode = CompanyRoleCodes.Developer,
        int managerCount = 2)
    {
        var permissions = targetUserId == currentUserId
            ? Array.Empty<string>()
            : new[] { PermissionKeys.ProjectMemberManage };
        var access = SetupProjectAccess(userId: currentUserId, permissions: permissions);
        var member = new ProjectMember
        {
            ProjectId = 200,
            UserId = targetUserId,
            RoleId = 52,
            IsActive = true,
            Role = new CompanyRole { Id = 52, Code = roleCode, Scope = RoleScopes.Project, IsActive = true }
        };
        ProjectRepositoryMock.Setup(r => r.GetActiveMemberAsync(200, targetUserId, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        ProjectRepositoryMock.Setup(r => r.CountActiveProjectManagersAsync(200, It.IsAny<CancellationToken>())).ReturnsAsync(managerCount);
        return (access, member);
    }

    // UTC001: A manager can remove another member and mark the project membership inactive.
    [Fact]
    public async Task RemoveMemberAsync_WhenManagerRemovesMember_ReturnsNoContentAndDeactivatesMember()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var (_, member) = SetupValidRemoval();

        var result = await ProjectService.RemoveMemberAsync(200, 20, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status204NoContent, result.StatusCode);
        Assert.False(member.IsActive);
        ProjectRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        ProjectRepositoryMock.Verify(r => r.GetByIdAsync(200, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC002: A member can voluntarily leave without member-management permission.
    [Fact]
    public async Task RemoveMemberAsync_WhenMemberRemovesSelf_RequiresNoPermissionAndReturnsNoContent()
    {
        var (_, member) = SetupValidRemoval(currentUserId: 20, targetUserId: 20);

        var result = await ProjectService.RemoveMemberAsync(200, 20, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status204NoContent, result.StatusCode);
        Assert.False(member.IsActive);
        PermissionServiceMock.Verify(p => p.RequireProjectAsync(20, 200, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC003: An unauthenticated caller cannot remove a project member.
    [Fact]
    public async Task RemoveMemberAsync_WhenUserUnauthenticated_ReturnsUnauthorized()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns((long?)null);

        var result = await ProjectService.RemoveMemberAsync(200, 20, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
    }

    // UTC004: Removing a missing active member returns not found.
    [Fact]
    public async Task RemoveMemberAsync_WhenMemberIsMissing_ReturnsNotFound()
    {
        SetupProjectAccess(permissions: new[] { PermissionKeys.ProjectMemberManage });
        ProjectRepositoryMock.Setup(r => r.GetActiveMemberAsync(200, 20, It.IsAny<CancellationToken>())).ReturnsAsync((ProjectMember?)null);

        var result = await ProjectService.RemoveMemberAsync(200, 20, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    // UTC005: Removing the last project manager is rejected.
    [Fact]
    public async Task RemoveMemberAsync_WhenRemovingLastManager_ReturnsConflict()
    {
        var (_, member) = SetupValidRemoval(roleCode: CompanyRoleCodes.ProjectManager, managerCount: 1);

        var result = await ProjectService.RemoveMemberAsync(200, 20, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("LAST_PROJECT_MANAGER", result.ErrorCode);
        Assert.True(member.IsActive);
    }

    // UTC006: An archived project cannot remove members.
    [Fact]
    public async Task RemoveMemberAsync_WhenProjectArchived_ReturnsConflict()
    {
        SetupProjectAccess(permissions: new[] { PermissionKeys.ProjectMemberManage }, projectActive: false);

        var result = await ProjectService.RemoveMemberAsync(200, 20, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("PROJECT_ARCHIVED", result.ErrorCode);
    }

    // UTC007: A workspace archived state prevents member removal even when project access exists.
    [Fact]
    public async Task RemoveMemberAsync_WhenWorkspaceArchived_ReturnsConflict()
    {
        SetupProjectAccess(permissions: new[] { PermissionKeys.ProjectMemberManage }, workspaceActive: false);

        var result = await ProjectService.RemoveMemberAsync(200, 20, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("WORKSPACE_ARCHIVED", result.ErrorCode);
    }
}
