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

    // -- Basic --

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
    }

    [Fact]
    public async Task Where_HealthScore_BelowThreshold()
    {
        await _store.UpsertNodeAsync(
            new Node
            {
                Id = "n1",
                Type = "File",
                HealthScore = 0.9,
            }
        );
        await _store.UpsertNodeAsync(
            new Node
            {
                Id = "n2",
                Type = "File",
                HealthScore = 0.3,
            }
        );

        var results = new List<QueryResult>();
        await foreach (
            var r in _builder
                .Nodes("File")
                .Where("HealthScore", FilterOperator.LessThan, 0.5)
                .ExecuteAsync()
        )
            results.Add(r);

        Assert.Single(results);
        Assert.Equal("n2", results[0].Node!.Id);
    }

    // -- AND / OR --

    [Fact]
    public async Task And_TwoFilters_BothApply()
    {
        await _store.UpsertNodeAsync(
            new Node
            {
                Id = "n1",
                Type = "File",
                HealthScore = 0.9,
                FailureProbability = 0.05,
            }
        );
        await _store.UpsertNodeAsync(
            new Node
            {
                Id = "n2",
                Type = "File",
                HealthScore = 0.3,
                FailureProbability = 0.2,
            }
        );

        var results = new List<QueryResult>();
        await foreach (
            var r in _builder
                .Nodes("File")
                .Where("HealthScore", FilterOperator.LessThan, 0.5)
                .And()
                .Where("FailureProb", FilterOperator.GreaterThan, 0.1)
                .ExecuteAsync()
        )
            results.Add(r);

        Assert.Single(results);
        Assert.Equal("n2", results[0].Node!.Id);
    }

    [Fact]
    public async Task Or_TwoFilters_EitherApplies()
    {
        await _store.UpsertNodeAsync(
            new Node
            {
                Id = "n1",
                Type = "File",
                HealthScore = 0.9,
            }
        );
        await _store.UpsertNodeAsync(
            new Node
            {
                Id = "n2",
                Type = "File",
                HealthScore = 0.3,
            }
        );

        var results = new List<QueryResult>();
        await foreach (
            var r in _builder
                .Nodes("File")
                .Where("HealthScore", FilterOperator.LessThan, 0.35)
                .Or()
                .Where("HealthScore", FilterOperator.GreaterThan, 0.8)
                .ExecuteAsync()
        )
            results.Add(r);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task Or_OnlyAffectsNextWhere()
    {
        await _store.UpsertNodeAsync(
            new Node
            {
                Id = "n1",
                Type = "File",
                HealthScore = 0.9,
                FailureProbability = 0.05,
            }
        );
        await _store.UpsertNodeAsync(
            new Node
            {
                Id = "n2",
                Type = "File",
                HealthScore = 0.3,
                FailureProbability = 0.05,
            }
        );
        await _store.UpsertNodeAsync(
            new Node
            {
                Id = "n3",
                Type = "File",
                HealthScore = 0.9,
                FailureProbability = 0.2,
            }
        );

        var results = new List<QueryResult>();
        await foreach (
            var r in _builder
                .Nodes("File")
                .Where("HealthScore", FilterOperator.LessThan, 0.5)
                .Or()
                .Where("HealthScore", FilterOperator.GreaterThan, 0.8)
                .Where("FailureProb", FilterOperator.GreaterThan, 0.1)
                .ExecuteAsync()
        )
            results.Add(r);

        Assert.Single(results);
        Assert.Equal("n3", results[0].Node!.Id);
    }

    // -- ShortestPath --

    [Fact]
    public async Task ShortestPath_ExistingPath_ReturnsIt()
    {
        await _store.UpsertNodeAsync(new Node { Id = "A", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "B", Type = "File" });
        await _store.UpsertEdgeAsync(
            new Edge
            {
                FromNodeId = "A",
                ToNodeId = "B",
                Type = EdgeType.DerivedFrom,
            }
        );

        var results = new List<QueryResult>();
        await foreach (var r in _builder.ShortestPath("A", "B", "DerivedFrom").ExecuteAsync())
            results.Add(r);

        Assert.Single(results);
        Assert.Equal(ResultType.Path, results[0].Type);
        Assert.Equal(2, results[0].Path!.Count);
    }

    // -- Traverse --

    [Fact]
    public async Task Traverse_WithDepth_VisitsNodes()
    {
        await _store.UpsertNodeAsync(new Node { Id = "A", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "B", Type = "File" });
        await _store.UpsertEdgeAsync(
            new Edge
            {
                FromNodeId = "A",
                ToNodeId = "B",
                Type = EdgeType.DerivedFrom,
            }
        );

        var results = new List<QueryResult>();
        await foreach (var r in _builder.StartFrom("A").Traverse("DerivedFrom", 3).ExecuteAsync())
            results.Add(r);

        Assert.Equal(2, results.Count);
    }

    // -- Limit / OrderBy --

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
        await _store.UpsertNodeAsync(
            new Node
            {
                Id = "n1",
                Type = "File",
                HealthScore = 0.3,
            }
        );
        await _store.UpsertNodeAsync(
            new Node
            {
                Id = "n2",
                Type = "File",
                HealthScore = 0.9,
            }
        );

        var results = new List<QueryResult>();
        await foreach (var r in _builder.Nodes("File").OrderBy("HealthScore").ExecuteAsync())
            results.Add(r);

        Assert.True(results[0].Node!.HealthScore <= results[1].Node!.HealthScore);
    }

    // -- AsOf / Time-Travel --

    [Fact]
    public async Task AsOf_ReturnsHistoricalState()
    {
        var past = DateTime.UtcNow.AddDays(-30);
        var recent = DateTime.UtcNow.AddDays(-1);

        await _store.UpsertNodeAsync(
            new Node
            {
                Id = "old",
                Type = "File",
                ValidFrom = past,
            }
        );
        await _store.UpsertNodeAsync(
            new Node
            {
                Id = "new",
                Type = "File",
                ValidFrom = recent,
            }
        );

        var results = new List<QueryResult>();
        await foreach (
            var r in _builder.Nodes("File").AsOf(DateTime.UtcNow.AddDays(-7)).ExecuteAsync()
        )
            results.Add(r);

        Assert.Single(results);
        Assert.Equal("old", results[0].Node!.Id);
    }

    [Fact]
    public async Task Between_TimeRange_NotYetImplemented()
    {
        await _store.UpsertNodeAsync(
            new Node
            {
                Id = "n1",
                Type = "File",
                ValidFrom = DateTime.UtcNow.AddDays(-5),
            }
        );

        var results = new List<QueryResult>();
        await foreach (
            var r in _builder
                .Nodes("File")
                .Between(DateTime.UtcNow.AddDays(-10), DateTime.UtcNow)
                .ExecuteAsync()
        )
            results.Add(r);

        Assert.Single(results);
    }

    // -- WhereNode --

    [Fact]
    public async Task WhereNode_FiltersDuringTraversal()
    {
        await _store.UpsertNodeAsync(
            new Node
            {
                Id = "A",
                Type = "File",
                HealthScore = 1.0,
            }
        );
        await _store.UpsertNodeAsync(
            new Node
            {
                Id = "B",
                Type = "File",
                HealthScore = 0.02,
            }
        );

        await _store.UpsertEdgeAsync(
            new Edge
            {
                FromNodeId = "A",
                ToNodeId = "B",
                Type = EdgeType.DerivedFrom,
            }
        );

        var results = new List<QueryResult>();
        await foreach (
            var r in _builder
                .ShortestPath("A", "B", "DerivedFrom")
                .WhereNode(node => node.HealthScore > 0.1)
                .ExecuteAsync()
        )
            results.Add(r);

        Assert.Empty(results);
    }

    // -- MinWeight / MinConfidence --

    [Fact]
    public async Task MinWeight_FiltersWeakEdges()
    {
        await _store.UpsertNodeAsync(new Node { Id = "A", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "B", Type = "File" });
        await _store.UpsertEdgeAsync(
            new Edge
            {
                FromNodeId = "A",
                ToNodeId = "B",
                Type = EdgeType.DerivedFrom,
                Weight = 0.2,
            }
        );

        var results = new List<QueryResult>();
        await foreach (
            var r in _builder.ShortestPath("A", "B", "DerivedFrom").MinWeight(0.5).ExecuteAsync()
        )
            results.Add(r);

        Assert.Empty(results);
    }

    [Fact]
    public async Task MinConfidence_FiltersLowConfidenceEdges()
    {
        await _store.UpsertNodeAsync(new Node { Id = "A", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "B", Type = "File" });
        await _store.UpsertEdgeAsync(
            new Edge
            {
                FromNodeId = "A",
                ToNodeId = "B",
                Type = EdgeType.DerivedFrom,
                Confidence = 0.3,
            }
        );

        var results = new List<QueryResult>();
        await foreach (
            var r in _builder
                .ShortestPath("A", "B", "DerivedFrom")
                .MinConfidence(0.9)
                .ExecuteAsync()
        )
            results.Add(r);

        Assert.Empty(results);
    }

    // -- Factory method --

    [Fact]
    public void Create_ReturnsBuilder()
    {
        var builder = MemoryQueryBuilder.Create(_store, _cache);
        Assert.NotNull(builder);
    }

    public ValueTask DisposeAsync() => _store.DisposeAsync();
}
