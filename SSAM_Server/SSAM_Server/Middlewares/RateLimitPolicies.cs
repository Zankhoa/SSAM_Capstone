namespace SSAM_Server;

/// <summary>Names of the rate-limiting policies registered in Program.cs.</summary>
public static class RateLimitPolicies
{
    /// <summary>Credential/token endpoints: brute-force and token-guessing protection.</summary>
    public const string Auth = "auth";

    /// <summary>Endpoints that trigger outgoing e-mail: stricter, to prevent mail-bombing.</summary>
    public const string Email = "email";
}
