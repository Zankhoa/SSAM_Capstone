using System.Text.Json.Nodes;
using SSAM_Server.Services;

namespace SSAM_Server.UnitTests.Services.DocumentContentHelperTests;

/// <summary>ProcessContent: requirement detection (ExtractRequirementItems) - headings, uid/title/actor rules, sections.</summary>
public class ProcessContentRequirementTests : DocumentContentHelperTestBase
{
    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Heading "REQ-01 — Title" with body -> Full node populated (uid, anchor, title, body, flags, sort order)
    [Fact]
    public void ExtractRequirements_WithRegexHeadingAndBody_ReturnsFullNode()
    {
        var req = Assert.Single(Extract(H(2, "REQ-01 — Đăng nhập", "\"id\":\"a1\""), P("Body text")));

        Assert.Equal("REQ-01", req.ReqUid);
        Assert.Equal("a1", req.AnchorId);
        Assert.Equal("Đăng nhập", req.Title);
        Assert.Equal("Body text", req.BodyPreview);
        Assert.Equal(9, req.BodyLength);
        Assert.Null(req.Actor);
        Assert.Null(req.Preconditions);
        Assert.Empty(req.Constraints);
        Assert.Empty(req.AcceptanceCriteria);
        Assert.False(req.HasTable);
        Assert.False(req.HasDiagram);
        Assert.False(req.HasFigma);
        Assert.Matches("^[0-9a-f]{64}$", req.ContentHash);
        Assert.Equal(0, req.SortOrder);
    }

    // UTC02: Dash separator "-" -> Matched
    // UTC03: Em dash "—" -> Matched
    // UTC04: En dash "–" -> Matched
    // UTC05: Colon ":" -> Matched
    // UTC06: Middle dot "·" -> Matched
    [Theory]
    [InlineData("REQ-01 - Login", "Login")]   // UTC02
    [InlineData("REQ-01 — Login", "Login")]   // UTC03
    [InlineData("REQ-01 – Login", "Login")]   // UTC04
    [InlineData("REQ-01: Login", "Login")]    // UTC05
    [InlineData("REQ-01 · Login", "Login")]   // UTC06
    public void ExtractRequirements_WithSeparatorVariant_MatchesAndExtractsTitle(string heading, string expectedTitle)
    {
        var req = Assert.Single(Extract(H(2, heading)));

        Assert.Equal("REQ-01", req.ReqUid);
        Assert.Equal(expectedTitle, req.Title);
    }

    // UTC07: Separator without surrounding spaces "REQ-7—Login" -> Matched
    [Fact]
    public void ExtractRequirements_WithNoSpacesAroundSeparator_Matches()
    {
        var req = Assert.Single(Extract(H(2, "REQ-7—Login")));

        Assert.Equal("REQ-7", req.ReqUid);
        Assert.Equal("Login", req.Title);
    }

    // UTC08: Lowercase "req-5: abc" -> ReqUid upper-cased to "REQ-5"
    [Fact]
    public void ExtractRequirements_WithLowercaseReq_UppercasesReqUid()
    {
        var req = Assert.Single(Extract(H(2, "req-5: abc")));

        Assert.Equal("REQ-5", req.ReqUid);
        Assert.Equal("abc", req.Title);
    }

    // UTC09: Multiple spaces inside heading text -> Collapsed before matching, title collapsed
    [Fact]
    public void ExtractRequirements_WithRepeatedSpaces_CollapsesTitle()
    {
        var req = Assert.Single(Extract(H(2, "REQ-01   —    Login    page")));

        Assert.Equal("Login page", req.Title);
    }

    // UTC10: Heading levels 1, 2, 3 -> Each recognised as a requirement (UTC10 level 1)
    // UTC11: Level 2
    // UTC12: Level 3
    [Theory]
    [InlineData(1)] // UTC10
    [InlineData(2)] // UTC11
    [InlineData(3)] // UTC12
    public void ExtractRequirements_WithSupportedHeadingLevel_ExtractsRequirement(int level)
    {
        var req = Assert.Single(Extract(H(level, "REQ-01 — Login")));

        Assert.Equal("REQ-01", req.ReqUid);
    }

    // UTC13: attrs.req = true without regex match and no reqUid -> Auto uid "REQ-01", title = heading text
    [Fact]
    public void ExtractRequirements_WithReqAttrOnly_GeneratesUidFromCount()
    {
        var req = Assert.Single(Extract(ReqH(2, "Quản lý người dùng")));

        Assert.Equal("REQ-01", req.ReqUid);
        Assert.Equal("Quản lý người dùng", req.Title);
    }

    // UTC14: Several attr-only requirements -> Auto uids REQ-01, REQ-02, REQ-03 by position; sort order 0..2
    [Fact]
    public void ExtractRequirements_WithMultipleReqAttrOnly_GeneratesSequentialUids()
    {
        var list = Extract(ReqH(2, "One"), ReqH(2, "Two"), ReqH(2, "Three"));

        Assert.Equal(new[] { "REQ-01", "REQ-02", "REQ-03" }, list.Select(r => r.ReqUid).ToArray());
        Assert.Equal(new[] { "One", "Two", "Three" }, list.Select(r => r.Title).ToArray());
        Assert.Equal(new[] { 0, 1, 2 }, list.Select(r => r.SortOrder).ToArray());
    }

    // UTC15: attrs.reqUid supplied -> Used as is (case preserved) even when heading matches regex
    [Fact]
    public void ExtractRequirements_WithReqUidAttr_UsesAttrValue()
    {
        var req = Assert.Single(Extract(ReqH(2, "REQ-01 — Login", "\"reqUid\":\"fr-9\"")));

        Assert.Equal("fr-9", req.ReqUid);
        Assert.Equal("Login", req.Title);
    }

    // UTC16: attrs.reqUid has surrounding whitespace -> Trimmed
    [Fact]
    public void ExtractRequirements_WithPaddedReqUidAttr_Trims()
    {
        var req = Assert.Single(Extract(ReqH(2, "Login", "\"reqUid\":\"  FR-1  \"")));

        Assert.Equal("FR-1", req.ReqUid);
    }

    // UTC17: req=false but heading matches regex -> Still a requirement
    [Fact]
    public void ExtractRequirements_WithReqFalseButRegexMatch_IsRequirement()
    {
        var req = Assert.Single(Extract(H(2, "REQ-02 — Pay", "\"req\":false")));

        Assert.Equal("REQ-02", req.ReqUid);
    }

    // UTC18: Normal heading without req attr and without regex match -> Not a requirement
    [Fact]
    public void ExtractRequirements_WithPlainHeading_ReturnsEmpty()
    {
        Assert.Empty(Extract(H(2, "Giới thiệu"), P("text")));
    }

    // UTC19: Actor and preconditions attrs -> Trimmed values stored
    [Fact]
    public void ExtractRequirements_WithActorAndPreconditions_StoresTrimmedValues()
    {
        var req = Assert.Single(Extract(H(2, "REQ-01 — T", "\"actor\":\"  Admin \",\"preconditions\":\" Logged in  \"")));

        Assert.Equal("Admin", req.Actor);
        Assert.Equal("Logged in", req.Preconditions);
    }

    // UTC20: Priority missing -> normalized JSON gets MEDIUM; reqUid/req written back
    [Fact]
    public void ExtractRequirements_WritesBackNormalizedAttrsToJson()
    {
        var result = Process(H(2, "REQ-03 — T"));

        var attrs = (JsonObject)((JsonObject)((JsonArray)((JsonObject)JsonNode.Parse(result.NormalizedContentJson)!)["content"]!)[0]!)["attrs"]!;
        Assert.True(attrs["req"]!.GetValue<bool>());
        Assert.Equal("REQ-03", attrs["reqUid"]!.GetValue<string>());
        Assert.Equal("MEDIUM", attrs["priority"]!.GetValue<string>());
    }

    // UTC21: Duplicate reqUid (same case) -> Only first kept
    [Fact]
    public void ExtractRequirements_WithDuplicateReqUid_KeepsFirst()
    {
        var list = Extract(H(2, "REQ-01 — First"), P("a"), H(2, "REQ-01 — Second"), P("b"));

        var req = Assert.Single(list);
        Assert.Equal("First", req.Title);
    }

    // UTC22: Duplicate reqUid with different case -> Treated as duplicate (OrdinalIgnoreCase)
    [Fact]
    public void ExtractRequirements_WithDuplicateReqUidDifferentCase_KeepsFirst()
    {
        var list = Extract(ReqH(2, "A", "\"reqUid\":\"fr-1\""), ReqH(2, "B", "\"reqUid\":\"FR-1\""));

        var req = Assert.Single(list);
        Assert.Equal("fr-1", req.ReqUid);
        Assert.Equal("A", req.Title);
    }

    // UTC23: Duplicate in the middle -> SortOrder continues from kept count (0, 1)
    [Fact]
    public void ExtractRequirements_WithDuplicateInMiddle_SortOrderIsContiguous()
    {
        var list = Extract(H(2, "REQ-01 — A"), H(2, "REQ-01 — dup"), H(2, "REQ-02 — B"));

        Assert.Equal(new[] { "REQ-01", "REQ-02" }, list.Select(r => r.ReqUid).ToArray());
        Assert.Equal(new[] { 0, 1 }, list.Select(r => r.SortOrder).ToArray());
    }

    // UTC24: Auto uid after a duplicate was dropped -> Uses count of kept requirements (REQ-02)
    [Fact]
    public void ExtractRequirements_AutoUidAfterDuplicate_UsesKeptCount()
    {
        var list = Extract(H(2, "REQ-01 — A"), H(2, "REQ-01 — dup"), ReqH(2, "No uid"));

        Assert.Equal(new[] { "REQ-01", "REQ-02" }, list.Select(r => r.ReqUid).ToArray());
        Assert.Equal("No uid", list[1].Title);
    }

    // UTC25: Auto uid collides with an existing explicit uid -> Second is dropped as duplicate
    [Fact]
    public void ExtractRequirements_AutoUidCollidesWithExistingUid_DroppedAsDuplicate()
    {
        var list = Extract(ReqH(2, "A"), H(2, "REQ-02 — B"), ReqH(2, "C"));

        // A => REQ-01, B => REQ-02, C => auto uid REQ-03 (count = 2) -> no collision here
        Assert.Equal(new[] { "REQ-01", "REQ-02", "REQ-03" }, list.Select(r => r.ReqUid).ToArray());

        var collide = Extract(H(2, "REQ-02 — A"), ReqH(2, "B"));
        // A => REQ-02, B => auto REQ-02 (count 1 + 1) -> duplicate, dropped
        Assert.Equal(new[] { "REQ-02" }, collide.Select(r => r.ReqUid).ToArray());
    }

    // UTC26: Mixed top-level blocks -> Only qualifying headings extracted in document order
    [Fact]
    public void ExtractRequirements_WithMixedBlocks_ExtractsInOrder()
    {
        var list = Extract(P("intro"), H(1, "Chapter"), H(2, "REQ-01 — A"), P("x"), H(2, "REQ-02 — B"), P("y"));

        Assert.Equal(new[] { "REQ-01", "REQ-02" }, list.Select(r => r.ReqUid).ToArray());
    }

    // UTC27: Section ends at next heading of same level -> Body only contains own section
    [Fact]
    public void ExtractRequirements_SectionEndsAtSameLevelHeading()
    {
        var list = Extract(H(2, "REQ-01 — A"), P("alpha"), H(2, "REQ-02 — B"), P("beta"));

        Assert.Equal("alpha", list[0].BodyPreview);
        Assert.Equal("beta", list[1].BodyPreview);
    }

    // UTC28: Section ends at a higher-level heading (level 1 ends level 2) -> Body excludes later content
    [Fact]
    public void ExtractRequirements_SectionEndsAtHigherLevelHeading()
    {
        var req = Assert.Single(Extract(H(2, "REQ-01 — A"), P("alpha"), H(1, "Chapter"), P("outside")));

        Assert.Equal("alpha", req.BodyPreview);
    }

    // UTC29: Deeper heading does not end the section -> Its text is part of the body
    [Fact]
    public void ExtractRequirements_DeeperHeadingIsIncludedInBody()
    {
        var req = Assert.Single(Extract(H(2, "REQ-01 — A"), P("alpha"), H(3, "Sub"), P("beta")));

        Assert.Equal("alpha Sub beta", req.BodyPreview);
    }

    // UTC30: Heading without level inside the section -> Does not end section (level 0), included in body
    [Fact]
    public void ExtractRequirements_HeadingWithoutLevelDoesNotEndSection()
    {
        var noLevel = "{\"type\":\"heading\",\"content\":[" + TextNode("Loose") + "]}";

        var req = Assert.Single(Extract(H(2, "REQ-01 — A"), noLevel, P("beta")));

        Assert.Equal("Loose beta", req.BodyPreview);
    }

    // UTC31: Nested requirement (level 3 under level 2 requirement) -> Both extracted; parent body contains child text
    [Fact]
    public void ExtractRequirements_NestedRequirements_BothExtracted()
    {
        var list = Extract(H(2, "REQ-01 — Parent"), P("p"), H(3, "REQ-02 — Child"), P("c"));

        Assert.Equal(2, list.Count);
        Assert.Equal("p REQ-02 — Child c", list[0].BodyPreview);
        Assert.Equal("c", list[1].BodyPreview);
    }

    // UTC32: Non-object entries inside section -> Skipped
    [Fact]
    public void ExtractRequirements_SectionWithNonObjectEntries_SkipsThem()
    {
        var json = "{\"type\":\"doc\",\"content\":[" + H(2, "REQ-01 — A") + ",\"str\",7," + P("ok") + "]}";

        var result = DocumentContentHelper.ProcessContent(Json(json));

        var req = Assert.Single(result.Requirements);
        Assert.Equal("ok", req.BodyPreview);
    }

    // UTC33: Heading with inline children -> Title built from joined children text
    [Fact]
    public void ExtractRequirements_HeadingWithMultipleChildren_JoinsChildrenText()
    {
        var heading = "{\"type\":\"heading\",\"attrs\":{\"level\":2},\"content\":[" + TextNode("REQ-01") + "," + TextNode("— Login") + "]}";

        var req = Assert.Single(Extract(heading));

        Assert.Equal("REQ-01", req.ReqUid);
        Assert.Equal("Login", req.Title);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC34: Heading level 4 with req attr -> Ignored
    [Fact]
    public void ExtractRequirements_WithLevel4Heading_Ignored()
    {
        Assert.Empty(Extract(ReqH(4, "REQ-01 — A")));
    }

    // UTC35: Heading level 0 -> Ignored
    [Fact]
    public void ExtractRequirements_WithLevel0Heading_Ignored()
    {
        Assert.Empty(Extract(ReqH(0, "REQ-01 — A")));
    }

    // UTC36: Heading level 5 and 6 -> Ignored
    [Theory]
    [InlineData(5)] // UTC36
    [InlineData(6)] // UTC37
    public void ExtractRequirements_WithLevelAbove4_Ignored(int level)
    {
        Assert.Empty(Extract(H(level, "REQ-01 — A")));
    }

    // UTC38: Negative heading level -> Ignored
    [Fact]
    public void ExtractRequirements_WithNegativeLevel_Ignored()
    {
        Assert.Empty(Extract(H(-1, "REQ-01 — A")));
    }

    // UTC39: Heading without level attr -> Ignored (level defaults to 0)
    [Fact]
    public void ExtractRequirements_WithoutLevelAttr_Ignored()
    {
        var heading = "{\"type\":\"heading\",\"attrs\":{\"req\":true},\"content\":[" + TextNode("REQ-01 — A") + "]}";

        Assert.Empty(Extract(heading));
    }

    // UTC40: Heading level as string "2" -> Throws (GetValue<int> on string, current behaviour)
    [Fact]
    public void ExtractRequirements_WithStringLevel_Throws()
    {
        var heading = "{\"type\":\"heading\",\"attrs\":{\"level\":\"2\"},\"content\":[" + TextNode("REQ-01 — A") + "]}";

        Assert.ThrowsAny<Exception>(() => Extract(heading));
    }

    // UTC41: "REQ-01" alone (no separator/title) -> No regex match, ignored
    [Fact]
    public void ExtractRequirements_WithReqUidOnlyHeading_Ignored()
    {
        Assert.Empty(Extract(H(2, "REQ-01")));
    }

    // UTC42: "REQ-01 Login" (no separator) -> Ignored
    [Fact]
    public void ExtractRequirements_WithoutSeparator_Ignored()
    {
        Assert.Empty(Extract(H(2, "REQ-01 Login")));
    }

    // UTC43: "REQ- — Login" (no digits) -> Ignored
    [Fact]
    public void ExtractRequirements_WithoutDigits_Ignored()
    {
        Assert.Empty(Extract(H(2, "REQ- — Login")));
    }

    // UTC44: "XREQ-01 — Login" (prefix before REQ) -> Ignored (regex anchored)
    [Fact]
    public void ExtractRequirements_WithPrefixBeforeReq_Ignored()
    {
        Assert.Empty(Extract(H(2, "XREQ-01 — Login")));
    }

    // UTC45: "REQ-01 — " (empty title after separator) -> Collapsed text "REQ-01 —" has no match, ignored
    [Fact]
    public void ExtractRequirements_WithEmptyTitleAfterSeparator_Ignored()
    {
        Assert.Empty(Extract(H(2, "REQ-01 — ")));
    }

    // UTC46: req=true with empty heading and no reqUid -> Title falls back to generated uid
    [Fact]
    public void ExtractRequirements_WithEmptyHeadingText_TitleFallsBackToReqUid()
    {
        var heading = "{\"type\":\"heading\",\"attrs\":{\"level\":2,\"req\":true}}";

        var req = Assert.Single(Extract(heading));

        Assert.Equal("REQ-01", req.ReqUid);
        Assert.Equal("REQ-01", req.Title);
    }

    // UTC47: req=true with whitespace-only heading and reqUid attr -> Title falls back to attr reqUid
    [Fact]
    public void ExtractRequirements_WithWhitespaceHeadingAndReqUid_TitleFallsBackToReqUid()
    {
        var req = Assert.Single(Extract(ReqH(2, "   ", "\"reqUid\":\"FR-7\"")));

        Assert.Equal("FR-7", req.ReqUid);
        Assert.Equal("FR-7", req.Title);
    }

    // UTC48: attrs.reqUid blank/whitespace -> Falls back to regex uid
    [Fact]
    public void ExtractRequirements_WithBlankReqUidAttr_FallsBackToRegexUid()
    {
        var req = Assert.Single(Extract(ReqH(2, "REQ-05 — T", "\"reqUid\":\"   \"")));

        Assert.Equal("REQ-05", req.ReqUid);
    }

    // UTC49: attrs.reqUid blank and no regex match -> Auto uid
    [Fact]
    public void ExtractRequirements_WithBlankReqUidAttrAndNoRegex_UsesAutoUid()
    {
        var req = Assert.Single(Extract(ReqH(2, "Free text", "\"reqUid\":\"\"")));

        Assert.Equal("REQ-01", req.ReqUid);
        Assert.Equal("Free text", req.Title);
    }

    // UTC50: attrs.req is string "true" (non-bool) -> Not treated as req attr, ignored without regex match
    [Fact]
    public void ExtractRequirements_WithStringReqAttr_NotTreatedAsRequirement()
    {
        Assert.Empty(Extract(H(2, "Free text", "\"req\":\"true\"")));
    }

    // UTC51: Actor whitespace-only -> null
    [Fact]
    public void ExtractRequirements_WithWhitespaceActor_IsNull()
    {
        var req = Assert.Single(Extract(ReqH(2, "T", "\"actor\":\"   \",\"preconditions\":\"  \"")));

        Assert.Null(req.Actor);
        Assert.Null(req.Preconditions);
    }

    // UTC52: Actor as number / object -> null (non-string)
    [Fact]
    public void ExtractRequirements_WithNonStringActor_IsNull()
    {
        var req = Assert.Single(Extract(ReqH(2, "T", "\"actor\":5,\"preconditions\":{\"a\":1}")));

        Assert.Null(req.Actor);
        Assert.Null(req.Preconditions);
    }

    // UTC53: Actor null -> null
    [Fact]
    public void ExtractRequirements_WithNullActor_IsNull()
    {
        var req = Assert.Single(Extract(ReqH(2, "T", "\"actor\":null")));

        Assert.Null(req.Actor);
    }

    // UTC54: Requirement-looking heading nested deeper than top-level -> Not extracted (see ProcessContent UTC09)
    [Fact]
    public void ExtractRequirements_WithOnlyNonHeadingBlocks_ReturnsEmpty()
    {
        Assert.Empty(Extract(P("REQ-01 — Not a heading")));
    }

    // UTC55: Heading with missing type value ("type" absent) -> Skipped
    [Fact]
    public void ExtractRequirements_BlockWithoutType_Skipped()
    {
        var block = "{\"attrs\":{\"level\":2,\"req\":true},\"content\":[" + TextNode("REQ-01 — A") + "]}";

        Assert.Empty(Extract(block));
    }

    // =========================================================================
    // 3. BOUNDARY CASES (reqUid: 29, 30, 31 | Title: 199, 200, 201 | Actor/Preconditions: 199, 200, 201 | Body preview: 279, 280, 281)
    // =========================================================================

    // UTC56: reqUid attr length 29 -> Preserved
    [Fact]
    public void ExtractRequirements_WhenReqUidLengthIs29_Preserved()
    {
        var uid = new string('U', 29);

        var req = Assert.Single(Extract(ReqH(2, "T", "\"reqUid\":\"" + uid + "\"")));

        Assert.Equal(uid, req.ReqUid);
    }

    // UTC57: reqUid attr length 30 -> Preserved
    [Fact]
    public void ExtractRequirements_WhenReqUidLengthIs30_Preserved()
    {
        var uid = new string('U', 30);

        var req = Assert.Single(Extract(ReqH(2, "T", "\"reqUid\":\"" + uid + "\"")));

        Assert.Equal(uid, req.ReqUid);
    }

    // UTC58: reqUid attr length 31 -> Truncated to 30
    [Fact]
    public void ExtractRequirements_WhenReqUidLengthIs31_TruncatedTo30()
    {
        var req = Assert.Single(Extract(ReqH(2, "T", "\"reqUid\":\"" + new string('U', 31) + "\"")));

        Assert.Equal(new string('U', 30), req.ReqUid);
    }

    // UTC59: Two uids that only differ after char 30 -> Collide after truncation, second dropped
    [Fact]
    public void ExtractRequirements_WhenTruncatedUidsCollide_SecondDropped()
    {
        var a = new string('U', 30) + "1";
        var b = new string('U', 30) + "2";

        var list = Extract(ReqH(2, "A", "\"reqUid\":\"" + a + "\""), ReqH(2, "B", "\"reqUid\":\"" + b + "\""));

        var req = Assert.Single(list);
        Assert.Equal("A", req.Title);
    }

    // UTC60: Title length 199 -> Preserved
    [Fact]
    public void ExtractRequirements_WhenTitleLengthIs199_Preserved()
    {
        var title = new string('t', 199);

        var req = Assert.Single(Extract(H(2, "REQ-01 — " + title)));

        Assert.Equal(title, req.Title);
    }

    // UTC61: Title length 200 -> Preserved
    [Fact]
    public void ExtractRequirements_WhenTitleLengthIs200_Preserved()
    {
        var title = new string('t', 200);

        var req = Assert.Single(Extract(H(2, "REQ-01 — " + title)));

        Assert.Equal(title, req.Title);
    }

    // UTC62: Title length 201 -> Truncated to 200
    [Fact]
    public void ExtractRequirements_WhenTitleLengthIs201_TruncatedTo200()
    {
        var req = Assert.Single(Extract(H(2, "REQ-01 — " + new string('t', 201))));

        Assert.Equal(new string('t', 200), req.Title);
    }

    // UTC63: Attr-only heading text length 201 -> Title truncated to 200
    [Fact]
    public void ExtractRequirements_WhenAttrOnlyTitleLengthIs201_TruncatedTo200()
    {
        var req = Assert.Single(Extract(ReqH(2, new string('x', 201))));

        Assert.Equal(new string('x', 200), req.Title);
    }

    // UTC64: Actor length 199 -> Preserved
    [Fact]
    public void ExtractRequirements_WhenActorLengthIs199_Preserved()
    {
        var req = Assert.Single(Extract(ReqH(2, "T", "\"actor\":\"" + new string('a', 199) + "\"")));

        Assert.Equal(new string('a', 199), req.Actor);
    }

    // UTC65: Actor length 200 -> Preserved
    [Fact]
    public void ExtractRequirements_WhenActorLengthIs200_Preserved()
    {
        var req = Assert.Single(Extract(ReqH(2, "T", "\"actor\":\"" + new string('a', 200) + "\"")));

        Assert.Equal(new string('a', 200), req.Actor);
    }

    // UTC66: Actor length 201 -> Truncated to 200
    [Fact]
    public void ExtractRequirements_WhenActorLengthIs201_TruncatedTo200()
    {
        var req = Assert.Single(Extract(ReqH(2, "T", "\"actor\":\"" + new string('a', 201) + "\"")));

        Assert.Equal(new string('a', 200), req.Actor);
    }

    // UTC67: Preconditions length 199 -> Preserved
    [Fact]
    public void ExtractRequirements_WhenPreconditionsLengthIs199_Preserved()
    {
        var req = Assert.Single(Extract(ReqH(2, "T", "\"preconditions\":\"" + new string('p', 199) + "\"")));

        Assert.Equal(new string('p', 199), req.Preconditions);
    }

    // UTC68: Preconditions length 200 -> Preserved
    [Fact]
    public void ExtractRequirements_WhenPreconditionsLengthIs200_Preserved()
    {
        var req = Assert.Single(Extract(ReqH(2, "T", "\"preconditions\":\"" + new string('p', 200) + "\"")));

        Assert.Equal(new string('p', 200), req.Preconditions);
    }

    // UTC69: Preconditions length 201 -> Truncated to 200
    [Fact]
    public void ExtractRequirements_WhenPreconditionsLengthIs201_TruncatedTo200()
    {
        var req = Assert.Single(Extract(ReqH(2, "T", "\"preconditions\":\"" + new string('p', 201) + "\"")));

        Assert.Equal(new string('p', 200), req.Preconditions);
    }

    // UTC70: Body length 279 -> Preview not truncated, BodyLength 279
    [Fact]
    public void ExtractRequirements_WhenBodyLengthIs279_PreviewFull()
    {
        var body = new string('b', 279);

        var req = ExtractOne(P(body));

        Assert.Equal(body, req.BodyPreview);
        Assert.Equal(279, req.BodyLength);
    }

    // UTC71: Body length 280 -> Preview not truncated, BodyLength 280
    [Fact]
    public void ExtractRequirements_WhenBodyLengthIs280_PreviewFull()
    {
        var body = new string('b', 280);

        var req = ExtractOne(P(body));

        Assert.Equal(body, req.BodyPreview);
        Assert.Equal(280, req.BodyLength);
    }

    // UTC72: Body length 281 -> Preview truncated to 280, BodyLength remains 281
    [Fact]
    public void ExtractRequirements_WhenBodyLengthIs281_PreviewTruncatedTo280()
    {
        var req = ExtractOne(P(new string('b', 281)));

        Assert.Equal(new string('b', 280), req.BodyPreview);
        Assert.Equal(281, req.BodyLength);
    }

    // UTC73: Empty body -> BodyPreview empty, BodyLength 0
    [Fact]
    public void ExtractRequirements_WhenNoBody_PreviewEmptyAndLengthZero()
    {
        var req = ExtractOne();

        Assert.Equal(string.Empty, req.BodyPreview);
        Assert.Equal(0, req.BodyLength);
    }

    // UTC74: Body spans multiple paragraphs with extra spaces -> Joined and collapsed
    [Fact]
    public void ExtractRequirements_WhenMultipleParagraphs_BodyJoinedAndCollapsed()
    {
        var req = ExtractOne(P("one   two"), P("  three  "));

        Assert.Equal("one two three", req.BodyPreview);
        Assert.Equal(13, req.BodyLength);
    }

    // UTC75: Section ends at the very last block of the document (no following heading) -> Includes all remaining blocks
    [Fact]
    public void ExtractRequirements_SectionRunsToEndOfDocument()
    {
        var req = Assert.Single(Extract(H(3, "REQ-01 — A"), P("a"), P("b"), P("c")));

        Assert.Equal("a b c", req.BodyPreview);
    }

    // UTC76: Requirement heading is the last block -> Empty body
    [Fact]
    public void ExtractRequirements_RequirementAtEndOfDocument_HasEmptyBody()
    {
        var req = Assert.Single(Extract(P("intro"), H(2, "REQ-01 — A")));

        Assert.Equal(string.Empty, req.BodyPreview);
    }
}
