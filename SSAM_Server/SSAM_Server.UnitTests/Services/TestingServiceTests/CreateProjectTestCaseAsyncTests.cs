using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Testing;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.TestingServiceTests;

public class CreateProjectTestCaseAsyncTests : TestingCasesTestBase
{
    private CasesSeedTree SetupValidProjectSection(long projectId = TestingSeedHelpersCases.ProjectId)
    {
        SetupCurrentUser(1);
        SetupGuardAllows(projectId, PermissionKeys.TestCaseCreate);
        return TestingSeedHelpersCases.AddPlanTree(Db, projectId);
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Section belongs to the project -> delegates to CreateTestCaseAsync and returns 201 with the created case
    [Fact]
    public async Task CreateProjectTestCaseAsync_WithSectionInProject_ReturnsCreatedTestCase()
    {
        var tree = SetupValidProjectSection();
        var request = new CreateTestCaseRequest { SectionId = tree.Section.Id, Title = "  Login with valid account  " };

        var result = await TestingService.CreateProjectTestCaseAsync(TestingSeedHelpersCases.ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(tree.Plan.Id, result.Data.PlanId);
        Assert.Equal(tree.Module.Id, result.Data.ModuleId);
        Assert.Equal(tree.Section.Id, result.Data.SectionId);
        Assert.Equal("TC_1", result.Data.Code);
        Assert.Equal("LOG-1", result.Data.ModuleCode);
        Assert.Equal("Login with valid account", result.Data.Title);
        Assert.Equal(TestCaseStatuses.Active, result.Data.Status);

        var saved = await Db.TestCases.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(result.Data.TestCaseId, saved.Id);
        Assert.Equal(TestingSeedHelpersCases.ProjectId, saved.ProjectId);
        Assert.Equal(tree.Plan.Id, saved.PlanId);
        Assert.Equal("Login with valid account", saved.Title);

        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(TestingSeedHelpersCases.ProjectId, PermissionKeys.TestCaseCreate, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Project has several plans -> case is created in the plan that owns the section
    [Fact]
    public async Task CreateProjectTestCaseAsync_WithMultiplePlans_CreatesCaseInPlanOwningSection()
    {
        var treeA = SetupValidProjectSection();
        var treeB = TestingSeedHelpersCases.AddPlanTree(Db);
        var request = new CreateTestCaseRequest { SectionId = treeB.Section.Id, Title = "In plan B" };

        var result = await TestingService.CreateProjectTestCaseAsync(TestingSeedHelpersCases.ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(treeB.Plan.Id, result.Data!.PlanId);
        Assert.Equal(0, await Db.TestCases.CountAsync(c => c.PlanId == treeA.Plan.Id, TestContext.Current.CancellationToken));
        Assert.Equal(1, await Db.TestCases.CountAsync(c => c.PlanId == treeB.Plan.Id, TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC03: Section id does not exist -> 404 SECTION_NOT_FOUND, no permission check, nothing persisted
    [Fact]
    public async Task CreateProjectTestCaseAsync_WhenSectionDoesNotExist_ReturnsNotFound()
    {
        SetupValidProjectSection();

        var result = await TestingService.CreateProjectTestCaseAsync(TestingSeedHelpersCases.ProjectId, new CreateTestCaseRequest { SectionId = 9999 }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("SECTION_NOT_FOUND", result.ErrorCode);
        Assert.Equal("Test section not found in project.", result.ErrorMessage);
        Assert.Empty(Db.TestCases);
        VerifyGuardNeverCalled();
    }

    // UTC04: Section belongs to a different project -> 404 SECTION_NOT_FOUND
    [Fact]
    public async Task CreateProjectTestCaseAsync_WhenSectionBelongsToAnotherProject_ReturnsNotFound()
    {
        SetupValidProjectSection();
        var other = TestingSeedHelpersCases.AddPlanTree(Db, projectId: 200);

        var result = await TestingService.CreateProjectTestCaseAsync(TestingSeedHelpersCases.ProjectId, new CreateTestCaseRequest { SectionId = other.Section.Id }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("SECTION_NOT_FOUND", result.ErrorCode);
        Assert.Empty(Db.TestCases);
        VerifyGuardNeverCalled();
    }

    // UTC05: User has no testcase.manage permission -> 403 propagated from the delegate, nothing persisted
    [Fact]
    public async Task CreateProjectTestCaseAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        SetupCurrentUser(1);
        SetupGuardDenies(TestingSeedHelpersCases.ProjectId, PermissionKeys.TestCaseCreate);
        var tree = TestingSeedHelpersCases.AddPlanTree(Db);

        var result = await TestingService.CreateProjectTestCaseAsync(TestingSeedHelpersCases.ProjectId, new CreateTestCaseRequest { SectionId = tree.Section.Id }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.TestCaseCreate, result.RequiredPermission);
        Assert.Empty(Db.TestCases);
        Assert.Empty(Db.TestActivities);
    }

    // UTC06: Unauthenticated caller -> 401 propagated from the access guard, nothing persisted
    [Fact]
    public async Task CreateProjectTestCaseAsync_WhenUnauthenticated_ReturnsUnauthorized()
    {
        SetupGuardUnauthenticated(TestingSeedHelpersCases.ProjectId, PermissionKeys.TestCaseCreate);
        var tree = TestingSeedHelpersCases.AddPlanTree(Db);

        var result = await TestingService.CreateProjectTestCaseAsync(TestingSeedHelpersCases.ProjectId, new CreateTestCaseRequest { SectionId = tree.Section.Id }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Empty(Db.TestCases);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC07: SectionId = 0 -> 404 SECTION_NOT_FOUND
    [Fact]
    public async Task CreateProjectTestCaseAsync_WithSectionIdZero_ReturnsNotFound()
    {
        SetupValidProjectSection();

        var result = await TestingService.CreateProjectTestCaseAsync(TestingSeedHelpersCases.ProjectId, new CreateTestCaseRequest { SectionId = 0 }, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("SECTION_NOT_FOUND", result.ErrorCode);
    }

    // UTC08: SectionId negative -> 404 SECTION_NOT_FOUND
    [Fact]
    public async Task CreateProjectTestCaseAsync_WithNegativeSectionId_ReturnsNotFound()
    {
        SetupValidProjectSection();

        var result = await TestingService.CreateProjectTestCaseAsync(TestingSeedHelpersCases.ProjectId, new CreateTestCaseRequest { SectionId = -1 }, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("SECTION_NOT_FOUND", result.ErrorCode);
    }

    // UTC09: ProjectId = 0 (no such project) -> 404 SECTION_NOT_FOUND even though the section exists elsewhere
    [Fact]
    public async Task CreateProjectTestCaseAsync_WithProjectIdZero_ReturnsNotFound()
    {
        var tree = SetupValidProjectSection();

        var result = await TestingService.CreateProjectTestCaseAsync(0, new CreateTestCaseRequest { SectionId = tree.Section.Id }, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("SECTION_NOT_FOUND", result.ErrorCode);
        Assert.Empty(Db.TestCases);
    }
}
