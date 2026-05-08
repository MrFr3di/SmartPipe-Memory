using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
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

    public InMemoryGraphStore(int metricsCapacity = 10000)
    {
        _metricsChannel = Channel.CreateBounded<MetricsEntry>(new BoundedChannelOptions(metricsCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    public StoreState State => _state;
    public bool IsDraining => _state is StoreState.Draining or StoreState.Drained;
    public ChannelWriter<MetricsEntry> MetricsChannel => _metricsChannel.Writer;

    // -- Nodes --

    public Task<Node> UpsertNodeAsync(Node node, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ThrowIfNotRunning();

        _lock.EnterWriteLock();
        try
        {
            var updated = new Node
            {
                Id = node.Id,
                Type = node.Type,
                Label = node.Label,
                Properties = node.Properties,
                Metadata = node.Metadata,
                Embedding = node.Embedding,
                HealthScore = node.HealthScore,
                FailureProbability = node.FailureProbability,
                PredictedLatencyMs = node.PredictedLatencyMs,
                ResourceStrain = node.ResourceStrain,
                ValidFrom = node.ValidFrom,
                ValidTo = node.ValidTo,
                Version = node.Version + 1
            };

            _nodes[node.Id] = updated;
            return Task.FromResult(updated);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Task BatchUpsertNodesAsync(IAsyncEnumerable<Node> nodes, CancellationToken ct = default)
    {
        ThrowIfNotRunning();

        return Task.Run(async () =>
        {
            await foreach (var node in nodes.WithCancellation(ct))
                await UpsertNodeAsync(node, ct);
        }, ct);
    }

    public Task<Node?> GetNodeAsync(string nodeId, CancellationToken ct = default)
    {
        _nodes.TryGetValue(nodeId, out var node);
        return Task.FromResult(node);
    }

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
                ValidTo = edge.ValidTo
            };

            var edges = _outEdges.GetOrAdd(newEdge.FromNodeId, _ => new List<Edge>());
            var existingIndex = edges.FindIndex(e => e.ToNodeId == newEdge.ToNodeId && e.Type == newEdge.Type);

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

    public async IAsyncEnumerable<Node> QueryNodesAsync(
        MemoryQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        IEnumerable<Node> result = _nodes.Values;

        if (query.NodeType is not null)
            result = result.Where(n => n.Type == query.NodeType);

        if (query.Filter is FilterNode.PropertyFilter pf)
            result = ApplyPropertyFilter(result, pf);

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

    public Task<IReadOnlyList<PathSegment>> FindPathAsync(
        string fromNodeId,
        string toNodeId,
        string edgeType,
        int maxDepth,
        CancellationToken ct = default)
    {
        var segments = BreadthFirstSearch(fromNodeId, toNodeId, edgeType, maxDepth, ct);
        return Task.FromResult<IReadOnlyList<PathSegment>>(segments);
    }

    public async IAsyncEnumerable<(Node Node, int Depth)> TraverseAsync(
        string startNodeId,
        string edgeType,
        int maxDepth,
        int limit,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var visited = new HashSet<string>();
        var queue = new Queue<(string NodeId, int Depth)>();
        queue.Enqueue((startNodeId, 0));
        visited.Add(startNodeId);

        var count = 0;
        while (queue.Count > 0 && count < limit)
        {
            ct.ThrowIfCancellationRequested();

            var (currentId, depth) = queue.Dequeue();

            if (_nodes.TryGetValue(currentId, out var node))
            {
                yield return (node, depth);
                count++;
            }

            if (depth >= maxDepth) continue;

            if (_outEdges.TryGetValue(currentId, out var edges))
            {
                foreach (var edge in edges.Where(e => e.Type.ToString() == edgeType))
                {
                    if (visited.Add(edge.ToNodeId))
                        queue.Enqueue((edge.ToNodeId, depth + 1));
                }
            }
        }

        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<Edge> QueryInsightsAsync(
        MemoryQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public Task<IReadOnlyList<Edge>> GetWeakenedEdgesFromAsync(
        string nodeId,
        CancellationToken ct = default)
    {
        if (_outEdges.TryGetValue(nodeId, out var edges))
            return Task.FromResult<IReadOnlyList<Edge>>(edges.Where(e => e.Weight < 0.3).ToList());

        return Task.FromResult<IReadOnlyList<Edge>>(Array.Empty<Edge>());
    }

    public Task InsertInsightAsync(Insight insight, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(insight);
        _insights.Add(insight);
        return Task.CompletedTask;
    }

    public Task UpdateNodeHealthAsync(
        string nodeId,
        double healthScore,
        double failureProb,
        double predictedLatencyMs,
        double resourceStrain,
        int expectedVersion,
        CancellationToken ct = default)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            if (node.Version != expectedVersion)
                throw new InvalidOperationException(
                    $"Version mismatch for node {nodeId}: expected {expectedVersion}, actual {node.Version}");

            node.HealthScore = healthScore;
            node.FailureProbability = failureProb;
            node.PredictedLatencyMs = predictedLatencyMs;
            node.ResourceStrain = resourceStrain;
        }

        return Task.CompletedTask;
    }

    public Task DrainAsync(CancellationToken ct = default)
    {
        _state = StoreState.Draining;
        _metricsChannel.Writer.Complete();
        _state = StoreState.Drained;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _lock.Dispose();
        return ValueTask.CompletedTask;
    }

    // -- Private helpers --

    private static IEnumerable<Node> ApplyPropertyFilter(IEnumerable<Node> nodes, FilterNode.PropertyFilter filter)
    {
        return filter.Operator switch
        {
            FilterOperator.LessThan => nodes.Where(n => GetProperty(n, filter.Property) < filter.Value),
            FilterOperator.GreaterThan => nodes.Where(n => GetProperty(n, filter.Property) > filter.Value),
            FilterOperator.Equals => nodes.Where(n => Math.Abs(GetProperty(n, filter.Property) - filter.Value) < 0.001),
            _ => nodes
        };
    }

    private static double GetProperty(Node node, string property) => property switch
    {
        "HealthScore" => node.HealthScore,
        "FailureProb" => node.FailureProbability,
        "ResourceStrain" => node.ResourceStrain,
        "PredictedLatencyMs" => node.PredictedLatencyMs,
        _ => 0.0
    };

    private static IEnumerable<Node> ApplyOrdering(IEnumerable<Node> nodes, string property, bool descending)
    {
        var ordered = property switch
        {
            "HealthScore" => nodes.OrderBy(n => n.HealthScore),
            "CreatedAt" => nodes.OrderBy(n => n.ValidFrom),
            _ => nodes.OrderBy(n => n.HealthScore)
        };

        return descending ? ordered.OrderByDescending(_ => 0) : ordered;
    }

    private List<PathSegment> BreadthFirstSearch(
        string fromNodeId,
        string toNodeId,
        string edgeType,
        int maxDepth,
        CancellationToken ct)
    {
        var parent = new Dictionary<string, (string NodeId, string EdgeType, double Weight)>();
        var queue = new Queue<(string NodeId, int Depth)>();
        var visited = new HashSet<string>();

        queue.Enqueue((fromNodeId, 0));
        visited.Add(fromNodeId);

        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            var (currentId, depth) = queue.Dequeue();

            if (currentId == toNodeId)
                return ReconstructPath(parent, fromNodeId, toNodeId);

            if (depth >= maxDepth) continue;

            if (_outEdges.TryGetValue(currentId, out var edges))
            {
                foreach (var edge in edges.Where(e => e.Type.ToString() == edgeType))
                {
                    if (visited.Add(edge.ToNodeId))
                    {
                        parent[edge.ToNodeId] = (currentId, edge.Type.ToString(), edge.Weight);
                        queue.Enqueue((edge.ToNodeId, depth + 1));
                    }
                }
            }
        }

        return new List<PathSegment>();
    }

    private static List<PathSegment> ReconstructPath(
        Dictionary<string, (string NodeId, string EdgeType, double Weight)> parent,
        string fromNodeId,
        string toNodeId)
    {
        var path = new List<PathSegment>();
        var current = toNodeId;

        while (current != fromNodeId && parent.TryGetValue(current, out var p))
        {
            path.Add(new PathSegment { NodeId = current, EdgeType = p.EdgeType, Weight = p.Weight });
            current = p.NodeId;
        }

        path.Add(new PathSegment { NodeId = fromNodeId, EdgeType = string.Empty, Weight = 0.0 });
        path.Reverse();
        return path;
    }

    private void ThrowIfNotRunning()
    {
        if (_state != StoreState.Running)
            throw new InvalidOperationException($"Store is not running. Current state: {_state}");
    }
}