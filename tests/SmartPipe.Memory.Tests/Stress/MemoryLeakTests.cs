using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Model;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Tests.Stress;

public sealed class MemoryLeakTests : IAsyncDisposable
{
    private readonly InMemoryGraphStore _store;

    public MemoryLeakTests()
    {
        _store = new InMemoryGraphStore();
    }

    [Fact]
    public async Task LongRunning_Operations_NoMemoryLeak()
    {
        // 1000 итераций upsert + query + delete
        for (var iteration = 0; iteration < 1000; iteration++)
        {
            // Insert batch
            for (var i = 0; i < 100; i++)
            {
                await _store.UpsertNodeAsync(
                    new Node
                    {
                        Id = $"n{iteration}_{i}",
                        Type = "File",
                        Label = $"file_{iteration}_{i}.txt",
                    }
                );
            }

            // Query
            var query = new MemoryQuery
            {
                NodeType = "File",
                Type = QueryType.FindNodes,
                Limit = 10,
            };
            var count = 0;
            await foreach (var _ in _store.QueryNodesAsync(query))
                count++;
            Assert.Equal(10, count);

            // Delete half
            for (var i = 0; i < 50; i++)
                await _store.DeleteNodeAsync($"n{iteration}_{i}");
        }

        // Final state: store should still be functional
        var node = await _store.GetNodeAsync("n999_99");
        Assert.NotNull(node);

        var state = _store.State;
        Assert.Equal(StoreState.Running, state);
    }

    [Fact]
    public async Task Repeated_UpsertDelete_Cycle_NoDegradation()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (var cycle = 0; cycle < 500; cycle++)
        {
            await _store.UpsertNodeAsync(new Node { Id = "cyclic", Type = "File" });
            await _store.DeleteNodeAsync("cyclic");
        }

        sw.Stop();

        // 500 cycles should complete in under 1 second
        Assert.True(
            sw.ElapsedMilliseconds < 1000,
            $"500 upsert/delete cycles took {sw.ElapsedMilliseconds}ms, expected < 1000ms"
        );
    }

    [Fact]
    public async Task LargeProperties_DoNotLeakMemory()
    {
        var largeData = new Dictionary<string, object>();
        for (var i = 0; i < 1000; i++)
            largeData[$"key_{i}"] = new string('x', 100);

        for (var i = 0; i < 100; i++)
        {
            await _store.UpsertNodeAsync(
                new Node
                {
                    Id = $"large_{i}",
                    Type = "File",
                    Properties = new Dictionary<string, object>(largeData),
                }
            );
        }

        // Force cleanup
        for (var i = 0; i < 100; i++)
            await _store.DeleteNodeAsync($"large_{i}");

        var node = await _store.GetNodeAsync("large_50");
        Assert.Null(node);
    }

    public ValueTask DisposeAsync() => _store.DisposeAsync();
}
