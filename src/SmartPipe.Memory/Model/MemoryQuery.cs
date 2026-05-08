namespace SmartPipe.Memory.Model;

/// <summary>
/// Immutable graph query object.
/// Supports structural search, pathfinding, traversal, and insight retrieval.
/// </summary>
public sealed record MemoryQuery
{
    /// <summary>Filter by node type (e.g., "File", "Transformer").</summary>
    public string? NodeType { get; init; }

    /// <summary>Filter tree for HealthScore, FailureProb, ResourceStrain, PredictedLatencyMs.</summary>
    public FilterNode? Filter { get; init; }

    /// <summary>Filter by edge type for graph traversal.</summary>
    public string? EdgeType { get; init; }

    /// <summary>Start node for pathfinding or traversal.</summary>
    public string? StartNodeId { get; init; }

    /// <summary>Target node for pathfinding.</summary>
    public string? TargetNodeId { get; init; }

    /// <summary>Maximum depth for traversal queries.</summary>
    public int? MaxDepth { get; init; }

    /// <summary>Maximum number of results.</summary>
    public int? Limit { get; init; }

    /// <summary>Property to order by: "HealthScore", "CreatedAt".</summary>
    public string? OrderBy { get; init; }

    /// <summary>Order descending if true.</summary>
    public bool OrderDesc { get; init; }

    /// <summary>Type of query to execute.</summary>
    public QueryType Type { get; init; }
}

/// <summary>
/// Types of queries supported by MemoryQueryExecutor.
/// </summary>
public enum QueryType
{
    /// <summary>Find nodes matching filters.</summary>
    FindNodes,

    /// <summary>Find shortest path between two nodes.</summary>
    FindPath,

    /// <summary>Traverse graph from a starting node.</summary>
    Traverse,

    /// <summary>Find generated insights.</summary>
    FindInsights
}