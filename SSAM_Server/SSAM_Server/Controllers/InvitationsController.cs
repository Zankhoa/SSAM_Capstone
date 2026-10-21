using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Invitations;
using SSAM_Server.Services;

namespace SSAM_Server.Controllers;

[ApiController]
[Route("api/v1/invitations")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public class InvitationsController : ControllerBase
{
    private readonly IInvitationService _invitationService;

    public InvitationsController(IInvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    [HttpGet("{token}")]
    [ProducesResponseType(typeof(InvitationDetailsResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDetails(string token, CancellationToken cancellationToken)
    {
        var result = await _invitationService.GetInvitationDetailsAsync(token, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{token}/accept")]
    [ProducesResponseType(typeof(AcceptInvitationResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Accept(string token, [FromBody] AcceptInvitationRequestDto? request, CancellationToken cancellationToken)
    {
        var result = await _invitationService.AcceptInvitationAsync(token, request, cancellationToken);
        return result.ToActionResult(this);
    }
}
