using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Ai;
using SSAM_Server.Interfaces.Services;

namespace SSAM_Server.Controllers;

[Authorize]
[ApiController]
[Route("api/v1")]
public class AiController : ControllerBase
{
    private readonly IAiChatService _aiChatService;

    public AiController(IAiChatService aiChatService)
    {
        _aiChatService = aiChatService;
    }

    [HttpGet("ai/status")]
    [ProducesResponseType(typeof(AiStatusDto), StatusCodes.Status200OK)]
    public IActionResult GetStatus()
    {
        return Ok(_aiChatService.GetStatus());
    }

    [HttpPost("ai/chat")]
    [ProducesResponseType(typeof(AiChatResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Chat([FromBody] AiChatRequestDto request, CancellationToken ct)
    {
        var result = await _aiChatService.ChatAsync(null, request, ct);
        return result.ToActionResult(this);
    }

    [HttpPost("projects/{projectId:long}/ai/chat")]
    [ProducesResponseType(typeof(AiChatResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChatInProject(long projectId, [FromBody] AiChatRequestDto request, CancellationToken ct)
    {
        var result = await _aiChatService.ChatAsync(projectId, request, ct);
        return result.ToActionResult(this);
    }
}
