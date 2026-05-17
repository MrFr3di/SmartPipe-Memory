using SmartPipe.Memory.Extensions;
using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Extensions.Tests;

public sealed class AsGraphSourceTests : IAsyncDisposable
{
    private readonly InMemoryGraphStore _store;

    public AsGraphSourceTests()
    {
        _store = new InMemoryGraphStore();
    }

    [Fact]
    public async Task AsGraphSource_EmptyStore_ReturnsEmpty()
    {
        var source = _store.AsGraphSource("File");
        await source.InitializeAsync(CancellationToken.None);

        var nodes = new List<Node>();
        await foreach (var ctx in source.ReadAsync(CancellationToken.None))
            nodes.Add(ctx.Payload);

        Assert.Empty(nodes);
    }

    [Fact]
    public async Task AsGraphSource_FiltersByType()
    {
        await _store.UpsertNodeAsync(new Node { Id = "f1", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "t1", Type = "Transformer" });

        var source = _store.AsGraphSource("File");
        await source.InitializeAsync(CancellationToken.None);

        var nodes = new List<Node>();
        await foreach (var ctx in source.ReadAsync(CancellationToken.None))
            nodes.Add(ctx.Payload);

        Assert.Single(nodes);
        Assert.Equal("f1", nodes[0].Id);
    }

    [Fact]
    public async Task AsGraphSource_Cancellation_StopsReading()
    {
        for (var i = 0; i < 100_000; i++)
            await _store.UpsertNodeAsync(new Node { Id = $"n{i}", Type = "File" });

        var source = _store.AsGraphSource("File");
        await source.InitializeAsync(CancellationToken.None);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(1);

        var count = 0;
        try
        {
            await foreach (var ctx in source.ReadAsync(cts.Token))
                count++;
        }
        catch (OperationCanceledException) { }

        Assert.True(count < 100_000);
    }

    public ValueTask DisposeAsync() => _store.DisposeAsync();
}
