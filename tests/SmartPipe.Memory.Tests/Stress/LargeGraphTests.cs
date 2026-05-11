using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Model;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Tests.Stress;

public sealed class LargeGraphTests : IAsyncDisposable
{
    private readonly InMemoryGraphStore _store;

    public LargeGraphTests()
    {
        _store = new InMemoryGraphStore(100000);
    }

    [Fact]
    public async Task Insert_100K_Nodes()
    {
        var nodes = Enumerable.Range(0, 100_000)
            .Select(i => new Node { Id = $"n{i}", Type = "File", Label = $"file_{i}.txt" });

        foreach (var node in nodes)
            await _store.UpsertNodeAsync(node);

        var retrieved = await _store.GetNodeAsync("n50000");
        Assert.NotNull(retrieved);
        Assert.Equal("file_50000.txt", retrieved.Label);
    }

    [Fact]
    public async Task Insert_100K_Edges()
    {
        for (var i = 0; i < 100_000; i++)
            await _store.UpsertNodeAsync(new Node { Id = $"n{i}", Type = "File" });

        for (var i = 0; i < 99_999; i++)
        {
            await _store.UpsertEdgeAsync(new Edge
            {
                FromNodeId = $"n{i}",
                ToNodeId = $"n{i + 1}",
                Type = EdgeType.DerivedFrom
            });
        }

        var path = await _store.FindPathAsync("n0", "n1000", "DerivedFrom", 2000);
        Assert.Equal(1001, path.Count);
    }

    [Fact]
    public async Task Traverse_DeepGraph_DoesNotStackOverflow()
    {
        for (var i = 0; i < 10_000; i++)
            await _store.UpsertNodeAsync(new Node { Id = $"n{i}", Type = "File" });

        for (var i = 0; i < 9_999; i++)
        {
            await _store.UpsertEdgeAsync(new Edge
            {
                FromNodeId = $"n{i}",
                ToNodeId = $"n{i + 1}",
                Type = EdgeType.DerivedFrom
            });
        }

        var results = new List<(Node Node, int Depth)>();
        await foreach (var item in _store.TraverseAsync("n0", "DerivedFrom", 100, 50))
            results.Add(item);

        Assert.Equal(50, results.Count);
    }

    [Fact]
    public async Task Concurrent_Upserts_NoDataLoss()
    {
        var tasks = Enumerable.Range(0, 100).Select(async i =>
        {
            for (var j = 0; j < 100; j++)
            {
                var id = $"n{i}_{j}";
                await _store.UpsertNodeAsync(new Node { Id = id, Type = "File" });
            }
        });

        await Task.WhenAll(tasks);

        var query = new MemoryQuery { NodeType = "File", Type = QueryType.FindNodes };
        var count = 0;
        await foreach (var _ in _store.QueryNodesAsync(query))
            count++;

        Assert.Equal(10_000, count);
    }

    // -- New in v0.1.1 --

    [Fact]
    public async Task FindPath_NodeFilter_BlocksUnhealthy_InLargeGraph()
    {
        for (var i = 0; i < 100; i++)
            await _store.UpsertNodeAsync(new Node { Id = $"n{i}", Type = "File", HealthScore = 1.0 });

        await _store.UpsertNodeAsync(new Node { Id = "bad", Type = "File", HealthScore = 0.01 });

        await _store.UpsertEdgeAsync(new Edge { FromNodeId = "n0", ToNodeId = "bad", Type = EdgeType.DerivedFrom });
        await _store.UpsertEdgeAsync(new Edge { FromNodeId = "bad", ToNodeId = "n99", Type = EdgeType.DerivedFrom });

        var path = await _store.FindPathAsync("n0", "n99", "DerivedFrom", 10, node => node.HealthScore > 0.1);
        Assert.Empty(path);
    }

    [Fact]
    public async Task TimeTravel_AsOf_FiltersExpiredNodes_InLargeGraph()
    {
        var past = DateTime.UtcNow.AddDays(-30);
        var recent = DateTime.UtcNow.AddDays(-1);

        for (var i = 0; i < 1000; i++)
            await _store.UpsertNodeAsync(new Node { Id = $"old_{i}", Type = "File", ValidFrom = past });

        for (var i = 0; i < 1000; i++)
            await _store.UpsertNodeAsync(new Node { Id = $"new_{i}", Type = "File", ValidFrom = recent });

        var query = new MemoryQuery { NodeType = "File", AsOf = DateTime.UtcNow.AddDays(-7), Type = QueryType.FindNodes };
        var count = 0;
        await foreach (var _ in _store.QueryNodesAsync(query))
            count++;

        Assert.Equal(1000, count);
    }

    [Fact]
    public async Task QueryNodesAsOfAsync_LargeGraph_ReturnsCorrectCount()
    {
        var past = DateTime.UtcNow.AddDays(-30);
        for (var i = 0; i < 5000; i++)
            await _store.UpsertNodeAsync(new Node { Id = $"n{i}", Type = "File", ValidFrom = past });

        var query = new MemoryQuery { NodeType = "File", Type = QueryType.FindNodes };
        var count = 0;
        await foreach (var _ in _store.QueryNodesAsOfAsync(query, DateTime.UtcNow))
            count++;

        Assert.Equal(5000, count);
    }

    public ValueTask DisposeAsync() => _store.DisposeAsync();
}