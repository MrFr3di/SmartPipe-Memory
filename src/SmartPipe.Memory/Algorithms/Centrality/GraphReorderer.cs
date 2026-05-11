namespace SmartPipe.Memory.Algorithms.Centrality;

/// <summary>
/// Reorders nodes for better cache locality during graph traversals.
/// Groups nodes by community (from Leiden clustering) or by degree (hubs first).
/// </summary>
public sealed class GraphReorderer
{
    /// <summary>
    /// Reorder nodes by community. Nodes in the same cluster are placed together.
    /// </summary>
    /// <param name="nodes">All nodes in the graph.</param>
    /// <param name="clusters">Clusters from Leiden clustering.</param>
    /// <returns>Reordered list of nodes.</returns>
    public IReadOnlyList<Graph.Node> ReorderByCommunity(
        IReadOnlyList<Graph.Node> nodes,
        IReadOnlyList<Graph.Cluster> clusters)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(clusters);

        var clusterMap = new Dictionary<string, int>();
        foreach (var cluster in clusters)
        {
            foreach (var nodeId in cluster.NodeIds)
                clusterMap[nodeId] = int.Parse(cluster.Id);
        }

        return nodes
            .OrderBy(n => clusterMap.GetValueOrDefault(n.Id, int.MaxValue))
            .ThenByDescending(n => n.HealthScore)
            .ToList();
    }

    /// <summary>
    /// Reorder nodes by degree. Nodes with more connections are placed first.
    /// </summary>
    /// <param name="nodes">All nodes in the graph.</param>
    /// <param name="outEdges">Outgoing edges keyed by source node id.</param>
    /// <returns>Reordered list of nodes.</returns>
    public IReadOnlyList<Graph.Node> ReorderByDegree(
        IReadOnlyList<Graph.Node> nodes,
        IReadOnlyDictionary<string, IReadOnlyList<Graph.Edge>> outEdges)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(outEdges);

        return nodes
            .OrderByDescending(n => outEdges.TryGetValue(n.Id, out var edges) ? edges.Count : 0)
            .ToList();
    }

    /// <summary>
    /// Reorder nodes by accessibility. Frequently accessed nodes are placed first.
    /// </summary>
    /// <param name="nodes">All nodes in the graph.</param>
    /// <returns>Reordered list of nodes.</returns>
    public IReadOnlyList<Graph.Node> ReorderByAccessibility(IReadOnlyList<Graph.Node> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        return nodes
            .OrderByDescending(n => n.HealthScore)
            .ToList();
    }
}