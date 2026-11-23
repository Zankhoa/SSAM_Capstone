using Microsoft.AspNetCore.Http;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class UpdateSprintAsyncTests : KanbanServiceTestBase
{
    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: Updating all mutable fields trims Name/Goal, parses dates, updates CapacityPoints, and returns computed Stats.
    [Fact]
    public async Task UpdateSprintAsync_WithAllValidFields_UpdatesSprintAndReturnsStats()
    {
        SeedWorkflow();
        var sprint = SeedSprint(sprintId: 400, status: SprintStatuses.Planned, projectId: 100);
        var todoIssue = SeedIssue(issueId: 201, statusId: 30, projectId: 100);
        todoIssue.SprintId = sprint.Id;
        todoIssue.Points = 3;

        var doneIssue = SeedIssue(issueId: 202, statusId: 31, projectId: 100);
        doneIssue.SprintId = sprint.Id;
        doneIssue.Points = 5;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.UpdateSprintAsync(sprint.Id, new UpdateSprintRequest
        {
            Name = "  Sprint 1 Revised  ",
            Goal = "  Finalize authentication module  ",
            StartDate = "2026-12-01",
            EndDate = "2026-12-15",
            CapacityPoints = 25
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);

        var dto = result.Data!;
        Assert.Equal("Sprint 1 Revised", dto.Name);
        Assert.Equal("Finalize authentication module", dto.Goal);
        Assert.Equal("2026-12-01", dto.StartDate);
        Assert.Equal("2026-12-15", dto.EndDate);
        Assert.Equal(25, dto.CapacityPoints);
        Assert.Equal(2, dto.Stats.TotalIssues);
        Assert.Equal(1, dto.Stats.DoneIssues);
        Assert.Equal(8, dto.Stats.TotalPoints);
        Assert.Equal(5, dto.Stats.DonePoints);
    }

    // UTC002: Partial update keeps unspecified fields intact and clears specified nullable fields when passed null/whitespace.
    [Fact]
    public async Task UpdateSprintAsync_WithPartialAndClearedFields_PreservesUnspecifiedAndClearsNullable()
    {
        SeedProject(projectId: 100);
        var sprint = SeedSprint(sprintId: 400, status: SprintStatuses.Planned, projectId: 100);
        sprint.Name = "Original Sprint";
        sprint.Goal = "Initial goal";
        sprint.StartDate = new DateOnly(2026, 12, 1);
        sprint.EndDate = new DateOnly(2026, 12, 14);
        sprint.CapacityPoints = 20;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.UpdateSprintAsync(sprint.Id, new UpdateSprintRequest
        {
            Goal = "   ",
            StartDate = null,
            EndDate = "",
            CapacityPoints = null
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Original Sprint", result.Data!.Name);
        Assert.Null(result.Data.Goal);
        Assert.Null(result.Data.StartDate);
        Assert.Null(result.Data.EndDate);
        Assert.Null(result.Data.CapacityPoints);
    }

    // UTC003: Setting Status = "ACTIVE" on a PLANNED sprint delegates to StartSprintAsync and activates the sprint.
    [Fact]
    public async Task UpdateSprintAsync_WhenTransitioningPlannedToActive_DelegatesToStartSprint()
    {
        SeedProject(projectId: 100);
        var sprint = SeedSprint(sprintId: 400, status: SprintStatuses.Planned, projectId: 100);

        var result = await KanbanService.UpdateSprintAsync(sprint.Id, new UpdateSprintRequest
        {
            Status = " active ",
            Goal = "Active sprint goal",
            StartDate = "2026-11-25",
            EndDate = "2026-12-09"
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(SprintStatuses.Active, result.Data!.Status);
        Assert.Equal("Active sprint goal", result.Data.Goal);
        Assert.Equal("2026-11-25", result.Data.StartDate);
        Assert.Equal("2026-12-09", result.Data.EndDate);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC004: Non-existent SprintId returns NotFound ("SPRINT_NOT_FOUND").
    [Fact]
    public async Task UpdateSprintAsync_WhenSprintNotFound_ReturnsNotFound()
    {
        SeedProject(projectId: 100);

        var result = await KanbanService.UpdateSprintAsync(9999, new UpdateSprintRequest
        {
            Name = "Updated Name"
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("SPRINT_NOT_FOUND", result.ErrorCode);
    }

    // UTC005: Caller lacking SprintManage permission returns Forbidden without modifying sprint.
    [Fact]
    public async Task UpdateSprintAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        SeedProject(projectId: 100);
        var sprint = SeedSprint(sprintId: 400, status: SprintStatuses.Planned, projectId: 100);
        AccessGuardMock
            .Setup(g => g.EnsureProjectPermissionAsync(100, PermissionKeys.SprintManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Forbidden("FORBIDDEN", "Permission denied."));

        var result = await KanbanService.UpdateSprintAsync(sprint.Id, new UpdateSprintRequest
        {
            Name = "Blocked Update"
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("Sprint 1", Db.Sprints.Single(s => s.Id == sprint.Id).Name);
    }

    // UTC006: Blank or >100-character Name returns ValidationFailure on "name".
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task UpdateSprintAsync_WithInvalidName_ReturnsValidationFailure(string? rawName)
    {
        SeedProject(projectId: 100);
        var sprint = SeedSprint(sprintId: 400, status: SprintStatuses.Planned, projectId: 100);
        var name = rawName ?? new string('N', 101);

        var result = await KanbanService.UpdateSprintAsync(sprint.Id, new UpdateSprintRequest
        {
            Name = name
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("name", result.Errors!.Keys);
    }

    // UTC007: Goal exceeding 500 characters returns ValidationFailure on "goal".
    [Fact]
    public async Task UpdateSprintAsync_WhenGoalExceeds500Chars_ReturnsValidationFailure()
    {
        SeedProject(projectId: 100);
        var sprint = SeedSprint(sprintId: 400, status: SprintStatuses.Planned, projectId: 100);

        var result = await KanbanService.UpdateSprintAsync(sprint.Id, new UpdateSprintRequest
        {
            Goal = new string('G', 501)
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("goal", result.Errors!.Keys);
    }

    // UTC008: EndDate earlier than StartDate (both newly provided or compared against existing StartDate) returns ValidationFailure on "endDate".
    [Fact]
    public async Task UpdateSprintAsync_WhenEndDateIsBeforeStartDate_ReturnsValidationFailure()
    {
        SeedProject(projectId: 100);
        var sprint = SeedSprint(sprintId: 400, status: SprintStatuses.Planned, projectId: 100);
        sprint.StartDate = new DateOnly(2026, 12, 10);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.UpdateSprintAsync(sprint.Id, new UpdateSprintRequest
        {
            EndDate = "2026-12-05"
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("endDate", result.Errors!.Keys);
    }

    // UTC009: Negative CapacityPoints returns ValidationFailure on "capacityPoints".
    [Fact]
    public async Task UpdateSprintAsync_WhenCapacityPointsIsNegative_ReturnsValidationFailure()
    {
        SeedProject(projectId: 100);
        var sprint = SeedSprint(sprintId: 400, status: SprintStatuses.Planned, projectId: 100);

        var result = await KanbanService.UpdateSprintAsync(sprint.Id, new UpdateSprintRequest
        {
            CapacityPoints = -5
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("capacityPoints", result.Errors!.Keys);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC010: Name with exact minimum (1 char) and maximum (100 chars) length succeeds.
    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task UpdateSprintAsync_WithBoundaryNameLength_Succeeds(int length)
    {
        SeedProject(projectId: 100);
        var sprint = SeedSprint(sprintId: 400, status: SprintStatuses.Planned, projectId: 100);
        var exactName = new string('S', length);

        var result = await KanbanService.UpdateSprintAsync(sprint.Id, new UpdateSprintRequest
        {
            Name = exactName
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(exactName, result.Data!.Name);
    }

    // UTC011: Goal with exact maximum length (500 chars) succeeds.
    [Fact]
    public async Task UpdateSprintAsync_With500CharGoal_Succeeds()
    {
        SeedProject(projectId: 100);
        var sprint = SeedSprint(sprintId: 400, status: SprintStatuses.Planned, projectId: 100);
        var exactGoal = new string('G', 500);

        var result = await KanbanService.UpdateSprintAsync(sprint.Id, new UpdateSprintRequest
        {
            Goal = exactGoal
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(exactGoal, result.Data!.Goal);
    }

    // UTC012: CapacityPoints = 0 and EndDate == StartDate (single-day sprint boundary) succeed.
    [Fact]
    public async Task UpdateSprintAsync_WithZeroCapacityAndSameDayDates_Succeeds()
    {
        SeedProject(projectId: 100);
        var sprint = SeedSprint(sprintId: 400, status: SprintStatuses.Planned, projectId: 100);

        var result = await KanbanService.UpdateSprintAsync(sprint.Id, new UpdateSprintRequest
        {
            StartDate = "2026-12-01",
            EndDate = "2026-12-01",
            CapacityPoints = 0
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("2026-12-01", result.Data!.StartDate);
        Assert.Equal("2026-12-01", result.Data.EndDate);
        Assert.Equal(0, result.Data.CapacityPoints);
    }
}