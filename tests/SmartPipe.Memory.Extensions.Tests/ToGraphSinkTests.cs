using SmartPipe.Core;
using SmartPipe.Memory.Extensions;
using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Extensions.Tests;

public sealed class ToGraphSinkTests : IAsyncDisposable
{
    private readonly InMemoryGraphStore _store;

    public ToGraphSinkTests()
    {
        _store = new InMemoryGraphStore();
    }

    [Fact]
    public async Task ToGraphSink_WritesSuccessResult()
    {
        var sink = _store.ToGraphSink<TestEntity>(entity => new Node
        {
            Id = entity.Id,
            Type = "File",
            Label = entity.Name,
            Properties = new Dictionary<string, object> { ["size"] = entity.Size }
        });

        await sink.InitializeAsync(CancellationToken.None);

        var entity = new TestEntity { Id = "e1", Name = "doc.pdf", Size = 2048 };
        var result = ProcessingResult<TestEntity>.Success(entity, 1);

        await sink.WriteAsync(result, CancellationToken.None);

        var node = await _store.GetNodeAsync("e1");
        Assert.NotNull(node);
        Assert.Equal("doc.pdf", node!.Label);
        Assert.Equal("File", node.Type);
        Assert.Equal(2048L, (long)node.Properties["size"]);
    }

    [Fact]
    public async Task ToGraphSink_SkipsFailedResult()
    {
        var sink = _store.ToGraphSink<TestEntity>(entity => new Node
        {
            Id = entity.Id,
            Type = "File",
            Label = entity.Name
        });

        await sink.InitializeAsync(CancellationToken.None);

        var error = new SmartPipeError("Test error", ErrorType.Permanent, "Test");
        var result = ProcessingResult<TestEntity>.Failure(error, 1);

        await sink.WriteAsync(result, CancellationToken.None);

        var node = await _store.GetNodeAsync("e1");
        Assert.Null(node);
    }

    [Fact]
    public async Task ToGraphSink_HandlesMultipleWrites()
    {
        var sink = _store.ToGraphSink<TestEntity>(entity => new Node
        {
            Id = entity.Id,
            Type = "File",
            Label = entity.Name
        });

        await sink.InitializeAsync(CancellationToken.None);

        for (var i = 0; i < 100; i++)
        {
            var entity = new TestEntity { Id = $"e{i}", Name = $"file_{i}.txt" };
            var result = ProcessingResult<TestEntity>.Success(entity, (ulong)i);
            await sink.WriteAsync(result, CancellationToken.None);
        }

        for (var i = 0; i < 100; i++)
        {
            var node = await _store.GetNodeAsync($"e{i}");
            Assert.NotNull(node);
        }
    }

    private sealed class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public long Size { get; set; }
    }

    public ValueTask DisposeAsync() => _store.DisposeAsync();
}