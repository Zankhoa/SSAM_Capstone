using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.Services;

namespace SSAM_Server.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public class KanbanController : ControllerBase
{
    private readonly IKanbanService _kanban;

    public KanbanController(IKanbanService kanban)
    {
        _kanban = kanban;
    }

    // #26 GET /projects/{projectId}/board
    [HttpGet("projects/{projectId:long}/board")]
    public async Task<IActionResult> GetBoard(long projectId, CancellationToken ct)
    {
        var result = await _kanban.GetBoardAsync(projectId, ct);
        return result.ToActionResult(this);
    }

    // #27 PUT /projects/{projectId}/board/wip
    [HttpPut("projects/{projectId:long}/board/wip")]
    public async Task<IActionResult> UpdateWipLimits(
        long projectId,
        [FromBody] UpdateWipLimitsRequest request,
        CancellationToken ct)
    {
        var result = await _kanban.UpdateWipLimitsAsync(projectId, request, ct);
        return result.ToActionResult(this);
    }

    // #28 GET /projects/{projectId}/issues
    [HttpGet("projects/{projectId:long}/issues")]
    public async Task<IActionResult> GetProjectIssues(
        long projectId,
        [FromQuery] string? sprintId,
        [FromQuery] string? assigneeId,
        [FromQuery] string? type,
        [FromQuery] long? statusId,
        [FromQuery] string? reqUid,
        [FromQuery] bool? reqChanged,
        [FromQuery] string? search,
        [FromQuery] string? parentId,
        CancellationToken ct)
    {
        var query = new IssueListQuery
        {
            SprintId = sprintId,
            AssigneeId = assigneeId,
            Type = type,
            StatusId = statusId,
            ReqUid = reqUid,
            ReqChanged = reqChanged,
            Search = search,
            ParentId = parentId
        };
        var result = await _kanban.GetProjectIssuesAsync(projectId, query, ct);
        return result.ToActionResult(this);
    }

    // #29 POST /projects/{projectId}/issues
    [HttpPost("projects/{projectId:long}/issues")]
    public async Task<IActionResult> CreateIssue(
        long projectId,
        [FromBody] CreateIssueRequest request,
        CancellationToken ct)
    {
        var result = await _kanban.CreateIssueAsync(projectId, request, ct);
        return result.ToActionResult(this);
    }

    // #30 GET /issues/{issueId}
    [HttpGet("issues/{issueId:long}")]
    public async Task<IActionResult> GetIssueDetail(long issueId, CancellationToken ct)
    {
        var result = await _kanban.GetIssueDetailAsync(issueId, ct);
        return result.ToActionResult(this);
    }

    // #31 PATCH /issues/{issueId}
    [HttpPatch("issues/{issueId:long}")]
    public async Task<IActionResult> UpdateIssue(
        long issueId,
        [FromBody] UpdateIssueRequest request,
        CancellationToken ct)
    {
        var result = await _kanban.UpdateIssueAsync(issueId, request, ct);
        return result.ToActionResult(this);
    }

    // #32 DELETE /issues/{issueId}
    [HttpDelete("issues/{issueId:long}")]
    public async Task<IActionResult> DeleteIssue(long issueId, CancellationToken ct)
    {
        var result = await _kanban.DeleteIssueAsync(issueId, ct);
        return result.ToActionResult(this);
    }

    // #33 POST /issues/{issueId}/transition
    [HttpPost("issues/{issueId:long}/transition")]
    public async Task<IActionResult> TransitionIssue(
        long issueId,
        [FromBody] TransitionIssueRequest request,
        CancellationToken ct)
    {
        var result = await _kanban.TransitionIssueAsync(issueId, request, ct);
        return result.ToActionResult(this);
    }

    // #34 POST /issues/{issueId}/rank
    [HttpPost("issues/{issueId:long}/rank")]
    public async Task<IActionResult> RankIssue(
        long issueId,
        [FromBody] RankIssueRequest request,
        CancellationToken ct)
    {
        var result = await _kanban.RankIssueAsync(issueId, request, ct);
        return result.ToActionResult(this);
    }

    // #35 POST /projects/{projectId}/issues/from-requirements
    [HttpPost("projects/{projectId:long}/issues/from-requirements")]
    public async Task<IActionResult> CreateIssuesFromRequirements(
        long projectId,
        [FromBody] CreateIssuesFromRequirementsRequest request,
        CancellationToken ct)
    {
        var result = await _kanban.CreateIssuesFromRequirementsAsync(projectId, request, ct);
        return result.ToActionResult(this);
    }

    // #36 PUT /issues/{issueId}/requirements
    [HttpPut("issues/{issueId:long}/requirements")]
    public async Task<IActionResult> UpdateIssueRequirements(
        long issueId,
        [FromBody] UpdateIssueRequirementsRequest request,
        CancellationToken ct)
    {
        var result = await _kanban.UpdateIssueRequirementsAsync(issueId, request, ct);
        return result.ToActionResult(this);
    }

    // #37 POST /issues/{issueId}/requirements/ack
    [HttpPost("issues/{issueId:long}/requirements/ack")]
    public async Task<IActionResult> AcknowledgeIssueRequirements(long issueId, CancellationToken ct)
    {
        var result = await _kanban.AcknowledgeIssueRequirementsAsync(issueId, ct);
        return result.ToActionResult(this);
    }

    // #38 GET /issues/{issueId}/comments
    [HttpGet("issues/{issueId:long}/comments")]
    public async Task<IActionResult> GetIssueComments(long issueId, CancellationToken ct)
    {
        var result = await _kanban.GetIssueCommentsAsync(issueId, ct);
        return result.ToActionResult(this);
    }

    // #39 POST /issues/{issueId}/comments
    [HttpPost("issues/{issueId:long}/comments")]
    public async Task<IActionResult> CreateIssueComment(
        long issueId,
        [FromBody] CreateIssueCommentRequest request,
        CancellationToken ct)
    {
        var result = await _kanban.CreateIssueCommentAsync(issueId, request, ct);
        return result.ToActionResult(this);
    }

    // #40 GET /issues/{issueId}/activities (and alias /activity)
    [HttpGet("issues/{issueId:long}/activities")]
    [HttpGet("issues/{issueId:long}/activity")]
    public async Task<IActionResult> GetIssueActivities(long issueId, CancellationToken ct)
    {
        var result = await _kanban.GetIssueActivitiesAsync(issueId, ct);
        return result.ToActionResult(this);
    }

    // #41 GET /projects/{projectId}/sprints?status=
    [HttpGet("projects/{projectId:long}/sprints")]
    public async Task<IActionResult> GetProjectSprints(
        long projectId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var result = await _kanban.GetProjectSprintsAsync(projectId, status, ct);
        return result.ToActionResult(this);
    }

    // #42 POST /projects/{projectId}/sprints
    [HttpPost("projects/{projectId:long}/sprints")]
    public async Task<IActionResult> CreateSprint(
        long projectId,
        [FromBody] CreateSprintRequest request,
        CancellationToken ct)
    {
        var result = await _kanban.CreateSprintAsync(projectId, request, ct);
        return result.ToActionResult(this);
    }

    // #43 PATCH /sprints/{sprintId}
    [HttpPatch("sprints/{sprintId:long}")]
    public async Task<IActionResult> UpdateSprint(
        long sprintId,
        [FromBody] UpdateSprintRequest request,
        CancellationToken ct)
    {
        var result = await _kanban.UpdateSprintAsync(sprintId, request, ct);
        return result.ToActionResult(this);
    }

    // #43b POST /sprints/{sprintId}/start
    [HttpPost("sprints/{sprintId:long}/start")]
    public async Task<IActionResult> StartSprint(
        long sprintId,
        [FromBody] StartSprintRequest? request,
        CancellationToken ct)
    {
        var result = await _kanban.StartSprintAsync(sprintId, request, ct);
        return result.ToActionResult(this);
    }

    // #44 POST /sprints/{sprintId}/complete
    [HttpPost("sprints/{sprintId:long}/complete")]
    public async Task<IActionResult> CompleteSprint(
        long sprintId,
        [FromBody] CompleteSprintRequest? request,
        CancellationToken ct)
    {
        var result = await _kanban.CompleteSprintAsync(sprintId, request, ct);
        return result.ToActionResult(this);
    }
}
