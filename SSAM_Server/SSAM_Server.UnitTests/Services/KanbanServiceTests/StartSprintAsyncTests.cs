using Microsoft.AspNetCore.Http;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class StartSprintAsyncTests : KanbanServiceTestBase
{
    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: Starting a PLANNED sprint with explicit StartDate, EndDate, and Goal updates fields, sets Status = ACTIVE, and returns computed Stats.
    [Fact]
    public async Task StartSprintAsync_WithExplicitRequestFields_ActivatesSprintAndComputesStats()
    {
        SeedWorkflow();
        var sprint = SeedSprint(sprintId: 400, status: SprintStatuses.Planned, projectId: 100);
        var issue1 = SeedIssue(issueId: 201, statusId: 30, projectId: 100);
        issue1.SprintId = sprint.Id;
        issue1.Points = 5;

        var issue2 = SeedIssue(issueId: 202, statusId: 31, projectId: 100);
        issue2.SprintId = sprint.Id;
        issue2.Points = 8;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.StartSprintAsync(sprint.Id, new StartSprintRequest
        {
            StartDate = "2026-11-25",
            EndDate = "2026-12-09",
            Goal = "  Complete core Kanban features  "
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);

        var dto = result.Data!;
        Assert.Equal(SprintStatuses.Active, dto.Status);
        Assert.Equal("2026-11-25", dto.StartDate);
        Assert.Equal("2026-12-09", dto.EndDate);
        Assert.Equal("Complete core Kanban features", dto.Goal);
        Assert.Equal(2, dto.Stats.TotalIssues);
        Assert.Equal(1, dto.Stats.DoneIssues);
        Assert.Equal(13, dto.Stats.TotalPoints);
        Assert.Equal(8, dto.Stats.DonePoints);

        var persisted = Db.Sprints.Single(s => s.Id == sprint.Id);
        Assert.Equal(SprintStatuses.Active, persisted.Status);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, persisted.UpdatedAt);
    }

    // UTC002: Starting a PLANNED sprint with null request defaults StartDate to today (Clock) and EndDate to StartDate + 14 days.
    [Fact]
    public async Task StartSprintAsync_WithNullRequestAndNoDates_DefaultsStartDateToTodayAndEndDatePlus14Days()
    {
        SeedProject(projectId: 100);
        var sprint = SeedSprint(sprintId: 400, status: SprintStatuses.Planned, projectId: 100);
        sprint.Goal = "Existing sprint goal";
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.StartSprintAsync(sprint.Id, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(SprintStatuses.Active, result.Data!.Status);
        Assert.Equal("2026-11-24", result.Data.StartDate);
        Assert.Equal("2026-12-08", result.Data.EndDate);
        Assert.Equal("Existing sprint goal", result.Data.Goal);
    }

    // UTC003: Starting a PLANNED sprint that already has pre-configured StartDate/EndDate preserves them when request omits dates, and clears Goal when passed whitespace.
    [Fact]
    public async Task StartSprintAsync_WithExistingDatesAndWhitespaceGoal_PreservesDatesAndClearsGoal()
    {
        SeedProject(projectId: 100);
        var sprint = SeedSprint(sprintId: 400, status: SprintStatuses.Planned, projectId: 100);
        sprint.StartDate = new DateOnly(2026, 12, 1);
        sprint.EndDate = new DateOnly(2026, 12, 10);
        sprint.Goal = "Old goal";
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await KanbanService.StartSprintAsync(sprint.Id, new StartSprintRequest
        {
            StartDate = null,
            EndDate = "   ",
            Goal = "   "
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(SprintStatuses.Active, result.Data!.Status);
        Assert.Equal("2026-12-01", result.Data.StartDate);
        Assert.Equal("2026-12-10", result.Data.EndDate);
        Assert.Null(result.Data.Goal);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC004: Non-existent SprintId returns NotFound ("SPRINT_NOT_FOUND").
    [Fact]
    public async Task StartSprintAsync_WhenSprintNotFound_ReturnsNotFound()
    {
        SeedProject(projectId: 100);

        var result = await KanbanService.StartSprintAsync(9999, new StartSprintRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("SPRINT_NOT_FOUND", result.ErrorCode);
    }

    // UTC005: Caller lacking SprintManage permission returns Forbidden without changing sprint status.
    [Fact]
    public async Task StartSprintAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        SeedProject(projectId: 100);
        var sprint = SeedSprint(sprintId: 400, status: SprintStatuses.Planned, projectId: 100);
        AccessGuardMock
            .Setup(g => g.EnsureProjectPermissionAsync(100, PermissionKeys.SprintManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Forbidden("FORBIDDEN", "Permission denied."));

        var result = await KanbanService.StartSprintAsync(sprint.Id, new StartSprintRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal(SprintStatuses.Planned, Db.Sprints.Single(s => s.Id == sprint.Id).Status);
    }

    // UTC006: Starting a sprint that is already ACTIVE or COMPLETED returns Conflict ("INVALID_SPRINT_STATE").
    [Theory]
    [InlineData(SprintStatuses.Active)]
    [InlineData(SprintStatuses.Completed)]
    public async Task StartSprintAsync_WhenSprintIsNotPlanned_ReturnsConflict(string currentStatus)
    {
        SeedProject(projectId: 100);
        var sprint = SeedSprint(sprintId: 400, status: currentStatus, projectId: 100);

        var result = await KanbanService.StartSprintAsync(sprint.Id, new StartSprintRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("INVALID_SPRINT_STATE", result.ErrorCode);
    }

    // UTC007: Starting a PLANNED sprint when another sprint in the same project is already ACTIVE returns Conflict ("ACTIVE_SPRINT_EXISTS").
    [Fact]
    public async Task StartSprintAsync_WhenAnotherActiveSprintExistsInProject_ReturnsConflict()
    {
        SeedProject(projectId: 100);
        SeedSprint(sprintId: 400, status: SprintStatuses.Active, projectId: 100);
        var plannedSprint = SeedSprint(sprintId: 401, status: SprintStatuses.Planned, projectId: 100);

        var result = await KanbanService.StartSprintAsync(plannedSprint.Id, new StartSprintRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("ACTIVE_SPRINT_EXISTS", result.ErrorCode);
        Assert.Equal(SprintStatuses.Planned, Db.Sprints.Single(s => s.Id == plannedSprint.Id).Status);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC008: Starting a PLANNED sprint in project 100 succeeds even when a different project (101) already has an ACTIVE sprint.
    [Fact]
    public async Task StartSprintAsync_WhenActiveSprintExistsInDifferentProject_Succeeds()
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

        SeedSprint(sprintId: 400, status: SprintStatuses.Active, projectId: 101);
        var targetSprint = SeedSprint(sprintId: 401, status: SprintStatuses.Planned, projectId: 100);

        var result = await KanbanService.StartSprintAsync(targetSprint.Id, new StartSprintRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(SprintStatuses.Active, result.Data!.Status);
    }

    // UTC009: When StartDate is explicitly provided and EndDate is null, EndDate defaults to StartDate + 14 days (exact 14-day default window).
    [Fact]
    public async Task StartSprintAsync_WithOnlyStartDateProvided_DefaultsEndDateToStartDatePlus14Days()
    {
        SeedProject(projectId: 100);
        var sprint = SeedSprint(sprintId: 400, status: SprintStatuses.Planned, projectId: 100);

        var result = await KanbanService.StartSprintAsync(sprint.Id, new StartSprintRequest
        {
            StartDate = "2026-12-01",
            EndDate = null
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("2026-12-01", result.Data!.StartDate);
        Assert.Equal("2026-12-15", result.Data.EndDate);
    }

    // UTC010: Single-day sprint boundary where EndDate equals StartDate succeeds.
    [Fact]
    public async Task StartSprintAsync_WhenEndDateEqualsStartDate_Succeeds()
    {
        SeedProject(projectId: 100);
        var sprint = SeedSprint(sprintId: 400, status: SprintStatuses.Planned, projectId: 100);

        var result = await KanbanService.StartSprintAsync(sprint.Id, new StartSprintRequest
        {
            StartDate = "2026-11-24",
            EndDate = "2026-11-24"
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("2026-11-24", result.Data!.StartDate);
        Assert.Equal("2026-11-24", result.Data.EndDate);
    }
}