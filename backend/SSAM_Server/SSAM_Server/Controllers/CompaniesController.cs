using Microsoft.AspNetCore.Mvc;
using SSAM.Application.Common.Interfaces.Services;
using SSAM.Application.DTOs.Companies;

namespace SSAM_Server.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companyService;

    public CompaniesController(ICompanyService companyService)
    {
        _companyService = companyService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCompanyRequestDto request, [FromHeader(Name = "X-User-Id")] long userId, CancellationToken cancellationToken)
    {
        var company = await _companyService.CreateCompanyAsync(request, userId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = company.Id }, company);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var company = await _companyService.GetCompanyByIdAsync(id, cancellationToken);
        if (company == null)
        {
            return NotFound();
        }

        return Ok(company);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var companies = await _companyService.GetAllActiveCompaniesAsync(cancellationToken);
        return Ok(companies);
    }
}
