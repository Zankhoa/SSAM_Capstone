using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Invitations;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.InvitationServiceTests;

public class AcceptInvitationAsyncTests : InvitationServiceTestBase
{
    private CompanyInvitation SetupValidPendingInvite(
        string token = "valid-invite-token",
        long companyId = 10,
        string email = "invitee@acme.com",
        long? roleId = 2,
        string roleName = "Member",
        long? workspaceId = 100,
        long? workspaceRoleId = 7,
        DateTime? expiresAt = null)
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var invite = new CompanyInvitation
        {
            Id = 500,
            CompanyId = companyId,
            Email = email,
            RoleId = roleId,
            RoleName = roleName,
            WorkspaceId = workspaceId,
            WorkspaceRoleId = workspaceRoleId,
            Status = "pending",
            Token = token,
            ExpiresAt = expiresAt ?? now.AddDays(3),
            CreatedAt = now.AddDays(-1)
        };

        InvitationRepositoryMock
            .Setup(r => r.GetByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invite);

        InvitationRepositoryMock
            .Setup(r => r.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<ServiceResult<AcceptInvitationResponseDto>>>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<ServiceResult<AcceptInvitationResponseDto>>>, CancellationToken>(
                (action, ct) => action(ct));

        CompanyRepositoryMock
            .Setup(c => c.ResolveRoleIdAsync(companyId, It.IsAny<string?>(), RoleScopes.Tenant, CompanyRoleCodes.Member, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        CompanyRepositoryMock
            .Setup(c => c.ResolveRoleIdAsync(companyId, CompanyRoleCodes.WorkspaceMember, RoleScopes.Workspace, CompanyRoleCodes.WorkspaceMember, It.IsAny<CancellationToken>()))
            .ReturnsAsync(6);

        CompanyRepositoryMock
            .Setup(c => c.IsMemberAsync(companyId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        WorkspaceRepositoryMock
            .Setup(w => w.GetMemberRecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkspaceMember?)null);

        return invite;
    }

    private void SetupNewUserRegistrationMocks(long generatedAccountId = 30, long generatedUserId = 40)
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns((long?)null);

        AuthRepositoryMock
            .Setup(a => a.GetAccountByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        PasswordHasherMock
            .Setup(h => h.HashPassword(It.IsAny<string>()))
            .Returns("hashed_password_123");

        AuthRepositoryMock
            .Setup(a => a.AddAccountAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .Callback<Account, CancellationToken>((acc, _) => acc.Id = generatedAccountId)
            .Returns(Task.CompletedTask);

        AuthRepositoryMock
            .Setup(a => a.AddUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((usr, _) => usr.Id = generatedUserId)
            .Returns(Task.CompletedTask);

        JwtTokenGeneratorMock
            .Setup(j => j.GenerateAccessToken(It.IsAny<Account>(), It.IsAny<User>(), null))
            .Returns(("access_jwt_token", 3600));

        JwtTokenGeneratorMock
            .Setup(j => j.GenerateRefreshToken())
            .Returns("raw_refresh_token");

        JwtTokenGeneratorMock
            .Setup(j => j.HashRefreshToken("raw_refresh_token"))
            .Returns("hashed_refresh_token");
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: Unauthenticated new user accepts invitation with valid Tenant & Workspace roles -> Registers Account/User, issues tokens, adds memberships & marks accepted
    [Fact]
    public async Task AcceptInvitationAsync_WhenUnauthenticatedNewUser_RegistersUserIssuesTokensAndAcceptsInvite()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var invite = SetupValidPendingInvite(
            token: "new-user-token", companyId: 10, email: "new.dev@acme.com", roleId: 2, workspaceId: 100, workspaceRoleId: 7);
        SetupNewUserRegistrationMocks(generatedAccountId: 30, generatedUserId: 40);

        CompanyRepositoryMock
            .Setup(c => c.GetRoleByIdAsync(10, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyRole { Id = 2, CompanyId = 10, Scope = RoleScopes.Tenant, IsActive = true });
        CompanyRepositoryMock
            .Setup(c => c.GetRoleByIdAsync(10, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyRole { Id = 7, CompanyId = 10, Scope = RoleScopes.Workspace, IsActive = true });

        User? savedUser = null;
        AuthRepositoryMock
            .Setup(a => a.AddUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((usr, _) =>
            {
                usr.Id = 40;
                savedUser = usr;
            })
            .Returns(Task.CompletedTask);

        CompanyMember? savedCompanyMember = null;
        CompanyRepositoryMock
            .Setup(c => c.AddMemberAsync(It.IsAny<CompanyMember>(), It.IsAny<CancellationToken>()))
            .Callback<CompanyMember, CancellationToken>((m, _) => savedCompanyMember = m)
            .Returns(Task.CompletedTask);

        WorkspaceMember? savedWsMember = null;
        WorkspaceRepositoryMock
            .Setup(w => w.AddMemberAsync(It.IsAny<WorkspaceMember>(), It.IsAny<CancellationToken>()))
            .Callback<WorkspaceMember, CancellationToken>((m, _) => savedWsMember = m)
            .Returns(Task.CompletedTask);

        var request = new AcceptInvitationRequestDto
        {
            FullName = "  Nguyễn Văn A  ",
            Password = "Password@123"
        };

        var result = await InvitationService.AcceptInvitationAsync("new-user-token", request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("access_jwt_token", result.Data.AccessToken);
        Assert.Equal("raw_refresh_token", result.Data.RefreshToken);
        Assert.Equal(3600, result.Data.ExpiresIn);
        Assert.Equal(10, result.Data.CompanyId);
        Assert.Equal(100, result.Data.WorkspaceId);

        Assert.NotNull(savedUser);
        Assert.Equal("Nguyễn Văn A", savedUser.FullName);
        Assert.Equal("new.dev@acme.com", savedUser.Email);

        Assert.NotNull(savedCompanyMember);
        Assert.Equal(40, savedCompanyMember.UserId);
        Assert.Equal(2, savedCompanyMember.RoleId);

        Assert.NotNull(savedWsMember);
        Assert.Equal(40, savedWsMember.UserId);
        Assert.Equal(7, savedWsMember.RoleId);

        Assert.Equal("accepted", invite.Status);
        InvitationRepositoryMock.Verify(r => r.UpdateInvitation(invite), Times.Once);
        InvitationRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC002: Authenticated user accepts invitation with case-insensitive matching email & reactivates previous inactive WorkspaceMember
    [Fact]
    public async Task AcceptInvitationAsync_WhenAuthenticatedUserWithInactiveWorkspaceMember_ReactivatesAndAcceptsInvite()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var invite = SetupValidPendingInvite(
            token: "auth-user-token", companyId: 10, email: "dev@acme.com", roleId: null, roleName: "Developer", workspaceId: 100, workspaceRoleId: null);

        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(40);
        AuthRepositoryMock
            .Setup(a => a.GetUserByIdAsync(40, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 40, Email = "DEV@ACME.COM", FullName = "Existing Dev" });

        var inactiveWsMember = new WorkspaceMember
        {
            WorkspaceId = 100,
            UserId = 40,
            RoleId = 99,
            IsActive = false,
            JoinedAt = now.AddDays(-20)
        };
        WorkspaceRepositoryMock
            .Setup(w => w.GetMemberRecordAsync(100, 40, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveWsMember);

        var result = await InvitationService.AcceptInvitationAsync("auth-user-token", null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Null(result.Data!.AccessToken);
        Assert.Null(result.Data.RefreshToken);
        Assert.Null(result.Data.ExpiresIn);
        Assert.Equal(10, result.Data.CompanyId);
        Assert.Equal(100, result.Data.WorkspaceId);

        Assert.True(inactiveWsMember.IsActive);
        Assert.Equal(6, inactiveWsMember.RoleId); // Default WORKSPACE_MEMBER resolved ID
        Assert.Equal(now, inactiveWsMember.JoinedAt);
        Assert.Equal("accepted", invite.Status);

        CompanyRepositoryMock.Verify(c => c.AddMemberAsync(It.IsAny<CompanyMember>(), It.IsAny<CancellationToken>()), Times.Once);
        WorkspaceRepositoryMock.Verify(w => w.AddMemberAsync(It.IsAny<WorkspaceMember>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC003: Invite references inactive/wrong-scope roles & user is already active in company and workspace -> Falls back to ResolveRoleIdAsync & skips duplicate additions
    [Fact]
    public async Task AcceptInvitationAsync_WhenRolesInactiveOrWrongScopeAndMembershipsExist_ResolvesFallbackRolesAndSkipsDuplicateAdd()
    {
        var invite = SetupValidPendingInvite(
            token: "fallback-token", companyId: 10, email: "dev@acme.com", roleId: 5, workspaceId: 100, workspaceRoleId: 12);

        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(40);
        AuthRepositoryMock
            .Setup(a => a.GetUserByIdAsync(40, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 40, Email = "dev@acme.com" });

        // RoleId 5 is WORKSPACE scope (mismatch for Tenant)
        CompanyRepositoryMock
            .Setup(c => c.GetRoleByIdAsync(10, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyRole { Id = 5, CompanyId = 10, Scope = RoleScopes.Workspace, IsActive = true });

        // WorkspaceRoleId 12 is inactive
        CompanyRepositoryMock
            .Setup(c => c.GetRoleByIdAsync(10, 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyRole { Id = 12, CompanyId = 10, Scope = RoleScopes.Workspace, IsActive = false });

        CompanyRepositoryMock
            .Setup(c => c.IsMemberAsync(10, 40, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        WorkspaceRepositoryMock
            .Setup(w => w.GetMemberRecordAsync(100, 40, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceMember { WorkspaceId = 100, UserId = 40, IsActive = true });

        var result = await InvitationService.AcceptInvitationAsync("fallback-token", null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal("accepted", invite.Status);

        CompanyRepositoryMock.Verify(c => c.AddMemberAsync(It.IsAny<CompanyMember>(), It.IsAny<CancellationToken>()), Times.Never);
        WorkspaceRepositoryMock.Verify(w => w.AddMemberAsync(It.IsAny<WorkspaceMember>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC004: Invitation token does not exist -> Returns BadRequest (TOKEN_INVALID)
    [Fact]
    public async Task AcceptInvitationAsync_WhenTokenNotFound_ReturnsBadRequest()
    {
        InvitationRepositoryMock
            .Setup(r => r.GetByTokenAsync("unknown-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyInvitation?)null);

        var result = await InvitationService.AcceptInvitationAsync("unknown-token", null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("TOKEN_INVALID", result.ErrorCode);
        Assert.Equal("Mã lời mời không hợp lệ.", result.ErrorMessage);
    }

    // UTC005: Invitation has already been accepted -> Returns BadRequest (TOKEN_INVALID)
    [Fact]
    public async Task AcceptInvitationAsync_WhenInvitationAlreadyAccepted_ReturnsBadRequest()
    {
        var invite = SetupValidPendingInvite(token: "used-token");
        invite.Status = "accepted";

        var result = await InvitationService.AcceptInvitationAsync("used-token", null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("TOKEN_INVALID", result.ErrorCode);
        Assert.Equal("Lời mời đã được sử dụng.", result.ErrorMessage);
    }

    // UTC006: Invitation is expired (ExpiresAt == now or ExpiresAt < now) -> Returns BadRequest (TOKEN_EXPIRED)
    [Theory]
    [InlineData(0)]   // UTC006: Exact expiration boundary (ExpiresAt == now)
    [InlineData(-1)]  // UTC007: Expired 1 second ago
    public async Task AcceptInvitationAsync_WhenInvitationExpired_ReturnsBadRequest(int offsetSeconds)
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupValidPendingInvite(token: "expired-token", expiresAt: now.AddSeconds(offsetSeconds));

        var result = await InvitationService.AcceptInvitationAsync("expired-token", null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("TOKEN_EXPIRED", result.ErrorCode);
        Assert.Equal("Lời mời đã hết hạn.", result.ErrorMessage);
    }

    // UTC008: Invitation is deleted concurrently before transaction reads it -> Returns BadRequest (TOKEN_INVALID)
    [Fact]
    public async Task AcceptInvitationAsync_WhenInviteDeletedConcurrentlyInTransaction_ReturnsBadRequest()
    {
        var invite = SetupValidPendingInvite(token: "race-token");
        InvitationRepositoryMock
            .SetupSequence(r => r.GetByTokenAsync("race-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invite)
            .ReturnsAsync((CompanyInvitation?)null);

        var result = await InvitationService.AcceptInvitationAsync("race-token", null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("TOKEN_INVALID", result.ErrorCode);
        Assert.Equal("Mã lời mời không hợp lệ.", result.ErrorMessage);
    }

    // UTC009: Unauthenticated user with null request or blank FullName & empty Password -> Returns Validation Error
    [Fact]
    public async Task AcceptInvitationAsync_WhenUnauthenticatedAndRequestIsNull_ReturnsValidationError()
    {
        SetupValidPendingInvite(token: "valid-token");
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns((long?)null);

        var result = await InvitationService.AcceptInvitationAsync("valid-token", null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("fullName", result.Errors!.Keys);
        Assert.Contains("Họ và tên là bắt buộc.", result.Errors["fullName"]);
        Assert.Contains("password", result.Errors.Keys);
        Assert.Contains("Password must contain at least 8 characters.", result.Errors["password"]);
    }

    // UTC010: Unauthenticated user with password violating complexity rules -> Returns Validation Error
    [Theory]
    [InlineData("Password!", "Password must contain at least one digit.")] // UTC010: Missing digit
    [InlineData("password1!", "Password must contain both uppercase and lowercase letters.")] // UTC011: Missing uppercase
    [InlineData("Password12", "Password must contain at least one special character.")] // UTC012: Missing special character
    public async Task AcceptInvitationAsync_WhenPasswordViolatesComplexityRules_ReturnsValidationError(
        string weakPassword,
        string expectedError)
    {
        SetupValidPendingInvite(token: "valid-token");
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns((long?)null);

        var request = new AcceptInvitationRequestDto
        {
            FullName = "Valid Name",
            Password = weakPassword
        };

        var result = await InvitationService.AcceptInvitationAsync("valid-token", request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("password", result.Errors!.Keys);
        Assert.Contains(expectedError, result.Errors["password"]);
    }

    // UTC013: Unauthenticated user when invite email already has an existing account -> Returns Conflict
    [Fact]
    public async Task AcceptInvitationAsync_WhenUnauthenticatedAndAccountAlreadyExists_ReturnsConflict()
    {
        SetupValidPendingInvite(token: "valid-token", email: "existing@acme.com");
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns((long?)null);
        AuthRepositoryMock
            .Setup(a => a.GetAccountByEmailAsync("existing@acme.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Account { Id = 15, Email = "existing@acme.com" });

        var request = new AcceptInvitationRequestDto
        {
            FullName = "Existing User",
            Password = "Password@123"
        };

        var result = await InvitationService.AcceptInvitationAsync("valid-token", request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("CONFLICT", result.ErrorCode);
        Assert.Equal("Email này đã có tài khoản, vui lòng đăng nhập trước khi chấp nhận lời mời.", result.ErrorMessage);
    }

    // UTC014: Authenticated user is not found in database -> Returns NotFound
    [Fact]
    public async Task AcceptInvitationAsync_WhenAuthenticatedUserNotFound_ReturnsNotFound()
    {
        SetupValidPendingInvite(token: "valid-token");
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(999);
        AuthRepositoryMock
            .Setup(a => a.GetUserByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await InvitationService.AcceptInvitationAsync("valid-token", null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("Không tìm thấy người dùng.", result.ErrorMessage);
    }

    // UTC015: Authenticated user email does not match invitation email -> Returns Conflict
    [Fact]
    public async Task AcceptInvitationAsync_WhenAuthenticatedUserEmailMismatches_ReturnsConflict()
    {
        SetupValidPendingInvite(token: "valid-token", email: "target@acme.com");
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(40);
        AuthRepositoryMock
            .Setup(a => a.GetUserByIdAsync(40, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 40, Email = "other@acme.com" });

        var result = await InvitationService.AcceptInvitationAsync("valid-token", null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("CONFLICT", result.ErrorCode);
        Assert.Equal("Email của tài khoản đang đăng nhập không khớp với email trong lời mời.", result.ErrorMessage);
    }

    // =========================================================================
    // 3. BOUNDARY CASES (ExpiresAt: +1s | Password: 7, 8 | FullName: 1, 255, 256)
    // =========================================================================

    // UTC016: ExpiresAt boundary (now + 1 second, just above <= now) -> Succeeds and returns Ok
    [Fact]
    public async Task AcceptInvitationAsync_WhenExpiresAtIsOneSecondInFuture_ReturnsOk()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupValidPendingInvite(token: "boundary-expiry-token", email: "dev@acme.com", workspaceId: null, expiresAt: now.AddSeconds(1));
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(40);
        AuthRepositoryMock
            .Setup(a => a.GetUserByIdAsync(40, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 40, Email = "dev@acme.com" });

        var result = await InvitationService.AcceptInvitationAsync("boundary-expiry-token", null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
    }

    // UTC017: Password boundary length 7 (below minimum 8) -> Returns Validation Error
    [Fact]
    public async Task AcceptInvitationAsync_WhenPasswordLengthIs7_ReturnsValidationError()
    {
        SetupValidPendingInvite(token: "valid-token");
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns((long?)null);

        var request = new AcceptInvitationRequestDto
        {
            FullName = "Valid Name",
            Password = "Abcde1!" // 7 characters
        };

        var result = await InvitationService.AcceptInvitationAsync("valid-token", request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("password", result.Errors!.Keys);
        Assert.Contains("Password must contain at least 8 characters.", result.Errors["password"]);
    }

    // UTC018: Password boundary length 8 (exact minimum length) -> Returns Ok
    [Fact]
    public async Task AcceptInvitationAsync_WhenPasswordLengthIs8_ReturnsOk()
    {
        SetupValidPendingInvite(token: "valid-token", workspaceId: null);
        SetupNewUserRegistrationMocks();

        var request = new AcceptInvitationRequestDto
        {
            FullName = "Valid Name",
            Password = "Abcdef1!" // 8 characters with upper, lower, digit, special char
        };

        var result = await InvitationService.AcceptInvitationAsync("valid-token", request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
    }

    // UTC019 + UTC020: FullName boundary length 1 and 255 (DB NVARCHAR(255) upper boundary) -> Returns Ok
    [Theory]
    [InlineData(1)]    // UTC019: FullName 1 character (lower non-whitespace boundary)
    [InlineData(255)]  // UTC020: FullName 255 characters (DB upper boundary)
    public async Task AcceptInvitationAsync_WhenFullNameLengthIsWithinDbBoundary_ReturnsOk(int length)
    {
        SetupValidPendingInvite(token: "valid-token", workspaceId: null);
        SetupNewUserRegistrationMocks();

        User? savedUser = null;
        AuthRepositoryMock
            .Setup(a => a.AddUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((usr, _) =>
            {
                usr.Id = 40;
                savedUser = usr;
            })
            .Returns(Task.CompletedTask);

        var validFullName = new string('N', length);
        var request = new AcceptInvitationRequestDto
        {
            FullName = validFullName,
            Password = "Password@123"
        };

        var result = await InvitationService.AcceptInvitationAsync("valid-token", request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(validFullName, savedUser!.FullName);
        Assert.Equal(length, savedUser.FullName.Length);
    }

    // UTC021: FullName boundary length 256 (exceeds DB NVARCHAR(255)) -> Throws DbUpdateException
    [Fact]
    public async Task AcceptInvitationAsync_WhenFullNameExceeds255Chars_ThrowsDbUpdateException()
    {
        SetupValidPendingInvite(token: "valid-token", workspaceId: null);
        SetupNewUserRegistrationMocks();

        AuthRepositoryMock
            .SetupSequence(a => a.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1) // Save Account succeeds (1 row affected)
            .ThrowsAsync(new DbUpdateException("String or binary data would be truncated in table 'Users', column 'full_name'."));

        var request = new AcceptInvitationRequestDto
        {
            FullName = new string('N', 256),
            Password = "Password@123"
        };

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            InvitationService.AcceptInvitationAsync("valid-token", request, TestContext.Current.CancellationToken));
    }
}