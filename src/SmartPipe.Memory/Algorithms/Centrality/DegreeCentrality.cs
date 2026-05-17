namespace SmartPipe.Memory.Algorithms.Centrality;

/// <summary>
/// Degree centrality — counts the number of direct connections of a node.
/// High degree centrality indicates a node with many relationships.
/// </summary>
public sealed class DegreeCentrality
{
    /// <summary>
    /// Compute the degree centrality for a node.
    /// </summary>
    /// <param name="edges">All edges in the graph, keyed by source node id.</param>
    /// <param name="nodeId">Node identifier to compute centrality for.</param>
    /// <returns>Number of outgoing edges from the node.</returns>
    public int Compute(IReadOnlyDictionary<string, IReadOnlyList<Graph.Edge>> edges, string nodeId)
    {
        ArgumentNullException.ThrowIfNull(edges);
        ArgumentException.ThrowIfNullOrEmpty(nodeId);

        if (edges.TryGetValue(nodeId, out var outgoing))
            return outgoing.Count;

        return 0;
    }
}
