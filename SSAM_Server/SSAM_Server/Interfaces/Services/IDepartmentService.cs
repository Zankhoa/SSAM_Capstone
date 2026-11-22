using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Companies;

namespace SSAM_Server.Interfaces.Services;

public interface IDepartmentService
{
    Task<ServiceResult<ItemsDto<DepartmentDto>>> ListAsync(long companyId, CancellationToken ct = default);
    Task<ServiceResult<DepartmentDto>> CreateAsync(long companyId, CreateDepartmentRequestDto request, CancellationToken ct = default);
    Task<ServiceResult<DepartmentDto>> UpdateAsync(long companyId, long departmentId, UpdateDepartmentRequestDto request, CancellationToken ct = default);
    Task<ServiceResult> DeleteAsync(long companyId, long departmentId, CancellationToken ct = default);
    Task<ServiceResult<DepartmentDto>> AssignMemberAsync(long companyId, long departmentId, AssignDepartmentMemberRequestDto request, CancellationToken ct = default);
    Task<ServiceResult<DepartmentDto>> RemoveMemberAsync(long companyId, long departmentId, long targetUserId, CancellationToken ct = default);
}
