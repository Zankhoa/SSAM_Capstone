using SSAM_Server.Services;

namespace SSAM_Server.UnitTests.Services.DiagramGeneratorHelperTests;

/// <summary>GenerateDiagram (+ NormalizeKind, BuildFlow, BuildSequence, BuildState, EscapeLabel, step parsing).</summary>
public class GenerateDiagramTests
{
    private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines);

    private static string Flow(params string[] labels)
    {
        var lines = new List<string> { "flowchart TD" };
        for (var i = 0; i < labels.Length; i++)
        {
            lines.Add($"    N{i + 1}[\"{labels[i]}\"]");
            if (i > 0) lines.Add($"    N{i} --> N{i + 1}");
        }
        return Lines(lines.ToArray());
    }

    private static string State(params string[] labels)
    {
        var lines = new List<string> { "stateDiagram-v2", "    [*] --> S1" };
        for (var i = 0; i < labels.Length; i++)
        {
            lines.Add($"    S{i + 1} : {labels[i]}");
            if (i > 0) lines.Add($"    S{i} --> S{i + 1}");
        }
        lines.Add($"    S{labels.Length} --> [*]");
        return Lines(lines.ToArray());
    }

    private static string Sequence(params string[] labels)
    {
        var lines = new List<string>
        {
            "sequenceDiagram",
            "    actor User as Người dùng",
            "    participant System as Hệ thống",
            "    participant DB as Cơ sở dữ liệu"
        };
        for (var i = 0; i < labels.Length; i++)
        {
            if (i % 2 == 0)
            {
                lines.Add($"    User->>System: {labels[i]}");
            }
            else
            {
                lines.Add($"    System->>DB: {labels[i]}");
                lines.Add("    DB-->>System: Kết quả");
            }
        }
        lines.Add("    System-->>User: Phản hồi hoàn tất");
        return Lines(lines.ToArray());
    }

    private static string FlowNote(int n) => $"Sơ đồ luồng (Flowchart) gồm {n} bước được trích xuất từ mô tả.";
    private static string StateNote(int n) => $"Sơ đồ trạng thái gồm {n} trạng thái chuyển tiếp.";
    private const string SequenceNote = "Sơ đồ tuần tự (Sequence Diagram) minh hoạ tương tác giữa Người dùng, Hệ thống và CSDL.";

    private static readonly string[] DefaultSteps = { "Bắt đầu quy trình", "Xử lý yêu cầu", "Hoàn tất" };

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Plain text, no preferred kind -> Flowchart with exact mermaid and note
    [Fact]
    public void GenerateDiagram_WithPlainTextAndNoKind_ReturnsFlowchart()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("Nhập thông tin. Kiểm tra dữ liệu. Lưu lại", null);

        Assert.Equal("flowchart", result.Kind);
        Assert.Equal(Flow("Nhập thông tin", "Kiểm tra dữ liệu", "Lưu lại"), result.Mermaid);
        Assert.Equal(new[] { FlowNote(3) }, result.Notes.ToArray());
    }

    // UTC02: preferredKind "sequence" -> Sequence diagram exact output
    [Fact]
    public void GenerateDiagram_WithSequenceKind_ReturnsSequenceDiagram()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("Mở trang. Nhập mã. Xác nhận", "sequence");

        Assert.Equal("sequence", result.Kind);
        Assert.Equal(Sequence("Mở trang", "Nhập mã", "Xác nhận"), result.Mermaid);
        Assert.Equal(new[] { SequenceNote }, result.Notes.ToArray());
    }

    // UTC03: preferredKind "state" -> State diagram exact output
    [Fact]
    public void GenerateDiagram_WithStateKind_ReturnsStateDiagram()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("Mới tạo. Đang xử lý. Hoàn thành", "state");

        Assert.Equal("state", result.Kind);
        Assert.Equal(State("Mới tạo", "Đang xử lý", "Hoàn thành"), result.Mermaid);
        Assert.Equal(new[] { StateNote(3) }, result.Notes.ToArray());
    }

    // UTC04: preferredKind "flow" -> Flowchart
    // UTC05: "FLOWCHART" -> Flowchart
    // UTC06: "flowchart" mixed case/padding -> Flowchart
    [Theory]
    [InlineData("flow")]          // UTC04
    [InlineData("FLOWCHART")]     // UTC05
    [InlineData("  FlowChart ")]  // UTC06
    public void GenerateDiagram_WithFlowKindVariants_ReturnsFlowchart(string kind)
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("alpha step. beta step", kind);

        Assert.Equal("flowchart", result.Kind);
        Assert.StartsWith("flowchart TD", result.Mermaid);
    }

    // UTC07: "SEQ" / "seq" / " Sequence " -> Sequence
    [Theory]
    [InlineData("SEQ")]           // UTC07
    [InlineData("seq")]           // UTC08
    [InlineData("  Sequence ")]   // UTC09
    public void GenerateDiagram_WithSequenceKindVariants_ReturnsSequence(string kind)
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("alpha step. beta step", kind);

        Assert.Equal("sequence", result.Kind);
        Assert.StartsWith("sequenceDiagram", result.Mermaid);
    }

    // UTC10: "STATE" / "ER" -> State (ER maps to state)
    [Theory]
    [InlineData("STATE")]    // UTC10
    [InlineData("er")]       // UTC11
    [InlineData(" ER ")]     // UTC12
    public void GenerateDiagram_WithStateKindVariants_ReturnsState(string kind)
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("alpha step. beta step", kind);

        Assert.Equal("state", result.Kind);
        Assert.StartsWith("stateDiagram-v2", result.Mermaid);
    }

    // UTC13: Preferred kind wins over text keywords (text says "state", kind says flow)
    [Fact]
    public void GenerateDiagram_PreferredKindOverridesTextKeywords()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("change state of order", "flow");

        Assert.Equal("flowchart", result.Kind);
    }

    // UTC14: Text with "trạng thái" -> Inferred state
    // UTC15: "state" -> state
    // UTC16: "status" -> state
    // UTC17: "chuyển sang" -> state
    [Theory]
    [InlineData("Đơn hàng có trạng thái mới")]       // UTC14
    [InlineData("order state changes")]               // UTC15
    [InlineData("check status now")]                  // UTC16
    [InlineData("Đơn hàng chuyển sang đã giao")]      // UTC17
    public void GenerateDiagram_TextWithStateKeyword_InfersState(string text)
    {
        Assert.Equal("state", DiagramGeneratorHelper.GenerateDiagram(text, null).Kind);
    }

    // UTC18: "api" -> sequence
    // UTC19: "gửi" -> sequence
    // UTC20: "phản hồi" -> sequence
    // UTC21: "request" -> sequence
    // UTC22: "client" -> sequence
    [Theory]
    [InlineData("Gọi API thanh toán")]        // UTC18
    [InlineData("Người dùng gửi biểu mẫu")]   // UTC19
    [InlineData("Máy chủ phản hồi kết quả")]  // UTC20
    [InlineData("Send request to server")]    // UTC21
    [InlineData("The Client opens page")]     // UTC22
    public void GenerateDiagram_TextWithSequenceKeyword_InfersSequence(string text)
    {
        Assert.Equal("sequence", DiagramGeneratorHelper.GenerateDiagram(text, null).Kind);
    }

    // UTC23: Keywords are case-insensitive (upper-case text) -> inferred
    [Fact]
    public void GenerateDiagram_KeywordsCaseInsensitive()
    {
        Assert.Equal("state", DiagramGeneratorHelper.GenerateDiagram("TRẠNG THÁI MỚI", null).Kind);
        Assert.Equal("sequence", DiagramGeneratorHelper.GenerateDiagram("CALL THE API", null).Kind);
    }

    // UTC24: Text with both state and sequence keywords -> State wins
    [Fact]
    public void GenerateDiagram_StateKeywordTakesPriorityOverSequence()
    {
        Assert.Equal("state", DiagramGeneratorHelper.GenerateDiagram("api updates the status", null).Kind);
    }

    // UTC25: Steps split on newline (\n)
    [Fact]
    public void GenerateDiagram_SplitsOnLineFeed()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("first step\nsecond step", "flow");

        Assert.Equal(Flow("first step", "second step"), result.Mermaid);
    }

    // UTC26: Steps split on CRLF -> No empty entries
    [Fact]
    public void GenerateDiagram_SplitsOnCrLf()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("first step\r\nsecond step\r\nthird step", "flow");

        Assert.Equal(Flow("first step", "second step", "third step"), result.Mermaid);
    }

    // UTC27: Steps split on semicolon
    [Fact]
    public void GenerateDiagram_SplitsOnSemicolon()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("first step;second step", "flow");

        Assert.Equal(Flow("first step", "second step"), result.Mermaid);
    }

    // UTC28: Steps split on period
    [Fact]
    public void GenerateDiagram_SplitsOnPeriod()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("first step. second step.", "flow");

        Assert.Equal(Flow("first step", "second step"), result.Mermaid);
    }

    // UTC29: Bullet prefixes "-", "*", "•", "+" -> Stripped
    [Theory]
    [InlineData("- Do work")]   // UTC29
    [InlineData("* Do work")]   // UTC30
    [InlineData("• Do work")]   // UTC31
    [InlineData("+ Do work")]   // UTC32
    [InlineData("-Do work")]    // UTC33
    public void GenerateDiagram_StripsBulletPrefix(string line)
    {
        var result = DiagramGeneratorHelper.GenerateDiagram(line, "flow");

        Assert.Equal(Flow("Do work"), result.Mermaid);
    }

    // UTC34: Numbered prefix "1) " -> Stripped
    [Fact]
    public void GenerateDiagram_StripsNumberedParenPrefix()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("1) Open page\n2) Submit form", "flow");

        Assert.Equal(Flow("Open page", "Submit form"), result.Mermaid);
    }

    // UTC35: Numbered prefix "1. " -> Period splits first, "1" is shorter than 3 chars and dropped
    [Fact]
    public void GenerateDiagram_NumberedDotPrefix_LeavesTextAfterDot()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("1. Open page\n2. Submit form", "flow");

        Assert.Equal(Flow("Open page", "Submit form"), result.Mermaid);
    }

    // UTC36: Prefix strip is also applied mid-text for "digits)" (quirk of unanchored second alternative)
    [Fact]
    public void GenerateDiagram_DigitParenMidText_IsStrippedToo()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("Chọn mục 2) xong", "flow");

        Assert.Equal(Flow("Chọn mục xong"), result.Mermaid);
    }

    // UTC37: Steps are trimmed
    [Fact]
    public void GenerateDiagram_TrimsSteps()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("   alpha   ;\t beta  ", "flow");

        Assert.Equal(Flow("alpha", "beta"), result.Mermaid);
    }

    // UTC38: Double quotes in a step -> Replaced by single quotes (EscapeLabel)
    [Fact]
    public void GenerateDiagram_EscapesDoubleQuotes()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("Click \"Save\" button", "flow");

        Assert.Equal(Flow("Click 'Save' button"), result.Mermaid);
    }

    // UTC39: Sequence with 4 steps -> Alternates User->>System and System->>DB with result lines
    [Fact]
    public void GenerateDiagram_SequenceFourSteps_AlternatesParticipants()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("aaa;bbb;ccc;ddd", "seq");

        Assert.Equal(Sequence("aaa", "bbb", "ccc", "ddd"), result.Mermaid);
    }

    // UTC40: Sequence with 1 step -> Only first message then final reply
    [Fact]
    public void GenerateDiagram_SequenceOneStep_OnlyUserMessage()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("only step", "seq");

        Assert.Equal(Sequence("only step"), result.Mermaid);
        Assert.DoesNotContain("System->>DB", result.Mermaid);
    }

    // UTC41: State with 1 step -> [*] --> S1, label, S1 --> [*]
    [Fact]
    public void GenerateDiagram_StateOneStep_HasStartAndEnd()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("only step", "state");

        Assert.Equal(State("only step"), result.Mermaid);
        Assert.Equal(new[] { StateNote(1) }, result.Notes.ToArray());
    }

    // UTC42: Flow with 1 step -> Single node, no edges
    [Fact]
    public void GenerateDiagram_FlowOneStep_NoEdges()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("only step", "flow");

        Assert.Equal(Flow("only step"), result.Mermaid);
        Assert.DoesNotContain("-->", result.Mermaid);
        Assert.Equal(new[] { FlowNote(1) }, result.Notes.ToArray());
    }

    // UTC43: Unicode (Vietnamese) steps -> Preserved
    [Fact]
    public void GenerateDiagram_VietnameseSteps_Preserved()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("Người dùng đăng nhập; Hệ thống xác thực", "flow");

        Assert.Equal(Flow("Người dùng đăng nhập", "Hệ thống xác thực"), result.Mermaid);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC44: Null text -> Default three steps (flowchart)
    [Fact]
    public void GenerateDiagram_WithNullText_UsesDefaultSteps()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram(null!, null);

        Assert.Equal("flowchart", result.Kind);
        Assert.Equal(Flow(DefaultSteps), result.Mermaid);
        Assert.Equal(new[] { FlowNote(3) }, result.Notes.ToArray());
    }

    // UTC45: Empty text -> Default steps
    // UTC46: Whitespace text -> Default steps
    [Theory]
    [InlineData("")]          // UTC45
    [InlineData("   \r\n  ")] // UTC46
    public void GenerateDiagram_WithEmptyOrWhitespaceText_UsesDefaultSteps(string text)
    {
        var result = DiagramGeneratorHelper.GenerateDiagram(text, null);

        Assert.Equal(Flow(DefaultSteps), result.Mermaid);
    }

    // UTC47: All steps shorter than 3 chars -> Default steps
    [Fact]
    public void GenerateDiagram_WithOnlyShortSteps_UsesDefaultSteps()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("a;bc;de.f", "flow");

        Assert.Equal(Flow(DefaultSteps), result.Mermaid);
    }

    // UTC48: Only separators -> Default steps
    [Fact]
    public void GenerateDiagram_WithOnlySeparators_UsesDefaultSteps()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("...;;;\n\r", "flow");

        Assert.Equal(Flow(DefaultSteps), result.Mermaid);
    }

    // UTC49: Steps that become empty after prefix strip -> Dropped; default steps used
    [Fact]
    public void GenerateDiagram_WithOnlyPrefixes_UsesDefaultSteps()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("- \n* \n+ \n1) ", "flow");

        Assert.Equal(Flow(DefaultSteps), result.Mermaid);
    }

    // UTC50: Default steps with sequence kind -> Sequence diagram with default labels
    [Fact]
    public void GenerateDiagram_DefaultStepsWithSequence_BuildsSequence()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("", "seq");

        Assert.Equal(Sequence(DefaultSteps), result.Mermaid);
    }

    // UTC51: Default steps with state kind -> State diagram with 3 default states
    [Fact]
    public void GenerateDiagram_DefaultStepsWithState_BuildsState()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("", "state");

        Assert.Equal(State(DefaultSteps), result.Mermaid);
        Assert.Equal(new[] { StateNote(3) }, result.Notes.ToArray());
    }

    // UTC52: Unknown preferred kind -> Falls back to text inference (state keyword)
    [Fact]
    public void GenerateDiagram_UnknownKind_FallsBackToTextInference()
    {
        Assert.Equal("state", DiagramGeneratorHelper.GenerateDiagram("check the status", "gantt").Kind);
        Assert.Equal("sequence", DiagramGeneratorHelper.GenerateDiagram("call the api", "gantt").Kind);
        Assert.Equal("flowchart", DiagramGeneratorHelper.GenerateDiagram("simple things", "gantt").Kind);
    }

    // UTC53: Blank preferred kind -> Falls back to text inference
    // UTC54: Whitespace preferred kind
    [Theory]
    [InlineData("")]     // UTC53
    [InlineData("   ")]  // UTC54
    public void GenerateDiagram_BlankKind_FallsBackToTextInference(string kind)
    {
        Assert.Equal("state", DiagramGeneratorHelper.GenerateDiagram("check the status", kind).Kind);
    }

    // UTC55: Text without any keyword and no kind -> Flowchart
    [Fact]
    public void GenerateDiagram_NoKeywords_DefaultsToFlowchart()
    {
        Assert.Equal("flowchart", DiagramGeneratorHelper.GenerateDiagram("buy milk then cook", null).Kind);
    }

    // UTC56: Keyword is only in a short, filtered-out step -> Kind still inferred from whole text
    [Fact]
    public void GenerateDiagram_KindInferredFromWholeTextEvenIfStepFiltered()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("api;x", null);

        Assert.Equal("sequence", result.Kind);
        Assert.Equal(Sequence("api"), result.Mermaid);
    }

    // UTC57: Text only partially matching a keyword ("stat") -> Not state
    [Fact]
    public void GenerateDiagram_PartialKeyword_NotMatched()
    {
        Assert.Equal("flowchart", DiagramGeneratorHelper.GenerateDiagram("check stat now", null).Kind);
    }

    // UTC58: Only the quote char in a step ("\"\"\"") -> Length 3, kept, label becomes '''
    [Fact]
    public void GenerateDiagram_QuoteOnlyStep_BecomesSingleQuotes()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("\"\"\"", "flow");

        Assert.Equal(Flow("'''"), result.Mermaid);
    }

    // =========================================================================
    // 3. BOUNDARY CASES (step length 2/3/4 | step count 6/7/8 | label length 59/60/61)
    // =========================================================================

    // UTC59: Step length 2 -> Dropped
    [Fact]
    public void GenerateDiagram_StepLength2_Dropped()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("ab;abcd", "flow");

        Assert.Equal(Flow("abcd"), result.Mermaid);
    }

    // UTC60: Step length 3 -> Kept
    [Fact]
    public void GenerateDiagram_StepLength3_Kept()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("abc;ab", "flow");

        Assert.Equal(Flow("abc"), result.Mermaid);
    }

    // UTC61: Step length 4 -> Kept
    [Fact]
    public void GenerateDiagram_StepLength4_Kept()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("abcd", "flow");

        Assert.Equal(Flow("abcd"), result.Mermaid);
    }

    // UTC62: Length measured after prefix strip ("- ab" -> "ab" dropped; "- abc" -> kept)
    [Fact]
    public void GenerateDiagram_LengthMeasuredAfterPrefixStrip()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram("- ab;- abc", "flow");

        Assert.Equal(Flow("abc"), result.Mermaid);
    }

    // UTC63: 6 steps -> All kept
    [Fact]
    public void GenerateDiagram_SixSteps_AllKept()
    {
        var labels = Enumerable.Range(1, 6).Select(i => "step" + i).ToArray();

        var result = DiagramGeneratorHelper.GenerateDiagram(string.Join(";", labels), "flow");

        Assert.Equal(Flow(labels), result.Mermaid);
        Assert.Equal(new[] { FlowNote(6) }, result.Notes.ToArray());
    }

    // UTC64: 7 steps -> All kept (cap boundary)
    [Fact]
    public void GenerateDiagram_SevenSteps_AllKept()
    {
        var labels = Enumerable.Range(1, 7).Select(i => "step" + i).ToArray();

        var result = DiagramGeneratorHelper.GenerateDiagram(string.Join(";", labels), "flow");

        Assert.Equal(Flow(labels), result.Mermaid);
        Assert.Equal(new[] { FlowNote(7) }, result.Notes.ToArray());
    }

    // UTC65: 8 steps -> Capped at 7 (first seven kept)
    [Fact]
    public void GenerateDiagram_EightSteps_CappedAtSeven()
    {
        var labels = Enumerable.Range(1, 8).Select(i => "step" + i).ToArray();

        var result = DiagramGeneratorHelper.GenerateDiagram(string.Join(";", labels), "flow");

        Assert.Equal(Flow(labels.Take(7).ToArray()), result.Mermaid);
        Assert.DoesNotContain("step8", result.Mermaid);
        Assert.Equal(new[] { FlowNote(7) }, result.Notes.ToArray());
    }

    // UTC66: 20 steps with state kind -> Capped at 7 states
    [Fact]
    public void GenerateDiagram_ManyStepsState_CappedAtSeven()
    {
        var labels = Enumerable.Range(1, 20).Select(i => "step" + i).ToArray();

        var result = DiagramGeneratorHelper.GenerateDiagram(string.Join(";", labels), "state");

        Assert.Equal(State(labels.Take(7).ToArray()), result.Mermaid);
        Assert.Equal(new[] { StateNote(7) }, result.Notes.ToArray());
    }

    // UTC67: 20 steps with sequence kind -> Capped at 7 messages
    [Fact]
    public void GenerateDiagram_ManyStepsSequence_CappedAtSeven()
    {
        var labels = Enumerable.Range(1, 20).Select(i => "step" + i).ToArray();

        var result = DiagramGeneratorHelper.GenerateDiagram(string.Join(";", labels), "seq");

        Assert.Equal(Sequence(labels.Take(7).ToArray()), result.Mermaid);
    }

    // UTC68: Short steps do not count toward the cap -> 7 valid steps among 10 pieces
    [Fact]
    public void GenerateDiagram_ShortStepsDoNotCountTowardCap()
    {
        var text = "ab;step1;ab;step2;step3;ab;step4;step5;step6;step7;step8";

        var result = DiagramGeneratorHelper.GenerateDiagram(text, "flow");

        Assert.Equal(Flow("step1", "step2", "step3", "step4", "step5", "step6", "step7"), result.Mermaid);
    }

    // UTC69: Label length 59 -> Not truncated
    [Fact]
    public void GenerateDiagram_LabelLength59_NotTruncated()
    {
        var label = new string('l', 59);

        Assert.Equal(Flow(label), DiagramGeneratorHelper.GenerateDiagram(label, "flow").Mermaid);
    }

    // UTC70: Label length 60 -> Not truncated
    [Fact]
    public void GenerateDiagram_LabelLength60_NotTruncated()
    {
        var label = new string('l', 60);

        Assert.Equal(Flow(label), DiagramGeneratorHelper.GenerateDiagram(label, "flow").Mermaid);
    }

    // UTC71: Label length 61 -> Truncated to 60 chars + "..."
    [Fact]
    public void GenerateDiagram_LabelLength61_TruncatedWithEllipsis()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram(new string('l', 61), "flow");

        Assert.Equal(Flow(new string('l', 60) + "..."), result.Mermaid);
    }

    // UTC72: Long label in state diagram -> Truncated the same way
    [Fact]
    public void GenerateDiagram_LongLabelInState_Truncated()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram(new string('s', 100), "state");

        Assert.Equal(State(new string('s', 60) + "..."), result.Mermaid);
    }

    // UTC73: Long label in sequence diagram -> Truncated the same way
    [Fact]
    public void GenerateDiagram_LongLabelInSequence_Truncated()
    {
        var result = DiagramGeneratorHelper.GenerateDiagram(new string('q', 100), "seq");

        Assert.Equal(Sequence(new string('q', 60) + "..."), result.Mermaid);
    }

    // UTC74: Quotes counted after replacement (still same length) -> 61 chars with quotes truncated, quotes become '
    [Fact]
    public void GenerateDiagram_QuotesAndLongLabel_ReplacedThenTruncated()
    {
        var text = new string('"', 61);

        var result = DiagramGeneratorHelper.GenerateDiagram(text, "flow");

        Assert.Equal(Flow(new string('\'', 60) + "..."), result.Mermaid);
    }

    // UTC75: Result does not end with trailing newline (TrimEnd applied)
    [Fact]
    public void GenerateDiagram_MermaidHasNoTrailingWhitespace()
    {
        foreach (var kind in new[] { "flow", "seq", "state" })
        {
            var mermaid = DiagramGeneratorHelper.GenerateDiagram("alpha;beta", kind).Mermaid;

            Assert.Equal(mermaid.TrimEnd(), mermaid);
        }
    }
}
