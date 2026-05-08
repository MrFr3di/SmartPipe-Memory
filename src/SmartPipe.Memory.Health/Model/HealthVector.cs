namespace SmartPipe.Memory.Health;

/// <summary>
/// Health vector for predictive analytics.
/// Full implementation in v0.2.0.
/// </summary>
public sealed record HealthVector
{
    /// <summary>Predicted latency in milliseconds.</summary>
    public double PredictedLatencyMs { get; init; }

    /// <summary>Smoothed throughput (items/sec).</summary>
    public double SmoothThroughput { get; init; }

    /// <summary>Failure probability 0..1.</summary>
    public double FailureProbability { get; init; }

    /// <summary>Resource strain 0..1.</summary>
    public double ResourceStrain { get; init; }

    /// <summary>50th percentile latency.</summary>
    public double P50LatencyMs { get; init; }

    /// <summary>95th percentile latency.</summary>
    public double P95LatencyMs { get; init; }

    /// <summary>99th percentile latency.</summary>
    public double P99LatencyMs { get; init; }

    /// <summary>Aggregated health score 0..1.</summary>
    public double HealthScore { get; init; } = 1.0;
}