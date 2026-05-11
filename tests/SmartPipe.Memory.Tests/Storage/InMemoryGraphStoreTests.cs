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

    // -- Basic CRUD --

    [Fact]
    public async Task UpsertNode_NewNode_StoresIt()
    {
        var node = new Node { Id = "n1", Type = "File", Label = "test.txt" };
        var result = await _store.UpsertNodeAsync(node);

        Assert.Equal("n1", result.Id);
        var retrieved = await _store.GetNodeAsync("n1");
        Assert.NotNull(retrieved);
        Assert.Equal("test.txt", retrieved.Label);
    }

    [Fact]
    public async Task UpsertNode_ExistingNode_UpdatesIt()
    {
        await _store.UpsertNodeAsync(new Node { Id = "n1", Type = "File", Label = "old.txt" });
        await _store.UpsertNodeAsync(new Node { Id = "n1", Type = "File", Label = "new.txt" });

        var result = await _store.GetNodeAsync("n1");
        Assert.Equal("new.txt", result!.Label);
    }

    [Fact]
    public async Task DeleteNode_RemovesNode()
    {
        await _store.UpsertNodeAsync(new Node { Id = "n1", Type = "File" });
        await _store.DeleteNodeAsync("n1");
        Assert.Null(await _store.GetNodeAsync("n1"));
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

    // -- Query by type --

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

    // -- HealthScore filter --

    [Fact]
    public async Task QueryNodes_ByHealthScore_FiltersCorrectly()
    {
        await _store.UpsertNodeAsync(new Node { Id = "n1", Type = "File", HealthScore = 0.9 });
        await _store.UpsertNodeAsync(new Node { Id = "n2", Type = "File", HealthScore = 0.3 });

        var query = new MemoryQuery
        {
            NodeType = "File",
            Filter = new FilterNode.PropertyFilter { Property = "HealthScore", Operator = FilterOperator.LessThan, Value = 0.5 },
            Type = QueryType.FindNodes
        };

        var results = new List<Node>();
        await foreach (var node in _store.QueryNodesAsync(query))
            results.Add(node);

        Assert.Single(results);
        Assert.Equal("n2", results[0].Id);
    }

    // -- AND filter --

    [Fact]
    public async Task QueryNodes_AndFilter_CombinesCorrectly()
    {
        await _store.UpsertNodeAsync(new Node { Id = "n1", Type = "File", HealthScore = 0.9, FailureProbability = 0.05 });
        await _store.UpsertNodeAsync(new Node { Id = "n2", Type = "File", HealthScore = 0.3, FailureProbability = 0.2 });
        await _store.UpsertNodeAsync(new Node { Id = "n3", Type = "File", HealthScore = 0.4, FailureProbability = 0.05 });

        var filter = new FilterNode.And(
            new FilterNode.PropertyFilter { Property = "HealthScore", Operator = FilterOperator.LessThan, Value = 0.5 },
            new FilterNode.PropertyFilter { Property = "FailureProb", Operator = FilterOperator.GreaterThan, Value = 0.1 }
        );

        var query = new MemoryQuery { NodeType = "File", Filter = filter, Type = QueryType.FindNodes };
        var results = new List<Node>();
        await foreach (var node in _store.QueryNodesAsync(query))
            results.Add(node);

        Assert.Single(results);
        Assert.Equal("n2", results[0].Id);
    }

    // -- OR filter --

    [Fact]
    public async Task QueryNodes_OrFilter_CombinesCorrectly()
    {
        await _store.UpsertNodeAsync(new Node { Id = "n1", Type = "File", HealthScore = 0.9 });
        await _store.UpsertNodeAsync(new Node { Id = "n2", Type = "File", HealthScore = 0.3 });
        await _store.UpsertNodeAsync(new Node { Id = "n3", Type = "File", HealthScore = 0.4 });

        var filter = new FilterNode.Or(
            new FilterNode.PropertyFilter { Property = "HealthScore", Operator = FilterOperator.LessThan, Value = 0.35 },
            new FilterNode.PropertyFilter { Property = "HealthScore", Operator = FilterOperator.GreaterThan, Value = 0.8 }
        );

        var query = new MemoryQuery { NodeType = "File", Filter = filter, Type = QueryType.FindNodes };
        var results = new List<Node>();
        await foreach (var node in _store.QueryNodesAsync(query))
            results.Add(node);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, n => n.Id == "n1");
        Assert.Contains(results, n => n.Id == "n2");
    }

    // -- Time-travel: AsOf --

    [Fact]
    public async Task QueryNodes_AsOf_FiltersByTime()
    {
        var past = DateTime.UtcNow.AddDays(-30);
        var recent = DateTime.UtcNow.AddDays(-1);

        await _store.UpsertNodeAsync(new Node { Id = "old", Type = "File", ValidFrom = past });
        await _store.UpsertNodeAsync(new Node { Id = "new", Type = "File", ValidFrom = recent });

        var query = new MemoryQuery { NodeType = "File", AsOf = DateTime.UtcNow.AddDays(-7), Type = QueryType.FindNodes };
        var results = new List<Node>();
        await foreach (var node in _store.QueryNodesAsync(query))
            results.Add(node);

        Assert.Single(results);
        Assert.Equal("old", results[0].Id);
    }

    // -- Time-travel: expired node excluded --

    [Fact]
    public async Task QueryNodes_AsOf_ExcludesExpiredNodes()
    {
        var past = DateTime.UtcNow.AddDays(-30);
        var expired = DateTime.UtcNow.AddDays(-5);

        await _store.UpsertNodeAsync(new Node { Id = "expired", Type = "File", ValidFrom = past, ValidTo = expired });

        var query = new MemoryQuery { NodeType = "File", AsOf = DateTime.UtcNow, Type = QueryType.FindNodes };
        var results = new List<Node>();
        await foreach (var node in _store.QueryNodesAsync(query))
            results.Add(node);

        Assert.Empty(results);
    }

    // -- Time-travel: QueryNodesAsOfAsync --

    [Fact]
    public async Task QueryNodesAsOfAsync_ReturnsCorrectState()
    {
        var past = DateTime.UtcNow.AddDays(-30);
        await _store.UpsertNodeAsync(new Node { Id = "old", Type = "File", ValidFrom = past });

        var query = new MemoryQuery { NodeType = "File", Type = QueryType.FindNodes };
        var results = new List<Node>();
        await foreach (var node in _store.QueryNodesAsOfAsync(query, DateTime.UtcNow.AddDays(-7)))
            results.Add(node);

        Assert.Single(results);
        Assert.Equal("old", results[0].Id);
    }

    // -- Time-travel: QueryEdgesAsOfAsync --

    [Fact]
    public async Task QueryEdgesAsOfAsync_ReturnsCorrectState()
    {
        var past = DateTime.UtcNow.AddDays(-30);
        await _store.UpsertNodeAsync(new Node { Id = "n1", Type = "File", ValidFrom = past });
        await _store.UpsertNodeAsync(new Node { Id = "n2", Type = "File", ValidFrom = past });
        await _store.UpsertEdgeAsync(new Edge { FromNodeId = "n1", ToNodeId = "n2", Type = EdgeType.DerivedFrom, ValidFrom = past });

        var query = new MemoryQuery { EdgeType = "DerivedFrom", Type = QueryType.FindNodes };
        var results = new List<Edge>();
        await foreach (var edge in _store.QueryEdgesAsOfAsync(query, DateTime.UtcNow))
            results.Add(edge);

        Assert.Single(results);
    }

    // -- Node filter during pathfinding --

    [Fact]
    public async Task FindPath_NodeFilter_BlocksUnhealthyNodes()
    {
        await _store.UpsertNodeAsync(new Node { Id = "A", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "B", Type = "File", HealthScore = 0.05 });
        await _store.UpsertNodeAsync(new Node { Id = "C", Type = "File" });

        await _store.UpsertEdgeAsync(new Edge { FromNodeId = "A", ToNodeId = "B", Type = EdgeType.DerivedFrom });
        await _store.UpsertEdgeAsync(new Edge { FromNodeId = "B", ToNodeId = "C", Type = EdgeType.DerivedFrom });

        var path = await _store.FindPathAsync("A", "C", "DerivedFrom", 10, node => node.HealthScore > 0.1);

        Assert.Empty(path);
    }

    // -- Node filter during traversal --

    [Fact]
    public async Task Traverse_NodeFilter_SkipsFilteredNodes()
    {
        await _store.UpsertNodeAsync(new Node { Id = "A", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "B", Type = "File", HealthScore = 0.05 });

        await _store.UpsertEdgeAsync(new Edge { FromNodeId = "A", ToNodeId = "B", Type = EdgeType.DerivedFrom });

        var results = new List<(Node Node, int Depth)>();
        await foreach (var item in _store.TraverseAsync("A", "DerivedFrom", 10, 100, node => node.HealthScore > 0.1))
            results.Add(item);

        Assert.Single(results);
        Assert.Equal("A", results[0].Node.Id);
    }

    // -- Version check --

    [Fact]
    public async Task UpdateNodeHealth_WrongVersion_Throws()
    {
        await _store.UpsertNodeAsync(new Node { Id = "n1", Type = "File" });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _store.UpdateNodeHealthAsync("n1", 0.5, 0.3, 150.0, 0.7, 999));
    }

    // -- Ordering --

    [Fact]
    public async Task QueryNodes_OrderBy_HealthScore_Ascending()
    {
        await _store.UpsertNodeAsync(new Node { Id = "n1", Type = "File", HealthScore = 0.3 });
        await _store.UpsertNodeAsync(new Node { Id = "n2", Type = "File", HealthScore = 0.9 });

        var query = new MemoryQuery { NodeType = "File", OrderBy = "HealthScore", OrderDesc = false, Type = QueryType.FindNodes };
        var results = new List<Node>();
        await foreach (var node in _store.QueryNodesAsync(query))
            results.Add(node);

        Assert.True(results[0].HealthScore <= results[1].HealthScore);
    }

    // -- Limit --

    [Fact]
    public async Task QueryNodes_Limit_RestrictsResults()
    {
        for (var i = 0; i < 100; i++)
            await _store.UpsertNodeAsync(new Node { Id = $"n{i}", Type = "File" });

        var query = new MemoryQuery { NodeType = "File", Limit = 10, Type = QueryType.FindNodes };
        var results = new List<Node>();
        await foreach (var node in _store.QueryNodesAsync(query))
            results.Add(node);

        Assert.Equal(10, results.Count);
    }

    // -- Drain --

    [Fact]
    public async Task DrainAsync_ChangesState()
    {
        Assert.Equal(StoreState.Running, _store.State);
        await _store.DrainAsync();
        Assert.Equal(StoreState.Drained, _store.State);
    }

    public ValueTask DisposeAsync() => _store.DisposeAsync();
}