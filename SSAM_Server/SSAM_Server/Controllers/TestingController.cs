using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Testing;
using SSAM_Server.Services;

namespace SSAM_Server.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public class TestingController : ControllerBase
{
    private readonly ITestingService _testing;

    public TestingController(ITestingService testing)
    {
        _testing = testing;
    }

    // #45 GET /projects/{projectId}/test-tree
    [HttpGet("projects/{projectId:long}/test-tree")]
    public async Task<IActionResult> GetTestTree(long projectId, CancellationToken ct)
    {
        var result = await _testing.GetTestTreeAsync(projectId, ct);
        return result.ToActionResult(this);
    }

    // #46 POST /projects/{projectId}/test-folders
    [HttpPost("projects/{projectId:long}/test-folders")]
    public async Task<IActionResult> CreateTestFolder(
        long projectId,
        [FromBody] CreateTestFolderRequest request,
        CancellationToken ct)
    {
        var result = await _testing.CreateTestFolderAsync(projectId, request, ct);
        return result.ToActionResult(this);
    }

    // #47 GET /projects/{projectId}/testcases
    [HttpGet("projects/{projectId:long}/testcases")]
    public async Task<IActionResult> GetProjectTestCases(
        long projectId,
        [FromQuery] string? reqUid,
        [FromQuery] long? documentId,
        [FromQuery] long? folderId,
        [FromQuery] bool? unlinked,
        [FromQuery] bool? needsReview,
        [FromQuery] string? lastResult,
        [FromQuery] long? issueId,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var query = new TestCaseListQuery
        {
            ReqUid = reqUid,
            DocumentId = documentId,
            FolderId = folderId,
            Unlinked = unlinked,
            NeedsReview = needsReview,
            LastResult = lastResult,
            IssueId = issueId,
            Search = search
        };
        var result = await _testing.GetProjectTestCasesAsync(projectId, query, ct);
        return result.ToActionResult(this);
    }

    // #48 POST /projects/{projectId}/testcases
    [HttpPost("projects/{projectId:long}/testcases")]
    public async Task<IActionResult> CreateTestCase(
        long projectId,
        [FromBody] CreateTestCaseRequest request,
        CancellationToken ct)
    {
        var result = await _testing.CreateTestCaseAsync(projectId, request, ct);
        return result.ToActionResult(this);
    }

    // #49 GET /testcases/{testCaseId}
    [HttpGet("testcases/{testCaseId:long}")]
    public async Task<IActionResult> GetTestCaseDetail(long testCaseId, CancellationToken ct)
    {
        var result = await _testing.GetTestCaseDetailAsync(testCaseId, ct);
        return result.ToActionResult(this);
    }

    // #50 PATCH /testcases/{testCaseId}
    [HttpPatch("testcases/{testCaseId:long}")]
    public async Task<IActionResult> UpdateTestCase(
        long testCaseId,
        [FromBody] UpdateTestCaseRequest request,
        CancellationToken ct)
    {
        var result = await _testing.UpdateTestCaseAsync(testCaseId, request, ct);
        return result.ToActionResult(this);
    }

    // #51 DELETE /testcases/{testCaseId}
    [HttpDelete("testcases/{testCaseId:long}")]
    public async Task<IActionResult> DeleteTestCase(long testCaseId, CancellationToken ct)
    {
        var result = await _testing.DeleteTestCaseAsync(testCaseId, ct);
        return result.ToActionResult(this);
    }

    // #52 POST /projects/{projectId}/testcases/suggest
    [HttpPost("projects/{projectId:long}/testcases/suggest")]
    public async Task<IActionResult> SuggestTestCases(
        long projectId,
        [FromBody] SuggestTestCasesRequest request,
        CancellationToken ct)
    {
        var result = await _testing.SuggestTestCasesAsync(projectId, request, ct);
        return result.ToActionResult(this);
    }

    // #53 GET /projects/{projectId}/test-cycles?status=
    [HttpGet("projects/{projectId:long}/test-cycles")]
    public async Task<IActionResult> GetProjectTestCycles(
        long projectId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var result = await _testing.GetProjectTestCyclesAsync(projectId, status, ct);
        return result.ToActionResult(this);
    }

    // #54 POST /projects/{projectId}/test-cycles
    [HttpPost("projects/{projectId:long}/test-cycles")]
    public async Task<IActionResult> CreateTestCycle(
        long projectId,
        [FromBody] CreateTestCycleRequest request,
        CancellationToken ct)
    {
        var result = await _testing.CreateTestCycleAsync(projectId, request, ct);
        return result.ToActionResult(this);
    }

    // #55 PATCH /test-cycles/{cycleId}
    [HttpPatch("test-cycles/{cycleId:long}")]
    public async Task<IActionResult> UpdateTestCycle(
        long cycleId,
        [FromBody] UpdateTestCycleRequest request,
        CancellationToken ct)
    {
        var result = await _testing.UpdateTestCycleAsync(cycleId, request, ct);
        return result.ToActionResult(this);
    }

    // #56 GET /test-cycles/{cycleId}/runs
    [HttpGet("test-cycles/{cycleId:long}/runs")]
    public async Task<IActionResult> GetCycleRuns(
        long cycleId,
        [FromQuery] string? assigneeId,
        [FromQuery] string? result,
        [FromQuery] bool? needsRerun,
        CancellationToken ct)
    {
        var query = new TestRunListQuery
        {
            AssigneeId = assigneeId,
            Result = result,
            NeedsRerun = needsRerun
        };
        var res = await _testing.GetCycleRunsAsync(cycleId, query, ct);
        return res.ToActionResult(this);
    }

    // #57 PATCH /test-runs/{runId}
    [HttpPatch("test-runs/{runId:long}")]
    public async Task<IActionResult> UpdateTestRun(
        long runId,
        [FromBody] UpdateTestRunRequest request,
        CancellationToken ct)
    {
        var result = await _testing.UpdateTestRunAsync(runId, request, ct);
        return result.ToActionResult(this);
    }

    // #58 POST /test-runs/{runId}/result
    [HttpPost("test-runs/{runId:long}/result")]
    public async Task<IActionResult> RecordTestRunResult(
        long runId,
        [FromBody] RecordTestRunResultRequest request,
        CancellationToken ct)
    {
        var result = await _testing.RecordTestRunResultAsync(runId, request, ct);
        return result.ToActionResult(this);
    }
}
