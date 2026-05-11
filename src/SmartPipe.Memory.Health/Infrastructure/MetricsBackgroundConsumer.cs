using SmartPipe.Memory.Health.Analysis;
using SmartPipe.Memory.Storage;
using System.Threading.Channels;

namespace SmartPipe.Memory.Health.Infrastructure;

/// <summary>
/// Background service that consumes metrics from the MetricsChannel
/// and persists them to the graph store for health analysis.
/// </summary>
public sealed class MetricsBackgroundConsumer : IAsyncDisposable
{
    private readonly ChannelReader<MetricsEntry> _reader;
    private readonly IGraphStore _store;
    private readonly HealthVectorCalculator _calculator;
    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// Create a new MetricsBackgroundConsumer.
    /// </summary>
    /// <param name="reader">Channel reader for metrics entries.</param>
    /// <param name="store">Graph store for updating node health.</param>
    /// <param name="calculator">Health vector calculator.</param>
    public MetricsBackgroundConsumer(
        ChannelReader<MetricsEntry> reader,
        IGraphStore store,
        HealthVectorCalculator calculator)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
    }

    /// <summary>
    /// Start consuming metrics in the background.
    /// </summary>
    public Task StartAsync()
    {
        _ = Task.Run(() => ConsumeAsync(_cts.Token));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stop the consumer gracefully.
    /// </summary>
    public Task StopAsync()
    {
        _cts.Cancel();
        return Task.CompletedTask;
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
        var history = new Dictionary<string, List<MetricsEntry>>();

        await foreach (var entry in _reader.ReadAllAsync(ct))
        {
            if (!history.ContainsKey(entry.NodeId))
                history[entry.NodeId] = new List<MetricsEntry>();

            history[entry.NodeId].Add(entry);

            // Compute health vector for this node
            var health = await _calculator.ComputeAsync(
                entry.NodeId,
                history[entry.NodeId],
                ct: ct);

            // Get current version for optimistic concurrency
            var node = await _store.GetNodeAsync(entry.NodeId, ct);
            if (node is null) continue;

            await _store.UpdateNodeHealthAsync(
                entry.NodeId,
                health.HealthScore,
                health.FailureProbability,
                health.PredictedLatencyMs,
                health.ResourceStrain,
                node.Version,
                ct);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _cts.Dispose();
        return ValueTask.CompletedTask;
    }
}