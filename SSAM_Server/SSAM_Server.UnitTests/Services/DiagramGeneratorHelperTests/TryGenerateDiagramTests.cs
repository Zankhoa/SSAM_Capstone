using SSAM_Server.Services;

namespace SSAM_Server.UnitTests.Services.DiagramGeneratorHelperTests;

/// <summary>TryGenerateDiagram (returns null when fewer than 2 steps can be extracted).</summary>
public class TryGenerateDiagramTests
{
    // UTC01: Null text -> null
    [Fact]
    public void TryGenerateDiagram_WithNullText_ReturnsNull()
    {
        var result = DiagramGeneratorHelper.TryGenerateDiagram(null!, null);

        Assert.Null(result);
    }

    // UTC02: Empty / whitespace text -> null (no default steps are injected)
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGenerateDiagram_WithBlankText_ReturnsNull(string text)
    {
        var result = DiagramGeneratorHelper.TryGenerateDiagram(text, null);

        Assert.Null(result);
    }

    // UTC03: Only one valid step -> null
    [Fact]
    public void TryGenerateDiagram_WithSingleStep_ReturnsNull()
    {
        var result = DiagramGeneratorHelper.TryGenerateDiagram("Nhập thông tin", null);

        Assert.Null(result);
    }

    // UTC04: Second step shorter than 3 chars is filtered out -> only one step -> null
    [Fact]
    public void TryGenerateDiagram_WithOneStepAndOneTooShortStep_ReturnsNull()
    {
        var result = DiagramGeneratorHelper.TryGenerateDiagram("Nhập thông tin. ab", null);

        Assert.Null(result);
    }

    // UTC05: Two valid steps -> Flowchart returned
    [Fact]
    public void TryGenerateDiagram_WithTwoSteps_ReturnsFlowchart()
    {
        var result = DiagramGeneratorHelper.TryGenerateDiagram("Nhập thông tin. Lưu lại", null);

        Assert.NotNull(result);
        Assert.Equal("flowchart", result!.Kind);
        Assert.Contains("N1[\"Nhập thông tin\"]", result.Mermaid);
        Assert.Contains("N2[\"Lưu lại\"]", result.Mermaid);
        Assert.Contains("N1 --> N2", result.Mermaid);
    }

    // UTC06: Same input as GenerateDiagram -> identical result
    [Fact]
    public void TryGenerateDiagram_WithValidText_MatchesGenerateDiagram()
    {
        const string text = "Nhập thông tin. Kiểm tra dữ liệu. Lưu lại";

        var tried = DiagramGeneratorHelper.TryGenerateDiagram(text, "flow");
        var generated = DiagramGeneratorHelper.GenerateDiagram(text, "flow");

        Assert.NotNull(tried);
        Assert.Equal(generated.Kind, tried!.Kind);
        Assert.Equal(generated.Mermaid, tried.Mermaid);
        Assert.Equal(generated.Notes, tried.Notes);
    }

    // UTC07: preferredKind "sequence" -> Sequence diagram
    [Fact]
    public void TryGenerateDiagram_WithSequenceKind_ReturnsSequence()
    {
        var result = DiagramGeneratorHelper.TryGenerateDiagram("alpha;beta", "sequence");

        Assert.NotNull(result);
        Assert.Equal("sequence", result!.Kind);
        Assert.StartsWith("sequenceDiagram", result.Mermaid);
    }

    // UTC08: preferredKind "state" -> State diagram
    [Fact]
    public void TryGenerateDiagram_WithStateKind_ReturnsState()
    {
        var result = DiagramGeneratorHelper.TryGenerateDiagram("alpha;beta", "state");

        Assert.NotNull(result);
        Assert.Equal("state", result!.Kind);
        Assert.StartsWith("stateDiagram-v2", result.Mermaid);
    }

    // UTC09: No preferredKind, text mentions "trạng thái" -> kind inferred from text (state)
    [Fact]
    public void TryGenerateDiagram_WithNoKindAndStateKeyword_InfersState()
    {
        var result = DiagramGeneratorHelper.TryGenerateDiagram("Đổi trạng thái. Hoàn tất", null);

        Assert.NotNull(result);
        Assert.Equal("state", result!.Kind);
    }

    // UTC10: More than 7 steps -> capped at 7 nodes
    [Fact]
    public void TryGenerateDiagram_WithMoreThanSevenSteps_TakesFirstSeven()
    {
        var result = DiagramGeneratorHelper.TryGenerateDiagram("a1a;b2b;c3c;d4d;e5e;f6f;g7g;h8h;i9i", "flow");

        Assert.NotNull(result);
        Assert.Contains("N7[\"g7g\"]", result!.Mermaid);
        Assert.DoesNotContain("N8", result.Mermaid);
    }
}
