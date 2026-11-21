using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Diagrams;
using SSAM_Server.Interfaces.Services;

namespace SSAM_Server.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/diagrams")]
public class DiagramsController : ControllerBase
{
    private readonly IDiagramService _diagramService;

    public DiagramsController(IDiagramService diagramService)
    {
        _diagramService = diagramService;
    }

    [HttpPost("render")]
    [EnableRateLimiting("diagram")]
    [ProducesResponseType(typeof(RenderDiagramResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Render([FromBody] RenderDiagramRequest request, CancellationToken ct)
    {
        var result = await _diagramService.RenderAsync(request, ct);
        return result.ToActionResult(this);
    }
}
