namespace SmartPipe.Memory.Model;

/// <summary>
/// Filter tree for graph queries.
/// v0.1.0: PropertyFilter only.
/// v0.2.0: And/Or combinators.
/// </summary>
public abstract record FilterNode
{
    /// <summary>Filter by a numeric property of a node.</summary>
    public sealed record PropertyFilter : FilterNode
    {
        /// <summary>Property name: "HealthScore", "FailureProb", "ResourceStrain", "PredictedLatencyMs".</summary>
        public string Property { get; init; } = string.Empty;

        /// <summary>Comparison operator.</summary>
        public FilterOperator Operator { get; init; }

        /// <summary>Value to compare against.</summary>
        public double Value { get; init; }
    }

    /// <summary>Logical AND combinator (v0.2.0).</summary>
    public sealed record And(FilterNode Left, FilterNode Right) : FilterNode;

    /// <summary>Logical OR combinator (v0.2.0).</summary>
    public sealed record Or(FilterNode Left, FilterNode Right) : FilterNode;
}

/// <summary>
/// Comparison operators for property filters.
/// </summary>
public enum FilterOperator
{
    LessThan,
    GreaterThan,
    Equals
}