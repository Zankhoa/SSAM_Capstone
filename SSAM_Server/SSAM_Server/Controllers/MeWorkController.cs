using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSAM_Server.Common;
using SSAM_Server.Services;

namespace SSAM_Server.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/me")]
public class MeWorkController : ControllerBase
{
    private readonly IWorkService _work;

    public MeWorkController(IWorkService work)
    {
        _work = work;
    }

    // #23 GET /me/document-work?companyId=&projectId=
    [HttpGet("document-work")]
    public async Task<IActionResult> GetMyDocumentWork([FromQuery] long? companyId,[FromQuery] long? projectId, CancellationToken ct)
    {
        var result = await _work.GetMyDocumentWorkAsync(projectId, companyId, ct);
        return result.ToActionResult(this);
    }

    // #24 GET /me/work-items?companyId=&projectId=
    [HttpGet("work-items")]
    public async Task<IActionResult> GetMyWorkItems(
        [FromQuery] long? companyId,
        [FromQuery] long? projectId,
        CancellationToken ct)
    {
        var result = await _work.GetMyWorkItemsAsync(projectId, companyId, ct);
        return result.ToActionResult(this);
    }
}
