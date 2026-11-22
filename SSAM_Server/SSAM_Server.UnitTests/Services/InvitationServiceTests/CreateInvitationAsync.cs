using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Invitations;
using SSAM_Server.Interfaces.Security;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.InvitationServiceTests;

public class CreateInvitationAsyncTests : InvitationServiceTestBase
{
    private (Company Company, Workspace Workspace) SetupValidCompanyAndCaller(
        long callerUserId = 1,
        long companyId = 10,
        bool companyActive = true,
        long workspaceId = 100,
        long workspaceCompanyId = 10)
    {
        CurrentUserMock
            .Setup(u => u.UserIdOrNull)
            .Returns(callerUserId);

        var company = new Company
        {
            Id = companyId,
            Name = "Acme Tech Corp",
            IsActive = companyActive
        };

        CompanyRepositoryMock
            .Setup(c => c.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(callerUserId, companyId, PermissionKeys.UserInvite, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Ok(new HashSet<string> { PermissionKeys.UserInvite }));

        CompanyRepositoryMock
            .Setup(c => c.IsEmailMemberAsync(companyId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var workspace = new Workspace
        {
            Id = workspaceId,
            CompanyId = workspaceCompanyId,
            Name = "Engineering Workspace",
            IsActive = true
        };

        var wsContext = new WorkspaceAccessContext(
            workspace,
            new HashSet<string> { PermissionKeys.WorkspaceMemberManage },
            new RoleRefDto {
                RoleId = 5,
                Code = CompanyRoleCodes.WorkspaceAdmin,
                Name = "Workspace Admin"
            });

        PermissionServiceMock
            .Setup(p => p.RequireWorkspaceAsync(callerUserId, workspaceId, PermissionKeys.WorkspaceMemberManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<WorkspaceAccessContext>.Ok(wsContext));

        InvitationRepositoryMock
            .Setup(r => r.AddInvitationAsync(It.IsAny<CompanyInvitation>(), It.IsAny<CancellationToken>()))
            .Callback<CompanyInvitation, CancellationToken>((inv, _) => inv.Id = 500)
            .Returns(Task.CompletedTask);

        return (company, workspace);
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: Create invitation with full valid fields (Tenant RoleId + WorkspaceId + WorkspaceRoleId) -> Returns Created & sends email
    [Fact]
    public async Task CreateInvitationAsync_WithFullValidTenantAndWorkspaceRoles_ReturnsCreatedAndSendsEmail()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupValidCompanyAndCaller(callerUserId: 1, companyId: 10, workspaceId: 100);
        ClientAppOptions.FrontendBaseUrl = "https://app.ssam.vn/";

        CompanyRepositoryMock
            .Setup(c => c.GetRoleByIdAsync(10, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyRole
            {
                Id = 2,
                CompanyId = 10,
                Code = CompanyRoleCodes.Member,
                Name = "Company Member",
                Scope = RoleScopes.Tenant,
                IsActive = true
            });

        CompanyRepositoryMock
            .Setup(c => c.GetRoleByIdAsync(10, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyRole
            {
                Id = 7,
                CompanyId = 10,
                Code = CompanyRoleCodes.ProjectInitiator,
                Name = "Project Initiator",
                Scope = RoleScopes.Workspace,
                IsActive = true
            });

        CompanyInvitation? capturedInvite = null;
        InvitationRepositoryMock
            .Setup(r => r.AddInvitationAsync(It.IsAny<CompanyInvitation>(), It.IsAny<CancellationToken>()))
            .Callback<CompanyInvitation, CancellationToken>((inv, _) =>
            {
                inv.Id = 501;
                capturedInvite = inv;
            })
            .Returns(Task.CompletedTask);

        var request = new CreateInvitationRequestDto
        {
            Email = "  new.dev@acme.com  ",
            RoleId = 2,
            RoleName = "Ignored Fallback Name",
            WorkspaceId = 100,
            WorkspaceRoleId = 7
        };

        var result = await InvitationService.CreateInvitationAsync(10, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(501, result.Data.InvitationId);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.Token));
        Assert.Equal($"https://app.ssam.vn/invite/{result.Data.Token}", result.Data.InviteUrl);
        Assert.Equal(now.AddDays(7), result.Data.ExpiresAt);

        Assert.NotNull(capturedInvite);
        Assert.Equal(10, capturedInvite.CompanyId);
        Assert.Equal("new.dev@acme.com", capturedInvite.Email);
        Assert.Equal(2, capturedInvite.RoleId);
        Assert.Equal("Company Member", capturedInvite.RoleName);
        Assert.Equal(100, capturedInvite.WorkspaceId);
        Assert.Equal(7, capturedInvite.WorkspaceRoleId);
        Assert.Equal("pending", capturedInvite.Status);
        Assert.Equal(now, capturedInvite.CreatedAt);
        Assert.Equal(now.AddDays(7), capturedInvite.ExpiresAt);

        InvitationRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        EmailServiceMock.Verify(e => e.SendInvitationEmailAsync("new.dev@acme.com", "Acme Tech Corp", result.Data.Token, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC002: Create invitation without RoleId & WorkspaceId -> Uses fallback RoleName ("Member" when null) & clears WorkspaceRoleId
    [Fact]
    public async Task CreateInvitationAsync_WithoutRoleIdAndWorkspaceId_UsesDefaultRoleNameAndIgnoresWorkspaceRoleId()
    {
        SetupValidCompanyAndCaller(callerUserId: 1, companyId: 10);
        ClientAppOptions.FrontendBaseUrl = null!; // Triggers default "http://localhost:5174"

        CompanyInvitation? capturedInvite = null;
        InvitationRepositoryMock
            .Setup(r => r.AddInvitationAsync(It.IsAny<CompanyInvitation>(), It.IsAny<CancellationToken>()))
            .Callback<CompanyInvitation, CancellationToken>((inv, _) =>
            {
                inv.Id = 502;
                capturedInvite = inv;
            })
            .Returns(Task.CompletedTask);

        var request = new CreateInvitationRequestDto
        {
            Email = "member@acme.com",
            RoleId = null,
            RoleName = null,
            WorkspaceId = null,
            WorkspaceRoleId = 7 // Must be ignored when WorkspaceId is null
        };

        var result = await InvitationService.CreateInvitationAsync(10, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal($"http://localhost:5174/invite/{result.Data!.Token}", result.Data.InviteUrl);

        Assert.NotNull(capturedInvite);
        Assert.Null(capturedInvite.RoleId);
        Assert.Equal("Member", capturedInvite.RoleName);
        Assert.Null(capturedInvite.WorkspaceId);
        Assert.Null(capturedInvite.WorkspaceRoleId);
    }

    // UTC003: Create invitation without RoleId but with custom RoleName -> Uses custom RoleName
    [Fact]
    public async Task CreateInvitationAsync_WithoutRoleIdButWithCustomRoleName_UsesCustomRoleName()
    {
        SetupValidCompanyAndCaller(callerUserId: 1, companyId: 10);

        CompanyInvitation? capturedInvite = null;
        InvitationRepositoryMock
            .Setup(r => r.AddInvitationAsync(It.IsAny<CompanyInvitation>(), It.IsAny<CancellationToken>()))
            .Callback<CompanyInvitation, CancellationToken>((inv, _) => capturedInvite = inv)
            .Returns(Task.CompletedTask);

        var request = new CreateInvitationRequestDto
        {
            Email = "custom@acme.com",
            RoleId = null,
            RoleName = "External Auditor"
        };

        var result = await InvitationService.CreateInvitationAsync(10, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal("External Auditor", capturedInvite!.RoleName);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC004: User is not authenticated -> Returns Unauthorized
    [Fact]
    public async Task CreateInvitationAsync_WhenUserUnauthenticated_ReturnsUnauthorized()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns((long?)null);
        var request = new CreateInvitationRequestDto { Email = "user@acme.com" };

        var result = await InvitationService.CreateInvitationAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("Your session is invalid or has expired.", result.ErrorMessage);
    }

    // UTC005: Company does not exist or is inactive -> Returns NotFound
    [Theory]
    [InlineData(true)]  // Company is null
    [InlineData(false)] // Company is inactive (IsActive = false)
    public async Task CreateInvitationAsync_WhenCompanyMissingOrInactive_ReturnsNotFound(bool isNullCompany)
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1);
        CompanyRepositoryMock
            .Setup(c => c.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(isNullCompany ? null : new Company { Id = 10, Name = "Inactive Corp", IsActive = false });

        var request = new CreateInvitationRequestDto { Email = "user@acme.com" };

        var result = await InvitationService.CreateInvitationAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("Không tìm thấy công ty.", result.ErrorMessage);
    }

    // UTC006: User lacks UserInvite permission on company -> Returns Forbidden
    [Fact]
    public async Task CreateInvitationAsync_WhenTenantPermissionDenied_ReturnsForbidden()
    {
        SetupValidCompanyAndCaller(callerUserId: 1, companyId: 10);
        PermissionServiceMock
            .Setup(p => p.RequireTenantAsync(1, 10, PermissionKeys.UserInvite, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Forbidden("FORBIDDEN", "Bạn không có quyền mời thành viên."));

        var request = new CreateInvitationRequestDto { Email = "user@acme.com" };

        var result = await InvitationService.CreateInvitationAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    // UTC007: Email is whitespace or missing '@' -> Returns Validation Error
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-email-format")]
    public async Task CreateInvitationAsync_WhenEmailIsInvalid_ReturnsValidationError(string invalidEmail)
    {
        SetupValidCompanyAndCaller(callerUserId: 1, companyId: 10);
        var request = new CreateInvitationRequestDto { Email = invalidEmail };

        var result = await InvitationService.CreateInvitationAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("email", result.Errors!.Keys);
        Assert.Contains("Địa chỉ email không hợp lệ.", result.Errors["email"]);
    }

    // UTC008: Email is already a member of the company -> Returns Conflict (ALREADY_MEMBER)
    [Fact]
    public async Task CreateInvitationAsync_WhenEmailAlreadyCompanyMember_ReturnsConflict()
    {
        SetupValidCompanyAndCaller(callerUserId: 1, companyId: 10);
        CompanyRepositoryMock
            .Setup(c => c.IsEmailMemberAsync(10, "existing@acme.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new CreateInvitationRequestDto { Email = "existing@acme.com" };

        var result = await InvitationService.CreateInvitationAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("ALREADY_MEMBER", result.ErrorCode);
        Assert.Equal("Email này đã là thành viên công ty.", result.ErrorMessage);
    }

    // UTC009: Email belongs to another company -> Returns Conflict (USER_IN_ANOTHER_COMPANY)
    [Fact]
    public async Task CreateInvitationAsync_WhenEmailBelongsToAnotherCompany_ReturnsConflict()
    {
        SetupValidCompanyAndCaller(callerUserId: 1, companyId: 10);
        CompanyRepositoryMock
            .Setup(c => c.IsEmailInAnyOtherCompanyAsync(10, "external@acme.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await InvitationService.CreateInvitationAsync(
            10,
            new CreateInvitationRequestDto { Email = "external@acme.com" },
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("USER_IN_ANOTHER_COMPANY", result.ErrorCode);
        Assert.Equal("Email này đã thuộc một công ty khác.", result.ErrorMessage);
    }

    // UTC010: Department does not exist, is inactive, or belongs to another company -> Returns NotFound
    [Fact]
    public async Task CreateInvitationAsync_WhenDepartmentIsInvalid_ReturnsNotFound()
    {
        SetupValidCompanyAndCaller(callerUserId: 1, companyId: 10);
        Db.Departments.Add(new Department { Id = 20, CompanyId = 10, Name = "Inactive", IsActive = false });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await InvitationService.CreateInvitationAsync(
            10,
            new CreateInvitationRequestDto { Email = "user@acme.com", DepartmentId = 20 },
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("Không tìm thấy phòng ban.", result.ErrorMessage);
    }

    // UTC011: Tenant RoleId does not exist -> Returns NotFound
    [Fact]
    public async Task CreateInvitationAsync_WhenTenantRoleNotFound_ReturnsNotFound()
    {
        SetupValidCompanyAndCaller(callerUserId: 1, companyId: 10);
        CompanyRepositoryMock
            .Setup(c => c.GetRoleByIdAsync(10, 999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyRole?)null);

        var request = new CreateInvitationRequestDto { Email = "user@acme.com", RoleId = 999 };

        var result = await InvitationService.CreateInvitationAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("Không tìm thấy dữ liệu.", result.ErrorMessage);
    }

    // UTC012: Tenant RoleId belongs to another scope (e.g. WORKSPACE) -> Returns BadRequest (ROLE_SCOPE_MISMATCH)
    [Fact]
    public async Task CreateInvitationAsync_WhenTenantRoleScopeMismatches_ReturnsBadRequest()
    {
        SetupValidCompanyAndCaller(callerUserId: 1, companyId: 10);
        CompanyRepositoryMock
            .Setup(c => c.GetRoleByIdAsync(10, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyRole
            {
                Id = 5,
                CompanyId = 10,
                Code = CompanyRoleCodes.WorkspaceAdmin,
                Scope = RoleScopes.Workspace,
                IsActive = true
            });

        var request = new CreateInvitationRequestDto { Email = "user@acme.com", RoleId = 5 };

        var result = await InvitationService.CreateInvitationAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("ROLE_SCOPE_MISMATCH", result.ErrorCode);
        Assert.Equal($"Role này thuộc phạm vi {RoleScopes.Workspace}, không gán ở đây được.", result.ErrorMessage);
    }

    // UTC013: Tenant RoleId is inactive -> Returns BadRequest (ROLE_INACTIVE)
    [Fact]
    public async Task CreateInvitationAsync_WhenTenantRoleIsInactive_ReturnsBadRequest()
    {
        SetupValidCompanyAndCaller(callerUserId: 1, companyId: 10);
        CompanyRepositoryMock
            .Setup(c => c.GetRoleByIdAsync(10, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyRole
            {
                Id = 3,
                CompanyId = 10,
                Code = "INACTIVE_ROLE",
                Scope = RoleScopes.Tenant,
                IsActive = false
            });

        var request = new CreateInvitationRequestDto { Email = "user@acme.com", RoleId = 3 };

        var result = await InvitationService.CreateInvitationAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("ROLE_INACTIVE", result.ErrorCode);
        Assert.Equal("Role đang tắt, không gán được.", result.ErrorMessage);
    }

    // UTC014: Caller lacks WorkspaceMemberManage permission on specified WorkspaceId -> Returns Forbidden
    [Fact]
    public async Task CreateInvitationAsync_WhenWorkspacePermissionDenied_ReturnsForbidden()
    {
        SetupValidCompanyAndCaller(callerUserId: 1, companyId: 10, workspaceId: 100);
        PermissionServiceMock
            .Setup(p => p.RequireWorkspaceAsync(1, 100, PermissionKeys.WorkspaceMemberManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<WorkspaceAccessContext>.Forbidden("FORBIDDEN", "Bạn không có quyền quản lý thành viên workspace."));

        var request = new CreateInvitationRequestDto { Email = "user@acme.com", WorkspaceId = 100 };

        var result = await InvitationService.CreateInvitationAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    // UTC015: Specified WorkspaceId belongs to a different company -> Returns NotFound
    [Fact]
    public async Task CreateInvitationAsync_WhenWorkspaceBelongsToAnotherCompany_ReturnsNotFound()
    {
        SetupValidCompanyAndCaller(callerUserId: 1, companyId: 10, workspaceId: 100, workspaceCompanyId: 99);

        var request = new CreateInvitationRequestDto { Email = "user@acme.com", WorkspaceId = 100 };

        var result = await InvitationService.CreateInvitationAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("Không tìm thấy dữ liệu.", result.ErrorMessage);
    }

    // UTC016: WorkspaceRoleId does not exist -> Returns NotFound
    [Fact]
    public async Task CreateInvitationAsync_WhenWorkspaceRoleNotFound_ReturnsNotFound()
    {
        SetupValidCompanyAndCaller(callerUserId: 1, companyId: 10, workspaceId: 100);
        CompanyRepositoryMock
            .Setup(c => c.GetRoleByIdAsync(10, 888, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyRole?)null);

        var request = new CreateInvitationRequestDto
        {
            Email = "user@acme.com",
            WorkspaceId = 100,
            WorkspaceRoleId = 888
        };

        var result = await InvitationService.CreateInvitationAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("Không tìm thấy dữ liệu.", result.ErrorMessage);
    }

    // UTC017: WorkspaceRoleId belongs to another scope (e.g. PROJECT) -> Returns BadRequest (ROLE_SCOPE_MISMATCH)
    [Fact]
    public async Task CreateInvitationAsync_WhenWorkspaceRoleScopeMismatches_ReturnsBadRequest()
    {
        SetupValidCompanyAndCaller(callerUserId: 1, companyId: 10, workspaceId: 100);
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

        var request = new CreateInvitationRequestDto
        {
            Email = "user@acme.com",
            WorkspaceId = 100,
            WorkspaceRoleId = 12
        };

        var result = await InvitationService.CreateInvitationAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("ROLE_SCOPE_MISMATCH", result.ErrorCode);
        Assert.Equal($"Role này thuộc phạm vi {RoleScopes.Project}, không gán ở đây được.", result.ErrorMessage);
    }

    // UTC018: WorkspaceRoleId is inactive -> Returns BadRequest (ROLE_INACTIVE)
    [Fact]
    public async Task CreateInvitationAsync_WhenWorkspaceRoleIsInactive_ReturnsBadRequest()
    {
        SetupValidCompanyAndCaller(callerUserId: 1, companyId: 10, workspaceId: 100);
        CompanyRepositoryMock
            .Setup(c => c.GetRoleByIdAsync(10, 14, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyRole
            {
                Id = 14,
                CompanyId = 10,
                Code = "INACTIVE_WS_ROLE",
                Scope = RoleScopes.Workspace,
                IsActive = false
            });

        var request = new CreateInvitationRequestDto
        {
            Email = "user@acme.com",
            WorkspaceId = 100,
            WorkspaceRoleId = 14
        };

        var result = await InvitationService.CreateInvitationAsync(10, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("ROLE_INACTIVE", result.ErrorCode);
        Assert.Equal("Role đang tắt, không gán được.", result.ErrorMessage);
    }

    // =========================================================================
    // 3. BOUNDARY CASES (Email: 3, 255, 256 | RoleName: 1, 100, 101)
    // =========================================================================

    // UTC19 + UTC20: Email boundary length 3 ("a@b") and 255 (DB upper boundary) -> Returns Created
    [Theory]
    [InlineData(3)]   // UTC19: Minimal valid email format with '@' (3 chars: "a@b")
    [InlineData(255)] // UTC20: DB NVARCHAR(255) upper boundary
    public async Task CreateInvitationAsync_WhenEmailLengthIsWithinDbBoundary_ReturnsCreated(int length)
    {
        SetupValidCompanyAndCaller(callerUserId: 1, companyId: 10);
        var validEmail = length == 3
            ? "a@b"
            : new string('a', 246) + "@acme.com"; // 246 + 9 = 255 chars

        var request = new CreateInvitationRequestDto { Email = validEmail };

        var result = await InvitationService.CreateInvitationAsync(10, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
    }

    // UTC21: Email boundary length 256 (exceeds DB NVARCHAR(255)) -> Throws DbUpdateException
    [Fact]
    public async Task CreateInvitationAsync_WhenEmailExceeds255Chars_ThrowsDbUpdateException()
    {
        SetupValidCompanyAndCaller(callerUserId: 1, companyId: 10);
        InvitationRepositoryMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("String or binary data would be truncated in table 'MembershipRequests', column 'email'."));

        var overLengthEmail = new string('a', 247) + "@acme.com"; // 256 chars
        var request = new CreateInvitationRequestDto { Email = overLengthEmail };

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            InvitationService.CreateInvitationAsync(10, request, TestContext.Current.CancellationToken));
    }

    // UTC22 + UTC23: RoleName boundary length 1 and 100 (DB upper boundary) -> Returns Created
    [Theory]
    [InlineData(1)]   // UTC22: RoleName 1 character
    [InlineData(100)] // UTC23: RoleName 100 characters (DB NVARCHAR(100) upper boundary)
    public async Task CreateInvitationAsync_WhenRoleNameLengthIsWithinDbBoundary_ReturnsCreated(int length)
    {
        SetupValidCompanyAndCaller(callerUserId: 1, companyId: 10);
        var validRoleName = new string('R', length);
        var request = new CreateInvitationRequestDto
        {
            Email = "user@acme.com",
            RoleName = validRoleName
        };

        var result = await InvitationService.CreateInvitationAsync(10, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
    }

    // UTC24: RoleName boundary length 101 (exceeds DB NVARCHAR(100)) -> Throws DbUpdateException
    [Fact]
    public async Task CreateInvitationAsync_WhenRoleNameExceeds100Chars_ThrowsDbUpdateException()
    {
        SetupValidCompanyAndCaller(callerUserId: 1, companyId: 10);
        InvitationRepositoryMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("String or binary data would be truncated in table 'MembershipRequests', column 'role_name'."));

        var request = new CreateInvitationRequestDto
        {
            Email = "user@acme.com",
            RoleName = new string('R', 101)
        };

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            InvitationService.CreateInvitationAsync(10, request, TestContext.Current.CancellationToken));
    }
}