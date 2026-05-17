using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Model;

namespace SmartPipe.Memory.Storage;

/// <summary>
/// Core contract for graph storage.
/// Supports CRUD operations, queries, pathfinding, traversal,
/// time-travel queries, and node-filtered traversals.
/// Implementations: InMemoryGraphStore, SqliteWALStore, DiskBackedGraphStore (v0.1.1).
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

    /// <summary>
    /// Stream nodes as they existed at a specific point in time.
    /// Filters by <c>ValidFrom &lt;= asOf AND (ValidTo IS NULL OR ValidTo &gt; asOf)</c>.
    /// </summary>
    IAsyncEnumerable<Node> QueryNodesAsOfAsync(
        MemoryQuery query,
        DateTime asOf,
        CancellationToken ct = default
    );

    /// <summary>
    /// Stream edges as they existed at a specific point in time.
    /// Filters by <c>ValidFrom &lt;= asOf AND (ValidTo IS NULL OR ValidTo &gt; asOf)</c>.
    /// </summary>
    IAsyncEnumerable<Edge> QueryEdgesAsOfAsync(
        MemoryQuery query,
        DateTime asOf,
        CancellationToken ct = default
    );

    /// <summary>Find shortest path between two nodes.</summary>
    /// <param name="fromNodeId">Start node identifier.</param>
    /// <param name="toNodeId">Target node identifier.</param>
    /// <param name="edgeType">Edge type to traverse.</param>
    /// <param name="maxDepth">Maximum search depth.</param>
    /// <param name="nodeFilter">Optional filter applied to each visited node. Nodes returning false are skipped.</param>
    /// <param name="minWeight">Optional minimum edge weight. Edges below this threshold are skipped.</param>
    /// <param name="minConfidence">Optional minimum edge confidence. Edges below this threshold are skipped.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<PathSegment>> FindPathAsync(
        string fromNodeId,
        string toNodeId,
        string edgeType,
        int maxDepth,
        Func<Node, bool>? nodeFilter = null,
        double? minWeight = null,
        double? minConfidence = null,
        CancellationToken ct = default
    );

    /// <summary>Traverse graph from a starting node.</summary>
    /// <param name="startNodeId">Start node identifier.</param>
    /// <param name="edgeType">Edge type to traverse.</param>
    /// <param name="maxDepth">Maximum traversal depth.</param>
    /// <param name="limit">Maximum number of nodes to visit.</param>
    /// <param name="nodeFilter">Optional filter applied to each visited node. Nodes returning false are skipped.</param>
    /// <param name="minWeight">Optional minimum edge weight. Edges below this threshold are skipped.</param>
    /// <param name="minConfidence">Optional minimum edge confidence. Edges below this threshold are skipped.</param>
    /// <param name="ct">Cancellation token.</param>
    IAsyncEnumerable<(Node Node, int Depth)> TraverseAsync(
        string startNodeId,
        string edgeType,
        int maxDepth,
        int limit,
        Func<Node, bool>? nodeFilter = null,
        double? minWeight = null,
        double? minConfidence = null,
        CancellationToken ct = default
    );

    /// <summary>Query insights.</summary>
    IAsyncEnumerable<Edge> QueryInsightsAsync(MemoryQuery query, CancellationToken ct = default);

    /// <summary>Run Leiden clustering and return discovered clusters.</summary>
    Task<IReadOnlyList<Cluster>> ClusterAsync(CancellationToken ct = default);

    /// <summary>Get all outgoing edges keyed by source node id.</summary>
    IReadOnlyDictionary<string, IReadOnlyList<Edge>> GetOutEdges();

    /// <summary>Returns all nodes as a read-only dictionary.</summary>
    IReadOnlyDictionary<string, Node> GetAllNodes();

    /// <summary>Get weakened edges from a node (weight &lt; 0.3) for decay recalculation.</summary>
    Task<IReadOnlyList<Edge>> GetWeakenedEdgesFromAsync(
        string nodeId,
        CancellationToken ct = default
    );

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
        CancellationToken ct = default
    );

    /// <summary>Channel for buffering metrics from synchronous OnMetrics callback.</summary>
    System.Threading.Channels.ChannelWriter<MetricsEntry> MetricsChannel { get; }
}

/// <summary>
/// Store state enum.
/// </summary>
public enum StoreState
{
    /// <summary>Normal operation.</summary>
    Running,

    /// <summary>Shutting down, rejecting new writes.</summary>
    Draining,

    /// <summary>Shutdown complete, read-only.</summary>
    Drained,

    /// <summary>Error state.</summary>
    Faulted,
}

/// <summary>
/// Segment of a path returned by FindPathAsync.
/// </summary>
public readonly record struct PathSegment
{
    /// <summary>Node identifier.</summary>
    public string NodeId { get; init; }

    /// <summary>Edge type traversed to reach this node.</summary>
    public string EdgeType { get; init; }

    /// <summary>Weight of the edge traversed.</summary>
    public double Weight { get; init; }
}

/// <summary>
/// Metrics entry for asynchronous buffering from OnMetrics callback.
/// </summary>
public readonly record struct MetricsEntry
{
    /// <summary>Node identifier these metrics belong to.</summary>
    public string NodeId { get; init; }

    /// <summary>When the metrics were recorded.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>Metric name to value map.</summary>
    public IReadOnlyDictionary<string, double> Values { get; init; }
}
