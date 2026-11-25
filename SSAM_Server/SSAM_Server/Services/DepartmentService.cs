using Microsoft.EntityFrameworkCore;
using SSAM_Server.Common;
using SSAM_Server.Data;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Companies;
using SSAM_Server.Interfaces.Repositories;
using SSAM_Server.Interfaces.Services;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.Services;

public class DepartmentService : IDepartmentService
{
    private readonly AppDbContext _db;
    private readonly ICompanyRepository _companyRepository;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public DepartmentService(
        AppDbContext db,
        ICompanyRepository companyRepository,
        ICurrentUser currentUser,
        TimeProvider clock)
    {
        _db = db;
        _companyRepository = companyRepository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ServiceResult<ItemsDto<DepartmentDto>>> ListAsync(long companyId, CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<ItemsDto<DepartmentDto>>.Unauthorized("Your session is invalid or has expired.");

        if (!await _companyRepository.IsMemberAsync(companyId, userId.Value, ct))
            return ServiceResult<ItemsDto<DepartmentDto>>.Forbidden("FORBIDDEN", "Bạn không thuộc công ty này.");

        var departments = await _db.Departments
            .AsNoTracking()
            .Where(d => d.CompanyId == companyId)
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentDto
            {
                DepartmentId = d.Id,
                CompanyId = d.CompanyId,
                Name = d.Name,
                Code = d.Code,
                Description = d.Description,
                IsActive = d.IsActive,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt,
                MemberCount = d.Members.Count(m => m.IsActive),
                Members = d.Members
                    .Where(m => m.IsActive)
                    .OrderBy(m => m.User.FullName)
                    .Select(m => new UserRefDto
                    {
                        UserId = m.UserId,
                        FullName = m.User.FullName,
                        Email = m.User.Email,
                        AvatarUrl = m.User.AvatarUrl
                    })
                    .ToList()
            })
            .ToListAsync(ct);

        return ServiceResult<ItemsDto<DepartmentDto>>.Ok(new ItemsDto<DepartmentDto>(departments));
    }

    public async Task<ServiceResult<DepartmentDto>> CreateAsync(long companyId, CreateDepartmentRequestDto request, CancellationToken ct = default)
    {
        var guard = await RequireAdminAsync<DepartmentDto>(companyId, ct);
        if (guard != null) return guard;

        var name = request.Name?.Trim() ?? string.Empty;
        var code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim().ToUpperInvariant();
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(name))
            errors["name"] = new[] { "Vui lòng nhập tên phòng ban." };
        else if (name.Length > 150)
            errors["name"] = new[] { "Tên phòng ban tối đa 150 ký tự." };

        if (code != null && code.Length > 50)
            errors["code"] = new[] { "Mã phòng ban tối đa 50 ký tự." };

        if (description != null && description.Length > 500)
            errors["description"] = new[] { "Mô tả phòng ban tối đa 500 ký tự." };

        if (errors.Count > 0)
            return ServiceResult<DepartmentDto>.Validation(errors);

        if (await _db.Departments.AsNoTracking().AnyAsync(d => d.CompanyId == companyId && d.Name == name, ct))
            return ServiceResult<DepartmentDto>.Conflict("DEPARTMENT_NAME_EXISTS", "Tên phòng ban đã tồn tại trong công ty.");

        if (code != null && await _db.Departments.AsNoTracking().AnyAsync(d => d.CompanyId == companyId && d.Code == code, ct))
            return ServiceResult<DepartmentDto>.Conflict("DEPARTMENT_CODE_EXISTS", "Mã phòng ban đã tồn tại trong công ty.");

        var now = _clock.GetUtcNow().UtcDateTime;
        var department = new Department
        {
            CompanyId = companyId,
            Name = name,
            Code = code,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Departments.Add(department);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<DepartmentDto>.Created(await GetDtoAsync(department.Id, ct));
    }

    public async Task<ServiceResult<DepartmentDto>> UpdateAsync(long companyId, long departmentId, UpdateDepartmentRequestDto request, CancellationToken ct = default)
    {
        var guard = await RequireAdminAsync<DepartmentDto>(companyId, ct);
        if (guard != null) return guard;

        var department = await _db.Departments.FirstOrDefaultAsync(d => d.Id == departmentId && d.CompanyId == companyId, ct);
        if (department == null)
            return ServiceResult<DepartmentDto>.NotFound("Không tìm thấy phòng ban.");

        if (request.Name != null)
        {
            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 150)
                return ServiceResult<DepartmentDto>.Validation(new Dictionary<string, string[]> { ["name"] = new[] { "Tên phòng ban từ 1 đến 150 ký tự." } });

            if (await _db.Departments.AsNoTracking().AnyAsync(d => d.CompanyId == companyId && d.Id != departmentId && d.Name == name, ct))
                return ServiceResult<DepartmentDto>.Conflict("DEPARTMENT_NAME_EXISTS", "Tên phòng ban đã tồn tại trong công ty.");

            department.Name = name;
        }

        if (request.Code != null)
        {
            var code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim().ToUpperInvariant();
            if (code != null && code.Length > 50)
                return ServiceResult<DepartmentDto>.Validation(new Dictionary<string, string[]> { ["code"] = new[] { "Mã phòng ban tối đa 50 ký tự." } });

            if (code != null && await _db.Departments.AsNoTracking().AnyAsync(d => d.CompanyId == companyId && d.Id != departmentId && d.Code == code, ct))
                return ServiceResult<DepartmentDto>.Conflict("DEPARTMENT_CODE_EXISTS", "Mã phòng ban đã tồn tại trong công ty.");

            department.Code = code;
        }

        if (request.Description != null)
        {
            var desc = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            if (desc != null && desc.Length > 500)
                return ServiceResult<DepartmentDto>.Validation(new Dictionary<string, string[]> { ["description"] = new[] { "Mô tả phòng ban tối đa 500 ký tự." } });
            department.Description = desc;
        }

        if (request.IsActive.HasValue)
            department.IsActive = request.IsActive.Value;

        department.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);

        return ServiceResult<DepartmentDto>.Ok(await GetDtoAsync(department.Id, ct));
    }

    public async Task<ServiceResult> DeleteAsync(long companyId, long departmentId, CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult.Unauthorized("Your session is invalid or has expired.");

        if (!await _companyRepository.IsAdminAsync(companyId, userId.Value, ct))
            return ServiceResult.Forbidden("ADMIN_ONLY", "Chỉ quản trị viên (Admin) mới có quyền quản lý phòng ban.");

        var department = await _db.Departments.FirstOrDefaultAsync(d => d.Id == departmentId && d.CompanyId == companyId, ct);
        if (department == null)
            return ServiceResult.NotFound("Không tìm thấy phòng ban.");

        var members = await _db.CompanyMembers
            .Where(cm => cm.CompanyId == companyId && cm.DepartmentId == departmentId)
            .ToListAsync(ct);
        foreach (var m in members)
            m.DepartmentId = null;

        _db.Departments.Remove(department);
        await _db.SaveChangesAsync(ct);
        return ServiceResult.NoContent();
    }

    public async Task<ServiceResult<DepartmentDto>> AssignMemberAsync(long companyId, long departmentId, AssignDepartmentMemberRequestDto request, CancellationToken ct = default)
    {
        var guard = await RequireAdminAsync<DepartmentDto>(companyId, ct);
        if (guard != null) return guard;

        var department = await _db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == departmentId && d.CompanyId == companyId, ct);
        if (department == null)
            return ServiceResult<DepartmentDto>.NotFound("Không tìm thấy phòng ban.");

        var member = await _db.CompanyMembers.FirstOrDefaultAsync(cm => cm.CompanyId == companyId && cm.UserId == request.UserId && cm.IsActive, ct);
        if (member == null)
            return ServiceResult<DepartmentDto>.NotFound("Nhân viên không thuộc công ty này.");

        // Mỗi nhân viên chỉ thuộc tối đa 1 phòng ban (ghi đè phòng ban cũ nếu có).
        member.DepartmentId = departmentId;
        await _db.SaveChangesAsync(ct);

        return ServiceResult<DepartmentDto>.Ok(await GetDtoAsync(departmentId, ct));
    }

    public async Task<ServiceResult<DepartmentDto>> RemoveMemberAsync(long companyId, long departmentId, long targetUserId, CancellationToken ct = default)
    {
        var guard = await RequireAdminAsync<DepartmentDto>(companyId, ct);
        if (guard != null) return guard;

        var member = await _db.CompanyMembers.FirstOrDefaultAsync(
            cm => cm.CompanyId == companyId && cm.UserId == targetUserId && cm.DepartmentId == departmentId, ct);
        if (member == null)
            return ServiceResult<DepartmentDto>.NotFound("Nhân viên không thuộc phòng ban này.");

        member.DepartmentId = null;
        await _db.SaveChangesAsync(ct);

        return ServiceResult<DepartmentDto>.Ok(await GetDtoAsync(departmentId, ct));
    }

    private async Task<ServiceResult<T>?> RequireAdminAsync<T>(long companyId, CancellationToken ct)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<T>.Unauthorized("Your session is invalid or has expired.");

        if (!await _companyRepository.IsAdminAsync(companyId, userId.Value, ct))
            return ServiceResult<T>.Forbidden("ADMIN_ONLY", "Chỉ quản trị viên (Admin) mới có quyền quản lý phòng ban.");

        return null;
    }

    private async Task<DepartmentDto> GetDtoAsync(long departmentId, CancellationToken ct)
        => await _db.Departments
            .AsNoTracking()
            .Where(d => d.Id == departmentId)
            .Select(d => new DepartmentDto
            {
                DepartmentId = d.Id,
                CompanyId = d.CompanyId,
                Name = d.Name,
                Code = d.Code,
                Description = d.Description,
                IsActive = d.IsActive,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt,
                MemberCount = d.Members.Count(m => m.IsActive),
                Members = d.Members
                    .Where(m => m.IsActive)
                    .OrderBy(m => m.User.FullName)
                    .Select(m => new UserRefDto
                    {
                        UserId = m.UserId,
                        FullName = m.User.FullName,
                        Email = m.User.Email,
                        AvatarUrl = m.User.AvatarUrl
                    })
                    .ToList()
            })
            .FirstAsync(ct);
}
