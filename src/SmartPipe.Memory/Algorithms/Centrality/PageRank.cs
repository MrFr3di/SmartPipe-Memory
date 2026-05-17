using System.Collections.Concurrent;

namespace SmartPipe.Memory.Algorithms.Centrality;

/// <summary>
/// PageRank algorithm for computing node importance in a graph.
/// Higher PageRank indicates more important nodes.
/// </summary>
public sealed class PageRank
{
    private const double DefaultDamping = 0.85;
    private const double DefaultTolerance = 1e-6;
    private const int DefaultMaxIterations = 100;

    /// <summary>
    /// Compute PageRank for all nodes in the graph.
    /// </summary>
    /// <param name="nodes">All nodes in the graph.</param>
    /// <param name="outEdges">Outgoing edges keyed by source node id.</param>
    /// <param name="damping">Damping factor, usually 0.85.</param>
    /// <param name="tolerance">Convergence tolerance.</param>
    /// <param name="maxIterations">Maximum number of iterations.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Node id to PageRank score (0..1).</returns>
    public IReadOnlyDictionary<string, double> Compute(
        IReadOnlyDictionary<string, Graph.Node> nodes,
        IReadOnlyDictionary<string, IReadOnlyList<Graph.Edge>> outEdges,
        double damping = DefaultDamping,
        double tolerance = DefaultTolerance,
        int maxIterations = DefaultMaxIterations,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(outEdges);

        var nodeCount = nodes.Count;
        if (nodeCount == 0)
            return new ConcurrentDictionary<string, double>();

        var ranks = new ConcurrentDictionary<string, double>();
        var newRanks = new ConcurrentDictionary<string, double>();
        var startRank = 1.0 / nodeCount;
        var dampingFactor = (1.0 - damping) / nodeCount;

        foreach (var nodeId in nodes.Keys)
            ranks[nodeId] = startRank;

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            ct.ThrowIfCancellationRequested();

            double maxDiff = 0;

            foreach (var nodeId in nodes.Keys)
            {
                double sum = 0;

                foreach (var (_, edges) in outEdges)
                {
                    foreach (var edge in edges)
                    {
                        if (edge.ToNodeId == nodeId)
                        {
                            var outDegree = outEdges.TryGetValue(edge.FromNodeId, out var fromEdges)
                                ? fromEdges.Count
                                : 1;
                            sum += ranks.GetValueOrDefault(edge.FromNodeId, 0) / outDegree;
                        }
                    }
                }

                var newRank = dampingFactor + damping * sum;
                newRanks[nodeId] = newRank;

                var diff = Math.Abs(newRank - ranks.GetValueOrDefault(nodeId, 0));
                if (diff > maxDiff)
                    maxDiff = diff;
            }

            (ranks, newRanks) = (newRanks, ranks);

            if (maxDiff < tolerance)
                break;
        }

        return ranks;
    }

    /// <summary>
    /// Compute PageRank using ConcurrentDictionary for edges.
    /// </summary>
    public IReadOnlyDictionary<string, double> Compute(
        IReadOnlyDictionary<string, Graph.Node> nodes,
        ConcurrentDictionary<string, List<Graph.Edge>> outEdges,
        double damping = DefaultDamping,
        double tolerance = DefaultTolerance,
        int maxIterations = DefaultMaxIterations,
        CancellationToken ct = default
    )
    {
        var edgesAsReadOnly = outEdges.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<Graph.Edge>)kvp.Value
        );

        return Compute(nodes, edgesAsReadOnly, damping, tolerance, maxIterations, ct);
    }
}
