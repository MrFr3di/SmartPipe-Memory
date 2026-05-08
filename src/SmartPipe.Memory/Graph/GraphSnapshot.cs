namespace SmartPipe.Memory.Graph;

/// <summary>
/// Full graph snapshot for export/import.
/// </summary>
public sealed record GraphSnapshot
{
    /// <summary>All nodes in the graph.</summary>
    public IReadOnlyList<Node> Nodes { get; init; } = Array.Empty<Node>();

    /// <summary>All edges in the graph.</summary>
    public IReadOnlyList<Edge> Edges { get; init; } = Array.Empty<Edge>();

    /// <summary>Schema version at export time.</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>When the snapshot was created.</summary>
    public DateTime ExportedAt { get; init; } = DateTime.UtcNow;
}