using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Designs;
using SSAM_Server.Interfaces.Services;

namespace SSAM_Server.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/designs")]
public class DesignsController : ControllerBase
{
    private readonly IFigmaService _figmaService;

    public DesignsController(IFigmaService figmaService)
    {
        _figmaService = figmaService;
    }

    [HttpPost("resolve")]
    [ProducesResponseType(typeof(ResolveDesignResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Resolve([FromBody] ResolveDesignRequest request, CancellationToken ct)
    {
        var result = await _figmaService.ResolveAsync(request, ct);
        return result.ToActionResult(this);
    }
}
