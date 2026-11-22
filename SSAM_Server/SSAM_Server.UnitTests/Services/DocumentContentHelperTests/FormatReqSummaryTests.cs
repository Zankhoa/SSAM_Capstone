using SSAM_Server.Services;

namespace SSAM_Server.UnitTests.Services.DocumentContentHelperTests;

public class FormatReqSummaryTests
{
    private const string NoChange = "Không thay đổi yêu cầu nào";

    private static DocumentContentHelper.ReqChangeSummary Summary(
        string[]? added = null, string[]? modified = null, string[]? removed = null, string[]? unchanged = null)
        => new(
            (added ?? Array.Empty<string>()).ToList(),
            (modified ?? Array.Empty<string>()).ToList(),
            (removed ?? Array.Empty<string>()).ToList(),
            (unchanged ?? Array.Empty<string>()).ToList());

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Only modified -> "Sửa ..."
    [Fact]
    public void FormatReqSummary_OnlyModified_ReturnsSua()
    {
        Assert.Equal("Sửa REQ-01", DocumentContentHelper.FormatReqSummary(Summary(modified: new[] { "REQ-01" })));
    }

    // UTC02: Only added -> "Thêm ..."
    [Fact]
    public void FormatReqSummary_OnlyAdded_ReturnsThem()
    {
        Assert.Equal("Thêm REQ-02", DocumentContentHelper.FormatReqSummary(Summary(added: new[] { "REQ-02" })));
    }

    // UTC03: Only removed -> "Xoá ..."
    [Fact]
    public void FormatReqSummary_OnlyRemoved_ReturnsXoa()
    {
        Assert.Equal("Xoá REQ-03", DocumentContentHelper.FormatReqSummary(Summary(removed: new[] { "REQ-03" })));
    }

    // UTC04: Several uids in a bucket -> Joined with ", "
    [Fact]
    public void FormatReqSummary_MultipleUidsInBucket_JoinedWithComma()
    {
        Assert.Equal("Sửa A, B, C", DocumentContentHelper.FormatReqSummary(Summary(modified: new[] { "A", "B", "C" })));
    }

    // UTC05: All three buckets -> Order Sửa, Thêm, Xoá joined with " · "
    [Fact]
    public void FormatReqSummary_AllBuckets_OrderedModifiedAddedRemoved()
    {
        var text = DocumentContentHelper.FormatReqSummary(Summary(
            added: new[] { "A1", "A2" }, modified: new[] { "M1" }, removed: new[] { "R1" }));

        Assert.Equal("Sửa M1 · Thêm A1, A2 · Xoá R1", text);
    }

    // UTC06: Modified + added -> "Sửa .. · Thêm .."
    [Fact]
    public void FormatReqSummary_ModifiedAndAdded_Combined()
    {
        Assert.Equal("Sửa M · Thêm A", DocumentContentHelper.FormatReqSummary(Summary(added: new[] { "A" }, modified: new[] { "M" })));
    }

    // UTC07: Modified + removed -> "Sửa .. · Xoá .."
    [Fact]
    public void FormatReqSummary_ModifiedAndRemoved_Combined()
    {
        Assert.Equal("Sửa M · Xoá R", DocumentContentHelper.FormatReqSummary(Summary(modified: new[] { "M" }, removed: new[] { "R" })));
    }

    // UTC08: Added + removed -> "Thêm .. · Xoá .."
    [Fact]
    public void FormatReqSummary_AddedAndRemoved_Combined()
    {
        Assert.Equal("Thêm A · Xoá R", DocumentContentHelper.FormatReqSummary(Summary(added: new[] { "A" }, removed: new[] { "R" })));
    }

    // UTC09: Unchanged items are never mentioned -> Ignored when other buckets exist
    [Fact]
    public void FormatReqSummary_UnchangedIgnoredWhenOtherBucketsExist()
    {
        var text = DocumentContentHelper.FormatReqSummary(Summary(added: new[] { "A" }, unchanged: new[] { "U1", "U2" }));

        Assert.Equal("Thêm A", text);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC10: All buckets empty -> "Không thay đổi yêu cầu nào"
    [Fact]
    public void FormatReqSummary_AllEmpty_ReturnsNoChangeMessage()
    {
        Assert.Equal(NoChange, DocumentContentHelper.FormatReqSummary(Summary()));
    }

    // UTC11: Only unchanged items -> "Không thay đổi yêu cầu nào"
    [Fact]
    public void FormatReqSummary_OnlyUnchanged_ReturnsNoChangeMessage()
    {
        Assert.Equal(NoChange, DocumentContentHelper.FormatReqSummary(Summary(unchanged: new[] { "U" })));
    }

    // UTC12: Null summary -> Throws NullReferenceException
    [Fact]
    public void FormatReqSummary_NullSummary_ThrowsNullReferenceException()
    {
        Assert.Throws<NullReferenceException>(() => DocumentContentHelper.FormatReqSummary(null!));
    }

    // UTC13: Bucket containing an empty uid -> Still reported (no filtering)
    [Fact]
    public void FormatReqSummary_EmptyUidInBucket_StillReported()
    {
        Assert.Equal("Thêm ", DocumentContentHelper.FormatReqSummary(Summary(added: new[] { "" })));
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC14: Exactly one item in one bucket -> No comma / separator
    [Fact]
    public void FormatReqSummary_SingleItem_NoSeparators()
    {
        var text = DocumentContentHelper.FormatReqSummary(Summary(removed: new[] { "X" }));

        Assert.DoesNotContain(",", text);
        Assert.DoesNotContain("·", text);
    }

    // UTC15: Many items (100) -> All present joined by ", "
    [Fact]
    public void FormatReqSummary_ManyItems_AllListed()
    {
        var ids = Enumerable.Range(1, 100).Select(i => "R" + i).ToArray();

        var text = DocumentContentHelper.FormatReqSummary(Summary(added: ids));

        Assert.Equal("Thêm " + string.Join(", ", ids), text);
    }
}
