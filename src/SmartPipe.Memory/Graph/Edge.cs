namespace SmartPipe.Memory.Graph;

/// <summary>
/// Graph edge — represents a relationship between two nodes with full transformation history.
/// </summary>
public sealed class Edge
{
    /// <summary>Unique edge identifier. Immutable after creation.</summary>
    public long Id { get; init; }

    /// <summary>Source node identifier.</summary>
    public string FromNodeId { get; init; } = string.Empty;

    /// <summary>Target node identifier.</summary>
    public string ToNodeId { get; init; } = string.Empty;

    /// <summary>Edge type: DerivedFrom, DuplicateOf, VersionOf, AggregatedFrom, FilteredFrom, FeedsInto.</summary>
    public EdgeType Type { get; init; }

    /// <summary>Edge weight 0..1 (automatically decays via MemoryDecayPolicy).</summary>
    public double Weight { get; set; } = 1.0;

    /// <summary>Confidence in this edge 0..1 (1.0 = deterministic, &lt;0.7 = inference).</summary>
    public double Confidence { get; init; } = 1.0;

    /// <summary>Source of the edge: "LOG", "STATIC", "GENAI".</summary>
    public string SourceType { get; init; } = "LOG";

    /// <summary>Chain of transformation steps that led to this edge.</summary>
    public List<TransformationStep> Steps { get; init; } = new();

    /// <summary>Start of validity period (bitemporal).</summary>
    public DateTime ValidFrom { get; init; } = DateTime.UtcNow;

    /// <summary>End of validity period (null = currently valid).</summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>Transaction time (when the record entered the system). Immutable after creation.</summary>
    public DateTime TxTime { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// One step in the transformation chain.
/// </summary>
public sealed record TransformationStep(
    string TransformerName,
    DateTime ExecutedAt,
    TimeSpan Duration,
    Dictionary<string, string>? Metadata
);