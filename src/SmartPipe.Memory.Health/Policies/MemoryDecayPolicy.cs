namespace SmartPipe.Memory.Health;

/// <summary>
/// Policy for edge weight decay over time.
/// Full implementation in v0.2.0.
/// </summary>
public sealed class MemoryDecayPolicy
{
    /// <summary>Half-life for edge weight decay. Default: 30 days.</summary>
    public TimeSpan HalfLife { get; init; } = TimeSpan.FromDays(30);

    /// <summary>Minimum weight threshold. Default: 0.1.</summary>
    public double MinWeight { get; init; } = 0.1;
}