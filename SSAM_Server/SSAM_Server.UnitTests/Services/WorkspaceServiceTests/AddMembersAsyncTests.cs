using Microsoft.AspNetCore.Http;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Workspaces;
using SSAM_Server.Interfaces.Security;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.WorkspaceServiceTests;

public class AddMembersAsyncTests : WorkspaceServiceTestBase
{
    private Workspace SetupValidWorkspaceAndDefaults(
        long userId = 1,
        long companyId = 10,
        long workspaceId = 100,
        long? workspaceCompanyId = 10,
        bool isActive = true,
        long defaultMemberRoleId = 6)
    {
        CurrentUserMock
            .Setup(u => u.UserIdOrNull)
            .Returns(userId);

        var workspace = new Workspace
        {
            Id = workspaceId,
            CompanyId = workspaceCompanyId,
            Name = "Engineering Workspace",
            Code = "eng-ws",
            IsActive = isActive,
            UpdatedAt = Clock.GetUtcNow().UtcDateTime.AddDays(-2)
        };

        var context = new WorkspaceAccessContext(
            workspace,
            new HashSet<string> { PermissionKeys.WorkspaceMemberManage },
            new RoleRefDto {
                RoleId = 5,
                Code = CompanyRoleCodes.WorkspaceAdmin,
                Name = "Workspace Admin"
            });

        PermissionServiceMock
            .Setup(p => p.RequireWorkspaceAsync(userId, workspaceId, PermissionKeys.WorkspaceMemberManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<WorkspaceAccessContext>.Ok(context));

        CompanyRepositoryMock
            .Setup(c => c.GetRoleByCodeAsync(companyId, CompanyRoleCodes.WorkspaceMember, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyRole
            {
                Id = defaultMemberRoleId,
                CompanyId = companyId,
                Code = CompanyRoleCodes.WorkspaceMember,
                Scope = RoleScopes.Workspace,
                IsActive = true
            });

        CompanyRepositoryMock
            .Setup(c => c.IsMemberAsync(companyId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        WorkspaceRepositoryMock
            .Setup(r => r.IsMemberAsync(workspaceId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        WorkspaceRepositoryMock
            .Setup(r => r.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<ServiceResult<ItemsDto<WorkspaceMemberDto>>>>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<ServiceResult<ItemsDto<WorkspaceMemberDto>>>>, CancellationToken>(
                (action, ct) => action(ct));

        WorkspaceRepositoryMock
            .Setup(r => r.GetMemberRecordAsync(workspaceId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkspaceMember?)null);

        WorkspaceRepositoryMock
            .Setup(r => r.GetByIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        return workspace;
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Add multiple new members (one with explicit RoleId, one with null RoleId -> uses default WORKSPACE_MEMBER role)
    [Fact]
    public async Task AddMembersAsync_WithExplicitAndDefaultRoles_ReturnsCreatedAndAddsMembers()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var workspace = SetupValidWorkspaceAndDefaults(
            userId: 1, companyId: 10, workspaceId: 100, defaultMemberRoleId: 6);

        CompanyRepositoryMock
            .Setup(c => c.GetRoleByIdAsync(10, 8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyRole
            {
                Id = 8,
                CompanyId = 10,
                Code = CompanyRoleCodes.ProjectInitiator,
                Scope = RoleScopes.Workspace,
                IsActive = true
            });

        var addedMembers = new List<WorkspaceMember>();
        WorkspaceRepositoryMock
            .Setup(r => r.AddMemberAsync(It.IsAny<WorkspaceMember>(), It.IsAny<CancellationToken>()))
            .Callback<WorkspaceMember, CancellationToken>((m, _) => addedMembers.Add(m))
            .Returns(Task.CompletedTask);

        var dto1 = new WorkspaceMemberDto();
        var dto2 = new WorkspaceMemberDto();
        WorkspaceRepositoryMock
            .Setup(r => r.GetMemberDtoAsync(100, 10, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto1);
        WorkspaceRepositoryMock
            .Setup(r => r.GetMemberDtoAsync(100, 10, 21, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto2);

        var request = new AddWorkspaceMembersRequestDto
        {
            Members = new List<AddWorkspaceMemberItemDto>
            {
                new() { UserId = 20, RoleId = 8 },    // Explicit role
                new() { UserId = 21, RoleId = null }  // Default WORKSPACE_MEMBER role (Id = 6)
            }
        };

        var result = await WorkspaceService.AddMembersAsync(100, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(2, result.Data!.Items.Count);
        Assert.Contains(dto1, result.Data.Items);
        Assert.Contains(dto2, result.Data.Items);

        Assert.Equal(2, addedMembers.Count);
        Assert.Contains(addedMembers, m => m.UserId == 20 && m.RoleId == 8 && m.IsActive && m.JoinedAt == now);
        Assert.Contains(addedMembers, m => m.UserId == 21 && m.RoleId == 6 && m.IsActive && m.JoinedAt == now);
        Assert.Equal(now, workspace.UpdatedAt);

        WorkspaceRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Re-add a previously removed member (GetMemberRecordAsync returns inactive record) -> Reactivates existing record
    [Fact]
    public async Task AddMembersAsync_WhenPreviousInactiveMemberExists_ReactivatesExistingRecord()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupValidWorkspaceAndDefaults(userId: 1, companyId: 10, workspaceId: 100, defaultMemberRoleId: 6);

        var previousRecord = new WorkspaceMember
        {
            WorkspaceId = 100,
            UserId = 20,
            RoleId = 99,
            IsActive = false,
            JoinedAt = now.AddDays(-30)
        };

        WorkspaceRepositoryMock
            .Setup(r => r.GetMemberRecordAsync(100, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(previousRecord);

        WorkspaceRepositoryMock
            .Setup(r => r.GetMemberDtoAsync(100, 10, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceMemberDto());

        var request = new AddWorkspaceMembersRequestDto
        {
            Members = new List<AddWorkspaceMemberItemDto>
            {
                new() { UserId = 20, RoleId = null }
            }
        };

        var result = await WorkspaceService.AddMembersAsync(100, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.True(previousRecord.IsActive);
        Assert.Equal(6, previousRecord.RoleId);
        Assert.Equal(now, previousRecord.JoinedAt);

        WorkspaceRepositoryMock.Verify(r => r.AddMemberAsync(It.IsAny<WorkspaceMember>(), It.IsAny<CancellationToken>()), Times.Never);
        WorkspaceRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC03: User is not authenticated -> Returns Unauthorized
    [Fact]
    public async Task AddMembersAsync_WhenUserUnauthenticated_ReturnsUnauthorized()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns((long?)null);
        var request = new AddWorkspaceMembersRequestDto
        {
            Members = new List<AddWorkspaceMemberItemDto> { new() { UserId = 20 } }
        };

        var result = await WorkspaceService.AddMembersAsync(100, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("Your session is invalid or has expired.", result.ErrorMessage);
    }

    // UTC04: User lacks WorkspaceMemberManage permission -> Returns Forbidden
    [Fact]
    public async Task AddMembersAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireWorkspaceAsync(1, 100, PermissionKeys.WorkspaceMemberManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<WorkspaceAccessContext>.Forbidden("FORBIDDEN", "Bạn không có quyền quản lý thành viên."));

        var request = new AddWorkspaceMembersRequestDto
        {
            Members = new List<AddWorkspaceMemberItemDto> { new() { UserId = 20 } }
        };

        var result = await WorkspaceService.AddMembersAsync(100, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    // UTC05: Workspace is a personal workspace (CompanyId == null) -> Returns BadRequest (PERSONAL_WORKSPACE)
    [Fact]
    public async Task AddMembersAsync_WhenPersonalWorkspace_ReturnsBadRequest()
    {
        SetupValidWorkspaceAndDefaults(userId: 1, workspaceId: 100, workspaceCompanyId: null);
        var request = new AddWorkspaceMembersRequestDto
        {
            Members = new List<AddWorkspaceMemberItemDto> { new() { UserId = 20 } }
        };

        var result = await WorkspaceService.AddMembersAsync(100, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("PERSONAL_WORKSPACE", result.ErrorCode);
        Assert.Equal("Workspace cá nhân không có thành viên khác.", result.ErrorMessage);
    }

    // UTC06: Workspace is archived (IsActive == false) -> Returns Conflict (WORKSPACE_ARCHIVED)
    [Fact]
    public async Task AddMembersAsync_WhenWorkspaceIsArchived_ReturnsConflict()
    {
        SetupValidWorkspaceAndDefaults(userId: 1, companyId: 10, workspaceId: 100, isActive: false);
        var request = new AddWorkspaceMembersRequestDto
        {
            Members = new List<AddWorkspaceMemberItemDto> { new() { UserId = 20 } }
        };

        var result = await WorkspaceService.AddMembersAsync(100, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("WORKSPACE_ARCHIVED", result.ErrorCode);
        Assert.Equal("Workspace đã được lưu trữ, dữ liệu chỉ đọc.", result.ErrorMessage);
    }

    // UTC07: Members list is null or empty -> Returns Validation Error
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddMembersAsync_WhenMembersListIsNullOrEmpty_ReturnsValidationError(bool isNullList)
    {
        SetupValidWorkspaceAndDefaults(userId: 1, companyId: 10, workspaceId: 100);
        var request = new AddWorkspaceMembersRequestDto
        {
            Members = isNullList ? null! : new List<AddWorkspaceMemberItemDto>()
        };

        var result = await WorkspaceService.AddMembersAsync(100, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("members", result.Errors!.Keys);
        Assert.Contains("Chọn ít nhất 1 thành viên", result.Errors["members"]);
    }

    // UTC08: Target user is not a member of the company -> Returns Forbidden (NOT_COMPANY_MEMBER)
    [Fact]
    public async Task AddMembersAsync_WhenTargetUserNotInCompany_ReturnsForbidden()
    {
        SetupValidWorkspaceAndDefaults(userId: 1, companyId: 10, workspaceId: 100);
        CompanyRepositoryMock
            .Setup(c => c.IsMemberAsync(10, 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new AddWorkspaceMembersRequestDto
        {
            Members = new List<AddWorkspaceMemberItemDto> { new() { UserId = 99 } }
        };

        var result = await WorkspaceService.AddMembersAsync(100, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("NOT_COMPANY_MEMBER", result.ErrorCode);
        Assert.Equal("Người dùng không thuộc công ty.", result.ErrorMessage);
    }

    // UTC09: Target user is already an active member of the workspace -> Returns Conflict (ALREADY_MEMBER)
    [Fact]
    public async Task AddMembersAsync_WhenTargetUserAlreadyMember_ReturnsConflict()
    {
        SetupValidWorkspaceAndDefaults(userId: 1, companyId: 10, workspaceId: 100);
        WorkspaceRepositoryMock
            .Setup(r => r.IsMemberAsync(100, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new AddWorkspaceMembersRequestDto
        {
            Members = new List<AddWorkspaceMemberItemDto> { new() { UserId = 20 } }
        };

        var result = await WorkspaceService.AddMembersAsync(100, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("ALREADY_MEMBER", result.ErrorCode);
        Assert.Equal("Người dùng đã là thành viên.", result.ErrorMessage);
    }

    // UTC10: Specified RoleId does not exist in company -> Returns NotFound
    [Fact]
    public async Task AddMembersAsync_WhenSpecifiedRoleNotFound_ReturnsNotFound()
    {
        SetupValidWorkspaceAndDefaults(userId: 1, companyId: 10, workspaceId: 100);
        CompanyRepositoryMock
            .Setup(c => c.GetRoleByIdAsync(10, 999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyRole?)null);

        var request = new AddWorkspaceMembersRequestDto
        {
            Members = new List<AddWorkspaceMemberItemDto> { new() { UserId = 20, RoleId = 999 } }
        };

        var result = await WorkspaceService.AddMembersAsync(100, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("Không tìm thấy dữ liệu.", result.ErrorMessage);
    }

    // UTC11: Specified RoleId belongs to another scope (e.g. PROJECT) -> Returns BadRequest (ROLE_SCOPE_MISMATCH)
    [Fact]
    public async Task AddMembersAsync_WhenRoleScopeIsNotWorkspace_ReturnsBadRequest()
    {
        SetupValidWorkspaceAndDefaults(userId: 1, companyId: 10, workspaceId: 100);
        CompanyRepositoryMock
            .Setup(c => c.GetRoleByIdAsync(10, 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyRole
            {
                Id = 12,
                CompanyId = 10,
                Code = CompanyRoleCodes.Developer,
                Scope = RoleScopes.Project,
                IsActive = true
            });

        var request = new AddWorkspaceMembersRequestDto
        {
            Members = new List<AddWorkspaceMemberItemDto> { new() { UserId = 20, RoleId = 12 } }
        };

        var result = await WorkspaceService.AddMembersAsync(100, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("ROLE_SCOPE_MISMATCH", result.ErrorCode);
        Assert.Equal($"Role này thuộc phạm vi {RoleScopes.Project}, không gán ở đây được.", result.ErrorMessage);
    }

    // UTC12: Specified RoleId is inactive (IsActive == false) -> Returns BadRequest (ROLE_INACTIVE)
    [Fact]
    public async Task AddMembersAsync_WhenRoleIsInactive_ReturnsBadRequest()
    {
        SetupValidWorkspaceAndDefaults(userId: 1, companyId: 10, workspaceId: 100);
        CompanyRepositoryMock
            .Setup(c => c.GetRoleByIdAsync(10, 15, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyRole
            {
                Id = 15,
                CompanyId = 10,
                Code = "CUSTOM_WS_ROLE",
                Scope = RoleScopes.Workspace,
                IsActive = false
            });

        var request = new AddWorkspaceMembersRequestDto
        {
            Members = new List<AddWorkspaceMemberItemDto> { new() { UserId = 20, RoleId = 15 } }
        };

        var result = await WorkspaceService.AddMembersAsync(100, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("ROLE_INACTIVE", result.ErrorCode);
        Assert.Equal("Role đang tắt, không gán được.", result.ErrorMessage);
    }

    // =========================================================================
    // 3. BOUNDARY CASES (Members count: 1)
    // =========================================================================

    // UTC13: Boundary - Exactly 1 member in Members list (minimum valid count) -> Returns Created
    [Fact]
    public async Task AddMembersAsync_WithSingleMemberBoundary_ReturnsCreated()
    {
        SetupValidWorkspaceAndDefaults(userId: 1, companyId: 10, workspaceId: 100, defaultMemberRoleId: 6);
        WorkspaceRepositoryMock
            .Setup(r => r.GetMemberDtoAsync(100, 10, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceMemberDto());

        var request = new AddWorkspaceMembersRequestDto
        {
            Members = new List<AddWorkspaceMemberItemDto>
            {
                new() { UserId = 20, RoleId = null }
            }
        };

        var result = await WorkspaceService.AddMembersAsync(100, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Single(result.Data!.Items);
    }
}