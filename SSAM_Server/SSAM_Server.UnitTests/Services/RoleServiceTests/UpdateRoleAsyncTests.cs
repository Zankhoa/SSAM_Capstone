using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Roles;
using SSAM_Server.Models;
using SSAM_Server.Security;
using SSAM_Server.UnitTests.Services.RoleServiceTests;

namespace SSAM_Server.UnitTests.Services.RoleService;

public class UpdateRoleAsyncTests : RoleServiceTestBase
{
    private CompanyRole SetupValidAuthorizationAndRole(
        long userId = 1,
        long companyId = 10,
        long roleId = 50,
        string code = "CUSTOM_ROLE",
        string scope = RoleScopes.Project,
        string kind = RoleKinds.Dev,
        int memberCount = 3)
    {
        CurrentUserMock
            .Setup(u => u.UserIdOrNull)
            .Returns(userId);

        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(userId, companyId, PermissionKeys.RoleManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.RoleManage }));

        var existingRole = new CompanyRole
        {
            Id = roleId,
            CompanyId = companyId,
            Code = code,
            Name = "Old Role Name",
            Description = "Old Description",
            Scope = scope,
            Kind = kind,
            IsSystem = false,
            IsActive = true,
            CreatedAt = Clock.GetUtcNow().UtcDateTime.AddDays(-5),
            UpdatedAt = Clock.GetUtcNow().UtcDateTime.AddDays(-1),
            RolePermissions = new List<RolePermission>()
        };

        RoleRepositoryMock
            .Setup(r => r.GetRoleByIdAsync(companyId, roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRole);

        RoleRepositoryMock
            .Setup(r => r.RoleNameExistsAsync(companyId, It.IsAny<string>(), roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        RoleRepositoryMock
            .Setup(r => r.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<ServiceResult<RoleDto>>>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<ServiceResult<RoleDto>>>, CancellationToken>(
                (action, ct) => action(ct));

        RoleRepositoryMock
            .Setup(r => r.GetSingleRoleMemberCountAsync(existingRole, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memberCount);

        return existingRole;
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: Update a PROJECT role successfully with name, kind, description, and permissions.
    [Fact]
    public async Task UpdateRoleAsync_WithFullValidProjectRoleRequest_ReturnsOkAndUpdatesRole()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var existingRole = SetupValidAuthorizationAndRole(
            userId: 1, companyId: 10, roleId: 50, scope: RoleScopes.Project, kind: RoleKinds.Dev, memberCount: 4);

        var permissions = new List<Permission>
        {
            new() { Id = 1, Code = PermissionKeys.TestCaseView, Scope = RoleScopes.Project, IsActive = true, SortOrder = 1 },
            new() { Id = 2, Code = PermissionKeys.TestCaseExecute, Scope = RoleScopes.Project, IsActive = true, SortOrder = 2 }
        };

        RoleRepositoryMock
            .Setup(r => r.GetPermissionsByCodesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);

        RoleRepositoryMock
            .Setup(r => r.ReplaceRolePermissionsAsync(50, It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .Callback<long, IEnumerable<long>, CancellationToken>((_, _, _) =>
            {
                existingRole.RolePermissions = permissions
                    .Select(p => new RolePermission { RoleId = 50, PermissionId = p.Id, Permission = p })
                    .ToList();
            })
            .Returns(Task.CompletedTask);

        var request = new UpdateRoleRequestDto
        {
            Name = "  Senior QA Engineer  ",
            Kind = "qa",
            Description = "  Phụ trách kiểm thử tự động  ",
            PermissionKeys = new List<string> { PermissionKeys.TestCaseView, PermissionKeys.TestCaseExecute }
        };

        var result = await RoleService.UpdateRoleAsync(10, 50, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(50, result.Data.RoleId);
        Assert.Equal("Senior QA Engineer", result.Data.Name);
        Assert.Equal(RoleKinds.Qa, result.Data.Kind);
        Assert.Equal("Phụ trách kiểm thử tự động", result.Data.Description);
        Assert.Equal(new[] { PermissionKeys.TestCaseView, PermissionKeys.TestCaseExecute }, result.Data.PermissionKeys);
        Assert.Equal(4, result.Data.MemberCount);
        Assert.Equal(now, existingRole.UpdatedAt);

        RoleRepositoryMock.Verify(r => r.ReplaceRolePermissionsAsync(50, It.Is<IEnumerable<long>>(ids => ids.SequenceEqual(new long[] { 1, 2 })), It.IsAny<CancellationToken>()), Times.Once);
        RoleRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC002: Omitting Name preserves the existing name while explicitly updating Description.
    [Fact]
    public async Task UpdateRoleAsync_WhenNameIsOmitted_PreservesNameAndUpdatesDescription()
    {
        var existingRole = SetupValidAuthorizationAndRole(
            userId: 1, companyId: 10, roleId: 50, scope: RoleScopes.Project, kind: RoleKinds.Ba, memberCount: 2);
        existingRole.Description = "Old Description";

        var request = new UpdateRoleRequestDto
        {
            Description = "New Description"
        };

        Assert.Null(request.Name);
        Assert.True(request.DescriptionSpecified);

        var result = await RoleService.UpdateRoleAsync(10, 50, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal("Old Role Name", result.Data!.Name);
        Assert.Equal("New Description", result.Data.Description);
        Assert.Equal("Old Role Name", existingRole.Name);
        Assert.Equal("New Description", existingRole.Description);
        RoleRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC003: A name-only update preserves unspecified description, kind, and permissions.
    [Fact]
    public async Task UpdateRoleAsync_WithPartialUpdate_PreservesUnspecifiedFields()
    {
        var existingRole = SetupValidAuthorizationAndRole(
            userId: 1, companyId: 10, roleId: 50, scope: RoleScopes.Project, kind: RoleKinds.Ba, memberCount: 2);
        existingRole.Description = "Keep Existing Description";

        var request = new UpdateRoleRequestDto
        {
            Name = "Lead Business Analyst"
            // Kind, Description, PermissionKeys are intentionally omitted
        };

        Assert.False(request.DescriptionSpecified);

        var result = await RoleService.UpdateRoleAsync(10, 50, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal("Lead Business Analyst", result.Data!.Name);
        Assert.Equal(RoleKinds.Ba, result.Data.Kind);
        Assert.Equal("Keep Existing Description", result.Data.Description);

        RoleRepositoryMock.Verify(r => r.ReplaceRolePermissionsAsync(It.IsAny<long>(), It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()), Times.Never);
        RoleRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC004: Clearing description on a non-PROJECT role sets it to null and forces Kind to OTHER.
    [Fact]
    public async Task UpdateRoleAsync_WhenDescriptionClearedAndScopeIsTenant_ClearsDescriptionAndForcesKindOther()
    {
        var existingRole = SetupValidAuthorizationAndRole(
            userId: 1, companyId: 10, roleId: 50, scope: RoleScopes.Tenant, kind: RoleKinds.Other, memberCount: 1);

        var request = new UpdateRoleRequestDto
        {
            Description = "   ", // Sets DescriptionSpecified = true
            Kind = RoleKinds.Dev // Ignored because Scope != PROJECT -> forced to OTHER
        };

        Assert.True(request.DescriptionSpecified);

        var result = await RoleService.UpdateRoleAsync(10, 50, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Null(result.Data!.Description);
        Assert.Null(existingRole.Description);
        Assert.Equal(RoleKinds.Other, result.Data.Kind);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC005: An unauthenticated user returns Unauthorized.
    [Fact]
    public async Task UpdateRoleAsync_WhenUserUnauthenticated_ReturnsUnauthorized()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns((long?)null);
        var request = new UpdateRoleRequestDto { Name = "Updated Role" };

        var result = await RoleService.UpdateRoleAsync(10, 50, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("Your session is invalid or has expired.", result.ErrorMessage);
    }

    // UTC006: A user without RoleManage permission returns Forbidden.
    [Fact]
    public async Task UpdateRoleAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.RoleManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Forbidden("FORBIDDEN", "Bạn không có quyền quản lý role."));

        var request = new UpdateRoleRequestDto { Name = "Updated Role" };

        var result = await RoleService.UpdateRoleAsync(10, 50, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    // UTC007: A missing role returns NotFound.
    [Fact]
    public async Task UpdateRoleAsync_WhenRoleNotFound_ReturnsNotFound()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.RoleManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.RoleManage }));
        RoleRepositoryMock
            .Setup(r => r.GetRoleByIdAsync(10, 999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyRole?)null);

        var request = new UpdateRoleRequestDto { Name = "Updated Role" };

        var result = await RoleService.UpdateRoleAsync(10, 999, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("Không tìm thấy dữ liệu.", result.ErrorMessage);
    }

    // UTC008: A locked system role returns Forbidden with ROLE_LOCKED.
    [Theory]
    [InlineData(CompanyRoleCodes.TenantAdmin)]
    [InlineData(CompanyRoleCodes.WorkspaceAdmin)]
    [InlineData(CompanyRoleCodes.ProjectManager)]
    public async Task UpdateRoleAsync_WhenRoleIsLocked_ReturnsForbidden(string lockedCode)
    {
        SetupValidAuthorizationAndRole(userId: 1, companyId: 10, roleId: 50, code: lockedCode);
        var request = new UpdateRoleRequestDto { Name = "Attempt Rename Locked Role" };

        var result = await RoleService.UpdateRoleAsync(10, 50, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("ROLE_LOCKED", result.ErrorCode);
        Assert.Equal("Role hệ thống này được khoá, không chỉnh sửa được.", result.ErrorMessage);
    }

    // UTC009: A duplicate role name returns Conflict with ROLE_NAME_TAKEN.
    [Fact]
    public async Task UpdateRoleAsync_WhenRoleNameAlreadyExists_ReturnsConflict()
    {
        SetupValidAuthorizationAndRole(userId: 1, companyId: 10, roleId: 50);
        RoleRepositoryMock
            .Setup(r => r.RoleNameExistsAsync(10, "Duplicate Role Name", 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new UpdateRoleRequestDto { Name = "Duplicate Role Name" };

        var result = await RoleService.UpdateRoleAsync(10, 50, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("ROLE_NAME_TAKEN", result.ErrorCode);
        Assert.Equal("Tên role đã tồn tại trong công ty.", result.ErrorMessage);
    }

    // UTC010: An invalid kind for a PROJECT role returns a validation error.
    [Fact]
    public async Task UpdateRoleAsync_WhenProjectKindIsInvalid_ReturnsValidationError()
    {
        SetupValidAuthorizationAndRole(userId: 1, companyId: 10, roleId: 50, scope: RoleScopes.Project);
        var request = new UpdateRoleRequestDto { Kind = "INVALID_KIND" };

        var result = await RoleService.UpdateRoleAsync(10, 50, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("kind", result.Errors!.Keys);
        Assert.Contains("Chọn nhóm chuyên môn hợp lệ", result.Errors["kind"]);
    }

    // UTC011: A non-existent permission key returns BadRequest with UNKNOWN_PERMISSION.
    [Fact]
    public async Task UpdateRoleAsync_WhenPermissionKeyDoesNotExist_ReturnsBadRequest()
    {
        SetupValidAuthorizationAndRole(userId: 1, companyId: 10, roleId: 50, scope: RoleScopes.Project);
        RoleRepositoryMock
            .Setup(r => r.GetPermissionsByCodesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Permission>());

        var request = new UpdateRoleRequestDto
        {
            PermissionKeys = new List<string> { "non.existent.permission" }
        };

        var result = await RoleService.UpdateRoleAsync(10, 50, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("UNKNOWN_PERMISSION", result.ErrorCode);
        Assert.Equal("Quyền \"non.existent.permission\" không tồn tại.", result.ErrorMessage);
    }

    // UTC012: A permission from another scope returns BadRequest with PERMISSION_SCOPE_MISMATCH.
    [Fact]
    public async Task UpdateRoleAsync_WhenPermissionScopeMismatchesRoleScope_ReturnsBadRequest()
    {
        SetupValidAuthorizationAndRole(userId: 1, companyId: 10, roleId: 50, scope: RoleScopes.Project);
        RoleRepositoryMock
            .Setup(r => r.GetPermissionsByCodesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Permission>
            {
                new() { Id = 1, Code = PermissionKeys.CompanyUpdate, Scope = RoleScopes.Tenant, IsActive = true }
            });

        var request = new UpdateRoleRequestDto
        {
            PermissionKeys = new List<string> { PermissionKeys.CompanyUpdate }
        };

        var result = await RoleService.UpdateRoleAsync(10, 50, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("PERMISSION_SCOPE_MISMATCH", result.ErrorCode);
        Assert.Equal($"Quyền \"{PermissionKeys.CompanyUpdate}\" không thuộc phạm vi {RoleScopes.Project}.", result.ErrorMessage);
    }

    // UTC013: A role deleted before the transaction executes returns NotFound inside the transaction.
    [Fact]
    public async Task UpdateRoleAsync_WhenRoleDeletedConcurrentlyBeforeTransaction_ReturnsNotFound()
    {
        var existingRole = SetupValidAuthorizationAndRole(userId: 1, companyId: 10, roleId: 50);
        RoleRepositoryMock
            .SetupSequence(r => r.GetRoleByIdAsync(10, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRole)      // Initial lookup succeeds
            .ReturnsAsync((CompanyRole?)null); // Inside transaction returns null

        var request = new UpdateRoleRequestDto { Name = "Updated Name" };

        var result = await RoleService.UpdateRoleAsync(10, 50, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("Không tìm thấy dữ liệu.", result.ErrorMessage);
    }

    // =========================================================================
    // 3. BOUNDARY CASES (Name: 1, 2, 100, 101 | Description: 1, 500, 501)
    // =========================================================================

    // UTC014: A one-character name returns a validation error.
    [Fact]
    public async Task UpdateRoleAsync_WhenNameLengthIs1_ReturnsValidationError()
    {
        SetupValidAuthorizationAndRole(userId: 1, companyId: 10, roleId: 50);
        var request = new UpdateRoleRequestDto { Name = "A" };

        var result = await RoleService.UpdateRoleAsync(10, 50, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("name", result.Errors!.Keys);
        Assert.Contains("Tên role từ 2 đến 100 ký tự", result.Errors["name"]);
    }

    // UTC015 + UTC016: Name boundary lengths 2 and 100 return Ok.
    [Theory]
    [InlineData(2)]     // UTC015: Name contains 2 characters.
    [InlineData(100)]   // UTC016: Name contains 100 characters.
    public async Task UpdateRoleAsync_WhenNameLengthIsWithinBoundary_ReturnsOk(int length)
    {
        SetupValidAuthorizationAndRole(userId: 1, companyId: 10, roleId: 50);
        var validName = new string('A', length);
        var request = new UpdateRoleRequestDto { Name = validName };

        var result = await RoleService.UpdateRoleAsync(10, 50, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(validName, result.Data!.Name);
        Assert.Equal(length, result.Data.Name.Length);
    }

    // UTC017: A 101-character name returns a validation error.
    [Fact]
    public async Task UpdateRoleAsync_WhenNameExceeds100Chars_ReturnsValidationError()
    {
        SetupValidAuthorizationAndRole(userId: 1, companyId: 10, roleId: 50);
        var request = new UpdateRoleRequestDto { Name = new string('A', 101) };

        var result = await RoleService.UpdateRoleAsync(10, 50, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("name", result.Errors!.Keys);
        Assert.Contains("Tên role từ 2 đến 100 ký tự", result.Errors["name"]);
    }

    // UTC018 + UTC019: Description boundary lengths 1 and 500 return Ok.
    [Theory]
    [InlineData(1)]     // UTC018: Description contains 1 character.
    [InlineData(500)]   // UTC019: Description contains 500 characters.
    public async Task UpdateRoleAsync_WhenDescriptionLengthIsWithinBoundary_ReturnsOk(int length)
    {
        SetupValidAuthorizationAndRole(userId: 1, companyId: 10, roleId: 50);
        var validDescription = new string('D', length);
        var request = new UpdateRoleRequestDto { Description = validDescription };

        var result = await RoleService.UpdateRoleAsync(10, 50, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(validDescription, result.Data!.Description);
        Assert.Equal(length, result.Data.Description!.Length);
    }

    // UTC020: A 501-character description propagates the database update exception.
    [Fact]
    public async Task UpdateRoleAsync_WhenDescriptionExceeds500Chars_ThrowsDbUpdateException()
    {
        SetupValidAuthorizationAndRole(userId: 1, companyId: 10, roleId: 50);
        RoleRepositoryMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("String or binary data would be truncated in table 'CompanyRoles', column 'Description'."));

        var request = new UpdateRoleRequestDto { Description = new string('D', 501) };

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            RoleService.UpdateRoleAsync(10, 50, request, TestContext.Current.CancellationToken));
    }
}