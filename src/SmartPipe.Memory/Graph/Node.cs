namespace SmartPipe.Memory.Graph;

/// <summary>
/// Graph node — the fundamental unit of graph memory.
/// Represents any object in the system: file, database record, data stream, pipeline component.
/// </summary>
public sealed class Node
{
    /// <summary>Unique node identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Node type: "File", "Record", "Stream", "Transformer", "Source", "Sink".</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Human-readable node name.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Metadata: hash, size, path, any user-defined data.</summary>
    public Dictionary<string, object> Properties { get; init; } = new();

    /// <summary>LineageContext: source, pipeline, enteredAt, transform.</summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>Vector embedding (reserved for hybrid search, v1.0.0).</summary>
    public float[]? Embedding { get; set; }

    /// <summary>Aggregated health score 0..1 (1 = perfect health).</summary>
    public double HealthScore { get; set; } = 1.0;

    /// <summary>Failure probability 0..1 (from CircuitBreaker EWMA).</summary>
    public double FailureProbability { get; set; }

    /// <summary>Predicted latency in milliseconds.</summary>
    public double PredictedLatencyMs { get; set; }

    /// <summary>Resource strain 0..1 (increases with many weak connections).</summary>
    public double ResourceStrain { get; set; }

    /// <summary>Start of validity period (bitemporal).</summary>
    public DateTime ValidFrom { get; init; } = DateTime.UtcNow;

    /// <summary>End of validity period (null = currently valid).</summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>Transaction time (when the record entered the system). Immutable after creation.</summary>
    public DateTime TxTime { get; init; } = DateTime.UtcNow;

    /// <summary>Version for optimistic concurrency control.</summary>
    public int Version { get; init; } = 1;
}