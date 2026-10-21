using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Projects;
using SSAM_Server.Services;

namespace SSAM_Server.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/sdlc-templates")]
public class SdlcTemplatesController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ItemsDto<SdlcTemplateDto>), StatusCodes.Status200OK)]
    public IActionResult List()
    {
        return Ok(new ItemsDto<SdlcTemplateDto>(SdlcTemplateCatalog.Templates));
    }
}
