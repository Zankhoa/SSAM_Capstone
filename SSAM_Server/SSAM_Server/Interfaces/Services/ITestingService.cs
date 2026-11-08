using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Testing;

namespace SSAM_Server.Services;

public interface ITestingService
{
    Task<ServiceResult<ItemsDto<TestTreeNodeDto>>> GetTestTreeAsync(long projectId, CancellationToken ct = default);
    Task<ServiceResult<TestFolderDto>> CreateTestFolderAsync(long projectId, CreateTestFolderRequest request, CancellationToken ct = default);
    Task<ServiceResult<ItemsDto<TestCaseSummaryDto>>> GetProjectTestCasesAsync(long projectId, TestCaseListQuery query, CancellationToken ct = default);
    Task<ServiceResult<TestCaseDetailDto>> CreateTestCaseAsync(long projectId, CreateTestCaseRequest request, CancellationToken ct = default);
    Task<ServiceResult<TestCaseDetailDto>> GetTestCaseDetailAsync(long testCaseId, CancellationToken ct = default);
    Task<ServiceResult<TestCaseDetailDto>> UpdateTestCaseAsync(long testCaseId, UpdateTestCaseRequest request, CancellationToken ct = default);
    Task<ServiceResult> DeleteTestCaseAsync(long testCaseId, CancellationToken ct = default);
    Task<ServiceResult<ItemsDto<SuggestedTestCaseDto>>> SuggestTestCasesAsync(long projectId, SuggestTestCasesRequest request, CancellationToken ct = default);
    Task<ServiceResult<ItemsDto<TestCycleDto>>> GetProjectTestCyclesAsync(long projectId, string? status, CancellationToken ct = default);
    Task<ServiceResult<TestCycleDto>> CreateTestCycleAsync(long projectId, CreateTestCycleRequest request, CancellationToken ct = default);
    Task<ServiceResult<TestCycleDto>> UpdateTestCycleAsync(long cycleId, UpdateTestCycleRequest request, CancellationToken ct = default);
    Task<ServiceResult<ItemsDto<TestRunDto>>> GetCycleRunsAsync(long cycleId, TestRunListQuery query, CancellationToken ct = default);
    Task<ServiceResult<TestRunDto>> UpdateTestRunAsync(long runId, UpdateTestRunRequest request, CancellationToken ct = default);
    Task<ServiceResult<TestRunDto>> RecordTestRunResultAsync(long runId, RecordTestRunResultRequest request, CancellationToken ct = default);
}
