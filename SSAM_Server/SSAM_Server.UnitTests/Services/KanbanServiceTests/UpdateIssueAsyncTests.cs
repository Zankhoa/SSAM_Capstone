using Microsoft.AspNetCore.Http;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class UpdateIssueAsyncTests : KanbanServiceTestBase
{
    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: Updating scalar fields normalizes values, updates timestamp, and logs a single Updated activity.
    [Fact]
    public async Task UpdateIssueAsync_WithValidScalarFields_UpdatesIssueAndRecordsActivity()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);
        var dept = SeedDepartment(departmentId: 50);

        var result = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest
        {
            Title = "  Updated Issue Title  ",
            Description = "Updated description content",
            Type = "bug",
            Priority = "high",
            Points = 8,
            DepartmentId = dept.Id,
            Labels = new List<string> { "urgent", "api" },
            DueDate = "2026-12-15"
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);

        var updated = Db.Issues.Single(i => i.Id == issue.Id);
        Assert.Equal("Updated Issue Title", updated.Title);
        Assert.Equal("Updated description content", updated.Description);
        Assert.Equal(IssueTypes.Bug, updated.Type);
        Assert.Equal(IssuePriorities.High, updated.Priority);
        Assert.Equal(8, updated.Points);
        Assert.Equal(dept.Id, updated.DepartmentId);
        Assert.Equal("[\"urgent\",\"api\"]", updated.Labels);
        Assert.Equal(new DateOnly(2026, 12, 15), updated.DueDate);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, updated.UpdatedAt);

        var activity = Assert.Single(Db.IssueActivities.Where(a => a.IssueId == issue.Id));
        Assert.Equal(IssueActivityActions.Updated, activity.Action);
    }

    // UTC002: Assigning a valid user updates AssigneeId and records an Assigned activity.
    [Fact]
    public async Task UpdateIssueAsync_WhenAssigningValidUser_UpdatesAssigneeAndLogsActivity()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);
        var (_, assignee) = SeedUser(userId: 2, fullName: "Nguyễn Văn B");

        var result = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest
        {
            AssigneeId = assignee.Id
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(assignee.Id, Db.Issues.Single(i => i.Id == issue.Id).AssigneeId);

        var activity = Assert.Single(Db.IssueActivities.Where(a => a.IssueId == issue.Id));
        Assert.Equal(IssueActivityActions.Assigned, activity.Action);
        Assert.Equal("Giao cho Nguyễn Văn B", activity.Note);
    }

    // UTC003: Setting AssigneeId to null unassigns the issue and logs an unassignment activity.
    [Fact]
    public async Task UpdateIssueAsync_WhenUnassigningUser_ClearsAssigneeAndLogsActivity()
    {
        SeedWorkflow();
        var (_, assignee) = SeedUser(userId: 2);
        var issue = SeedIssue(issueId: 200);
        issue.AssigneeId = assignee.Id;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest
        {
            AssigneeId = null
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(Db.Issues.Single(i => i.Id == issue.Id).AssigneeId);

        var activity = Assert.Single(Db.IssueActivities.Where(a => a.IssueId == issue.Id));
        Assert.Equal(IssueActivityActions.Assigned, activity.Action);
        Assert.Equal("Bỏ phân công người thực hiện", activity.Note);
    }

    // UTC004: Assignee-only update succeeds when caller has IssueAssign permission without IssueUpdate.
    [Fact]
    public async Task UpdateIssueAsync_WithAssigneeOnlyAndIssueAssignPermission_SucceedsWithoutIssueUpdate()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);
        var (_, assignee) = SeedUser(userId: 2);

        AccessGuardMock
            .Setup(g => g.EnsureProjectPermissionAsync(100, PermissionKeys.IssueUpdate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Forbidden("FORBIDDEN", "No update permission."));
        AccessGuardMock
            .Setup(g => g.EnsureProjectPermissionAsync(100, PermissionKeys.IssueAssign, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Ok());

        var result = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest
        {
            AssigneeId = assignee.Id
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(assignee.Id, Db.Issues.Single(i => i.Id == issue.Id).AssigneeId);
        AccessGuardMock.Verify(
            g => g.EnsureProjectPermissionAsync(100, PermissionKeys.IssueAssign, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // UTC005: Assignee-only update falls back to IssueUpdate permission when IssueAssign is denied.
    [Fact]
    public async Task UpdateIssueAsync_WithAssigneeOnlyWhenIssueAssignDenied_FallsBackToIssueUpdate()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);
        var (_, assignee) = SeedUser(userId: 2);

        AccessGuardMock
            .Setup(g => g.EnsureProjectPermissionAsync(100, PermissionKeys.IssueAssign, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Forbidden("FORBIDDEN", "No assign permission."));
        AccessGuardMock
            .Setup(g => g.EnsureProjectPermissionAsync(100, PermissionKeys.IssueUpdate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Ok());

        var result = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest
        {
            AssigneeId = assignee.Id
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(assignee.Id, Db.Issues.Single(i => i.Id == issue.Id).AssigneeId);
        AccessGuardMock.Verify(
            g => g.EnsureProjectPermissionAsync(100, PermissionKeys.IssueUpdate, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // UTC006: Moving an issue into a sprint and back to backlog updates SprintId and logs SprintChanged activities.
    [Fact]
    public async Task UpdateIssueAsync_WhenChangingSprintAndMovingToBacklog_UpdatesSprintAndLogsActivities()
    {
        SeedWorkflow();
        var sprint = SeedSprint(sprintId: 400);
        var issue = SeedIssue(issueId: 200);

        var moveToSprint = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest
        {
            SprintId = sprint.Id
        }, TestContext.Current.CancellationToken);

        Assert.True(moveToSprint.IsSuccess);
        Assert.Equal(sprint.Id, Db.Issues.Single(i => i.Id == issue.Id).SprintId);

        var moveToBacklog = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest
        {
            SprintId = null
        }, TestContext.Current.CancellationToken);

        Assert.True(moveToBacklog.IsSuccess);
        Assert.Null(Db.Issues.Single(i => i.Id == issue.Id).SprintId);

        var activities = Db.IssueActivities.Where(a => a.IssueId == issue.Id).OrderBy(a => a.Id).ToList();
        Assert.Equal(2, activities.Count);
        Assert.Equal("Chuyển vào Sprint 1", activities[0].Note);
        Assert.Equal("Chuyển về Backlog", activities[1].Note);
    }

    // UTC007: Explicitly specifying null/empty for Points, DueDate, and DepartmentId clears those fields.
    [Fact]
    public async Task UpdateIssueAsync_WhenClearingNullableFields_SetsFieldsToNull()
    {
        SeedWorkflow();
        var dept = SeedDepartment(departmentId: 50);
        var issue = SeedIssue(issueId: 200);
        issue.Points = 5;
        issue.DepartmentId = dept.Id;
        issue.DueDate = new DateOnly(2026, 12, 1);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest
        {
            Points = null,
            DepartmentId = null,
            DueDate = "   "
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var updated = Db.Issues.Single(i => i.Id == issue.Id);
        Assert.Null(updated.Points);
        Assert.Null(updated.DepartmentId);
        Assert.Null(updated.DueDate);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC008: Updating a non-existent or soft-deleted issue returns NotFound.
    [Theory]
    [InlineData(false)] // Issue does not exist
    [InlineData(true)]  // Issue is soft-deleted (IsDeleted = true)
    public async Task UpdateIssueAsync_WhenIssueMissingOrDeleted_ReturnsNotFound(bool seedDeleted)
    {
        SeedWorkflow();
        long targetId = 9999;
        if (seedDeleted)
        {
            var deleted = SeedIssue(issueId: 200);
            deleted.IsDeleted = true;
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
            targetId = deleted.Id;
        }

        var result = await KanbanService.UpdateIssueAsync(targetId, new UpdateIssueRequest
        {
            Title = "New Title"
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("ISSUE_NOT_FOUND", result.ErrorCode);
    }

    // UTC009: Caller lacking IssueUpdate permission for general field edits returns Forbidden.
    [Fact]
    public async Task UpdateIssueAsync_WhenUpdatePermissionDenied_ReturnsForbidden()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);
        AccessGuardMock
            .Setup(g => g.EnsureProjectPermissionAsync(100, PermissionKeys.IssueUpdate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Forbidden("FORBIDDEN", "Permission denied."));

        var result = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest
        {
            Title = "Blocked Update"
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    // UTC010: Caller lacking both IssueAssign and IssueUpdate for assignee-only edit returns Forbidden.
    [Fact]
    public async Task UpdateIssueAsync_WhenBothAssignAndUpdatePermissionsDenied_ReturnsForbidden()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);
        AccessGuardMock
            .Setup(g => g.EnsureProjectPermissionAsync(100, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Forbidden("FORBIDDEN", "Permission denied."));

        var result = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest
        {
            AssigneeId = 2
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    // UTC011: Blank or whitespace-only Title returns ValidationFailure.
    [Fact]
    public async Task UpdateIssueAsync_WhenTitleIsBlank_ReturnsValidationError()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);

        var result = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest
        {
            Title = "   "
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("title", result.Errors!.Keys);
    }

    // UTC012: Invalid issue Type returns ValidationFailure.
    [Fact]
    public async Task UpdateIssueAsync_WhenTypeIsInvalid_ReturnsValidationError()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);

        var result = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest
        {
            Type = "EPIC_UNKNOWN"
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("type", result.Errors!.Keys);
    }

    // UTC013: Invalid Priority returns ValidationFailure.
    [Fact]
    public async Task UpdateIssueAsync_WhenPriorityIsInvalid_ReturnsValidationError()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);

        var result = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest
        {
            Priority = "CRITICAL_UNKNOWN"
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("priority", result.Errors!.Keys);
    }

    // UTC014: Malformed DueDate string returns ValidationFailure.
    [Fact]
    public async Task UpdateIssueAsync_WhenDueDateFormatIsInvalid_ReturnsValidationError()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);

        var result = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest
        {
            DueDate = "not-a-valid-date"
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("dueDate", result.Errors!.Keys);
    }

    // UTC015: Assigning a non-existent user returns NotFound.
    [Fact]
    public async Task UpdateIssueAsync_WhenAssigneeDoesNotExist_ReturnsNotFound()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);

        var result = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest
        {
            AssigneeId = 9999
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("USER_NOT_FOUND", result.ErrorCode);
    }

    // UTC016: Assigning a non-existent sprint or sprint from another project returns NotFound.
    [Fact]
    public async Task UpdateIssueAsync_WhenSprintNotInProject_ReturnsNotFound()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);
        var otherSprint = SeedSprint(sprintId: 401, projectId: 999);

        var result = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest
        {
            SprintId = otherSprint.Id
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("SPRINT_NOT_FOUND", result.ErrorCode);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC017: Title at lower (1 char) and upper (200 chars) boundaries succeeds.
    [Theory]
    [InlineData(1)]
    [InlineData(200)]
    public async Task UpdateIssueAsync_WithTitleAtValidBoundaries_ReturnsOk(int length)
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);
        var title = new string('T', length);

        var result = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest
        {
            Title = title
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(length, Db.Issues.Single(i => i.Id == issue.Id).Title.Length);
    }

    // UTC018: Title exceeding 200 characters (201 chars) returns ValidationFailure.
    [Fact]
    public async Task UpdateIssueAsync_WithTitleAboveMaximumLength_ReturnsValidationError()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);

        var result = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest
        {
            Title = new string('T', 201)
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("title", result.Errors!.Keys);
    }

    // UTC019: Description at 10000 chars succeeds while 10001 chars returns ValidationFailure.
    [Fact]
    public async Task UpdateIssueAsync_WithDescriptionBoundaries_Validates10000CharLimit()
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);

        var okResult = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest
        {
            Description = new string('D', 10000)
        }, TestContext.Current.CancellationToken);
        Assert.True(okResult.IsSuccess);
        Assert.Equal(10000, Db.Issues.Single(i => i.Id == issue.Id).Description.Length);

        var failResult = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest
        {
            Description = new string('D', 10001)
        }, TestContext.Current.CancellationToken);
        Assert.False(failResult.IsSuccess);
        Assert.Contains("description", failResult.Errors!.Keys);
    }

    // UTC020: Points at valid boundary endpoints (0 and 100) succeed.
    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task UpdateIssueAsync_WithPointsAtValidBoundaries_ReturnsOk(int points)
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);

        var result = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest
        {
            Points = points
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(points, Db.Issues.Single(i => i.Id == issue.Id).Points);
    }

    // UTC021: Points outside valid boundaries (-1 and 101) return ValidationFailure.
    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task UpdateIssueAsync_WithPointsOutsideBoundaries_ReturnsValidationError(int points)
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);

        var result = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest
        {
            Points = points
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("points", result.Errors!.Keys);
    }

    // UTC022: DueDate at ISO parsing boundaries ("0001-01-01" and "9999-12-31") succeeds.
    [Theory]
    [InlineData("0001-01-01", 1, 1, 1)]
    [InlineData("9999-12-31", 9999, 12, 31)]
    public async Task UpdateIssueAsync_WithDueDateBoundaries_ReturnsOk(string rawDate, int year, int month, int day)
    {
        SeedWorkflow();
        var issue = SeedIssue(issueId: 200);

        var result = await KanbanService.UpdateIssueAsync(issue.Id, new UpdateIssueRequest
        {
            DueDate = rawDate
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(year, month, day), Db.Issues.Single(i => i.Id == issue.Id).DueDate);
    }
}