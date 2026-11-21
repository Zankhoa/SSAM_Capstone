using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Ai;
using SSAM_Server.DTOs.Common;
using SSAM_Server.Interfaces.Services;

namespace SSAM_Server.Controllers;

[Authorize]
[ApiController]
[Route("api/v1")]
public class AiController : ControllerBase
{
    private readonly IAiChatService _aiChatService;
    private readonly IAiTaskService _aiTaskService;

    public AiController(IAiChatService aiChatService, IAiTaskService aiTaskService)
    {
        _aiChatService = aiChatService;
        _aiTaskService = aiTaskService;
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

    [HttpPost("projects/{projectId:long}/ai/tasks")]
    [EndpointSummary("Run a project-scoped AI task")]
    [EndpointDescription("Runs an allow-listed AI task after checking project access, the ai.use permission, and the project's AI setting.")]
    [ProducesResponseType(typeof(AiTaskResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RunTask(
        long projectId,
        [FromBody] AiTaskRequestDto request,
        CancellationToken ct)
    {
        var result = await _aiTaskService.RunAsync(projectId, request, ct);
        return result.ToActionResult(this);
    }
}
