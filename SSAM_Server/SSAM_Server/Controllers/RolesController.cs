using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Roles;
using SSAM_Server.Services;

namespace SSAM_Server.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/companies/{companyId:long}/roles")]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ItemsDto<RoleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        long companyId,
        [FromQuery] string? scope,
        [FromQuery] bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _roleService.ListRolesAsync(companyId, scope, includeInactive, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("{roleId:long}")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(long companyId, long roleId, CancellationToken cancellationToken)
    {
        var result = await _roleService.GetRoleAsync(companyId, roleId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("{roleId:long}/members")]
    [ProducesResponseType(typeof(ItemsDto<RoleMemberDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMembers(long companyId, long roleId, CancellationToken cancellationToken)
    {
        var result = await _roleService.GetRoleMembersAsync(companyId, roleId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(long companyId, [FromBody] CreateRoleRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _roleService.CreateRoleAsync(companyId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPatch("{roleId:long}")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        long companyId,
        long roleId,
        [FromBody] UpdateRoleRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _roleService.UpdateRoleAsync(companyId, roleId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPatch("{roleId:long}/status")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStatus(
        long companyId,
        long roleId,
        [FromBody] UpdateRoleStatusRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _roleService.UpdateRoleStatusAsync(companyId, roleId, request, cancellationToken);
        return result.ToActionResult(this);
    }
}
