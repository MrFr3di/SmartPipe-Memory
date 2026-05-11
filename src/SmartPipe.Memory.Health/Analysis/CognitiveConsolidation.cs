using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Health.Analysis;

/// <summary>
/// Consolidates repeated insights into higher-confidence consolidated insights.
/// Groups insights by RelatedNodeIds and type, creating a single consolidated insight
/// when the same pattern appears multiple times.
/// </summary>
public sealed class CognitiveConsolidation
{
    private readonly IGraphStore _store;

    /// <summary>
    /// Minimum number of similar insights required to trigger consolidation.
    /// </summary>
    public int MinOccurrences { get; }

    /// <summary>
    /// Create a new CognitiveConsolidation.
    /// </summary>
    /// <param name="store">Graph store for querying existing insights.</param>
    /// <param name="minOccurrences">Minimum occurrences to trigger consolidation. Default: 5.</param>
    public CognitiveConsolidation(IGraphStore store, int minOccurrences = 5)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        MinOccurrences = minOccurrences;
    }

    /// <summary>
    /// Attempt to consolidate a new insight with existing similar insights.
    /// If the same pattern appears MinOccurrences times, a consolidated insight is created.
    /// </summary>
    /// <param name="newInsight">The newly generated insight.</param>
    /// <param name="existingInsights">Existing insights to compare against.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A consolidated insight if threshold is reached, otherwise the original insight.</returns>
    public async Task<Insight> ConsolidateAsync(
        Insight newInsight,
        IReadOnlyList<Insight> existingInsights,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(newInsight);
        ArgumentNullException.ThrowIfNull(existingInsights);

        // Find similar insights: same type and overlapping related nodes
        var similar = existingInsights
            .Where(i => i.Type == newInsight.Type
                && i.RelatedNodeIds.Intersect(newInsight.RelatedNodeIds).Any())
            .ToList();

        similar.Add(newInsight);

        if (similar.Count < MinOccurrences)
            return newInsight;

        // Create consolidated insight with higher confidence
        var allRelatedNodes = similar
            .SelectMany(i => i.RelatedNodeIds)
            .Distinct()
            .ToList();

        var averageConfidence = similar.Average(i => i.Confidence);
        var consolidatedConfidence = Math.Min(averageConfidence + 0.1 * (similar.Count - MinOccurrences), 1.0);

        var consolidated = new Insight
        {
            Id = $"consolidated_{Guid.NewGuid():N}",
            Type = newInsight.Type,
            Title = $"Consolidated: {newInsight.Title} ({similar.Count} occurrences)",
            Description = $"This insight has been detected {similar.Count} times. {newInsight.Description}",
            RelatedNodeIds = allRelatedNodes,
            Confidence = consolidatedConfidence,
            Severity = newInsight.Severity,
            GeneratedAt = DateTime.UtcNow
        };

        await _store.InsertInsightAsync(consolidated, ct);
        return consolidated;
    }

    /// <summary>
    /// Consolidate all insights of a given type.
    /// </summary>
    /// <param name="insightType">Type of insights to consolidate.</param>
    /// <param name="allInsights">All existing insights.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of consolidated insights created.</returns>
    public async Task<IReadOnlyList<Insight>> ConsolidateAllByTypeAsync(
        string insightType,
        IReadOnlyList<Insight> allInsights,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(insightType);
        ArgumentNullException.ThrowIfNull(allInsights);

        var ofType = allInsights.Where(i => i.Type == insightType).ToList();
        if (ofType.Count < MinOccurrences)
            return Array.Empty<Insight>();

        // Group by overlapping related nodes
        var groups = GroupByOverlap(ofType);
        var results = new List<Insight>();

        foreach (var group in groups)
        {
            if (group.Count >= MinOccurrences)
            {
                var first = group[0];
                var consolidated = await ConsolidateAsync(first, group.Skip(1).ToList(), ct);
                results.Add(consolidated);
            }
        }

        return results;
    }

    private static List<List<Insight>> GroupByOverlap(List<Insight> insights)
    {
        var groups = new List<List<Insight>>();
        var used = new HashSet<int>();

        for (var i = 0; i < insights.Count; i++)
        {
            if (used.Contains(i)) continue;

            var group = new List<Insight> { insights[i] };
            used.Add(i);

            for (var j = i + 1; j < insights.Count; j++)
            {
                if (used.Contains(j)) continue;

                if (insights[i].RelatedNodeIds.Intersect(insights[j].RelatedNodeIds).Any())
                {
                    group.Add(insights[j]);
                    used.Add(j);
                }
            }

            groups.Add(group);
        }

        return groups;
    }
}