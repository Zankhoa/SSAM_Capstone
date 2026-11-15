using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Testing;

namespace SSAM_Server.Interfaces.Services;

public interface ITestingService
{
    // --- Test Plans ---
    Task<ServiceResult<ItemsDto<TestPlanDto>>> GetProjectTestPlansAsync(long projectId, CancellationToken ct = default);
    Task<ServiceResult<TestPlanDto>> CreateTestPlanAsync(long projectId, CreateTestPlanRequest request, CancellationToken ct = default);
    Task<ServiceResult<TestPlanDto>> GetTestPlanAsync(long planId, CancellationToken ct = default);
    Task<ServiceResult<TestPlanDto>> UpdateTestPlanAsync(long planId, UpdateTestPlanRequest request, CancellationToken ct = default);
    Task<ServiceResult> DeleteTestPlanAsync(long planId, CancellationToken ct = default);

    // --- Environments & Builds ---
    Task<ServiceResult<TestEnvironmentDto>> CreateEnvironmentAsync(long planId, CreateTestEnvironmentRequest request, CancellationToken ct = default);
    Task<ServiceResult<TestEnvironmentDto>> UpdateEnvironmentAsync(long envId, UpdateTestEnvironmentRequest request, CancellationToken ct = default);
    Task<ServiceResult> DeleteEnvironmentAsync(long envId, CancellationToken ct = default);
    Task<ServiceResult<DeployTestBuildResponse>> DeployBuildAsync(long envId, DeployTestBuildRequest request, CancellationToken ct = default);
    Task<ServiceResult<TriggerRegressionResponse>> TriggerRegressionAsync(long envId, TriggerRegressionRequest request, CancellationToken ct = default);

    // --- Modules & Sections ---
    Task<ServiceResult<TestModuleDto>> CreateModuleAsync(long planId, CreateTestModuleRequest request, CancellationToken ct = default);
    Task<ServiceResult<TestModuleDto>> UpdateModuleAsync(long moduleId, UpdateTestModuleRequest request, CancellationToken ct = default);
    Task<ServiceResult> DeleteModuleAsync(long moduleId, CancellationToken ct = default);
    Task<ServiceResult<TestSectionDto>> CreateSectionAsync(long moduleId, CreateTestSectionRequest request, CancellationToken ct = default);
    Task<ServiceResult<TestSectionDto>> UpdateSectionAsync(long sectionId, UpdateTestSectionRequest request, CancellationToken ct = default);
    Task<ServiceResult> DeleteSectionAsync(long sectionId, CancellationToken ct = default);

    // --- Assignments & Layout ---
    Task<ServiceResult<ItemsDto<TestAssignmentDto>>> SetAssignmentAsync(long planId, SetTestAssignmentRequest request, CancellationToken ct = default);
    Task<ServiceResult<SheetLayoutDto>> SaveSheetLayoutAsync(long planId, SheetLayoutDto request, CancellationToken ct = default);

    // --- Test Cases ---
    Task<ServiceResult<ItemsDto<TestCaseDto>>> ListProjectTestCasesAsync(long projectId, ListTestCasesQuery query, CancellationToken ct = default);
    Task<ServiceResult<TestCaseDto>> GetTestCaseAsync(long testCaseId, CancellationToken ct = default);
    Task<ServiceResult<TestCaseDto>> CreateProjectTestCaseAsync(long projectId, CreateTestCaseRequest request, CancellationToken ct = default);
    Task<ServiceResult<TestCaseDto>> CreateTestCaseAsync(long planId, CreateTestCaseRequest request, CancellationToken ct = default);
    Task<ServiceResult<TestCaseDto>> UpdateTestCaseAsync(long testCaseId, UpdateTestCaseRequest request, CancellationToken ct = default);
    Task<ServiceResult<ItemsDto<TestCaseDto>>> BulkUpdateTestCasesAsync(long planId, BulkUpdateTestCasesRequest request, CancellationToken ct = default);
    Task<ServiceResult> DeleteTestCaseAsync(long testCaseId, CancellationToken ct = default);

    // --- Execution Results & Bugs ---
    Task<ServiceResult<RecordTestResultsResponse>> RecordSingleResultAsync(long testCaseId, RecordSingleResultRequest request, CancellationToken ct = default);
    Task<ServiceResult<RecordTestResultsResponse>> RecordResultsAsync(long planId, RecordTestResultsRequest request, CancellationToken ct = default);
    Task<ServiceResult<ReportTestBugResponse>> ReportBugAsync(long planId, ReportTestBugRequest request, CancellationToken ct = default);
    Task<ServiceResult<ItemsDto<TestCaseDto>>> LinkBugAsync(long planId, long issueId, LinkTestBugRequest request, CancellationToken ct = default);

    // --- Handover & Reopen ---
    Task<ServiceResult<TestPlanDto>> HandoverPlanAsync(long planId, HandoverTestPlanRequest request, CancellationToken ct = default);
    Task<ServiceResult<TestPlanDto>> ReopenPlanAsync(long planId, CancellationToken ct = default);

    // --- Activities, Traceability & AI ---
    Task<ServiceResult<ItemsDto<TestActivityDto>>> ListActivitiesAsync(long planId, ListActivitiesQuery query, CancellationToken ct = default);
    Task<ServiceResult<ItemsDto<TraceRowDto>>> GetTraceabilityAsync(long planId, long? envId, CancellationToken ct = default);
    Task<ServiceResult<ItemsDto<SuggestedTestCaseDto>>> SuggestTestCasesAsync(long projectId, SuggestTestCasesRequest request, CancellationToken ct = default);
    Task<ServiceResult<ItemsDto<TestCaseDto>>> AiApplyTestCasesAsync(long planId, AiApplyTestCasesRequest request, CancellationToken ct = default);
}
