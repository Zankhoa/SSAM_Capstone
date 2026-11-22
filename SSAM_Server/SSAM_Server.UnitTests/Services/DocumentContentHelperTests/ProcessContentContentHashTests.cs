using System.Security.Cryptography;
using System.Text;

namespace SSAM_Server.UnitTests.Services.DocumentContentHelperTests;

/// <summary>ProcessContent: ContentHash computation of extracted requirements.</summary>
public class ProcessContentContentHashTests : DocumentContentHelperTestBase
{
    private static string Sha(string payload)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

    private static string HashOf(string heading, params string[] section)
    {
        var blocks = new List<string> { heading };
        blocks.AddRange(section);
        return Assert.Single(Extract(blocks.ToArray())).ContentHash;
    }

    private static string Hash(params string[] section) => HashOf(H(2, "REQ-01 — Title"), section);

    private static string Mermaid(string code) => "{\"type\":\"mermaidBlock\",\"attrs\":{\"code\":" + Quote(code) + "}}";

    private static string Figma(string url) => "{\"type\":\"figma\",\"attrs\":{\"url\":" + Quote(url) + "}}";

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Hash is 64 lowercase hex chars (SHA-256)
    [Fact]
    public void ContentHash_IsLowercaseSha256Hex()
    {
        var hash = Hash(P("body"));

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    // UTC02: Minimal requirement -> Equals SHA-256 of canonical payload "title\n" + 7 empty parts
    [Fact]
    public void ContentHash_MinimalRequirement_MatchesCanonicalPayload()
    {
        var hash = Hash();

        Assert.Equal(Sha("title\n\n\n\n\n\n\n"), hash);
    }

    // UTC03: Full requirement -> Matches the canonical payload including every component
    [Fact]
    public void ContentHash_FullRequirement_MatchesCanonicalPayload()
    {
        var heading = H(2, "REQ-01 — Title  X", "\"actor\":\"Admin\",\"preconditions\":\"Logged In\"");

        var hash = HashOf(heading, P("Body  Text"), TaskList("AC One", "AC  Two"), Mermaid("graph  TD"), Figma("HTTPS://F"));

        var expected = "title x\nadmin\nlogged in\nbody text\nac one\nac two\n\ngraph TD\n{\"url\":\"https://f\"}";
        Assert.Equal(Sha(expected), hash);
    }

    // UTC04: Same document processed twice -> Identical hash (deterministic)
    [Fact]
    public void ContentHash_SameInputTwice_IsDeterministic()
    {
        Assert.Equal(Hash(P("body"), TaskList("a")), Hash(P("body"), TaskList("a")));
    }

    // UTC05: Title differing only by case -> Same hash
    [Fact]
    public void ContentHash_TitleCaseInsensitive()
    {
        Assert.Equal(HashOf(H(2, "REQ-01 — LOGIN")), HashOf(H(2, "REQ-01 — login")));
    }

    // UTC06: Title with different internal whitespace -> Same hash
    [Fact]
    public void ContentHash_TitleWhitespaceInsensitive()
    {
        Assert.Equal(HashOf(H(2, "REQ-01 — Log   in")), HashOf(H(2, "REQ-01 — Log in")));
    }

    // UTC07: Different title -> Different hash
    [Fact]
    public void ContentHash_TitleChange_ChangesHash()
    {
        Assert.NotEqual(HashOf(H(2, "REQ-01 — A")), HashOf(H(2, "REQ-01 — B")));
    }

    // UTC08: Actor change -> Different hash; actor case -> same hash
    [Fact]
    public void ContentHash_ActorChange_ChangesHashButCaseDoesNot()
    {
        var a = HashOf(H(2, "REQ-01 — T", "\"actor\":\"Admin\""));
        var b = HashOf(H(2, "REQ-01 — T", "\"actor\":\"User\""));
        var c = HashOf(H(2, "REQ-01 — T", "\"actor\":\"ADMIN\""));

        Assert.NotEqual(a, b);
        Assert.Equal(a, c);
    }

    // UTC09: Preconditions change -> Different hash; case -> same hash
    [Fact]
    public void ContentHash_PreconditionsChange_ChangesHashButCaseDoesNot()
    {
        var a = HashOf(H(2, "REQ-01 — T", "\"preconditions\":\"Ready\""));
        var b = HashOf(H(2, "REQ-01 — T", "\"preconditions\":\"Done\""));
        var c = HashOf(H(2, "REQ-01 — T", "\"preconditions\":\"READY\""));

        Assert.NotEqual(a, b);
        Assert.Equal(a, c);
    }

    // UTC10: Actor vs preconditions with same text -> Different hash (positional components)
    [Fact]
    public void ContentHash_ActorAndPreconditionsArePositional()
    {
        var a = HashOf(H(2, "REQ-01 — T", "\"actor\":\"X\""));
        var b = HashOf(H(2, "REQ-01 — T", "\"preconditions\":\"X\""));

        Assert.NotEqual(a, b);
    }

    // UTC11: Body change -> Different hash
    [Fact]
    public void ContentHash_BodyChange_ChangesHash()
    {
        Assert.NotEqual(Hash(P("alpha")), Hash(P("beta")));
    }

    // UTC12: Body case and whitespace -> Same hash
    [Fact]
    public void ContentHash_BodyCaseAndWhitespaceInsensitive()
    {
        Assert.Equal(Hash(P("Hello   World")), Hash(P("hello world")));
    }

    // UTC13: Acceptance criteria change -> Different hash
    [Fact]
    public void ContentHash_AcceptanceCriteriaChange_ChangesHash()
    {
        Assert.NotEqual(Hash(TaskList("a")), Hash(TaskList("b")));
    }

    // UTC14: Acceptance criteria added -> Different hash
    [Fact]
    public void ContentHash_AcceptanceCriteriaAdded_ChangesHash()
    {
        Assert.NotEqual(Hash(), Hash(TaskList("a")));
    }

    // UTC15: Acceptance criteria case/whitespace -> Same hash
    [Fact]
    public void ContentHash_AcceptanceCriteriaCaseAndWhitespaceInsensitive()
    {
        Assert.Equal(Hash(TaskList("Do  It")), Hash(TaskList("do it")));
    }

    // UTC16: Acceptance criteria order -> Different hash
    [Fact]
    public void ContentHash_AcceptanceCriteriaOrder_ChangesHash()
    {
        Assert.NotEqual(Hash(TaskList("a", "b")), Hash(TaskList("b", "a")));
    }

    // UTC17: Table text change -> Different hash
    [Fact]
    public void ContentHash_TableChange_ChangesHash()
    {
        string Table(string t) => "{\"type\":\"table\",\"content\":[{\"type\":\"tableRow\",\"content\":[{\"type\":\"tableCell\",\"content\":[" + P(t) + "]}]}]}";

        Assert.NotEqual(Hash(Table("a")), Hash(Table("b")));
        Assert.Equal(Hash(Table("A")), Hash(Table("a")));
    }

    // UTC18: Constraint row change -> Different hash
    [Fact]
    public void ContentHash_ConstraintChange_ChangesHash()
    {
        Assert.NotEqual(Hash(Constraints("[{\"field\":\"A\"}]")), Hash(Constraints("[{\"field\":\"B\"}]")));
    }

    // UTC19: Diagram code change -> Different hash
    [Fact]
    public void ContentHash_DiagramChange_ChangesHash()
    {
        Assert.NotEqual(Hash(Mermaid("graph A")), Hash(Mermaid("graph B")));
    }

    // UTC20: Diagram code whitespace -> Same hash (collapsed) but case-sensitive (not lowered)
    [Fact]
    public void ContentHash_DiagramWhitespaceInsensitiveButCaseSensitive()
    {
        Assert.Equal(Hash(Mermaid("graph   TD")), Hash(Mermaid("graph TD")));
        Assert.NotEqual(Hash(Mermaid("graph TD")), Hash(Mermaid("GRAPH td")));
    }

    // UTC21: Figma attrs change -> Different hash; attrs case -> same hash (lower-cased)
    [Fact]
    public void ContentHash_FigmaChange_ChangesHashButCaseDoesNot()
    {
        Assert.NotEqual(Hash(Figma("https://f/1")), Hash(Figma("https://f/2")));
        Assert.Equal(Hash(Figma("https://F/1")), Hash(Figma("https://f/1")));
    }

    // UTC22: Figma block added -> Different hash
    [Fact]
    public void ContentHash_FigmaAdded_ChangesHash()
    {
        Assert.NotEqual(Hash(), Hash(Figma("u")));
    }

    // UTC23: Figma block without attrs -> Same hash as no figma block (hasFigma only)
    [Fact]
    public void ContentHash_FigmaWithoutAttrs_DoesNotChangeHash()
    {
        Assert.Equal(Hash(), Hash("{\"type\":\"figma\"}"));
    }

    // UTC24: Diagram vs equal text in body -> Different hash (positional)
    [Fact]
    public void ContentHash_DiagramAndBodyTextAreDifferentComponents()
    {
        Assert.NotEqual(Hash(Mermaid("graph")), Hash(P("graph")));
    }

    // UTC25: Requirement uid change only -> Same hash (uid not part of hash)
    [Fact]
    public void ContentHash_ReqUidChange_DoesNotChangeHash()
    {
        Assert.Equal(HashOf(H(2, "REQ-01 — T")), HashOf(H(2, "REQ-99 — T")));
    }

    // UTC26: Requirement priority attr change -> Same hash
    [Fact]
    public void ContentHash_PriorityChange_DoesNotChangeHash()
    {
        Assert.Equal(
            HashOf(H(2, "REQ-01 — T", "\"priority\":\"HIGH\"")),
            HashOf(H(2, "REQ-01 — T", "\"priority\":\"LOW\"")));
    }

    // UTC27: Different heading level -> Same hash (level not part of hash)
    [Fact]
    public void ContentHash_HeadingLevelChange_DoesNotChangeHash()
    {
        Assert.Equal(HashOf(H(2, "REQ-01 — T")), HashOf(H(3, "REQ-01 — T")));
    }

    // UTC28: Two requirements with identical content -> Equal hashes, different uids
    [Fact]
    public void ContentHash_TwoIdenticalRequirements_HaveEqualHashes()
    {
        var list = Extract(H(2, "REQ-01 — Same"), P("x"), H(2, "REQ-02 — Same"), P("x"));

        Assert.Equal(list[0].ContentHash, list[1].ContentHash);
        Assert.NotEqual(list[0].ReqUid, list[1].ReqUid);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC29: Blank diagram code -> Hash same as no diagram (blank not added)
    [Fact]
    public void ContentHash_BlankDiagramCode_SameAsNoDiagram()
    {
        Assert.Equal(Hash(), Hash(Mermaid("   ")));
    }

    // UTC30: Empty taskList item -> Same hash as none
    [Fact]
    public void ContentHash_EmptyTaskItems_SameAsNoCriteria()
    {
        Assert.Equal(Hash(), Hash(TaskList("   ")));
    }

    // UTC31: constraints block with only blank-field rows -> Same hash as no constraints
    [Fact]
    public void ContentHash_ConstraintsWithoutValidRows_SameAsNoConstraints()
    {
        Assert.Equal(Hash(), Hash(Constraints("[{\"field\":\"\"}]")));
    }

    // UTC32: Content after the section end (next same-level heading) -> Does not affect hash
    [Fact]
    public void ContentHash_ContentOutsideSection_DoesNotAffectHash()
    {
        var a = Extract(H(2, "REQ-01 — A"), P("in"), H(2, "REQ-02 — B"), P("x"))[0].ContentHash;
        var b = Extract(H(2, "REQ-01 — A"), P("in"), H(2, "REQ-02 — B"), P("y"))[0].ContentHash;

        Assert.Equal(a, b);
    }

    // UTC33: Missing actor vs whitespace actor -> Same hash (both null)
    [Fact]
    public void ContentHash_WhitespaceActor_SameAsNoActor()
    {
        Assert.Equal(HashOf(H(2, "REQ-01 — T")), HashOf(H(2, "REQ-01 — T", "\"actor\":\"   \"")));
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC34: Title truncated at 200 -> Titles differing only after char 200 yield same hash
    [Fact]
    public void ContentHash_TitlesDifferingAfter200Chars_SameHash()
    {
        var a = HashOf(H(2, "REQ-01 — " + new string('t', 200) + "A"));
        var b = HashOf(H(2, "REQ-01 — " + new string('t', 200) + "B"));

        Assert.Equal(a, b);
    }

    // UTC35: Titles differing at char 200 -> Different hash
    [Fact]
    public void ContentHash_TitlesDifferingAtChar200_DifferentHash()
    {
        var a = HashOf(H(2, "REQ-01 — " + new string('t', 199) + "A"));
        var b = HashOf(H(2, "REQ-01 — " + new string('t', 199) + "B"));

        Assert.NotEqual(a, b);
    }

    // UTC36: Body beyond preview (281 chars) -> Full body still part of hash
    [Fact]
    public void ContentHash_BodyBeyondPreview_StillAffectsHash()
    {
        var a = Hash(P(new string('b', 300) + "x"));
        var b = Hash(P(new string('b', 300) + "y"));

        Assert.NotEqual(a, b);
    }

    // UTC37: Empty-string payload vs single space in actor -> equal (null) - covered; here: empty body equals no section
    [Fact]
    public void ContentHash_EmptyParagraph_SameAsNoSection()
    {
        Assert.Equal(Hash(), Hash("{\"type\":\"paragraph\"}"));
    }
}
