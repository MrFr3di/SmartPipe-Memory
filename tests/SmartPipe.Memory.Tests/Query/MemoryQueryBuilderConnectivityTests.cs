using SmartPipe.Memory.Caching;
using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Query;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Tests.Query;

public sealed class MemoryQueryBuilderConnectivityTests : IAsyncDisposable
{
    private readonly InMemoryGraphStore _store;
    private readonly NodeCache _cache;
    private readonly MemoryQueryExecutor _executor;
    private readonly MemoryQueryBuilder _builder;

    public MemoryQueryBuilderConnectivityTests()
    {
        _store = new InMemoryGraphStore();
        _cache = new NodeCache(100);
        _executor = new MemoryQueryExecutor(_store, _cache);
        _builder = new MemoryQueryBuilder(_executor);
    }

    [Fact]
    public async Task TopologicalSort_LinearGraph_ReturnsCorrectOrder()
    {
        await _store.UpsertNodeAsync(new Node { Id = "A", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "B", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "C", Type = "File" });
        await _store.UpsertEdgeAsync(new Edge { FromNodeId = "A", ToNodeId = "B", Type = EdgeType.DerivedFrom });
        await _store.UpsertEdgeAsync(new Edge { FromNodeId = "B", ToNodeId = "C", Type = EdgeType.DerivedFrom });

        var result = _builder.TopologicalSort();
        Assert.Equal(new[] { "A", "B", "C" }, result.Sorted);
        Assert.False(result.HasCycles);
    }

    [Fact]
    public async Task HasCycles_CyclicGraph_ReturnsTrue()
    {
        await _store.UpsertNodeAsync(new Node { Id = "A", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "B", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "C", Type = "File" });
        await _store.UpsertEdgeAsync(new Edge { FromNodeId = "A", ToNodeId = "B", Type = EdgeType.DerivedFrom });
        await _store.UpsertEdgeAsync(new Edge { FromNodeId = "B", ToNodeId = "C", Type = EdgeType.DerivedFrom });
        await _store.UpsertEdgeAsync(new Edge { FromNodeId = "C", ToNodeId = "A", Type = EdgeType.DerivedFrom });

        Assert.True(_builder.HasCycles());
    }

    [Fact]
    public async Task FindSCC_CycleGraph_ReturnsOneComponent()
    {
        await _store.UpsertNodeAsync(new Node { Id = "A", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "B", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "C", Type = "File" });
        await _store.UpsertEdgeAsync(new Edge { FromNodeId = "A", ToNodeId = "B", Type = EdgeType.DerivedFrom });
        await _store.UpsertEdgeAsync(new Edge { FromNodeId = "B", ToNodeId = "C", Type = EdgeType.DerivedFrom });
        await _store.UpsertEdgeAsync(new Edge { FromNodeId = "C", ToNodeId = "A", Type = EdgeType.DerivedFrom });

        var components = _builder.FindSCC();
        Assert.Single(components);
        Assert.Equal(3, components[0].Count);
    }

    [Fact]
    public async Task FindWCC_DisconnectedGraph_ReturnsCorrectComponents()
    {
        await _store.UpsertNodeAsync(new Node { Id = "A", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "B", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "C", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "D", Type = "File" });
        await _store.UpsertEdgeAsync(new Edge { FromNodeId = "A", ToNodeId = "B", Type = EdgeType.DerivedFrom });
        await _store.UpsertEdgeAsync(new Edge { FromNodeId = "C", ToNodeId = "D", Type = EdgeType.DerivedFrom });

        var components = _builder.FindWCC();
        Assert.Equal(2, components.Count);
    }

    public ValueTask DisposeAsync() => _store.DisposeAsync();
}