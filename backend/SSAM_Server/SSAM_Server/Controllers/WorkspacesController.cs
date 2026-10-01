using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSAM.Application.Common.Exceptions;
using SSAM.Application.Common.Interfaces.Services;
using SSAM.Application.DTOs.Workspaces;

namespace SSAM_Server.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/workspaces")]
public class WorkspacesController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;

    public WorkspacesController(IWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(WorkspaceSummaryDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateWorkspaceRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var workspace = await _workspaceService.CreateWorkspaceAsync(request, userId, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, workspace);
    }

    private long GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                 ?? User.FindFirst("sub")?.Value;
        if (long.TryParse(claim, out var userId)) return userId;
        throw AppException.Unauthorized("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");
    }
}
