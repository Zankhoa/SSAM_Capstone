using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Kanban;

namespace SSAM_Server.Interfaces.Services;

public interface IKanbanService
{
    Task<ServiceResult<BoardDto>> GetBoardAsync(long projectId, CancellationToken ct = default);
    Task<ServiceResult<BoardDto>> UpdateWipLimitsAsync(long projectId, UpdateWipLimitsRequest request, CancellationToken ct = default);
    Task<ServiceResult<ItemsDto<IssueSummaryDto>>> GetProjectIssuesAsync(long projectId, IssueListQuery query, CancellationToken ct = default);
    Task<ServiceResult<IssueDetailDto>> CreateIssueAsync(long projectId, CreateIssueRequest request, CancellationToken ct = default);
    Task<ServiceResult<IssueDetailDto>> GetIssueDetailAsync(long issueId, CancellationToken ct = default);
    Task<ServiceResult<IssueDetailDto>> UpdateIssueAsync(long issueId, UpdateIssueRequest request, CancellationToken ct = default);
    Task<ServiceResult> DeleteIssueAsync(long issueId, CancellationToken ct = default);
    Task<ServiceResult<IssueDetailDto>> TransitionIssueAsync(long issueId, TransitionIssueRequest request, CancellationToken ct = default);
    Task<ServiceResult<IssueSummaryDto>> RankIssueAsync(long issueId, RankIssueRequest request, CancellationToken ct = default);
    Task<ServiceResult<ItemsDto<IssueDetailDto>>> CreateIssuesFromRequirementsAsync(long projectId, CreateIssuesFromRequirementsRequest request, CancellationToken ct = default);
    Task<ServiceResult<IssueDetailDto>> UpdateIssueRequirementsAsync(long issueId, UpdateIssueRequirementsRequest request, CancellationToken ct = default);
    Task<ServiceResult<IssueDetailDto>> AcknowledgeIssueRequirementsAsync(long issueId, CancellationToken ct = default);
    Task<ServiceResult<ItemsDto<IssueCommentDto>>> GetIssueCommentsAsync(long issueId, CancellationToken ct = default);
    Task<ServiceResult<IssueCommentDto>> CreateIssueCommentAsync(long issueId, CreateIssueCommentRequest request, CancellationToken ct = default);
    Task<ServiceResult<ItemsDto<IssueActivityDto>>> GetIssueActivitiesAsync(long issueId, CancellationToken ct = default);
    Task<ServiceResult<ItemsDto<SprintDto>>> GetProjectSprintsAsync(long projectId, string? status, CancellationToken ct = default);
    Task<ServiceResult<SprintDto>> CreateSprintAsync(long projectId, CreateSprintRequest request, CancellationToken ct = default);
    Task<ServiceResult<SprintDto>> UpdateSprintAsync(long sprintId, UpdateSprintRequest request, CancellationToken ct = default);
    Task<ServiceResult<SprintDto>> StartSprintAsync(long sprintId, StartSprintRequest? request, CancellationToken ct = default);
    Task<ServiceResult<SprintDto>> CompleteSprintAsync(long sprintId, CompleteSprintRequest? request, CancellationToken ct = default);
}
