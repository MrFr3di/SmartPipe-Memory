using SmartPipe.Core;
using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Health.Analysis;

/// <summary>
/// Computes HealthVector for a node from buffered metrics and graph state.
/// Uses AdaptiveMetrics, ExponentialHistogram, and CircuitBreaker metrics from SmartPipe.Core.
/// </summary>
public sealed class HealthVectorCalculator
{
    private readonly IGraphStore _store;
    private readonly IClock _clock;

    /// <summary>
    /// Create a new HealthVectorCalculator.
    /// </summary>
    /// <param name="store">Graph store for retrieving node state.</param>
    /// <param name="clock">Clock for time calculations.</param>
    public HealthVectorCalculator(IGraphStore store, IClock? clock = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? new TimeProviderClock();
    }

    /// <summary>
    /// Compute a HealthVector for a node from its metric history.
    /// </summary>
    /// <param name="nodeId">Node identifier.</param>
    /// <param name="metricHistory">Ordered metric history for this node.</param>
    /// <param name="retryBudget">Current retry budget.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A computed HealthVector.</returns>
    public async Task<HealthVector> ComputeAsync(
        string nodeId,
        IReadOnlyList<MetricsEntry> metricHistory,
        int retryBudget = 3,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);
        ArgumentNullException.ThrowIfNull(metricHistory);

        // Compute latency percentiles via ExponentialHistogram
        var latencyHistogram = new ExponentialHistogram();
        var adaptiveMetrics = new AdaptiveMetrics();

        double totalLatency = 0;
        double totalThroughput = 0;
        double failureProbability = 0;

        foreach (var entry in metricHistory)
        {
            if (entry.Values.TryGetValue("AvgLatencyMs", out var latency))
            {
                latencyHistogram.Record(latency);
                adaptiveMetrics.Update(latency);
                totalLatency += latency;
            }

            if (entry.Values.TryGetValue("SmoothThroughput", out var throughput))
                totalThroughput += throughput;

            if (entry.Values.TryGetValue("ItemsFailed", out var failed))
                failureProbability += failed > 0 ? 1 : 0;
        }

        var count = metricHistory.Count;
        if (count == 0)
            return HealthVector.Create(0, 0, 0, 0, 0, 0, 0, retryBudget);

        failureProbability /= count;

        // Compute ResourceStrain from weakened edges
        var weakenedEdges = await _store.GetWeakenedEdgesFromAsync(nodeId, ct);
        var resourceStrain = Math.Min(weakenedEdges.Count / 100.0, 1.0);

        return HealthVector.Create(
            adaptiveMetrics.PredictNextLatency(),
            adaptiveMetrics.SmoothThroughputPerSec,
            failureProbability,
            resourceStrain,
            latencyHistogram.GetPercentile(50),
            latencyHistogram.GetPercentile(95),
            latencyHistogram.GetPercentile(99),
            retryBudget);
    }

    /// <summary>
    /// Compute a HealthVector from a single metrics snapshot.
    /// </summary>
    public Task<HealthVector> ComputeFromSnapshotAsync(
        string nodeId,
        MetricsEntry snapshot,
        int retryBudget = 3,
        CancellationToken ct = default)
    {
        return ComputeAsync(nodeId, new[] { snapshot }, retryBudget, ct);
    }
}