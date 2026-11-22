using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Documents;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.DocumentServiceTests;

public class CreateRevisionAsyncTests : DocumentServiceTestBase
{
    private const long ProjectId = 10;
    private const long DocumentId = 100;
    private const long BaseVersionId = 1000;
    private const long UserId = 2;

    private void SetupAuthorizedUser(IReadOnlyCollection<string>? permissions = null)
    {
        CurrentUserMock.Setup(u => u.UserId).Returns(UserId);
        DocumentTestData.GuardAllow(AccessGuardMock, ProjectId, PermissionKeys.DocumentCreate);
        DocumentTestData.GuardAllow(AccessGuardMock, ProjectId, PermissionKeys.DocumentView);
        PermissionServiceMock
            .Setup(p => p.GetProjectPermissionsAsync(UserId, ProjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions ?? new[] { PermissionKeys.DocumentEdit });
    }

    /// <summary>Seeds a page whose current version is APPROVED (label configurable) with two requirements, subtitle and content.</summary>
    private (DocumentNode Node, DocumentVersion Version) SetupApprovedBaseline(string label = "1.0", string status = DocumentVersionStatuses.Approved)
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var content = DocumentTestData.DocJsonWithReqs(("REQ-001", "Login"), ("REQ-002", "Logout"));
        var seeded = DocumentTestData.AddPageWithVersion(Db, now, DocumentId, BaseVersionId, ProjectId, status, label, content, "Login Logout text", 8);
        seeded.Version.Subtitle = "Base subtitle";
        seeded.Node.BaselineVersionId = status == DocumentVersionStatuses.Approved ? BaseVersionId : null;
        Db.SaveChanges();

        var r1 = DocumentTestData.AddRequirement(Db, DocumentId, BaseVersionId, "REQ-001", "Login", "h1");
        r1.Actor = "Customer";
        r1.Preconditions = "Logged out";
        r1.Constraints = "[{\"field\":\"email\"}]";
        r1.AcceptanceCriteria = "[\"Shows error\"]";
        r1.HasTable = true;
        DocumentTestData.AddRequirement(Db, DocumentId, BaseVersionId, "REQ-002", "Logout", "h2", 1);
        Db.SaveChanges();
        return seeded;
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Create revision with no request on an APPROVED page -> 201, new DRAFT v1.1 copying content, subtitle and requirements, node points to it
    [Fact]
    public async Task CreateRevisionAsync_WithNoRequestOnApprovedPage_CreatesDraftCopy()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        var (_, baseVersion) = SetupApprovedBaseline();

        var result = await DocumentService.CreateRevisionAsync(DocumentId, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.NotEqual(BaseVersionId, result.Data.Current.VersionId);
        Assert.Equal("1.1", result.Data.Current.Label);
        Assert.Equal(DocumentVersionStatuses.Draft, result.Data.Current.Status);
        Assert.Equal("Base subtitle", result.Data.Current.Subtitle);
        Assert.Equal(BaseVersionId, result.Data.Current.BaseVersionId);
        Assert.Equal(1, result.Data.Current.RowVersion);
        Assert.Equal(UserId, result.Data.Current.Author.UserId);
        Assert.Equal(UserId, result.Data.Current.UpdatedBy.UserId);
        Assert.Equal(2, result.Data.Node.ReqCount);
        Assert.Equal(result.Data.Current.VersionId, result.Data.Node.CurrentVersion!.VersionId);
        Assert.Equal(BaseVersionId, result.Data.Node.BaselineVersionId);
        Assert.Equal(2, result.Data.Versions.Count);
        Assert.True(result.Data.Can.Edit);
        Assert.False(result.Data.Can.Revise);

        var versions = await Db.DocumentVersions.OrderBy(v => v.Id).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, versions.Count);
        var created = versions.Single(v => v.Id != BaseVersionId);
        Assert.Equal(DocumentId, created.DocumentId);
        Assert.Equal("1.1", created.Label);
        Assert.Equal(DocumentVersionStatuses.Draft, created.Status);
        Assert.Equal(baseVersion.Content, created.Content);
        Assert.Equal("Login Logout text", created.ContentText);
        Assert.Equal("Base subtitle", created.Subtitle);
        Assert.Equal(BaseVersionId, created.BaseVersionId);
        Assert.Equal(1, created.RowVersion);
        Assert.Equal(UserId, created.AuthorId);
        Assert.Equal(UserId, created.UpdatedById);
        Assert.Equal(now, created.CreatedAt);
        Assert.Equal(now, created.UpdatedAt);
        Assert.Null(created.ReviewNote);
        Assert.Equal(DocumentVersionStatuses.Approved, versions.Single(v => v.Id == BaseVersionId).Status);

        var copied = await Db.RequirementItems.Where(r => r.VersionId == created.Id).OrderBy(r => r.SortOrder).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "REQ-001", "REQ-002" }, copied.Select(r => r.ReqUid).ToArray());
        Assert.Equal("Customer", copied[0].Actor);
        Assert.Equal("Logged out", copied[0].Preconditions);
        Assert.Equal("[{\"field\":\"email\"}]", copied[0].Constraints);
        Assert.Equal("[\"Shows error\"]", copied[0].AcceptanceCriteria);
        Assert.True(copied[0].HasTable);
        Assert.Equal("h1", copied[0].ContentHash);
        Assert.Equal(DocumentId, copied[0].DocumentId);
        Assert.Equal(2, await Db.RequirementItems.CountAsync(r => r.VersionId == BaseVersionId, TestContext.Current.CancellationToken));

        var node = await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(created.Id, node.CurrentVersionId);
        Assert.Equal(BaseVersionId, node.BaselineVersionId);
        Assert.Equal(now, node.UpdatedAt);

        var activity = await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DocumentActivityActions.RevisionCreated, activity.Action);
        Assert.Equal(DocumentId, activity.DocumentId);
        Assert.Equal(created.Id, activity.VersionId);
        Assert.Equal(UserId, activity.ActorId);
        Assert.Equal("Tạo bản nháp v1.1 từ baseline v1.0", activity.Note);
        Assert.Equal(now, activity.CreatedAt);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.DocumentCreate, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Bump MAJOR -> label 2.0
    [Fact]
    public async Task CreateRevisionAsync_WithMajorBump_IncrementsMajorAndResetsMinor()
    {
        SetupAuthorizedUser();
        SetupApprovedBaseline("1.3");

        var result = await DocumentService.CreateRevisionAsync(DocumentId, new CreateRevisionRequest { Bump = "MAJOR" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("2.0", result.Data!.Current.Label);
        Assert.Contains("v2.0 từ baseline v1.3", (await Db.DocumentActivities.SingleAsync(TestContext.Current.CancellationToken)).Note);
    }

    // UTC03: Bump "major" in lowercase -> treated as MAJOR (case-insensitive)
    [Fact]
    public async Task CreateRevisionAsync_WithLowercaseMajorBump_IncrementsMajor()
    {
        SetupAuthorizedUser();
        SetupApprovedBaseline("2.4");

        var result = await DocumentService.CreateRevisionAsync(DocumentId, new CreateRevisionRequest { Bump = "major" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("3.0", result.Data!.Current.Label);
    }

    // UTC04: Bump MINOR (explicit) -> minor incremented
    [Fact]
    public async Task CreateRevisionAsync_WithMinorBump_IncrementsMinor()
    {
        SetupAuthorizedUser();
        SetupApprovedBaseline("1.0");

        var result = await DocumentService.CreateRevisionAsync(DocumentId, new CreateRevisionRequest { Bump = "MINOR" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("1.1", result.Data!.Current.Label);
    }

    // UTC05: Unknown bump value -> falls back to a minor bump
    [Fact]
    public async Task CreateRevisionAsync_WithUnknownBump_FallsBackToMinorBump()
    {
        SetupAuthorizedUser();
        SetupApprovedBaseline("1.5");

        var result = await DocumentService.CreateRevisionAsync(DocumentId, new CreateRevisionRequest { Bump = "PATCH" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("1.6", result.Data!.Current.Label);
    }

    // UTC06: Label with a leading "v" ("v2.3") -> prefix stripped, minor bump gives 2.4
    [Fact]
    public async Task CreateRevisionAsync_WithLabelHavingVPrefix_StripsPrefix()
    {
        SetupAuthorizedUser();
        SetupApprovedBaseline("v2.3");

        var result = await DocumentService.CreateRevisionAsync(DocumentId, new CreateRevisionRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("2.4", result.Data!.Current.Label);
    }

    // UTC07: ChangeSummary provided -> trimmed and stored as the new version's ReviewNote
    [Fact]
    public async Task CreateRevisionAsync_WithChangeSummary_StoresItAsReviewNote()
    {
        SetupAuthorizedUser();
        SetupApprovedBaseline();

        var result = await DocumentService.CreateRevisionAsync(DocumentId, new CreateRevisionRequest { ChangeSummary = "  Add payment rules  " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Add payment rules", result.Data!.Current.ReviewNote);
        Assert.Equal("Add payment rules", result.Data.Current.ChangeSummary);
        var created = await Db.DocumentVersions.SingleAsync(v => v.Id != BaseVersionId, TestContext.Current.CancellationToken);
        Assert.Equal("Add payment rules", created.ReviewNote);
    }

    // UTC08: Only Summary provided -> used as the ReviewNote fallback
    [Fact]
    public async Task CreateRevisionAsync_WithSummaryOnly_StoresSummaryAsReviewNote()
    {
        SetupAuthorizedUser();
        SetupApprovedBaseline();

        var result = await DocumentService.CreateRevisionAsync(DocumentId, new CreateRevisionRequest { Summary = " Fix typos " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Fix typos", (await Db.DocumentVersions.SingleAsync(v => v.Id != BaseVersionId, TestContext.Current.CancellationToken)).ReviewNote);
    }

    // UTC09: Both ChangeSummary and Summary provided -> ChangeSummary wins
    [Fact]
    public async Task CreateRevisionAsync_WithBothSummaries_PrefersChangeSummary()
    {
        SetupAuthorizedUser();
        SetupApprovedBaseline();

        var result = await DocumentService.CreateRevisionAsync(DocumentId, new CreateRevisionRequest { ChangeSummary = "Primary", Summary = "Secondary" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Primary", (await Db.DocumentVersions.SingleAsync(v => v.Id != BaseVersionId, TestContext.Current.CancellationToken)).ReviewNote);
    }

    // UTC10: Blank ChangeSummary with a Summary -> falls back to Summary
    [Fact]
    public async Task CreateRevisionAsync_WithBlankChangeSummaryAndSummary_FallsBackToSummary()
    {
        SetupAuthorizedUser();
        SetupApprovedBaseline();

        var result = await DocumentService.CreateRevisionAsync(DocumentId, new CreateRevisionRequest { ChangeSummary = "   ", Summary = "Fallback" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Fallback", (await Db.DocumentVersions.SingleAsync(v => v.Id != BaseVersionId, TestContext.Current.CancellationToken)).ReviewNote);
    }

    // UTC11: Both summaries blank -> ReviewNote stays null
    [Fact]
    public async Task CreateRevisionAsync_WithBlankSummaries_LeavesReviewNoteNull()
    {
        SetupAuthorizedUser();
        SetupApprovedBaseline();

        var result = await DocumentService.CreateRevisionAsync(DocumentId, new CreateRevisionRequest { ChangeSummary = " ", Summary = "" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null((await Db.DocumentVersions.SingleAsync(v => v.Id != BaseVersionId, TestContext.Current.CancellationToken)).ReviewNote);
    }

    // UTC12: Requirements of other documents are not copied
    [Fact]
    public async Task CreateRevisionAsync_WithOtherDocumentRequirements_CopiesOnlyOwnRequirements()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        SetupApprovedBaseline();
        DocumentTestData.AddPageWithVersion(Db, now, 200, 2000, ProjectId, title: "Other", code: "SRS-02");
        DocumentTestData.AddRequirement(Db, 200, 2000, "REQ-900");

        var result = await DocumentService.CreateRevisionAsync(DocumentId, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await Db.RequirementItems.CountAsync(r => r.ReqUid == "REQ-900", TestContext.Current.CancellationToken));
        Assert.Equal(4, await Db.RequirementItems.CountAsync(r => r.DocumentId == DocumentId, TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC13: Document does not exist -> 404 DOCUMENT_NOT_FOUND, guard not consulted
    [Fact]
    public async Task CreateRevisionAsync_WithNonExistentDocument_ReturnsNotFound()
    {
        var result = await DocumentService.CreateRevisionAsync(999, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC14: Document is soft-deleted -> 404 DOCUMENT_NOT_FOUND
    [Fact]
    public async Task CreateRevisionAsync_WithDeletedDocument_ReturnsNotFound()
    {
        SetupAuthorizedUser();
        var (node, _) = SetupApprovedBaseline();
        node.IsDeleted = true;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DocumentService.CreateRevisionAsync(DocumentId, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
    }

    // UTC15: Unauthenticated caller -> 401, no revision created
    [Fact]
    public async Task CreateRevisionAsync_WhenUnauthenticated_ReturnsUnauthorizedAndCreatesNothing()
    {
        DocumentTestData.GuardUnauthenticated(AccessGuardMock, ProjectId, PermissionKeys.DocumentCreate);
        SetupApprovedBaseline();

        var result = await DocumentService.CreateRevisionAsync(DocumentId, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Single(await Db.DocumentVersions.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC16: Permission denied -> 403 FORBIDDEN with required permission, no revision created
    [Fact]
    public async Task CreateRevisionAsync_WhenPermissionDenied_ReturnsForbiddenAndCreatesNothing()
    {
        DocumentTestData.GuardDeny(AccessGuardMock, ProjectId, PermissionKeys.DocumentCreate);
        SetupApprovedBaseline();

        var result = await DocumentService.CreateRevisionAsync(DocumentId, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.DocumentCreate, result.RequiredPermission);
        Assert.Single(await Db.DocumentVersions.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC17: Target is a FOLDER -> 400 NOT_A_PAGE
    [Fact]
    public async Task CreateRevisionAsync_WithFolder_ReturnsNotAPage()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        DocumentTestData.AddNode(Db, now, 1, ProjectId, DocumentNodeKinds.Folder, "Folder");

        var result = await DocumentService.CreateRevisionAsync(1, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("NOT_A_PAGE", result.ErrorCode);
    }

    // UTC18: PAGE without a current version -> 400 NOT_A_PAGE
    [Fact]
    public async Task CreateRevisionAsync_WithPageWithoutCurrentVersion_ReturnsNotAPage()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        DocumentTestData.AddNode(Db, now, 1, ProjectId, DocumentNodeKinds.Page, "Empty page", code: "SRS-01");

        var result = await DocumentService.CreateRevisionAsync(1, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("NOT_A_PAGE", result.ErrorCode);
        Assert.Empty(await Db.DocumentVersions.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC19-UTC22: Current version is not APPROVED (DRAFT/IN_REVIEW/CHANGES_REQUESTED/SUPERSEDED) -> 409 REVISION_ALREADY_OPEN
    [Theory]
    [InlineData(DocumentVersionStatuses.Draft)] // UTC19
    [InlineData(DocumentVersionStatuses.InReview)] // UTC20
    [InlineData(DocumentVersionStatuses.ChangesRequested)] // UTC21
    [InlineData(DocumentVersionStatuses.Superseded)] // UTC22
    public async Task CreateRevisionAsync_WhenCurrentVersionNotApproved_ReturnsRevisionAlreadyOpen(string status)
    {
        SetupAuthorizedUser();
        SetupApprovedBaseline(status: status);

        var result = await DocumentService.CreateRevisionAsync(DocumentId, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("REVISION_ALREADY_OPEN", result.ErrorCode);
        Assert.Single(await Db.DocumentVersions.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.DocumentActivities.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(BaseVersionId, (await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken)).CurrentVersionId);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC23: Unparsable label ("abc") -> next label falls back to 1.1
    [Fact]
    public async Task CreateRevisionAsync_WithUnparsableLabel_FallsBackToOnePointOne()
    {
        SetupAuthorizedUser();
        SetupApprovedBaseline("abc");

        var result = await DocumentService.CreateRevisionAsync(DocumentId, new CreateRevisionRequest { Bump = "MAJOR" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("1.1", result.Data!.Current.Label);
    }

    // UTC24: Three-part label ("1.2.3") -> not supported, falls back to 1.1
    [Fact]
    public async Task CreateRevisionAsync_WithThreePartLabel_FallsBackToOnePointOne()
    {
        SetupAuthorizedUser();
        SetupApprovedBaseline("1.2.3");

        var result = await DocumentService.CreateRevisionAsync(DocumentId, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("1.1", result.Data!.Current.Label);
    }

    // UTC25: Minor 9 -> 10 (numeric, not decimal) on minor bump
    [Fact]
    public async Task CreateRevisionAsync_WithMinorNine_ProducesMinorTen()
    {
        SetupAuthorizedUser();
        SetupApprovedBaseline("1.9");

        var result = await DocumentService.CreateRevisionAsync(DocumentId, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("1.10", result.Data!.Current.Label);
    }

    // UTC26: Major bump from 9.9 -> 10.0
    [Fact]
    public async Task CreateRevisionAsync_WithMajorBumpFromNineNine_ProducesTenZero()
    {
        SetupAuthorizedUser();
        SetupApprovedBaseline("9.9");

        var result = await DocumentService.CreateRevisionAsync(DocumentId, new CreateRevisionRequest { Bump = "MAJOR" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("10.0", result.Data!.Current.Label);
    }

    // UTC27: Baseline without requirements -> revision created with zero requirements
    [Fact]
    public async Task CreateRevisionAsync_WithBaselineWithoutRequirements_CreatesRevisionWithoutRequirements()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        SetupAuthorizedUser();
        DocumentTestData.AddPageWithVersion(Db, now, DocumentId, BaseVersionId, ProjectId, DocumentVersionStatuses.Approved);

        var result = await DocumentService.CreateRevisionAsync(DocumentId, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Data!.Node.ReqCount);
        Assert.Empty(await Db.RequirementItems.ToListAsync(TestContext.Current.CancellationToken));
    }
}
