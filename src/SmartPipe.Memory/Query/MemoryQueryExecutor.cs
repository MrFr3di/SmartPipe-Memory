using System.Diagnostics;
using System.Runtime.CompilerServices;
using SmartPipe.Memory.Diagnostics;
using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Model;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Query;

/// <summary>
/// Executes graph queries against the configured store.
/// Checks cache first, then delegates to the appropriate strategy.
/// Emits metrics and tracing spans for observability.
/// </summary>
public sealed class MemoryQueryExecutor
{
    private readonly IGraphStore _store;
    private readonly Caching.NodeCache _cache;
    private readonly MemoryMetrics? _metrics;

    /// <summary>
    /// Create a new query executor.
    /// </summary>
    /// <param name="store">The graph store to query.</param>
    /// <param name="cache">Node cache for faster lookups.</param>
    /// <param name="metrics">Optional metrics collector.</param>
    public MemoryQueryExecutor(IGraphStore store, Caching.NodeCache cache, MemoryMetrics? metrics = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _metrics = metrics;
    }

    /// <summary>
    /// Execute a query and stream results.
    /// </summary>
    /// <param name="query">The query to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async stream of query results.</returns>
    public async IAsyncEnumerable<QueryResult> ExecuteAsync(
        MemoryQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        _metrics?.RecordQuery();

        switch (query.Type)
        {
            case QueryType.FindNodes:
                await foreach (var result in ExecuteFindNodesAsync(query, ct))
                    yield return result;
                break;

            case QueryType.FindPath:
                await foreach (var result in ExecuteFindPathAsync(query, ct))
                    yield return result;
                break;

            case QueryType.Traverse:
                await foreach (var result in ExecuteTraverseAsync(query, ct))
                    yield return result;
                break;

            case QueryType.FindInsights:
                await foreach (var result in ExecuteFindInsightsAsync(query, ct))
                    yield return result;
                break;

            default:
                throw new NotSupportedException($"Query type '{query.Type}' is not supported.");
        }
    }

    private async IAsyncEnumerable<QueryResult> ExecuteFindNodesAsync(
        MemoryQuery query,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var activity = MemoryActivitySource.StartQuery("FindNodes");
        try
        {
            var nodes = query.AsOf.HasValue
                ? _store.QueryNodesAsOfAsync(query, query.AsOf.Value, ct)
                : _store.QueryNodesAsync(query, ct);

            await foreach (var node in nodes)
            {
                _cache.Set(node.Id, node);
                yield return new QueryResult { Type = ResultType.Node, Node = node };
            }
        }
        finally
        {
            // activity is disposed, which stops the span
        }
    }

    private async IAsyncEnumerable<QueryResult> ExecuteFindPathAsync(
        MemoryQuery query,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var activity = MemoryActivitySource.StartQuery("FindPath");
        try
        {
            var segments = await _store.FindPathAsync(
                query.StartNodeId!,
                query.TargetNodeId!,
                query.EdgeType ?? "DerivedFrom",
                query.MaxDepth ?? 10,
                query.NodeFilter,
                query.MinWeight,
                query.MinConfidence,
                ct);

            if (segments.Count > 0)
            {
                yield return new QueryResult
                {
                    Type = ResultType.Path,
                    Path = segments.Select(s => s.NodeId).ToList(),
                    TotalWeight = segments.Sum(s => s.Weight)
                };
            }
        }
        finally
        {
            // activity disposed
        }
    }

    private async IAsyncEnumerable<QueryResult> ExecuteTraverseAsync(
        MemoryQuery query,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var activity = MemoryActivitySource.StartQuery("Traverse");
        try
        {
            await foreach (var (node, depth) in _store.TraverseAsync(
                query.StartNodeId!,
                query.EdgeType ?? "DerivedFrom",
                query.MaxDepth ?? 3,
                query.Limit ?? 100,
                query.NodeFilter,
                query.MinWeight,
                query.MinConfidence,
                ct))
            {
                _cache.Set(node.Id, node);
                yield return new QueryResult { Type = ResultType.Node, Node = node, Depth = depth };
            }
        }
        finally
        {
            // activity disposed
        }
    }

    /// <summary>
    /// Execute an insights query.
    /// Note: Insights are implemented in SmartPipe.Memory.Health (v0.1.1).
    /// In v0.1.0, this method returns an empty result set.
    /// </summary>
    private static async IAsyncEnumerable<QueryResult> ExecuteFindInsightsAsync(
        MemoryQuery query,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.CompletedTask;
        yield break;
    }

    /// <summary>
    /// Execute Leiden clustering.
    /// </summary>
    public async Task<IReadOnlyList<Cluster>> ClusterAsync(CancellationToken ct = default)
    {
        using var activity = MemoryActivitySource.StartClustering(0);
        _metrics?.RecordQuery();
        try
        {
            return await _store.ClusterAsync(ct);
        }
        finally
        {
            // activity disposed
        }
    }

    /// <summary>
    /// Get all outgoing edges keyed by source node id.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<Edge>> GetOutEdges()
    {
        return _store.GetOutEdges();
    }
}