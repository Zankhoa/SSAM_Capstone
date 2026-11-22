using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.DTOs.Testing;
using SSAM_Server.Models;
using SSAM_Server.Security;
using TSH = SSAM_Server.UnitTests.Services.TestingServiceTests.TestingSeedHelpersCases;

namespace SSAM_Server.UnitTests.Services.TestingServiceTests;

public class BulkUpdateTestCasesAsyncTests : TestingCasesTestBase
{
    private CasesSeedTree SetupValidPlan(string planStatus = TestPlanStatuses.Open)
    {
        SetupCurrentUser(1);
        SetupGuardAllows(TSH.ProjectId, PermissionKeys.TestCaseUpdate);
        return TSH.AddPlanTree(Db, TSH.ProjectId, planStatus);
    }

    private static BulkUpdateTestCasesRequest Bulk(params (long Id, UpdateTestCaseRequest Changes)[] items)
        => new() { Items = items.Select(i => new BulkUpdateTestCaseItemDto { TestCaseId = i.Id, Changes = i.Changes }).ToList() };

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Update two cases with different changes -> 200, both returned in DB order, both persisted, plan touched
    [Fact]
    public async Task BulkUpdateTestCasesAsync_WithTwoItems_UpdatesBothAndReturnsThem()
    {
        var tree = SetupValidPlan();
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);
        var b = TSH.AddCase(Db, tree, "TC_2", "B", 2);

        var result = await TestingService.BulkUpdateTestCasesAsync(tree.Plan.Id, Bulk(
            (a.Id, new UpdateTestCaseRequest { Title = "  A2  ", Priority = "HIGH" }),
            (b.Id, new UpdateTestCaseRequest { Status = "DELETED", Position = 5 })), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Items.Count);
        var dtoA = result.Data.Items.Single(x => x.TestCaseId == a.Id);
        var dtoB = result.Data.Items.Single(x => x.TestCaseId == b.Id);
        Assert.Equal("A2", dtoA.Title);
        Assert.Equal("P0", dtoA.Priority);
        Assert.Equal(TestCaseStatuses.Deleted, dtoB.Status);
        Assert.Equal(5, dtoB.Position);

        var savedA = await LoadCaseAsync(a.Id);
        var savedB = await LoadCaseAsync(b.Id);
        Assert.Equal("A2", savedA.Title);
        Assert.Equal("P0", savedA.Priority);
        Assert.Equal(Now, savedA.UpdatedAt);
        Assert.Equal("B", savedB.Title);
        Assert.Equal(TestCaseStatuses.Deleted, savedB.Status);
        Assert.Equal(5, savedB.Position);
        Assert.Equal(Now, savedB.UpdatedAt);
        Assert.Equal(Now, (await LoadPlanAsync(tree.Plan.Id)).UpdatedAt);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(TSH.ProjectId, PermissionKeys.TestCaseUpdate, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Single item -> 200 with one case
    [Fact]
    public async Task BulkUpdateTestCasesAsync_WithSingleItem_UpdatesIt()
    {
        var tree = SetupValidPlan();
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);

        var result = await TestingService.BulkUpdateTestCasesAsync(tree.Plan.Id, Bulk((a.Id, new UpdateTestCaseRequest { Browser = "Edge" })), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var dto = Assert.Single(result.Data!.Items);
        Assert.Equal("Edge", dto.Browser);
        Assert.Equal("Edge", (await LoadCaseAsync(a.Id)).Browser);
    }

    // UTC03: Bulk move to another section changes SectionId/ModuleId of the moved cases only
    [Fact]
    public async Task BulkUpdateTestCasesAsync_WithSectionMove_MovesOnlyTargetedCases()
    {
        var tree = SetupValidPlan();
        var module2 = TSH.AddModule(Db, tree.Plan, "PAY", "Payment");
        var section2 = TSH.AddSection(Db, module2, "Checkout");
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);
        var b = TSH.AddCase(Db, tree, "TC_2", "B", 2);

        var result = await TestingService.BulkUpdateTestCasesAsync(tree.Plan.Id, Bulk((a.Id, new UpdateTestCaseRequest { SectionId = section2.Id })), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(section2.Id, result.Data!.Items.Single().SectionId);
        Assert.Equal(module2.Id, result.Data.Items.Single().ModuleId);
        var savedA = await LoadCaseAsync(a.Id);
        var savedB = await LoadCaseAsync(b.Id);
        Assert.Equal(section2.Id, savedA.SectionId);
        Assert.Equal(module2.Id, savedA.ModuleId);
        Assert.Equal(tree.Section.Id, savedB.SectionId);
        Assert.Equal(TSH.OldDate, savedB.UpdatedAt);
    }

    // UTC04: Bulk requirement re-link replaces links of the targeted case
    [Fact]
    public async Task BulkUpdateTestCasesAsync_WithRequirementRefs_ReplacesLinks()
    {
        var tree = SetupValidPlan();
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);
        var doc = TSH.AddDocumentWithRequirement(Db);

        var result = await TestingService.BulkUpdateTestCasesAsync(tree.Plan.Id, Bulk((a.Id, new UpdateTestCaseRequest
        {
            RequirementRefs = new List<RequirementRefInputDto> { new() { DocumentId = doc.Id, ReqUid = "req-001" } }
        })), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var req = Assert.Single(result.Data!.Items.Single().Requirements);
        Assert.Equal("SRS-1:REQ-001", req.ReqKey);
        var link = await Db.TestCaseRequirementLinks.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(a.Id, link.TestCaseId);
        Assert.Equal("hash-1", link.ReqHash);
    }

    // UTC05: Cell style changes are applied in bulk
    [Fact]
    public async Task BulkUpdateTestCasesAsync_WithCellStyles_AppliesStyles()
    {
        var tree = SetupValidPlan();
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);

        var result = await TestingService.BulkUpdateTestCasesAsync(tree.Plan.Id, Bulk((a.Id, new UpdateTestCaseRequest
        {
            CellStyles = new Dictionary<string, CellStyleDto?> { ["title"] = new CellStyleDto { B = true, Fill = "#FFFF00" } }
        })), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var style = result.Data!.Items.Single().CellStyles["title"];
        Assert.True(style.B);
        Assert.Equal("#ffff00", style.Fill);
    }

    // UTC06: Same case listed twice -> both changes applied in order (last wins) and the case is returned once
    [Fact]
    public async Task BulkUpdateTestCasesAsync_WithDuplicateCaseIds_AppliesInOrderAndReturnsCaseOnce()
    {
        var tree = SetupValidPlan();
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);

        var result = await TestingService.BulkUpdateTestCasesAsync(tree.Plan.Id, Bulk(
            (a.Id, new UpdateTestCaseRequest { Title = "first", Browser = "Chrome 1" }),
            (a.Id, new UpdateTestCaseRequest { Title = "second" })), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var dto = Assert.Single(result.Data!.Items);
        Assert.Equal("second", dto.Title);
        Assert.Equal("Chrome 1", dto.Browser);
    }

    // UTC07: Ids not in this plan (other plan / unknown) are ignored: not changed and not returned
    [Fact]
    public async Task BulkUpdateTestCasesAsync_WithCasesOfOtherPlanOrUnknown_IgnoresThem()
    {
        var tree = SetupValidPlan();
        var mine = TSH.AddCase(Db, tree, "TC_1", "Mine", 1);
        var otherTree = TSH.AddPlanTree(Db);
        var foreign = TSH.AddCase(Db, otherTree, "TC_1", "Foreign", 1);

        var result = await TestingService.BulkUpdateTestCasesAsync(tree.Plan.Id, Bulk(
            (mine.Id, new UpdateTestCaseRequest { Title = "Mine2" }),
            (foreign.Id, new UpdateTestCaseRequest { Title = "Hacked" }),
            (9999, new UpdateTestCaseRequest { Title = "Ghost" })), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var dto = Assert.Single(result.Data!.Items);
        Assert.Equal(mine.Id, dto.TestCaseId);
        Assert.Equal("Mine2", dto.Title);
        Assert.Equal("Foreign", (await LoadCaseAsync(foreign.Id)).Title);
        Assert.Equal(TSH.OldDate, (await LoadCaseAsync(foreign.Id)).UpdatedAt);
    }

    // UTC08: Item whose Changes is null is skipped, case is still returned unchanged
    [Fact]
    public async Task BulkUpdateTestCasesAsync_WithNullChanges_SkipsItemButReturnsCase()
    {
        var tree = SetupValidPlan();
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);
        var request = new BulkUpdateTestCasesRequest { Items = new List<BulkUpdateTestCaseItemDto> { new() { TestCaseId = a.Id, Changes = null! } } };

        var result = await TestingService.BulkUpdateTestCasesAsync(tree.Plan.Id, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var dto = Assert.Single(result.Data!.Items);
        Assert.Equal("A", dto.Title);
        Assert.Equal(TSH.OldDate, (await LoadCaseAsync(a.Id)).UpdatedAt);
        Assert.Equal(Now, (await LoadPlanAsync(tree.Plan.Id)).UpdatedAt);
    }

    // UTC09: Empty Items list -> 200 with empty result, plan not touched
    [Fact]
    public async Task BulkUpdateTestCasesAsync_WithEmptyItems_ReturnsEmptyListWithoutTouchingPlan()
    {
        var tree = SetupValidPlan();

        var result = await TestingService.BulkUpdateTestCasesAsync(tree.Plan.Id, new BulkUpdateTestCasesRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data.Items);
        Assert.Equal(TSH.OldDate, (await LoadPlanAsync(tree.Plan.Id)).UpdatedAt);
    }

    // UTC10: Null Items -> treated as empty, 200
    [Fact]
    public async Task BulkUpdateTestCasesAsync_WithNullItems_ReturnsEmptyList()
    {
        var tree = SetupValidPlan();

        var result = await TestingService.BulkUpdateTestCasesAsync(tree.Plan.Id, new BulkUpdateTestCasesRequest { Items = null! }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Items);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC11: Plan not found -> 404 TEST_PLAN_NOT_FOUND, guard not called
    [Fact]
    public async Task BulkUpdateTestCasesAsync_WhenPlanNotFound_ReturnsNotFound()
    {
        SetupCurrentUser(1);

        var result = await TestingService.BulkUpdateTestCasesAsync(9999, Bulk((1, new UpdateTestCaseRequest { Title = "x" })), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_PLAN_NOT_FOUND", result.ErrorCode);
        Assert.Equal("Test plan not found.", result.ErrorMessage);
        VerifyGuardNeverCalled();
    }

    // UTC12: Unauthenticated -> 401, nothing changed
    [Fact]
    public async Task BulkUpdateTestCasesAsync_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var tree = TSH.AddPlanTree(Db);
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);
        SetupGuardUnauthenticated(TSH.ProjectId, PermissionKeys.TestCaseUpdate);

        var result = await TestingService.BulkUpdateTestCasesAsync(tree.Plan.Id, Bulk((a.Id, new UpdateTestCaseRequest { Title = "x" })), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Equal("A", (await LoadCaseAsync(a.Id)).Title);
    }

    // UTC13: Permission denied -> 403, nothing changed
    [Fact]
    public async Task BulkUpdateTestCasesAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        var tree = TSH.AddPlanTree(Db);
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);
        SetupGuardDenies(TSH.ProjectId, PermissionKeys.TestCaseUpdate);

        var result = await TestingService.BulkUpdateTestCasesAsync(tree.Plan.Id, Bulk((a.Id, new UpdateTestCaseRequest { Title = "x" })), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.TestCaseUpdate, result.RequiredPermission);
        Assert.Equal("A", (await LoadCaseAsync(a.Id)).Title);
        Assert.Equal(TSH.OldDate, (await LoadPlanAsync(tree.Plan.Id)).UpdatedAt);
    }

    // UTC14: Plan handed over -> 400 PLAN_HANDED_OVER, nothing changed (even with empty items the status check comes first)
    [Fact]
    public async Task BulkUpdateTestCasesAsync_WhenPlanHandedOver_ReturnsBadRequest()
    {
        var tree = SetupValidPlan(TestPlanStatuses.HandedOver);
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);

        var result = await TestingService.BulkUpdateTestCasesAsync(tree.Plan.Id, Bulk((a.Id, new UpdateTestCaseRequest { Title = "x" })), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("PLAN_HANDED_OVER", result.ErrorCode);
        Assert.Equal("A", (await LoadCaseAsync(a.Id)).Title);

        var emptyResult = await TestingService.BulkUpdateTestCasesAsync(tree.Plan.Id, new BulkUpdateTestCasesRequest(), TestContext.Current.CancellationToken);
        Assert.Equal("PLAN_HANDED_OVER", emptyResult.ErrorCode);
    }

    // UTC15: Only ids from other plans -> 200 with empty items (nothing found, nothing failed)
    [Fact]
    public async Task BulkUpdateTestCasesAsync_WhenNoItemBelongsToPlan_ReturnsEmptyList()
    {
        var tree = SetupValidPlan();
        var otherTree = TSH.AddPlanTree(Db);
        var foreign = TSH.AddCase(Db, otherTree, "TC_1", "Foreign", 1);

        var result = await TestingService.BulkUpdateTestCasesAsync(tree.Plan.Id, Bulk((foreign.Id, new UpdateTestCaseRequest { Title = "x" })), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Items);
        Assert.Equal("Foreign", (await LoadCaseAsync(foreign.Id)).Title);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC16: PlanId = 0 -> 404 TEST_PLAN_NOT_FOUND
    [Fact]
    public async Task BulkUpdateTestCasesAsync_WithPlanIdZero_ReturnsNotFound()
    {
        var result = await TestingService.BulkUpdateTestCasesAsync(0, new BulkUpdateTestCasesRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_PLAN_NOT_FOUND", result.ErrorCode);
    }

    // UTC17: Negative PlanId -> 404 TEST_PLAN_NOT_FOUND
    [Fact]
    public async Task BulkUpdateTestCasesAsync_WithNegativePlanId_ReturnsNotFound()
    {
        var result = await TestingService.BulkUpdateTestCasesAsync(-1, new BulkUpdateTestCasesRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_PLAN_NOT_FOUND", result.ErrorCode);
    }

    // UTC18: Large batch (50 cases) is processed in one call
    [Fact]
    public async Task BulkUpdateTestCasesAsync_WithFiftyItems_UpdatesAll()
    {
        var tree = SetupValidPlan();
        var ids = Enumerable.Range(1, 50).Select(i => TSH.AddCase(Db, tree, $"TC_{i}", $"Case {i}", i).Id).ToList();

        var result = await TestingService.BulkUpdateTestCasesAsync(tree.Plan.Id,
            new BulkUpdateTestCasesRequest { Items = ids.Select(id => new BulkUpdateTestCaseItemDto { TestCaseId = id, Changes = new UpdateTestCaseRequest { Priority = "LOW" } }).ToList() },
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(50, result.Data!.Items.Count);
        Assert.All(result.Data.Items, i => Assert.Equal("P2", i.Priority));
        Assert.Equal(50, await Db.TestCases.AsNoTracking().CountAsync(c => c.Priority == "P2", TestContext.Current.CancellationToken));
    }
}
