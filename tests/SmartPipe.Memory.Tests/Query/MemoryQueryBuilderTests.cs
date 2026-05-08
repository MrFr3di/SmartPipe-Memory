using SmartPipe.Memory.Caching;
using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Model;
using SmartPipe.Memory.Query;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Tests.Query;

public sealed class MemoryQueryBuilderTests : IAsyncDisposable
{
    private readonly InMemoryGraphStore _store;
    private readonly NodeCache _cache;
    private readonly MemoryQueryExecutor _executor;
    private readonly MemoryQueryBuilder _builder;

    public MemoryQueryBuilderTests()
    {
        _store = new InMemoryGraphStore();
        _cache = new NodeCache(100);
        _executor = new MemoryQueryExecutor(_store, _cache);
        _builder = new MemoryQueryBuilder(_executor);
    }

    [Fact]
    public async Task Nodes_WithType_ReturnsFilteredResults()
    {
        await _store.UpsertNodeAsync(new Node { Id = "f1", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "f2", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "t1", Type = "Transformer" });

        var results = new List<QueryResult>();
        await foreach (var r in _builder.Nodes("File").ExecuteAsync())
            results.Add(r);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("File", r.Node!.Type));
    }

    [Fact]
    public async Task Where_HealthScore_BelowThreshold()
    {
        await _store.UpsertNodeAsync(new Node { Id = "n1", Type = "File", HealthScore = 0.9 });
        await _store.UpsertNodeAsync(new Node { Id = "n2", Type = "File", HealthScore = 0.3 });

        var results = new List<QueryResult>();
        await foreach (var r in _builder
            .Nodes("File")
            .Where("HealthScore", FilterOperator.LessThan, 0.5)
            .ExecuteAsync())
            results.Add(r);

        Assert.Single(results);
        Assert.Equal("n2", results[0].Node!.Id);
    }

    [Fact]
    public async Task ConnectedVia_FiltersByEdgeType()
    {
        await _store.UpsertNodeAsync(new Node { Id = "A", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "B", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "C", Type = "File" });

        await _store.UpsertEdgeAsync(new Edge { FromNodeId = "A", ToNodeId = "B", Type = EdgeType.DuplicateOf });
        await _store.UpsertEdgeAsync(new Edge { FromNodeId = "A", ToNodeId = "C", Type = EdgeType.DerivedFrom });

        var results = new List<QueryResult>();
        await foreach (var r in _builder
            .Nodes("File")
            .ConnectedVia(EdgeType.DuplicateOf.ToString())
            .ExecuteAsync())
            results.Add(r);

        Assert.All(results, r => Assert.Equal("File", r.Node!.Type));
    }

    [Fact]
    public async Task ShortestPath_ExistingPath_ReturnsIt()
    {
        await _store.UpsertNodeAsync(new Node { Id = "A", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "B", Type = "File" });

        await _store.UpsertEdgeAsync(new Edge { FromNodeId = "A", ToNodeId = "B", Type = EdgeType.DerivedFrom });

        var results = new List<QueryResult>();
        await foreach (var r in _builder
            .ShortestPath("A", "B", EdgeType.DerivedFrom.ToString())
            .ExecuteAsync())
            results.Add(r);

        Assert.Single(results);
        Assert.Equal(ResultType.Path, results[0].Type);
        Assert.Equal(2, results[0].Path!.Count);
    }

    [Fact]
    public async Task Traverse_WithDepth_VisitsNodes()
    {
        await _store.UpsertNodeAsync(new Node { Id = "A", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "B", Type = "File" });
        await _store.UpsertEdgeAsync(new Edge { FromNodeId = "A", ToNodeId = "B", Type = EdgeType.DerivedFrom });

        var results = new List<QueryResult>();
        await foreach (var r in _builder
            .StartFrom("A")
            .Traverse(EdgeType.DerivedFrom.ToString(), 3)
            .ExecuteAsync())
            results.Add(r);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task Limit_RestrictsResults()
    {
        for (var i = 0; i < 100; i++)
            await _store.UpsertNodeAsync(new Node { Id = $"n{i}", Type = "File" });

        var results = new List<QueryResult>();
        await foreach (var r in _builder.Nodes("File").Limit(10).ExecuteAsync())
            results.Add(r);

        Assert.Equal(10, results.Count);
    }

    [Fact]
    public async Task OrderBy_HealthScore_SortsCorrectly()
    {
        await _store.UpsertNodeAsync(new Node { Id = "n1", Type = "File", HealthScore = 0.3 });
        await _store.UpsertNodeAsync(new Node { Id = "n2", Type = "File", HealthScore = 0.9 });

        var results = new List<QueryResult>();
        await foreach (var r in _builder
            .Nodes("File")
            .OrderBy("HealthScore")
            .ExecuteAsync())
            results.Add(r);

        Assert.True(results[0].Node!.HealthScore <= results[1].Node!.HealthScore);
    }

    public ValueTask DisposeAsync() => _store.DisposeAsync();
}