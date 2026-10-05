using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSAM.Application.Common.Exceptions;
using SSAM.Application.Features.Auth;
using SSAM.Application.Features.Auth.DTOs;

namespace SSAM_Server.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("google")]
    [ProducesResponseType(typeof(AuthSessionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequestDto request, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var result = await _authService.GoogleLoginAsync(request, ip, userAgent, cancellationToken);
        return Ok(result);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthSessionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var result = await _authService.LoginAsync(request, ip, userAgent, cancellationToken);
        return Ok(result);
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(TokenRefreshResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var result = await _authService.RefreshTokenAsync(request, ip, userAgent, cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequestDto request, CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(request, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(AuthMeResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _authService.GetMeAsync(userId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("email/verify")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequestDto request, CancellationToken cancellationToken)
    {
        await _authService.VerifyEmailAsync(request.Token, cancellationToken);
        return Ok(new { verified = true });
    }

    [HttpPost("email/resend")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ResendEmail([FromBody] EmailRequestDto request, CancellationToken cancellationToken)
    {
        await _authService.ResendEmailVerificationAsync(request.Email, cancellationToken);
        return Accepted();
    }

    [HttpPost("password/forgot")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword([FromBody] EmailRequestDto request, CancellationToken cancellationToken)
    {
        await _authService.ForgotPasswordAsync(request.Email, cancellationToken);
        return Accepted();
    }

    [HttpPost("password/reset")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request, CancellationToken cancellationToken)
    {
        await _authService.ResetPasswordAsync(request, cancellationToken);
        return Ok(new { success = true });
    }

    private long GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                 ?? User.FindFirst("sub")?.Value;
        if (long.TryParse(claim, out var userId)) return userId;
        throw AppException.Unauthorized("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");
    }
}
