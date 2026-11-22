

using Microsoft.AspNetCore.Http;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.Interfaces.Security;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.WorkspaceServiceTests;

public class RemoveMemberAsyncTests : WorkspaceServiceTestBase
{
    private (Workspace Workspace, WorkspaceMember TargetMember) SetupValidWorkspaceAndMember(
        long currentUserId = 1,
        long targetUserId = 20,
        long workspaceId = 100,
        long? companyId = 10,
        bool isActive = true,
        string targetRoleCode = CompanyRoleCodes.WorkspaceMember)
    {
        CurrentUserMock
            .Setup(u => u.UserIdOrNull)
            .Returns(currentUserId);

        var workspace = new Workspace
        {
            Id = workspaceId,
            CompanyId = companyId,
            Name = "Engineering Workspace",
            Code = "eng-ws",
            IsActive = isActive,
            UpdatedAt = Clock.GetUtcNow().UtcDateTime.AddDays(-2)
        };

        var requiredPerm = targetUserId == currentUserId ? null : PermissionKeys.WorkspaceMemberManage;
        var permissions = requiredPerm != null
            ? new HashSet<string> { requiredPerm }
            : new HashSet<string>();

        var context = new WorkspaceAccessContext(
            workspace,
            permissions,
            new RoleRefDto {
                RoleId = 5,
                Code = CompanyRoleCodes.WorkspaceAdmin,
                Name = "Workspace Admin"
            });

        PermissionServiceMock
            .Setup(p => p.RequireWorkspaceAsync(currentUserId, workspaceId, requiredPerm, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<WorkspaceAccessContext>.Ok(context));

        var member = new WorkspaceMember
        {
            WorkspaceId = workspaceId,
            UserId = targetUserId,
            RoleId = 6,
            IsActive = true,
            Role = new CompanyRole { Id = 6, Code = targetRoleCode }
        };

        WorkspaceRepositoryMock
            .Setup(r => r.GetActiveMemberAsync(workspaceId, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        WorkspaceRepositoryMock
            .Setup(r => r.FindSoleProjectManagerProjectKeyAsync(workspaceId, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        WorkspaceRepositoryMock
            .Setup(r => r.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<ServiceResult>>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<ServiceResult>>, CancellationToken>(
                (action, ct) => action(ct));

        WorkspaceRepositoryMock
            .Setup(r => r.GetByIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        return (workspace, member);
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: A manager removes another active member and deactivates related memberships.
    [Fact]
    public async Task RemoveMemberAsync_WhenManagerRemovesMember_DeactivatesMembershipsAndReturnsNoContent()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var (workspace, member) = SetupValidWorkspaceAndMember(
            currentUserId: 1, targetUserId: 20, workspaceId: 100, targetRoleCode: CompanyRoleCodes.WorkspaceMember);

        var result = await WorkspaceService.RemoveMemberAsync(100, 20, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status204NoContent, result.StatusCode);
        Assert.False(member.IsActive);
        Assert.Equal(now, workspace.UpdatedAt);

        PermissionServiceMock.Verify(
            p => p.RequireWorkspaceAsync(1, 100, PermissionKeys.WorkspaceMemberManage, It.IsAny<CancellationToken>()),
            Times.Once);
        WorkspaceRepositoryMock.Verify(
            r => r.DeactivateProjectMembershipsInWorkspaceAsync(100, 20, It.IsAny<CancellationToken>()),
            Times.Once);
        WorkspaceRepositoryMock.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // UTC002: A member voluntarily leaves without requiring management permission.
    [Fact]
    public async Task RemoveMemberAsync_WhenMemberLeavesVoluntarily_RequiresNullPermissionAndReturnsNoContent()
    {
        var (workspace, member) = SetupValidWorkspaceAndMember(
            currentUserId: 20, targetUserId: 20, workspaceId: 100, targetRoleCode: CompanyRoleCodes.WorkspaceMember);

        var result = await WorkspaceService.RemoveMemberAsync(100, 20, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status204NoContent, result.StatusCode);
        Assert.False(member.IsActive);
        Assert.NotNull(workspace);

        PermissionServiceMock.Verify(
            p => p.RequireWorkspaceAsync(20, 100, null, It.IsAny<CancellationToken>()),
            Times.Once);
        WorkspaceRepositoryMock.Verify(
            r => r.DeactivateProjectMembershipsInWorkspaceAsync(100, 20, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC003: An unauthenticated user cannot remove a member.
    [Fact]
    public async Task RemoveMemberAsync_WhenUserUnauthenticated_ReturnsUnauthorized()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns((long?)null);

        var result = await WorkspaceService.RemoveMemberAsync(100, 20, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("Your session is invalid or has expired.", result.ErrorMessage);
    }

    // UTC004: A user without member-management permission is forbidden.
    [Fact]
    public async Task RemoveMemberAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireWorkspaceAsync(1, 100, PermissionKeys.WorkspaceMemberManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<WorkspaceAccessContext>.Forbidden("FORBIDDEN", "Bạn không có quyền quản lý thành viên."));

        var result = await WorkspaceService.RemoveMemberAsync(100, 20, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    // UTC005: Removing a member from a personal workspace returns BadRequest.
    [Fact]
    public async Task RemoveMemberAsync_WhenPersonalWorkspace_ReturnsBadRequest()
    {
        SetupValidWorkspaceAndMember(currentUserId: 1, targetUserId: 1, workspaceId: 100, companyId: null);

        var result = await WorkspaceService.RemoveMemberAsync(100, 1, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("PERSONAL_WORKSPACE", result.ErrorCode);
        Assert.Equal("Workspace cá nhân không có thành viên khác.", result.ErrorMessage);
    }

    // UTC006: Removing a member from an archived workspace returns Conflict.
    [Fact]
    public async Task RemoveMemberAsync_WhenWorkspaceIsArchived_ReturnsConflict()
    {
        SetupValidWorkspaceAndMember(currentUserId: 1, targetUserId: 20, workspaceId: 100, isActive: false);

        var result = await WorkspaceService.RemoveMemberAsync(100, 20, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("WORKSPACE_ARCHIVED", result.ErrorCode);
        Assert.Equal("Workspace đã được lưu trữ, dữ liệu chỉ đọc.", result.ErrorMessage);
    }

    // UTC007: Removing a non-member returns NotFound.
    [Fact]
    public async Task RemoveMemberAsync_WhenTargetMemberNotFound_ReturnsNotFound()
    {
        SetupValidWorkspaceAndMember(currentUserId: 1, targetUserId: 99, workspaceId: 100);
        WorkspaceRepositoryMock
            .Setup(r => r.GetActiveMemberAsync(100, 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkspaceMember?)null);

        var result = await WorkspaceService.RemoveMemberAsync(100, 99, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("Không tìm thấy dữ liệu.", result.ErrorMessage);
    }

    // UTC008: Removing the sole project manager returns Conflict.
    [Fact]
    public async Task RemoveMemberAsync_WhenTargetUserIsSoleProjectManager_ReturnsConflict()
    {
        SetupValidWorkspaceAndMember(currentUserId: 1, targetUserId: 20, workspaceId: 100);
        WorkspaceRepositoryMock
            .Setup(r => r.FindSoleProjectManagerProjectKeyAsync(100, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync("SSAM");

        var result = await WorkspaceService.RemoveMemberAsync(100, 20, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("LAST_PROJECT_MANAGER", result.ErrorCode);
        Assert.Equal("Người này là Project Manager duy nhất của dự án SSAM — chuyển PM trước khi gỡ.", result.ErrorMessage);
    }

    // =========================================================================
    // 3. BOUNDARY CASES (Active WorkspaceAdmin count: 1 vs 2)
    // =========================================================================

    // UTC009 + UTC010: Target user is WorkspaceAdmin & active admin count is 0 or 1 (minimum boundary) -> Returns Conflict (LAST_WORKSPACE_ADMIN)
    [Theory]
    [InlineData(0)] // UTC009: Below boundary (0)
    [InlineData(1)] // UTC010: Exact lower boundary (1 last admin)
    public async Task RemoveMemberAsync_WhenRemovingLastWorkspaceAdmin_ReturnsConflict(int activeAdminCount)
    {
        SetupValidWorkspaceAndMember(
            currentUserId: 1, targetUserId: 20, workspaceId: 100, targetRoleCode: CompanyRoleCodes.WorkspaceAdmin);
        WorkspaceRepositoryMock
            .Setup(r => r.CountActiveWorkspaceAdminsAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeAdminCount);

        var result = await WorkspaceService.RemoveMemberAsync(100, 20, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("LAST_WORKSPACE_ADMIN", result.ErrorCode);
        Assert.Equal("Workspace phải còn ít nhất 1 Workspace Admin.", result.ErrorMessage);
    }

    // UTC011: Removing a workspace admin succeeds when another admin remains.
    [Fact]
    public async Task RemoveMemberAsync_WhenMultipleWorkspaceAdminsExist_ReturnsNoContent()
    {
        var (_, member) = SetupValidWorkspaceAndMember(
            currentUserId: 1, targetUserId: 20, workspaceId: 100, targetRoleCode: CompanyRoleCodes.WorkspaceAdmin);
        WorkspaceRepositoryMock
            .Setup(r => r.CountActiveWorkspaceAdminsAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await WorkspaceService.RemoveMemberAsync(100, 20, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status204NoContent, result.StatusCode);
        Assert.False(member.IsActive);
    }
}