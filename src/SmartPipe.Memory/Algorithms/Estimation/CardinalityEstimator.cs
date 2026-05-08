using SmartPipe.Core;

namespace SmartPipe.Memory.Algorithms.Estimation;

/// <summary>
/// Estimates the number of unique neighboring nodes using HyperLogLog.
/// Provides O(1) memory estimation with ~3% accuracy.
/// </summary>
public sealed class CardinalityEstimator
{
    private readonly HyperLogLogEstimator _hll;

    /// <summary>
    /// Create a new cardinality estimator.
    /// </summary>
    /// <param name="precision">HyperLogLog precision (4..16). Higher = more accurate, more memory. Default 12.</param>
    public CardinalityEstimator(int precision = 12)
    {
        _hll = new HyperLogLogEstimator(precision);
    }

    /// <summary>
    /// Add a neighboring node hash to the estimator.
    /// </summary>
    /// <param name="nodeId">Node identifier to add.</param>
    public void Add(string nodeId)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);
        var hash = (ulong)nodeId.GetHashCode();
        _hll.Add(hash);
    }

    /// <summary>
    /// Estimate the number of unique nodes added so far.
    /// </summary>
    public double Estimate() => _hll.Estimate();

    /// <summary>
    /// Estimate the number of unique neighbors for a given node.
    /// </summary>
    public double EstimateNeighbors(
        IReadOnlyDictionary<string, IReadOnlyList<Graph.Edge>> edges,
        string nodeId)
    {
        ArgumentNullException.ThrowIfNull(edges);
        ArgumentException.ThrowIfNullOrEmpty(nodeId);

        var estimator = new CardinalityEstimator();
        if (edges.TryGetValue(nodeId, out var outgoing))
        {
            foreach (var edge in outgoing)
                estimator.Add(edge.ToNodeId);
        }

        return estimator.Estimate();
    }
}