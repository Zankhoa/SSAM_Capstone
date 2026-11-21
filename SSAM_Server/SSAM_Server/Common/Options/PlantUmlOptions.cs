namespace SSAM_Server.Common.Options;

public class PlantUmlOptions
{
    public const string SectionName = "PlantUml";

    public string? ServerUrl { get; set; }
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxCodeLength { get; set; } = 20_000;
    public int MaxSvgBytes { get; set; } = 2_000_000;
}
