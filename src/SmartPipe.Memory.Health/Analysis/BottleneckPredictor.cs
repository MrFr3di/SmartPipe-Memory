using SmartPipe.Core;
using SmartPipe.Memory.Health.Analysis;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Health.Analysis;

/// <summary>
/// Predicts bottlenecks by comparing current HealthVector
/// with historical state using temporal comparison.
/// </summary>
public sealed class BottleneckPredictor
{
    private readonly HealthVectorCalculator _calculator;
    private readonly IGraphStore _store;
    private readonly IClock _clock;

    /// <summary>
    /// Latency threshold in milliseconds for bottleneck detection.
    /// </summary>
    public double LatencyThresholdMs { get; }

    /// <summary>
    /// Health score threshold for bottleneck detection.
    /// </summary>
    public double HealthScoreThreshold { get; }

    /// <summary>
    /// Create a new BottleneckPredictor.
    /// </summary>
    /// <param name="calculator">HealthVector calculator.</param>
    /// <param name="store">Graph store for retrieving historical metrics.</param>
    /// <param name="latencyThresholdMs">Latency threshold for bottleneck detection.</param>
    /// <param name="healthScoreThreshold">Health score threshold for bottleneck detection.</param>
    /// <param name="clock">Clock for time calculations.</param>
    public BottleneckPredictor(
        HealthVectorCalculator calculator,
        IGraphStore store,
        double latencyThresholdMs = 500,
        double healthScoreThreshold = 0.3,
        IClock? clock = null)
    {
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        LatencyThresholdMs = latencyThresholdMs;
        HealthScoreThreshold = healthScoreThreshold;
        _clock = clock ?? new TimeProviderClock();
    }

    /// <summary>
    /// Predict whether a node will become a bottleneck.
    /// Compares current HealthVector with historical state.
    /// </summary>
    /// <param name="nodeId">Node identifier.</param>
    /// <param name="currentMetrics">Current metrics snapshot.</param>
    /// <param name="historicalMetrics">Historical metrics for comparison.</param>
    /// <param name="historicalTimestamp">Timestamp of the historical metrics snapshot.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Prediction result with confidence and time estimate.</returns>
    public async Task<BottleneckPrediction> PredictAsync(
        string nodeId,
        MetricsEntry currentMetrics,
        IReadOnlyList<MetricsEntry> historicalMetrics,
        DateTime historicalTimestamp,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);

        var currentHealth = await _calculator.ComputeFromSnapshotAsync(nodeId, currentMetrics, ct: ct);
        var historicalHealth = await _calculator.ComputeAsync(nodeId, historicalMetrics, ct: ct);

        var healthDelta = historicalHealth.HealthScore - currentHealth.HealthScore;
        var latencyDelta = currentHealth.PredictedLatencyMs - historicalHealth.PredictedLatencyMs;

        var isBottleneck = currentHealth.PredictedLatencyMs > LatencyThresholdMs
            || currentHealth.HealthScore < HealthScoreThreshold;

        var confidence = ComputeConfidence(healthDelta, latencyDelta, currentHealth);
        var timeToImpact = ComputeTimeToImpact(currentHealth, historicalHealth, historicalTimestamp);

        return new BottleneckPrediction
        {
            NodeId = nodeId,
            IsBottleneck = isBottleneck,
            Confidence = confidence,
            TimeToImpactMs = timeToImpact,
            CurrentHealth = currentHealth,
            HistoricalHealth = historicalHealth,
            HealthDelta = healthDelta,
            LatencyDelta = latencyDelta
        };
    }

    private static double ComputeConfidence(
        double healthDelta,
        double latencyDelta,
        HealthVector currentHealth)
    {
        // Higher confidence if health is degrading AND latency is increasing
        var healthFactor = Math.Max(0, healthDelta);
        var latencyFactor = Math.Max(0, latencyDelta / 1000.0);
        var failureFactor = currentHealth.FailureProbability;

        return Math.Min(healthFactor * 0.4 + latencyFactor * 0.3 + failureFactor * 0.3, 1.0);
    }

    private double ComputeTimeToImpact(HealthVector current, HealthVector historical, DateTime historicalTimestamp)
    {
        if (current.PredictedLatencyMs <= historical.PredictedLatencyMs)
            return double.PositiveInfinity;

        var timeDeltaSeconds = (_clock.UtcNow - historicalTimestamp).TotalSeconds;
        if (timeDeltaSeconds <= 0)
            return double.PositiveInfinity;

        var latencyVelocity = (current.PredictedLatencyMs - historical.PredictedLatencyMs) / timeDeltaSeconds;
        if (latencyVelocity <= 0)
            return double.PositiveInfinity;

        return (LatencyThresholdMs - current.PredictedLatencyMs) / latencyVelocity * 1000;
    }
}

/// <summary>
/// Result of bottleneck prediction.
/// </summary>
public sealed record BottleneckPrediction
{
    /// <summary>Node identifier.</summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>Whether this node is predicted to become a bottleneck.</summary>
    public bool IsBottleneck { get; init; }

    /// <summary>Confidence in the prediction 0..1.</summary>
    public double Confidence { get; init; }

    /// <summary>Estimated time until impact in milliseconds.</summary>
    public double TimeToImpactMs { get; init; }

    /// <summary>Current health vector.</summary>
    public HealthVector? CurrentHealth { get; init; }

    /// <summary>Historical health vector for comparison.</summary>
    public HealthVector? HistoricalHealth { get; init; }

    /// <summary>Change in health score.</summary>
    public double HealthDelta { get; init; }

    /// <summary>Change in predicted latency.</summary>
    public double LatencyDelta { get; init; }
}