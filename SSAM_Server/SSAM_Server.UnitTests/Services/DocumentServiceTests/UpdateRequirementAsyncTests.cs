using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Documents;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.DocumentServiceTests;

public class UpdateRequirementAsyncTests : DocumentServiceTestBase
{
    private const long ProjectId = 10;
    private const long DocumentId = 100;
    private const long VersionId = 1000;

    private void SetupAuthorizedUser()
    {
        CurrentUserMock.Setup(u => u.UserId).Returns(1);
        DocumentTestData.GuardAllow(AccessGuardMock, ProjectId, PermissionKeys.DocumentUpdate);
    }

    /// <summary>Seeds a page whose version content has REQ-001 and REQ-002 headings; returns the persisted REQ-001 / REQ-002 rows.</summary>
    private (RequirementItem First, RequirementItem Second) SetupRequirements(string? content = null, string status = DocumentVersionStatuses.Draft)
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        DocumentTestData.AddPageWithVersion(
            Db, now, DocumentId, VersionId, ProjectId, status,
            content: content ?? DocumentTestData.DocJsonWithReqs(("REQ-001", "Login"), ("REQ-002", "Logout")));
        var first = DocumentTestData.AddRequirement(Db, DocumentId, VersionId, "REQ-001", "Login", "h1");
        first.Actor = "Customer";
        first.Preconditions = "Logged out";
        first.AcceptanceCriteria = "[\"Shows error\",\"Locks account\"]";
        first.HasDiagram = true;
        Db.SaveChanges();
        var second = DocumentTestData.AddRequirement(Db, DocumentId, VersionId, "REQ-002", "Logout", "h2", 1);
        return (first, second);
    }

    private async Task<Dictionary<string, string?>> StoredPrioritiesAsync()
    {
        var content = (await Db.DocumentVersions.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).Content;
        using var doc = JsonDocument.Parse(content);
        var result = new Dictionary<string, string?>();
        foreach (var block in doc.RootElement.GetProperty("content").EnumerateArray())
        {
            if (block.GetProperty("type").GetString() != "heading") continue;
            if (!block.TryGetProperty("attrs", out var attrs)) continue;
            var uid = attrs.GetProperty("reqUid").GetString()!;
            result[uid] = attrs.TryGetProperty("priority", out var p) ? p.GetString() : null;
        }
        return result;
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Set priority HIGH on REQ-001 -> DTO priority HIGH, priority written into the matching heading only, DTO fields mapped
    [Fact]
    public async Task UpdateRequirementAsync_WithHighPriority_UpdatesMatchingHeadingAndReturnsDto()
    {
        SetupAuthorizedUser();
        var (first, _) = SetupRequirements();

        var result = await DocumentService.UpdateRequirementAsync(first.Id, new UpdateRequirementRequest { Priority = "HIGH" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(first.Id, result.Data.RequirementId);
        Assert.Equal(DocumentId, result.Data.DocumentId);
        Assert.Equal(VersionId, result.Data.VersionId);
        Assert.Equal("REQ-001", result.Data.ReqUid);
        Assert.Equal("REQ-001", result.Data.ReqKey);
        Assert.Equal("a-REQ-001", result.Data.AnchorId);
        Assert.Equal("Login", result.Data.Title);
        Assert.Equal("HIGH", result.Data.Priority);
        Assert.Equal("FRESH", result.Data.Freshness);
        Assert.False(result.Data.ChangedSinceBaseline);
        Assert.Equal("Customer", result.Data.Actor);
        Assert.Equal("Logged out", result.Data.Preconditions);
        Assert.Equal("preview", result.Data.BodyPreview);
        Assert.Equal(7, result.Data.BodyLength);
        Assert.Equal(new[] { "Shows error", "Locks account" }, result.Data.AcceptanceCriteria.Select(a => a.Text).ToArray());
        Assert.All(result.Data.AcceptanceCriteria, a => Assert.True(a.Checked));
        Assert.True(result.Data.HasDiagram);
        Assert.False(result.Data.HasTable);
        Assert.Equal("h1", result.Data.ContentHash);
        Assert.Equal(0, result.Data.SortOrder);

        var priorities = await StoredPrioritiesAsync();
        Assert.Equal("HIGH", priorities["REQ-001"]);
        Assert.Null(priorities["REQ-002"]);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.DocumentUpdate, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Priority " low " (case/space-insensitive) -> normalized to LOW
    [Fact]
    public async Task UpdateRequirementAsync_WithLowercasePaddedPriority_NormalizesToUpperCase()
    {
        SetupAuthorizedUser();
        var (_, second) = SetupRequirements();

        var result = await DocumentService.UpdateRequirementAsync(second.Id, new UpdateRequirementRequest { Priority = " low " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("LOW", result.Data!.Priority);
        Assert.Equal("LOW", (await StoredPrioritiesAsync())["REQ-002"]);
    }

    // UTC03: Priority MEDIUM -> written as MEDIUM
    [Fact]
    public async Task UpdateRequirementAsync_WithMediumPriority_WritesMedium()
    {
        SetupAuthorizedUser();
        var (first, _) = SetupRequirements();

        var result = await DocumentService.UpdateRequirementAsync(first.Id, new UpdateRequirementRequest { Priority = "MEDIUM" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("MEDIUM", result.Data!.Priority);
        Assert.Equal("MEDIUM", (await StoredPrioritiesAsync())["REQ-001"]);
    }

    // UTC04: Unknown priority "URGENT" -> falls back to MEDIUM
    [Fact]
    public async Task UpdateRequirementAsync_WithUnknownPriority_FallsBackToMedium()
    {
        SetupAuthorizedUser();
        var (first, _) = SetupRequirements();

        var result = await DocumentService.UpdateRequirementAsync(first.Id, new UpdateRequirementRequest { Priority = "URGENT" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("MEDIUM", result.Data!.Priority);
        Assert.Equal("MEDIUM", (await StoredPrioritiesAsync())["REQ-001"]);
    }

    // UTC05: Null request -> defaults to MEDIUM
    [Fact]
    public async Task UpdateRequirementAsync_WithNullRequest_DefaultsToMedium()
    {
        SetupAuthorizedUser();
        var (first, _) = SetupRequirements();

        var result = await DocumentService.UpdateRequirementAsync(first.Id, null!, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("MEDIUM", result.Data!.Priority);
    }

    // UTC06: Heading matches only by anchor id (reqUid differs) -> priority still written
    [Fact]
    public async Task UpdateRequirementAsync_WithHeadingMatchedByAnchorId_UpdatesPriority()
    {
        SetupAuthorizedUser();
        var content = "{\"type\":\"doc\",\"content\":[{\"type\":\"heading\",\"attrs\":{\"level\":2,\"reqUid\":\"REQ-777\",\"id\":\"a-REQ-001\"},\"content\":[{\"type\":\"text\",\"text\":\"x\"}]}]}";
        var (first, _) = SetupRequirements(content);

        var result = await DocumentService.UpdateRequirementAsync(first.Id, new UpdateRequirementRequest { Priority = "HIGH" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("HIGH", (await StoredPrioritiesAsync())["REQ-777"]);
    }

    // UTC07: Heading reqUid differing only by case -> matched (case-insensitive)
    [Fact]
    public async Task UpdateRequirementAsync_WithReqUidDifferingByCase_UpdatesPriority()
    {
        SetupAuthorizedUser();
        var content = "{\"type\":\"doc\",\"content\":[{\"type\":\"heading\",\"attrs\":{\"level\":2,\"reqUid\":\"req-001\",\"id\":\"zzz\"},\"content\":[]}]}";
        var (first, _) = SetupRequirements(content);

        var result = await DocumentService.UpdateRequirementAsync(first.Id, new UpdateRequirementRequest { Priority = "LOW" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("LOW", (await StoredPrioritiesAsync())["req-001"]);
    }

    // UTC08: No heading matches the requirement -> success, DTO priority set, content unchanged semantically
    [Fact]
    public async Task UpdateRequirementAsync_WithNoMatchingHeading_StillReturnsDto()
    {
        SetupAuthorizedUser();
        var content = DocumentTestData.DocJsonWithReqs(("REQ-050", "Other"));
        var (first, _) = SetupRequirements(content);

        var result = await DocumentService.UpdateRequirementAsync(first.Id, new UpdateRequirementRequest { Priority = "HIGH" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("HIGH", result.Data!.Priority);
        Assert.Null((await StoredPrioritiesAsync())["REQ-050"]);
    }

    // UTC09: Version content is malformed JSON -> parse error swallowed, success with DTO priority, content untouched
    [Fact]
    public async Task UpdateRequirementAsync_WithMalformedVersionContent_IgnoresParseErrorAndSucceeds()
    {
        SetupAuthorizedUser();
        var (first, _) = SetupRequirements("{not json");

        var result = await DocumentService.UpdateRequirementAsync(first.Id, new UpdateRequirementRequest { Priority = "HIGH" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("HIGH", result.Data!.Priority);
        Assert.Equal("{not json", (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Content);
    }

    // UTC10: Version content has no "content" array -> nothing saved, success
    [Fact]
    public async Task UpdateRequirementAsync_WithContentWithoutBlocksArray_SucceedsWithoutChangingContent()
    {
        SetupAuthorizedUser();
        var (first, _) = SetupRequirements("{\"type\":\"doc\"}");

        var result = await DocumentService.UpdateRequirementAsync(first.Id, new UpdateRequirementRequest { Priority = "HIGH" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("{\"type\":\"doc\"}", (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).Content);
    }

    // UTC11: Non-heading blocks and headings without attrs are skipped safely
    [Fact]
    public async Task UpdateRequirementAsync_WithNonHeadingAndAttrlessBlocks_SkipsThemSafely()
    {
        SetupAuthorizedUser();
        var content = "{\"type\":\"doc\",\"content\":[" +
                      "{\"type\":\"paragraph\",\"content\":[]}," +
                      "{\"type\":\"heading\"}," +
                      "{\"type\":\"heading\",\"attrs\":{\"level\":2,\"reqUid\":\"REQ-001\",\"id\":\"x\"},\"content\":[]}]}";
        var (first, _) = SetupRequirements(content);

        var result = await DocumentService.UpdateRequirementAsync(first.Id, new UpdateRequirementRequest { Priority = "HIGH" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("HIGH", (await StoredPrioritiesAsync())["REQ-001"]);
    }

    // UTC12: Requirement uid not following REQ-xx pattern -> ReqKey generated from sort order (REQ-<sort+1>)
    [Fact]
    public async Task UpdateRequirementAsync_WithNonStandardReqUid_GeneratesReqKeyFromSortOrder()
    {
        SetupAuthorizedUser();
        var (_, second) = SetupRequirements();
        second.ReqUid = "FUNCTIONAL-REQ-LOGIN";
        second.SortOrder = 4;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DocumentService.UpdateRequirementAsync(second.Id, new UpdateRequirementRequest { Priority = "LOW" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("FUNCTIONAL-REQ-LOGIN", result.Data!.ReqUid);
        Assert.Equal("REQ-05", result.Data.ReqKey);
    }

    // UTC13: Version already APPROVED -> priority is still written (current behaviour: no lock/state check, no RowVersion bump)
    [Fact]
    public async Task UpdateRequirementAsync_OnApprovedVersion_StillWritesPriorityWithoutBumpingRowVersion()
    {
        SetupAuthorizedUser();
        var (first, _) = SetupRequirements(status: DocumentVersionStatuses.Approved);

        var result = await DocumentService.UpdateRequirementAsync(first.Id, new UpdateRequirementRequest { Priority = "HIGH" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("HIGH", (await StoredPrioritiesAsync())["REQ-001"]);
        Assert.Equal(1, (await Db.DocumentVersions.SingleAsync(TestContext.Current.CancellationToken)).RowVersion);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC14: Requirement does not exist -> 404 REQUIREMENT_NOT_FOUND, guard not consulted
    [Fact]
    public async Task UpdateRequirementAsync_WithNonExistentRequirement_ReturnsNotFound()
    {
        var result = await DocumentService.UpdateRequirementAsync(999, new UpdateRequirementRequest { Priority = "HIGH" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("REQUIREMENT_NOT_FOUND", result.ErrorCode);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC15: Parent document is soft-deleted -> 404 REQUIREMENT_NOT_FOUND
    [Fact]
    public async Task UpdateRequirementAsync_WithRequirementOfDeletedDocument_ReturnsNotFound()
    {
        SetupAuthorizedUser();
        var (first, _) = SetupRequirements();
        var node = await Db.DocumentNodes.SingleAsync(TestContext.Current.CancellationToken);
        node.IsDeleted = true;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DocumentService.UpdateRequirementAsync(first.Id, new UpdateRequirementRequest { Priority = "HIGH" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("REQUIREMENT_NOT_FOUND", result.ErrorCode);
    }

    // UTC16: Unauthenticated caller -> 401, content unchanged
    [Fact]
    public async Task UpdateRequirementAsync_WhenUnauthenticated_ReturnsUnauthorizedAndKeepsContent()
    {
        DocumentTestData.GuardUnauthenticated(AccessGuardMock, ProjectId, PermissionKeys.DocumentUpdate);
        var (first, _) = SetupRequirements();
        var before = (await Db.DocumentVersions.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).Content;

        var result = await DocumentService.UpdateRequirementAsync(first.Id, new UpdateRequirementRequest { Priority = "HIGH" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal(before, (await Db.DocumentVersions.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).Content);
    }

    // UTC17: Permission denied -> 403 FORBIDDEN with required permission, content unchanged
    [Fact]
    public async Task UpdateRequirementAsync_WhenPermissionDenied_ReturnsForbiddenAndKeepsContent()
    {
        DocumentTestData.GuardDeny(AccessGuardMock, ProjectId, PermissionKeys.DocumentUpdate);
        var (first, _) = SetupRequirements();
        var before = (await Db.DocumentVersions.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).Content;

        var result = await DocumentService.UpdateRequirementAsync(first.Id, new UpdateRequirementRequest { Priority = "HIGH" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.DocumentUpdate, result.RequiredPermission);
        Assert.Equal(before, (await Db.DocumentVersions.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).Content);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC18: Empty priority string -> MEDIUM
    [Fact]
    public async Task UpdateRequirementAsync_WithEmptyPriority_FallsBackToMedium()
    {
        SetupAuthorizedUser();
        var (first, _) = SetupRequirements();

        var result = await DocumentService.UpdateRequirementAsync(first.Id, new UpdateRequirementRequest { Priority = "" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("MEDIUM", result.Data!.Priority);
    }

    // UTC19: Whitespace-only priority -> MEDIUM
    [Fact]
    public async Task UpdateRequirementAsync_WithWhitespacePriority_FallsBackToMedium()
    {
        SetupAuthorizedUser();
        var (first, _) = SetupRequirements();

        var result = await DocumentService.UpdateRequirementAsync(first.Id, new UpdateRequirementRequest { Priority = "   " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("MEDIUM", result.Data!.Priority);
    }

    // UTC20: Requirement with empty acceptance criteria JSON and malformed constraints -> DTO lists are empty (safe deserialisation)
    [Fact]
    public async Task UpdateRequirementAsync_WithMalformedStoredJsonColumns_ReturnsEmptyLists()
    {
        SetupAuthorizedUser();
        var (first, _) = SetupRequirements();
        first.AcceptanceCriteria = "not-json";
        first.Constraints = "also-not-json";
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DocumentService.UpdateRequirementAsync(first.Id, new UpdateRequirementRequest { Priority = "HIGH" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.AcceptanceCriteria);
        Assert.Empty(result.Data.Constraints);
    }
}
