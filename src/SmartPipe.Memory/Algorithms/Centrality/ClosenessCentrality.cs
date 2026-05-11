namespace SmartPipe.Memory.Algorithms.Centrality;

/// <summary>
/// Closeness centrality — measures how close a node is to all other nodes.
/// Higher closeness indicates a node that can quickly reach all others.
/// </summary>
public sealed class ClosenessCentrality
{
    /// <summary>
    /// Compute closeness centrality for all nodes in the graph.
    /// </summary>
    /// <param name="nodes">All nodes in the graph.</param>
    /// <param name="outEdges">Outgoing edges keyed by source node id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Node id to closeness score (0..1).</returns>
    public IReadOnlyDictionary<string, double> Compute(
        IReadOnlyDictionary<string, Graph.Node> nodes,
        IReadOnlyDictionary<string, IReadOnlyList<Graph.Edge>> outEdges,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(outEdges);

        var closeness = new Dictionary<string, double>();
        var nodeCount = nodes.Count;

        if (nodeCount <= 1)
        {
            foreach (var nodeId in nodes.Keys)
                closeness[nodeId] = nodeCount == 1 ? 1.0 : 0.0;
            return closeness;
        }

        foreach (var sourceId in nodes.Keys)
        {
            ct.ThrowIfCancellationRequested();

            var distances = BfsDistances(nodes, outEdges, sourceId, ct);
            var sum = distances.Values.Where(d => d > 0).Sum(d => (double)d);

            if (sum > 0)
                closeness[sourceId] = (nodeCount - 1) / sum;
            else
                closeness[sourceId] = 0.0;
        }

        return closeness;
    }

    /// <summary>
    /// Compute closeness centrality for a subset of nodes.
    /// </summary>
    public IReadOnlyDictionary<string, double> ComputeForSubset(
        IReadOnlyDictionary<string, Graph.Node> nodes,
        IReadOnlyDictionary<string, IReadOnlyList<Graph.Edge>> outEdges,
        IEnumerable<string> subsetIds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(outEdges);
        ArgumentNullException.ThrowIfNull(subsetIds);

        var closeness = new Dictionary<string, double>();
        var nodeCount = nodes.Count;

        if (nodeCount <= 1)
        {
            foreach (var nodeId in subsetIds)
                closeness[nodeId] = nodeCount == 1 ? 1.0 : 0.0;
            return closeness;
        }

        foreach (var sourceId in subsetIds)
        {
            ct.ThrowIfCancellationRequested();

            var distances = BfsDistances(nodes, outEdges, sourceId, ct);
            var sum = distances.Values.Where(d => d > 0).Sum(d => (double)d);

            closeness[sourceId] = sum > 0 ? (nodeCount - 1) / sum : 0.0;
        }

        return closeness;
    }

    private static Dictionary<string, int> BfsDistances(
        IReadOnlyDictionary<string, Graph.Node> nodes,
        IReadOnlyDictionary<string, IReadOnlyList<Graph.Edge>> outEdges,
        string sourceId,
        CancellationToken ct)
    {
        var distances = new Dictionary<string, int>();
        var queue = new Queue<string>();

        foreach (var nodeId in nodes.Keys)
            distances[nodeId] = -1;

        distances[sourceId] = 0;
        queue.Enqueue(sourceId);

        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            var current = queue.Dequeue();

            if (!outEdges.TryGetValue(current, out var outgoing))
                continue;

            foreach (var edge in outgoing)
            {
                if (distances[edge.ToNodeId] >= 0)
                    continue;

                distances[edge.ToNodeId] = distances[current] + 1;
                queue.Enqueue(edge.ToNodeId);
            }
        }

        return distances;
    }
}