using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Testing;
using SSAM_Server.Services;

namespace SSAM_Server.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class TestingController : ControllerBase
{
    private readonly ITestingService _testingService;

    public TestingController(ITestingService testingService)
    {
        _testingService = testingService;
    }

    private IActionResult FromResult(ServiceResult result) => result.ToActionResult(this);
    private IActionResult FromResult<T>(ServiceResult<T> result) => result.ToActionResult(this);

    // --- Test Plans ---

    [HttpGet("projects/{projectId:long}/test-plans")]
    public async Task<IActionResult> GetProjectTestPlans(long projectId, CancellationToken ct)
    {
        var result = await _testingService.GetProjectTestPlansAsync(projectId, ct);
        return FromResult(result);
    }

    [HttpPost("projects/{projectId:long}/test-plans")]
    public async Task<IActionResult> CreateTestPlan(
        long projectId,
        [FromBody] CreateTestPlanRequest request,
        CancellationToken ct)
    {
        var result = await _testingService.CreateTestPlanAsync(projectId, request, ct);
        return FromResult(result);
    }

    [HttpGet("test-plans/{planId:long}")]
    public async Task<IActionResult> GetTestPlan(long planId, CancellationToken ct)
    {
        var result = await _testingService.GetTestPlanAsync(planId, ct);
        return FromResult(result);
    }

    [HttpPatch("test-plans/{planId:long}")]
    public async Task<IActionResult> UpdateTestPlan(
        long planId,
        [FromBody] UpdateTestPlanRequest request,
        CancellationToken ct)
    {
        var result = await _testingService.UpdateTestPlanAsync(planId, request, ct);
        return FromResult(result);
    }

    [HttpDelete("test-plans/{planId:long}")]
    public async Task<IActionResult> DeleteTestPlan(long planId, CancellationToken ct)
    {
        var result = await _testingService.DeleteTestPlanAsync(planId, ct);
        return FromResult(result);
    }

    [HttpGet("projects/{projectId:long}/test-repository")]
    public async Task<IActionResult> GetTestRepository(long projectId, CancellationToken ct)
    {
        var result = await _testingService.GetTestRepositoryAsync(projectId, ct);
        return FromResult(result);
    }

    [HttpPost("test-plans/{planId:long}/testcases/from-repository")]
    public async Task<IActionResult> AddFromRepository(
        long planId,
        [FromBody] AddFromRepositoryRequest request,
        CancellationToken ct)
    {
        var result = await _testingService.AddTestCasesFromRepositoryAsync(planId, request, ct);
        return FromResult(result);
    }

    // --- Environments & Builds ---

    [HttpPost("test-plans/{planId:long}/environments")]
    public async Task<IActionResult> CreateEnvironment(
        long planId,
        [FromBody] CreateTestEnvironmentRequest request,
        CancellationToken ct)
    {
        var result = await _testingService.CreateEnvironmentAsync(planId, request, ct);
        return FromResult(result);
    }

    [HttpPatch("test-environments/{envId:long}")]
    public async Task<IActionResult> UpdateEnvironment(
        long envId,
        [FromBody] UpdateTestEnvironmentRequest request,
        CancellationToken ct)
    {
        var result = await _testingService.UpdateEnvironmentAsync(envId, request, ct);
        return FromResult(result);
    }

    [HttpDelete("test-environments/{envId:long}")]
    public async Task<IActionResult> DeleteEnvironment(long envId, CancellationToken ct)
    {
        var result = await _testingService.DeleteEnvironmentAsync(envId, ct);
        return FromResult(result);
    }

    [HttpPost("test-environments/{envId:long}/builds")]
    public async Task<IActionResult> DeployBuild(
        long envId,
        [FromBody] DeployTestBuildRequest request,
        CancellationToken ct)
    {
        var result = await _testingService.DeployBuildAsync(envId, request, ct);
        return FromResult(result);
    }

    [HttpPost("test-environments/{envId:long}/regression")]
    public async Task<IActionResult> TriggerRegression(
        long envId,
        [FromBody] TriggerRegressionRequest? request,
        CancellationToken ct)
    {
        var result = await _testingService.TriggerRegressionAsync(envId, request ?? new TriggerRegressionRequest(), ct);
        return FromResult(result);
    }

    // --- Modules & Sections ---

    [HttpPost("test-plans/{planId:long}/modules")]
    public async Task<IActionResult> CreateModule(
        long planId,
        [FromBody] CreateTestModuleRequest request,
        CancellationToken ct)
    {
        var result = await _testingService.CreateModuleAsync(planId, request, ct);
        return FromResult(result);
    }

    [HttpPatch("test-modules/{moduleId:long}")]
    public async Task<IActionResult> UpdateModule(
        long moduleId,
        [FromBody] UpdateTestModuleRequest request,
        CancellationToken ct)
    {
        var result = await _testingService.UpdateModuleAsync(moduleId, request, ct);
        return FromResult(result);
    }

    [HttpDelete("test-modules/{moduleId:long}")]
    public async Task<IActionResult> DeleteModule(long moduleId, CancellationToken ct)
    {
        var result = await _testingService.DeleteModuleAsync(moduleId, ct);
        return FromResult(result);
    }

    [HttpPost("test-modules/{moduleId:long}/sections")]
    public async Task<IActionResult> CreateSection(
        long moduleId,
        [FromBody] CreateTestSectionRequest request,
        CancellationToken ct)
    {
        var result = await _testingService.CreateSectionAsync(moduleId, request, ct);
        return FromResult(result);
    }

    [HttpPatch("test-sections/{sectionId:long}")]
    public async Task<IActionResult> UpdateSection(
        long sectionId,
        [FromBody] UpdateTestSectionRequest request,
        CancellationToken ct)
    {
        var result = await _testingService.UpdateSectionAsync(sectionId, request, ct);
        return FromResult(result);
    }

    [HttpDelete("test-sections/{sectionId:long}")]
    public async Task<IActionResult> DeleteSection(long sectionId, CancellationToken ct)
    {
        var result = await _testingService.DeleteSectionAsync(sectionId, ct);
        return FromResult(result);
    }

    // --- Assignments & Layout ---

    [HttpPut("test-plans/{planId:long}/assignments")]
    public async Task<IActionResult> SetAssignment(
        long planId,
        [FromBody] SetTestAssignmentRequest request,
        CancellationToken ct)
    {
        var result = await _testingService.SetAssignmentAsync(planId, request, ct);
        return FromResult(result);
    }

    [HttpPut("test-plans/{planId:long}/sheet-layout")]
    [HttpPut("test-plans/{planId:long}/layout")]
    public async Task<IActionResult> SaveSheetLayout(
        long planId,
        [FromBody] SheetLayoutDto request,
        CancellationToken ct)
    {
        var result = await _testingService.SaveSheetLayoutAsync(planId, request, ct);
        return FromResult(result);
    }

    // --- Test Cases ---

    [HttpGet("projects/{projectId:long}/testcases")]
    public async Task<IActionResult> ListProjectTestCases(
        long projectId,
        [FromQuery] ListTestCasesQuery query,
        CancellationToken ct)
    {
        var result = await _testingService.ListProjectTestCasesAsync(projectId, query ?? new ListTestCasesQuery(), ct);
        return FromResult(result);
    }

    [HttpPost("projects/{projectId:long}/testcases")]
    public async Task<IActionResult> CreateProjectTestCase(
        long projectId,
        [FromBody] CreateTestCaseRequest request,
        CancellationToken ct)
    {
        var result = await _testingService.CreateProjectTestCaseAsync(projectId, request, ct);
        return FromResult(result);
    }

    [HttpGet("testcases/{testCaseId:long}")]
    public async Task<IActionResult> GetTestCase(long testCaseId, CancellationToken ct)
    {
        var result = await _testingService.GetTestCaseAsync(testCaseId, ct);
        return FromResult(result);
    }

    [HttpPost("test-plans/{planId:long}/testcases")]
    public async Task<IActionResult> CreateTestCase(
        long planId,
        [FromBody] CreateTestCaseRequest request,
        CancellationToken ct)
    {
        var result = await _testingService.CreateTestCaseAsync(planId, request, ct);
        return FromResult(result);
    }

    [HttpPatch("testcases/{testCaseId:long}")]
    public async Task<IActionResult> UpdateTestCase(
        long testCaseId,
        [FromBody] UpdateTestCaseRequest request,
        CancellationToken ct)
    {
        var result = await _testingService.UpdateTestCaseAsync(testCaseId, request, ct);
        return FromResult(result);
    }

    [HttpPost("test-plans/{planId:long}/testcases/bulk-update")]
    [HttpPost("test-plans/{planId:long}/testcases/bulk")]
    public async Task<IActionResult> BulkUpdateTestCases(
        long planId,
        [FromBody] BulkUpdateTestCasesRequest request,
        CancellationToken ct)
    {
        var result = await _testingService.BulkUpdateTestCasesAsync(planId, request, ct);
        return FromResult(result);
    }

    [HttpDelete("testcases/{testCaseId:long}")]
    public async Task<IActionResult> DeleteTestCase(long testCaseId, CancellationToken ct)
    {
        var result = await _testingService.DeleteTestCaseAsync(testCaseId, ct);
        return FromResult(result);
    }

    // --- Execution Results & Bugs ---

    [HttpPost("testcases/{testCaseId:long}/results")]
    public async Task<IActionResult> RecordSingleResult(
        long testCaseId,
        [FromBody] RecordSingleResultRequest request,
        CancellationToken ct)
    {
        var result = await _testingService.RecordSingleResultAsync(testCaseId, request, ct);
        return FromResult(result);
    }

    [HttpPost("test-plans/{planId:long}/results")]
    public async Task<IActionResult> RecordResults(
        long planId,
        [FromBody] RecordTestResultsRequest request,
        CancellationToken ct)
    {
        var result = await _testingService.RecordResultsAsync(planId, request, ct);
        return FromResult(result);
    }

    [HttpPost("test-plans/{planId:long}/bugs")]
    public async Task<IActionResult> ReportBug(
        long planId,
        [FromBody] ReportTestBugRequest request,
        CancellationToken ct)
    {
        var result = await _testingService.ReportBugAsync(planId, request, ct);
        return FromResult(result);
    }

    [HttpPost("test-plans/{planId:long}/bugs/{issueId:long}/links")]
    public async Task<IActionResult> LinkBug(
        long planId,
        long issueId,
        [FromBody] LinkTestBugRequest request,
        CancellationToken ct)
    {
        var result = await _testingService.LinkBugAsync(planId, issueId, request, ct);
        return FromResult(result);
    }

    // --- Handover & Reopen ---

    [HttpPost("test-plans/{planId:long}/handover")]
    public async Task<IActionResult> HandoverPlan(
        long planId,
        [FromBody] HandoverTestPlanRequest? request,
        CancellationToken ct)
    {
        var result = await _testingService.HandoverPlanAsync(planId, request ?? new HandoverTestPlanRequest(), ct);
        return FromResult(result);
    }

    [HttpPost("test-plans/{planId:long}/reopen")]
    public async Task<IActionResult> ReopenPlan(long planId, CancellationToken ct)
    {
        var result = await _testingService.ReopenPlanAsync(planId, ct);
        return FromResult(result);
    }

    // --- Activities, Traceability & AI ---

    [HttpGet("test-plans/{planId:long}/activity")]
    [HttpGet("test-plans/{planId:long}/activities")]
    public async Task<IActionResult> ListActivities(
        long planId,
        [FromQuery] ListActivitiesQuery query,
        CancellationToken ct)
    {
        var result = await _testingService.ListActivitiesAsync(planId, query ?? new ListActivitiesQuery(), ct);
        return FromResult(result);
    }

    [HttpGet("test-plans/{planId:long}/traceability")]
    public async Task<IActionResult> GetTraceability(
        long planId,
        [FromQuery] long? envId,
        CancellationToken ct)
    {
        var result = await _testingService.GetTraceabilityAsync(planId, envId, ct);
        return FromResult(result);
    }

    [HttpPost("projects/{projectId:long}/testcases/suggest")]
    public async Task<IActionResult> SuggestTestCases(
        long projectId,
        [FromBody] SuggestTestCasesRequest request,
        CancellationToken ct)
    {
        var result = await _testingService.SuggestTestCasesAsync(projectId, request, ct);
        return FromResult(result);
    }

    [HttpPost("test-plans/{planId:long}/testcases/ai-apply")]
    [HttpPost("test-plans/{planId:long}/ai-apply")]
    public async Task<IActionResult> AiApplyTestCases(
        long planId,
        [FromBody] AiApplyTestCasesRequest request,
        CancellationToken ct)
    {
        var result = await _testingService.AiApplyTestCasesAsync(planId, request, ct);
        return FromResult(result);
    }
}
