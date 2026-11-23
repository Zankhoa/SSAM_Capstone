using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class CompleteSprintAsyncTests : KanbanServiceTestBase
{
    private static JsonElement ParseJson(string rawJson)
    {
        using var doc = JsonDocument.Parse(rawJson);
        return doc.RootElement.Clone();
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: Completing an ACTIVE sprint with null request moves open issues to Backlog (SprintId = null), keeps DONE issues in sprint, records SprintChanged activities, and marks sprint COMPLETED.
    [Fact]
    public async Task CompleteSprintAsync_WithNullRequest_MovesOpenIssuesToBacklogAndCompletesSprint()
    {
        SeedWorkflow();
        var sprint = SeedSprint(sprintId: 400, status: SprintStatuses.Active, projectId: 100);

        var openIssue = SeedIssue(issueId: 201, statusId: 30, projectId: 100);
        openIssue.SprintId = sprint.Id;
        openIssue.Points = 3;

        var doneIssue = SeedIssue(issueId: 202, statusId: 31, projectId: 100);
        doneIssue.SprintId = sprint.Id;
        doneIssue.Points = 5;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.CompleteSprintAsync(sprint.Id, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);

        var dto = result.Data!;
        Assert.Equal(SprintStatuses.Completed, dto.Status);
        Assert.NotNull(dto.CompletedAt);
        Assert.Equal(1, dto.Stats.TotalIssues);
        Assert.Equal(1, dto.Stats.DoneIssues);
        Assert.Equal(5, dto.Stats.TotalPoints);
        Assert.Equal(5, dto.Stats.DonePoints);

        Assert.Null(Db.Issues.Single(i => i.Id == openIssue.Id).SprintId);
        Assert.Equal(sprint.Id, Db.Issues.Single(i => i.Id == doneIssue.Id).SprintId);

        var activity = Db.IssueActivities.Single(a => a.IssueId == openIssue.Id && a.Action == IssueActivityActions.SprintChanged);
        Assert.Contains("Backlog", activity.Note);
        Assert.DoesNotContain(Db.IssueActivities, a => a.IssueId == doneIssue.Id);
    }

    // UTC002: Completing an ACTIVE sprint with numeric MoveOpenIssuesTo moves open issues into the target PLANNED sprint in the same project.
    [Fact]
    public async Task CompleteSprintAsync_WithNumericTargetPlannedSprint_MovesOpenIssuesToTargetSprint()
    {
        SeedWorkflow();
        var activeSprint = SeedSprint(sprintId: 400, status: SprintStatuses.Active, projectId: 100);
        var nextSprint = SeedSprint(sprintId: 401, status: SprintStatuses.Planned, projectId: 100);
        nextSprint.Name = "Sprint 2";
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var openIssue = SeedIssue(issueId: 201, statusId: 30, projectId: 100);
        openIssue.SprintId = activeSprint.Id;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.CompleteSprintAsync(activeSprint.Id, new CompleteSprintRequest
        {
            MoveOpenIssuesTo = ParseJson("401")
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(SprintStatuses.Completed, result.Data!.Status);
        Assert.Equal(nextSprint.Id, Db.Issues.Single(i => i.Id == openIssue.Id).SprintId);

        var activity = Db.IssueActivities.Single(a => a.IssueId == openIssue.Id);
        Assert.Contains("Sprint 2", activity.Note);
    }

    // UTC003: Completing an ACTIVE sprint with string MoveOpenIssuesTo ("401" or "BACKLOG") parses string destination properly.
    [Theory]
    [InlineData("\"401\"", 401L, "Sprint 2")]
    [InlineData("\"backlog\"", null, "Backlog")]
    public async Task CompleteSprintAsync_WithStringMoveOpenIssuesTo_ParsesTargetCorrectly(
        string jsonValue,
        long? expectedSprintId,
        string expectedLabel)
    {
        SeedWorkflow();
        var activeSprint = SeedSprint(sprintId: 400, status: SprintStatuses.Active, projectId: 100);
        var nextSprint = SeedSprint(sprintId: 401, status: SprintStatuses.Planned, projectId: 100);
        nextSprint.Name = "Sprint 2";
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var openIssue = SeedIssue(issueId: 201, statusId: 30, projectId: 100);
        openIssue.SprintId = activeSprint.Id;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.CompleteSprintAsync(activeSprint.Id, new CompleteSprintRequest
        {
            MoveOpenIssuesTo = ParseJson(jsonValue)
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedSprintId, Db.Issues.Single(i => i.Id == openIssue.Id).SprintId);

        var activity = Db.IssueActivities.Single(a => a.IssueId == openIssue.Id);
        Assert.Contains(expectedLabel, activity.Note);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC004: Non-existent SprintId returns NotFound ("SPRINT_NOT_FOUND").
    [Fact]
    public async Task CompleteSprintAsync_WhenSprintNotFound_ReturnsNotFound()
    {
        SeedProject(projectId: 100);

        var result = await KanbanService.CompleteSprintAsync(9999, new CompleteSprintRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("SPRINT_NOT_FOUND", result.ErrorCode);
    }

    // UTC005: Caller lacking SprintManage permission returns Forbidden without modifying sprint.
    [Fact]
    public async Task CompleteSprintAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        SeedProject(projectId: 100);
        var sprint = SeedSprint(sprintId: 400, status: SprintStatuses.Active, projectId: 100);
        AccessGuardMock
            .Setup(g => g.EnsureProjectPermissionAsync(100, PermissionKeys.SprintManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Forbidden("FORBIDDEN", "Permission denied."));

        var result = await KanbanService.CompleteSprintAsync(sprint.Id, new CompleteSprintRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal(SprintStatuses.Active, Db.Sprints.Single(s => s.Id == sprint.Id).Status);
    }

    // UTC006: Completing a sprint that is PLANNED or already COMPLETED returns Conflict ("INVALID_SPRINT_STATE").
    [Theory]
    [InlineData(SprintStatuses.Planned)]
    [InlineData(SprintStatuses.Completed)]
    public async Task CompleteSprintAsync_WhenSprintIsNotActive_ReturnsConflict(string currentStatus)
    {
        SeedProject(projectId: 100);
        var sprint = SeedSprint(sprintId: 400, status: currentStatus, projectId: 100);

        var result = await KanbanService.CompleteSprintAsync(sprint.Id, new CompleteSprintRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("INVALID_SPRINT_STATE", result.ErrorCode);
    }

    // UTC007: Target sprint ID that does not exist or is not PLANNED (e.g., COMPLETED) returns BadRequest ("INVALID_TARGET_SPRINT").
    [Theory]
    [InlineData("9999")]
    [InlineData("402")]
    public async Task CompleteSprintAsync_WhenTargetSprintInvalidOrNotPlanned_ReturnsBadRequest(string targetJson)
    {
        SeedProject(projectId: 100);
        var activeSprint = SeedSprint(sprintId: 400, status: SprintStatuses.Active, projectId: 100);
        SeedSprint(sprintId: 402, status: SprintStatuses.Completed, projectId: 100);

        var result = await KanbanService.CompleteSprintAsync(activeSprint.Id, new CompleteSprintRequest
        {
            MoveOpenIssuesTo = ParseJson(targetJson)
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("INVALID_TARGET_SPRINT", result.ErrorCode);
        Assert.Equal(SprintStatuses.Active, Db.Sprints.Single(s => s.Id == activeSprint.Id).Status);
    }

    // UTC008: Target PLANNED sprint belonging to a different project returns BadRequest ("INVALID_TARGET_SPRINT").
    [Fact]
    public async Task CompleteSprintAsync_WhenTargetSprintBelongsToOtherProject_ReturnsBadRequest()
    {
        SeedProject(projectId: 100, workspaceId: 10, companyId: 1);
        var otherWorkspace = new Workspace
        {
            Id = 11,
            CompanyId = 1,
            Name = "Other Workspace",
            Code = "other",
            IsActive = true,
            UpdatedAt = Clock.GetUtcNow().UtcDateTime
        };
        Db.Projects.Add(new Project
        {
            Id = 101,
            WorkspaceId = 11,
            Workspace = otherWorkspace,
            Key = "OTH",
            Name = "Other Project",
            SdlcMethod = SdlcMethods.Scrum,
            CreatedBy = 1,
            IsActive = true,
            CreatedAt = Clock.GetUtcNow().UtcDateTime,
            UpdatedAt = Clock.GetUtcNow().UtcDateTime
        });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var activeSprint = SeedSprint(sprintId: 400, status: SprintStatuses.Active, projectId: 100);
        SeedSprint(sprintId: 405, status: SprintStatuses.Planned, projectId: 101);

        var result = await KanbanService.CompleteSprintAsync(activeSprint.Id, new CompleteSprintRequest
        {
            MoveOpenIssuesTo = ParseJson("405")
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("INVALID_TARGET_SPRINT", result.ErrorCode);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC009: Completing an ACTIVE sprint with 0 issues succeeds cleanly with zeroed stats and no activities created.
    [Fact]
    public async Task CompleteSprintAsync_WhenSprintHasZeroIssues_CompletesWithZeroStats()
    {
        SeedProject(projectId: 100);
        var sprint = SeedSprint(sprintId: 400, status: SprintStatuses.Active, projectId: 100);

        var result = await KanbanService.CompleteSprintAsync(sprint.Id, new CompleteSprintRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(SprintStatuses.Completed, result.Data!.Status);
        Assert.Equal(0, result.Data.Stats.TotalIssues);
        Assert.Equal(0, result.Data.Stats.DoneIssues);
        Assert.Empty(Db.IssueActivities);
    }

    // UTC010: Soft-deleted open issues in the sprint are ignored and neither moved nor logged when completing sprint.
    [Fact]
    public async Task CompleteSprintAsync_WhenOpenIssueIsSoftDeleted_IgnoresDeletedIssue()
    {
        SeedWorkflow();
        var sprint = SeedSprint(sprintId: 400, status: SprintStatuses.Active, projectId: 100);
        var deletedOpenIssue = SeedIssue(issueId: 201, statusId: 30, projectId: 100);
        deletedOpenIssue.SprintId = sprint.Id;
        deletedOpenIssue.IsDeleted = true;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.CompleteSprintAsync(sprint.Id, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(sprint.Id, Db.Issues.Single(i => i.Id == deletedOpenIssue.Id).SprintId);
        Assert.Empty(Db.IssueActivities);
    }
}