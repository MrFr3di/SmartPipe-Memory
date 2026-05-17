using SmartPipe.Memory.Graph;

namespace SmartPipe.Memory.Algorithms.Connectivity;

/// <summary>
/// Union-Find (Disjoint Set Union) with path compression and union by rank.
/// Finds weakly connected components in a directed graph by treating edges as undirected.
/// Time complexity: O(E × α(V)), where α is the inverse Ackermann function.
/// Space complexity: O(V).
/// </summary>
public static class WeaklyConnectedComponents
{
    /// <summary>
    /// Finds all weakly connected components.
    /// </summary>
    /// <param name="nodes">All nodes in the graph.</param>
    /// <param name="outEdges">Outgoing edges keyed by source node id.</param>
    /// <returns>List of WCCs, each represented as a list of node identifiers.</returns>
    public static List<List<string>> Find(
        IReadOnlyDictionary<string, Node> nodes,
        IReadOnlyDictionary<string, IReadOnlyList<Edge>> outEdges
    )
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(outEdges);

        var parent = new Dictionary<string, string>();
        var rank = new Dictionary<string, int>();

        foreach (var nodeId in nodes.Keys)
        {
            parent[nodeId] = nodeId;
            rank[nodeId] = 0;
        }

        foreach (var (from, outgoing) in outEdges)
        {
            foreach (var edge in outgoing)
                Union(from, edge.ToNodeId, parent, rank);
        }

        var componentMap = new Dictionary<string, List<string>>();
        foreach (var nodeId in nodes.Keys)
        {
            var root = Find(nodeId, parent);
            if (!componentMap.ContainsKey(root))
                componentMap[root] = new List<string>();
            componentMap[root].Add(nodeId);
        }

        return componentMap.Values.ToList();
    }

    private static string Find(string nodeId, Dictionary<string, string> parent)
    {
        if (parent[nodeId] != nodeId)
            parent[nodeId] = Find(parent[nodeId], parent);
        return parent[nodeId];
    }

    private static void Union(
        string a,
        string b,
        Dictionary<string, string> parent,
        Dictionary<string, int> rank
    )
    {
        var rootA = Find(a, parent);
        var rootB = Find(b, parent);

        if (rootA == rootB)
            return;

        if (rank[rootA] < rank[rootB])
            parent[rootA] = rootB;
        else if (rank[rootA] > rank[rootB])
            parent[rootB] = rootA;
        else
        {
            parent[rootB] = rootA;
            rank[rootA]++;
        }
    }
}
