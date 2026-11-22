using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.DTOs.Testing;
using SSAM_Server.Models;
using SSAM_Server.Security;
using TSH = SSAM_Server.UnitTests.Services.TestingServiceTests.TestingSeedHelpersCases;

namespace SSAM_Server.UnitTests.Services.TestingServiceTests;

public class CreateTestCaseAsyncTests : TestingCasesTestBase
{
    private CasesSeedTree SetupValidPlanAndSection(string planStatus = TestPlanStatuses.Open)
    {
        SetupCurrentUser(1);
        SetupGuardAllows(TSH.ProjectId, PermissionKeys.TestCaseCreate);
        return TSH.AddPlanTree(Db, TSH.ProjectId, planStatus);
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Create test case with every field populated -> 201, DTO + DB + activity + plan/module bookkeeping all correct
    [Fact]
    public async Task CreateTestCaseAsync_WithFullValidRequest_ReturnsCreatedAndPersistsEverything()
    {
        var tree = SetupValidPlanAndSection();
        var request = new CreateTestCaseRequest
        {
            SectionId = tree.Section.Id,
            Title = "  Login with valid credentials  ",
            Preconditions = "  User exists  ",
            Steps = new List<TestStepDto>
            {
                new() { Action = "Open page", Expected = "Form shown" },
                new() { Action = "Submit", Expected = "Dashboard shown" }
            },
            Actual = "  Works  ",
            AiSuggestedReqs = true,
            Priority = "high",
            Category = "  Functional  ",
            Browser = "  Chrome  ",
            CustomFields = new Dictionary<string, object?> { ["env"] = "qa", ["n"] = 3 }
        };

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        var dto = result.Data!;
        Assert.True(dto.TestCaseId > 0);
        Assert.Equal(tree.Plan.Id, dto.PlanId);
        Assert.Equal(tree.Module.Id, dto.ModuleId);
        Assert.Equal(tree.Section.Id, dto.SectionId);
        Assert.Equal("TC_1", dto.Code);
        Assert.Equal("LOG-1", dto.ModuleCode);
        Assert.Equal("Login with valid credentials", dto.Title);
        Assert.Equal("User exists", dto.Preconditions);
        Assert.Equal("Works", dto.Actual);
        Assert.True(dto.AiSuggestedReqs);
        Assert.Equal("P0", dto.Priority);
        Assert.Equal("Functional", dto.Category);
        Assert.Equal("Chrome", dto.Browser);
        Assert.Equal(TestCaseStatuses.Active, dto.Status);
        Assert.Equal(1, dto.Position);
        Assert.Equal(2, dto.Steps.Count);
        Assert.Equal("Open page", dto.Steps[0].Action);
        Assert.Equal("Form shown", dto.Steps[0].Expected);
        Assert.Equal("Submit", dto.Steps[1].Action);
        Assert.Equal(2, dto.CustomFields.Count);
        Assert.True(dto.CustomFields.ContainsKey("env"));
        Assert.True(dto.CustomFields.ContainsKey("n"));
        Assert.Empty(dto.Requirements);
        Assert.Empty(dto.Results);
        Assert.Empty(dto.Retest);
        Assert.Null(dto.Bug);
        Assert.Equal(NowIso, dto.UpdatedAt);

        var saved = await Db.TestCases.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(TSH.ProjectId, saved.ProjectId);
        Assert.Equal(tree.Plan.Id, saved.PlanId);
        Assert.Equal(tree.Module.Id, saved.ModuleId);
        Assert.Equal(tree.Section.Id, saved.SectionId);
        Assert.Equal("TC_1", saved.Code);
        Assert.Equal("Login with valid credentials", saved.Title);
        Assert.Equal("User exists", saved.Preconditions);
        Assert.Equal("Works", saved.Actual);
        Assert.Equal("P0", saved.Priority);
        Assert.Equal("Functional", saved.Category);
        Assert.Equal("Chrome", saved.Browser);
        Assert.Equal(TestCaseStatuses.Active, saved.Status);
        Assert.Equal(1, saved.Position);
        Assert.Equal(1, saved.CreatedById);
        Assert.Equal(Now, saved.CreatedAt);
        Assert.Equal(Now, saved.UpdatedAt);
        Assert.Contains("\"action\":\"Open page\"", saved.Steps);
        Assert.Contains("\"expected\":\"Dashboard shown\"", saved.Steps);
        Assert.Contains("\"env\":\"qa\"", saved.CustomFields);
        Assert.Contains("\"n\":3", saved.CustomFields);
        Assert.Null(saved.BugIssueId);

        var activity = await Db.TestActivities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(tree.Plan.Id, activity.PlanId);
        Assert.Equal(TestActivityTypes.TestCase, activity.Type);
        Assert.Equal(Now, activity.At);
        Assert.Equal(1, activity.ActorId);
        Assert.Equal(tree.Module.Id, activity.ModuleId);
        Assert.Equal(saved.Id, activity.TestCaseId);
        Assert.Equal("Created TC_1 · Login with valid credentials", activity.Text);

        var plan = await Db.TestPlans.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(Now, plan.UpdatedAt);
        var module = await Db.TestModules.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, module.CaseSeq);

        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(TSH.ProjectId, PermissionKeys.TestCaseCreate, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Create with only SectionId (all optional fields null) -> defaults applied (empty strings, P1, no steps)
    [Fact]
    public async Task CreateTestCaseAsync_WithOnlySectionId_AppliesDefaults()
    {
        var tree = SetupValidPlanAndSection();

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = tree.Section.Id }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        var dto = result.Data!;
        Assert.Equal(string.Empty, dto.Title);
        Assert.Null(dto.Preconditions);
        Assert.Null(dto.Actual);
        Assert.Null(dto.Category);
        Assert.Null(dto.Browser);
        Assert.False(dto.AiSuggestedReqs);
        Assert.Equal("P1", dto.Priority);
        Assert.Empty(dto.Steps);
        Assert.Empty(dto.CustomFields);
        Assert.Empty(dto.Requirements);

        var saved = await Db.TestCases.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(string.Empty, saved.Title);
        Assert.Equal(string.Empty, saved.Preconditions);
        Assert.Equal(string.Empty, saved.Actual);
        Assert.Equal(string.Empty, saved.Category);
        Assert.Equal(string.Empty, saved.Browser);
        Assert.Equal("[]", saved.Steps);
        Assert.Equal("{}", saved.CustomFields);
        Assert.Equal("P1", saved.Priority);
        Assert.False(saved.AiSuggestedReqs);
    }

    // UTC03: Category omitted and section is a child section -> category defaults to the section title
    [Fact]
    public async Task CreateTestCaseAsync_WithNullCategoryInChildSection_UsesSectionTitleAsCategory()
    {
        var tree = SetupValidPlanAndSection();
        var child = TSH.AddSection(Db, tree.Module, "Password rules", tree.Section.Id, 2);

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = child.Id, Title = "T" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Password rules", result.Data!.Category);
        Assert.Equal("Password rules", (await Db.TestCases.SingleAsync(TestContext.Current.CancellationToken)).Category);
    }

    // UTC04: Category omitted and section is top-level -> category is empty (DTO null)
    [Fact]
    public async Task CreateTestCaseAsync_WithNullCategoryInTopLevelSection_LeavesCategoryEmpty()
    {
        var tree = SetupValidPlanAndSection();

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = tree.Section.Id }, TestContext.Current.CancellationToken);

        Assert.Null(result.Data!.Category);
        Assert.Equal(string.Empty, (await Db.TestCases.SingleAsync(TestContext.Current.CancellationToken)).Category);
    }

    // UTC05: Explicit empty category in a child section is respected (no fallback to section title)
    [Fact]
    public async Task CreateTestCaseAsync_WithEmptyCategoryInChildSection_DoesNotFallBackToSectionTitle()
    {
        var tree = SetupValidPlanAndSection();
        var child = TSH.AddSection(Db, tree.Module, "Child", tree.Section.Id, 2);

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = child.Id, Category = "   " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.Category);
        Assert.Equal(string.Empty, (await Db.TestCases.SingleAsync(TestContext.Current.CancellationToken)).Category);
    }

    // UTC06: Priority is normalised to P0..P3 (HIGH/P0->P0, MEDIUM/P1/unknown/null->P1, LOW/P2->P2, P3->P3, trimmed & case-insensitive)
    [Theory]
    [InlineData("HIGH", "P0")]        // UTC06a
    [InlineData("P0", "P0")]          // UTC06b
    [InlineData("  high  ", "P0")]    // UTC06c
    [InlineData("MEDIUM", "P1")]      // UTC06d
    [InlineData("P1", "P1")]          // UTC06e
    [InlineData("whatever", "P1")]    // UTC06f
    [InlineData("", "P1")]            // UTC06g
    [InlineData(null, "P1")]          // UTC06h
    [InlineData("LOW", "P2")]         // UTC06i
    [InlineData("p2", "P2")]          // UTC06j
    [InlineData("P3", "P3")]          // UTC06k
    public async Task CreateTestCaseAsync_NormalizesPriority(string? input, string expected)
    {
        var tree = SetupValidPlanAndSection();

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = tree.Section.Id, Priority = input }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Data!.Priority);
        Assert.Equal(expected, (await Db.TestCases.SingleAsync(TestContext.Current.CancellationToken)).Priority);
    }

    // UTC07: First case in a plan with no cases gets code TC_1 and position 1
    [Fact]
    public async Task CreateTestCaseAsync_InEmptyPlan_AllocatesFirstCodeAndPosition()
    {
        var tree = SetupValidPlanAndSection();

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = tree.Section.Id }, TestContext.Current.CancellationToken);

        Assert.Equal("TC_1", result.Data!.Code);
        Assert.Equal(1, result.Data.Position);
    }

    // UTC08: Existing codes TC_1 and TC_5 -> next code is TC_6 (max + 1, not count + 1)
    [Fact]
    public async Task CreateTestCaseAsync_WithExistingCodes_AllocatesMaxPlusOne()
    {
        var tree = SetupValidPlanAndSection();
        TSH.AddCase(Db, tree, "TC_1", position: 1);
        TSH.AddCase(Db, tree, "TC_5", position: 2);

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = tree.Section.Id }, TestContext.Current.CancellationToken);

        Assert.Equal("TC_6", result.Data!.Code);
    }

    // UTC09: Existing code in dash/lower-case form "tc-3" is recognised -> next code TC_4
    [Fact]
    public async Task CreateTestCaseAsync_WithDashLowerCaseExistingCode_AllocatesNextNumber()
    {
        var tree = SetupValidPlanAndSection();
        TSH.AddCase(Db, tree, "tc-3", position: 1);

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = tree.Section.Id }, TestContext.Current.CancellationToken);

        Assert.Equal("TC_4", result.Data!.Code);
    }

    // UTC10: Existing codes that do not match TC_n -> falls back to count + 1 (2 cases -> TC_3)
    [Fact]
    public async Task CreateTestCaseAsync_WithNonMatchingExistingCodes_FallsBackToCountPlusOne()
    {
        var tree = SetupValidPlanAndSection();
        TSH.AddCase(Db, tree, "LOGIN-A", position: 1);
        TSH.AddCase(Db, tree, "LOGIN-B", position: 2);

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = tree.Section.Id }, TestContext.Current.CancellationToken);

        Assert.Equal("TC_3", result.Data!.Code);
    }

    // UTC11: Soft-deleted cases still count when allocating the code (no reuse of TC_n)
    [Fact]
    public async Task CreateTestCaseAsync_WithDeletedCaseHoldingHighestCode_DoesNotReuseCode()
    {
        var tree = SetupValidPlanAndSection();
        TSH.AddCase(Db, tree, "TC_2", position: 1, status: TestCaseStatuses.Deleted);

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = tree.Section.Id }, TestContext.Current.CancellationToken);

        Assert.Equal("TC_3", result.Data!.Code);
    }

    // UTC12: Codes of other plans are ignored when allocating the code
    [Fact]
    public async Task CreateTestCaseAsync_WithCasesInOtherPlan_IgnoresThemForCode()
    {
        var tree = SetupValidPlanAndSection();
        var other = TSH.AddPlanTree(Db);
        TSH.AddCase(Db, other, "TC_9", position: 1);

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = tree.Section.Id }, TestContext.Current.CancellationToken);

        Assert.Equal("TC_1", result.Data!.Code);
    }

    // UTC13: Two consecutive creations -> TC_1, TC_2, positions 1, 2 and module CaseSeq = 2
    [Fact]
    public async Task CreateTestCaseAsync_CalledTwice_IncrementsCodePositionAndCaseSeq()
    {
        var tree = SetupValidPlanAndSection();
        var request = new CreateTestCaseRequest { SectionId = tree.Section.Id };

        var first = await TestingService.CreateTestCaseAsync(tree.Plan.Id, request, TestContext.Current.CancellationToken);
        var second = await TestingService.CreateTestCaseAsync(tree.Plan.Id, request, TestContext.Current.CancellationToken);

        Assert.Equal("TC_1", first.Data!.Code);
        Assert.Equal("TC_2", second.Data!.Code);
        Assert.Equal(1, first.Data.Position);
        Assert.Equal(2, second.Data.Position);
        Assert.Equal("LOG-1", first.Data.ModuleCode);
        Assert.Equal("LOG-2", second.Data.ModuleCode);
        Assert.Equal(2, (await Db.TestModules.SingleAsync(TestContext.Current.CancellationToken)).CaseSeq);
        Assert.Equal(2, await Db.TestActivities.CountAsync(TestContext.Current.CancellationToken));
    }

    // UTC14: Existing siblings (positions 1, 2) and no AfterTestCaseId -> appended at max + 1
    [Fact]
    public async Task CreateTestCaseAsync_WithoutAfterCase_AppendsAfterLastSibling()
    {
        var tree = SetupValidPlanAndSection();
        TSH.AddCase(Db, tree, "TC_1", position: 1);
        TSH.AddCase(Db, tree, "TC_2", position: 7);

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = tree.Section.Id }, TestContext.Current.CancellationToken);

        Assert.Equal(8, result.Data!.Position);
    }

    // UTC15: AfterTestCaseId of a middle case -> inserted right after it and later siblings are shifted by one
    [Fact]
    public async Task CreateTestCaseAsync_WithAfterTestCaseId_InsertsAfterAndShiftsFollowingSiblings()
    {
        var tree = SetupValidPlanAndSection();
        var a = TSH.AddCase(Db, tree, "TC_1", position: 1);
        var b = TSH.AddCase(Db, tree, "TC_2", position: 2);
        var c = TSH.AddCase(Db, tree, "TC_3", position: 3);

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = tree.Section.Id, Title = "Inserted", AfterTestCaseId = a.Id }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Position);
        Assert.Equal("TC_4", result.Data.Code);
        Assert.Equal(1, (await Db.TestCases.SingleAsync(x => x.Id == a.Id, TestContext.Current.CancellationToken)).Position);
        Assert.Equal(3, (await Db.TestCases.SingleAsync(x => x.Id == b.Id, TestContext.Current.CancellationToken)).Position);
        Assert.Equal(4, (await Db.TestCases.SingleAsync(x => x.Id == c.Id, TestContext.Current.CancellationToken)).Position);
        // module code reflects the new ordering: a=LOG-1, inserted=LOG-2, b=LOG-3
        Assert.Equal("LOG-2", result.Data.ModuleCode);
    }

    // UTC16: AfterTestCaseId of the last case -> appended at last.Position + 1 and nothing else shifts
    [Fact]
    public async Task CreateTestCaseAsync_WithAfterTestCaseIdOfLastCase_AppendsWithoutShifting()
    {
        var tree = SetupValidPlanAndSection();
        var a = TSH.AddCase(Db, tree, "TC_1", position: 1);
        var b = TSH.AddCase(Db, tree, "TC_2", position: 2);

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = tree.Section.Id, AfterTestCaseId = b.Id }, TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Data!.Position);
        Assert.Equal(1, (await Db.TestCases.SingleAsync(x => x.Id == a.Id, TestContext.Current.CancellationToken)).Position);
        Assert.Equal(2, (await Db.TestCases.SingleAsync(x => x.Id == b.Id, TestContext.Current.CancellationToken)).Position);
    }

    // UTC17: AfterTestCaseId that is not a sibling in the section (unknown id) -> appended at max + 1
    [Fact]
    public async Task CreateTestCaseAsync_WithUnknownAfterTestCaseId_AppendsAtEnd()
    {
        var tree = SetupValidPlanAndSection();
        TSH.AddCase(Db, tree, "TC_1", position: 4);

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = tree.Section.Id, AfterTestCaseId = 9999 }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Data!.Position);
    }

    // UTC18: AfterTestCaseId pointing to a case in another section is not a sibling -> appended at end of target section, other section untouched
    [Fact]
    public async Task CreateTestCaseAsync_WithAfterTestCaseIdFromOtherSection_AppendsAtEndAndLeavesOtherSectionUntouched()
    {
        var tree = SetupValidPlanAndSection();
        var otherSection = TSH.AddSection(Db, tree.Module, "Other", null, 2);
        var foreign = TSH.AddCase(Db, tree, "TC_1", position: 3, section: otherSection);

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = tree.Section.Id, AfterTestCaseId = foreign.Id }, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Data!.Position);
        Assert.Equal(3, (await Db.TestCases.SingleAsync(x => x.Id == foreign.Id, TestContext.Current.CancellationToken)).Position);
    }

    // UTC19: Requirement refs are linked (upper-cased, hash from baseline/current version) and exposed in the DTO
    [Fact]
    public async Task CreateTestCaseAsync_WithValidRequirementRefs_CreatesLinksAndMapsRequirements()
    {
        var tree = SetupValidPlanAndSection();
        var doc = TSH.AddDocumentWithRequirement(Db, TSH.ProjectId, "SRS-1", "REQ-001", "hash-1", "Login requirement");
        var request = new CreateTestCaseRequest
        {
            SectionId = tree.Section.Id,
            RequirementRefs = new List<RequirementRefInputDto> { new() { DocumentId = doc.Id, ReqUid = " req-001 " } }
        };

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var req = Assert.Single(result.Data!.Requirements);
        Assert.Equal(doc.Id, req.DocumentId);
        Assert.Equal("SRS-1", req.DocumentCode);
        Assert.Equal("Software Requirements", req.DocumentTitle);
        Assert.Equal("REQ-001", req.ReqUid);
        Assert.Equal("SRS-1:REQ-001", req.ReqKey);
        Assert.Equal("Login requirement", req.Title);
        Assert.Equal("anchor-REQ-001", req.AnchorId);
        Assert.False(req.Changed);
        Assert.Equal("FRESH", req.Freshness);
        Assert.Null(req.ChangedNote);

        var link = await Db.TestCaseRequirementLinks.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(result.Data.TestCaseId, link.TestCaseId);
        Assert.Equal(doc.Id, link.DocumentId);
        Assert.Equal("REQ-001", link.ReqUid);
        Assert.Equal("hash-1", link.ReqHash);
        Assert.Null(link.ChangedVersionLabel);
        Assert.Equal(Now, link.LinkedAt);
    }

    // UTC20: Document whose requirement lives in the baseline version -> hash is taken from the baseline version
    [Fact]
    public async Task CreateTestCaseAsync_WithDocumentUsingBaselineVersion_LinksUsingBaselineHash()
    {
        var tree = SetupValidPlanAndSection();
        var doc = TSH.AddDocumentWithRequirement(Db, TSH.ProjectId, "SRS-B", "REQ-777", "baseline-hash", useBaseline: true);
        var request = new CreateTestCaseRequest
        {
            SectionId = tree.Section.Id,
            RequirementRefs = new List<RequirementRefInputDto> { new() { DocumentId = doc.Id, ReqUid = "REQ-777" } }
        };

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("baseline-hash", (await Db.TestCaseRequirementLinks.SingleAsync(TestContext.Current.CancellationToken)).ReqHash);
        Assert.Equal("FRESH", result.Data!.Requirements.Single().Freshness);
    }

    // UTC21: Duplicate refs (same doc, differing case/whitespace in ReqUid) are collapsed into a single link
    [Fact]
    public async Task CreateTestCaseAsync_WithDuplicateRequirementRefs_CreatesSingleLink()
    {
        var tree = SetupValidPlanAndSection();
        var doc = TSH.AddDocumentWithRequirement(Db);
        var request = new CreateTestCaseRequest
        {
            SectionId = tree.Section.Id,
            RequirementRefs = new List<RequirementRefInputDto>
            {
                new() { DocumentId = doc.Id, ReqUid = "REQ-001" },
                new() { DocumentId = doc.Id, ReqUid = "req-001" },
                new() { DocumentId = doc.Id, ReqUid = " REQ-001 " }
            }
        };

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Requirements);
        Assert.Equal(1, await Db.TestCaseRequirementLinks.CountAsync(TestContext.Current.CancellationToken));
    }

    // UTC22: Invalid refs are silently skipped (docId 0, blank uid, doc of another project, deleted doc, unknown doc) while valid ones are kept
    [Fact]
    public async Task CreateTestCaseAsync_WithInvalidRequirementRefs_SkipsInvalidAndKeepsValid()
    {
        var tree = SetupValidPlanAndSection();
        var good = TSH.AddDocumentWithRequirement(Db, TSH.ProjectId, "SRS-1", "REQ-001", "h1");
        var otherProject = TSH.AddDocumentWithRequirement(Db, 200, "SRS-X", "REQ-001", "h2");
        var deleted = TSH.AddDocumentWithRequirement(Db, TSH.ProjectId, "SRS-D", "REQ-001", "h3", isDeleted: true);
        var request = new CreateTestCaseRequest
        {
            SectionId = tree.Section.Id,
            RequirementRefs = new List<RequirementRefInputDto>
            {
                new() { DocumentId = 0, ReqUid = "REQ-001" },
                new() { DocumentId = good.Id, ReqUid = "   " },
                new() { DocumentId = otherProject.Id, ReqUid = "REQ-001" },
                new() { DocumentId = deleted.Id, ReqUid = "REQ-001" },
                new() { DocumentId = 9999, ReqUid = "REQ-001" },
                new() { DocumentId = good.Id, ReqUid = "REQ-001" }
            }
        };

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var link = await Db.TestCaseRequirementLinks.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(good.Id, link.DocumentId);
        Assert.Single(result.Data!.Requirements);
    }

    // UTC23: Requirement uid not present in the document version -> link is stored with empty hash and the DTO has no ReqKey/Freshness
    [Fact]
    public async Task CreateTestCaseAsync_WithUnknownReqUidInDocument_LinksWithEmptyHash()
    {
        var tree = SetupValidPlanAndSection();
        var doc = TSH.AddDocumentWithRequirement(Db, TSH.ProjectId, "SRS-1", "REQ-001", "h1");
        var request = new CreateTestCaseRequest
        {
            SectionId = tree.Section.Id,
            RequirementRefs = new List<RequirementRefInputDto> { new() { DocumentId = doc.Id, ReqUid = "REQ-404" } }
        };

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, (await Db.TestCaseRequirementLinks.SingleAsync(TestContext.Current.CancellationToken)).ReqHash);
        var req = result.Data!.Requirements.Single();
        Assert.Equal("REQ-404", req.ReqUid);
        Assert.Null(req.ReqKey);
        Assert.Null(req.Title);
        Assert.Null(req.Freshness);
    }

    // UTC24: Document without any version -> link stored with empty hash
    [Fact]
    public async Task CreateTestCaseAsync_WithDocumentWithoutVersion_LinksWithEmptyHash()
    {
        var tree = SetupValidPlanAndSection();
        var doc = TSH.AddDocumentWithoutVersion(Db);
        var request = new CreateTestCaseRequest
        {
            SectionId = tree.Section.Id,
            RequirementRefs = new List<RequirementRefInputDto> { new() { DocumentId = doc.Id, ReqUid = "REQ-001" } }
        };

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, (await Db.TestCaseRequirementLinks.SingleAsync(TestContext.Current.CancellationToken)).ReqHash);
    }

    // UTC25: Empty RequirementRefs list -> no links created
    [Fact]
    public async Task CreateTestCaseAsync_WithEmptyRequirementRefs_CreatesNoLinks()
    {
        var tree = SetupValidPlanAndSection();

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = tree.Section.Id, RequirementRefs = new List<RequirementRefInputDto>() }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Requirements);
        Assert.Empty(Db.TestCaseRequirementLinks);
    }

    // UTC26: AiSuggestedReqs null/false/true -> only true is stored as true
    [Theory]
    [InlineData(null, false)]   // UTC26a
    [InlineData(false, false)]  // UTC26b
    [InlineData(true, true)]    // UTC26c
    public async Task CreateTestCaseAsync_MapsAiSuggestedReqsFlag(bool? input, bool expected)
    {
        var tree = SetupValidPlanAndSection();

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = tree.Section.Id, AiSuggestedReqs = input }, TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Data!.AiSuggestedReqs);
        Assert.Equal(expected, (await Db.TestCases.SingleAsync(TestContext.Current.CancellationToken)).AiSuggestedReqs);
    }

    // UTC27: Plan that is already handed over still accepts new cases (current behaviour: no status check in CreateTestCaseAsync)
    [Fact]
    public async Task CreateTestCaseAsync_WhenPlanIsHandedOver_StillCreatesCase()
    {
        var tree = SetupValidPlanAndSection(TestPlanStatuses.HandedOver);

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = tree.Section.Id, Title = "Late case" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(1, await Db.TestCases.CountAsync(TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC28: Plan does not exist -> 404 TEST_PLAN_NOT_FOUND, no permission check, nothing persisted
    [Fact]
    public async Task CreateTestCaseAsync_WhenPlanNotFound_ReturnsNotFound()
    {
        SetupCurrentUser(1);

        var result = await TestingService.CreateTestCaseAsync(9999, new CreateTestCaseRequest { SectionId = 1 }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_PLAN_NOT_FOUND", result.ErrorCode);
        Assert.Equal("Test plan not found.", result.ErrorMessage);
        Assert.Null(result.Data);
        VerifyGuardNeverCalled();
        Assert.Empty(Db.TestCases);
        Assert.Empty(Db.TestActivities);
    }

    // UTC29: Unauthenticated user -> 401 propagated from guard, nothing persisted
    [Fact]
    public async Task CreateTestCaseAsync_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var tree = TSH.AddPlanTree(Db);
        SetupGuardUnauthenticated(TSH.ProjectId, PermissionKeys.TestCaseCreate);

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = tree.Section.Id }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Empty(Db.TestCases);
        Assert.Empty(Db.TestActivities);
        Assert.Equal(TSH.OldDate, (await Db.TestPlans.SingleAsync(TestContext.Current.CancellationToken)).UpdatedAt);
    }

    // UTC30: User lacks testcase.manage -> 403 FORBIDDEN with required permission, nothing persisted
    [Fact]
    public async Task CreateTestCaseAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        var tree = TSH.AddPlanTree(Db);
        SetupGuardDenies(TSH.ProjectId, PermissionKeys.TestCaseCreate);

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = tree.Section.Id }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.TestCaseCreate, result.RequiredPermission);
        Assert.Empty(Db.TestCases);
        Assert.Empty(Db.TestActivities);
        Assert.Equal(0, (await Db.TestModules.SingleAsync(TestContext.Current.CancellationToken)).CaseSeq);
    }

    // UTC31: Section id does not exist -> 404 SECTION_NOT_FOUND
    [Fact]
    public async Task CreateTestCaseAsync_WhenSectionNotFound_ReturnsNotFound()
    {
        var tree = SetupValidPlanAndSection();

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = 9999 }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("SECTION_NOT_FOUND", result.ErrorCode);
        Assert.Equal("Test section not found in plan.", result.ErrorMessage);
        Assert.Empty(Db.TestCases);
        Assert.Equal(0, (await Db.TestModules.SingleAsync(TestContext.Current.CancellationToken)).CaseSeq);
    }

    // UTC32: Section belongs to a different plan -> 404 SECTION_NOT_FOUND
    [Fact]
    public async Task CreateTestCaseAsync_WhenSectionBelongsToAnotherPlan_ReturnsNotFound()
    {
        var tree = SetupValidPlanAndSection();
        var other = TSH.AddPlanTree(Db);

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = other.Section.Id }, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("SECTION_NOT_FOUND", result.ErrorCode);
        Assert.Empty(Db.TestCases);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC33: PlanId = 0 -> 404 TEST_PLAN_NOT_FOUND
    [Fact]
    public async Task CreateTestCaseAsync_WithPlanIdZero_ReturnsNotFound()
    {
        var result = await TestingService.CreateTestCaseAsync(0, new CreateTestCaseRequest { SectionId = 1 }, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_PLAN_NOT_FOUND", result.ErrorCode);
    }

    // UTC34: Negative PlanId -> 404 TEST_PLAN_NOT_FOUND
    [Fact]
    public async Task CreateTestCaseAsync_WithNegativePlanId_ReturnsNotFound()
    {
        var result = await TestingService.CreateTestCaseAsync(-1, new CreateTestCaseRequest { SectionId = 1 }, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_PLAN_NOT_FOUND", result.ErrorCode);
    }

    // UTC35: SectionId = 0 -> 404 SECTION_NOT_FOUND
    [Fact]
    public async Task CreateTestCaseAsync_WithSectionIdZero_ReturnsNotFound()
    {
        var tree = SetupValidPlanAndSection();

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = 0 }, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("SECTION_NOT_FOUND", result.ErrorCode);
    }

    // UTC36: Whitespace-only title -> trimmed to empty string (no length/blank validation in service)
    [Fact]
    public async Task CreateTestCaseAsync_WithWhitespaceTitle_StoresEmptyTitle()
    {
        var tree = SetupValidPlanAndSection();

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = tree.Section.Id, Title = "     " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, result.Data!.Title);
        Assert.Equal(string.Empty, (await Db.TestCases.SingleAsync(TestContext.Current.CancellationToken)).Title);
    }

    // UTC37: Single-character title (minimum non-empty) is accepted as-is
    [Fact]
    public async Task CreateTestCaseAsync_WithSingleCharacterTitle_IsAccepted()
    {
        var tree = SetupValidPlanAndSection();

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = tree.Section.Id, Title = "A" }, TestContext.Current.CancellationToken);

        Assert.Equal("A", result.Data!.Title);
    }

    // UTC38: Very long title (10 000 chars) is accepted -> the service enforces no maximum length
    [Fact]
    public async Task CreateTestCaseAsync_WithVeryLongTitle_IsAcceptedWithoutLengthLimit()
    {
        var tree = SetupValidPlanAndSection();
        var title = new string('x', 10_000);

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = tree.Section.Id, Title = title }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(10_000, result.Data!.Title.Length);
        Assert.Equal(title, (await Db.TestCases.SingleAsync(TestContext.Current.CancellationToken)).Title);
    }

    // UTC39: Empty step list and empty custom fields are stored as "[]" / "{}"
    [Fact]
    public async Task CreateTestCaseAsync_WithEmptyStepsAndCustomFields_StoresEmptyJson()
    {
        var tree = SetupValidPlanAndSection();

        var result = await TestingService.CreateTestCaseAsync(tree.Plan.Id, new CreateTestCaseRequest { SectionId = tree.Section.Id, Steps = new List<TestStepDto>(), CustomFields = new Dictionary<string, object?>() }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var saved = await Db.TestCases.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("[]", saved.Steps);
        Assert.Equal("{}", saved.CustomFields);
    }
}
