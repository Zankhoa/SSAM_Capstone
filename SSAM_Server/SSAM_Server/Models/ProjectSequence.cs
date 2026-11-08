namespace SSAM_Server.Models;

public static class SequenceScopes
{
    public const string Issue = "ISSUE";
    public const string TestCase = "TESTCASE";
    public const string Req = "REQ";
    public const string DocBrd = "DOC_BRD";
    public const string DocSrs = "DOC_SRS";
    public const string DocAdr = "DOC_ADR";
    public const string DocUiSpec = "DOC_UI_SPEC";
    public const string DocTestPlan = "DOC_TEST_PLAN";
    public const string DocOther = "DOC_OTHER";

    public static string ForDocType(string docType) => docType switch
    {
        "BRD" => DocBrd,
        "SRS" => DocSrs,
        "ADR" => DocAdr,
        "UI_SPEC" => DocUiSpec,
        "TEST_PLAN" => DocTestPlan,
        _ => DocOther
    };
}

public class ProjectSequence
{
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string Scope { get; set; } = string.Empty;
    public int LastValue { get; set; } = 0;
}
