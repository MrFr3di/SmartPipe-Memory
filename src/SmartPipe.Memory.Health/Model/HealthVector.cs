namespace SmartPipe.Memory.Health;

/// <summary>
/// Health vector for predictive analytics.
/// Contains all health metrics for a graph node,
/// computed from metrics streamed via OnMetrics callback.
/// </summary>
public sealed record HealthVector
{
    /// <summary>
    /// Predicted latency in milliseconds from Double EMA.
    /// </summary>
    public double PredictedLatencyMs { get; init; }

    /// <summary>
    /// Smoothed throughput in items per second.
    /// </summary>
    public double SmoothThroughput { get; init; }

    /// <summary>
    /// Failure probability 0..1 from CircuitBreaker EWMA.
    /// </summary>
    public double FailureProbability { get; init; }

    /// <summary>
    /// Resource strain 0..1. Increases with many weak connections.
    /// </summary>
    public double ResourceStrain { get; init; }

    /// <summary>
    /// 50th percentile latency in milliseconds.
    /// </summary>
    public double P50LatencyMs { get; init; }

    /// <summary>
    /// 95th percentile latency in milliseconds.
    /// </summary>
    public double P95LatencyMs { get; init; }

    /// <summary>
    /// 99th percentile latency in milliseconds.
    /// </summary>
    public double P99LatencyMs { get; init; }

    /// <summary>
    /// Aggregated health score 0..1. 1 = perfect health, 0 = dead.
    /// </summary>
    public double HealthScore { get; init; } = 1.0;

    /// <summary>
    /// Remaining retry budget for this node.
    /// </summary>
    public int RetryBudget { get; init; } = 3;

    /// <summary>
    /// Create a HealthVector from individual metric values using default weights.
    /// Default weights: FailureProbability = 0.35, Latency = 0.35, ResourceStrain = 0.30.
    /// </summary>
    public static HealthVector Create(
        double predictedLatencyMs,
        double smoothThroughput,
        double failureProbability,
        double resourceStrain,
        double p50LatencyMs,
        double p95LatencyMs,
        double p99LatencyMs,
        int retryBudget = 3)
    {
        return CreateWithWeights(
            predictedLatencyMs, smoothThroughput, failureProbability, resourceStrain,
            p50LatencyMs, p95LatencyMs, p99LatencyMs, retryBudget,
            failureWeight: 0.35, latencyWeight: 0.35, strainWeight: 0.30);
    }

    /// <summary>
    /// Create a HealthVector with custom weights for the health score formula.
    /// Formula: 1.0 - (failureWeight * FailureProbability + latencyWeight * LatencyComponent + strainWeight * ResourceStrain)
    /// where LatencyComponent = min(PredictedLatencyMs / 1000, 1.0).
    /// Weights must sum to 1.0.
    /// </summary>
    public static HealthVector CreateWithWeights(
        double predictedLatencyMs,
        double smoothThroughput,
        double failureProbability,
        double resourceStrain,
        double p50LatencyMs,
        double p95LatencyMs,
        double p99LatencyMs,
        int retryBudget,
        double failureWeight,
        double latencyWeight,
        double strainWeight)
    {
        if (Math.Abs(failureWeight + latencyWeight + strainWeight - 1.0) > 1e-6)
            throw new ArgumentException("Weights must sum to 1.0");

        var latencyComponent = Math.Min(predictedLatencyMs / 1000.0, 1.0);
        var healthScore = 1.0 - (failureWeight * failureProbability + latencyWeight * latencyComponent + strainWeight * resourceStrain);

        return new HealthVector
        {
            PredictedLatencyMs = predictedLatencyMs,
            SmoothThroughput = smoothThroughput,
            FailureProbability = failureProbability,
            ResourceStrain = resourceStrain,
            P50LatencyMs = p50LatencyMs,
            P95LatencyMs = p95LatencyMs,
            P99LatencyMs = p99LatencyMs,
            HealthScore = Math.Clamp(healthScore, 0.0, 1.0),
            RetryBudget = retryBudget
        };
    }
}