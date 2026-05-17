using SmartPipe.Core;
using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Health.Policies;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Health.Tests.Policies;

public sealed class ConflictResolverTests : IAsyncDisposable
{
    private readonly InMemoryGraphStore _store;
    private readonly MemoryDecayPolicy _decayPolicy;
    private readonly ConflictResolver _resolver;

    public ConflictResolverTests()
    {
        _store = new InMemoryGraphStore();
        var clock = new TimeProviderClock();
        _decayPolicy = new MemoryDecayPolicy(clock: clock);
        _resolver = new ConflictResolver(_decayPolicy, clock);
    }

    [Fact]
    public async Task ResolveAsync_WeakensExistingEdge()
    {
        var clock = new TimeProviderClock();
        var decayPolicy = new MemoryDecayPolicy(clock: clock);
        var resolver = new ConflictResolver(decayPolicy, clock);
        var store = new InMemoryGraphStore();

        await store.UpsertNodeAsync(new Node { Id = "n1", Type = "File" });
        await store.UpsertNodeAsync(new Node { Id = "n2", Type = "File" });

        var edge = await store.UpsertEdgeAsync(
            new Edge
            {
                FromNodeId = "n1",
                ToNodeId = "n2",
                Type = EdgeType.DerivedFrom,
                Weight = 1.0,
                Confidence = 1.0,
                ValidFrom = clock.UtcNow,
            }
        );

        await resolver.ResolveAsync(edge, store);

        // After resolve the edge should not be visible in the future
        var edgesAfter = new List<Edge>();
        await foreach (var e in store.QueryEdgesAsOfAsync(new Model.MemoryQuery(), clock.UtcNow))
        {
            edgesAfter.Add(e);
        }

        var nonExistent = edgesAfter.FirstOrDefault(e =>
            e.FromNodeId == "n1" && e.ToNodeId == "n2" && e.Type == EdgeType.DerivedFrom
        );
        Assert.Null(nonExistent);
    }

    [Fact]
    public void HasConflict_SameFromToType_DifferentValidFrom_ReturnsTrue()
    {
        var edge1 = new Edge
        {
            FromNodeId = "A",
            ToNodeId = "B",
            Type = EdgeType.DerivedFrom,
            ValidFrom = DateTime.UtcNow.AddDays(-2),
        };
        var edge2 = new Edge
        {
            FromNodeId = "A",
            ToNodeId = "B",
            Type = EdgeType.DerivedFrom,
            ValidFrom = DateTime.UtcNow,
        };

        Assert.True(_resolver.HasConflict(edge1, edge2));
    }

    [Fact]
    public void HasConflict_DifferentType_ReturnsFalse()
    {
        var edge1 = new Edge
        {
            FromNodeId = "A",
            ToNodeId = "B",
            Type = EdgeType.DerivedFrom,
            ValidFrom = DateTime.UtcNow,
        };
        var edge2 = new Edge
        {
            FromNodeId = "A",
            ToNodeId = "B",
            Type = EdgeType.DuplicateOf,
            ValidFrom = DateTime.UtcNow,
        };

        Assert.False(_resolver.HasConflict(edge1, edge2));
    }

    [Fact]
    public void HasConflict_DifferentToNode_ReturnsFalse()
    {
        var edge1 = new Edge
        {
            FromNodeId = "A",
            ToNodeId = "B",
            Type = EdgeType.DerivedFrom,
            ValidFrom = DateTime.UtcNow,
        };
        var edge2 = new Edge
        {
            FromNodeId = "A",
            ToNodeId = "C",
            Type = EdgeType.DerivedFrom,
            ValidFrom = DateTime.UtcNow,
        };

        Assert.False(_resolver.HasConflict(edge1, edge2));
    }

    public ValueTask DisposeAsync() => _store.DisposeAsync();
}
