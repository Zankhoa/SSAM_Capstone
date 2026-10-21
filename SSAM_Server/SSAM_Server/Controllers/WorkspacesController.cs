using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Workspaces;
using SSAM_Server.Services;

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

    [HttpGet]
    [ProducesResponseType(typeof(ItemsDto<WorkspaceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] long? companyId,
        [FromQuery] string? status,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await _workspaceService.ListWorkspacesAsync(companyId, status, search, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("code-availability")]
    [ProducesResponseType(typeof(WorkspaceCodeAvailabilityDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckCodeAvailability(
        [FromQuery] long companyId,
        [FromQuery] string? code,
        CancellationToken cancellationToken)
    {
        var result = await _workspaceService.CheckCodeAvailabilityAsync(companyId, code, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("{workspaceId:long}")]
    [ProducesResponseType(typeof(WorkspaceDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(long workspaceId, CancellationToken cancellationToken)
    {
        var result = await _workspaceService.GetWorkspaceAsync(workspaceId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [ProducesResponseType(typeof(WorkspaceDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateWorkspaceRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _workspaceService.CreateWorkspaceAsync(request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPatch("{workspaceId:long}")]
    [ProducesResponseType(typeof(WorkspaceDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        long workspaceId,
        [FromBody] UpdateWorkspaceRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _workspaceService.UpdateWorkspaceAsync(workspaceId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{workspaceId:long}/archive")]
    [ProducesResponseType(typeof(WorkspaceDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Archive(long workspaceId, CancellationToken cancellationToken)
    {
        var result = await _workspaceService.ArchiveWorkspaceAsync(workspaceId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{workspaceId:long}/restore")]
    [ProducesResponseType(typeof(WorkspaceDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Restore(long workspaceId, CancellationToken cancellationToken)
    {
        var result = await _workspaceService.RestoreWorkspaceAsync(workspaceId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("{workspaceId:long}/members")]
    [ProducesResponseType(typeof(ItemsDto<WorkspaceMemberDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMembers(
        long workspaceId,
        [FromQuery] long? roleId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await _workspaceService.ListMembersAsync(workspaceId, roleId, search, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("{workspaceId:long}/member-candidates")]
    [ProducesResponseType(typeof(ItemsDto<UserRefDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMemberCandidates(
        long workspaceId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await _workspaceService.ListMemberCandidatesAsync(workspaceId, search, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{workspaceId:long}/members")]
    [ProducesResponseType(typeof(ItemsDto<WorkspaceMemberDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddMembers(
        long workspaceId,
        [FromBody] AddWorkspaceMembersRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _workspaceService.AddMembersAsync(workspaceId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPatch("{workspaceId:long}/members/{userId:long}")]
    [ProducesResponseType(typeof(WorkspaceMemberDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMemberRole(
        long workspaceId,
        long userId,
        [FromBody] UpdateWorkspaceMemberRoleRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _workspaceService.UpdateMemberRoleAsync(workspaceId, userId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpDelete("{workspaceId:long}/members/{userId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveMember(long workspaceId, long userId, CancellationToken cancellationToken)
    {
        var result = await _workspaceService.RemoveMemberAsync(workspaceId, userId, cancellationToken);
        return result.ToActionResult(this);
    }
}
