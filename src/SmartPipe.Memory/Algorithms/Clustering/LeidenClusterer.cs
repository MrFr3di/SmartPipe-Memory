namespace SmartPipe.Memory.Algorithms.Clustering;

/// <summary>
/// Leiden community detection algorithm.
/// Partitions a graph into communities by optimizing modularity.
/// Based on the Newman-Girvan quality function.
/// </summary>
public sealed class LeidenClusterer
{
    /// <summary>
    /// Quality function value for convergence checks.
    /// </summary>
    public double CurrentQuality { get; private set; }

    /// <summary>
    /// Run Leiden clustering on a graph.
    /// </summary>
    /// <param name="nodes">All nodes in the graph.</param>
    /// <param name="edges">All edges keyed by source node id.</param>
    /// <param name="maxIterations">Maximum number of outer iterations. Default 10.</param>
    /// <param name="minImprovement">Minimum quality improvement to continue. Default 0.001.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of clusters with their node members.</returns>
    public IReadOnlyList<Graph.Cluster> Cluster(
        IReadOnlyDictionary<string, Graph.Node> nodes,
        IReadOnlyDictionary<string, IReadOnlyList<Graph.Edge>> edges,
        int maxIterations = 10,
        double minImprovement = 0.001,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        var communities = InitializeCommunities(nodes);
        var totalWeight = ComputeTotalWeight(edges);

        CurrentQuality = ComputeModularity(communities, edges, totalWeight);

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            ct.ThrowIfCancellationRequested();

            var improved = LocalMovingPhase(communities, edges, totalWeight);
            if (!improved) break;

            var newQuality = ComputeModularity(communities, edges, totalWeight);

            if (newQuality - CurrentQuality < minImprovement) break;

            CurrentQuality = newQuality;
        }

        return BuildClusters(communities, nodes.Keys);
    }

    private static Dictionary<string, int> InitializeCommunities(
        IReadOnlyDictionary<string, Graph.Node> nodes)
    {
        var communities = new Dictionary<string, int>();
        var id = 0;

        foreach (var nodeId in nodes.Keys)
            communities[nodeId] = id++;

        return communities;
    }

    private static double ComputeTotalWeight(
        IReadOnlyDictionary<string, IReadOnlyList<Graph.Edge>> edges)
    {
        var total = 0.0;

        foreach (var (_, outgoing) in edges)
        {
            foreach (var edge in outgoing)
                total += edge.Weight;
        }

        return total;
    }

    private static bool LocalMovingPhase(
        Dictionary<string, int> communities,
        IReadOnlyDictionary<string, IReadOnlyList<Graph.Edge>> edges,
        double totalWeight)
    {
        var totalWeight2M = 2.0 * totalWeight;
        var improved = false;

        foreach (var (nodeId, currentCommunity) in communities)
        {
            var bestCommunity = currentCommunity;
            var bestDelta = 0.0;

            if (!edges.TryGetValue(nodeId, out var outgoing))
                continue;

            var neighborCommunities = new HashSet<int>();
            foreach (var edge in outgoing)
            {
                if (communities.TryGetValue(edge.ToNodeId, out var targetCommunity))
                    neighborCommunities.Add(targetCommunity);
            }

            foreach (var targetCommunity in neighborCommunities)
            {
                var delta = ComputeDeltaModularity(
                    communities, edges, totalWeight2M, nodeId, currentCommunity, targetCommunity);

                if (delta > bestDelta)
                {
                    bestDelta = delta;
                    bestCommunity = targetCommunity;
                }
            }

            if (bestCommunity != currentCommunity)
            {
                communities[nodeId] = bestCommunity;
                improved = true;
            }
        }

        return improved;
    }

    private static double ComputeDeltaModularity(
        Dictionary<string, int> communities,
        IReadOnlyDictionary<string, IReadOnlyList<Graph.Edge>> edges,
        double totalWeight2M,
        string nodeId,
        int fromCommunity,
        int toCommunity)
    {
        var ki = ComputeNodeWeight(edges, nodeId);
        var kiIn = ComputeWeightToCommunity(edges, communities, nodeId, fromCommunity);
        var sigmaTot = ComputeCommunityWeight(edges, communities, fromCommunity);

        var delta = (kiIn / totalWeight2M) - ((sigmaTot * ki) / (totalWeight2M * totalWeight2M));
        return delta;
    }

    private static double ComputeNodeWeight(
        IReadOnlyDictionary<string, IReadOnlyList<Graph.Edge>> edges, string nodeId)
    {
        if (!edges.TryGetValue(nodeId, out var outgoing))
            return 0.0;

        return outgoing.Sum(e => e.Weight);
    }

    private static double ComputeWeightToCommunity(
        IReadOnlyDictionary<string, IReadOnlyList<Graph.Edge>> edges,
        Dictionary<string, int> communities,
        string nodeId,
        int community)
    {
        if (!edges.TryGetValue(nodeId, out var outgoing))
            return 0.0;

        return outgoing
            .Where(e => communities.TryGetValue(e.ToNodeId, out var c) && c == community)
            .Sum(e => e.Weight);
    }

    private static double ComputeCommunityWeight(
        IReadOnlyDictionary<string, IReadOnlyList<Graph.Edge>> edges,
        Dictionary<string, int> communities,
        int community)
    {
        var total = 0.0;

        foreach (var (nodeId, commId) in communities)
        {
            if (commId != community) continue;

            if (edges.TryGetValue(nodeId, out var outgoing))
                total += outgoing.Sum(e => e.Weight);
        }

        return total;
    }

    private static double ComputeModularity(
        Dictionary<string, int> communities,
        IReadOnlyDictionary<string, IReadOnlyList<Graph.Edge>> edges,
        double totalWeight)
    {
        if (totalWeight == 0.0) return 0.0;

        var totalWeight2M = 2.0 * totalWeight;
        var modularity = 0.0;

        var communitySet = new HashSet<int>(communities.Values);

        foreach (var community in communitySet)
        {
            var communityNodes = communities.Where(c => c.Value == community).Select(c => c.Key).ToList();
            var eii = ComputeInternalEdges(edges, communities, community);
            var ai = ComputeCommunityDegree(edges, communities, community) / totalWeight2M;

            modularity += (eii / totalWeight) - (ai * ai);
        }

        return modularity;
    }

    private static double ComputeInternalEdges(
        IReadOnlyDictionary<string, IReadOnlyList<Graph.Edge>> edges,
        Dictionary<string, int> communities,
        int community)
    {
        var total = 0.0;

        foreach (var (nodeId, commId) in communities)
        {
            if (commId != community) continue;
            if (!edges.TryGetValue(nodeId, out var outgoing)) continue;

            total += outgoing
                .Where(e => communities.TryGetValue(e.ToNodeId, out var c) && c == community)
                .Sum(e => e.Weight);
        }

        return total;
    }

    private static double ComputeCommunityDegree(
        IReadOnlyDictionary<string, IReadOnlyList<Graph.Edge>> edges,
        Dictionary<string, int> communities,
        int community)
    {
        var total = 0.0;

        foreach (var (nodeId, commId) in communities)
        {
            if (commId != community) continue;
            if (!edges.TryGetValue(nodeId, out var outgoing)) continue;

            total += outgoing.Sum(e => e.Weight);
        }

        return total;
    }

    private IReadOnlyList<Graph.Cluster> BuildClusters(
        Dictionary<string, int> communities,
        IEnumerable<string> nodeIds)
    {
        var clusters = new Dictionary<int, List<string>>();

        foreach (var nodeId in nodeIds)
        {
            if (!communities.TryGetValue(nodeId, out var community))
                continue;

            if (!clusters.ContainsKey(community))
                clusters[community] = new List<string>();

            clusters[community].Add(nodeId);
        }

        return clusters.Select(kvp => new Graph.Cluster
        {
            Id = kvp.Key.ToString(),
            NodeIds = kvp.Value,
            Modularity = CurrentQuality,
            ComputedAt = DateTime.UtcNow
        }).ToList();
    }
}