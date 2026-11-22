using System.Text.Json.Nodes;
using SSAM_Server.Models;
using SSAM_Server.Services;

namespace SSAM_Server.UnitTests.Services.DocumentContentHelperTests;

public class BuildInitialTemplateContentJsonTests
{
    private static JsonArray ContentOf(string json)
    {
        var root = (JsonObject)JsonNode.Parse(json)!;
        Assert.Equal("doc", root["type"]!.GetValue<string>());
        return (JsonArray)root["content"]!;
    }

    private static string TextOf(JsonNode? node)
        => ((JsonObject)((JsonArray)node!["content"]!)[0]!)["text"]!.GetValue<string>();

    private static void AssertRequirementTemplate(string json, string title, string expectedUid)
    {
        var content = ContentOf(json);
        Assert.Equal(5, content.Count);

        Assert.Equal("heading", content[0]!["type"]!.GetValue<string>());
        Assert.Equal(2, content[0]!["attrs"]!["level"]!.GetValue<int>());
        Assert.Equal("1. Bối cảnh & Mục tiêu", TextOf(content[0]));
        Assert.Null(content[0]!["attrs"]!["req"]);

        Assert.Equal("paragraph", content[1]!["type"]!.GetValue<string>());
        Assert.Equal($"Tài liệu {title} mô tả phạm vi, tác nhân và các yêu cầu nghiệp vụ cốt lõi.", TextOf(content[1]));

        Assert.Equal("heading", content[2]!["type"]!.GetValue<string>());
        Assert.Equal(2, content[2]!["attrs"]!["level"]!.GetValue<int>());
        Assert.True(content[2]!["attrs"]!["req"]!.GetValue<bool>());
        Assert.Equal(expectedUid, content[2]!["attrs"]!["reqUid"]!.GetValue<string>());
        Assert.Equal("MEDIUM", content[2]!["attrs"]!["priority"]!.GetValue<string>());
        Assert.Equal($"{expectedUid} — Yêu cầu chức năng mẫu", TextOf(content[2]));

        Assert.Equal("paragraph", content[3]!["type"]!.GetValue<string>());
        Assert.Equal("Mô tả chi tiết luồng xử lý chính và quy tắc nghiệp vụ cho yêu cầu này.", TextOf(content[3]));

        Assert.Equal("taskList", content[4]!["type"]!.GetValue<string>());
        var items = (JsonArray)content[4]!["content"]!;
        Assert.Equal(2, items.Count);
        Assert.Equal("Hệ thống kiểm tra điều kiện đầu vào hợp lệ.", TextOf(((JsonArray)items[0]!["content"]!)[0]));
        Assert.Equal("Ghi nhận lịch sử thao tác và trả về trạng thái thành công.", TextOf(((JsonArray)items[1]!["content"]!)[0]));
    }

    private static void AssertGenericTemplate(string json, string title)
    {
        var content = ContentOf(json);
        Assert.Equal(2, content.Count);
        Assert.Equal("heading", content[0]!["type"]!.GetValue<string>());
        Assert.Equal(2, content[0]!["attrs"]!["level"]!.GetValue<int>());
        Assert.Equal("1. Tổng quan", TextOf(content[0]));
        Assert.Equal("paragraph", content[1]!["type"]!.GetValue<string>());
        Assert.Equal($"Nội dung khởi tạo cho tài liệu {title}.", TextOf(content[1]));
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: URD with default reqUid -> Requirement template with REQ-01
    [Fact]
    public void BuildInitialTemplateContentJson_Urd_ReturnsRequirementTemplateWithDefaultUid()
    {
        var json = DocumentContentHelper.BuildInitialTemplateContentJson(DocumentTypes.Urd, "Quản lý đơn hàng");

        AssertRequirementTemplate(json, "Quản lý đơn hàng", "REQ-01");
    }

    // UTC02: BRD -> Requirement template
    [Fact]
    public void BuildInitialTemplateContentJson_Brd_ReturnsRequirementTemplate()
    {
        AssertRequirementTemplate(DocumentContentHelper.BuildInitialTemplateContentJson(DocumentTypes.Brd, "T"), "T", "REQ-01");
    }

    // UTC03: SRS -> Requirement template
    [Fact]
    public void BuildInitialTemplateContentJson_Srs_ReturnsRequirementTemplate()
    {
        AssertRequirementTemplate(DocumentContentHelper.BuildInitialTemplateContentJson(DocumentTypes.Srs, "T"), "T", "REQ-01");
    }

    // UTC04: Custom reqUid -> Used in heading text and attrs
    [Fact]
    public void BuildInitialTemplateContentJson_WithCustomReqUid_UsesIt()
    {
        var json = DocumentContentHelper.BuildInitialTemplateContentJson(DocumentTypes.Urd, "Doc", "REQ-42");

        AssertRequirementTemplate(json, "Doc", "REQ-42");
    }

    // UTC05: ADR / UI_SPEC / TEST_PLAN / OTHER -> Generic template
    [Theory]
    [InlineData(DocumentTypes.Adr)]       // UTC05
    [InlineData(DocumentTypes.UiSpec)]    // UTC06
    [InlineData(DocumentTypes.TestPlan)]  // UTC07
    [InlineData(DocumentTypes.Other)]     // UTC08
    public void BuildInitialTemplateContentJson_NonRequirementTypes_ReturnsGenericTemplate(string docType)
    {
        AssertGenericTemplate(DocumentContentHelper.BuildInitialTemplateContentJson(docType, "My doc"), "My doc");
    }

    // UTC09: reqUid is ignored for generic types -> Generic template unchanged
    [Fact]
    public void BuildInitialTemplateContentJson_GenericTypeIgnoresReqUid()
    {
        var json = DocumentContentHelper.BuildInitialTemplateContentJson(DocumentTypes.Adr, "A", "REQ-9");

        AssertGenericTemplate(json, "A");
        Assert.DoesNotContain("REQ-9", json);
    }

    // UTC10: Headings have generated 10-char anchor ids mirrored in id and anchorId
    [Fact]
    public void BuildInitialTemplateContentJson_HeadingsHaveGeneratedAnchorIds()
    {
        var content = ContentOf(DocumentContentHelper.BuildInitialTemplateContentJson(DocumentTypes.Urd, "T"));

        foreach (var idx in new[] { 0, 2 })
        {
            var attrs = content[idx]!["attrs"]!;
            Assert.Matches("^h-[0-9a-f]{8}$", attrs["id"]!.GetValue<string>());
            Assert.Equal(attrs["id"]!.GetValue<string>(), attrs["anchorId"]!.GetValue<string>());
        }
    }

    // UTC11: Result round-trips through ProcessContent -> One requirement with the supplied uid and 2 criteria
    [Fact]
    public void BuildInitialTemplateContentJson_RoundTripsThroughProcessContent()
    {
        var json = DocumentContentHelper.BuildInitialTemplateContentJson(DocumentTypes.Srs, "Hệ thống", "REQ-07");

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var result = DocumentContentHelper.ProcessContent(doc.RootElement);

        var req = Assert.Single(result.Requirements);
        Assert.Equal("REQ-07", req.ReqUid);
        Assert.Equal("Yêu cầu chức năng mẫu", req.Title);
        Assert.Equal(2, req.AcceptanceCriteria.Count);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC12: Unknown doc type -> Generic template
    [Fact]
    public void BuildInitialTemplateContentJson_UnknownType_ReturnsGenericTemplate()
    {
        AssertGenericTemplate(DocumentContentHelper.BuildInitialTemplateContentJson("UNKNOWN", "T"), "T");
    }

    // UTC13: Lowercase "urd" (case-sensitive comparison) -> Generic template
    [Fact]
    public void BuildInitialTemplateContentJson_LowercaseUrd_ReturnsGenericTemplate()
    {
        AssertGenericTemplate(DocumentContentHelper.BuildInitialTemplateContentJson("urd", "T"), "T");
    }

    // UTC14: Empty doc type -> Generic template
    [Fact]
    public void BuildInitialTemplateContentJson_EmptyDocType_ReturnsGenericTemplate()
    {
        AssertGenericTemplate(DocumentContentHelper.BuildInitialTemplateContentJson("", "T"), "T");
    }

    // UTC15: Null doc type -> Generic template
    [Fact]
    public void BuildInitialTemplateContentJson_NullDocType_ReturnsGenericTemplate()
    {
        AssertGenericTemplate(DocumentContentHelper.BuildInitialTemplateContentJson(null!, "T"), "T");
    }

    // UTC16: Title with quotes, backslash and newline -> Escaped in JSON, restored on parse
    [Fact]
    public void BuildInitialTemplateContentJson_TitleWithSpecialChars_IsEscaped()
    {
        var title = "He said \"hi\" \\ path\nline";

        var json = DocumentContentHelper.BuildInitialTemplateContentJson(DocumentTypes.Adr, title);

        AssertGenericTemplate(json, title);
    }

    // UTC17: Null title -> Interpolated as empty text
    [Fact]
    public void BuildInitialTemplateContentJson_NullTitle_UsesEmptyTitle()
    {
        AssertGenericTemplate(DocumentContentHelper.BuildInitialTemplateContentJson(DocumentTypes.Other, null!), "");
    }

    // UTC18: Empty reqUid (not null) -> Used as is, heading text starts with " — "
    [Fact]
    public void BuildInitialTemplateContentJson_EmptyReqUid_UsedAsIs()
    {
        var content = ContentOf(DocumentContentHelper.BuildInitialTemplateContentJson(DocumentTypes.Urd, "T", ""));

        Assert.Equal("", content[2]!["attrs"]!["reqUid"]!.GetValue<string>());
        Assert.Equal(" — Yêu cầu chức năng mẫu", TextOf(content[2]));
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC19: Empty title -> Paragraph text with empty title
    [Fact]
    public void BuildInitialTemplateContentJson_EmptyTitle_Handled()
    {
        AssertRequirementTemplate(DocumentContentHelper.BuildInitialTemplateContentJson(DocumentTypes.Urd, ""), "", "REQ-01");
    }

    // UTC20: Very long title (10000 chars) -> Preserved fully
    [Fact]
    public void BuildInitialTemplateContentJson_VeryLongTitle_Preserved()
    {
        var title = new string('x', 10000);

        AssertGenericTemplate(DocumentContentHelper.BuildInitialTemplateContentJson(DocumentTypes.Adr, title), title);
    }

    // UTC21: Unicode / emoji title -> Preserved
    [Fact]
    public void BuildInitialTemplateContentJson_UnicodeTitle_Preserved()
    {
        AssertGenericTemplate(DocumentContentHelper.BuildInitialTemplateContentJson(DocumentTypes.Adr, "Phòng 😀 Ban"), "Phòng 😀 Ban");
    }

    // UTC22: Two calls generate different heading ids -> Not deterministic
    [Fact]
    public void BuildInitialTemplateContentJson_TwoCalls_HaveDifferentAnchorIds()
    {
        var a = ContentOf(DocumentContentHelper.BuildInitialTemplateContentJson(DocumentTypes.Adr, "T"))[0]!["attrs"]!["id"]!.GetValue<string>();
        var b = ContentOf(DocumentContentHelper.BuildInitialTemplateContentJson(DocumentTypes.Adr, "T"))[0]!["attrs"]!["id"]!.GetValue<string>();

        Assert.NotEqual(a, b);
    }
}
