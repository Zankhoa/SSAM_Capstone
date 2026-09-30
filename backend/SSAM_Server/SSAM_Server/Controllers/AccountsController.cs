using Microsoft.AspNetCore.Mvc;
using SSAM.Application.Common.Interfaces.Services;
using SSAM.Application.DTOs.Auth;

namespace SSAM_Server.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountsController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _accountService.RegisterAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _accountService.LoginAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token, CancellationToken cancellationToken)
    {
        var isVerified = await _accountService.VerifyEmailAsync(token, cancellationToken);
        if (!isVerified)
        {
            return BadRequest(new { message = "Invalid or expired verification token." });
        }

        return Ok(new { message = "Email verified successfully." });
    }
}
