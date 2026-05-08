using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Model;

namespace SmartPipe.Memory.Storage;

/// <summary>
/// Core contract for graph storage.
/// Supports CRUD operations, queries, pathfinding, and traversal.
/// Implementations: InMemoryGraphStore, SqliteWALStore.
/// </summary>
public interface IGraphStore : IAsyncDisposable
{
    /// <summary>Current store state.</summary>
    StoreState State { get; }

    /// <summary>Whether the store is in the process of draining. New writes are rejected when true.</summary>
    bool IsDraining { get; }

    /// <summary>
    /// Graceful shutdown: stop accepting writes, complete active operations,
    /// flush in-memory buffers to disk, perform WAL checkpoint.
    /// After this, the store is read-only.
    /// </summary>
    Task DrainAsync(CancellationToken ct = default);

    // -- Nodes --

    /// <summary>Insert or update a node.</summary>
    Task<Node> UpsertNodeAsync(Node node, CancellationToken ct = default);

    /// <summary>
    /// Batch insert nodes. Uses INSERT OR REPLACE with multiple VALUES
    /// to minimize round-trips to SQLite. Buffers per 100 nodes.
    /// </summary>
    Task BatchUpsertNodesAsync(IAsyncEnumerable<Node> nodes, CancellationToken ct = default);

    /// <summary>Get a node by id or null.</summary>
    Task<Node?> GetNodeAsync(string nodeId, CancellationToken ct = default);

    /// <summary>Delete a node and all its edges (cascade).</summary>
    Task DeleteNodeAsync(string nodeId, CancellationToken ct = default);

    // -- Edges --

    /// <summary>Insert or update an edge. Uniqueness: (from, to, type).</summary>
    Task<Edge> UpsertEdgeAsync(Edge edge, CancellationToken ct = default);

    /// <summary>Delete an edge by id.</summary>
    Task DeleteEdgeAsync(long edgeId, CancellationToken ct = default);

    // -- Queries --

    /// <summary>Stream nodes matching the query.</summary>
    IAsyncEnumerable<Node> QueryNodesAsync(MemoryQuery query, CancellationToken ct = default);

    /// <summary>Find shortest path between two nodes.</summary>
    Task<IReadOnlyList<PathSegment>> FindPathAsync(
        string fromNodeId,
        string toNodeId,
        string edgeType,
        int maxDepth,
        CancellationToken ct = default);

    /// <summary>Traverse graph from a starting node.</summary>
    IAsyncEnumerable<(Node Node, int Depth)> TraverseAsync(
        string startNodeId,
        string edgeType,
        int maxDepth,
        int limit,
        CancellationToken ct = default);

    /// <summary>Query insights (placeholder for v0.2.0).</summary>
    IAsyncEnumerable<Edge> QueryInsightsAsync(MemoryQuery query, CancellationToken ct = default);

    /// <summary>Get weakened edges from a node (weight &lt; 0.3) for decay recalculation.</summary>
    Task<IReadOnlyList<Edge>> GetWeakenedEdgesFromAsync(string nodeId, CancellationToken ct = default);

    /// <summary>Save a predictive analytics insight.</summary>
    Task InsertInsightAsync(Insight insight, CancellationToken ct = default);

    /// <summary>Update node health atomically with optimistic concurrency.</summary>
    Task UpdateNodeHealthAsync(
        string nodeId,
        double healthScore,
        double failureProb,
        double predictedLatencyMs,
        double resourceStrain,
        int expectedVersion,
        CancellationToken ct = default);

    /// <summary>Channel for buffering metrics from synchronous OnMetrics callback.</summary>
    System.Threading.Channels.ChannelWriter<MetricsEntry> MetricsChannel { get; }
}

/// <summary>
/// Store state enum.
/// </summary>
public enum StoreState
{
    Running,
    Draining,
    Drained,
    Faulted
}

/// <summary>
/// Segment of a path returned by FindPathAsync.
/// </summary>
public readonly record struct PathSegment
{
    public string NodeId { get; init; }
    public string EdgeType { get; init; }
    public double Weight { get; init; }
}

/// <summary>
/// Metrics entry for asynchronous buffering from OnMetrics callback.
/// </summary>
public readonly record struct MetricsEntry
{
    public string NodeId { get; init; }
    public DateTime Timestamp { get; init; }
    public IReadOnlyDictionary<string, double> Values { get; init; }
}