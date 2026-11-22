using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Roles;
using SSAM_Server.Models;
using SSAM_Server.Security;
using SSAM_Server.UnitTests.Services.RoleServiceTests;

namespace SSAM_Server.UnitTests.Services.RoleService;

public class CreateRoleAsyncTests : RoleServiceTestBase
{
    private void SetupSuccessfulCreation(long userId = 1, long companyId = 10, long generatedRoleId = 50, List<Permission>? permissions = null)
    {
        CurrentUserMock
            .Setup(u => u.UserIdOrNull)
            .Returns(userId);

        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(userId, companyId, PermissionKeys.RoleManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.RoleManage }));

        RoleRepositoryMock
            .Setup(r => r.RoleNameExistsAsync(companyId, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        RoleRepositoryMock
            .Setup(r => r.RoleCodeExistsAsync(companyId, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        RoleRepositoryMock
            .Setup(r => r.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<ServiceResult<RoleDto>>>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<ServiceResult<RoleDto>>>, CancellationToken>(
                (action, ct) => action(ct));

        CompanyRole? capturedRole = null;
        RoleRepositoryMock
            .Setup(r => r.AddRoleAsync(It.IsAny<CompanyRole>(), It.IsAny<CancellationToken>()))
            .Callback<CompanyRole, CancellationToken>((role, _) =>
            {
                role.Id = generatedRoleId;
                role.RolePermissions = (permissions ?? new List<Permission>())
                    .Select(p => new RolePermission { RoleId = generatedRoleId, PermissionId = p.Id, Permission = p })
                    .ToList();
                capturedRole = role;
            })
            .Returns(Task.CompletedTask);

        RoleRepositoryMock
            .Setup(r => r.GetRoleByIdAsync(companyId, generatedRoleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => capturedRole);
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: Create a PROJECT role successfully with explicit code, kind, description, and permissions.
    [Fact]
    public async Task CreateRoleAsync_WithFullValidProjectRoleRequest_ReturnsCreated()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var permissions = new List<Permission>
        {
            new() { Id = 1, Code = PermissionKeys.IssueView, Scope = RoleScopes.Project, IsActive = true, SortOrder = 1 },
            new() { Id = 2, Code = PermissionKeys.IssueCreate, Scope = RoleScopes.Project, IsActive = true, SortOrder = 2 }
        };

        SetupSuccessfulCreation(userId: 1, companyId: 10, generatedRoleId: 50, permissions: permissions);
        RoleRepositoryMock
            .Setup(r => r.GetPermissionsByCodesAsync(
                It.Is<IEnumerable<string>>(keys => keys.Count() == 2),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);

        var request = new CreateRoleRequestDto
        {
            Name = "  Backend Developer  ",
            Code = "  BACKEND_DEV  ",
            Description = "  Phát triển hệ thống API  ",
            Scope = "project",
            Kind = "dev",
            PermissionKeys = new List<string> { "issue.view", " issue.create ", "issue.view" }
        };

        var result = await RoleService.CreateRoleAsync(10, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(50, result.Data.RoleId);
        Assert.Equal("Backend Developer", result.Data.Name);
        Assert.Equal("BACKEND_DEV", result.Data.Code);
        Assert.Equal("Phát triển hệ thống API", result.Data.Description);
        Assert.Equal(RoleScopes.Project, result.Data.Scope);
        Assert.Equal(RoleKinds.Dev, result.Data.Kind);
        Assert.False(result.Data.IsSystem);
        Assert.True(result.Data.IsActive);
        Assert.Equal(new[] { PermissionKeys.IssueView, PermissionKeys.IssueCreate }, result.Data.PermissionKeys);
        Assert.Equal(now, result.Data.CreatedAt);

        RoleRepositoryMock.Verify(r => r.ReplaceRolePermissionsAsync(50, It.Is<IEnumerable<long>>(ids => ids.SequenceEqual(new long[] { 1, 2 })), It.IsAny<CancellationToken>()), Times.Once);
        RoleRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // UTC002: Create a TENANT role without code or permissions; generate a unique code and ignore Kind.
    [Fact]
    public async Task CreateRoleAsync_WithoutCodeAndPermissions_AutoGeneratesCodeWithSuffixAndReturnsCreated()
    {
        SetupSuccessfulCreation(userId: 1, companyId: 10, generatedRoleId: 51);
        RoleRepositoryMock
            .SetupSequence(r => r.RoleCodeExistsAsync(10, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)   // "HR_MANAGER" already exists -> triggers suffix _2
            .ReturnsAsync(false); // "HR_MANAGER_2" is available

        var request = new CreateRoleRequestDto
        {
            Name = "HR Manager",
            Code = null,
            Description = "   ",
            Scope = RoleScopes.Tenant,
            Kind = RoleKinds.Pm, // Should be ignored because Scope != PROJECT
            PermissionKeys = null
        };

        var result = await RoleService.CreateRoleAsync(10, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("HR_MANAGER_2", result.Data.Code);
        Assert.Null(result.Data.Description);
        Assert.Equal(RoleScopes.Tenant, result.Data.Scope);
        Assert.Equal(RoleKinds.Other, result.Data.Kind);
        Assert.Empty(result.Data.PermissionKeys);

        RoleRepositoryMock.Verify(r => r.ReplaceRolePermissionsAsync(It.IsAny<long>(), It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()), Times.Never);
        RoleRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC003: Fall back to the default ROLE code when the name produces no alphanumeric code.
    [Fact]
    public async Task CreateRoleAsync_WhenNameHasNoAlphanumericCharsAndCodeIsNull_FallsBackToDefaultRoleCode()
    {
        SetupSuccessfulCreation(userId: 1, companyId: 10, generatedRoleId: 52);

        var request = new CreateRoleRequestDto
        {
            Name = "@@",
            Code = null,
            Scope = RoleScopes.Workspace
        };

        var result = await RoleService.CreateRoleAsync(10, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal("ROLE", result.Data!.Code);
        Assert.Equal(RoleScopes.Workspace, result.Data.Scope);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC004: An unauthenticated user returns Unauthorized.
    [Fact]
    public async Task CreateRoleAsync_WhenUserUnauthenticated_ReturnsUnauthorized()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns((long?)null);
        var request = new CreateRoleRequestDto { Name = "QA Lead", Scope = RoleScopes.Project };

        var result = await RoleService.CreateRoleAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("Your session is invalid or has expired.", result.ErrorMessage);
    }

    // UTC005: A user without RoleManage permission returns Forbidden.
    [Fact]
    public async Task CreateRoleAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.RoleManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Forbidden("FORBIDDEN", "Bạn không có quyền quản lý role."));

        var request = new CreateRoleRequestDto { Name = "QA Lead", Scope = RoleScopes.Project };

        var result = await RoleService.CreateRoleAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    // UTC006 + UTC007: An empty or invalid scope returns a validation error.
    [Theory]
    [InlineData("")]          // UTC006: Empty scope.
    [InlineData("GLOBAL")]    // UTC007: Unsupported scope.
    public async Task CreateRoleAsync_WhenScopeIsInvalid_ReturnsValidationError(string invalidScope)
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.RoleManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.RoleManage }));

        var request = new CreateRoleRequestDto
        {
            Name = "Custom Role",
            Scope = invalidScope
        };

        var result = await RoleService.CreateRoleAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("scope", result.Errors!.Keys);
        Assert.Contains("Chọn phạm vi áp dụng", result.Errors["scope"]);
    }

    // UTC008: An invalid project-role kind returns a validation error.
    [Fact]
    public async Task CreateRoleAsync_WhenProjectKindIsInvalid_ReturnsValidationError()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.RoleManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.RoleManage }));

        var request = new CreateRoleRequestDto
        {
            Name = "Project Member",
            Scope = RoleScopes.Project,
            Kind = "INVALID_KIND"
        };

        var result = await RoleService.CreateRoleAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("kind", result.Errors!.Keys);
        Assert.Contains("Chọn nhóm chuyên môn hợp lệ", result.Errors["kind"]);
    }

    // UTC009 + UTC010: An invalid role-code format returns a validation error.
    [Theory]
    [InlineData("custom_role")]   // UTC009: Lowercase letters are not allowed.
    [InlineData("1CUSTOM_ROLE")]  // UTC010: The code cannot start with a digit.
    public async Task CreateRoleAsync_WhenCodeFormatIsInvalid_ReturnsValidationError(string invalidCode)
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.RoleManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.RoleManage }));

        var request = new CreateRoleRequestDto
        {
            Name = "Custom Role",
            Scope = RoleScopes.Tenant,
            Code = invalidCode
        };

        var result = await RoleService.CreateRoleAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("code", result.Errors!.Keys);
        Assert.Contains("Mã viết HOA, bắt đầu bằng chữ, chỉ gồm A-Z 0-9 _", result.Errors["code"]);
    }

    // UTC011: Multiple invalid fields return all validation errors.
    [Fact]
    public async Task CreateRoleAsync_WhenMultipleFieldsAreInvalid_ReturnsAllValidationErrors()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.RoleManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.RoleManage }));

        var request = new CreateRoleRequestDto
        {
            Name = "A",
            Scope = "INVALID",
            Code = "bad-code"
        };

        var result = await RoleService.CreateRoleAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal(3, result.Errors!.Count);
        Assert.Contains("name", result.Errors.Keys);
        Assert.Contains("scope", result.Errors.Keys);
        Assert.Contains("code", result.Errors.Keys);
    }

    // UTC012: A duplicate role name returns Conflict.
    [Fact]
    public async Task CreateRoleAsync_WhenRoleNameAlreadyExists_ReturnsConflict()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.RoleManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.RoleManage }));
        RoleRepositoryMock
            .Setup(r => r.RoleNameExistsAsync(10, "Project Lead", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new CreateRoleRequestDto
        {
            Name = "Project Lead",
            Scope = RoleScopes.Project
        };

        var result = await RoleService.CreateRoleAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("ROLE_NAME_TAKEN", result.ErrorCode);
        Assert.Equal("Tên role đã tồn tại trong công ty.", result.ErrorMessage);
    }

    // UTC013: A duplicate explicit role code returns Conflict.
    [Fact]
    public async Task CreateRoleAsync_WhenExplicitRoleCodeAlreadyExists_ReturnsConflict()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.RoleManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.RoleManage }));
        RoleRepositoryMock
            .Setup(r => r.RoleNameExistsAsync(10, "Project Lead", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        RoleRepositoryMock
            .Setup(r => r.RoleCodeExistsAsync(10, "PROJECT_LEAD", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new CreateRoleRequestDto
        {
            Name = "Project Lead",
            Code = "PROJECT_LEAD",
            Scope = RoleScopes.Project
        };

        var result = await RoleService.CreateRoleAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("ROLE_CODE_TAKEN", result.ErrorCode);
        Assert.Equal("Mã role đã tồn tại trong công ty.", result.ErrorMessage);
    }

    // UTC014: A non-existent permission key returns BadRequest with UNKNOWN_PERMISSION.
    [Fact]
    public async Task CreateRoleAsync_WhenPermissionKeyDoesNotExist_ReturnsBadRequest()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.RoleManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.RoleManage }));
        RoleRepositoryMock
            .Setup(r => r.RoleNameExistsAsync(10, "QA Role", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        RoleRepositoryMock
            .Setup(r => r.RoleCodeExistsAsync(10, "QA_ROLE", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        RoleRepositoryMock
            .Setup(r => r.GetPermissionsByCodesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Permission>());

        var request = new CreateRoleRequestDto
        {
            Name = "QA Role",
            Code = "QA_ROLE",
            Scope = RoleScopes.Project,
            PermissionKeys = new List<string> { "unknown.permission" }
        };

        var result = await RoleService.CreateRoleAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("UNKNOWN_PERMISSION", result.ErrorCode);
        Assert.Equal("Quyền \"unknown.permission\" không tồn tại.", result.ErrorMessage);
    }

    // UTC015: A permission from another scope returns BadRequest with PERMISSION_SCOPE_MISMATCH.
    [Fact]
    public async Task CreateRoleAsync_WhenPermissionScopeMismatchesRoleScope_ReturnsBadRequest()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.RoleManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.RoleManage }));
        RoleRepositoryMock
            .Setup(r => r.RoleNameExistsAsync(10, "QA Role", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        RoleRepositoryMock
            .Setup(r => r.RoleCodeExistsAsync(10, "QA_ROLE", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        RoleRepositoryMock
            .Setup(r => r.GetPermissionsByCodesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Permission>
            {
                new() { Id = 1, Code = PermissionKeys.CompanyView, Scope = RoleScopes.Tenant, IsActive = true }
            });

        var request = new CreateRoleRequestDto
        {
            Name = "QA Role",
            Code = "QA_ROLE",
            Scope = RoleScopes.Project,
            PermissionKeys = new List<string> { PermissionKeys.CompanyView }
        };

        var result = await RoleService.CreateRoleAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("PERMISSION_SCOPE_MISMATCH", result.ErrorCode);
        Assert.Equal($"Quyền \"{PermissionKeys.CompanyView}\" không thuộc phạm vi {RoleScopes.Project}.", result.ErrorMessage);
    }

    // =========================================================================
    // 3. BOUNDARY CASES (Name: 1, 2, 100, 101 | Code: 1, 2, 50, 51 | Description: 1, 500, 501)
    // =========================================================================

    // UTC016: A one-character name returns a validation error.
    [Fact]
    public async Task CreateRoleAsync_WhenNameLengthIs1_ReturnsValidationError()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.RoleManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.RoleManage }));

        var request = new CreateRoleRequestDto
        {
            Name = "A",
            Scope = RoleScopes.Tenant
        };

        var result = await RoleService.CreateRoleAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("name", result.Errors!.Keys);
        Assert.Contains("Tên role từ 2 đến 100 ký tự", result.Errors["name"]);
    }

    // UTC017 + UTC018: Name boundary lengths 2 and 100 return Created.
    [Theory]
    [InlineData(2)]     // UTC017: Name contains 2 characters.
    [InlineData(100)]   // UTC018: Name contains 100 characters.
    public async Task CreateRoleAsync_WhenNameLengthIsWithinBoundary_ReturnsCreated(int length)
    {
        var validName = new string('A', length);
        SetupSuccessfulCreation(userId: 1, companyId: 10, generatedRoleId: 50);

        var request = new CreateRoleRequestDto
        {
            Name = validName,
            Code = "BOUNDARY_ROLE",
            Scope = RoleScopes.Tenant
        };

        var result = await RoleService.CreateRoleAsync(10, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(validName, result.Data!.Name);
        Assert.Equal(length, result.Data.Name.Length);
    }

    // UTC019: A 101-character name returns a validation error.
    [Fact]
    public async Task CreateRoleAsync_WhenNameExceeds100Chars_ReturnsValidationError()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.RoleManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.RoleManage }));

        var request = new CreateRoleRequestDto
        {
            Name = new string('A', 101),
            Scope = RoleScopes.Tenant
        };

        var result = await RoleService.CreateRoleAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("name", result.Errors!.Keys);
        Assert.Contains("Tên role từ 2 đến 100 ký tự", result.Errors["name"]);
    }

    // UTC020: A one-character code returns a validation error.
    [Fact]
    public async Task CreateRoleAsync_WhenCodeLengthIs1_ReturnsValidationError()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.RoleManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.RoleManage }));

        var request = new CreateRoleRequestDto
        {
            Name = "Boundary Code Role",
            Code = "A",
            Scope = RoleScopes.Tenant
        };

        var result = await RoleService.CreateRoleAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("code", result.Errors!.Keys);
        Assert.Contains("Mã viết HOA, bắt đầu bằng chữ, chỉ gồm A-Z 0-9 _", result.Errors["code"]);
    }

    // UTC021 + UTC022: Code boundary lengths 2 and 50 return Created.
    [Theory]
    [InlineData(2)]     // UTC021: Code contains 2 characters.
    [InlineData(50)]    // UTC022: Code contains 50 characters.
    public async Task CreateRoleAsync_WhenCodeLengthIsWithinBoundary_ReturnsCreated(int length)
    {
        var validCode = new string('A', length);
        SetupSuccessfulCreation(userId: 1, companyId: 10, generatedRoleId: 50);

        var request = new CreateRoleRequestDto
        {
            Name = "Boundary Code Role",
            Code = validCode,
            Scope = RoleScopes.Tenant
        };

        var result = await RoleService.CreateRoleAsync(10, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(validCode, result.Data!.Code);
        Assert.Equal(length, result.Data!.Code!.Length);
    }

    // UTC023: A 51-character code returns a validation error.
    [Fact]
    public async Task CreateRoleAsync_WhenCodeExceeds50Chars_ReturnsValidationError()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.RoleManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.RoleManage }));

        var request = new CreateRoleRequestDto
        {
            Name = "Long Code Role",
            Code = new string('A', 51),
            Scope = RoleScopes.Tenant
        };

        var result = await RoleService.CreateRoleAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("code", result.Errors!.Keys);
        Assert.Contains("Mã viết HOA, bắt đầu bằng chữ, chỉ gồm A-Z 0-9 _", result.Errors["code"]);
    }

    // UTC024 + UTC025: Description boundary lengths 1 and 500 return Created.
    [Theory]
    [InlineData(1)]     // UTC024: Description contains 1 character.
    [InlineData(500)]   // UTC025: Description contains 500 characters.
    public async Task CreateRoleAsync_WhenDescriptionLengthIsWithinBoundary_ReturnsCreated(int length)
    {
        var validDescription = new string('D', length);
        SetupSuccessfulCreation(userId: 1, companyId: 10, generatedRoleId: 50);

        var request = new CreateRoleRequestDto
        {
            Name = "Boundary Description Role",
            Code = "BOUNDARY_DESC_ROLE",
            Description = validDescription,
            Scope = RoleScopes.Tenant
        };

        var result = await RoleService.CreateRoleAsync(10, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(validDescription, result.Data!.Description);
        Assert.Equal(length, result.Data.Description!.Length);
    }

    // UTC026: A 501-character description propagates the database update exception.
    [Fact]
    public async Task CreateRoleAsync_WhenDescriptionExceeds500Chars_ThrowsDbUpdateException()
    {
        var longDescription = new string('D', 501);
        SetupSuccessfulCreation(userId: 1, companyId: 10, generatedRoleId: 50);

        RoleRepositoryMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("String or binary data would be truncated in table 'CompanyRoles', column 'Description'."));

        var request = new CreateRoleRequestDto
        {
            Name = "Long Description Role",
            Code = "LONG_DESC_ROLE",
            Description = longDescription,
            Scope = RoleScopes.Tenant
        };

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            RoleService.CreateRoleAsync(10, request, TestContext.Current.CancellationToken));
    }
}