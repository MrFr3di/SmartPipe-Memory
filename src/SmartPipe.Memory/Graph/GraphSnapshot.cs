namespace SmartPipe.Memory.Graph;

/// <summary>
/// Full graph snapshot for export/import.
/// </summary>
public sealed record GraphSnapshot
{
    /// <summary>All nodes in the graph.</summary>
    public IReadOnlyList<Node> Nodes { get; init; } = [];

    /// <summary>All edges in the graph.</summary>
    public IReadOnlyList<Edge> Edges { get; init; } = [];

    /// <summary>Schema version at export time.</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>When the snapshot was created.</summary>
    public DateTime ExportedAt { get; init; } = DateTime.UtcNow;
}
