namespace SmartPipe.Memory.Health;

/// <summary>Type of a generated insight.</summary>
public enum InsightType
{
    /// <summary>A bottleneck is predicted.</summary>
    BottleneckPrediction,

    /// <summary>An anomaly was detected in metrics.</summary>
    AnomalyDetected,

    /// <summary>A cluster of related nodes was discovered.</summary>
    ClusterDiscovered,

    /// <summary>Node health is degrading.</summary>
    HealthDegradation,

    /// <summary>Retry budget exhausted for a node.</summary>
    RetryBudgetExhausted,
}
