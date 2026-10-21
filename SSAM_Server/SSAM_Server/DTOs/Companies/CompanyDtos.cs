using System.Text.Json.Serialization;
using SSAM_Server.DTOs.Common;

namespace SSAM_Server.DTOs.Companies;

public class CompanyDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("emailDomain")]
    public string? EmailDomain { get; set; }

    [JsonPropertyName("domainAutoJoinEnabled")]
    public bool DomainAutoJoinEnabled { get; set; }

    [JsonPropertyName("taxCode")]
    public string TaxCode { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("logoUrl")]
    public string? LogoUrl { get; set; }
}

public class CompanyMatchResponseDto
{
    [JsonPropertyName("matched")]
    public bool Matched { get; set; }

    [JsonPropertyName("company")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MatchedCompanyDto? Company { get; set; }
}

public class MatchedCompanyDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("domain")]
    public string? Domain { get; set; }

    [JsonPropertyName("logoUrl")]
    public string? LogoUrl { get; set; }

    [JsonPropertyName("autoJoinEnabled")]
    public bool AutoJoinEnabled { get; set; }
}

public class CreateCompanyRequestDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("taxCode")]
    public string TaxCode { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("emailDomain")]
    public string? EmailDomain { get; set; }

    [JsonPropertyName("adminPhone")]
    public string? AdminPhone { get; set; }

    [JsonPropertyName("workspaceSlug")]
    public string WorkspaceSlug { get; set; } = string.Empty;

    [JsonPropertyName("domainAutoJoinEnabled")]
    public bool DomainAutoJoinEnabled { get; set; }
}

public class CompanyJoinResponseDto
{
    [JsonPropertyName("companyId")]
    public long CompanyId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "joined";
}

public class JoinRequestResponseDto
{
    [JsonPropertyName("requestId")]
    public long RequestId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";
}

public class JoinRequestStatusResponseDto
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";
}

public class CompanyUserDto : UserRefDto
{
    [JsonPropertyName("role")]
    public RoleRefDto Role { get; set; } = null!;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "ACTIVE";

    [JsonPropertyName("workspaceCount")]
    public int WorkspaceCount { get; set; }

    [JsonPropertyName("joinedAt")]
    public DateTime JoinedAt { get; set; }
}

public class UpdateCompanyUserRoleRequestDto
{
    [JsonPropertyName("roleId")]
    public long RoleId { get; set; }
}

