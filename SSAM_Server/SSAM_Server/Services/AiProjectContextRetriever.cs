using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SSAM_Server.Interfaces.Repositories;
using SSAM_Server.DTOs.Ai;
using SSAM_Server.Interfaces.Services;
using SSAM_Server.Models;

namespace SSAM_Server.Services;

public class AiProjectContextRetriever : IAiProjectContextRetriever
{
    private const int MaxSearchTerms = 8;
    private const int MaxDocumentCandidates = 24;
    private const int MaxRequirementCandidates = 40;
    private const int MaxIssueCandidates = 30;
    private const int MaxTestCaseCandidates = 30;
    private const int MaxDocuments = 4;
    private const int MaxRequirements = 10;
    private const int MaxIssues = 6;
    private const int MaxTestCases = 8;
    private const int MaxTraceabilityLinks = 30;
    private const int MaxDocumentExcerptLength = 6000;
    private const int MaxArtifactExcerptLength = 1800;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "can", "do", "for", "from",
        "how", "in", "is", "it", "me", "of", "on", "or", "that", "the", "this", "to",
        "what", "when", "where", "which", "who", "why", "with",
        "các", "của", "đã", "đang", "được", "gì", "là", "nào", "những", "trong", "và", "về"
    };

    private readonly IAiRepository _db;

    public AiProjectContextRetriever(IAiRepository db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AiRetrievedSourceDto>> RetrieveAsync(
        long projectId,
        string query,
        bool canViewDocuments,
        bool canViewIssues,
        bool canViewTests,
        long? documentId = null,
        CancellationToken ct = default)
    {
        var terms = GetSearchTerms(query);
        var sources = new List<AiRetrievedSourceDto>();

        if (canViewDocuments)
        {
            sources.AddRange(await RetrieveDocumentsAsync(projectId, terms, documentId, ct));
            sources.AddRange(await RetrieveRequirementsAsync(projectId, terms, documentId, ct));
        }

        if (canViewIssues)
            sources.AddRange(await RetrieveIssuesAsync(projectId, terms, ct));

        if (canViewTests)
            sources.AddRange(await RetrieveTestCasesAsync(projectId, terms, ct));

        if (canViewDocuments && (canViewIssues || canViewTests))
            sources.AddRange(await RetrieveTraceabilityAsync(projectId, sources, canViewIssues, canViewTests, ct));

        return sources
            .DistinctBy(s => (s.Type, s.Reference, s.DocumentId, s.VersionId, s.IssueId, s.TestCaseId))
            .Select(s => s with
            {
                CitationKey = AiCitationMapper.CreateCitationKey(s),
                Excerpt = s.Excerpt.Length > MaxArtifactExcerptLength && s.Type != "document"
                    ? s.Excerpt[..MaxArtifactExcerptLength]
                    : s.Excerpt
            })
            .ToList();
    }

    private async Task<List<AiRetrievedSourceDto>> RetrieveDocumentsAsync(
        long projectId,
        IReadOnlyList<string> terms,
        long? documentId,
        CancellationToken ct)
    {
        var query = _db.DocumentVersions
            .AsNoTracking()
            .Where(v =>
                v.Document.ProjectId == projectId &&
                !v.Document.IsDeleted &&
                v.Document.Kind == DocumentNodeKinds.Page &&
                v.Id == (v.Document.BaselineVersionId ?? v.Document.CurrentVersionId));

        if (documentId.HasValue)
            query = query.Where(v => v.DocumentId == documentId.Value);
        else if (terms.Count > 0)
            query = query.Where(ContainsAny(terms,
                (Expression<Func<DocumentVersion, string?>>)(v => v.Document.Code),
                v => v.Document.Title,
                v => v.ContentText));
        else
            return new List<AiRetrievedSourceDto>();

        var candidates = await query
            .OrderByDescending(v => v.Document.UpdatedAt)
            .ThenBy(v => v.Document.Id)
            .Take(documentId.HasValue ? 1 : MaxDocumentCandidates)
            .Select(v => new AiRetrievedSourceDto
            {
                Type = "document",
                Reference = v.Document.Code ?? $"DOC-{v.DocumentId}",
                Title = v.Document.Title,
                Excerpt = v.ContentText.Length > MaxDocumentExcerptLength
                    ? v.ContentText.Substring(0, MaxDocumentExcerptLength)
                    : v.ContentText,
                DocumentId = v.DocumentId,
                VersionId = v.Id
            })
            .ToListAsync(ct);

        return documentId.HasValue
            ? candidates
            : candidates
                .OrderByDescending(s => Score(s, terms))
                .Take(MaxDocuments)
                .ToList();
    }

    private async Task<List<AiRetrievedSourceDto>> RetrieveRequirementsAsync(
        long projectId,
        IReadOnlyList<string> terms,
        long? documentId,
        CancellationToken ct)
    {
        var query = _db.RequirementItems
            .AsNoTracking()
            .Where(r =>
                r.Document.ProjectId == projectId &&
                !r.Document.IsDeleted &&
                r.Document.Kind == DocumentNodeKinds.Page &&
                r.VersionId == (r.Document.BaselineVersionId ?? r.Document.CurrentVersionId));

        if (documentId.HasValue)
            query = query.Where(r => r.DocumentId == documentId.Value);

        if (terms.Count > 0)
        {
            query = query.Where(ContainsAny(terms,
                (Expression<Func<RequirementItem, string?>>)(r => r.Document.Code),
                r => r.ReqUid,
                r => r.Title,
                r => r.BodyPreview,
                r => r.AcceptanceCriteria));
        }
        else if (!documentId.HasValue)
        {
            return new List<AiRetrievedSourceDto>();
        }

        var candidates = await query
            .OrderBy(r => r.DocumentId)
            .ThenBy(r => r.SortOrder)
            .Take(MaxRequirementCandidates)
            .Select(r => new AiRetrievedSourceDto
            {
                Type = "requirement",
                Reference = (r.Document.Code ?? $"DOC-{r.DocumentId}") + ":" + r.ReqUid,
                Title = r.Title,
                Excerpt = $"Requirement: {r.BodyPreview}\nAcceptance criteria: {r.AcceptanceCriteria}",
                DocumentId = r.DocumentId,
                VersionId = r.VersionId,
                RequirementId = r.Id,
                ReqUid = r.ReqUid,
                AnchorId = r.AnchorId
            })
            .ToListAsync(ct);

        if (documentId.HasValue && candidates.Count == 0)
        {
            candidates = await _db.RequirementItems
                .AsNoTracking()
                .Where(r =>
                    r.DocumentId == documentId.Value &&
                    r.Document.ProjectId == projectId &&
                    !r.Document.IsDeleted &&
                    r.VersionId == (r.Document.BaselineVersionId ?? r.Document.CurrentVersionId))
                .OrderBy(r => r.SortOrder)
                .Take(MaxRequirements)
                .Select(r => new AiRetrievedSourceDto
                {
                    Type = "requirement",
                    Reference = (r.Document.Code ?? $"DOC-{r.DocumentId}") + ":" + r.ReqUid,
                    Title = r.Title,
                    Excerpt = $"Requirement: {r.BodyPreview}\nAcceptance criteria: {r.AcceptanceCriteria}",
                    DocumentId = r.DocumentId,
                    VersionId = r.VersionId,
                    RequirementId = r.Id,
                    ReqUid = r.ReqUid,
                    AnchorId = r.AnchorId
                })
                .ToListAsync(ct);
        }

        return candidates
            .OrderByDescending(s => Score(s, terms))
            .Take(MaxRequirements)
            .ToList();
    }

    private async Task<List<AiRetrievedSourceDto>> RetrieveIssuesAsync(
        long projectId,
        IReadOnlyList<string> terms,
        CancellationToken ct)
    {
        if (terms.Count == 0) return new List<AiRetrievedSourceDto>();

        var candidates = await _db.Issues
            .AsNoTracking()
            .Where(i => i.ProjectId == projectId && !i.IsDeleted)
            .Where(ContainsAny(terms,
                (Expression<Func<Issue, string?>>)(i => i.Key),
                i => i.Title,
                i => i.Description))
            .OrderByDescending(i => i.UpdatedAt)
            .Take(MaxIssueCandidates)
            .Select(i => new AiRetrievedSourceDto
            {
                Type = "issue",
                Reference = i.Key,
                TargetReference = i.Key,
                Title = i.Title,
                Excerpt = $"Type: {i.Type}\nStatus: {i.Status.Name}\nPriority: {i.Priority}\nDescription: {i.Description}",
                IssueId = i.Id
            })
            .ToListAsync(ct);

        return candidates
            .OrderByDescending(s => Score(s, terms))
            .Take(MaxIssues)
            .ToList();
    }

    private async Task<List<AiRetrievedSourceDto>> RetrieveTestCasesAsync(
        long projectId,
        IReadOnlyList<string> terms,
        CancellationToken ct)
    {
        if (terms.Count == 0) return new List<AiRetrievedSourceDto>();

        var candidates = await _db.TestCases
            .AsNoTracking()
            .Where(tc => tc.ProjectId == projectId && tc.Status == TestCaseStatuses.Active)
            .Where(ContainsAny(terms,
                (Expression<Func<TestCase, string?>>)(tc => tc.Code),
                tc => tc.Title,
                tc => tc.Preconditions,
                tc => tc.Steps))
            .OrderByDescending(tc => tc.UpdatedAt)
            .Take(MaxTestCaseCandidates)
            .Select(tc => new AiRetrievedSourceDto
            {
                Type = "testCase",
                Reference = tc.Code,
                TargetReference = tc.Code,
                Title = tc.Title,
                Excerpt = $"Preconditions: {tc.Preconditions}\nSteps: {tc.Steps}",
                TestCaseId = tc.Id,
                PlanId = tc.PlanId
            })
            .ToListAsync(ct);

        var matches = candidates
            .OrderByDescending(s => Score(s, terms))
            .Take(MaxTestCases)
            .ToList();
        if (matches.Count == 0) return matches;

        var testCaseIds = matches.Select(s => s.TestCaseId!.Value).ToList();
        var latestResults = await _db.TestResults
            .AsNoTracking()
            .Where(r =>
                testCaseIds.Contains(r.TestCaseId) &&
                r.TestCase.ProjectId == projectId &&
                !r.Environment.IsDeleted &&
                !_db.TestResults.Any(other =>
                    other.TestCaseId == r.TestCaseId &&
                    other.EnvId == r.EnvId &&
                    (other.At > r.At || (other.At == r.At && other.Id > r.Id))))
            .Select(r => new
            {
                r.TestCaseId,
                Environment = r.Environment.Name,
                r.Build,
                r.Result
            })
            .ToListAsync(ct);

        foreach (var group in latestResults.GroupBy(r => r.TestCaseId))
        {
            var source = matches.First(s => s.TestCaseId == group.Key);
            source.Excerpt += "\nLatest results: " + string.Join(
                "; ",
                group.Select(r => $"{r.Environment}: {r.Result} (build {r.Build})"));
        }

        return matches;
    }

    private async Task<List<AiRetrievedSourceDto>> RetrieveTraceabilityAsync(
        long projectId,
        IReadOnlyList<AiRetrievedSourceDto> relevantSources,
        bool canViewIssues,
        bool canViewTests,
        CancellationToken ct)
    {
        var requirements = relevantSources
            .Where(s => s.Type == "requirement" && s.DocumentId.HasValue && !string.IsNullOrEmpty(s.ReqUid))
            .ToList();
        var documentIds = requirements.Select(r => r.DocumentId!.Value).Distinct().ToList();
        var requirementUids = requirements
            .Select(r => r.ReqUid!.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var testCaseIds = relevantSources
            .Where(s => s.Type == "testCase" && s.TestCaseId.HasValue)
            .Select(s => s.TestCaseId!.Value)
            .Distinct()
            .ToList();
        var issueIds = relevantSources
            .Where(s => s.Type == "issue" && s.IssueId.HasValue)
            .Select(s => s.IssueId!.Value)
            .Distinct()
            .ToList();

        if (documentIds.Count == 0 && testCaseIds.Count == 0 && issueIds.Count == 0)
            return new List<AiRetrievedSourceDto>();

        var sources = new List<AiRetrievedSourceDto>();

        if (canViewTests)
        {
            var testLinkQuery = _db.TestCaseRequirementLinks
                .AsNoTracking()
                .Where(l =>
                    l.TestCase.ProjectId == projectId &&
                    l.TestCase.Status == TestCaseStatuses.Active &&
                    l.Document.ProjectId == projectId &&
                    !l.Document.IsDeleted);
            if (documentIds.Count > 0 && testCaseIds.Count > 0)
                testLinkQuery = testLinkQuery.Where(l =>
                    testCaseIds.Contains(l.TestCaseId) ||
                    (documentIds.Contains(l.DocumentId) && requirementUids.Contains(l.ReqUid.ToUpper())));
            else if (testCaseIds.Count > 0)
                testLinkQuery = testLinkQuery.Where(l => testCaseIds.Contains(l.TestCaseId));
            else if (documentIds.Count > 0)
                testLinkQuery = testLinkQuery.Where(l =>
                    documentIds.Contains(l.DocumentId) && requirementUids.Contains(l.ReqUid.ToUpper()));
            else
                testLinkQuery = testLinkQuery.Where(_ => false);

            var testLinks = await testLinkQuery
                .OrderBy(l => l.DocumentId)
                .ThenBy(l => l.ReqUid)
                .Take(MaxTraceabilityLinks)
                .Select(l => new
                {
                    l.DocumentId,
                    l.ReqUid,
                    DocumentCode = l.Document.Code,
                    TestCaseId = l.TestCaseId,
                    PlanId = l.TestCase.PlanId,
                    TestCaseCode = l.TestCase.Code,
                    TestCaseTitle = l.TestCase.Title
                })
                .ToListAsync(ct);

            sources.AddRange(testLinks.Select(l => new AiRetrievedSourceDto
            {
                Type = "traceability",
                Reference = $"{l.DocumentCode ?? $"DOC-{l.DocumentId}"}:{l.ReqUid}",
                Title = $"Requirement to test case: {l.TestCaseCode}",
                Excerpt = $"{l.DocumentCode ?? $"DOC-{l.DocumentId}"}:{l.ReqUid} is covered by test case {l.TestCaseCode}: {l.TestCaseTitle}",
                DocumentId = l.DocumentId,
                ReqUid = l.ReqUid,
                TestCaseId = l.TestCaseId,
                PlanId = l.PlanId,
                TargetReference = l.TestCaseCode
            }));
        }

        if (canViewIssues)
        {
            var issueLinkQuery = _db.IssueRequirementLinks
                .AsNoTracking()
                .Where(l =>
                    l.Issue.ProjectId == projectId &&
                    !l.Issue.IsDeleted &&
                    l.Document.ProjectId == projectId &&
                    !l.Document.IsDeleted);
            if (documentIds.Count > 0 && issueIds.Count > 0)
                issueLinkQuery = issueLinkQuery.Where(l =>
                    issueIds.Contains(l.IssueId) ||
                    (documentIds.Contains(l.DocumentId) && requirementUids.Contains(l.ReqUid.ToUpper())));
            else if (issueIds.Count > 0)
                issueLinkQuery = issueLinkQuery.Where(l => issueIds.Contains(l.IssueId));
            else if (documentIds.Count > 0)
                issueLinkQuery = issueLinkQuery.Where(l =>
                    documentIds.Contains(l.DocumentId) && requirementUids.Contains(l.ReqUid.ToUpper()));
            else
                issueLinkQuery = issueLinkQuery.Where(_ => false);

            var issueLinks = await issueLinkQuery
                .OrderBy(l => l.DocumentId)
                .ThenBy(l => l.ReqUid)
                .Take(MaxTraceabilityLinks)
                .Select(l => new
                {
                    l.DocumentId,
                    l.ReqUid,
                    DocumentCode = l.Document.Code,
                    IssueId = l.IssueId,
                    IssueKey = l.Issue.Key,
                    IssueTitle = l.Issue.Title
                })
                .ToListAsync(ct);

            sources.AddRange(issueLinks.Select(l => new AiRetrievedSourceDto
            {
                Type = "traceability",
                Reference = $"{l.DocumentCode ?? $"DOC-{l.DocumentId}"}:{l.ReqUid}",
                Title = $"Requirement to issue: {l.IssueKey}",
                Excerpt = $"{l.DocumentCode ?? $"DOC-{l.DocumentId}"}:{l.ReqUid} is linked to issue {l.IssueKey}: {l.IssueTitle}",
                DocumentId = l.DocumentId,
                ReqUid = l.ReqUid,
                IssueId = l.IssueId,
                TargetReference = l.IssueKey
            }));
        }

        return sources;
    }

    private static IReadOnlyList<string> GetSearchTerms(string query) =>
        Regex.Matches(query, @"[\p{L}\p{N}][\p{L}\p{N}_-]{1,}")
            .Select(m => m.Value)
            .Where(term => !StopWords.Contains(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxSearchTerms)
            .ToList();

    private static Expression<Func<T, bool>> ContainsAny<T>(
        IReadOnlyList<string> terms,
        params Expression<Func<T, string?>>[] fields)
    {
        var parameter = Expression.Parameter(typeof(T), "item");
        Expression body = Expression.Constant(false);
        var contains = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;

        foreach (var term in terms)
        {
            foreach (var field in fields)
            {
                var member = new ParameterReplaceVisitor(field.Parameters[0], parameter).Visit(field.Body)!;
                var hasValue = Expression.NotEqual(member, Expression.Constant(null, typeof(string)));
                var matches = Expression.Call(member, contains, Expression.Constant(term));
                body = Expression.OrElse(body, Expression.AndAlso(hasValue, matches));
            }
        }

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    private static int Score(AiRetrievedSourceDto source, IReadOnlyList<string> terms)
    {
        var text = $"{source.Reference}\n{source.Title}\n{source.Excerpt}";
        return terms.Count(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class ParameterReplaceVisitor(ParameterExpression source, ParameterExpression target)
        : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == source ? target : base.VisitParameter(node);
    }
}
