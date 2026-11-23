using Microsoft.AspNetCore.Http;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class TransitionIssueAsyncTests : KanbanServiceTestBase
{
    private void SetupNonAdminPermissions(long userId = 1, long projectId = 100)
    {
        PermissionServiceMock
            .Setup(p => p.GetProjectPermissionsAsync(userId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { PermissionKeys.IssueView, PermissionKeys.IssueTransition });
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: Valid transition to DONE updates status, sets ResolvedAt, computes tail rank, and logs activity.
    [Fact]
    public async Task TransitionIssueAsync_ToDoneStatus_UpdatesStatusSetsResolvedAtAndLogsActivity()
    {
        var (_, _, todo, done) = SeedWorkflow();
        var issue = SeedIssue(issueId: 200, statusId: todo.Id);
        SeedTransition(transitionId: 600, fromStatusId: todo.Id, toStatusId: done.Id);

        var result = await KanbanService.TransitionIssueAsync(issue.Id, new TransitionIssueRequest
        {
            ToStatusId = done.Id
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);

        var updated = Db.Issues.Single(i => i.Id == issue.Id);
        Assert.Equal(done.Id, updated.StatusId);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, updated.ResolvedAt);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, updated.UpdatedAt);
        Assert.Equal("00001000", updated.Rank);

        var activity = Assert.Single(Db.IssueActivities.Where(a => a.IssueId == issue.Id));
        Assert.Equal(IssueActivityActions.Transitioned, activity.Action);
        Assert.Equal("To do → Done", activity.Note);
    }

    // UTC002: Transitioning from DONE back to a non-DONE status clears ResolvedAt.
    [Fact]
    public async Task TransitionIssueAsync_FromDoneToNonDoneStatus_ClearsResolvedAt()
    {
        var (_, _, todo, done) = SeedWorkflow();
        var issue = SeedIssue(issueId: 200, statusId: done.Id);
        issue.ResolvedAt = Clock.GetUtcNow().UtcDateTime.AddHours(-2);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        SeedTransition(transitionId: 600, fromStatusId: done.Id, toStatusId: todo.Id);

        var result = await KanbanService.TransitionIssueAsync(issue.Id, new TransitionIssueRequest
        {
            ToStatusId = todo.Id
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var updated = Db.Issues.Single(i => i.Id == issue.Id);
        Assert.Equal(todo.Id, updated.StatusId);
        Assert.Null(updated.ResolvedAt);
    }

    // UTC003: Non-admin user whose project role is in AllowedRoles can execute the transition.
    [Fact]
    public async Task TransitionIssueAsync_WhenMemberRoleIsInAllowedRoles_Succeeds()
    {
        var (_, _, todo, done) = SeedWorkflow();
        var issue = SeedIssue(issueId: 200, statusId: todo.Id);
        SetupNonAdminPermissions(userId: 1, projectId: 100);
        SeedProjectMember(userId: 1, roleId: 52, projectId: 100);
        SeedTransition(transitionId: 600, fromStatusId: todo.Id, toStatusId: done.Id, allowedRoleIds: new[] { 52L });

        var result = await KanbanService.TransitionIssueAsync(issue.Id, new TransitionIssueRequest
        {
            ToStatusId = done.Id
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(done.Id, Db.Issues.Single(i => i.Id == issue.Id).StatusId);
    }

    // UTC004: Workflow admin bypasses AllowedRoles restriction even without a matching ProjectMember role.
    [Fact]
    public async Task TransitionIssueAsync_WhenUserHasWorkflowManagePermission_BypassesAllowedRoles()
    {
        var (_, _, todo, done) = SeedWorkflow();
        var issue = SeedIssue(issueId: 200, statusId: todo.Id);
        SeedTransition(transitionId: 600, fromStatusId: todo.Id, toStatusId: done.Id, allowedRoleIds: new[] { 99L });

        var result = await KanbanService.TransitionIssueAsync(issue.Id, new TransitionIssueRequest
        {
            ToStatusId = done.Id
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(done.Id, Db.Issues.Single(i => i.Id == issue.Id).StatusId);
    }

    // UTC005: Transition with required comment persists trimmed IssueComment along with status change.
    [Fact]
    public async Task TransitionIssueAsync_WithComment_SavesCommentAndTransitionsIssue()
    {
        var (_, _, todo, done) = SeedWorkflow();
        var issue = SeedIssue(issueId: 200, statusId: todo.Id);
        SeedTransition(
            transitionId: 600,
            fromStatusId: todo.Id,
            toStatusId: done.Id,
            requireComment: true,
            commentHint: "Provide resolution summary");

        var result = await KanbanService.TransitionIssueAsync(issue.Id, new TransitionIssueRequest
        {
            ToStatusId = done.Id,
            Comment = "  Fixed root cause and verified.  "
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var comment = Assert.Single(Db.IssueComments.Where(c => c.IssueId == issue.Id));
        Assert.Equal("Fixed root cause and verified.", comment.Body);
        Assert.Equal(1, comment.AuthorId);
    }

    // UTC006: Transition with all conditions satisfied (assignee_required, points_required, subtasks_done) succeeds.
    [Theory]
    [InlineData("assignee_required")]
    [InlineData("points_required")]
    [InlineData("subtasks_done")]
    public async Task TransitionIssueAsync_WhenTransitionConditionIsMet_Succeeds(string condition)
    {
        var (_, _, todo, done) = SeedWorkflow();
        var issue = SeedIssue(issueId: 200, statusId: todo.Id);
        issue.AssigneeId = 1;
        issue.Points = 3;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var subtask = SeedIssue(issueId: 201, statusId: done.Id, title: "Completed subtask");
        subtask.ParentId = issue.Id;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        SeedTransition(
            transitionId: 600,
            fromStatusId: todo.Id,
            toStatusId: done.Id,
            condition: condition);

        var result = await KanbanService.TransitionIssueAsync(issue.Id, new TransitionIssueRequest
        {
            ToStatusId = done.Id
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(done.Id, Db.Issues.Single(i => i.Id == issue.Id).StatusId);
    }

    // UTC007: Transition with explicit Rank (BeforeIssueId / AfterIssueId) or RankAfterIssueId positions issue accurately.
    [Fact]
    public async Task TransitionIssueAsync_WithExplicitRankPosition_RebalancesTargetColumnRanks()
    {
        var (_, _, todo, done) = SeedWorkflow();
        var existingDone1 = SeedIssue(issueId: 201, statusId: done.Id, title: "Done 1");
        existingDone1.Rank = "00001000";
        var existingDone2 = SeedIssue(issueId: 202, statusId: done.Id, title: "Done 2");
        existingDone2.Rank = "00002000";
        var movingIssue = SeedIssue(issueId: 200, statusId: todo.Id, title: "Moving");
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        SeedTransition(transitionId: 600, fromStatusId: todo.Id, toStatusId: done.Id);

        var result = await KanbanService.TransitionIssueAsync(movingIssue.Id, new TransitionIssueRequest
        {
            ToStatusId = done.Id,
            Rank = new IssueRankPositionInputDto { BeforeIssueId = existingDone2.Id }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("00001000", Db.Issues.Single(i => i.Id == existingDone1.Id).Rank);
        Assert.Equal("00002000", Db.Issues.Single(i => i.Id == movingIssue.Id).Rank);
        Assert.Equal("00003000", Db.Issues.Single(i => i.Id == existingDone2.Id).Rank);
    }

    // UTC008: Transitioning to the same status is a no-op and returns current issue detail without adding activity.
    [Fact]
    public async Task TransitionIssueAsync_WhenToStatusMatchesCurrentStatus_ReturnsDetailWithoutActivity()
    {
        var (_, _, todo, _) = SeedWorkflow();
        var issue = SeedIssue(issueId: 200, statusId: todo.Id);

        var result = await KanbanService.TransitionIssueAsync(issue.Id, new TransitionIssueRequest
        {
            ToStatusId = todo.Id
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Empty(Db.IssueActivities.Where(a => a.IssueId == issue.Id));
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC009: Transitioning a non-existent or soft-deleted issue returns NotFound.
    [Theory]
    [InlineData(false)] // Missing issue
    [InlineData(true)]  // Soft-deleted issue
    public async Task TransitionIssueAsync_WhenIssueMissingOrDeleted_ReturnsNotFound(bool seedDeleted)
    {
        var (_, _, todo, done) = SeedWorkflow();
        long targetId = 9999;
        if (seedDeleted)
        {
            var deleted = SeedIssue(issueId: 200, statusId: todo.Id);
            deleted.IsDeleted = true;
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
            targetId = deleted.Id;
        }

        var result = await KanbanService.TransitionIssueAsync(targetId, new TransitionIssueRequest
        {
            ToStatusId = done.Id
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("ISSUE_NOT_FOUND", result.ErrorCode);
    }

    // UTC010: Caller lacking IssueTransition permission returns Forbidden.
    [Fact]
    public async Task TransitionIssueAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        var (_, _, todo, done) = SeedWorkflow();
        var issue = SeedIssue(issueId: 200, statusId: todo.Id);

        AccessGuardMock
            .Setup(g => g.EnsureProjectPermissionAsync(100, PermissionKeys.IssueTransition, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Forbidden("FORBIDDEN", "Permission denied."));

        var result = await KanbanService.TransitionIssueAsync(issue.Id, new TransitionIssueRequest
        {
            ToStatusId = done.Id
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    // UTC011: Target status missing, inactive, or in another project returns NotFound.
    [Fact]
    public async Task TransitionIssueAsync_WhenTargetStatusMissingOrInactive_ReturnsNotFound()
    {
        var (_, _, todo, done) = SeedWorkflow();
        var issue = SeedIssue(issueId: 200, statusId: todo.Id);
        done.IsActive = false;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.TransitionIssueAsync(issue.Id, new TransitionIssueRequest
        {
            ToStatusId = done.Id
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("STATUS_NOT_FOUND", result.ErrorCode);
    }

    // UTC012: Disallowed workflow transition (no matching WorkflowTransition) returns Conflict.
    [Fact]
    public async Task TransitionIssueAsync_WhenTransitionRuleDoesNotExist_ReturnsConflict()
    {
        var (_, _, todo, done) = SeedWorkflow();
        var issue = SeedIssue(issueId: 200, statusId: todo.Id);

        var result = await KanbanService.TransitionIssueAsync(issue.Id, new TransitionIssueRequest
        {
            ToStatusId = done.Id
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("TRANSITION_NOT_ALLOWED", result.ErrorCode);
    }

    // UTC013: Non-admin user whose role is not in AllowedRoles returns Forbidden.
    [Fact]
    public async Task TransitionIssueAsync_WhenUserRoleIsNotAllowed_ReturnsForbidden()
    {
        var (_, _, todo, done) = SeedWorkflow();
        var issue = SeedIssue(issueId: 200, statusId: todo.Id);
        SetupNonAdminPermissions(userId: 1, projectId: 100);
        SeedProjectMember(userId: 1, roleId: 52, projectId: 100);
        SeedTransition(transitionId: 600, fromStatusId: todo.Id, toStatusId: done.Id, allowedRoleIds: new[] { 99L });

        var result = await KanbanService.TransitionIssueAsync(issue.Id, new TransitionIssueRequest
        {
            ToStatusId = done.Id
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("TRANSITION_ROLE_FORBIDDEN", result.ErrorCode);
    }

    // UTC014: Missing or whitespace comment when RequireComment is true returns BadRequest with CommentHint.
    [Fact]
    public async Task TransitionIssueAsync_WhenCommentRequiredButMissing_ReturnsBadRequest()
    {
        var (_, _, todo, done) = SeedWorkflow();
        var issue = SeedIssue(issueId: 200, statusId: todo.Id);
        SeedTransition(
            transitionId: 600,
            fromStatusId: todo.Id,
            toStatusId: done.Id,
            requireComment: true,
            commentHint: "Vui lòng nhập lý do hoàn thành.");

        var result = await KanbanService.TransitionIssueAsync(issue.Id, new TransitionIssueRequest
        {
            ToStatusId = done.Id,
            Comment = "   "
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("TRANSITION_COMMENT_REQUIRED", result.ErrorCode);
        Assert.Equal("Vui lòng nhập lý do hoàn thành.", result.ErrorMessage);
    }

    // UTC015: Condition "assignee_required" fails when issue has no AssigneeId.
    [Fact]
    public async Task TransitionIssueAsync_WhenAssigneeRequiredAndUnassigned_ReturnsConflict()
    {
        var (_, _, todo, done) = SeedWorkflow();
        var issue = SeedIssue(issueId: 200, statusId: todo.Id);
        issue.AssigneeId = null;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        SeedTransition(transitionId: 600, fromStatusId: todo.Id, toStatusId: done.Id, condition: "assignee_required");

        var result = await KanbanService.TransitionIssueAsync(issue.Id, new TransitionIssueRequest
        {
            ToStatusId = done.Id
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("TRANSITION_CONDITION_FAILED", result.ErrorCode);
    }

    // UTC016: Condition "points_required" fails when Points is null or <= 0.
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task TransitionIssueAsync_WhenPointsRequiredAndMissingOrZero_ReturnsConflict(int? points)
    {
        var (_, _, todo, done) = SeedWorkflow();
        var issue = SeedIssue(issueId: 200, statusId: todo.Id);
        issue.Points = points;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        SeedTransition(transitionId: 600, fromStatusId: todo.Id, toStatusId: done.Id, condition: "points_required");

        var result = await KanbanService.TransitionIssueAsync(issue.Id, new TransitionIssueRequest
        {
            ToStatusId = done.Id
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("TRANSITION_CONDITION_FAILED", result.ErrorCode);
    }

    // UTC017: Condition "subtasks_done" fails when at least one active subtask is not DONE.
    [Fact]
    public async Task TransitionIssueAsync_WhenSubtasksDoneRequiredAndSubtaskOpen_ReturnsConflict()
    {
        var (_, _, todo, done) = SeedWorkflow();
        var issue = SeedIssue(issueId: 200, statusId: todo.Id);
        var openSubtask = SeedIssue(issueId: 201, statusId: todo.Id, title: "Open subtask");
        openSubtask.ParentId = issue.Id;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        SeedTransition(transitionId: 600, fromStatusId: todo.Id, toStatusId: done.Id, condition: "subtasks_done");

        var result = await KanbanService.TransitionIssueAsync(issue.Id, new TransitionIssueRequest
        {
            ToStatusId = done.Id
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("TRANSITION_CONDITION_FAILED", result.ErrorCode);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC018: Condition "points_required" succeeds at the lower valid boundary Points = 1.
    [Fact]
    public async Task TransitionIssueAsync_WhenPointsRequiredAndPointsIs1_Succeeds()
    {
        var (_, _, todo, done) = SeedWorkflow();
        var issue = SeedIssue(issueId: 200, statusId: todo.Id);
        issue.Points = 1;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        SeedTransition(transitionId: 600, fromStatusId: todo.Id, toStatusId: done.Id, condition: "points_required");

        var result = await KanbanService.TransitionIssueAsync(issue.Id, new TransitionIssueRequest
        {
            ToStatusId = done.Id
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(done.Id, Db.Issues.Single(i => i.Id == issue.Id).StatusId);
    }

    // UTC019: Condition "subtasks_done" ignores soft-deleted open subtasks and succeeds.
    [Fact]
    public async Task TransitionIssueAsync_WhenSubtasksDoneRequiredAndOpenSubtaskIsDeleted_Succeeds()
    {
        var (_, _, todo, done) = SeedWorkflow();
        var issue = SeedIssue(issueId: 200, statusId: todo.Id);
        var deletedOpenSubtask = SeedIssue(issueId: 201, statusId: todo.Id, title: "Deleted open subtask");
        deletedOpenSubtask.ParentId = issue.Id;
        deletedOpenSubtask.IsDeleted = true;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        SeedTransition(transitionId: 600, fromStatusId: todo.Id, toStatusId: done.Id, condition: "subtasks_done");

        var result = await KanbanService.TransitionIssueAsync(issue.Id, new TransitionIssueRequest
        {
            ToStatusId = done.Id
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(done.Id, Db.Issues.Single(i => i.Id == issue.Id).StatusId);
    }

    // UTC020: Legacy RankAfterIssueId places transitioned issue immediately after the target sibling.
    [Fact]
    public async Task TransitionIssueAsync_WithRankAfterIssueId_PlacesIssueAfterTargetSibling()
    {
        var (_, _, todo, done) = SeedWorkflow();
        var firstDone = SeedIssue(issueId: 201, statusId: done.Id, title: "First Done");
        firstDone.Rank = "00001000";
        var secondDone = SeedIssue(issueId: 202, statusId: done.Id, title: "Second Done");
        secondDone.Rank = "00002000";
        var movingIssue = SeedIssue(issueId: 200, statusId: todo.Id, title: "Moving");
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        SeedTransition(transitionId: 600, fromStatusId: todo.Id, toStatusId: done.Id);

        var result = await KanbanService.TransitionIssueAsync(movingIssue.Id, new TransitionIssueRequest
        {
            ToStatusId = done.Id,
            RankAfterIssueId = firstDone.Id
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("00001000", Db.Issues.Single(i => i.Id == firstDone.Id).Rank);
        Assert.Equal("00002000", Db.Issues.Single(i => i.Id == movingIssue.Id).Rank);
        Assert.Equal("00003000", Db.Issues.Single(i => i.Id == secondDone.Id).Rank);
    }

    // UTC021: RequireComment = true succeeds with a minimal 1-character non-whitespace comment.
    [Fact]
    public async Task TransitionIssueAsync_WithSingleCharacterCommentWhenRequired_Succeeds()
    {
        var (_, _, todo, done) = SeedWorkflow();
        var issue = SeedIssue(issueId: 200, statusId: todo.Id);
        SeedTransition(transitionId: 600, fromStatusId: todo.Id, toStatusId: done.Id, requireComment: true);

        var result = await KanbanService.TransitionIssueAsync(issue.Id, new TransitionIssueRequest
        {
            ToStatusId = done.Id,
            Comment = " " + "X" + " "
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var comment = Assert.Single(Db.IssueComments.Where(c => c.IssueId == issue.Id));
        Assert.Equal("X", comment.Body);
    }
}