using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Companies;
using SSAM_Server.DTOs.Invitations;
using SSAM_Server.Services;

namespace SSAM_Server.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/companies")]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companyService;
    private readonly IInvitationService _invitationService;

    public CompaniesController(ICompanyService companyService, IInvitationService invitationService)
    {
        _companyService = companyService;
        _invitationService = invitationService;
    }

    [HttpGet("{companyId:long}")]
    [ProducesResponseType(typeof(CompanyDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(long companyId, CancellationToken cancellationToken)
    {
        var result = await _companyService.GetCompanyAsync(companyId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPatch("{companyId:long}")]
    [ProducesResponseType(typeof(CompanyDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(long companyId, [FromBody] UpdateCompanyRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _companyService.UpdateCompanyAsync(companyId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("match")]
    [ProducesResponseType(typeof(CompanyMatchResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Match([FromQuery] string email, CancellationToken cancellationToken)
    {
        var result = await _companyService.MatchDomainAsync(email, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CompanyDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateCompanyRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _companyService.CreateCompanyAsync(request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{companyId:long}/join")]
    [ProducesResponseType(typeof(CompanyJoinResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Join(long companyId, CancellationToken cancellationToken)
    {
        var result = await _companyService.JoinCompanyAsync(companyId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{companyId:long}/join-requests")]
    [ProducesResponseType(typeof(JoinRequestResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateJoinRequest(long companyId, CancellationToken cancellationToken)
    {
        var result = await _companyService.CreateJoinRequestAsync(companyId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("{companyId:long}/join-requests/{requestId:long}")]
    [ProducesResponseType(typeof(JoinRequestStatusResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJoinRequestStatus(long companyId, long requestId, CancellationToken cancellationToken)
    {
        var result = await _companyService.GetJoinRequestStatusAsync(companyId, requestId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{companyId:long}/invitations")]
    [ProducesResponseType(typeof(CreateInvitationResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateInvitation(long companyId, [FromBody] CreateInvitationRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _invitationService.CreateInvitationAsync(companyId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("{companyId:long}/users")]
    [ProducesResponseType(typeof(PageDto<CompanyUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListUsers(
        long companyId,
        [FromQuery] string? search,
        [FromQuery] long? roleId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _companyService.ListUsersAsync(companyId, search, roleId, status, page, pageSize, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPatch("{companyId:long}/users/{userId:long}/role")]
    [ProducesResponseType(typeof(CompanyUserDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateUserRole(
        long companyId,
        long userId,
        [FromBody] UpdateCompanyUserRoleRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _companyService.UpdateUserRoleAsync(companyId, userId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPatch("{companyId:long}/users/{userId:long}/status")]
    [ProducesResponseType(typeof(CompanyUserDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateUserStatus(
        long companyId,
        long userId,
        [FromBody] UpdateCompanyUserStatusRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _companyService.UpdateUserStatusAsync(companyId, userId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{companyId:long}/users")]
    [ProducesResponseType(typeof(CompanyUserDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateUser(
        long companyId,
        [FromBody] CreateCompanyUserRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _companyService.CreateUserAsync(companyId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPatch("{companyId:long}/users/{userId:long}")]
    [ProducesResponseType(typeof(CompanyUserDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateUser(
        long companyId,
        long userId,
        [FromBody] UpdateCompanyUserRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _companyService.UpdateUserAsync(companyId, userId, request, cancellationToken);
        return result.ToActionResult(this);
    }
}
