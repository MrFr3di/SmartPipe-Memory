using SmartPipe.Core;

namespace SmartPipe.Memory.Health.Policies;

/// <summary>
/// Policy for edge weight decay over time.
/// Implements Ebbinghaus-like forgetting curve with adaptive decay
/// based on access frequency from DecaySignalPropagator.
/// </summary>
public sealed class MemoryDecayPolicy
{
    private readonly IClock _clock;

    /// <summary>
    /// Half-life for edge weight decay. Default: 30 days.
    /// </summary>
    public TimeSpan HalfLife { get; }

    /// <summary>
    /// Minimum weight threshold. Default: 0.1.
    /// </summary>
    public double MinWeight { get; }

    /// <summary>
    /// Create a new decay policy.
    /// </summary>
    /// <param name="halfLife">Half-life period for weight decay.</param>
    /// <param name="minWeight">Minimum weight threshold.</param>
    /// <param name="clock">Clock for time calculations.</param>
    public MemoryDecayPolicy(
        TimeSpan? halfLife = null,
        double minWeight = 0.1,
        IClock? clock = null)
    {
        HalfLife = halfLife ?? TimeSpan.FromDays(30);
        MinWeight = minWeight;
        _clock = clock ?? new TimeProviderClock();
    }

    /// <summary>
    /// Compute the decayed weight of an edge at the current time.
    /// Formula: Weight * 0.5^(age / HalfLife), clamped to MinWeight.
    /// </summary>
    /// <param name="initialWeight">Initial edge weight.</param>
    /// <param name="establishedAt">When the edge was established.</param>
    /// <param name="accessCount">Number of times the edge was accessed (slows decay).</param>
    /// <returns>Decayed weight.</returns>
    public double ComputeStrength(
        double initialWeight,
        DateTime establishedAt,
        int accessCount = 0)
    {
        var age = _clock.UtcNow - establishedAt;

        // Adaptive half-life: frequently accessed edges decay slower
        var effectiveHalfLife = HalfLife.TotalSeconds * (1.0 + accessCount * 0.1);

        var decay = Math.Pow(0.5, age.TotalSeconds / effectiveHalfLife);
        return Math.Max(initialWeight * decay, MinWeight);
    }

    /// <summary>
    /// Compute the decayed weight for an edge.
    /// </summary>
    public double ComputeStrength(Graph.Edge edge, int accessCount = 0)
    {
        return ComputeStrength(edge.Weight, edge.ValidFrom, accessCount);
    }

    /// <summary>
    /// Check if an edge is weakened (weight below 0.3).
    /// </summary>
    public bool IsWeakened(Graph.Edge edge, int accessCount = 0)
    {
        return ComputeStrength(edge, accessCount) < 0.3;
    }
}