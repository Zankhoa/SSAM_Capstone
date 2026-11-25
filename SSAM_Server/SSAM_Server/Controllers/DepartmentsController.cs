using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Companies;
using SSAM_Server.Interfaces.Services;

namespace SSAM_Server.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/companies/{companyId:long}/departments")]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _departmentService;

    public DepartmentsController(IDepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ItemsDto<DepartmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(long companyId, CancellationToken ct)
    {
        var result = await _departmentService.ListAsync(companyId, ct);
        return result.ToActionResult(this);
    }

    [HttpGet("assignable-users")]
    [ProducesResponseType(typeof(ItemsDto<UserRefDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAssignableUsers(long companyId, CancellationToken ct)
    {
        var result = await _departmentService.ListAssignableUsersAsync(companyId, ct);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(long companyId, [FromBody] CreateDepartmentRequestDto request, CancellationToken ct)
    {
        var result = await _departmentService.CreateAsync(companyId, request, ct);
        return result.ToActionResult(this);
    }

    [HttpPatch("{departmentId:long}")]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(long companyId, long departmentId, [FromBody] UpdateDepartmentRequestDto request, CancellationToken ct)
    {
        var result = await _departmentService.UpdateAsync(companyId, departmentId, request, ct);
        return result.ToActionResult(this);
    }

    [HttpDelete("{departmentId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(long companyId, long departmentId, CancellationToken ct)
    {
        var result = await _departmentService.DeleteAsync(companyId, departmentId, ct);
        return result.ToActionResult(this);
    }

    [HttpPost("{departmentId:long}/members")]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignMember(long companyId, long departmentId, [FromBody] AssignDepartmentMemberRequestDto request, CancellationToken ct)
    {
        var result = await _departmentService.AssignMemberAsync(companyId, departmentId, request, ct);
        return result.ToActionResult(this);
    }

    [HttpDelete("{departmentId:long}/members/{userId:long}")]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveMember(long companyId, long departmentId, long userId, CancellationToken ct)
    {
        var result = await _departmentService.RemoveMemberAsync(companyId, departmentId, userId, ct);
        return result.ToActionResult(this);
    }
}
