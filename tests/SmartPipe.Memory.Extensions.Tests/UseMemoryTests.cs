using System.Runtime.CompilerServices;
using SmartPipe.Core;
using SmartPipe.Memory.Extensions;
using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Extensions.Tests;

public sealed class UseMemoryTests : IAsyncDisposable
{
    private readonly InMemoryGraphStore _store;

    public UseMemoryTests()
    {
        _store = new InMemoryGraphStore();
    }

    [Fact]
    public async Task UseMemory_RegistersPipelineTopology_OnRun()
    {
        var pipeline = new SmartPipeChannel<TestInput, TestOutput>(
            new SmartPipeChannelOptions { MaxDegreeOfParallelism = 1, ContinueOnError = true }
        );

        pipeline.AddSource(new TestSource(new TestInput { Data = "test" }));
        pipeline.AddTransformer(new TestTransformer());
        pipeline.AddSink(new TestSink());
        pipeline.UseMemory(_store);

        await pipeline.RunAsync();

        var pipelineNode = await _store.GetNodeAsync("pipeline_TestInput_TestOutput");
        Assert.NotNull(pipelineNode);
        Assert.Equal("Pipeline", pipelineNode!.Type);
    }

    [Fact]
    public async Task UseMemory_StreamsMetrics_ToStore()
    {
        var pipeline = new SmartPipeChannel<TestInput, TestOutput>(
            new SmartPipeChannelOptions { MaxDegreeOfParallelism = 1, ContinueOnError = true }
        );

        pipeline.AddSource(new TestSource(new TestInput { Data = "test" }));
        pipeline.AddTransformer(new TestTransformer());
        pipeline.AddSink(new TestSink());
        pipeline.UseMemory(_store);

        await pipeline.RunAsync();

        Assert.Equal(StoreState.Drained, _store.State);
    }

    [Fact]
    public void AsGraphSource_ReturnsNonNullSource()
    {
        var source = _store.AsGraphSource("File");
        Assert.NotNull(source);
    }

    [Fact]
    public async Task AsGraphSource_ReadsNodes_FromStore()
    {
        await _store.UpsertNodeAsync(new Node { Id = "f1", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "f2", Type = "File" });

        var source = _store.AsGraphSource("File");
        await source.InitializeAsync(CancellationToken.None);

        var nodes = new List<Node>();
        await foreach (var ctx in source.ReadAsync(CancellationToken.None))
            nodes.Add(ctx.Payload);

        Assert.Equal(2, nodes.Count);
        Assert.All(nodes, n => Assert.Equal("File", n.Type));
    }

    [Fact]
    public async Task ToGraphSink_WritesResults_ToStore()
    {
        var sink = _store.ToGraphSink<TestOutput>(result => new Node
        {
            Id = result.Id,
            Type = "File",
            Label = result.Name,
        });

        await sink.InitializeAsync(CancellationToken.None);

        var result = ProcessingResult<TestOutput>.Success(
            new TestOutput { Id = "n1", Name = "test.txt" },
            1
        );

        await sink.WriteAsync(result, CancellationToken.None);

        var node = await _store.GetNodeAsync("n1");
        Assert.NotNull(node);
        Assert.Equal("test.txt", node!.Label);
    }

    [Fact]
    public async Task TransformToEdges_CreatesEdges()
    {
        await _store.UpsertNodeAsync(new Node { Id = "n1", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "n2", Type = "File" });

        var transformer = _store.TransformToEdges<TestOutput>(item => new Edge
        {
            FromNodeId = "n1",
            ToNodeId = item.Id,
            Type = EdgeType.DerivedFrom,
        });

        await transformer.InitializeAsync(CancellationToken.None);

        var ctx = new ProcessingContext<TestOutput>(
            new TestOutput { Id = "n2", Name = "derived.txt" }
        );
        var result = await transformer.TransformAsync(ctx, CancellationToken.None);

        Assert.True(result.IsSuccess);

        var path = await _store.FindPathAsync("n1", "n2", EdgeType.DerivedFrom.ToString(), 10);
        Assert.Equal(2, path.Count);
    }

    private sealed class TestSource : ISource<TestInput>
    {
        private readonly TestInput[] _items;

        public TestSource(params TestInput[] items) => _items = items;

        public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;

        public async IAsyncEnumerable<ProcessingContext<TestInput>> ReadAsync(
            [EnumeratorCancellation] CancellationToken ct
        )
        {
            foreach (var item in _items)
            {
                yield return new ProcessingContext<TestInput>(item);
                await Task.CompletedTask;
            }
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }

    private sealed class TestTransformer : ITransformer<TestInput, TestOutput>
    {
        public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;

        public ValueTask<ProcessingResult<TestOutput>> TransformAsync(
            ProcessingContext<TestInput> ctx,
            CancellationToken ct
        )
        {
            var output = new TestOutput { Id = "out", Name = ctx.Payload.Data };
            return ValueTask.FromResult(ProcessingResult<TestOutput>.Success(output, ctx.TraceId));
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }

    private sealed class TestSink : ISink<TestOutput>
    {
        public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;

        public Task WriteAsync(ProcessingResult<TestOutput> result, CancellationToken ct) =>
            Task.CompletedTask;

        public Task DisposeAsync() => Task.CompletedTask;
    }

    public sealed class TestInput
    {
        public string Data { get; set; } = string.Empty;
    }

    public sealed class TestOutput
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public ValueTask DisposeAsync() => _store.DisposeAsync();
}
