using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Model;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Tests.Storage;

public sealed class InMemoryGraphStoreTests : IAsyncDisposable
{
    private readonly InMemoryGraphStore _store;

    public InMemoryGraphStoreTests()
    {
        _store = new InMemoryGraphStore();
    }

    [Fact]
    public async Task UpsertNode_NewNode_StoresIt()
    {
        var node = new Node { Id = "n1", Type = "File", Label = "test.txt" };

        var result = await _store.UpsertNodeAsync(node);

        Assert.Equal("n1", result.Id);
        Assert.Equal(2, result.Version);

        var retrieved = await _store.GetNodeAsync("n1");
        Assert.NotNull(retrieved);
        Assert.Equal("test.txt", retrieved.Label);
    }

    [Fact]
    public async Task UpsertNode_ExistingNode_UpdatesIt()
    {
        var node = new Node { Id = "n1", Type = "File", Label = "old.txt" };
        await _store.UpsertNodeAsync(node);

        var updated = new Node { Id = "n1", Type = "File", Label = "new.txt" };
        await _store.UpsertNodeAsync(updated);

        var result = await _store.GetNodeAsync("n1");
        Assert.NotNull(result);
        Assert.Equal("new.txt", result.Label);
    }

    [Fact]
    public async Task DeleteNode_RemovesNode()
    {
        await _store.UpsertNodeAsync(new Node { Id = "n1", Type = "File" });
        await _store.DeleteNodeAsync("n1");

        var result = await _store.GetNodeAsync("n1");
        Assert.Null(result);
    }

    [Fact]
    public async Task UpsertEdge_CreatesConnection()
    {
        await _store.UpsertNodeAsync(new Node { Id = "n1", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "n2", Type = "File" });

        var edge = new Edge { FromNodeId = "n1", ToNodeId = "n2", Type = EdgeType.DuplicateOf };
        var result = await _store.UpsertEdgeAsync(edge);

        Assert.Equal("n1", result.FromNodeId);
        Assert.Equal("n2", result.ToNodeId);
        Assert.True(result.Id > 0);
    }

    [Fact]
    public async Task QueryNodes_ByType_FiltersCorrectly()
    {
        await _store.UpsertNodeAsync(new Node { Id = "f1", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "f2", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "t1", Type = "Transformer" });

        var query = new MemoryQuery { NodeType = "File", Type = QueryType.FindNodes };
        var results = new List<Node>();

        await foreach (var node in _store.QueryNodesAsync(query))
            results.Add(node);

        Assert.Equal(2, results.Count);
        Assert.All(results, n => Assert.Equal("File", n.Type));
    }

    [Fact]
    public async Task QueryNodes_ByHealthScore_FiltersCorrectly()
    {
        await _store.UpsertNodeAsync(new Node { Id = "n1", Type = "File", HealthScore = 0.9 });
        await _store.UpsertNodeAsync(new Node { Id = "n2", Type = "File", HealthScore = 0.3 });
        await _store.UpsertNodeAsync(new Node { Id = "n3", Type = "File", HealthScore = 0.5 });

        var query = new MemoryQuery
        {
            NodeType = "File",
            Filter = new FilterNode.PropertyFilter
            {
                Property = "HealthScore",
                Operator = FilterOperator.LessThan,
                Value = 0.5
            },
            Type = QueryType.FindNodes
        };

        var results = new List<Node>();
        await foreach (var node in _store.QueryNodesAsync(query))
            results.Add(node);

        Assert.Single(results);
        Assert.Equal("n2", results[0].Id);
    }

    [Fact]
    public async Task FindPath_ExistingPath_ReturnsIt()
    {
        await _store.UpsertNodeAsync(new Node { Id = "A", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "B", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "C", Type = "File" });

        await _store.UpsertEdgeAsync(new Edge { FromNodeId = "A", ToNodeId = "B", Type = EdgeType.DerivedFrom });
        await _store.UpsertEdgeAsync(new Edge { FromNodeId = "B", ToNodeId = "C", Type = EdgeType.DerivedFrom });

        var path = await _store.FindPathAsync("A", "C", EdgeType.DerivedFrom.ToString(), 10);

        Assert.Equal(3, path.Count);
        Assert.Equal("A", path[0].NodeId);
        Assert.Equal("B", path[1].NodeId);
        Assert.Equal("C", path[2].NodeId);
    }

    [Fact]
    public async Task Traverse_FromStartNode_VisitsReachableNodes()
    {
        await _store.UpsertNodeAsync(new Node { Id = "A", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "B", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "C", Type = "File" });

        await _store.UpsertEdgeAsync(new Edge { FromNodeId = "A", ToNodeId = "B", Type = EdgeType.DerivedFrom });
        await _store.UpsertEdgeAsync(new Edge { FromNodeId = "A", ToNodeId = "C", Type = EdgeType.DerivedFrom });

        var results = new List<(Node Node, int Depth)>();
        await foreach (var item in _store.TraverseAsync("A", EdgeType.DerivedFrom.ToString(), 5, 100))
            results.Add(item);

        Assert.Equal(3, results.Count);
        Assert.Equal(0, results[0].Depth);
        Assert.Equal("A", results[0].Node.Id);
    }

    [Fact]
    public async Task UpdateNodeHealth_ValidVersion_UpdatesHealth()
    {
        var node = new Node { Id = "n1", Type = "File" };
        await _store.UpsertNodeAsync(node);

        await _store.UpdateNodeHealthAsync("n1", 0.5, 0.3, 150.0, 0.7, 2);

        var updated = await _store.GetNodeAsync("n1");
        Assert.Equal(0.5, updated!.HealthScore);
        Assert.Equal(0.3, updated.FailureProbability);
    }

    [Fact]
    public async Task UpdateNodeHealth_WrongVersion_Throws()
    {
        var node = new Node { Id = "n1", Type = "File" };
        await _store.UpsertNodeAsync(node);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _store.UpdateNodeHealthAsync("n1", 0.5, 0.3, 150.0, 0.7, 999));
    }

    [Fact]
    public async Task DrainAsync_ChangesState()
    {
        Assert.Equal(StoreState.Running, _store.State);

        await _store.DrainAsync();

        Assert.Equal(StoreState.Drained, _store.State);
    }

    [Fact]
    public async Task InsertInsight_StoresInsight()
    {
        var insight = new Insight
        {
            Id = "i1",
            Type = "BottleneckPrediction",
            Title = "Test insight",
            Confidence = 0.9
        };

        await _store.InsertInsightAsync(insight);
    }

    public ValueTask DisposeAsync() => _store.DisposeAsync();
}