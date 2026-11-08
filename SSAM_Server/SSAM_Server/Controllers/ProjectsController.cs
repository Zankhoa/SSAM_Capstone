using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Projects;
using SSAM_Server.Services;

namespace SSAM_Server.Controllers;

[Authorize]
[ApiController]
[Route("api/v1")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
    }

    // --- §10 Projects ---

    [HttpGet("workspaces/{workspaceId:long}/projects")]
    [ProducesResponseType(typeof(ItemsDto<ProjectDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListByWorkspace(
        long workspaceId,
        [FromQuery] string? status,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.ListWorkspaceProjectsAsync(workspaceId, status, search, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("projects")]
    [ProducesResponseType(typeof(ItemsDto<ProjectDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAll(
        [FromQuery] long? companyId,
        [FromQuery] string? status,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.ListProjectsAsync(companyId, status, search, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("workspaces/{workspaceId:long}/projects/key-availability")]
    [ProducesResponseType(typeof(ProjectKeyAvailabilityDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckKeyAvailability(
        long workspaceId,
        [FromQuery] string? key,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.CheckKeyAvailabilityAsync(workspaceId, key, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("workspaces/{workspaceId:long}/projects")]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        long workspaceId,
        [FromBody] CreateProjectRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.CreateProjectAsync(workspaceId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("projects/{projectId:long}")]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(long projectId, CancellationToken cancellationToken)
    {
        var result = await _projectService.GetProjectAsync(projectId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPatch("projects/{projectId:long}")]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        long projectId,
        [FromBody] UpdateProjectRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.UpdateProjectAsync(projectId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("projects/{projectId:long}/archive")]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Archive(long projectId, CancellationToken cancellationToken)
    {
        var result = await _projectService.ArchiveProjectAsync(projectId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("projects/{projectId:long}/restore")]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Restore(long projectId, CancellationToken cancellationToken)
    {
        var result = await _projectService.RestoreProjectAsync(projectId, cancellationToken);
        return result.ToActionResult(this);
    }

    // --- §11 Project Members ---

    [HttpGet("projects/{projectId:long}/members")]
    [ProducesResponseType(typeof(ItemsDto<ProjectMemberDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMembers(long projectId, CancellationToken cancellationToken)
    {
        var result = await _projectService.ListMembersAsync(projectId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("projects/{projectId:long}/member-candidates")]
    [ProducesResponseType(typeof(ItemsDto<ProjectMemberCandidateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMemberCandidates(
        long projectId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.ListMemberCandidatesAsync(projectId, search, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("projects/{projectId:long}/members")]
    [ProducesResponseType(typeof(ItemsDto<ProjectMemberDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddMembers(
        long projectId,
        [FromBody] AddProjectMembersRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.AddMembersAsync(projectId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPatch("projects/{projectId:long}/members/{userId:long}")]
    [ProducesResponseType(typeof(ProjectMemberDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMemberRole(
        long projectId,
        long userId,
        [FromBody] UpdateProjectMemberRoleRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.UpdateMemberRoleAsync(projectId, userId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpDelete("projects/{projectId:long}/members/{userId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveMember(long projectId, long userId, CancellationToken cancellationToken)
    {
        var result = await _projectService.RemoveMemberAsync(projectId, userId, cancellationToken);
        return result.ToActionResult(this);
    }

    // --- §12 Workflow ---

    [HttpGet("projects/{projectId:long}/workflow")]
    [ProducesResponseType(typeof(WorkflowDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWorkflow(long projectId, CancellationToken cancellationToken)
    {
        var result = await _projectService.GetWorkflowAsync(projectId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("projects/{projectId:long}/phases")]
    [ProducesResponseType(typeof(PhaseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreatePhase(
        long projectId,
        [FromBody] CreatePhaseRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.CreatePhaseAsync(projectId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPatch("projects/{projectId:long}/phases/{phaseId:long}")]
    [ProducesResponseType(typeof(WorkflowDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePhase(
        long projectId,
        long phaseId,
        [FromBody] UpdatePhaseRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.UpdatePhaseAsync(projectId, phaseId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPut("projects/{projectId:long}/phases/{phaseId:long}/workflow")]
    [ProducesResponseType(typeof(PhaseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePhaseWorkflow(
        long projectId,
        long phaseId,
        [FromBody] UpdatePhaseWorkflowRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.UpdatePhaseWorkflowAsync(projectId, phaseId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("projects/{projectId:long}/phases/{phaseId:long}/complete")]
    [ProducesResponseType(typeof(WorkflowDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompletePhase(
        long projectId,
        long phaseId,
        [FromBody] CompletePhaseRequestDto? request,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.CompletePhaseAsync(projectId, phaseId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("projects/{projectId:long}/phases/{phaseId:long}/reopen")]
    [ProducesResponseType(typeof(WorkflowDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReopenPhase(
        long projectId,
        long phaseId,
        [FromBody] ReopenPhaseRequestDto? request,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.ReopenPhaseAsync(projectId, phaseId, request, cancellationToken);
        return result.ToActionResult(this);
    }
}
