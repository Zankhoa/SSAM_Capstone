using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Workspaces;
using SSAM_Server.Interfaces.Security;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.WorkspaceServiceTests;

public class UpdateWorkspaceAsyncTests : WorkspaceServiceTestBase
{
    private (Workspace Workspace, WorkspaceDto ExpectedDto) SetupValidAuthorizationAndWorkspace(
        long userId = 1,
        long companyId = 10,
        long workspaceId = 100,
        string existingCode = "eng-team",
        bool isActive = true)
    {
        CurrentUserMock
            .Setup(u => u.UserIdOrNull)
            .Returns(userId);

        var workspace = new Workspace
        {
            Id = workspaceId,
            CompanyId = companyId,
            Name = "Old Workspace Name",
            Code = existingCode,
            Description = "Old Workspace Description",
            IsActive = isActive,
            CreatedAt = Clock.GetUtcNow().UtcDateTime.AddDays(-10),
            UpdatedAt = Clock.GetUtcNow().UtcDateTime.AddDays(-2)
        };

        var permissions = new HashSet<string> { PermissionKeys.WorkspaceUpdate };
        var myRole = new RoleRefDto {
            RoleId = 5,
            Code = CompanyRoleCodes.WorkspaceAdmin,
            Name = "Workspace Admin"
        };
        var context = new WorkspaceAccessContext(workspace, permissions, myRole);

        PermissionServiceMock
            .Setup(p => p.RequireWorkspaceAsync(userId, workspaceId, PermissionKeys.WorkspaceUpdate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<WorkspaceAccessContext>.Ok(context));

        WorkspaceRepositoryMock
            .Setup(r => r.CodeExistsAsync(companyId, It.IsAny<string>(), workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        WorkspaceRepositoryMock
            .Setup(r => r.GetByIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        var expectedDto = new WorkspaceDto();
        WorkspaceRepositoryMock
            .Setup(r => r.BuildWorkspaceDtoAsync(workspace, userId, myRole, permissions, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        return (workspace, expectedDto);
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: Updating all workspace fields succeeds and persists the new values.
    [Fact]
    public async Task UpdateWorkspaceAsync_WithFullValidRequest_ReturnsOkAndUpdatesWorkspace()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var (workspace, expectedDto) = SetupValidAuthorizationAndWorkspace(
            userId: 1, companyId: 10, workspaceId: 100, existingCode: "old-code");

        var request = new UpdateWorkspaceRequestDto
        {
            Name = "  Product Engineering  ",
            Code = "  product-eng-01  ",
            Description = "  Không gian phát triển sản phẩm  "
        };

        var result = await WorkspaceService.UpdateWorkspaceAsync(100, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(expectedDto, result.Data);
        Assert.Equal("Product Engineering", workspace.Name);
        Assert.Equal("product-eng-01", workspace.Code);
        Assert.Equal("Không gian phát triển sản phẩm", workspace.Description);
        Assert.Equal(now, workspace.UpdatedAt);

        WorkspaceRepositoryMock.Verify(r => r.CodeExistsAsync(10, "product-eng-01", 100, It.IsAny<CancellationToken>()), Times.Once);
        WorkspaceRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC002: A partial update preserves fields that were not specified.
    [Fact]
    public async Task UpdateWorkspaceAsync_WithPartialUpdate_PreservesUnspecifiedFields()
    {
        var (workspace, expectedDto) = SetupValidAuthorizationAndWorkspace(
            userId: 1, companyId: 10, workspaceId: 100, existingCode: "keep-code");

        var request = new UpdateWorkspaceRequestDto
        {
            Name = "Updated Name Only"
            // Code and Description are intentionally omitted
        };

        Assert.False(request.DescriptionSpecified);

        var result = await WorkspaceService.UpdateWorkspaceAsync(100, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(expectedDto, result.Data);
        Assert.Equal("Updated Name Only", workspace.Name);
        Assert.Equal("keep-code", workspace.Code);
        Assert.Equal("Old Workspace Description", workspace.Description);

        WorkspaceRepositoryMock.Verify(r => r.CodeExistsAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
        WorkspaceRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC003: An unchanged code skips the uniqueness check while blank description clears the value.
    [Fact]
    public async Task UpdateWorkspaceAsync_WithUnchangedCodeAndBlankDescription_SkipsCodeCheckAndClearsDescription()
    {
        var (workspace, _) = SetupValidAuthorizationAndWorkspace(
            userId: 1, companyId: 10, workspaceId: 100, existingCode: "same-code");

        var request = new UpdateWorkspaceRequestDto
        {
            Code = "same-code",
            Description = "   " // Sets DescriptionSpecified = true
        };

        Assert.True(request.DescriptionSpecified);

        var result = await WorkspaceService.UpdateWorkspaceAsync(100, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal("same-code", workspace.Code);
        Assert.Null(workspace.Description);

        WorkspaceRepositoryMock.Verify(r => r.CodeExistsAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC004: An invalid department ID returns a validation error without saving the workspace.
    [Fact]
    public async Task UpdateWorkspaceAsync_WhenDepartmentIsInvalid_ReturnsDepartmentValidationError()
    {
        SetupValidAuthorizationAndWorkspace(userId: 1, companyId: 10, workspaceId: 100);
        Db.Departments.Add(new Department { Id = 11, CompanyId = 10, Name = "Engineering", IsActive = true });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await WorkspaceService.UpdateWorkspaceAsync(
            100,
            new UpdateWorkspaceRequestDto { DepartmentIds = [999] },
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("departmentIds", result.Errors!.Keys);
        Assert.Contains("Danh sách phòng ban không hợp lệ", result.Errors["departmentIds"]);
        WorkspaceRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC005: Updating departments removes old links, adds new links, and adds members from the new department.
    [Fact]
    public async Task UpdateWorkspaceAsync_WithDepartmentsSynchronizesLinksAndAddsDepartmentMembers()
    {
        var (workspace, expectedDto) = SetupValidAuthorizationAndWorkspace(
            userId: 1, companyId: 10, workspaceId: 100);
        Db.Departments.AddRange(
            new Department { Id = 11, CompanyId = 10, Name = "Old", IsActive = true },
            new Department { Id = 12, CompanyId = 10, Name = "New", IsActive = true });
        var oldLink = new WorkspaceDepartment { WorkspaceId = 100, DepartmentId = 11 };
        Db.WorkspaceDepartments.Add(oldLink);
        Db.CompanyMembers.Add(new CompanyMember
        {
            Id = 1, CompanyId = 10, UserId = 20, RoleId = 2, DepartmentId = 12, IsActive = true
        });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        CompanyRepositoryMock
            .Setup(c => c.ResolveRoleIdAsync(10, CompanyRoleCodes.WorkspaceMember, RoleScopes.Workspace,
                CompanyRoleCodes.WorkspaceMember, It.IsAny<CancellationToken>()))
            .ReturnsAsync(6);
        WorkspaceRepositoryMock
            .Setup(r => r.GetMemberRecordAsync(100, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkspaceMember?)null);

        var result = await WorkspaceService.UpdateWorkspaceAsync(
            100,
            new UpdateWorkspaceRequestDto { DepartmentIds = [12] },
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Same(expectedDto, result.Data);
        Assert.Equal(EntityState.Deleted, Db.Entry(oldLink).State);
        Assert.Contains(Db.ChangeTracker.Entries<WorkspaceDepartment>(), e =>
            e.State == EntityState.Added && e.Entity.WorkspaceId == 100 && e.Entity.DepartmentId == 12);
        WorkspaceRepositoryMock.Verify(r => r.AddMemberAsync(
            It.Is<WorkspaceMember>(m => m.WorkspaceId == 100 && m.UserId == 20 && m.RoleId == 6 && m.IsActive),
            It.IsAny<CancellationToken>()), Times.Once);
        WorkspaceRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC006: User is not authenticated -> Returns Unauthorized
    [Fact]
    public async Task UpdateWorkspaceAsync_WhenUserUnauthenticated_ReturnsUnauthorized()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns((long?)null);
        var request = new UpdateWorkspaceRequestDto { Name = "New Name" };

        var result = await WorkspaceService.UpdateWorkspaceAsync(100, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("Your session is invalid or has expired.", result.ErrorMessage);
    }

    // UTC007: User lacks WorkspaceUpdate permission -> Returns Forbidden
    [Fact]
    public async Task UpdateWorkspaceAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireWorkspaceAsync(1, 100, PermissionKeys.WorkspaceUpdate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<WorkspaceAccessContext>.Forbidden("FORBIDDEN", "Bạn không có quyền cập nhật workspace."));

        var request = new UpdateWorkspaceRequestDto { Name = "New Name" };

        var result = await WorkspaceService.UpdateWorkspaceAsync(100, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    // UTC008: Workspace is archived (IsActive = false) -> Returns Conflict (WORKSPACE_ARCHIVED)
    [Fact]
    public async Task UpdateWorkspaceAsync_WhenWorkspaceIsArchived_ReturnsConflict()
    {
        SetupValidAuthorizationAndWorkspace(userId: 1, companyId: 10, workspaceId: 100, isActive: false);
        var request = new UpdateWorkspaceRequestDto { Name = "Attempt Update Archived" };

        var result = await WorkspaceService.UpdateWorkspaceAsync(100, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("WORKSPACE_ARCHIVED", result.ErrorCode);
        Assert.Equal("Workspace đã được lưu trữ, dữ liệu chỉ đọc.", result.ErrorMessage);
    }

    // UTC009: Code format is invalid (uppercase, underscore, or consecutive hyphens) -> Returns Validation Error
    [Theory]
    [InlineData("INVALID_CODE")]
    [InlineData("ws--01")]
    [InlineData("-ws-01")]
    public async Task UpdateWorkspaceAsync_WhenCodeFormatIsInvalid_ReturnsValidationError(string invalidCode)
    {
        SetupValidAuthorizationAndWorkspace(userId: 1, companyId: 10, workspaceId: 100);
        var request = new UpdateWorkspaceRequestDto { Code = invalidCode };

        var result = await WorkspaceService.UpdateWorkspaceAsync(100, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("code", result.Errors!.Keys);
        Assert.Contains("Mã chỉ gồm chữ thường, số và dấu \"-\" (2–50 ký tự)", result.Errors["code"]);
    }

    // UTC010: Multiple fields are invalid simultaneously -> Returns all validation errors
    [Fact]
    public async Task UpdateWorkspaceAsync_WhenMultipleFieldsAreInvalid_ReturnsAllValidationErrors()
    {
        SetupValidAuthorizationAndWorkspace(userId: 1, companyId: 10, workspaceId: 100);
        var request = new UpdateWorkspaceRequestDto
        {
            Name = "A",
            Code = "BAD_CODE"
        };

        var result = await WorkspaceService.UpdateWorkspaceAsync(100, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal(2, result.Errors!.Count);
        Assert.Contains("name", result.Errors.Keys);
        Assert.Contains("code", result.Errors.Keys);
    }

    // UTC011: New Code already exists on another workspace in company -> Returns Conflict (WORKSPACE_CODE_TAKEN)
    [Fact]
    public async Task UpdateWorkspaceAsync_WhenNewCodeAlreadyExists_ReturnsConflict()
    {
        SetupValidAuthorizationAndWorkspace(userId: 1, companyId: 10, workspaceId: 100, existingCode: "old-code");
        WorkspaceRepositoryMock
            .Setup(r => r.CodeExistsAsync(10, "taken-code", 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new UpdateWorkspaceRequestDto { Code = "taken-code" };

        var result = await WorkspaceService.UpdateWorkspaceAsync(100, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("WORKSPACE_CODE_TAKEN", result.ErrorCode);
        Assert.Equal("Mã workspace đã được dùng trong công ty.", result.ErrorMessage);
    }

    // UTC012: Tracked workspace entity is not found (GetByIdAsync returns null) -> Returns NotFound
    [Fact]
    public async Task UpdateWorkspaceAsync_WhenTrackedWorkspaceNotFound_ReturnsNotFound()
    {
        SetupValidAuthorizationAndWorkspace(userId: 1, companyId: 10, workspaceId: 100);
        WorkspaceRepositoryMock
            .Setup(r => r.GetByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workspace?)null);

        var request = new UpdateWorkspaceRequestDto { Name = "Updated Name" };

        var result = await WorkspaceService.UpdateWorkspaceAsync(100, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("Không tìm thấy dữ liệu.", result.ErrorMessage);
    }

    // =========================================================================
    // 3. BOUNDARY CASES (Name: 1, 2, 255, 256 | Code: 1, 2, 50, 51)
    // =========================================================================

    // UTC013: Name boundary length 1 (below minimum 2) -> Returns Validation Error
    [Fact]
    public async Task UpdateWorkspaceAsync_WhenNameLengthIs1_ReturnsValidationError()
    {
        SetupValidAuthorizationAndWorkspace(userId: 1, companyId: 10, workspaceId: 100);
        var request = new UpdateWorkspaceRequestDto { Name = "A" };

        var result = await WorkspaceService.UpdateWorkspaceAsync(100, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("name", result.Errors!.Keys);
        Assert.Contains("Tên workspace từ 2 đến 255 ký tự", result.Errors["name"]);
    }

    // UTC014 + UTC015: Name boundary length 2 (min) and 255 (max) -> Returns Ok
    [Theory]
    [InlineData(2)]     // UTC014: Name 2 characters (lower boundary)
    [InlineData(255)]   // UTC015: Name 255 characters (upper boundary)
    public async Task UpdateWorkspaceAsync_WhenNameLengthIsWithinBoundary_ReturnsOk(int length)
    {
        var (workspace, _) = SetupValidAuthorizationAndWorkspace(userId: 1, companyId: 10, workspaceId: 100);
        var validName = new string('A', length);
        var request = new UpdateWorkspaceRequestDto { Name = validName };

        var result = await WorkspaceService.UpdateWorkspaceAsync(100, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(validName, workspace.Name);
        Assert.Equal(length, workspace.Name.Length);
    }

    // UTC016: Name boundary length 256 (exceeds maximum 255) -> Returns Validation Error
    [Fact]
    public async Task UpdateWorkspaceAsync_WhenNameExceeds255Chars_ReturnsValidationError()
    {
        SetupValidAuthorizationAndWorkspace(userId: 1, companyId: 10, workspaceId: 100);
        var request = new UpdateWorkspaceRequestDto { Name = new string('A', 256) };

        var result = await WorkspaceService.UpdateWorkspaceAsync(100, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("name", result.Errors!.Keys);
        Assert.Contains("Tên workspace từ 2 đến 255 ký tự", result.Errors["name"]);
    }

    // UTC017: Code boundary length 1 (below minimum 2) -> Returns Validation Error
    [Fact]
    public async Task UpdateWorkspaceAsync_WhenCodeLengthIs1_ReturnsValidationError()
    {
        SetupValidAuthorizationAndWorkspace(userId: 1, companyId: 10, workspaceId: 100);
        var request = new UpdateWorkspaceRequestDto { Code = "a" };

        var result = await WorkspaceService.UpdateWorkspaceAsync(100, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("code", result.Errors!.Keys);
        Assert.Contains("Mã chỉ gồm chữ thường, số và dấu \"-\" (2–50 ký tự)", result.Errors["code"]);
    }

    // UTC018 + UTC019: Code boundary length 2 (min) and 50 (max) -> Returns Ok
    [Theory]
    [InlineData(2)]     // UTC018: Code 2 characters (lower boundary)
    [InlineData(50)]    // UTC019: Code 50 characters (upper boundary)
    public async Task UpdateWorkspaceAsync_WhenCodeLengthIsWithinBoundary_ReturnsOk(int length)
    {
        var (workspace, _) = SetupValidAuthorizationAndWorkspace(userId: 1, companyId: 10, workspaceId: 100);
        var validCode = new string('a', length);
        var request = new UpdateWorkspaceRequestDto { Code = validCode };

        var result = await WorkspaceService.UpdateWorkspaceAsync(100, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(validCode, workspace.Code);
        Assert.Equal(length, workspace.Code.Length);
    }

    // UTC020: Code boundary length 51 (exceeds maximum 50) -> Returns Validation Error
    [Fact]
    public async Task UpdateWorkspaceAsync_WhenCodeExceeds50Chars_ReturnsValidationError()
    {
        SetupValidAuthorizationAndWorkspace(userId: 1, companyId: 10, workspaceId: 100);
        var request = new UpdateWorkspaceRequestDto { Code = new string('a', 51) };

        var result = await WorkspaceService.UpdateWorkspaceAsync(100, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("code", result.Errors!.Keys);
        Assert.Contains("Mã chỉ gồm chữ thường, số và dấu \"-\" (2–50 ký tự)", result.Errors["code"]);
    }
}