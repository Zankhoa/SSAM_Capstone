using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSAM.Application.Common.Exceptions;
using SSAM.Application.Common.Interfaces.Services;
using SSAM.Application.DTOs.Companies;
using SSAM.Application.DTOs.Invitations;

namespace SSAM_Server.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/companies")]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companyService;

    public CompaniesController(ICompanyService companyService)
    {
        _companyService = companyService;
    }

    [HttpGet("match")]
    [ProducesResponseType(typeof(CompanyMatchResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Match([FromQuery] string email, CancellationToken cancellationToken)
    {
        var result = await _companyService.MatchDomainAsync(email, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CompanyDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateCompanyRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var company = await _companyService.CreateCompanyAsync(request, userId, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, company);
    }

    [HttpPost("{companyId:long}/join")]
    [ProducesResponseType(typeof(CompanyJoinResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Join(long companyId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _companyService.JoinCompanyAsync(companyId, userId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{companyId:long}/join-requests")]
    [ProducesResponseType(typeof(JoinRequestResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateJoinRequest(long companyId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _companyService.CreateJoinRequestAsync(companyId, userId, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("{companyId:long}/join-requests/{requestId:long}")]
    [ProducesResponseType(typeof(JoinRequestStatusResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJoinRequestStatus(long companyId, long requestId, CancellationToken cancellationToken)
    {
        var result = await _companyService.GetJoinRequestStatusAsync(companyId, requestId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{companyId:long}/invitations")]
    [ProducesResponseType(typeof(CreateInvitationResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateInvitation(long companyId, [FromBody] CreateInvitationRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _companyService.CreateInvitationAsync(companyId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    private long GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                 ?? User.FindFirst("sub")?.Value;
        if (long.TryParse(claim, out var userId)) return userId;
        throw AppException.Unauthorized("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");
    }
}
