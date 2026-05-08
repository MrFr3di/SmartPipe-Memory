using SmartPipe.Memory.Graph;

namespace SmartPipe.Memory.Model;

/// <summary>
/// Universal query result. Can contain nodes, edges, paths, or clusters.
/// Returned by MemoryQueryExecutor.
/// </summary>
public sealed record QueryResult
{
    /// <summary>Type of result.</summary>
    public ResultType Type { get; init; }

    /// <summary>Node result (for FindNodes queries).</summary>
    public Node? Node { get; init; }

    /// <summary>Edge result (for Traverse queries).</summary>
    public Edge? Edge { get; init; }

    /// <summary>Path as ordered list of node identifiers (for FindPath queries).</summary>
    public IReadOnlyList<string>? Path { get; init; }

    /// <summary>Total weight of the path (for FindPath queries).</summary>
    public double? TotalWeight { get; init; }

    /// <summary>Cluster result (for clustering queries).</summary>
    public Cluster? Cluster { get; init; }

    /// <summary>Depth in traversal (for Traverse queries).</summary>
    public int Depth { get; init; }
}

/// <summary>
/// Types of query results.
/// </summary>
public enum ResultType
{
    Node,
    Edge,
    Path,
    Cluster
}