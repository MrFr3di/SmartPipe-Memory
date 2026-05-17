using SmartPipe.Memory.Caching;
using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Model;
using SmartPipe.Memory.Query;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Tests.Query;

public sealed class MemoryQueryBuilderIntegrationTests : IAsyncDisposable
{
    private readonly InMemoryGraphStore _store;
    private readonly NodeCache _cache;
    private readonly MemoryQueryExecutor _executor;
    private readonly MemoryQueryBuilder _builder;

    public MemoryQueryBuilderIntegrationTests()
    {
        _store = new InMemoryGraphStore();
        _cache = new NodeCache(100);
        _executor = new MemoryQueryExecutor(_store, _cache);
        _builder = new MemoryQueryBuilder(_executor);
    }

    [Fact]
    public async Task FindClusters_WithEdges_ReturnsClusters()
    {
        await _store.UpsertNodeAsync(new Node { Id = "A", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "B", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "C", Type = "File" });
        await _store.UpsertEdgeAsync(
            new Edge
            {
                FromNodeId = "A",
                ToNodeId = "B",
                Type = EdgeType.DerivedFrom,
                Weight = 1.0,
            }
        );
        await _store.UpsertEdgeAsync(
            new Edge
            {
                FromNodeId = "B",
                ToNodeId = "C",
                Type = EdgeType.DerivedFrom,
                Weight = 1.0,
            }
        );
        await _store.UpsertEdgeAsync(
            new Edge
            {
                FromNodeId = "A",
                ToNodeId = "C",
                Type = EdgeType.DerivedFrom,
                Weight = 1.0,
            }
        );

        var clusters = new List<QueryResult>();
        await foreach (var c in _builder.FindClusters())
            clusters.Add(c);

        Assert.NotEmpty(clusters);
        Assert.All(clusters, c => Assert.Equal(ResultType.Cluster, c.Type));
    }

    [Fact]
    public async Task EstimateNeighbors_ReturnsEstimate()
    {
        await _store.UpsertNodeAsync(new Node { Id = "A", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "B", Type = "File" });
        await _store.UpsertEdgeAsync(
            new Edge
            {
                FromNodeId = "A",
                ToNodeId = "B",
                Type = EdgeType.DerivedFrom,
                Weight = 1.0,
            }
        );

        var estimate = _builder.EstimateNeighbors("A");
        Assert.True(estimate > 0);
    }

    [Fact]
    public async Task HasDegree_ReturnsCorrectCount()
    {
        await _store.UpsertNodeAsync(new Node { Id = "A", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "B", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "C", Type = "File" });
        await _store.UpsertEdgeAsync(
            new Edge
            {
                FromNodeId = "A",
                ToNodeId = "B",
                Type = EdgeType.DerivedFrom,
                Weight = 1.0,
            }
        );
        await _store.UpsertEdgeAsync(
            new Edge
            {
                FromNodeId = "A",
                ToNodeId = "C",
                Type = EdgeType.DerivedFrom,
                Weight = 1.0,
            }
        );

        var degree = _builder.HasDegree("A");
        Assert.Equal(2, degree);
    }

    public ValueTask DisposeAsync() => _store.DisposeAsync();
}
