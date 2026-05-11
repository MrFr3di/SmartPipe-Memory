using System.Collections.Concurrent;

namespace SmartPipe.Memory.Algorithms.Centrality;

/// <summary>
/// Betweenness centrality using Brandes' algorithm.
/// Identifies nodes that serve as bridges between different parts of the graph.
/// </summary>
public sealed class BetweennessCentrality
{
    /// <summary>
    /// Compute betweenness centrality for all nodes in the graph.
    /// </summary>
    /// <param name="nodes">All nodes in the graph.</param>
    /// <param name="outEdges">Outgoing edges keyed by source node id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Node id to betweenness score.</returns>
    public IReadOnlyDictionary<string, double> Compute(
        IReadOnlyDictionary<string, Graph.Node> nodes,
        ConcurrentDictionary<string, List<Graph.Edge>> outEdges,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(outEdges);

        var betweenness = new Dictionary<string, double>();
        foreach (var nodeId in nodes.Keys)
            betweenness[nodeId] = 0.0;

        foreach (var sourceId in nodes.Keys)
        {
            ct.ThrowIfCancellationRequested();
            ComputeSingleSource(nodes, outEdges, sourceId, betweenness, ct);
        }

        return betweenness;
    }

    private static void ComputeSingleSource(
        IReadOnlyDictionary<string, Graph.Node> nodes,
        ConcurrentDictionary<string, List<Graph.Edge>> outEdges,
        string sourceId,
        Dictionary<string, double> betweenness,
        CancellationToken ct)
    {
        var predecessors = new Dictionary<string, List<string>>();
        var distances = new Dictionary<string, int>();
        var sigma = new Dictionary<string, int>();
        var stack = new Stack<string>();
        var queue = new Queue<string>();

        foreach (var nodeId in nodes.Keys)
        {
            predecessors[nodeId] = new List<string>();
            distances[nodeId] = -1;
            sigma[nodeId] = 0;
        }

        distances[sourceId] = 0;
        sigma[sourceId] = 1;
        queue.Enqueue(sourceId);

        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            var current = queue.Dequeue();
            stack.Push(current);

            if (!outEdges.TryGetValue(current, out var outgoing))
                continue;

            foreach (var edge in outgoing)
            {
                if (distances[edge.ToNodeId] < 0)
                {
                    distances[edge.ToNodeId] = distances[current] + 1;
                    queue.Enqueue(edge.ToNodeId);
                }

                if (distances[edge.ToNodeId] == distances[current] + 1)
                {
                    sigma[edge.ToNodeId] += sigma[current];
                    predecessors[edge.ToNodeId].Add(current);
                }
            }
        }

        var delta = new Dictionary<string, double>();
        foreach (var nodeId in nodes.Keys)
            delta[nodeId] = 0.0;

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            foreach (var predecessor in predecessors[current])
            {
                var contribution = (double)sigma[predecessor] / sigma[current] * (1.0 + delta[current]);
                delta[predecessor] += contribution;
            }

            if (current != sourceId)
                betweenness[current] += delta[current];
        }
    }

    /// <summary>
    /// Compute betweenness centrality for a subset of nodes.
    /// </summary>
    /// <param name="nodes">All nodes in the graph.</param>
    /// <param name="outEdges">Outgoing edges keyed by source node id.</param>
    /// <param name="subsetIds">Node ids to compute centrality for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Node id to betweenness score.</returns>
    public IReadOnlyDictionary<string, double> ComputeForSubset(
        IReadOnlyDictionary<string, Graph.Node> nodes,
        ConcurrentDictionary<string, List<Graph.Edge>> outEdges,
        IEnumerable<string> subsetIds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(outEdges);
        ArgumentNullException.ThrowIfNull(subsetIds);

        var betweenness = new Dictionary<string, double>();
        foreach (var nodeId in nodes.Keys)
            betweenness[nodeId] = 0.0;

        foreach (var sourceId in subsetIds)
        {
            ct.ThrowIfCancellationRequested();
            ComputeSingleSource(nodes, outEdges, sourceId, betweenness, ct);
        }

        return betweenness;
    }
}