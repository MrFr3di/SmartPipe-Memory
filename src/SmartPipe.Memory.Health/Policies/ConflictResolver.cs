using SmartPipe.Core;
using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Health.Policies;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Health.Policies;

/// <summary>
/// Resolves conflicts when new facts contradict existing edges.
/// Weakens old edges instead of deleting them.
/// </summary>
public sealed class ConflictResolver
{
    private readonly MemoryDecayPolicy _decayPolicy;
    private readonly IClock _clock;

    /// <summary>
    /// Create a new ConflictResolver.
    /// </summary>
    /// <param name="decayPolicy">Decay policy for weakening old edges.</param>
    /// <param name="clock">Clock for time calculations.</param>
    public ConflictResolver(MemoryDecayPolicy decayPolicy, IClock? clock = null)
    {
        _decayPolicy = decayPolicy ?? throw new ArgumentNullException(nameof(decayPolicy));
        _clock = clock ?? new TimeProviderClock();
    }

    /// <summary>
    /// Resolve a conflict when adding a new edge between two nodes
    /// that already have an existing edge of the same type.
    /// The existing edge is weakened (weight reduced) instead of deleted.
    /// </summary>
    /// <param name="existingEdge">The existing edge to weaken.</param>
    /// <param name="store">Graph store to update the edge.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task ResolveAsync(Edge existingEdge, IGraphStore store, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(existingEdge);
        ArgumentNullException.ThrowIfNull(store);

        var weakenedEdge = new Edge
        {
            Id = existingEdge.Id,
            FromNodeId = existingEdge.FromNodeId,
            ToNodeId = existingEdge.ToNodeId,
            Type = existingEdge.Type,
            Weight = Math.Max(existingEdge.Weight * 0.5, _decayPolicy.MinWeight),
            Confidence = existingEdge.Confidence * 0.8,
            SourceType = existingEdge.SourceType,
            Steps = existingEdge.Steps,
            ValidFrom = existingEdge.ValidFrom,
            ValidTo = _clock.UtcNow
        };

        await store.UpsertEdgeAsync(weakenedEdge, ct);
    }

    /// <summary>
    /// Check if a conflict exists between two edges.
    /// Two edges conflict if they have the same From, To and Type,
    /// but different ValidFrom timestamps.
    /// </summary>
    public bool HasConflict(Edge edge1, Edge edge2)
    {
        return edge1.FromNodeId == edge2.FromNodeId
            && edge1.ToNodeId == edge2.ToNodeId
            && edge1.Type == edge2.Type
            && edge1.ValidFrom != edge2.ValidFrom;
    }
}