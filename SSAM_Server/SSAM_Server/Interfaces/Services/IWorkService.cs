using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Work;

namespace SSAM_Server.Interfaces.Services;

public interface IWorkService
{
    Task<ServiceResult<MyDocumentWorkDto>> GetMyDocumentWorkAsync(long? projectId, long? companyId = null, CancellationToken ct = default);
    Task<ServiceResult<ItemsDto<WorkItemDto>>> GetMyWorkItemsAsync(long? projectId, long? companyId = null, CancellationToken ct = default);
}
