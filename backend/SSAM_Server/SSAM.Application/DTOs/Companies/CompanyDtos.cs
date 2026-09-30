namespace SSAM.Application.DTOs.Companies;

public class CreateCompanyRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? EmailDomain { get; set; }
    public bool DomainAutoJoinEnabled { get; set; } = false;
    public string TaxCode { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public string CompanySize { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
}

public class CompanyResponseDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? EmailDomain { get; set; }
    public bool DomainAutoJoinEnabled { get; set; }
    public string TaxCode { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public string CompanySize { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
