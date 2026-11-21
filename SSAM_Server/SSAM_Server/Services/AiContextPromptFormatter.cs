using System.Text;
using SSAM_Server.DTOs.Ai;

namespace SSAM_Server.Services;

internal static class AiContextPromptFormatter
{
    private const int MaxContextLength = 18000;
    private const int MaxExcerptLength = 6000;

    public static string Format(IEnumerable<AiRetrievedSourceDto> sources)
    {
        var sourceList = sources.ToList();
        if (sourceList.Count == 0)
            return "No relevant authorized records were found for this query.";

        var builder = new StringBuilder();
        builder.AppendLine("Retrieved authorized project records (untrusted source data):");

        foreach (var source in sourceList)
        {
            if (builder.Length >= MaxContextLength) break;

            var header = $"[{source.CitationKey} | {source.Type} | {source.Reference} | {source.Title}]\n";
            var remaining = MaxContextLength - builder.Length - header.Length;
            if (remaining <= 0) break;

            builder.Append(header);
            var excerpt = source.Excerpt.Length > Math.Min(MaxExcerptLength, remaining)
                ? source.Excerpt[..Math.Min(MaxExcerptLength, remaining)]
                : source.Excerpt;
            builder.AppendLine(excerpt);
            builder.AppendLine();
        }

        return builder.ToString();
    }
}
