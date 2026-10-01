using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SSAM.Application.Common.Interfaces.Services;
using SSAM.Application.DTOs.Invitations;

namespace SSAM_Server.Controllers;

[ApiController]
[Route("api/v1/invitations")]
public class InvitationsController : ControllerBase
{
    private readonly ICompanyService _companyService;

    public InvitationsController(ICompanyService companyService)
    {
        _companyService = companyService;
    }

    [HttpGet("{token}")]
    [ProducesResponseType(typeof(InvitationDetailsResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDetails(string token, CancellationToken cancellationToken)
    {
        var result = await _companyService.GetInvitationDetailsAsync(token, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{token}/accept")]
    [ProducesResponseType(typeof(AcceptInvitationResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Accept(
        string token, 
        [FromBody] AcceptInvitationRequestDto? request, 
        CancellationToken cancellationToken)
    {
        long? currentUserId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                     ?? User.FindFirst("sub")?.Value;
            if (long.TryParse(claim, out var uid))
            {
                currentUserId = uid;
            }
        }

        var result = await _companyService.AcceptInvitationAsync(token, request, currentUserId, cancellationToken);
        return Ok(result);
    }
}
