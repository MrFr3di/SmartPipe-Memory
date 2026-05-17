namespace SmartPipe.Memory.Graph;

/// <summary>
/// Base class for predictive analytics insights.
/// In v0.1.0, this is a minimal placeholder.
/// Full implementation in SmartPipe.Memory.Health (v0.2.0).
/// </summary>
public sealed record Insight
{
    /// <summary>Unique insight identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Insight type as string (allows forward compatibility).</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Human-readable title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Detailed description.</summary>
    public string? Description { get; init; }

    /// <summary>Related node identifiers.</summary>
    public IReadOnlyList<string> RelatedNodeIds { get; init; } = [];

    /// <summary>Confidence 0..1.</summary>
    public double Confidence { get; init; }

    /// <summary>Severity: "Info", "Warning", "Critical".</summary>
    public string Severity { get; init; } = "Info";

    /// <summary>When the insight was generated.</summary>
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}
