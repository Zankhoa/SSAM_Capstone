using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Work;

namespace SSAM_Server.Services;

public interface IWorkService
{
    Task<ServiceResult<MyDocumentWorkDto>> GetMyDocumentWorkAsync(long? projectId, CancellationToken ct = default);
    Task<ServiceResult<ItemsDto<WorkItemDto>>> GetMyWorkItemsAsync(long? projectId, CancellationToken ct = default);
}
