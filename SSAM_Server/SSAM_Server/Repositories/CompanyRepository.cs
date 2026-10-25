using Microsoft.EntityFrameworkCore;
using SSAM_Server.Data;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Companies;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.Repositories;

public class CompanyRepository : ICompanyRepository
{
    private readonly AppDbContext _db;

    public CompanyRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Company?> GetByIdAsync(long companyId, CancellationToken ct = default)
        => await _db.Companies.FindAsync(new object[] { companyId }, ct);

    public Task<Company?> GetActiveByDomainAsync(string domain, CancellationToken ct = default)
        => _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.EmailDomain == domain && c.IsActive, ct);

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct = default)
        => _db.Companies.AsNoTracking().AnyAsync(c => c.Code == code, ct);

    public async Task AddCompanyAsync(Company company, CancellationToken ct = default)
        => await _db.Companies.AddAsync(company, ct);

    public async Task AddRolesAsync(IEnumerable<CompanyRole> roles, CancellationToken ct = default)
        => await _db.CompanyRoles.AddRangeAsync(roles, ct);

    public async Task SeedSystemRolesAndPermissionsAsync(long companyId, DateTime now, CancellationToken ct = default)
    {
        var specs = new (string Code, string Name, string Scope, string Description, string[] PermissionCodes)[]
        {
            (CompanyRoleCodes.TenantAdmin, "Tenant Admin", RoleScopes.Tenant,
                "Toàn quyền cấu hình tổ chức và bảo mật. Gán cho người tạo công ty.",
                PermissionKeys.AllTenantKeys),
            (CompanyRoleCodes.Member, "Member", RoleScopes.Tenant,
                "Thành viên cơ bản của công ty, mặc định khi được mời.",
                new[] { PermissionKeys.CompanyView, PermissionKeys.RoleView }),
            (CompanyRoleCodes.WorkspaceAdmin, "Workspace Admin", RoleScopes.Workspace,
                "Quản trị workspace và mọi dự án bên trong. Gán cho người tạo workspace.",
                PermissionKeys.AllWorkspaceKeys),
            (CompanyRoleCodes.ProjectInitiator, "Project Initiator", RoleScopes.Workspace,
                "Được khởi tạo dự án mới trong workspace (actor \"Project Initiator\" của SRS).",
                new[] { PermissionKeys.WorkspaceMemberView, PermissionKeys.ProjectCreate, PermissionKeys.ProjectInheritAccess }),
            (CompanyRoleCodes.WorkspaceMember, "Workspace Member", RoleScopes.Workspace,
                "Thành viên mặc định khi được thêm vào workspace.",
                new[] { PermissionKeys.WorkspaceMemberView, PermissionKeys.ProjectInheritAccess }),
            (CompanyRoleCodes.ProjectManager, "Project Manager", RoleScopes.Project,
                "Khởi tạo Sprint, cấu hình Phase SDLC, duyệt thành viên dự án. Gán cho người tạo dự án.",
                PermissionKeys.AllProjectKeys),
            (CompanyRoleCodes.TechLead, "Tech Lead", RoleScopes.Project,
                "Dẫn dắt kỹ thuật, duyệt code review.",
                new[] { PermissionKeys.ProjectView }),
            (CompanyRoleCodes.Ba, "Business Analyst", RoleScopes.Project,
                "Soạn thảo đặc tả SRS, quản lý khối yêu cầu và backlog.",
                new[] { PermissionKeys.ProjectView }),
            (CompanyRoleCodes.Developer, "Developer", RoleScopes.Project,
                "Lập trình, nhận issue, chuyển trạng thái theo luồng kỹ thuật.",
                new[] { PermissionKeys.ProjectView }),
            (CompanyRoleCodes.Qa, "QA / Tester", RoleScopes.Project,
                "Kiểm thử, xác nhận Definition of Done.",
                new[] { PermissionKeys.ProjectView })
        };

        var roles = specs.Select(s => new CompanyRole
        {
            CompanyId = companyId,
            Code = s.Code,
            Name = s.Name,
            Scope = s.Scope,
            Description = s.Description,
            IsSystem = true,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        }).ToList();

        await _db.CompanyRoles.AddRangeAsync(roles, ct);
        await _db.SaveChangesAsync(ct);

        var permissions = await _db.Permissions
            .AsNoTracking()
            .Where(p => p.IsActive)
            .ToDictionaryAsync(p => p.Code, p => p.Id, ct);

        var rolePermissions = new List<RolePermission>();
        for (int i = 0; i < specs.Length; i++)
        {
            var role = roles[i];
            foreach (var permCode in specs[i].PermissionCodes)
            {
                if (permissions.TryGetValue(permCode, out var permId))
                {
                    rolePermissions.Add(new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = permId
                    });
                }
            }
        }

        if (rolePermissions.Count > 0)
        {
            await _db.RolePermissions.AddRangeAsync(rolePermissions, ct);
            await _db.SaveChangesAsync(ct);
        }
    }

    public Task<CompanyRole?> GetRoleByIdAsync(long companyId, long roleId, CancellationToken ct = default)
        => _db.CompanyRoles.FirstOrDefaultAsync(r => r.Id == roleId && r.CompanyId == companyId, ct);

    public Task<CompanyRole?> GetRoleByCodeAsync(long companyId, string code, CancellationToken ct = default)
        => _db.CompanyRoles.FirstOrDefaultAsync(r => r.CompanyId == companyId && r.Code == code, ct);

    public async Task<long> ResolveRoleIdAsync(
        long companyId,
        string? roleName,
        string expectedScope = RoleScopes.Tenant,
        string fallbackCode = CompanyRoleCodes.Member,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(roleName))
        {
            var wanted = roleName.Trim().ToLower();
            var match = await _db.CompanyRoles.FirstOrDefaultAsync(
                r => r.CompanyId == companyId &&
                     r.Scope == expectedScope &&
                     r.IsActive &&
                     (r.Name.ToLower() == wanted || (r.Code != null && r.Code.ToLower() == wanted)), ct);
            if (match != null) return match.Id;
        }

        var fallback = await _db.CompanyRoles.FirstOrDefaultAsync(
            r => r.CompanyId == companyId && r.Code == fallbackCode, ct);

        return fallback?.Id ?? throw new InvalidOperationException(
            $"System role '{fallbackCode}' is missing for company {companyId}.");
    }

    public Task<CompanyMember?> GetMemberAsync(long companyId, long userId, CancellationToken ct = default)
        => _db.CompanyMembers
            .Include(cm => cm.Role)
            .FirstOrDefaultAsync(cm => cm.CompanyId == companyId && cm.UserId == userId, ct);

    public Task<bool> IsMemberAsync(long companyId, long userId, CancellationToken ct = default)
        => _db.CompanyMembers.AsNoTracking().AnyAsync(cm => cm.CompanyId == companyId && cm.UserId == userId && cm.IsActive, ct);

    public Task<bool> IsEmailMemberAsync(long companyId, string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLower();
        return _db.CompanyMembers
            .AsNoTracking()
            .AnyAsync(cm => cm.CompanyId == companyId && cm.IsActive && cm.User.Email.ToLower() == normalized, ct);
    }

    public Task<bool> IsAdminAsync(long companyId, long userId, CancellationToken ct = default)
        => _db.CompanyMembers.AsNoTracking().AnyAsync(
            cm => cm.CompanyId == companyId && cm.UserId == userId && cm.IsActive && cm.Role.Code == CompanyRoleCodes.TenantAdmin, ct);

    public Task<int> CountActiveTenantAdminsAsync(long companyId, CancellationToken ct = default)
        => _db.CompanyMembers.AsNoTracking().CountAsync(
            cm => cm.CompanyId == companyId && cm.IsActive && cm.Role.Code == CompanyRoleCodes.TenantAdmin, ct);

    public async Task AddMemberAsync(CompanyMember member, CancellationToken ct = default)
        => await _db.CompanyMembers.AddAsync(member, ct);

    public async Task<PageDto<CompanyUserDto>> ListCompanyUsersAsync(
        long companyId,
        string? search,
        long? roleId,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.CompanyMembers
            .AsNoTracking()
            .Where(cm => cm.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var upper = status.Trim().ToUpperInvariant();
            if (upper == "ACTIVE")
                query = query.Where(cm => cm.IsActive);
            else if (upper == "DEACTIVATED")
                query = query.Where(cm => !cm.IsActive);
        }

        if (roleId.HasValue)
        {
            query = query.Where(cm => cm.RoleId == roleId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(cm => cm.User.FullName.ToLower().Contains(term) || cm.User.Email.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderBy(cm => cm.JoinedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(cm => new CompanyUserDto
            {
                UserId = cm.UserId,
                FullName = cm.User.FullName,
                Email = cm.User.Email,
                AvatarUrl = cm.User.AvatarUrl,
                Role = new RoleRefDto
                {
                    RoleId = cm.Role.Id,
                    Code = cm.Role.Code,
                    Name = cm.Role.Name,
                    Scope = cm.Role.Scope
                },
                Status = cm.IsActive ? "ACTIVE" : "DEACTIVATED",
                WorkspaceCount = _db.WorkspaceMembers.Count(wm =>
                    wm.UserId == cm.UserId &&
                    wm.IsActive &&
                    wm.Workspace.CompanyId == companyId),
                JoinedAt = cm.JoinedAt
            })
            .ToListAsync(ct);

        return new PageDto<CompanyUserDto>
        {
            Items = rows,
            Page = page,
            PageSize = pageSize,
            Total = total
        };
    }

    public Task<CompanyUserDto?> GetCompanyUserDtoAsync(long companyId, long userId, CancellationToken ct = default)
        => _db.CompanyMembers
            .AsNoTracking()
            .Where(cm => cm.CompanyId == companyId && cm.UserId == userId)
            .Select(cm => new CompanyUserDto
            {
                UserId = cm.UserId,
                FullName = cm.User.FullName,
                Email = cm.User.Email,
                AvatarUrl = cm.User.AvatarUrl,
                Role = new RoleRefDto
                {
                    RoleId = cm.Role.Id,
                    Code = cm.Role.Code,
                    Name = cm.Role.Name,
                    Scope = cm.Role.Scope
                },
                Status = cm.IsActive ? "ACTIVE" : "DEACTIVATED",
                WorkspaceCount = _db.WorkspaceMembers.Count(wm =>
                    wm.UserId == cm.UserId &&
                    wm.IsActive &&
                    wm.Workspace.CompanyId == companyId),
                JoinedAt = cm.JoinedAt
            })
            .FirstOrDefaultAsync(ct);

    public async Task AddMembershipRequestAsync(MembershipRequest request, CancellationToken ct = default)
        => await _db.MembershipRequests.AddAsync(request, ct);

    public Task<MembershipRequest?> GetJoinRequestAsync(long companyId, long requestId, CancellationToken ct = default)
        => _db.MembershipRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == requestId && r.CompanyId == companyId, ct);

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work, CancellationToken ct = default)
        => _db.ExecuteInTransactionAsync(work, ct);
}
