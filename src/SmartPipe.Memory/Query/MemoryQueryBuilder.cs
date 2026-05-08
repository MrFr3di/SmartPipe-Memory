using SmartPipe.Memory.Model;

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
    private string? _edgeType;
    private string? _startNodeId;
    private string? _targetNodeId;
    private int? _maxDepth;
    private int? _limit;
    private string? _orderBy;
    private bool _orderDesc;

    internal MemoryQueryBuilder(MemoryQueryExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
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
        _filter = _filter is null ? filter : new FilterNode.And(_filter, filter);
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
            Type = _startNodeId is not null && _targetNodeId is not null
                ? QueryType.FindPath
                : _startNodeId is not null
                    ? QueryType.Traverse
                    : QueryType.FindNodes
        };

        return _executor.ExecuteAsync(query, ct);
    }
}