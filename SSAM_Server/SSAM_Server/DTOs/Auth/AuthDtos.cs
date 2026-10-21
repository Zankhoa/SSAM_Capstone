using System.Text.Json.Serialization;
using SSAM_Server.DTOs.Common;

namespace SSAM_Server.DTOs.Auth;

public class UserDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("avatarUrl")]
    public string? AvatarUrl { get; set; }

    [JsonPropertyName("emailVerified")]
    public bool EmailVerified { get; set; }
}

public class RegisterRequestDto
{
    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

public class RegisterResponseDto
{
    [JsonPropertyName("accountId")]
    public long AccountId { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("emailVerified")]
    public bool EmailVerified { get; set; }
}

public class LoginRequestDto
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

public class GoogleLoginRequestDto
{
    [JsonPropertyName("idToken")]
    public string IdToken { get; set; } = string.Empty;
}

public class AuthSessionDto
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonPropertyName("expiresIn")]
    public int ExpiresIn { get; set; } = 900;

    [JsonPropertyName("user")]
    public UserDto User { get; set; } = null!;

    [JsonPropertyName("isNewAccount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsNewAccount { get; set; }
}

public class RefreshTokenRequestDto
{
    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;
}

public class TokenRefreshResponseDto
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonPropertyName("expiresIn")]
    public int ExpiresIn { get; set; } = 900;
}

public class LogoutRequestDto
{
    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;
}

public class AuthMeResponseDto
{
    [JsonPropertyName("user")]
    public UserDto User { get; set; } = null!;

    [JsonPropertyName("companies")]
    public List<UserCompanyDto> Companies { get; set; } = new();

    [JsonPropertyName("workspaces")]
    public List<UserWorkspaceDto> Workspaces { get; set; } = new();
}

public class UserCompanyDto
{
    [JsonPropertyName("companyId")]
    public long CompanyId { get; set; }

    [JsonPropertyName("companyName")]
    public string CompanyName { get; set; } = string.Empty;

    [JsonPropertyName("roleName")]
    public string RoleName { get; set; } = string.Empty;

    [JsonPropertyName("roleId")]
    public long RoleId { get; set; }

    [JsonPropertyName("roleCode")]
    public string? RoleCode { get; set; }

    [JsonPropertyName("permissions")]
    public List<string> Permissions { get; set; } = new();
}

public class UserWorkspaceDto
{
    [JsonPropertyName("workspaceId")]
    public long WorkspaceId { get; set; }

    [JsonPropertyName("workspaceName")]
    public string WorkspaceName { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("companyId")]
    public long? CompanyId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "ACTIVE";

    [JsonPropertyName("role")]
    public RoleRefDto? Role { get; set; }

    [JsonPropertyName("permissions")]
    public List<string> Permissions { get; set; } = new();
}

public class VerifyEmailRequestDto
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;
}

public class EmailRequestDto
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequestDto
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("newPassword")]
    public string NewPassword { get; set; } = string.Empty;
}
