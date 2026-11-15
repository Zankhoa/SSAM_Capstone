using SSAM_Server.Common;
using SSAM_Server.DTOs.Diagrams;

namespace SSAM_Server.Interfaces.Services;

public interface IDiagramService
{
    Task<ServiceResult<RenderDiagramResponse>> RenderAsync(RenderDiagramRequest request, CancellationToken ct = default);
}
