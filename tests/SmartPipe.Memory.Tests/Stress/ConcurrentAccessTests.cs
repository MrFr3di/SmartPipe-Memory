using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Tests.Stress;

public sealed class ConcurrentAccessTests : IAsyncDisposable
{
    private readonly InMemoryGraphStore _store;

    public ConcurrentAccessTests()
    {
        _store = new InMemoryGraphStore();
    }

    [Fact]
    public async Task Parallel_Reads_NoDeadlock()
    {
        for (var i = 0; i < 1000; i++)
            await _store.UpsertNodeAsync(new Node { Id = $"n{i}", Type = "File" });

        var tasks = Enumerable.Range(0, 100).Select(async _ =>
        {
            for (var i = 0; i < 1000; i++)
            {
                var node = await _store.GetNodeAsync($"n{i}");
                Assert.NotNull(node);
            }
        });

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task Parallel_Writes_NoDeadlock()
    {
        await Task.WhenAll(Enumerable.Range(0, 10).Select(async i =>
        {
            for (var j = 0; j < 100; j++)
            {
                await _store.UpsertNodeAsync(new Node { Id = $"n{i}_{j}", Type = "File" });
            }
        }));
    }

    [Fact]
    public async Task Write_WhileReading_NoDeadlock()
    {
        for (var i = 0; i < 1000; i++)
            await _store.UpsertNodeAsync(new Node { Id = $"n{i}", Type = "File" });

        var readTask = Task.Run(async () =>
        {
            var query = new Model.MemoryQuery { NodeType = "File", Type = Model.QueryType.FindNodes };
            var count = 0;
            await foreach (var _ in _store.QueryNodesAsync(query))
                count++;
            return count;
        });

        var writeTask = Task.Run(async () =>
        {
            for (var i = 1000; i < 1500; i++)
                await _store.UpsertNodeAsync(new Node { Id = $"n{i}", Type = "File" });
        });

        await Task.WhenAll(readTask, writeTask);
    }

    public ValueTask DisposeAsync() => _store.DisposeAsync();
}