using Microsoft.AspNetCore.Http;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Workspaces;
using SSAM_Server.Interfaces.Security;
using SSAM_Server.Models;
using SSAM_Server.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSAM_Server.UnitTests.Services.WorkspaceServiceTests;

public class CreateWorkspaceAsyncTests : WorkspaceServiceTestBase
{
    private (Workspace? CapturedWorkspace, WorkspaceMember? CapturedMember, WorkspaceDto ExpectedDto) SetupSuccessfulCreation(
        long userId = 1,
        long companyId = 10,
        long generatedWorkspaceId = 100,
        long wsAdminRoleId = 5)
    {
        CurrentUserMock
            .Setup(u => u.UserIdOrNull)
            .Returns(userId);

        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(userId, companyId, PermissionKeys.WorkspaceCreate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.WorkspaceCreate }));

        WorkspaceRepositoryMock
            .Setup(r => r.CodeExistsAsync(companyId, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        WorkspaceRepositoryMock
            .Setup(r => r.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<WorkspaceDto>>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<WorkspaceDto>>, CancellationToken>(
                (action, ct) => action(ct));

        Workspace? capturedWorkspace = null;
        WorkspaceRepositoryMock
            .Setup(r => r.AddWorkspaceAsync(It.IsAny<Workspace>(), It.IsAny<CancellationToken>()))
            .Callback<Workspace, CancellationToken>((ws, _) =>
            {
                ws.Id = generatedWorkspaceId;
                capturedWorkspace = ws;
            })
            .Returns(Task.CompletedTask);

        CompanyRepositoryMock
            .Setup(c => c.GetRoleByCodeAsync(companyId, CompanyRoleCodes.WorkspaceAdmin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyRole { Id = wsAdminRoleId, Code = CompanyRoleCodes.WorkspaceAdmin });

        WorkspaceMember? capturedMember = null;
        WorkspaceRepositoryMock
            .Setup(r => r.AddMemberAsync(It.IsAny<WorkspaceMember>(), It.IsAny<CancellationToken>()))
            .Callback<WorkspaceMember, CancellationToken>((member, _) => capturedMember = member)
            .Returns(Task.CompletedTask);

        PermissionServiceMock
            .Setup(p => p.GetWorkspaceAccessAsync(userId, generatedWorkspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkspaceAccessContext?)null);

        var expectedDto = new WorkspaceDto();
        WorkspaceRepositoryMock
            .Setup(r => r.BuildWorkspaceDtoAsync(
                It.IsAny<Workspace>(),
                userId,
                It.IsAny<RoleRefDto?>(),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        return (capturedWorkspace, capturedMember, expectedDto);
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: Create workspace successfully with full valid fields (CompanyId, Name, Code, Description)
    [Fact]
    public async Task CreateWorkspaceAsync_WithFullValidRequest_ReturnsCreatedAndAddsCreatorAsAdmin()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        Workspace? savedWorkspace = null;
        WorkspaceMember? savedMember = null;

        var (_, _, expectedDto) = SetupSuccessfulCreation(userId: 1, companyId: 10, generatedWorkspaceId: 100, wsAdminRoleId: 5);

        WorkspaceRepositoryMock
            .Setup(r => r.AddWorkspaceAsync(It.IsAny<Workspace>(), It.IsAny<CancellationToken>()))
            .Callback<Workspace, CancellationToken>((ws, _) =>
            {
                ws.Id = 100;
                savedWorkspace = ws;
            })
            .Returns(Task.CompletedTask);

        WorkspaceRepositoryMock
            .Setup(r => r.AddMemberAsync(It.IsAny<WorkspaceMember>(), It.IsAny<CancellationToken>()))
            .Callback<WorkspaceMember, CancellationToken>((m, _) => savedMember = m)
            .Returns(Task.CompletedTask);

        var request = new CreateWorkspaceRequestDto
        {
            CompanyId = 10,
            Name = "  Engineering Team  ",
            Code = "  eng-team-01  ",
            Description = "  Không gian làm việc khối kỹ thuật  "
        };

        var result = await WorkspaceService.CreateWorkspaceAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Same(expectedDto, result.Data);

        Assert.NotNull(savedWorkspace);
        Assert.Equal(10, savedWorkspace.CompanyId);
        Assert.Equal("Engineering Team", savedWorkspace.Name);
        Assert.Equal("eng-team-01", savedWorkspace.Code);
        Assert.Equal("Không gian làm việc khối kỹ thuật", savedWorkspace.Description);
        Assert.Equal(1, savedWorkspace.CreatedBy);
        Assert.True(savedWorkspace.IsActive);
        Assert.Equal(now, savedWorkspace.CreatedAt);
        Assert.Equal(now, savedWorkspace.UpdatedAt);

        Assert.NotNull(savedMember);
        Assert.Equal(100, savedMember.WorkspaceId);
        Assert.Equal(1, savedMember.UserId);
        Assert.Equal(5, savedMember.RoleId);
        Assert.True(savedMember.IsActive);
        Assert.Equal(now, savedMember.JoinedAt);

        WorkspaceRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // UTC002: Create workspace without Code & with whitespace Description -> Auto-generates unique code with suffix "-2" & sets Description = null
    [Fact]
    public async Task CreateWorkspaceAsync_WithoutCodeAndBlankDescription_AutoGeneratesUniqueCodeAndNullsDescription()
    {
        Workspace? savedWorkspace = null;
        SetupSuccessfulCreation(userId: 1, companyId: 10, generatedWorkspaceId: 101);

        WorkspaceRepositoryMock
            .SetupSequence(r => r.CodeExistsAsync(10, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)   // "phong-ky-thuat" already exists
            .ReturnsAsync(false); // "phong-ky-thuat-2" is available

        WorkspaceRepositoryMock
            .Setup(r => r.AddWorkspaceAsync(It.IsAny<Workspace>(), It.IsAny<CancellationToken>()))
            .Callback<Workspace, CancellationToken>((ws, _) =>
            {
                ws.Id = 101;
                savedWorkspace = ws;
            })
            .Returns(Task.CompletedTask);

        var request = new CreateWorkspaceRequestDto
        {
            CompanyId = 10,
            Name = "Phòng Kỹ Thuật",
            Code = null,
            Description = "   "
        };

        var result = await WorkspaceService.CreateWorkspaceAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(savedWorkspace);
        Assert.Equal("phong-ky-thuat-2", savedWorkspace.Code);
        Assert.Null(savedWorkspace.Description);
    }

    // UTC003: Create workspace without Code when Name has no alphanumeric chars -> Falls back to baseSlug "workspace"
    [Fact]
    public async Task CreateWorkspaceAsync_WhenNameHasNoAlphanumericCharsAndCodeIsNull_FallsBackToDefaultSlug()
    {
        Workspace? savedWorkspace = null;
        SetupSuccessfulCreation(userId: 1, companyId: 10, generatedWorkspaceId: 102);

        WorkspaceRepositoryMock
            .Setup(r => r.AddWorkspaceAsync(It.IsAny<Workspace>(), It.IsAny<CancellationToken>()))
            .Callback<Workspace, CancellationToken>((ws, _) =>
            {
                ws.Id = 102;
                savedWorkspace = ws;
            })
            .Returns(Task.CompletedTask);

        var request = new CreateWorkspaceRequestDto
        {
            CompanyId = 10,
            Name = "@@@",
            Code = null
        };

        var result = await WorkspaceService.CreateWorkspaceAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(savedWorkspace);
        Assert.Equal("workspace", savedWorkspace.Code);
    }

    // UTC004: Auto-generated baseSlug (50 chars) collides -> Truncates prefix to 46 chars and appends "-2" (48 chars <= 50)
    [Fact]
    public async Task CreateWorkspaceAsync_WhenLongAutoGeneratedSlugCollides_TruncatesPrefixAndAppendsSuffix()
    {
        Workspace? savedWorkspace = null;
        SetupSuccessfulCreation(userId: 1, companyId: 10, generatedWorkspaceId: 103);

        var longName = new string('a', 50);
        WorkspaceRepositoryMock
            .SetupSequence(r => r.CodeExistsAsync(10, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)   // 50 'a's already exists
            .ReturnsAsync(false); // 46 'a's + "-2" is available

        WorkspaceRepositoryMock
            .Setup(r => r.AddWorkspaceAsync(It.IsAny<Workspace>(), It.IsAny<CancellationToken>()))
            .Callback<Workspace, CancellationToken>((ws, _) =>
            {
                ws.Id = 103;
                savedWorkspace = ws;
            })
            .Returns(Task.CompletedTask);

        var request = new CreateWorkspaceRequestDto
        {
            CompanyId = 10,
            Name = longName,
            Code = null
        };

        var result = await WorkspaceService.CreateWorkspaceAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(savedWorkspace);
        Assert.Equal(new string('a', 46) + "-2", savedWorkspace.Code);
        Assert.Equal(48, savedWorkspace.Code.Length);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC005: A company with active departments requires at least one selected department.
    [Fact]
    public async Task CreateWorkspaceAsync_WhenCompanyHasDepartmentsButNoneSelected_ReturnsDepartmentValidationError()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        Db.Departments.Add(new Department { Id = 11, CompanyId = 10, Name = "Engineering", IsActive = true });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await WorkspaceService.CreateWorkspaceAsync(
            new CreateWorkspaceRequestDto { CompanyId = 10, Name = "Engineering Workspace" },
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("departmentIds", result.Errors!.Keys);
        Assert.Contains("Vui lòng chọn ít nhất 1 phòng ban cho workspace", result.Errors["departmentIds"]);
    }

    // UTC006: Selected departments are linked and their active members are added with the workspace-member role.
    [Fact]
    public async Task CreateWorkspaceAsync_WithDepartments_AddsDepartmentLinksAndActiveDepartmentMembers()
    {
        var capturedMembers = new List<WorkspaceMember>();
        SetupSuccessfulCreation(userId: 1, companyId: 10, generatedWorkspaceId: 104, wsAdminRoleId: 5);
        Db.Departments.AddRange(
            new Department { Id = 11, CompanyId = 10, Name = "Engineering", IsActive = true },
            new Department { Id = 12, CompanyId = 10, Name = "Inactive", IsActive = false });
        Db.CompanyMembers.AddRange(
            new CompanyMember { Id = 1, CompanyId = 10, UserId = 1, RoleId = 2, DepartmentId = 11, IsActive = true },
            new CompanyMember { Id = 2, CompanyId = 10, UserId = 20, RoleId = 2, DepartmentId = 11, IsActive = true },
            new CompanyMember { Id = 3, CompanyId = 10, UserId = 21, RoleId = 2, DepartmentId = 11, IsActive = false });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        CompanyRepositoryMock
            .Setup(c => c.ResolveRoleIdAsync(10, CompanyRoleCodes.WorkspaceMember, RoleScopes.Workspace,
                CompanyRoleCodes.WorkspaceMember, It.IsAny<CancellationToken>()))
            .ReturnsAsync(6);
        WorkspaceRepositoryMock
            .Setup(r => r.AddMemberAsync(It.IsAny<WorkspaceMember>(), It.IsAny<CancellationToken>()))
            .Callback<WorkspaceMember, CancellationToken>((member, _) => capturedMembers.Add(member))
            .Returns(Task.CompletedTask);

        var result = await WorkspaceService.CreateWorkspaceAsync(
            new CreateWorkspaceRequestDto { CompanyId = 10, Name = "Engineering Workspace", DepartmentIds = [11, 11] },
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Contains(Db.ChangeTracker.Entries<WorkspaceDepartment>(), e =>
            e.Entity.WorkspaceId == 104 && e.Entity.DepartmentId == 11);
        Assert.Contains(capturedMembers, m => m.UserId == 1 && m.RoleId == 5);
        Assert.Contains(capturedMembers, m => m.UserId == 20 && m.RoleId == 6);
        Assert.DoesNotContain(capturedMembers, m => m.UserId == 21);
    }

    // UTC007: User is not authenticated -> Returns Unauthorized
    [Fact]
    public async Task CreateWorkspaceAsync_WhenUserUnauthenticated_ReturnsUnauthorized()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns((long?)null);
        var request = new CreateWorkspaceRequestDto { CompanyId = 10, Name = "Product Team" };

        var result = await WorkspaceService.CreateWorkspaceAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("Your session is invalid or has expired.", result.ErrorMessage);
    }

    // UTC008: CompanyId is null -> Returns Validation Error
    [Fact]
    public async Task CreateWorkspaceAsync_WhenCompanyIdIsNull_ReturnsValidationError()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        var request = new CreateWorkspaceRequestDto
        {
            CompanyId = null,
            Name = "Personal Workspace"
        };

        var result = await WorkspaceService.CreateWorkspaceAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("companyId", result.Errors!.Keys);
        Assert.Contains("Cần chọn công ty", result.Errors["companyId"]);
    }

    // UTC009: Code format is invalid (uppercase, underscore, or consecutive hyphens) -> Returns Validation Error
    [Theory]
    [InlineData("ENG_TEAM")]
    [InlineData("eng--team")]
    [InlineData("-eng-team")]
    public async Task CreateWorkspaceAsync_WhenCodeFormatIsInvalid_ReturnsValidationError(string invalidCode)
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        var request = new CreateWorkspaceRequestDto
        {
            CompanyId = 10,
            Name = "Engineering Team",
            Code = invalidCode
        };

        var result = await WorkspaceService.CreateWorkspaceAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("code", result.Errors!.Keys);
        Assert.Contains("Mã chỉ gồm chữ thường, số và dấu \"-\" (2–50 ký tự)", result.Errors["code"]);
    }

    // UTC010: Multiple fields invalid simultaneously -> Returns all validation errors
    [Fact]
    public async Task CreateWorkspaceAsync_WhenMultipleFieldsAreInvalid_ReturnsAllValidationErrors()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        var request = new CreateWorkspaceRequestDto
        {
            CompanyId = null,
            Name = "A",
            Code = "INVALID_CODE",
            Description = new string('D', 1001)
        };

        var result = await WorkspaceService.CreateWorkspaceAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal(4, result.Errors!.Count);
        Assert.Contains("companyId", result.Errors.Keys);
        Assert.Contains("name", result.Errors.Keys);
        Assert.Contains("code", result.Errors.Keys);
        Assert.Contains("description", result.Errors.Keys);
    }

    // UTC011: User lacks WorkspaceCreate permission on company -> Returns Forbidden
    [Fact]
    public async Task CreateWorkspaceAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.WorkspaceCreate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Forbidden("FORBIDDEN", "Bạn không có quyền tạo workspace."));

        var request = new CreateWorkspaceRequestDto
        {
            CompanyId = 10,
            Name = "Engineering Team"
        };

        var result = await WorkspaceService.CreateWorkspaceAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    // UTC012: Explicit Code already exists in company -> Returns Conflict (WORKSPACE_CODE_TAKEN)
    [Fact]
    public async Task CreateWorkspaceAsync_WhenExplicitCodeAlreadyExists_ReturnsConflict()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.WorkspaceCreate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.WorkspaceCreate }));
        WorkspaceRepositoryMock
            .Setup(r => r.CodeExistsAsync(10, "eng-team", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new CreateWorkspaceRequestDto
        {
            CompanyId = 10,
            Name = "Engineering Team",
            Code = "eng-team"
        };

        var result = await WorkspaceService.CreateWorkspaceAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("WORKSPACE_CODE_TAKEN", result.ErrorCode);
        Assert.Equal("Mã workspace đã được dùng trong công ty.", result.ErrorMessage);
    }

    // =========================================================================
    // 3. BOUNDARY CASES (Name: 1, 2, 255, 256 | Code: 1, 2, 50, 51 | Description: 1, 1000, 1001)
    // =========================================================================

    // UTC013: Name boundary length 1 (below minimum 2) -> Returns Validation Error
    [Fact]
    public async Task CreateWorkspaceAsync_WhenNameLengthIs1_ReturnsValidationError()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        var request = new CreateWorkspaceRequestDto
        {
            CompanyId = 10,
            Name = "A"
        };

        var result = await WorkspaceService.CreateWorkspaceAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("name", result.Errors!.Keys);
        Assert.Contains("Tên workspace từ 2 đến 255 ký tự", result.Errors["name"]);
    }

    // UTC014 + UTC015: Name boundary length 2 (min) and 255 (max) -> Returns Created
    [Theory]
    [InlineData(2)]     // UTC014: Name 2 characters (lower boundary)
    [InlineData(255)]   // UTC015: Name 255 characters (upper boundary)
    public async Task CreateWorkspaceAsync_WhenNameLengthIsWithinBoundary_ReturnsCreated(int length)
    {
        Workspace? savedWorkspace = null;
        SetupSuccessfulCreation(userId: 1, companyId: 10, generatedWorkspaceId: 100);

        WorkspaceRepositoryMock
            .Setup(r => r.AddWorkspaceAsync(It.IsAny<Workspace>(), It.IsAny<CancellationToken>()))
            .Callback<Workspace, CancellationToken>((ws, _) =>
            {
                ws.Id = 100;
                savedWorkspace = ws;
            })
            .Returns(Task.CompletedTask);

        var validName = new string('A', length);
        var request = new CreateWorkspaceRequestDto
        {
            CompanyId = 10,
            Name = validName,
            Code = "valid-ws"
        };

        var result = await WorkspaceService.CreateWorkspaceAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(validName, savedWorkspace!.Name);
        Assert.Equal(length, savedWorkspace.Name.Length);
    }

    // UTC016: Name boundary length 256 (exceeds maximum 255) -> Returns Validation Error
    [Fact]
    public async Task CreateWorkspaceAsync_WhenNameExceeds255Chars_ReturnsValidationError()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        var request = new CreateWorkspaceRequestDto
        {
            CompanyId = 10,
            Name = new string('A', 256)
        };

        var result = await WorkspaceService.CreateWorkspaceAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("name", result.Errors!.Keys);
        Assert.Contains("Tên workspace từ 2 đến 255 ký tự", result.Errors["name"]);
    }

    // UTC017: Code boundary length 1 (below minimum 2) -> Returns Validation Error
    [Fact]
    public async Task CreateWorkspaceAsync_WhenCodeLengthIs1_ReturnsValidationError()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        var request = new CreateWorkspaceRequestDto
        {
            CompanyId = 10,
            Name = "Boundary Code Workspace",
            Code = "a"
        };

        var result = await WorkspaceService.CreateWorkspaceAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("code", result.Errors!.Keys);
        Assert.Contains("Mã chỉ gồm chữ thường, số và dấu \"-\" (2–50 ký tự)", result.Errors["code"]);
    }

    // UTC018 + UTC019: Code boundary length 2 (min) and 50 (max) -> Returns Created
    [Theory]
    [InlineData(2)]     // UTC018: Code 2 characters (lower boundary)
    [InlineData(50)]    // UTC019: Code 50 characters (upper boundary)
    public async Task CreateWorkspaceAsync_WhenCodeLengthIsWithinBoundary_ReturnsCreated(int length)
    {
        Workspace? savedWorkspace = null;
        SetupSuccessfulCreation(userId: 1, companyId: 10, generatedWorkspaceId: 100);

        WorkspaceRepositoryMock
            .Setup(r => r.AddWorkspaceAsync(It.IsAny<Workspace>(), It.IsAny<CancellationToken>()))
            .Callback<Workspace, CancellationToken>((ws, _) =>
            {
                ws.Id = 100;
                savedWorkspace = ws;
            })
            .Returns(Task.CompletedTask);

        var validCode = new string('a', length);
        var request = new CreateWorkspaceRequestDto
        {
            CompanyId = 10,
            Name = "Boundary Code Workspace",
            Code = validCode
        };

        var result = await WorkspaceService.CreateWorkspaceAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(validCode, savedWorkspace!.Code);
        Assert.Equal(length, savedWorkspace.Code.Length);
    }

    // UTC020: Code boundary length 51 (exceeds maximum 50) -> Returns Validation Error
    [Fact]
    public async Task CreateWorkspaceAsync_WhenCodeExceeds50Chars_ReturnsValidationError()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        var request = new CreateWorkspaceRequestDto
        {
            CompanyId = 10,
            Name = "Long Code Workspace",
            Code = new string('a', 51)
        };

        var result = await WorkspaceService.CreateWorkspaceAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("code", result.Errors!.Keys);
        Assert.Contains("Mã chỉ gồm chữ thường, số và dấu \"-\" (2–50 ký tự)", result.Errors["code"]);
    }

    // UTC021 + UTC022: Description boundary length 1 and 1000 -> Returns Created
    [Theory]
    [InlineData(1)]     // UTC021: Description 1 character
    [InlineData(1000)]  // UTC022: Description 1000 characters (upper boundary)
    public async Task CreateWorkspaceAsync_WhenDescriptionLengthIsWithinBoundary_ReturnsCreated(int length)
    {
        Workspace? savedWorkspace = null;
        SetupSuccessfulCreation(userId: 1, companyId: 10, generatedWorkspaceId: 100);

        WorkspaceRepositoryMock
            .Setup(r => r.AddWorkspaceAsync(It.IsAny<Workspace>(), It.IsAny<CancellationToken>()))
            .Callback<Workspace, CancellationToken>((ws, _) =>
            {
                ws.Id = 100;
                savedWorkspace = ws;
            })
            .Returns(Task.CompletedTask);

        var validDescription = new string('D', length);
        var request = new CreateWorkspaceRequestDto
        {
            CompanyId = 10,
            Name = "Boundary Description Workspace",
            Code = "valid-desc-ws",
            Description = validDescription
        };

        var result = await WorkspaceService.CreateWorkspaceAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(validDescription, savedWorkspace!.Description);
        Assert.Equal(length, savedWorkspace.Description!.Length);
    }

    // UTC023: Description boundary length 1001 (exceeds maximum 1000) -> Returns Validation Error
    [Fact]
    public async Task CreateWorkspaceAsync_WhenDescriptionExceeds1000Chars_ReturnsValidationError()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        var request = new CreateWorkspaceRequestDto
        {
            CompanyId = 10,
            Name = "Long Description Workspace",
            Code = "long-desc-ws",
            Description = new string('D', 1001)
        };

        var result = await WorkspaceService.CreateWorkspaceAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("description", result.Errors!.Keys);
        Assert.Contains("Mô tả tối đa 1000 ký tự", result.Errors["description"]);
    }
}