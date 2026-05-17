namespace SmartPipe.Memory.Graph;

/// <summary>
/// Result of graph clustering — a group of related nodes.
/// </summary>
public sealed record Cluster
{
    /// <summary>Cluster identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Node identifiers in this cluster.</summary>
    public IReadOnlyList<string> NodeIds { get; init; } = Array.Empty<string>();

    /// <summary>Number of nodes in this cluster.</summary>
    public int Size => NodeIds.Count;

    /// <summary>Modularity score of this cluster (0..1).</summary>
    public double Modularity { get; init; }

    /// <summary>When the cluster was computed.</summary>
    public DateTime ComputedAt { get; init; } = DateTime.UtcNow;
}
