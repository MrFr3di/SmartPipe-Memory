using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Tests.Storage;

public sealed class SqliteWALStoreTests : IAsyncDisposable
{
    private readonly SqliteWALStore _store;
    private readonly string _dbPath;

    public SqliteWALStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_memory_{Guid.NewGuid()}.db");
        _store = new SqliteWALStore(_dbPath);
    }

    [Fact]
    public async Task InitializeAsync_CreatesTables()
    {
        await _store.InitializeAsync();

        Assert.Equal(StoreState.Running, _store.State);
    }

    [Fact]
    public async Task UpsertNode_PersistsToDisk()
    {
        await _store.InitializeAsync();

        await _store.UpsertNodeAsync(
            new Node
            {
                Id = "n1",
                Type = "File",
                Label = "test.txt",
            }
        );

        var node = await _store.GetNodeAsync("n1");
        Assert.NotNull(node);
        Assert.Equal("test.txt", node.Label);
    }

    [Fact]
    public async Task Data_SurvivesDispose()
    {
        await _store.InitializeAsync();
        await _store.UpsertNodeAsync(new Node { Id = "n1", Type = "File" });
        await _store.DisposeAsync();

        // Reopen
        var store2 = new SqliteWALStore(_dbPath);
        await store2.InitializeAsync();

        var node = await store2.GetNodeAsync("n1");
        Assert.NotNull(node);

        await store2.DisposeAsync();
    }

    [Fact]
    public async Task BatchUpsertNodes_InsertsAll()
    {
        await _store.InitializeAsync();

        var nodes = Enumerable.Range(0, 250).Select(i => new Node { Id = $"n{i}", Type = "File" });

        await _store.BatchUpsertNodesAsync(ToAsyncEnumerable(nodes));

        var node = await _store.GetNodeAsync("n249");
        Assert.NotNull(node);
    }

    [Fact]
    public async Task UpsertEdge_PersistsToDisk()
    {
        await _store.InitializeAsync();

        await _store.UpsertNodeAsync(new Node { Id = "n1", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "n2", Type = "File" });

        await _store.UpsertEdgeAsync(
            new Edge
            {
                FromNodeId = "n1",
                ToNodeId = "n2",
                Type = EdgeType.DuplicateOf,
            }
        );

        var path = await _store.FindPathAsync("n1", "n2", EdgeType.DuplicateOf.ToString(), 10);
        Assert.Equal(2, path.Count);
    }

    [Fact]
    public async Task DrainAsync_ChangesState()
    {
        await _store.InitializeAsync();
        Assert.Equal(StoreState.Running, _store.State);

        await _store.DrainAsync();
        Assert.Equal(StoreState.Drained, _store.State);
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            await Task.CompletedTask;
            yield return item;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _store.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
