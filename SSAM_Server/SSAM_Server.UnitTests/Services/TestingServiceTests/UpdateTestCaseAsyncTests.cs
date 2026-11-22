using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.DTOs.Testing;
using SSAM_Server.Models;
using SSAM_Server.Security;
using TSH = SSAM_Server.UnitTests.Services.TestingServiceTests.TestingSeedHelpersCases;

namespace SSAM_Server.UnitTests.Services.TestingServiceTests;

public class UpdateTestCaseAsyncTests : TestingCasesTestBase
{
    private (CasesSeedTree Tree, TestCase Case) SetupValidCase(string planStatus = TestPlanStatuses.Open)
    {
        SetupCurrentUser(1);
        SetupGuardAllows(TSH.ProjectId, PermissionKeys.TestCaseUpdate);
        var tree = TSH.AddPlanTree(Db, TSH.ProjectId, planStatus);
        var tc = TSH.AddCase(Db, tree, "TC_1", "Original title", 1);
        return (tree, tc);
    }

    private static Dictionary<string, CellStyleDto> ReadStyles(string json)
        => JsonSerializer.Deserialize<Dictionary<string, CellStyleDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Update every scalar field at once -> 200, DTO and DB reflect new values (trimmed / normalised), plan touched
    [Fact]
    public async Task UpdateTestCaseAsync_WithAllFields_ReturnsOkAndPersistsChanges()
    {
        var (tree, tc) = SetupValidCase();
        var request = new UpdateTestCaseRequest
        {
            Title = "  New title  ",
            Preconditions = "  New pre  ",
            Steps = new List<TestStepDto> { new() { Action = "Click", Expected = "Opens" } },
            Actual = "  New actual  ",
            AiSuggestedReqs = true,
            Priority = "low",
            Category = "  Security  ",
            Browser = "  Firefox  ",
            CustomFields = new Dictionary<string, object?> { ["owner"] = "qa" },
            Status = "deleted",
            Position = 9
        };

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        var dto = result.Data!;
        Assert.Equal(tc.Id, dto.TestCaseId);
        Assert.Equal("TC_1", dto.Code);
        Assert.Equal("New title", dto.Title);
        Assert.Equal("New pre", dto.Preconditions);
        Assert.Equal("New actual", dto.Actual);
        Assert.True(dto.AiSuggestedReqs);
        Assert.Equal("P2", dto.Priority);
        Assert.Equal("Security", dto.Category);
        Assert.Equal("Firefox", dto.Browser);
        Assert.Equal(TestCaseStatuses.Deleted, dto.Status);
        Assert.Equal(9, dto.Position);
        Assert.Single(dto.Steps);
        Assert.Equal("Click", dto.Steps[0].Action);
        Assert.True(dto.CustomFields.ContainsKey("owner"));
        Assert.Equal(NowIso, dto.UpdatedAt);

        var saved = await LoadCaseAsync(tc.Id);
        Assert.Equal("New title", saved.Title);
        Assert.Equal("New pre", saved.Preconditions);
        Assert.Equal("New actual", saved.Actual);
        Assert.True(saved.AiSuggestedReqs);
        Assert.Equal("P2", saved.Priority);
        Assert.Equal("Security", saved.Category);
        Assert.Equal("Firefox", saved.Browser);
        Assert.Equal(TestCaseStatuses.Deleted, saved.Status);
        Assert.Equal(9, saved.Position);
        Assert.Contains("\"action\":\"Click\"", saved.Steps);
        Assert.Contains("\"owner\":\"qa\"", saved.CustomFields);
        Assert.Equal(Now, saved.UpdatedAt);
        Assert.Equal("TC_1", saved.Code);
        Assert.Equal(tree.Plan.Id, saved.PlanId);

        Assert.Equal(Now, (await LoadPlanAsync(tree.Plan.Id)).UpdatedAt);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(TSH.ProjectId, PermissionKeys.TestCaseUpdate, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Empty request -> 200, test case content untouched (UpdatedAt kept) but plan UpdatedAt is bumped
    [Fact]
    public async Task UpdateTestCaseAsync_WithEmptyRequest_ChangesNothingExceptPlanTimestamp()
    {
        var (tree, tc) = SetupValidCase();

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, new UpdateTestCaseRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        var saved = await LoadCaseAsync(tc.Id);
        Assert.Equal("Original title", saved.Title);
        Assert.Equal("pre", saved.Preconditions);
        Assert.Equal("actual", saved.Actual);
        Assert.Equal("Chrome", saved.Browser);
        Assert.Equal("Functional", saved.Category);
        Assert.Equal(TestCasePriorities.P1, saved.Priority);
        Assert.Equal(1, saved.Position);
        Assert.Equal(TSH.OldDate, saved.UpdatedAt);
        Assert.Equal(Now, (await LoadPlanAsync(tree.Plan.Id)).UpdatedAt);
    }

    // UTC03: Preconditions/Actual/Category/Browser explicitly set to null -> stored as empty string (DTO null)
    [Fact]
    public async Task UpdateTestCaseAsync_WithExplicitNullTextFields_ClearsThem()
    {
        var (_, tc) = SetupValidCase();
        var request = new UpdateTestCaseRequest { Preconditions = null, Actual = null, Category = null, Browser = null };

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.Preconditions);
        Assert.Null(result.Data.Actual);
        Assert.Null(result.Data.Category);
        Assert.Null(result.Data.Browser);
        var saved = await LoadCaseAsync(tc.Id);
        Assert.Equal(string.Empty, saved.Preconditions);
        Assert.Equal(string.Empty, saved.Actual);
        Assert.Equal(string.Empty, saved.Category);
        Assert.Equal(string.Empty, saved.Browser);
        Assert.Equal(Now, saved.UpdatedAt);
    }

    // UTC04: Only title changes -> other fields remain untouched
    [Fact]
    public async Task UpdateTestCaseAsync_WithOnlyTitle_LeavesOtherFieldsUntouched()
    {
        var (_, tc) = SetupValidCase();

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, new UpdateTestCaseRequest { Title = "Only title" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var saved = await LoadCaseAsync(tc.Id);
        Assert.Equal("Only title", saved.Title);
        Assert.Equal("pre", saved.Preconditions);
        Assert.Equal("actual", saved.Actual);
        Assert.Equal("Chrome", saved.Browser);
        Assert.Equal("[]", saved.Steps);
        Assert.Equal("{}", saved.CustomFields);
        Assert.Equal(TestCaseStatuses.Active, saved.Status);
        Assert.Equal(Now, saved.UpdatedAt);
    }

    // UTC05: Steps replaced by empty list -> stored as "[]"
    [Fact]
    public async Task UpdateTestCaseAsync_WithEmptyStepsList_ClearsSteps()
    {
        var (_, tc) = SetupValidCase();
        var existing = await Db.TestCases.SingleAsync(TestContext.Current.CancellationToken);
        existing.Steps = "[{\"action\":\"a\",\"expected\":\"b\"}]";
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, new UpdateTestCaseRequest { Steps = new List<TestStepDto>() }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Steps);
        Assert.Equal("[]", (await LoadCaseAsync(tc.Id)).Steps);
    }

    // UTC06: Priority update normalises value (HIGH -> P0)
    [Fact]
    public async Task UpdateTestCaseAsync_WithHighPriority_StoresP0()
    {
        var (_, tc) = SetupValidCase();

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, new UpdateTestCaseRequest { Priority = "HIGH" }, TestContext.Current.CancellationToken);

        Assert.Equal("P0", result.Data!.Priority);
        Assert.Equal("P0", (await LoadCaseAsync(tc.Id)).Priority);
    }

    // UTC07: Blank / whitespace priority is ignored (existing priority kept)
    [Theory]
    [InlineData("")]        // UTC07a
    [InlineData("   ")]     // UTC07b
    public async Task UpdateTestCaseAsync_WithBlankPriority_KeepsExistingPriority(string priority)
    {
        var (_, tc) = SetupValidCase();

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, new UpdateTestCaseRequest { Priority = priority }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("P1", result.Data!.Priority);
        Assert.Equal("P1", (await LoadCaseAsync(tc.Id)).Priority);
    }

    // UTC08: Unknown priority text falls back to P1
    [Fact]
    public async Task UpdateTestCaseAsync_WithUnknownPriority_FallsBackToP1()
    {
        var (_, tc) = SetupValidCase();
        var existing = await Db.TestCases.SingleAsync(TestContext.Current.CancellationToken);
        existing.Priority = "P3";
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, new UpdateTestCaseRequest { Priority = "urgent!!" }, TestContext.Current.CancellationToken);

        Assert.Equal("P1", result.Data!.Priority);
    }

    // UTC09: Status "active" (lower-case) is upper-cased and applied (restores a deleted case)
    [Fact]
    public async Task UpdateTestCaseAsync_WithLowerCaseActiveStatus_RestoresDeletedCase()
    {
        SetupCurrentUser(1);
        SetupGuardAllows(TSH.ProjectId, PermissionKeys.TestCaseUpdate);
        var tree = TSH.AddPlanTree(Db);
        var tc = TSH.AddCase(Db, tree, status: TestCaseStatuses.Deleted);

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, new UpdateTestCaseRequest { Status = " active " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(TestCaseStatuses.Active, result.Data!.Status);
        Assert.Equal(TestCaseStatuses.Active, (await LoadCaseAsync(tc.Id)).Status);
    }

    // UTC10: Invalid status text is ignored (status unchanged)
    [Fact]
    public async Task UpdateTestCaseAsync_WithInvalidStatus_IgnoresStatus()
    {
        var (_, tc) = SetupValidCase();

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, new UpdateTestCaseRequest { Status = "ARCHIVED" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(TestCaseStatuses.Active, result.Data!.Status);
        Assert.Equal(TestCaseStatuses.Active, (await LoadCaseAsync(tc.Id)).Status);
    }

    // UTC11: Whitespace status is ignored
    [Fact]
    public async Task UpdateTestCaseAsync_WithWhitespaceStatus_IgnoresStatus()
    {
        var (_, tc) = SetupValidCase();

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, new UpdateTestCaseRequest { Status = "   " }, TestContext.Current.CancellationToken);

        Assert.Equal(TestCaseStatuses.Active, result.Data!.Status);
    }

    // UTC12: Move to another section of the same plan -> SectionId and ModuleId follow the target section
    [Fact]
    public async Task UpdateTestCaseAsync_WithSectionInAnotherModuleOfSamePlan_MovesCase()
    {
        var (tree, tc) = SetupValidCase();
        var module2 = TSH.AddModule(Db, tree.Plan, "PAY", "Payment");
        var section2 = TSH.AddSection(Db, module2, "Checkout");

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, new UpdateTestCaseRequest { SectionId = section2.Id }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(section2.Id, result.Data!.SectionId);
        Assert.Equal(module2.Id, result.Data.ModuleId);
        Assert.Equal("PAY-1", result.Data.ModuleCode);
        var saved = await LoadCaseAsync(tc.Id);
        Assert.Equal(section2.Id, saved.SectionId);
        Assert.Equal(module2.Id, saved.ModuleId);
        Assert.Equal(Now, saved.UpdatedAt);
    }

    // UTC13: SectionId equal to the current section -> no move, case unchanged
    [Fact]
    public async Task UpdateTestCaseAsync_WithSameSectionId_KeepsSection()
    {
        var (tree, tc) = SetupValidCase();

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, new UpdateTestCaseRequest { SectionId = tree.Section.Id }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(tree.Section.Id, result.Data!.SectionId);
        Assert.Equal(tree.Module.Id, result.Data.ModuleId);
    }

    // UTC14: Section of another plan is ignored silently (case stays where it is)
    [Fact]
    public async Task UpdateTestCaseAsync_WithSectionFromAnotherPlan_IgnoresMove()
    {
        var (tree, tc) = SetupValidCase();
        var other = TSH.AddPlanTree(Db);

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, new UpdateTestCaseRequest { SectionId = other.Section.Id }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(tree.Section.Id, result.Data!.SectionId);
        Assert.Equal(tree.Section.Id, (await LoadCaseAsync(tc.Id)).SectionId);
    }

    // UTC15: Unknown section id is ignored silently
    [Fact]
    public async Task UpdateTestCaseAsync_WithUnknownSectionId_IgnoresMove()
    {
        var (tree, tc) = SetupValidCase();

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, new UpdateTestCaseRequest { SectionId = 9999 }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(tree.Section.Id, (await LoadCaseAsync(tc.Id)).SectionId);
    }

    // UTC16: CellStyles are cleaned (lower-cased colors, valid size/align kept, flags only when true) and persisted
    [Fact]
    public async Task UpdateTestCaseAsync_WithCellStyles_StoresCleanedStyles()
    {
        var (_, tc) = SetupValidCase();
        var request = new UpdateTestCaseRequest
        {
            CellStyles = new Dictionary<string, CellStyleDto?>
            {
                ["title"] = new CellStyleDto { B = true, I = false, U = true, S = false, Size = 14, Color = "#FF00AA", Fill = " #00FF00 ", Align = " CENTER " }
            }
        };

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var style = Assert.Single(result.Data!.CellStyles);
        Assert.Equal("title", style.Key);
        Assert.True(style.Value.B);
        Assert.Null(style.Value.I);
        Assert.True(style.Value.U);
        Assert.Null(style.Value.S);
        Assert.Equal(14, style.Value.Size);
        Assert.Equal("#ff00aa", style.Value.Color);
        Assert.Equal("#00ff00", style.Value.Fill);
        Assert.Equal("center", style.Value.Align);

        var stored = ReadStyles((await LoadCaseAsync(tc.Id)).CellStyles);
        Assert.Equal("#ff00aa", stored["title"].Color);
        Assert.Equal("center", stored["title"].Align);
    }

    // UTC17: CellStyles are merged into existing styles; null value removes the key; empty style removes the key
    [Fact]
    public async Task UpdateTestCaseAsync_WithCellStylesMergeAndRemoval_MergesIntoExisting()
    {
        var (_, tc) = SetupValidCase();
        var existing = await Db.TestCases.SingleAsync(TestContext.Current.CancellationToken);
        existing.CellStyles = "{\"a\":{\"b\":true},\"b\":{\"i\":true},\"c\":{\"u\":true}}";
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var request = new UpdateTestCaseRequest
        {
            CellStyles = new Dictionary<string, CellStyleDto?>
            {
                ["a"] = null,                                   // removed
                ["b"] = new CellStyleDto { Size = 99 },         // cleans to nothing -> removed
                ["d"] = new CellStyleDto { S = true }           // added
            }
        };

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "c", "d" }, result.Data!.CellStyles.Keys.OrderBy(k => k).ToArray());
        var stored = ReadStyles((await LoadCaseAsync(tc.Id)).CellStyles);
        Assert.Equal(new[] { "c", "d" }, stored.Keys.OrderBy(k => k).ToArray());
        Assert.True(stored["c"].U);
        Assert.True(stored["d"].S);
    }

    // UTC18: Blank cell-style keys are skipped, surrounding whitespace of keys is trimmed
    [Fact]
    public async Task UpdateTestCaseAsync_WithBlankAndPaddedCellStyleKeys_SkipsBlankAndTrimsPadded()
    {
        var (_, tc) = SetupValidCase();
        var request = new UpdateTestCaseRequest
        {
            CellStyles = new Dictionary<string, CellStyleDto?>
            {
                ["   "] = new CellStyleDto { B = true },
                ["  steps  "] = new CellStyleDto { B = true }
            }
        };

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var style = Assert.Single(result.Data!.CellStyles);
        Assert.Equal("steps", style.Key);
    }

    // UTC19: Re-linking requirements replaces old links, creates new ones with the document hash and clears REQ_CHANGED retest flags only
    [Fact]
    public async Task UpdateTestCaseAsync_WithRequirementRefs_ReplacesLinksAndClearsReqChangedFlags()
    {
        var (_, tc) = SetupValidCase();
        var oldDoc = TSH.AddDocumentWithRequirement(Db, TSH.ProjectId, "SRS-OLD", "REQ-OLD", "old-hash");
        var newDoc = TSH.AddDocumentWithRequirement(Db, TSH.ProjectId, "SRS-NEW", "REQ-NEW", "new-hash", "New requirement");
        Db.TestCaseRequirementLinks.Add(new TestCaseRequirementLink { TestCaseId = tc.Id, DocumentId = oldDoc.Id, ReqUid = "REQ-OLD", ReqHash = "old-hash", LinkedAt = TSH.OldDate });
        Db.TestRetestFlags.Add(new TestRetestFlag { TestCaseId = tc.Id, EnvId = 1, Reason = TestRetestReasons.ReqChanged, At = TSH.OldDate });
        Db.TestRetestFlags.Add(new TestRetestFlag { TestCaseId = tc.Id, EnvId = 2, Reason = TestRetestReasons.Regression, At = TSH.OldDate });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new UpdateTestCaseRequest
        {
            RequirementRefs = new List<RequirementRefInputDto> { new() { DocumentId = newDoc.Id, ReqUid = "req-new" } }
        };

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var req = Assert.Single(result.Data!.Requirements);
        Assert.Equal("SRS-NEW:REQ-NEW", req.ReqKey);
        Assert.Equal("New requirement", req.Title);
        var link = await Db.TestCaseRequirementLinks.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(newDoc.Id, link.DocumentId);
        Assert.Equal("REQ-NEW", link.ReqUid);
        Assert.Equal("new-hash", link.ReqHash);
        Assert.Equal(Now, link.LinkedAt);
        var flag = await Db.TestRetestFlags.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(TestRetestReasons.Regression, flag.Reason);
        Assert.Equal(2, flag.EnvId);
        Assert.Equal(Now, (await LoadCaseAsync(tc.Id)).UpdatedAt);
    }

    // UTC20: Empty RequirementRefs list -> existing links are removed
    [Fact]
    public async Task UpdateTestCaseAsync_WithEmptyRequirementRefs_RemovesAllLinks()
    {
        var (_, tc) = SetupValidCase();
        var doc = TSH.AddDocumentWithRequirement(Db);
        Db.TestCaseRequirementLinks.Add(new TestCaseRequirementLink { TestCaseId = tc.Id, DocumentId = doc.Id, ReqUid = "REQ-001", ReqHash = "hash-1", LinkedAt = TSH.OldDate });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, new UpdateTestCaseRequest { RequirementRefs = new List<RequirementRefInputDto>() }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Requirements);
        Assert.Empty(Db.TestCaseRequirementLinks);
    }

    // UTC21: RequirementRefs null -> existing links untouched
    [Fact]
    public async Task UpdateTestCaseAsync_WithNullRequirementRefs_KeepsExistingLinks()
    {
        var (_, tc) = SetupValidCase();
        var doc = TSH.AddDocumentWithRequirement(Db);
        Db.TestCaseRequirementLinks.Add(new TestCaseRequirementLink { TestCaseId = tc.Id, DocumentId = doc.Id, ReqUid = "REQ-001", ReqHash = "hash-1", LinkedAt = TSH.OldDate });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, new UpdateTestCaseRequest { Title = "x" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Requirements);
        Assert.Equal(1, await Db.TestCaseRequirementLinks.CountAsync(TestContext.Current.CancellationToken));
    }

    // UTC22: Updating a case that has a linked bug returns bug info in the DTO
    [Fact]
    public async Task UpdateTestCaseAsync_WhenCaseHasLinkedBug_ReturnsBugRefInDto()
    {
        SetupCurrentUser(1);
        SetupGuardAllows(TSH.ProjectId, PermissionKeys.TestCaseUpdate);
        var tree = TSH.AddPlanTree(Db);
        var issue = TSH.AddIssue(Db, TSH.ProjectId, "BUG-7", "Crash on login", statusName: "In Progress", statusCategory: StatusCategories.InProgress, priority: IssuePriorities.Highest);
        var tc = TSH.AddCase(Db, tree, bugIssueId: issue.Id);

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, new UpdateTestCaseRequest { Title = "renamed" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var bug = result.Data!.Bug;
        Assert.NotNull(bug);
        Assert.Equal(issue.Id, bug.IssueId);
        Assert.Equal("BUG-7", bug.Key);
        Assert.Equal("Crash on login", bug.Title);
        Assert.Equal("HIGHEST", bug.Priority);
        Assert.Equal("In Progress", bug.StatusName);
        Assert.Equal(StatusCategories.InProgress, bug.StatusCategory);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC23: Test case does not exist -> 404 TEST_CASE_NOT_FOUND, guard not called
    [Fact]
    public async Task UpdateTestCaseAsync_WhenCaseNotFound_ReturnsNotFound()
    {
        SetupCurrentUser(1);

        var result = await TestingService.UpdateTestCaseAsync(9999, new UpdateTestCaseRequest { Title = "x" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_CASE_NOT_FOUND", result.ErrorCode);
        Assert.Equal("Test case not found.", result.ErrorMessage);
        VerifyGuardNeverCalled();
    }

    // UTC24: Unauthenticated user -> 401, case unchanged
    [Fact]
    public async Task UpdateTestCaseAsync_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var tree = TSH.AddPlanTree(Db);
        var tc = TSH.AddCase(Db, tree);
        SetupGuardUnauthenticated(TSH.ProjectId, PermissionKeys.TestCaseUpdate);

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, new UpdateTestCaseRequest { Title = "hacked" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Equal("Existing case", (await LoadCaseAsync(tc.Id)).Title);
        Assert.Equal(TSH.OldDate, (await LoadPlanAsync(tree.Plan.Id)).UpdatedAt);
    }

    // UTC25: Permission denied -> 403 with required permission, case unchanged
    [Fact]
    public async Task UpdateTestCaseAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        var tree = TSH.AddPlanTree(Db);
        var tc = TSH.AddCase(Db, tree);
        SetupGuardDenies(TSH.ProjectId, PermissionKeys.TestCaseUpdate);

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, new UpdateTestCaseRequest { Title = "hacked" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.TestCaseUpdate, result.RequiredPermission);
        Assert.Equal("Existing case", (await LoadCaseAsync(tc.Id)).Title);
    }

    // UTC26: Plan already handed over -> 400 PLAN_HANDED_OVER, nothing changed
    [Fact]
    public async Task UpdateTestCaseAsync_WhenPlanHandedOver_ReturnsBadRequest()
    {
        var (tree, tc) = SetupValidCase(TestPlanStatuses.HandedOver);

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, new UpdateTestCaseRequest { Title = "late edit" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("PLAN_HANDED_OVER", result.ErrorCode);
        Assert.Equal("Test plan has already been handed over.", result.ErrorMessage);
        Assert.Equal("Original title", (await LoadCaseAsync(tc.Id)).Title);
        Assert.Equal(TSH.OldDate, (await LoadPlanAsync(tree.Plan.Id)).UpdatedAt);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC27: TestCaseId = 0 -> 404 TEST_CASE_NOT_FOUND
    [Fact]
    public async Task UpdateTestCaseAsync_WithIdZero_ReturnsNotFound()
    {
        var result = await TestingService.UpdateTestCaseAsync(0, new UpdateTestCaseRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_CASE_NOT_FOUND", result.ErrorCode);
    }

    // UTC28: Negative TestCaseId -> 404 TEST_CASE_NOT_FOUND
    [Fact]
    public async Task UpdateTestCaseAsync_WithNegativeId_ReturnsNotFound()
    {
        var result = await TestingService.UpdateTestCaseAsync(-5, new UpdateTestCaseRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_CASE_NOT_FOUND", result.ErrorCode);
    }

    // UTC29: Title whitespace only -> stored as empty string (no blank-title validation)
    [Fact]
    public async Task UpdateTestCaseAsync_WithWhitespaceTitle_StoresEmptyTitle()
    {
        var (_, tc) = SetupValidCase();

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, new UpdateTestCaseRequest { Title = "    " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, result.Data!.Title);
        Assert.Equal(string.Empty, (await LoadCaseAsync(tc.Id)).Title);
    }

    // UTC30: Very long title (10 000 chars) accepted - no max length enforced
    [Fact]
    public async Task UpdateTestCaseAsync_WithVeryLongTitle_IsAccepted()
    {
        var (_, tc) = SetupValidCase();
        var title = new string('t', 10_000);

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, new UpdateTestCaseRequest { Title = title }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(title, (await LoadCaseAsync(tc.Id)).Title);
    }

    // UTC31: Position boundary values (0, negative, int.MaxValue) are stored verbatim
    [Theory]
    [InlineData(0)]                // UTC31a
    [InlineData(-1)]               // UTC31b
    [InlineData(int.MaxValue)]     // UTC31c
    public async Task UpdateTestCaseAsync_WithBoundaryPosition_StoresPosition(int position)
    {
        var (_, tc) = SetupValidCase();

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, new UpdateTestCaseRequest { Position = position }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(position, result.Data!.Position);
        Assert.Equal(position, (await LoadCaseAsync(tc.Id)).Position);
    }

    // UTC32: Cell-style key of exactly 64 chars is kept, 65 chars is skipped
    [Fact]
    public async Task UpdateTestCaseAsync_WithCellStyleKeyLengthBoundary_Keeps64AndSkips65()
    {
        var (_, tc) = SetupValidCase();
        var key64 = new string('k', 64);
        var key65 = new string('k', 65);
        var request = new UpdateTestCaseRequest
        {
            CellStyles = new Dictionary<string, CellStyleDto?>
            {
                [key64] = new CellStyleDto { B = true },
                [key65] = new CellStyleDto { B = true }
            }
        };

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var style = Assert.Single(result.Data!.CellStyles);
        Assert.Equal(key64, style.Key);
    }

    // UTC33: Cell-style font sizes - allowed values (11, 24) kept, neighbours (10, 25) dropped
    [Theory]
    [InlineData(11, 11)]       // UTC33a: min allowed
    [InlineData(24, 24)]       // UTC33b: max allowed
    [InlineData(10, null)]     // UTC33c: just below min
    [InlineData(25, null)]     // UTC33d: just above max
    [InlineData(13, null)]     // UTC33e: not in allowed list
    public async Task UpdateTestCaseAsync_WithCellStyleSize_KeepsOnlyAllowedSizes(int size, int? expectedSize)
    {
        var (_, tc) = SetupValidCase();
        var request = new UpdateTestCaseRequest
        {
            CellStyles = new Dictionary<string, CellStyleDto?> { ["c"] = new CellStyleDto { B = true, Size = size } }
        };

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedSize, result.Data!.CellStyles["c"].Size);
    }

    // UTC34: Cell-style colors - 6 hex digits accepted; 3 digits, 7 digits, missing '#', non-hex are dropped
    [Theory]
    [InlineData("#abcdef", "#abcdef")]   // UTC34a: valid
    [InlineData("#ABCDEF", "#abcdef")]   // UTC34b: valid, lower-cased
    [InlineData("#abc", null)]           // UTC34c: 3 digits
    [InlineData("#abcdef0", null)]       // UTC34d: 7 digits
    [InlineData("abcdef", null)]         // UTC34e: no '#'
    [InlineData("#gggggg", null)]        // UTC34f: not hex
    public async Task UpdateTestCaseAsync_WithCellStyleColor_AcceptsOnlySixDigitHex(string color, string? expected)
    {
        var (_, tc) = SetupValidCase();
        var request = new UpdateTestCaseRequest
        {
            CellStyles = new Dictionary<string, CellStyleDto?> { ["c"] = new CellStyleDto { B = true, Color = color } }
        };

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Data!.CellStyles["c"].Color);
    }

    // UTC35: Cell-style align - only center/right kept ("left" and garbage dropped)
    [Theory]
    [InlineData("right", "right")]   // UTC35a
    [InlineData("left", null)]       // UTC35b
    [InlineData("justify", null)]    // UTC35c
    public async Task UpdateTestCaseAsync_WithCellStyleAlign_KeepsOnlyCenterOrRight(string align, string? expected)
    {
        var (_, tc) = SetupValidCase();
        var request = new UpdateTestCaseRequest
        {
            CellStyles = new Dictionary<string, CellStyleDto?> { ["c"] = new CellStyleDto { B = true, Align = align } }
        };

        var result = await TestingService.UpdateTestCaseAsync(tc.Id, request, TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Data!.CellStyles["c"].Align);
    }
}
