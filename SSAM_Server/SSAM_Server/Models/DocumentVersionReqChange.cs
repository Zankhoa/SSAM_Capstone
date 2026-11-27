namespace SSAM_Server.Models;

public class DocumentVersionReqChange
{
    public long VersionId { get; set; }
    public DocumentVersion Version { get; set; } = null!;

    public string ReqUid { get; set; } = string.Empty;
    public string Level { get; set; } = "MAJOR";
}
