using SSAM_Server.Models;
using SSAM_Server.Services;

namespace SSAM_Server.UnitTests.Services.DocumentContentHelperTests;

public class SummarizeReqChangesTests
{
    private static RequirementItem Item(string uid, string hash) => new() { ReqUid = uid, ContentHash = hash };

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Item only in after -> Added
    [Fact]
    public void SummarizeReqChanges_ItemOnlyInAfter_IsAdded()
    {
        var result = DocumentContentHelper.SummarizeReqChanges(new List<RequirementItem>(), new[] { Item("REQ-01", "h1") });

        Assert.Equal(new[] { "REQ-01" }, result.Added.ToArray());
        Assert.Empty(result.Modified);
        Assert.Empty(result.Removed);
        Assert.Empty(result.Unchanged);
    }

    // UTC02: Item only in before -> Removed
    [Fact]
    public void SummarizeReqChanges_ItemOnlyInBefore_IsRemoved()
    {
        var result = DocumentContentHelper.SummarizeReqChanges(new[] { Item("REQ-01", "h1") }, new List<RequirementItem>());

        Assert.Empty(result.Added);
        Assert.Empty(result.Modified);
        Assert.Equal(new[] { "REQ-01" }, result.Removed.ToArray());
        Assert.Empty(result.Unchanged);
    }

    // UTC03: Same uid, different hash -> Modified
    [Fact]
    public void SummarizeReqChanges_SameUidDifferentHash_IsModified()
    {
        var result = DocumentContentHelper.SummarizeReqChanges(new[] { Item("REQ-01", "h1") }, new[] { Item("REQ-01", "h2") });

        Assert.Empty(result.Added);
        Assert.Equal(new[] { "REQ-01" }, result.Modified.ToArray());
        Assert.Empty(result.Removed);
        Assert.Empty(result.Unchanged);
    }

    // UTC04: Same uid, same hash -> Unchanged
    [Fact]
    public void SummarizeReqChanges_SameUidSameHash_IsUnchanged()
    {
        var result = DocumentContentHelper.SummarizeReqChanges(new[] { Item("REQ-01", "h1") }, new[] { Item("REQ-01", "h1") });

        Assert.Empty(result.Added);
        Assert.Empty(result.Modified);
        Assert.Empty(result.Removed);
        Assert.Equal(new[] { "REQ-01" }, result.Unchanged.ToArray());
    }

    // UTC05: Mixed scenario -> Each uid classified into the right bucket, after-order preserved
    [Fact]
    public void SummarizeReqChanges_MixedScenario_ClassifiesEveryItem()
    {
        var before = new[] { Item("REQ-01", "a"), Item("REQ-02", "b"), Item("REQ-03", "c"), Item("REQ-04", "d") };
        var after = new[] { Item("REQ-05", "e"), Item("REQ-02", "b2"), Item("REQ-01", "a"), Item("REQ-06", "f") };

        var result = DocumentContentHelper.SummarizeReqChanges(before, after);

        Assert.Equal(new[] { "REQ-05", "REQ-06" }, result.Added.ToArray());
        Assert.Equal(new[] { "REQ-02" }, result.Modified.ToArray());
        Assert.Equal(new[] { "REQ-03", "REQ-04" }, result.Removed.ToArray());
        Assert.Equal(new[] { "REQ-01" }, result.Unchanged.ToArray());
    }

    // UTC06: Uid comparison is case-insensitive -> Matched as same requirement
    [Fact]
    public void SummarizeReqChanges_UidCaseInsensitive_Matches()
    {
        var result = DocumentContentHelper.SummarizeReqChanges(new[] { Item("req-01", "h") }, new[] { Item("REQ-01", "h") });

        Assert.Empty(result.Added);
        Assert.Empty(result.Removed);
        Assert.Equal(new[] { "REQ-01" }, result.Unchanged.ToArray());
    }

    // UTC07: Hash comparison is case-insensitive -> Unchanged
    [Fact]
    public void SummarizeReqChanges_HashCaseInsensitive_IsUnchanged()
    {
        var result = DocumentContentHelper.SummarizeReqChanges(new[] { Item("REQ-01", "ABCDEF") }, new[] { Item("REQ-01", "abcdef") });

        Assert.Empty(result.Modified);
        Assert.Equal(new[] { "REQ-01" }, result.Unchanged.ToArray());
    }

    // UTC08: Removed uses before casing, Added/Modified use after casing
    [Fact]
    public void SummarizeReqChanges_UsesOriginalCasingOfRespectiveSide()
    {
        var result = DocumentContentHelper.SummarizeReqChanges(
            new[] { Item("req-01", "a"), Item("fr-9", "x") },
            new[] { Item("REQ-01", "b") });

        Assert.Equal(new[] { "REQ-01" }, result.Modified.ToArray());
        Assert.Equal(new[] { "fr-9" }, result.Removed.ToArray());
    }

    // UTC09: Both lists identical (multiple) -> All unchanged in order
    [Fact]
    public void SummarizeReqChanges_IdenticalLists_AllUnchanged()
    {
        var items = new[] { Item("REQ-01", "a"), Item("REQ-02", "b"), Item("REQ-03", "c") };

        var result = DocumentContentHelper.SummarizeReqChanges(items, items);

        Assert.Equal(new[] { "REQ-01", "REQ-02", "REQ-03" }, result.Unchanged.ToArray());
        Assert.Empty(result.Added);
        Assert.Empty(result.Modified);
        Assert.Empty(result.Removed);
    }

    // UTC10: Completely disjoint lists -> All added and all removed
    [Fact]
    public void SummarizeReqChanges_DisjointLists_AllAddedAndRemoved()
    {
        var result = DocumentContentHelper.SummarizeReqChanges(
            new[] { Item("A", "1"), Item("B", "2") },
            new[] { Item("C", "3"), Item("D", "4") });

        Assert.Equal(new[] { "C", "D" }, result.Added.ToArray());
        Assert.Equal(new[] { "A", "B" }, result.Removed.ToArray());
        Assert.Empty(result.Modified);
        Assert.Empty(result.Unchanged);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC11: Duplicate uid in before -> Throws ArgumentException
    [Fact]
    public void SummarizeReqChanges_DuplicateUidInBefore_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => DocumentContentHelper.SummarizeReqChanges(
            new[] { Item("REQ-01", "a"), Item("REQ-01", "b") },
            new List<RequirementItem>()));
    }

    // UTC12: Duplicate uid in after -> Throws ArgumentException
    [Fact]
    public void SummarizeReqChanges_DuplicateUidInAfter_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => DocumentContentHelper.SummarizeReqChanges(
            new List<RequirementItem>(),
            new[] { Item("REQ-01", "a"), Item("REQ-01", "b") }));
    }

    // UTC13: Uids differing only by case in the same list -> Throws ArgumentException
    [Fact]
    public void SummarizeReqChanges_CaseDuplicateInSameList_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => DocumentContentHelper.SummarizeReqChanges(
            new[] { Item("req-01", "a"), Item("REQ-01", "a") },
            new List<RequirementItem>()));
    }

    // UTC14: Null before list -> Throws ArgumentNullException
    [Fact]
    public void SummarizeReqChanges_NullBefore_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => DocumentContentHelper.SummarizeReqChanges(null!, new List<RequirementItem>()));
    }

    // UTC15: Null after list -> Throws ArgumentNullException
    [Fact]
    public void SummarizeReqChanges_NullAfter_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => DocumentContentHelper.SummarizeReqChanges(new List<RequirementItem>(), null!));
    }

    // UTC16: Empty uid on both sides with same hash -> Treated as a normal key (Unchanged)
    [Fact]
    public void SummarizeReqChanges_EmptyUid_TreatedAsKey()
    {
        var result = DocumentContentHelper.SummarizeReqChanges(new[] { Item("", "h") }, new[] { Item("", "h") });

        Assert.Equal(new[] { "" }, result.Unchanged.ToArray());
    }

    // UTC17: Empty hash vs non-empty hash -> Modified
    [Fact]
    public void SummarizeReqChanges_EmptyHashVsNonEmpty_IsModified()
    {
        var result = DocumentContentHelper.SummarizeReqChanges(new[] { Item("R", "") }, new[] { Item("R", "x") });

        Assert.Equal(new[] { "R" }, result.Modified.ToArray());
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC18: Both lists empty -> Every bucket empty
    [Fact]
    public void SummarizeReqChanges_BothEmpty_AllBucketsEmpty()
    {
        var result = DocumentContentHelper.SummarizeReqChanges(new List<RequirementItem>(), new List<RequirementItem>());

        Assert.Empty(result.Added);
        Assert.Empty(result.Modified);
        Assert.Empty(result.Removed);
        Assert.Empty(result.Unchanged);
    }

    // UTC19: Single item on each side with one-character difference in hash -> Modified
    [Fact]
    public void SummarizeReqChanges_OneCharHashDifference_IsModified()
    {
        var result = DocumentContentHelper.SummarizeReqChanges(new[] { Item("R", "abc1") }, new[] { Item("R", "abc2") });

        Assert.Equal(new[] { "R" }, result.Modified.ToArray());
    }

    // UTC20: Large lists (1000 items, every 10th modified) -> Counts correct
    [Fact]
    public void SummarizeReqChanges_LargeLists_CountsCorrect()
    {
        var before = Enumerable.Range(0, 1000).Select(i => Item("R" + i, "h" + i)).ToList();
        var after = Enumerable.Range(0, 1000).Select(i => Item("R" + i, i % 10 == 0 ? "changed" : "h" + i)).ToList();

        var result = DocumentContentHelper.SummarizeReqChanges(before, after);

        Assert.Equal(100, result.Modified.Count);
        Assert.Equal(900, result.Unchanged.Count);
        Assert.Empty(result.Added);
        Assert.Empty(result.Removed);
    }
}
