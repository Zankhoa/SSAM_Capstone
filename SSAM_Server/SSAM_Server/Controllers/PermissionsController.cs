using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SSAM_Server.Data;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Roles;

namespace SSAM_Server.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/permissions")]
public class PermissionsController : ControllerBase
{
    private readonly AppDbContext _db;

    public PermissionsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ItemsDto<PermissionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? scope, CancellationToken cancellationToken)
    {
        var query = _db.Permissions
            .AsNoTracking()
            .Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(scope))
        {
            var normalized = scope.Trim().ToUpperInvariant();
            query = query.Where(p => p.Scope == normalized);
        }

        var items = await query
            .OrderBy(p => p.SortOrder)
            .Select(p => new PermissionDto
            {
                Key = p.Code,
                Name = p.Name,
                Description = p.Description,
                Group = p.GroupName,
                Scope = p.Scope,
                IsSensitive = p.IsSensitive
            })
            .ToListAsync(cancellationToken);

        return Ok(new ItemsDto<PermissionDto>(items));
    }
}
