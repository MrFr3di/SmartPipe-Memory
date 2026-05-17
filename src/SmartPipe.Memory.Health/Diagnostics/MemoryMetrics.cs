using System.Diagnostics.Metrics;

namespace SmartPipe.Memory.Health;

/// <summary>
/// Health-specific metrics.
/// Full implementation in v0.2.0.
/// </summary>
public sealed class HealthMetrics
{
    private static readonly Meter _meter = new("SmartPipe.Memory.Health", "0.2.0");

    // Full implementation in v0.2.0
}
