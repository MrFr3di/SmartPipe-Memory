using System.Runtime.CompilerServices;
using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Model;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Query;

/// <summary>
/// Executes graph queries against the configured store.
/// Checks cache first, then delegates to the appropriate strategy.
/// </summary>
public sealed class MemoryQueryExecutor
{
    private readonly IGraphStore _store;
    private readonly Caching.NodeCache _cache;

    public MemoryQueryExecutor(IGraphStore store, Caching.NodeCache cache)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <summary>
    /// Execute a query and stream results.
    /// </summary>
    public async IAsyncEnumerable<QueryResult> ExecuteAsync(
        MemoryQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

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
        await foreach (var node in _store.QueryNodesAsync(query, ct))
        {
            _cache.Set(node.Id, node);
            yield return new QueryResult { Type = ResultType.Node, Node = node };
        }
    }

    private async IAsyncEnumerable<QueryResult> ExecuteFindPathAsync(
        MemoryQuery query,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var segments = await _store.FindPathAsync(
            query.StartNodeId!,
            query.TargetNodeId!,
            query.EdgeType ?? "DerivedFrom",
            query.MaxDepth ?? 10,
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

    private async IAsyncEnumerable<QueryResult> ExecuteTraverseAsync(
        MemoryQuery query,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var (node, depth) in _store.TraverseAsync(
            query.StartNodeId!,
            query.EdgeType ?? "DerivedFrom",
            query.MaxDepth ?? 3,
            query.Limit ?? 100,
            ct))
        {
            _cache.Set(node.Id, node);
            yield return new QueryResult { Type = ResultType.Node, Node = node, Depth = depth };
        }
    }

    /// <summary>
    /// Execute an insights query.
    /// Note: Insights are implemented in SmartPipe.Memory.Health (v0.2.0).
    /// In v0.1.0, this method returns an empty result set.
    /// </summary>
    private static async IAsyncEnumerable<QueryResult> ExecuteFindInsightsAsync(
        MemoryQuery query,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.CompletedTask;
        yield break;
    }
}