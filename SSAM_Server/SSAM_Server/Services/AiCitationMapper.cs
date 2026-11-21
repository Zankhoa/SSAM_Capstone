using SSAM_Server.DTOs.Ai;

namespace SSAM_Server.Services;

internal static class AiCitationMapper
{
    private const int MaxCitations = 20;

    public static List<AiCitationDto> Resolve(
        IEnumerable<string> requestedIds,
        IEnumerable<AiRetrievedSourceDto> sources)
    {
        var sourceById = sources
            .Where(source => !string.IsNullOrWhiteSpace(source.CitationKey))
            .GroupBy(source => source.CitationKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return requestedIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxCitations)
            .Where(sourceById.ContainsKey)
            .Select(id => sourceById[id])
            .Select(source => new AiCitationDto
            {
                Id = source.CitationKey,
                Type = source.Type,
                Reference = source.Reference,
                Title = source.Title,
                DocumentId = source.DocumentId,
                VersionId = source.VersionId,
                RequirementId = source.RequirementId,
                ReqUid = source.ReqUid,
                AnchorId = source.AnchorId,
                IssueId = source.IssueId,
                TestCaseId = source.TestCaseId,
                PlanId = source.PlanId,
                TargetReference = source.TargetReference
            })
            .ToList();
    }

    public static string CreateCitationKey(AiRetrievedSourceDto source) =>
        source.Type switch
        {
            "document" => $"doc:{source.DocumentId}:v:{source.VersionId}",
            "requirement" => $"req:{source.DocumentId}:{Uri.EscapeDataString(source.ReqUid ?? string.Empty)}",
            "issue" => $"issue:{source.IssueId}",
            "testCase" => $"test:{source.TestCaseId}",
            "traceability" => source.IssueId.HasValue
                ? $"trace:{source.DocumentId}:{Uri.EscapeDataString(source.ReqUid ?? string.Empty)}:issue:{source.IssueId}"
                : $"trace:{source.DocumentId}:{Uri.EscapeDataString(source.ReqUid ?? string.Empty)}:test:{source.TestCaseId}",
            _ => string.Empty
        };
}
