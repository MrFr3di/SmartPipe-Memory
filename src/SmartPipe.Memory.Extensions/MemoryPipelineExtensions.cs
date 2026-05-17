using System.Runtime.CompilerServices;
using System.Threading.Channels;
using SmartPipe.Core;
using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Health.Analysis;
using SmartPipe.Memory.Health.Infrastructure;
using SmartPipe.Memory.Query;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Extensions;

/// <summary>
/// Extension methods for integrating SmartPipe.Memory with SmartPipe.Core pipelines.
/// </summary>
public static class MemoryPipelineExtensions
{
    /// <summary>
    /// Connect a SmartPipe.Memory graph store to a pipeline.
    /// Automatically registers pipeline topology, streams metrics,
    /// and starts the metrics background consumer for health analysis.
    /// </summary>
    /// <typeparam name="TInput">Pipeline input type.</typeparam>
    /// <typeparam name="TOutput">Pipeline output type.</typeparam>
    /// <param name="pipeline">The pipeline to connect.</param>
    /// <param name="store">The graph store to write to.</param>
    /// <returns>The pipeline for chaining.</returns>
    public static SmartPipeChannel<TInput, TOutput> UseMemory<TInput, TOutput>(
        this SmartPipeChannel<TInput, TOutput> pipeline,
        IGraphStore store
    )
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(store);

        // Start metrics background consumer
        var metricsChannel = Channel.CreateBounded<MetricsEntry>(
            new BoundedChannelOptions(10000) { FullMode = BoundedChannelFullMode.DropOldest }
        );

        var calculator = new HealthVectorCalculator(store);
        var consumer = new MetricsBackgroundConsumer(metricsChannel.Reader, store, calculator);
        consumer.StartAsync();

        // Register pipeline topology when running starts
        pipeline.OnStateChanged += async (oldState, newState) =>
        {
            if (newState == PipelineState.Running)
            {
                await RegisterPipelineTopologyAsync(pipeline, store);
            }
        };

        // Stream metrics to the store
        pipeline.Options.OnMetrics = metrics =>
        {
            var entry = new MetricsEntry
            {
                NodeId = "pipeline",
                Timestamp = DateTime.UtcNow,
                Values = new Dictionary<string, double>
                {
                    ["ItemsProcessed"] = metrics.ItemsProcessed,
                    ["ItemsFailed"] = metrics.ItemsFailed,
                    ["AvgLatencyMs"] = metrics.AvgLatencyMs,
                    ["SmoothThroughput"] = metrics.SmoothThroughput,
                },
            };

            metricsChannel.Writer.TryWrite(entry);
            store.MetricsChannel.TryWrite(entry);
        };

        // Stop consumer when pipeline stops
        pipeline.OnStateChanged += (oldState, newState) =>
        {
            if (
                newState
                is PipelineState.Completed
                    or PipelineState.Faulted
                    or PipelineState.Cancelled
            )
            {
                metricsChannel.Writer.Complete();
                consumer.StopAsync();
            }
        };

        return pipeline;
    }

    /// <summary>
    /// Create a source that reads nodes from the graph store.
    /// </summary>
    /// <param name="store">The graph store to read from.</param>
    /// <param name="nodeType">Filter by node type (e.g., "File").</param>
    /// <returns>A source of nodes for use in a pipeline.</returns>
    public static ISource<Node> AsGraphSource(this IGraphStore store, string? nodeType = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        return new GraphSource(store, nodeType);
    }

    /// <summary>
    /// Create a sink that writes pipeline results to the graph store.
    /// </summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="store">The graph store to write to.</param>
    /// <param name="nodeFactory">Function to convert a result to a node.</param>
    /// <returns>A sink for use in a pipeline.</returns>
    public static ISink<T> ToGraphSink<T>(this IGraphStore store, Func<T, Node> nodeFactory)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(nodeFactory);
        return new GraphSink<T>(store, nodeFactory);
    }

    /// <summary>
    /// Create a transformer that converts pipeline elements to edges.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="store">The graph store to write edges to.</param>
    /// <param name="edgeFactory">Function to convert an element to an edge.</param>
    /// <returns>A transformer for use in a pipeline.</returns>
    public static ITransformer<T, T> TransformToEdges<T>(
        this IGraphStore store,
        Func<T, Edge> edgeFactory
    )
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(edgeFactory);
        return new EdgeTransformer<T>(store, edgeFactory);
    }

    private static async Task RegisterPipelineTopologyAsync<TInput, TOutput>(
        SmartPipeChannel<TInput, TOutput> pipeline,
        IGraphStore store
    )
    {
        var pipelineId = $"pipeline_{typeof(TInput).Name}_{typeof(TOutput).Name}";

        var pipelineNode = new Node
        {
            Id = pipelineId,
            Type = "Pipeline",
            Label = $"Pipeline<{typeof(TInput).Name}, {typeof(TOutput).Name}>",
        };

        await store.UpsertNodeAsync(pipelineNode);
    }

    // -- Internal implementations --

    private sealed class GraphSource : ISource<Node>
    {
        private readonly IGraphStore _store;
        private readonly string? _nodeType;

        public GraphSource(IGraphStore store, string? nodeType)
        {
            _store = store;
            _nodeType = nodeType;
        }

        public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;

        public async IAsyncEnumerable<ProcessingContext<Node>> ReadAsync(
            [EnumeratorCancellation] CancellationToken ct
        )
        {
            var query = new Model.MemoryQuery
            {
                NodeType = _nodeType,
                Type = Model.QueryType.FindNodes,
            };

            await foreach (var node in _store.QueryNodesAsync(query, ct))
            {
                yield return new ProcessingContext<Node>(node);
            }
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }

    private sealed class GraphSink<T> : ISink<T>
    {
        private readonly IGraphStore _store;
        private readonly Func<T, Node> _nodeFactory;

        public GraphSink(IGraphStore store, Func<T, Node> nodeFactory)
        {
            _store = store;
            _nodeFactory = nodeFactory;
        }

        public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;

        public async Task WriteAsync(ProcessingResult<T> result, CancellationToken ct)
        {
            if (result.IsSuccess && result.Value is not null)
            {
                var node = _nodeFactory(result.Value);
                await _store.UpsertNodeAsync(node, ct);
            }
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }

    private sealed class EdgeTransformer<T> : ITransformer<T, T>
    {
        private readonly IGraphStore _store;
        private readonly Func<T, Edge> _edgeFactory;

        public EdgeTransformer(IGraphStore store, Func<T, Edge> edgeFactory)
        {
            _store = store;
            _edgeFactory = edgeFactory;
        }

        public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;

        public async ValueTask<ProcessingResult<T>> TransformAsync(
            ProcessingContext<T> ctx,
            CancellationToken ct
        )
        {
            var edge = _edgeFactory(ctx.Payload);
            await _store.UpsertEdgeAsync(edge, ct);
            return ProcessingResult<T>.Success(ctx.Payload, ctx.TraceId);
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }
}
