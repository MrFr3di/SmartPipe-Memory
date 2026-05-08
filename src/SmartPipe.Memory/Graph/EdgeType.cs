namespace SmartPipe.Memory.Graph;

/// <summary>
/// Types of edges in the graph.
/// </summary>
public enum EdgeType
{
    /// <summary>Target was derived from source.</summary>
    DerivedFrom,

    /// <summary>Target is a duplicate of source.</summary>
    DuplicateOf,

    /// <summary>Target is a version of source.</summary>
    VersionOf,

    /// <summary>Target was aggregated from source.</summary>
    AggregatedFrom,

    /// <summary>Target was filtered from source.</summary>
    FilteredFrom,

    /// <summary>Pipeline component feeds into another component.</summary>
    FeedsInto
}