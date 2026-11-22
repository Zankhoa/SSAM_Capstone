using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SSAM_Server.Common;
using SSAM_Server.Common.Options;
using SSAM_Server.Data;
using SSAM_Server.DTOs.Invitations;
using SSAM_Server.Models;
using SSAM_Server.Repositories;
using SSAM_Server.Security;

namespace SSAM_Server.Services;

public class InvitationService : IInvitationService
{
    private readonly IInvitationRepository _invitationRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IAuthRepository _authRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissionService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IEmailService _emailService;
    private readonly ICurrentUser _currentUser;
    private readonly ClientAppOptions _clientAppOptions;
    private readonly TimeProvider _clock;

    public InvitationService(
        IInvitationRepository invitationRepository,
        ICompanyRepository companyRepository,
        IAuthRepository authRepository,
        IWorkspaceRepository workspaceRepository,
        AppDbContext db,
        IPermissionService permissionService,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IEmailService emailService,
        ICurrentUser currentUser,
        IOptions<ClientAppOptions> clientAppOptions,
        TimeProvider clock)
    {
        _invitationRepository = invitationRepository;
        _companyRepository = companyRepository;
        _authRepository = authRepository;
        _workspaceRepository = workspaceRepository;
        _db = db;
        _permissionService = permissionService;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _emailService = emailService;
        _currentUser = currentUser;
        _clientAppOptions = clientAppOptions.Value;
        _clock = clock;
    }

    public async Task<ServiceResult<CreateInvitationResponseDto>> CreateInvitationAsync(
        long companyId,
        CreateInvitationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var callerUserId = _currentUser.UserIdOrNull;
        if (callerUserId == null)
        {
            return ServiceResult<CreateInvitationResponseDto>.Unauthorized("Your session is invalid or has expired.");
        }

        var company = await _companyRepository.GetByIdAsync(companyId, cancellationToken);
        if (company == null || !company.IsActive)
        {
            return ServiceResult<CreateInvitationResponseDto>.NotFound("Không tìm thấy công ty.");
        }

        var tenantAuth = await _permissionService.RequireTenantAsync(callerUserId.Value, companyId, PermissionKeys.UserInvite, cancellationToken);
        if (!tenantAuth.IsSuccess)
        {
            return ServiceResult<CreateInvitationResponseDto>.FromError(tenantAuth);
        }

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
        {
            return ServiceResult<CreateInvitationResponseDto>.Validation(new Dictionary<string, string[]>
            {
                ["email"] = new[] { "Địa chỉ email không hợp lệ." }
            });
        }

        var email = request.Email.Trim();
        if (await _companyRepository.IsEmailMemberAsync(companyId, email, cancellationToken))
        {
            return ServiceResult<CreateInvitationResponseDto>.Conflict("ALREADY_MEMBER", "Email này đã là thành viên công ty.");
        }

        if (await _companyRepository.IsEmailInAnyOtherCompanyAsync(companyId, email, cancellationToken))
        {
            return ServiceResult<CreateInvitationResponseDto>.Conflict("USER_IN_ANOTHER_COMPANY", "Email này đã thuộc một công ty khác.");
        }

        CompanyRole? resolvedTenantRole = null;
        if (request.RoleId.HasValue)
        {
            resolvedTenantRole = await _companyRepository.GetRoleByIdAsync(companyId, request.RoleId.Value, cancellationToken);
            if (resolvedTenantRole == null)
                return ServiceResult<CreateInvitationResponseDto>.NotFound("Không tìm thấy dữ liệu.");
            if (resolvedTenantRole.Scope != RoleScopes.Tenant)
                return ServiceResult<CreateInvitationResponseDto>.BadRequest("ROLE_SCOPE_MISMATCH", $"Role này thuộc phạm vi {resolvedTenantRole.Scope}, không gán ở đây được.");
            if (!resolvedTenantRole.IsActive)
                return ServiceResult<CreateInvitationResponseDto>.BadRequest("ROLE_INACTIVE", "Role đang tắt, không gán được.");
        }

        if (request.WorkspaceId.HasValue)
        {
            var wsAuth = await _permissionService.RequireWorkspaceAsync(
                callerUserId.Value,
                request.WorkspaceId.Value,
                PermissionKeys.WorkspaceMemberManage,
                cancellationToken);
            if (!wsAuth.IsSuccess)
                return ServiceResult<CreateInvitationResponseDto>.FromError(wsAuth);

            if (wsAuth.Data!.Workspace.CompanyId != companyId)
                return ServiceResult<CreateInvitationResponseDto>.NotFound("Không tìm thấy dữ liệu.");

            if (request.WorkspaceRoleId.HasValue)
            {
                var wsRole = await _companyRepository.GetRoleByIdAsync(companyId, request.WorkspaceRoleId.Value, cancellationToken);
                if (wsRole == null)
                    return ServiceResult<CreateInvitationResponseDto>.NotFound("Không tìm thấy dữ liệu.");
                if (wsRole.Scope != RoleScopes.Workspace)
                    return ServiceResult<CreateInvitationResponseDto>.BadRequest("ROLE_SCOPE_MISMATCH", $"Role này thuộc phạm vi {wsRole.Scope}, không gán ở đây được.");
                if (!wsRole.IsActive)
                    return ServiceResult<CreateInvitationResponseDto>.BadRequest("ROLE_INACTIVE", "Role đang tắt, không gán được.");
            }
        }

        if (request.DepartmentId.HasValue)
        {
            var deptExists = await _db.Departments
                .AsNoTracking()
                .AnyAsync(d => d.Id == request.DepartmentId.Value && d.CompanyId == companyId && d.IsActive, cancellationToken);
            if (!deptExists)
                return ServiceResult<CreateInvitationResponseDto>.NotFound("Không tìm thấy phòng ban.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddDays(7);
        var token = AuthService.GenerateSecureToken();
        var invite = new CompanyInvitation
        {
            CompanyId = companyId,
            Email = email,
            RoleId = resolvedTenantRole?.Id,
            RoleName = resolvedTenantRole?.Name ?? request.RoleName ?? "Member",
            WorkspaceId = request.WorkspaceId,
            WorkspaceRoleId = request.WorkspaceId.HasValue ? request.WorkspaceRoleId : null,
            DepartmentId = request.DepartmentId,
            Status = "pending",
            Token = token,
            ExpiresAt = expiresAt,
            CreatedAt = now
        };

        await _invitationRepository.AddInvitationAsync(invite, cancellationToken);
        await _invitationRepository.SaveChangesAsync(cancellationToken);

        await _emailService.SendInvitationEmailAsync(invite.Email, company.Name, token, cancellationToken);

        var baseUrl = (_clientAppOptions.FrontendBaseUrl ?? "http://localhost:5174").TrimEnd('/');
        return ServiceResult<CreateInvitationResponseDto>.Created(new CreateInvitationResponseDto
        {
            InvitationId = invite.Id,
            Token = token,
            InviteUrl = $"{baseUrl}/invite/{token}",
            ExpiresAt = expiresAt
        });
    }

    public async Task<ServiceResult<InvitationDetailsResponseDto>> GetInvitationDetailsAsync(string token, CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var invite = await _invitationRepository.GetInvitationWithDetailsAsync(token, cancellationToken);

        if (invite == null)
            return ServiceResult<InvitationDetailsResponseDto>.Ok(new InvitationDetailsResponseDto { Valid = false, Reason = "NOT_FOUND" });
        if (invite.Status == "accepted")
            return ServiceResult<InvitationDetailsResponseDto>.Ok(new InvitationDetailsResponseDto { Valid = false, Reason = "ALREADY_USED" });
        if (invite.Status != "pending")
            return ServiceResult<InvitationDetailsResponseDto>.Ok(new InvitationDetailsResponseDto { Valid = false, Reason = "NOT_FOUND" });
        if (invite.ExpiresAt != null && invite.ExpiresAt <= now)
            return ServiceResult<InvitationDetailsResponseDto>.Ok(new InvitationDetailsResponseDto { Valid = false, Reason = "EXPIRED" });

        return ServiceResult<InvitationDetailsResponseDto>.Ok(new InvitationDetailsResponseDto
        {
            Valid = true,
            Email = invite.Email,
            CompanyName = invite.Company.Name,
            WorkspaceName = invite.Workspace?.Name,
            RoleName = invite.Role != null ? invite.Role.Name : invite.RoleName,
            ExpiresAt = invite.ExpiresAt
        });
    }

    public async Task<ServiceResult<AcceptInvitationResponseDto>> AcceptInvitationAsync(
        string token,
        AcceptInvitationRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUser.UserIdOrNull;
        var now = _clock.GetUtcNow().UtcDateTime;

        var invite = await _invitationRepository.GetByTokenAsync(token, cancellationToken);
        if (invite == null)
            return ServiceResult<AcceptInvitationResponseDto>.BadRequest("TOKEN_INVALID", "Mã lời mời không hợp lệ.");

        if (invite.Status == "accepted")
            return ServiceResult<AcceptInvitationResponseDto>.BadRequest("TOKEN_INVALID", "Lời mời đã được sử dụng.");

        if (invite.ExpiresAt != null && invite.ExpiresAt <= now)
            return ServiceResult<AcceptInvitationResponseDto>.BadRequest("TOKEN_EXPIRED", "Lời mời đã hết hạn.");

        return await _invitationRepository.ExecuteInTransactionAsync(async ct =>
        {
            var trackedInvite = await _invitationRepository.GetByTokenAsync(token, ct);
            if (trackedInvite == null)
                return ServiceResult<AcceptInvitationResponseDto>.BadRequest("TOKEN_INVALID", "Mã lời mời không hợp lệ.");

            long userId;
            string? accessToken = null;
            string? refreshToken = null;
            int? expiresIn = null;

            if (currentUserId == null)
            {
                var validationErrors = new Dictionary<string, string[]>();
                if (string.IsNullOrWhiteSpace(request?.FullName))
                    validationErrors["fullName"] = new[] { "Họ và tên là bắt buộc." };

                AuthService.ValidatePasswordRules(request?.Password ?? string.Empty, validationErrors);
                if (validationErrors.Count > 0)
                    return ServiceResult<AcceptInvitationResponseDto>.Validation(validationErrors);

                var existingAccount = await _authRepository.GetAccountByEmailAsync(trackedInvite.Email, ct);
                if (existingAccount != null)
                {
                    return ServiceResult<AcceptInvitationResponseDto>.Conflict("CONFLICT", "Email này đã có tài khoản, vui lòng đăng nhập trước khi chấp nhận lời mời.");
                }

                var newAccount = new Account
                {
                    Email = trackedInvite.Email,
                    Password = _passwordHasher.HashPassword(request!.Password!),
                    EmailVerified = true,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _authRepository.AddAccountAsync(newAccount, ct);
                await _authRepository.SaveChangesAsync(ct);

                var newUser = new User
                {
                    AccountId = newAccount.Id,
                    Email = trackedInvite.Email,
                    FullName = request.FullName!.Trim(),
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _authRepository.AddUserAsync(newUser, ct);
                await _authRepository.SaveChangesAsync(ct);

                userId = newUser.Id;

                var tokenPair = _jwtTokenGenerator.GenerateAccessToken(newAccount, newUser);
                accessToken = tokenPair.Token;
                expiresIn = tokenPair.ExpiresIn;
                refreshToken = _jwtTokenGenerator.GenerateRefreshToken();

                await _authRepository.AddRefreshTokenAsync(new RefreshToken
                {
                    AccountId = newAccount.Id,
                    TokenHash = _jwtTokenGenerator.HashRefreshToken(refreshToken),
                    IssuedAt = now,
                    ExpiresAt = now.AddDays(7)
                }, ct);
                await _authRepository.SaveChangesAsync(ct);
            }
            else
            {
                var loggedInUser = await _authRepository.GetUserByIdAsync(currentUserId.Value, ct);
                if (loggedInUser == null)
                {
                    return ServiceResult<AcceptInvitationResponseDto>.NotFound("Không tìm thấy người dùng.");
                }

                if (!string.Equals(loggedInUser.Email, trackedInvite.Email, StringComparison.OrdinalIgnoreCase))
                {
                    return ServiceResult<AcceptInvitationResponseDto>.Conflict("CONFLICT", "Email của tài khoản đang đăng nhập không khớp với email trong lời mời.");
                }

                if (await _companyRepository.HasAnyActiveCompanyAsync(loggedInUser.Id, trackedInvite.CompanyId, ct))
                {
                    return ServiceResult<AcceptInvitationResponseDto>.Conflict("USER_IN_ANOTHER_COMPANY", "Tài khoản này đã thuộc một công ty khác, không thể tham gia thêm công ty mới.");
                }

                userId = loggedInUser.Id;
            }

            long roleId;
            if (trackedInvite.RoleId.HasValue)
            {
                var role = await _companyRepository.GetRoleByIdAsync(trackedInvite.CompanyId, trackedInvite.RoleId.Value, ct);
                roleId = role != null && role.Scope == RoleScopes.Tenant && role.IsActive
                    ? role.Id
                    : await _companyRepository.ResolveRoleIdAsync(trackedInvite.CompanyId, trackedInvite.RoleName, RoleScopes.Tenant, CompanyRoleCodes.Member, ct);
            }
            else
            {
                roleId = await _companyRepository.ResolveRoleIdAsync(trackedInvite.CompanyId, trackedInvite.RoleName, RoleScopes.Tenant, CompanyRoleCodes.Member, ct);
            }

            if (!await _companyRepository.IsMemberAsync(trackedInvite.CompanyId, userId, ct))
            {
                await _companyRepository.AddMemberAsync(new CompanyMember
                {
                    CompanyId = trackedInvite.CompanyId,
                    UserId = userId,
                    RoleId = roleId,
                    DepartmentId = trackedInvite.DepartmentId,
                    IsActive = true,
                    JoinedAt = now
                }, ct);
                await _companyRepository.SaveChangesAsync(ct);
            }

            if (trackedInvite.WorkspaceId.HasValue)
            {
                long wsRoleId;
                if (trackedInvite.WorkspaceRoleId.HasValue)
                {
                    var wsRole = await _companyRepository.GetRoleByIdAsync(trackedInvite.CompanyId, trackedInvite.WorkspaceRoleId.Value, ct);
                    wsRoleId = wsRole != null && wsRole.Scope == RoleScopes.Workspace && wsRole.IsActive
                        ? wsRole.Id
                        : await _companyRepository.ResolveRoleIdAsync(trackedInvite.CompanyId, CompanyRoleCodes.WorkspaceMember, RoleScopes.Workspace, CompanyRoleCodes.WorkspaceMember, ct);
                }
                else
                {
                    wsRoleId = await _companyRepository.ResolveRoleIdAsync(trackedInvite.CompanyId, CompanyRoleCodes.WorkspaceMember, RoleScopes.Workspace, CompanyRoleCodes.WorkspaceMember, ct);
                }

                var existingWsMember = await _workspaceRepository.GetMemberRecordAsync(trackedInvite.WorkspaceId.Value, userId, ct);
                if (existingWsMember != null)
                {
                    if (!existingWsMember.IsActive)
                    {
                        existingWsMember.IsActive = true;
                        existingWsMember.RoleId = wsRoleId;
                        existingWsMember.JoinedAt = now;
                        await _workspaceRepository.SaveChangesAsync(ct);
                    }
                }
                else
                {
                    await _workspaceRepository.AddMemberAsync(new WorkspaceMember
                    {
                        WorkspaceId = trackedInvite.WorkspaceId.Value,
                        UserId = userId,
                        RoleId = wsRoleId,
                        IsActive = true,
                        JoinedAt = now
                    }, ct);
                    await _workspaceRepository.SaveChangesAsync(ct);
                }
            }

            trackedInvite.Status = "accepted";
            _invitationRepository.UpdateInvitation(trackedInvite);
            await _invitationRepository.SaveChangesAsync(ct);

            return ServiceResult<AcceptInvitationResponseDto>.Ok(new AcceptInvitationResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = expiresIn,
                CompanyId = trackedInvite.CompanyId,
                WorkspaceId = trackedInvite.WorkspaceId
            });
        }, cancellationToken);
    }
}
