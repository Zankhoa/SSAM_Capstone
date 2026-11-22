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
    private readonly IFigmaWatchService _figmaWatchService;

    public DesignsController(IFigmaService figmaService, IFigmaWatchService figmaWatchService)
    {
        _figmaService = figmaService;
        _figmaWatchService = figmaWatchService;
    }

    [HttpPost("resolve")]
    [ProducesResponseType(typeof(ResolveDesignResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Resolve([FromBody] ResolveDesignRequest request, CancellationToken ct)
    {
        var result = await _figmaService.ResolveAsync(request, ct);
        return result.ToActionResult(this);
    }

    [AllowAnonymous]
    [HttpPost("webhook")]
    [ProducesResponseType(typeof(FigmaCheckUpdatesResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Webhook([FromBody] FigmaWebhookPayload payload, CancellationToken ct)
    {
        var result = await _figmaWatchService.HandleWebhookAsync(payload, ct);
        return result.ToActionResult(this);
    }

    [HttpPost("check-updates")]
    [ProducesResponseType(typeof(FigmaCheckUpdatesResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckUpdates(CancellationToken ct)
    {
        var result = await _figmaWatchService.CheckAllWatchedFilesAsync(ct);
        return Ok(result);
    }
}
