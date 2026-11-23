using Microsoft.AspNetCore.Http;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class CreateSprintAsyncTests : KanbanServiceTestBase
{
    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: Creating a sprint with all valid fields trims Name/Goal, persists PLANNED sprint, and returns 201 Created.
    [Fact]
    public async Task CreateSprintAsync_WithAllValidFields_CreatesPlannedSprint()
    {
        SeedProject(projectId: 100);

        var result = await KanbanService.CreateSprintAsync(100, new CreateSprintRequest
        {
            Name = "  Sprint 1 - Core Auth  ",
            Goal = "  Deliver login and registration flows  ",
            StartDate = "2026-11-25",
            EndDate = "2026-12-09",
            CapacityPoints = 30
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);

        var dto = result.Data!;
        Assert.Equal(100, dto.ProjectId);
        Assert.Equal("Sprint 1 - Core Auth", dto.Name);
        Assert.Equal("Deliver login and registration flows", dto.Goal);
        Assert.Equal("2026-11-25", dto.StartDate);
        Assert.Equal("2026-12-09", dto.EndDate);
        Assert.Equal(SprintStatuses.Planned, dto.Status);
        Assert.Equal(30, dto.CapacityPoints);
        Assert.Equal(0, dto.Stats.TotalIssues);
        Assert.Equal(0, dto.Stats.TotalPoints);
        Assert.Null(dto.CompletedAt);

        var persisted = Assert.Single(Db.Sprints);
        Assert.Equal(1, persisted.CreatedById);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, persisted.CreatedAt);
    }

    // UTC002: Creating a sprint with only Name leaves optional Goal (including whitespace), dates, and CapacityPoints null.
    [Fact]
    public async Task CreateSprintAsync_WithOnlyRequiredName_CreatesSprintWithNullOptionals()
    {
        SeedProject(projectId: 100);

        var result = await KanbanService.CreateSprintAsync(100, new CreateSprintRequest
        {
            Name = "Sprint 2",
            Goal = "    ",
            StartDate = null,
            EndDate = null,
            CapacityPoints = null
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal("Sprint 2", result.Data!.Name);
        Assert.Null(result.Data.Goal);
        Assert.Null(result.Data.StartDate);
        Assert.Null(result.Data.EndDate);
        Assert.Null(result.Data.CapacityPoints);
        Assert.Equal(SprintStatuses.Planned, result.Data.Status);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC003: Caller lacking SprintManage permission returns Forbidden without persisting a sprint.
    [Fact]
    public async Task CreateSprintAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        SeedProject(projectId: 100);
        AccessGuardMock
            .Setup(g => g.EnsureProjectPermissionAsync(100, PermissionKeys.SprintManage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Forbidden("FORBIDDEN", "Permission denied."));

        var result = await KanbanService.CreateSprintAsync(100, new CreateSprintRequest
        {
            Name = "Sprint 1"
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Empty(Db.Sprints);
    }

    // UTC004: Empty, whitespace-only, or >100-character Name returns ValidationFailure on "name".
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateSprintAsync_WithInvalidName_ReturnsValidationFailure(string? rawName)
    {
        SeedProject(projectId: 100);
        var name = rawName ?? new string('S', 101);

        var result = await KanbanService.CreateSprintAsync(100, new CreateSprintRequest
        {
            Name = name
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("name", result.Errors!.Keys);
        Assert.Empty(Db.Sprints);
    }

    // UTC005: Goal exceeding 500 characters returns ValidationFailure on "goal".
    [Fact]
    public async Task CreateSprintAsync_WhenGoalExceeds500Chars_ReturnsValidationFailure()
    {
        SeedProject(projectId: 100);

        var result = await KanbanService.CreateSprintAsync(100, new CreateSprintRequest
        {
            Name = "Sprint 1",
            Goal = new string('G', 501)
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("goal", result.Errors!.Keys);
        Assert.Empty(Db.Sprints);
    }

    // UTC006: Negative CapacityPoints returns ValidationFailure on "capacityPoints".
    [Fact]
    public async Task CreateSprintAsync_WhenCapacityPointsIsNegative_ReturnsValidationFailure()
    {
        SeedProject(projectId: 100);

        var result = await KanbanService.CreateSprintAsync(100, new CreateSprintRequest
        {
            Name = "Sprint 1",
            CapacityPoints = -1
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("capacityPoints", result.Errors!.Keys);
        Assert.Empty(Db.Sprints);
    }

    // UTC007: Malformed StartDate or EndDate returns ValidationFailure on the corresponding field.
    [Theory]
    [InlineData("not-a-date", "2026-12-09", "startDate")]
    [InlineData("2026-11-25", "31/12/2026", "endDate")]
    public async Task CreateSprintAsync_WithInvalidDateFormat_ReturnsValidationFailure(
        string startDate,
        string endDate,
        string expectedErrorKey)
    {
        SeedProject(projectId: 100);

        var result = await KanbanService.CreateSprintAsync(100, new CreateSprintRequest
        {
            Name = "Sprint 1",
            StartDate = startDate,
            EndDate = endDate
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains(expectedErrorKey, result.Errors!.Keys);
        Assert.Empty(Db.Sprints);
    }

    // UTC008: EndDate earlier than StartDate returns ValidationFailure on "endDate".
    [Fact]
    public async Task CreateSprintAsync_WhenEndDateIsBeforeStartDate_ReturnsValidationFailure()
    {
        SeedProject(projectId: 100);

        var result = await KanbanService.CreateSprintAsync(100, new CreateSprintRequest
        {
            Name = "Sprint 1",
            StartDate = "2026-12-10",
            EndDate = "2026-12-09"
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("endDate", result.Errors!.Keys);
        Assert.Empty(Db.Sprints);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC009: Name with exact minimum (1 char) and maximum (100 chars) length succeeds.
    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task CreateSprintAsync_WithBoundaryNameLength_Succeeds(int length)
    {
        SeedProject(projectId: 100);
        var exactName = new string('S', length);

        var result = await KanbanService.CreateSprintAsync(100, new CreateSprintRequest
        {
            Name = exactName
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(exactName, result.Data!.Name);
    }

    // UTC010: Goal with exact maximum length (500 chars) succeeds.
    [Fact]
    public async Task CreateSprintAsync_With500CharGoal_Succeeds()
    {
        SeedProject(projectId: 100);
        var exactGoal = new string('G', 500);

        var result = await KanbanService.CreateSprintAsync(100, new CreateSprintRequest
        {
            Name = "Sprint 1",
            Goal = exactGoal
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(exactGoal, result.Data!.Goal);
    }

    // UTC011: CapacityPoints equal to 0 (lower bound) succeeds.
    [Fact]
    public async Task CreateSprintAsync_WithZeroCapacityPoints_Succeeds()
    {
        SeedProject(projectId: 100);

        var result = await KanbanService.CreateSprintAsync(100, new CreateSprintRequest
        {
            Name = "Sprint 1",
            CapacityPoints = 0
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Data!.CapacityPoints);
    }

    // UTC012: EndDate equal to StartDate (single-day sprint boundary) succeeds.
    [Fact]
    public async Task CreateSprintAsync_WhenEndDateEqualsStartDate_Succeeds()
    {
        SeedProject(projectId: 100);

        var result = await KanbanService.CreateSprintAsync(100, new CreateSprintRequest
        {
            Name = "One-Day Sprint",
            StartDate = "2026-11-25",
            EndDate = "2026-11-25"
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("2026-11-25", result.Data!.StartDate);
        Assert.Equal("2026-11-25", result.Data.EndDate);
    }
}