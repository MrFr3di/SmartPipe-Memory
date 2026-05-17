using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using SmartPipe.Memory.Algorithms.Classification;
using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Model;

namespace SmartPipe.Memory.Storage;

/// <summary>
/// In-memory graph store.
/// All graph traversals execute in memory for maximum performance.
/// Thread-safe via ConcurrentDictionary and ReaderWriterLockSlim.
/// </summary>
public sealed class InMemoryGraphStore : IGraphStore
{
    private readonly ConcurrentDictionary<string, Node> _nodes = new();
    private readonly ConcurrentDictionary<string, List<Edge>> _outEdges = new();
    private readonly List<Insight> _insights = new();
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly Channel<MetricsEntry> _metricsChannel;
    private StoreState _state = StoreState.Running;
    private long _nextEdgeId = 1;

    /// <summary>
    /// Create a new in-memory graph store.
    /// </summary>
    /// <param name="metricsCapacity">Capacity of the metrics buffer channel.</param>
    public InMemoryGraphStore(int metricsCapacity = 10000)
    {
        _metricsChannel = Channel.CreateBounded<MetricsEntry>(
            new BoundedChannelOptions(metricsCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
            }
        );
    }

    /// <inheritdoc />
    public StoreState State => _state;

    /// <inheritdoc />
    public bool IsDraining => _state is StoreState.Draining or StoreState.Drained;

    /// <inheritdoc />
    public ChannelWriter<MetricsEntry> MetricsChannel => _metricsChannel.Writer;

    // -- Nodes --

    /// <inheritdoc />
    public Task<Node> UpsertNodeAsync(Node node, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ThrowIfNotRunning();

        _lock.EnterWriteLock();
        try
        {
            var effectiveType = node.Type;
            if (Classifier is not null && string.IsNullOrEmpty(effectiveType))
            {
                effectiveType = Classifier.Classify(node);
            }

            var updated = new Node
            {
                Id = node.Id,
                Type = effectiveType,
                Label = node.Label,
                Properties = node.Properties,
                Metadata = node.Metadata,
                Embedding = node.Embedding,
                HealthScore = node.HealthScore,
                FailureProbability = node.FailureProbability,
                PredictedLatencyMs = node.PredictedLatencyMs,
                ResourceStrain = node.ResourceStrain,
                ValidFrom = node.ValidFrom.ToUniversalTime(),
                ValidTo = node.ValidTo?.ToUniversalTime(),
                Version = node.Version + 1,
            };

            _nodes[node.Id] = updated;
            return Task.FromResult(updated);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc />
    public Task BatchUpsertNodesAsync(IAsyncEnumerable<Node> nodes, CancellationToken ct = default)
    {
        ThrowIfNotRunning();
        return Task.Run(
            async () =>
            {
                await foreach (var node in nodes.WithCancellation(ct))
                    await UpsertNodeAsync(node, ct);
            },
            ct
        );
    }

    /// <inheritdoc />
    public Task<Node?> GetNodeAsync(string nodeId, CancellationToken ct = default)
    {
        _nodes.TryGetValue(nodeId, out var node);
        return Task.FromResult(node);
    }

    /// <inheritdoc />
    public Task DeleteNodeAsync(string nodeId, CancellationToken ct = default)
    {
        ThrowIfNotRunning();
        _lock.EnterWriteLock();
        try
        {
            _nodes.TryRemove(nodeId, out _);
            _outEdges.TryRemove(nodeId, out _);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
        return Task.CompletedTask;
    }

    // -- Edges --

    /// <inheritdoc />
    public Task<Edge> UpsertEdgeAsync(Edge edge, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(edge);
        ThrowIfNotRunning();

        _lock.EnterWriteLock();
        try
        {
            var newEdge = new Edge
            {
                Id = Interlocked.Increment(ref _nextEdgeId),
                FromNodeId = edge.FromNodeId,
                ToNodeId = edge.ToNodeId,
                Type = edge.Type,
                Weight = edge.Weight,
                Confidence = edge.Confidence,
                SourceType = edge.SourceType,
                Steps = edge.Steps,
                ValidFrom = edge.ValidFrom,
                ValidTo = edge.ValidTo,
            };

            var edges = _outEdges.GetOrAdd(newEdge.FromNodeId, _ => new List<Edge>());
            var existingIndex = edges.FindIndex(e =>
                e.ToNodeId == newEdge.ToNodeId && e.Type == newEdge.Type
            );

            if (existingIndex >= 0)
                edges[existingIndex] = newEdge;
            else
                edges.Add(newEdge);

            return Task.FromResult(newEdge);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc />
    public Task DeleteEdgeAsync(long edgeId, CancellationToken ct = default)
    {
        ThrowIfNotRunning();
        _lock.EnterWriteLock();
        try
        {
            foreach (var (_, edges) in _outEdges)
                edges.RemoveAll(e => e.Id == edgeId);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
        return Task.CompletedTask;
    }

    // -- Queries --

    /// <inheritdoc />
    public async IAsyncEnumerable<Node> QueryNodesAsync(
        MemoryQuery query,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        IEnumerable<Node> result = _nodes.Values;

        if (query.NodeType is not null)
            result = result.Where(n => n.Type == query.NodeType);

        if (query.Filter is not null)
            result = ApplyFilter(result, query.Filter);

        // Time-travel filter
        if (query.AsOf.HasValue)
            result = result.Where(n =>
                n.ValidFrom <= query.AsOf.Value
                && (n.ValidTo == null || n.ValidTo >= query.AsOf.Value)
            );

        if (query.OrderBy is not null)
            result = ApplyOrdering(result, query.OrderBy, query.OrderDesc);

        if (query.Limit.HasValue)
            result = result.Take(query.Limit.Value);

        foreach (var node in result)
        {
            ct.ThrowIfCancellationRequested();
            yield return node;
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Node> QueryNodesAsOfAsync(
        MemoryQuery query,
        DateTime asOf,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        var asOfQuery = query with { AsOf = asOf };
        await foreach (var node in QueryNodesAsync(asOfQuery, ct))
            yield return node;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Edge> QueryEdgesAsOfAsync(
        MemoryQuery query,
        DateTime asOf,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        foreach (var (_, edges) in _outEdges)
        {
            foreach (var edge in edges)
            {
                ct.ThrowIfCancellationRequested();

                if (edge.ValidFrom <= asOf && (edge.ValidTo == null || edge.ValidTo > asOf))
                {
                    // Apply edge type filter from query
                    if (query.EdgeType is null || edge.Type.ToString() == query.EdgeType)
                        yield return edge;
                }
            }
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PathSegment>> FindPathAsync(
        string fromNodeId,
        string toNodeId,
        string edgeType,
        int maxDepth,
        Func<Node, bool>? nodeFilter = null,
        double? minWeight = null,
        double? minConfidence = null,
        CancellationToken ct = default
    )
    {
        var result = GraphTraversalEngine.FindPath(
            _nodes,
            _outEdges,
            fromNodeId,
            toNodeId,
            edgeType,
            maxDepth,
            nodeFilter,
            minWeight,
            minConfidence,
            ct
        );

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<(Node Node, int Depth)> TraverseAsync(
        string startNodeId,
        string edgeType,
        int maxDepth,
        int limit,
        Func<Node, bool>? nodeFilter = null,
        double? minWeight = null,
        double? minConfidence = null,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        await foreach (
            var (node, depth) in GraphTraversalEngine.Traverse(
                _nodes,
                _outEdges,
                startNodeId,
                edgeType,
                maxDepth,
                limit,
                nodeFilter,
                minWeight,
                minConfidence,
                ct
            )
        )
        {
            yield return (node, depth);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Edge> QueryInsightsAsync(
        MemoryQuery query,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        // Return stored insights as Edge objects for compatibility
        foreach (var insight in _insights)
        {
            ct.ThrowIfCancellationRequested();

            if (query.NodeType is not null && insight.Type != query.NodeType)
                continue;

            if (query.Limit.HasValue && query.Limit.Value <= 0)
                break;

            yield return new Edge
            {
                Id = 0,
                FromNodeId = insight.RelatedNodeIds.FirstOrDefault() ?? string.Empty,
                ToNodeId = insight.Id,
                Type = EdgeType.FeedsInto,
                Weight = insight.Confidence,
                Confidence = insight.Confidence,
            };
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Cluster>> ClusterAsync(CancellationToken ct = default)
    {
        var leiden = new Algorithms.Clustering.LeidenClusterer();
        return leiden.Cluster(
            new Dictionary<string, Node>(_nodes),
            _outEdges.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<Edge>)kvp.Value),
            ct: ct
        );
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyList<Edge>> GetOutEdges()
    {
        return _outEdges.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<Edge>)kvp.Value);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, Node> GetAllNodes() => _nodes;

    /// <inheritdoc />
    public Task<IReadOnlyList<Edge>> GetWeakenedEdgesFromAsync(
        string nodeId,
        CancellationToken ct = default
    )
    {
        if (_outEdges.TryGetValue(nodeId, out var edges))
            return Task.FromResult<IReadOnlyList<Edge>>(edges.Where(e => e.Weight < 0.3).ToList());

        return Task.FromResult<IReadOnlyList<Edge>>(Array.Empty<Edge>());
    }

    /// <inheritdoc />
    public Task InsertInsightAsync(Insight insight, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(insight);
        _insights.Add(insight);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateNodeHealthAsync(
        string nodeId,
        double healthScore,
        double failureProb,
        double predictedLatencyMs,
        double resourceStrain,
        int expectedVersion,
        CancellationToken ct = default
    )
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            if (node.Version != expectedVersion)
                throw new InvalidOperationException(
                    $"Version mismatch for node {nodeId}: expected {expectedVersion}, actual {node.Version}"
                );

            node.HealthScore = healthScore;
            node.FailureProbability = failureProb;
            node.PredictedLatencyMs = predictedLatencyMs;
            node.ResourceStrain = resourceStrain;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DrainAsync(CancellationToken ct = default)
    {
        _state = StoreState.Draining;
        _metricsChannel.Writer.Complete();
        _state = StoreState.Drained;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _lock.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Optional classifier for nodes. When set and node Type is empty,
    /// the classifier will attempt to determine the type from Properties.
    /// </summary>
    public AutoClassifier? Classifier { get; set; }

    // -- Private helpers --

    private static IEnumerable<Node> ApplyPropertyFilter(
        IEnumerable<Node> nodes,
        FilterNode.PropertyFilter filter
    )
    {
        return filter.Operator switch
        {
            FilterOperator.LessThan => nodes.Where(n =>
                GetProperty(n, filter.Property) < filter.Value
            ),
            FilterOperator.GreaterThan => nodes.Where(n =>
                GetProperty(n, filter.Property) > filter.Value
            ),
            FilterOperator.Equals => nodes.Where(n =>
                Math.Abs(GetProperty(n, filter.Property) - filter.Value) < 0.001
            ),
            _ => nodes,
        };
    }

    private static IEnumerable<Node> ApplyAndFilter(IEnumerable<Node> nodes, FilterNode.And filter)
    {
        var leftIds = new HashSet<string>(ApplyFilter(nodes, filter.Left).Select(n => n.Id));
        return ApplyFilter(nodes, filter.Right).Where(n => leftIds.Contains(n.Id));
    }

    private static IEnumerable<Node> ApplyOrFilter(IEnumerable<Node> nodes, FilterNode.Or filter)
    {
        var leftResult = ApplyFilter(nodes, filter.Left);
        var rightResult = ApplyFilter(nodes, filter.Right);
        var allIds = new HashSet<string>(leftResult.Select(n => n.Id));
        allIds.UnionWith(rightResult.Select(n => n.Id));
        return nodes.Where(n => allIds.Contains(n.Id));
    }

    private static IEnumerable<Node> ApplyFilter(IEnumerable<Node> nodes, FilterNode filter)
    {
        return filter switch
        {
            FilterNode.PropertyFilter pf => ApplyPropertyFilter(nodes, pf),
            FilterNode.And and => ApplyAndFilter(nodes, and),
            FilterNode.Or or => ApplyOrFilter(nodes, or),
            _ => nodes,
        };
    }

    private static double GetProperty(Node node, string property) =>
        property switch
        {
            "HealthScore" => node.HealthScore,
            "FailureProb" => node.FailureProbability,
            "ResourceStrain" => node.ResourceStrain,
            "PredictedLatencyMs" => node.PredictedLatencyMs,
            _ => 0.0,
        };

    private static IEnumerable<Node> ApplyOrdering(
        IEnumerable<Node> nodes,
        string property,
        bool descending
    )
    {
        var ordered = property switch
        {
            "HealthScore" => nodes.OrderBy(n => n.HealthScore),
            "CreatedAt" => nodes.OrderBy(n => n.ValidFrom),
            _ => nodes.OrderBy(n => n.HealthScore),
        };

        return descending ? ordered.ThenByDescending(_ => 0) : ordered;
    }

    private void ThrowIfNotRunning()
    {
        if (_state != StoreState.Running)
            throw new InvalidOperationException($"Store is not running. Current state: {_state}");
    }
}
