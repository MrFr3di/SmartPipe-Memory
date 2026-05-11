using SmartPipe.Memory.Model;
using System.Runtime.CompilerServices;

namespace SmartPipe.Memory.Query;

/// <summary>
/// Fluent API for building type-safe graph queries.
/// The only public API for querying SmartPipe.Memory.
/// </summary>
public sealed class MemoryQueryBuilder
{
    private readonly MemoryQueryExecutor _executor;
    private string? _nodeType;
    private FilterNode? _filter;
    private bool _useOr;
    private string? _edgeType;
    private string? _startNodeId;
    private string? _targetNodeId;
    private int? _maxDepth;
    private int? _limit;
    private string? _orderBy;
    private bool _orderDesc;
    private DateTime? _asOf;
    private DateTime? _timeRangeFrom;
    private DateTime? _timeRangeTo;
    private double? _minWeight;
    private double? _minConfidence;
    private Func<Graph.Node, bool>? _nodeFilter;

    internal MemoryQueryBuilder(MemoryQueryExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    /// <summary>
    /// Create a new MemoryQueryBuilder without dependency injection.
    /// </summary>
    /// <param name="store">The graph store to query.</param>
    /// <param name="cache">Node cache for faster lookups.</param>
    /// <returns>A new MemoryQueryBuilder instance.</returns>
    public static MemoryQueryBuilder Create(Storage.IGraphStore store, Caching.NodeCache cache)
    {
        var executor = new MemoryQueryExecutor(store, cache);
        return new MemoryQueryBuilder(executor);
    }

    /// <summary>Filter by node type (e.g., "File", "Transformer").</summary>
    public MemoryQueryBuilder Nodes(string type)
    {
        _nodeType = type ?? throw new ArgumentNullException(nameof(type));
        return this;
    }

    /// <summary>Filter by a property of the node's health vector.</summary>
    public MemoryQueryBuilder Where(string property, FilterOperator op, double value)
    {
        var filter = new FilterNode.PropertyFilter { Property = property, Operator = op, Value = value };

        if (_filter is null)
        {
            _filter = filter;
        }
        else if (_useOr)
        {
            _filter = new FilterNode.Or(_filter, filter);
            _useOr = false;
        }
        else
        {
            _filter = new FilterNode.And(_filter, filter);
        }

        return this;
    }

    /// <summary>
    /// Combine the next Where filter with logical OR instead of AND.
    /// Only affects the immediately following Where call.
    /// </summary>
    public MemoryQueryBuilder Or()
    {
        _useOr = true;
        return this;
    }

    /// <summary>
    /// Combine the next Where filter with logical AND. This is the default behavior.
    /// </summary>
    public MemoryQueryBuilder And()
    {
        _useOr = false;
        return this;
    }

    /// <summary>Filter nodes connected via a specific edge type.</summary>
    public MemoryQueryBuilder ConnectedVia(string edgeType)
    {
        _edgeType = edgeType ?? throw new ArgumentNullException(nameof(edgeType));
        return this;
    }

    /// <summary>Start traversal from this node.</summary>
    public MemoryQueryBuilder StartFrom(string nodeId)
    {
        _startNodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
        return this;
    }

    /// <summary>Find path to this node.</summary>
    public MemoryQueryBuilder To(string nodeId)
    {
        _targetNodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
        return this;
    }

    /// <summary>Find the shortest path between two nodes.</summary>
    public MemoryQueryBuilder ShortestPath(string fromNodeId, string toNodeId, string edgeType)
    {
        _startNodeId = fromNodeId ?? throw new ArgumentNullException(nameof(fromNodeId));
        _targetNodeId = toNodeId ?? throw new ArgumentNullException(nameof(toNodeId));
        _edgeType = edgeType ?? throw new ArgumentNullException(nameof(edgeType));
        return this;
    }

    /// <summary>Traverse the graph from the current start node.</summary>
    public MemoryQueryBuilder Traverse(string edgeType, int maxDepth)
    {
        _edgeType = edgeType ?? throw new ArgumentNullException(nameof(edgeType));
        _maxDepth = maxDepth;
        return this;
    }

    /// <summary>Maximum traversal depth.</summary>
    public MemoryQueryBuilder MaxDepth(int depth)
    {
        if (depth <= 0) throw new ArgumentOutOfRangeException(nameof(depth), "Depth must be positive.");
        _maxDepth = depth;
        return this;
    }

    /// <summary>Maximum number of results.</summary>
    public MemoryQueryBuilder Limit(int limit)
    {
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be positive.");
        _limit = limit;
        return this;
    }

    /// <summary>Order results by a property.</summary>
    public MemoryQueryBuilder OrderBy(string property, bool descending = false)
    {
        _orderBy = property ?? throw new ArgumentNullException(nameof(property));
        _orderDesc = descending;
        return this;
    }

    /// <summary>
    /// Return the graph state as it was at this point in time.
    /// Filters nodes and edges by ValidFrom and ValidTo.
    /// </summary>
    public MemoryQueryBuilder AsOf(DateTime timestamp)
    {
        _asOf = timestamp;
        return this;
    }

    /// <summary>
    /// Return changes in a time range.
    /// </summary>
    public MemoryQueryBuilder Between(DateTime from, DateTime to)
    {
        _timeRangeFrom = from;
        _timeRangeTo = to;
        return this;
    }

    /// <summary>
    /// Filter nodes by a predicate during graph traversal (BFS, Dijkstra).
    /// Nodes returning false are skipped and not visited.
    /// </summary>
    public MemoryQueryBuilder WhereNode(Func<Graph.Node, bool> predicate)
    {
        _nodeFilter = predicate ?? throw new ArgumentNullException(nameof(predicate));
        return this;
    }

    /// <summary>
    /// Minimum edge weight for pathfinding and traversal.
    /// Edges with weight below this threshold are skipped.
    /// </summary>
    public MemoryQueryBuilder MinWeight(double weight)
    {
        if (weight < 0 || weight > 1) throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be between 0 and 1.");
        _minWeight = weight;
        return this;
    }

    /// <summary>
    /// Minimum edge confidence for pathfinding and traversal.
    /// Edges with confidence below this threshold are skipped.
    /// </summary>
    public MemoryQueryBuilder MinConfidence(double confidence)
    {
        if (confidence < 0 || confidence > 1) throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0 and 1.");
        _minConfidence = confidence;
        return this;
    }

    /// <summary>Execute the query and return results.</summary>
    public IAsyncEnumerable<QueryResult> ExecuteAsync(CancellationToken ct = default)
    {
        var query = new MemoryQuery
        {
            NodeType = _nodeType,
            Filter = _filter,
            EdgeType = _edgeType,
            StartNodeId = _startNodeId,
            TargetNodeId = _targetNodeId,
            MaxDepth = _maxDepth,
            Limit = _limit,
            OrderBy = _orderBy,
            OrderDesc = _orderDesc,
            AsOf = _asOf,
            TimeRangeFrom = _timeRangeFrom,
            TimeRangeTo = _timeRangeTo,
            MinWeight = _minWeight,
            MinConfidence = _minConfidence,
            NodeFilter = _nodeFilter,
            Type = _startNodeId is not null && _targetNodeId is not null
                ? QueryType.FindPath
                : _startNodeId is not null
                    ? QueryType.Traverse
                    : QueryType.FindNodes
        };

        return _executor.ExecuteAsync(query, ct);
    }

    /// <summary>Run Leiden clustering and return discovered clusters.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async stream of Cluster results.</returns>
    
    public IAsyncEnumerable<QueryResult> FindClusters(CancellationToken ct = default)
    {
        return ExecuteClusterAsync(ct);
    }

    private async IAsyncEnumerable<QueryResult> ExecuteClusterAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var clusters = await _executor.ClusterAsync(ct);
        foreach (var cluster in clusters)
        {
            yield return new QueryResult { Type = ResultType.Cluster, Cluster = cluster };
        }
    }

    /// <summary>
    /// Estimate the number of unique neighbors for the given node using HyperLogLog.
    /// </summary>
    /// <param name="nodeId">Node identifier.</param>
    /// <returns>Estimated number of unique target nodes.</returns>
    public double EstimateNeighbors(string nodeId)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);

        var estimator = new Algorithms.Estimation.CardinalityEstimator();
        return estimator.EstimateNeighbors(
            _executor.GetOutEdges(),
            nodeId);
    }

    /// <summary>
    /// Get the number of direct outgoing edges for a node.
    /// </summary>
    /// <param name="nodeId">Node identifier.</param>
    /// <returns>Number of outgoing edges.</returns>
    public int HasDegree(string nodeId)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);

        var centrality = new Algorithms.Centrality.DegreeCentrality();
        return centrality.Compute(
            _executor.GetOutEdges(),
            nodeId);
    }
}