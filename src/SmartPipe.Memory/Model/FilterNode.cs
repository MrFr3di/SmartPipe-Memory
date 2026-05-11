namespace SmartPipe.Memory.Model;

/// <summary>
/// Filter tree for graph queries.
/// Changed to abstract class for AOT compatibility in v0.1.1.
/// </summary>
public abstract class FilterNode
{
    /// <summary>
    /// Filter by a numeric property of a node.
    /// </summary>
    public sealed class PropertyFilter : FilterNode
    {
        /// <summary>Property name: "HealthScore", "FailureProb", "ResourceStrain", "PredictedLatencyMs".</summary>
        public string Property { get; init; } = string.Empty;

        /// <summary>Comparison operator.</summary>
        public FilterOperator Operator { get; init; }

        /// <summary>Value to compare against.</summary>
        public double Value { get; init; }
    }

    /// <summary>
    /// Logical AND combinator.
    /// </summary>
    public sealed class And : FilterNode
    {
        /// <summary>Left filter operand.</summary>
        public FilterNode Left { get; }

        /// <summary>Right filter operand.</summary>
        public FilterNode Right { get; }

        /// <summary>Creates a new AND filter.</summary>
        public And(FilterNode left, FilterNode right)
        {
            Left = left ?? throw new ArgumentNullException(nameof(left));
            Right = right ?? throw new ArgumentNullException(nameof(right));
        }
    }

    /// <summary>
    /// Logical OR combinator.
    /// </summary>
    public sealed class Or : FilterNode
    {
        /// <summary>Left filter operand.</summary>
        public FilterNode Left { get; }

        /// <summary>Right filter operand.</summary>
        public FilterNode Right { get; }

        /// <summary>Creates a new OR filter.</summary>
        public Or(FilterNode left, FilterNode right)
        {
            Left = left ?? throw new ArgumentNullException(nameof(left));
            Right = right ?? throw new ArgumentNullException(nameof(right));
        }
    }
}

/// <summary>
/// Comparison operators for property filters.
/// </summary>
public enum FilterOperator
{
    /// <summary>Property is less than value.</summary>
    LessThan,

    /// <summary>Property is greater than value.</summary>
    GreaterThan,

    /// <summary>Property equals value.</summary>
    Equals
}