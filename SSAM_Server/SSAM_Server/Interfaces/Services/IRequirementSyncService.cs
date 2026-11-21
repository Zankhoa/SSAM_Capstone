using SSAM_Server.Models;

namespace SSAM_Server.Interfaces.Services;

/// <summary>
/// Coordinates cross-module requirement change propagation and freshness synchronization
/// between Documents, Kanban issues, and Testing test cases.
/// </summary>
public interface IRequirementSyncService
{
    /// <summary>
    /// Marks stale issue and test-case requirement links (and raises retest flags/activities)
    /// when a document version is approved as baseline. Does not save changes itself.
    /// </summary>
    Task PropagateBaselineChangesAsync(
        DocumentNode node,
        DocumentVersion approvedVersion,
        IReadOnlyList<RequirementItem> newReqs,
        long actorUserId,
        DateTime now,
        CancellationToken ct = default);

    /// <summary>
    /// Updates issue and test-case requirement links to the latest content hash and clears
    /// stale flags/retest markers for the specified requirement. Does not save changes itself.
    /// </summary>
    Task ConfirmRequirementFreshAsync(
        long documentId,
        string normalizedReqUid,
        string latestContentHash,
        DateTime now,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the set of requirement UIDs for a document that currently have stale links
    /// in Kanban issues or Testing test cases.
    /// </summary>
    Task<HashSet<string>> GetStaleRequirementUidsAsync(long documentId, CancellationToken ct = default);
}
