using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Testing;
using SSAM_Server.Models;
using SSAM_Server.Security;
using H = SSAM_Server.UnitTests.Services.TestingServiceTests.TestingSeedHelpersPlan;

namespace SSAM_Server.UnitTests.Services.TestingServiceTests;

public class CreateModuleAsyncTests : TestingServiceTestBase
{
    private const long ProjectId = 5;
    private const long UserId = 7;

    private TestPlan SetupAuthorizedPlan()
    {
        var plan = H.AddPlan(Db, ProjectId);
        CurrentUserMock.Setup(u => u.UserId).Returns(UserId);
        H.AllowGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseCreate);
        return plan;
    }

    private async Task<List<TestSection>> SectionsOfAsync(long moduleId) =>
        await Db.TestSections.Where(s => s.ModuleId == moduleId).OrderBy(s => s.Position).ToListAsync(TestContext.Current.CancellationToken);

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Module without document, explicit name and short code -> 201, one default section named after module, persisted + activity
    [Fact]
    public async Task CreateModuleAsync_WithoutDocument_CreatesModuleWithDefaultSection()
    {
        var plan = SetupAuthorizedPlan();

        var result = await TestingService.CreateModuleAsync(plan.Id,
            new CreateTestModuleRequest { Name = "  Login  ", ShortCode = "  lg  " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.ModuleId > 0);
        Assert.Equal(0, result.Data.DocumentId);
        Assert.Equal("LG", result.Data.DocumentCode);
        Assert.Equal("Login", result.Data.DocumentTitle);
        Assert.Equal("Login", result.Data.Name);
        Assert.Equal("LG", result.Data.ShortCode);
        Assert.Equal(1, result.Data.Position);
        var secDto = Assert.Single(result.Data.Sections);
        Assert.Equal("1.", secDto.Code);
        Assert.Equal("Login", secDto.Title);
        Assert.Null(secDto.AnchorId);
        Assert.Null(secDto.ParentId);
        Assert.False(secDto.ReqChanged);
        Assert.Equal(1, secDto.Position);

        var module = await Db.TestModules.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(plan.Id, module.PlanId);
        Assert.Null(module.DocumentId);
        Assert.Equal("Login", module.Name);
        Assert.Equal("LG", module.ShortCode);
        Assert.Equal(1, module.Position);
        Assert.Equal(0, module.CaseSeq);

        var section = Assert.Single(await SectionsOfAsync(module.Id));
        Assert.Equal("1.", section.Code);
        Assert.Equal("Login", section.Title);
        Assert.Null(section.ParentId);
        Assert.Null(section.AnchorId);

        var activity = await Db.TestActivities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(plan.Id, activity.PlanId);
        Assert.Equal(TestActivityTypes.Setup, activity.Type);
        Assert.Equal(UserId, activity.ActorId);
        Assert.Equal(module.Id, activity.ModuleId);
        Assert.Equal("Added module Login (DOC-0)", activity.Text);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, activity.At);

        Db.ChangeTracker.Clear();
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, (await Db.TestPlans.SingleAsync(TestContext.Current.CancellationToken)).UpdatedAt);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.TestCaseCreate, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Module linked to a document with default headings (no req headings) -> sections = headings of level <= 2 with numbers stripped
    [Fact]
    public async Task CreateModuleAsync_WithDocumentHeadings_CreatesSectionsFromTopLevelHeadings()
    {
        var plan = SetupAuthorizedPlan();
        var doc = H.AddDocument(Db, ProjectId, "SRS Login", "SRS-1", H.HeadingJson(
            ("a1", "1. Overview", 1, false, null),
            ("a2", "1.1 Scope", 2, false, null),
            ("a3", "Deep detail", 3, false, null)));

        var result = await TestingService.CreateModuleAsync(plan.Id,
            new CreateTestModuleRequest { DocumentId = doc.Id, Name = "Login" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(doc.Id, result.Data!.DocumentId);
        Assert.Equal("SRS-1", result.Data.DocumentCode);
        Assert.Equal("SRS Login", result.Data.DocumentTitle);
        Assert.Equal(new[] { "Overview", "Scope" }, result.Data.Sections.Select(s => s.Title));
        Assert.Equal(new[] { "1.", "2." }, result.Data.Sections.Select(s => s.Code));
        Assert.Equal(new[] { "a1", "a2" }, result.Data.Sections.Select(s => s.AnchorId));
        Assert.Equal(new[] { 1, 2 }, result.Data.Sections.Select(s => s.Position));

        var module = await Db.TestModules.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(doc.Id, module.DocumentId);
        Assert.Equal(2, (await SectionsOfAsync(module.Id)).Count);
        var activity = await Db.TestActivities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Added module Login (SRS-1)", activity.Text);
    }

    // UTC03: Document has requirement headings -> only headings that own requirements become sections
    [Fact]
    public async Task CreateModuleAsync_WhenDocumentHasRequirementHeadings_CreatesOnlySectionsWithRequirements()
    {
        var plan = SetupAuthorizedPlan();
        var doc = H.AddDocument(Db, ProjectId, "SRS", "SRS-2", H.HeadingJson(
            ("h1", "Intro", 1, false, null),
            ("h2", "Features", 1, false, null),
            ("", "REQ Login", 3, true, "REQ-1"),
            ("h3", "Appendix", 1, false, null)));

        var result = await TestingService.CreateModuleAsync(plan.Id,
            new CreateTestModuleRequest { DocumentId = doc.Id }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var section = Assert.Single(result.Data!.Sections);
        Assert.Equal("Features", section.Title);
        Assert.Equal("h2", section.AnchorId);
        Assert.Equal("1.", section.Code);
    }

    // UTC04: AnchorIds filter provided -> only those headings (case-insensitive anchor match) become sections, in document order
    [Fact]
    public async Task CreateModuleAsync_WithAnchorIds_CreatesOnlySelectedSections()
    {
        var plan = SetupAuthorizedPlan();
        var doc = H.AddDocument(Db, ProjectId, "SRS", "SRS-3", H.HeadingJson(
            ("anchor-a", "Alpha", 1, false, null),
            ("anchor-b", "Beta", 3, false, null),
            ("anchor-c", "Gamma", 1, false, null)));

        var result = await TestingService.CreateModuleAsync(plan.Id,
            new CreateTestModuleRequest { DocumentId = doc.Id, AnchorIds = new List<string> { "ANCHOR-C", "anchor-b", "missing" } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "Beta", "Gamma" }, result.Data!.Sections.Select(s => s.Title));
        Assert.Equal(new[] { "1.", "2." }, result.Data.Sections.Select(s => s.Code));
    }

    // UTC05: Empty AnchorIds list -> falls back to the default section selection
    [Fact]
    public async Task CreateModuleAsync_WithEmptyAnchorIds_UsesDefaultSections()
    {
        var plan = SetupAuthorizedPlan();
        var doc = H.AddDocument(Db, ProjectId, "SRS", "SRS-4", H.HeadingJson(
            ("a", "Alpha", 1, false, null), ("b", "Beta", 4, false, null)));

        var result = await TestingService.CreateModuleAsync(plan.Id,
            new CreateTestModuleRequest { DocumentId = doc.Id, AnchorIds = new List<string>() }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "Alpha" }, result.Data!.Sections.Select(s => s.Title));
    }

    // UTC06: AnchorIds match nothing -> no document sections, falls back to the single default section named after the module
    [Fact]
    public async Task CreateModuleAsync_WhenAnchorIdsMatchNothing_CreatesDefaultSection()
    {
        var plan = SetupAuthorizedPlan();
        var doc = H.AddDocument(Db, ProjectId, "SRS", "SRS-5", H.HeadingJson(("a", "Alpha", 1, false, null)));

        var result = await TestingService.CreateModuleAsync(plan.Id,
            new CreateTestModuleRequest { DocumentId = doc.Id, Name = "Mod", AnchorIds = new List<string> { "nope" } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var section = Assert.Single(result.Data!.Sections);
        Assert.Equal("Mod", section.Title);
        Assert.Null(section.AnchorId);
    }

    // UTC07: Only deep headings (level > 2) and no requirements -> all headings are used
    [Fact]
    public async Task CreateModuleAsync_WhenAllHeadingsAreDeep_UsesAllHeadings()
    {
        var plan = SetupAuthorizedPlan();
        var doc = H.AddDocument(Db, ProjectId, "SRS", "SRS-6", H.HeadingJson(
            ("d1", "Deep one", 3, false, null), ("d2", "Deep two", 4, false, null)));

        var result = await TestingService.CreateModuleAsync(plan.Id,
            new CreateTestModuleRequest { DocumentId = doc.Id }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "Deep one", "Deep two" }, result.Data!.Sections.Select(s => s.Title));
    }

    // UTC08: Baseline version has priority over current version when extracting headings
    [Fact]
    public async Task CreateModuleAsync_WhenDocumentHasBaselineVersion_UsesBaselineHeadings()
    {
        var plan = SetupAuthorizedPlan();
        var doc = H.AddDocument(Db, ProjectId, "SRS", "SRS-7", H.HeadingJson(("cur", "Current heading", 1, false, null)));
        var baseline = new DocumentVersion { DocumentId = doc.Id, UpdatedById = 1, AuthorId = 1, Content = H.HeadingJson(("base", "Baseline heading", 1, false, null)) };
        Db.DocumentVersions.Add(baseline);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var tracked = await Db.DocumentNodes.SingleAsync(d => d.Id == doc.Id, TestContext.Current.CancellationToken);
        tracked.BaselineVersionId = baseline.Id;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await TestingService.CreateModuleAsync(plan.Id,
            new CreateTestModuleRequest { DocumentId = doc.Id }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var section = Assert.Single(result.Data!.Sections);
        Assert.Equal("Baseline heading", section.Title);
        Assert.Equal("base", section.AnchorId);
    }

    // UTC09: Document without any version -> default single section named after the module
    [Fact]
    public async Task CreateModuleAsync_WhenDocumentHasNoVersion_CreatesDefaultSection()
    {
        var plan = SetupAuthorizedPlan();
        var doc = H.AddDocument(Db, ProjectId, "Empty doc", "SRS-8", content: null);

        var result = await TestingService.CreateModuleAsync(plan.Id,
            new CreateTestModuleRequest { DocumentId = doc.Id }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Empty doc", result.Data!.Name);
        var section = Assert.Single(result.Data.Sections);
        Assert.Equal("Empty doc", section.Title);
    }

    // UTC10: Document version has no headings (or invalid JSON) -> default single section
    [Theory]
    [InlineData("{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\"}]}")] // UTC10a
    [InlineData("not json at all")]                                            // UTC10b
    public async Task CreateModuleAsync_WhenDocumentContentHasNoHeadings_CreatesDefaultSection(string content)
    {
        var plan = SetupAuthorizedPlan();
        var doc = H.AddDocument(Db, ProjectId, "Doc", "SRS-9", content);

        var result = await TestingService.CreateModuleAsync(plan.Id,
            new CreateTestModuleRequest { DocumentId = doc.Id, Name = "Named" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var section = Assert.Single(result.Data!.Sections);
        Assert.Equal("Named", section.Title);
    }

    // UTC11: Name omitted and no document -> name falls back to "Module"; short code generated as "MOD"
    [Theory]
    [InlineData(null)]  // UTC11a
    [InlineData("")]    // UTC11b
    [InlineData("   ")] // UTC11c
    public async Task CreateModuleAsync_WhenNameBlankAndNoDocument_UsesModuleFallbackName(string? name)
    {
        var plan = SetupAuthorizedPlan();

        var result = await TestingService.CreateModuleAsync(plan.Id, new CreateTestModuleRequest { Name = name }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Module", result.Data!.Name);
        Assert.Equal("MODU", result.Data.ShortCode);
        Assert.Equal("Module", Assert.Single(result.Data.Sections).Title);
    }

    // UTC12: DocumentId 0 or negative is ignored like "no document"
    [Theory]
    [InlineData(0)]  // UTC12a
    [InlineData(-5)] // UTC12b
    public async Task CreateModuleAsync_WhenDocumentIdNotPositive_IgnoresDocument(long documentId)
    {
        var plan = SetupAuthorizedPlan();

        var result = await TestingService.CreateModuleAsync(plan.Id,
            new CreateTestModuleRequest { DocumentId = documentId, Name = "Free" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Data!.DocumentId);
        Assert.Null((await Db.TestModules.SingleAsync(TestContext.Current.CancellationToken)).DocumentId);
    }

    // UTC13: Document without Code -> activity text and DTO use "DOC-{id}"
    [Fact]
    public async Task CreateModuleAsync_WhenDocumentHasNoCode_UsesDocIdFallbackInActivityAndDto()
    {
        var plan = SetupAuthorizedPlan();
        var doc = H.AddDocument(Db, ProjectId, "NoCode", null, content: null);

        var result = await TestingService.CreateModuleAsync(plan.Id,
            new CreateTestModuleRequest { DocumentId = doc.Id, Name = "M" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal($"DOC-{doc.Id}", result.Data!.DocumentCode);
        Assert.Equal($"Added module M (DOC-{doc.Id})", (await Db.TestActivities.SingleAsync(TestContext.Current.CancellationToken)).Text);
    }

    // UTC14: Short code is auto-generated from the name (initials / first word / diacritics removed / max 4 chars)
    [Theory]
    [InlineData("User Management", "UM")]        // UTC14a: initials of two words
    [InlineData("Authentication", "AUTH")]       // UTC14b: single word truncated to 4
    [InlineData("Quản lý đơn hàng", "QLDH")]     // UTC14c: diacritics and đ removed
    [InlineData("Alpha Beta Gamma Delta Epsilon", "ABGD")] // UTC14d: initials truncated to 4
    [InlineData("!!! ???", "MOD")]               // UTC14e: no alphanumeric -> fallback MOD
    [InlineData("Pay", "PAY")]                   // UTC14f: short single word
    public async Task CreateModuleAsync_WithoutShortCode_GeneratesCodeFromName(string name, string expectedCode)
    {
        var plan = SetupAuthorizedPlan();

        var result = await TestingService.CreateModuleAsync(plan.Id, new CreateTestModuleRequest { Name = name }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedCode, result.Data!.ShortCode);
        Assert.Equal(expectedCode, (await Db.TestModules.SingleAsync(TestContext.Current.CancellationToken)).ShortCode);
    }

    // UTC15: Generated short code collides with an existing one (case-insensitive) -> numeric suffix starting at 2
    [Fact]
    public async Task CreateModuleAsync_WhenGeneratedShortCodeCollides_AppendsNumericSuffix()
    {
        var plan = SetupAuthorizedPlan();
        H.AddModule(Db, plan.Id, "Existing 1", "um", 1);
        H.AddModule(Db, plan.Id, "Existing 2", "UM2", 2);

        var result = await TestingService.CreateModuleAsync(plan.Id, new CreateTestModuleRequest { Name = "User Management" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("UM3", result.Data!.ShortCode);
    }

    // UTC16: Explicit short code duplicates an existing one -> accepted as is (uniqueness only enforced for generated codes)
    [Fact]
    public async Task CreateModuleAsync_WhenExplicitShortCodeDuplicatesExisting_IsAccepted()
    {
        var plan = SetupAuthorizedPlan();
        H.AddModule(Db, plan.Id, "Existing", "DUP", 1);

        var result = await TestingService.CreateModuleAsync(plan.Id, new CreateTestModuleRequest { Name = "New", ShortCode = "dup" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("DUP", result.Data!.ShortCode);
        Assert.Equal(2, await Db.TestModules.CountAsync(m => m.ShortCode == "DUP", TestContext.Current.CancellationToken));
    }

    // UTC17: Whitespace short code -> treated as omitted (generated)
    [Fact]
    public async Task CreateModuleAsync_WhenShortCodeIsWhitespace_GeneratesCode()
    {
        var plan = SetupAuthorizedPlan();

        var result = await TestingService.CreateModuleAsync(plan.Id, new CreateTestModuleRequest { Name = "Billing", ShortCode = "   " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("BILL", result.Data!.ShortCode);
    }

    // UTC18: Plan already has modules -> position = max existing position + 1; modules of other plans ignored
    [Fact]
    public async Task CreateModuleAsync_WhenPlanHasModules_UsesNextPosition()
    {
        var plan = SetupAuthorizedPlan();
        var other = H.AddPlan(Db, ProjectId, "Other");
        H.AddModule(Db, plan.Id, "A", "A", 1);
        H.AddModule(Db, plan.Id, "B", "B", 7);
        H.AddModule(Db, other.Id, "Foreign", "F", 40);

        var result = await TestingService.CreateModuleAsync(plan.Id, new CreateTestModuleRequest { Name = "C" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(8, result.Data!.Position);
        Assert.Equal(4, await Db.TestModules.CountAsync(TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC19: Plan does not exist -> 404 TEST_PLAN_NOT_FOUND, guard never consulted
    [Fact]
    public async Task CreateModuleAsync_WhenPlanNotFound_ReturnsNotFound()
    {
        var result = await TestingService.CreateModuleAsync(999, new CreateTestModuleRequest { Name = "X" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_PLAN_NOT_FOUND", result.ErrorCode);
        Assert.Empty(await Db.TestModules.ToListAsync(TestContext.Current.CancellationToken));
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC20: Caller is not authenticated -> 401, nothing persisted
    [Fact]
    public async Task CreateModuleAsync_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var plan = H.AddPlan(Db, ProjectId);
        H.UnauthenticatedGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseCreate);

        var result = await TestingService.CreateModuleAsync(plan.Id, new CreateTestModuleRequest { Name = "X" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Empty(await Db.TestModules.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.TestSections.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.TestActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC21: Caller lacks testcase.manage -> 403 FORBIDDEN, nothing persisted
    [Fact]
    public async Task CreateModuleAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        var plan = H.AddPlan(Db, ProjectId);
        H.DenyGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseCreate);

        var result = await TestingService.CreateModuleAsync(plan.Id, new CreateTestModuleRequest { Name = "X" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.TestCaseCreate, result.RequiredPermission);
        Assert.Empty(await Db.TestModules.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.TestSections.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.TestActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC22: Document id does not exist -> 404 DOCUMENT_NOT_FOUND, nothing persisted
    [Fact]
    public async Task CreateModuleAsync_WhenDocumentNotFound_ReturnsNotFound()
    {
        var plan = SetupAuthorizedPlan();

        var result = await TestingService.CreateModuleAsync(plan.Id, new CreateTestModuleRequest { DocumentId = 12345, Name = "X" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
        Assert.Empty(await Db.TestModules.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.TestActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC23: Document belongs to another project -> 404 DOCUMENT_NOT_FOUND
    [Fact]
    public async Task CreateModuleAsync_WhenDocumentBelongsToAnotherProject_ReturnsNotFound()
    {
        var plan = SetupAuthorizedPlan();
        var doc = H.AddDocument(Db, projectId: 99, title: "Foreign");

        var result = await TestingService.CreateModuleAsync(plan.Id, new CreateTestModuleRequest { DocumentId = doc.Id, Name = "X" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
        Assert.Empty(await Db.TestModules.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC24: Document is soft-deleted -> 404 DOCUMENT_NOT_FOUND
    [Fact]
    public async Task CreateModuleAsync_WhenDocumentIsDeleted_ReturnsNotFound()
    {
        var plan = SetupAuthorizedPlan();
        var doc = H.AddDocument(Db, ProjectId, "Gone", isDeleted: true);

        var result = await TestingService.CreateModuleAsync(plan.Id, new CreateTestModuleRequest { DocumentId = doc.Id, Name = "X" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
        Assert.Empty(await Db.TestModules.ToListAsync(TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC25: Name with exactly 1 character -> created; short code is that character upper-cased
    [Fact]
    public async Task CreateModuleAsync_WhenNameHasOneChar_ReturnsCreated()
    {
        var plan = SetupAuthorizedPlan();

        var result = await TestingService.CreateModuleAsync(plan.Id, new CreateTestModuleRequest { Name = "x" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("x", result.Data!.Name);
        Assert.Equal("X", result.Data.ShortCode);
    }

    // UTC26: Very long name (200 / 201 / 1000 chars) -> accepted (no max-length validation), short code limited to 4 chars
    [Theory]
    [InlineData(200)]  // UTC26a
    [InlineData(201)]  // UTC26b
    [InlineData(1000)] // UTC26c
    public async Task CreateModuleAsync_WhenNameIsLong_IsAcceptedBecauseNoLengthValidation(int length)
    {
        var plan = SetupAuthorizedPlan();
        var name = new string('m', length);

        var result = await TestingService.CreateModuleAsync(plan.Id, new CreateTestModuleRequest { Name = name }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(length, result.Data!.Name.Length);
        Assert.Equal("MMMM", result.Data.ShortCode);
    }

    // UTC27: Generated short code of exactly 4 chars is kept untruncated; 5 chars is truncated to 4
    [Theory]
    [InlineData("Abcd", "ABCD")]   // UTC27a: exactly 4
    [InlineData("Abcde", "ABCD")]  // UTC27b: 5 -> truncated
    [InlineData("Abc", "ABC")]     // UTC27c: 3
    public async Task CreateModuleAsync_ShortCodeLengthBoundaries(string name, string expected)
    {
        var plan = SetupAuthorizedPlan();

        var result = await TestingService.CreateModuleAsync(plan.Id, new CreateTestModuleRequest { Name = name }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Data!.ShortCode);
    }

    // UTC28: First module in an empty plan -> position 1 (boundary of the "no modules" path)
    [Fact]
    public async Task CreateModuleAsync_OnPlanWithoutModules_StartsAtPositionOne()
    {
        var plan = SetupAuthorizedPlan();

        var result = await TestingService.CreateModuleAsync(plan.Id, new CreateTestModuleRequest { Name = "First" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.Position);
    }
}
