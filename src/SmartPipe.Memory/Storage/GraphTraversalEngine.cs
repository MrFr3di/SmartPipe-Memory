using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Infrastructure;

namespace SmartPipe.Memory.Storage;

internal static class GraphTraversalEngine
{
    public static IReadOnlyList<PathSegment> FindPath(
        ConcurrentDictionary<string, Node> nodes,
        ConcurrentDictionary<string, List<Edge>> outEdges,
        string fromNodeId,
        string toNodeId,
        string edgeType,
        int maxDepth,
        Func<Node, bool>? nodeFilter,
        double? minWeight,
        double? minConfidence,
        CancellationToken ct
    )
    {
        if (!nodes.ContainsKey(fromNodeId) || !nodes.ContainsKey(toNodeId))
            return [];

        var nodeList = nodes.Keys.ToList();
        var nodeIndex = new Dictionary<string, int>(nodeList.Count);
        for (int i = 0; i < nodeList.Count; i++)
            nodeIndex[nodeList[i]] = i;

        var visited = new FastBitArray(nodeList.Count);
        var parent = new Dictionary<string, (string NodeId, string EdgeType, double Weight)>();
        var queue = new (string NodeId, int Depth)[nodeList.Count];
        int head = 0,
            tail = 0;

        int startIndex = nodeIndex[fromNodeId];
        visited.Set(startIndex);
        queue[tail++] = (fromNodeId, 0);

        while (head < tail)
        {
            ct.ThrowIfCancellationRequested();
            var (currentId, depth) = queue[head++];

            if (currentId == toNodeId)
                return ReconstructPath(parent, fromNodeId, toNodeId);

            if (depth >= maxDepth)
                continue;
            if (!outEdges.TryGetValue(currentId, out var outgoing))
                continue;

            foreach (var edge in outgoing)
            {
                if (edge.Type.ToString() != edgeType)
                    continue;
                if (minWeight.HasValue && edge.Weight < minWeight.Value)
                    continue;
                if (minConfidence.HasValue && edge.Confidence < minConfidence.Value)
                    continue;

                int neighborIndex = nodeIndex[edge.ToNodeId];
                if (visited.IsSet(neighborIndex))
                    continue;

                if (nodeFilter is not null && nodes.TryGetValue(edge.ToNodeId, out var targetNode))
                    if (!nodeFilter(targetNode))
                        continue;

                visited.Set(neighborIndex);
                parent[edge.ToNodeId] = (currentId, edge.Type.ToString(), edge.Weight);
                queue[tail++] = (edge.ToNodeId, depth + 1);
            }
        }

        return [];
    }

    public static async IAsyncEnumerable<(Node Node, int Depth)> Traverse(
        ConcurrentDictionary<string, Node> nodes,
        ConcurrentDictionary<string, List<Edge>> outEdges,
        string startNodeId,
        string edgeType,
        int maxDepth,
        int limit,
        Func<Node, bool>? nodeFilter,
        double? minWeight,
        double? minConfidence,
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        if (!nodes.ContainsKey(startNodeId))
            yield break;

        var nodeList = nodes.Keys.ToList();
        var nodeIndex = new Dictionary<string, int>(nodeList.Count);
        for (int i = 0; i < nodeList.Count; i++)
            nodeIndex[nodeList[i]] = i;

        var visited = new FastBitArray(nodeList.Count);
        var queue = new (string NodeId, int Depth)[nodeList.Count];
        int head = 0,
            tail = 0;

        int startIndex = nodeIndex[startNodeId];
        visited.Set(startIndex);
        queue[tail++] = (startNodeId, 0);

        int count = 0;
        while (head < tail && count < limit)
        {
            ct.ThrowIfCancellationRequested();
            var (currentId, depth) = queue[head++];

            if (nodes.TryGetValue(currentId, out var currentNode))
            {
                if (nodeFilter is null || nodeFilter(currentNode))
                {
                    yield return (currentNode, depth);
                    count++;
                }
            }

            if (depth >= maxDepth)
                continue;
            if (!outEdges.TryGetValue(currentId, out var outgoing))
                continue;

            foreach (var edge in outgoing)
            {
                if (edge.Type.ToString() != edgeType)
                    continue;
                if (minWeight.HasValue && edge.Weight < minWeight.Value)
                    continue;
                if (minConfidence.HasValue && edge.Confidence < minConfidence.Value)
                    continue;

                int neighborIndex = nodeIndex[edge.ToNodeId];
                if (visited.IsSet(neighborIndex))
                    continue;

                if (nodeFilter is not null && nodes.TryGetValue(edge.ToNodeId, out var targetNode))
                    if (!nodeFilter(targetNode))
                        continue;

                visited.Set(neighborIndex);
                queue[tail++] = (edge.ToNodeId, depth + 1);
            }
        }
    }

    private static List<PathSegment> ReconstructPath(
        Dictionary<string, (string NodeId, string EdgeType, double Weight)> parent,
        string fromNodeId,
        string toNodeId
    )
    {
        var path = new List<PathSegment>();
        var current = toNodeId;

        while (current != fromNodeId && parent.TryGetValue(current, out var p))
        {
            path.Add(
                new PathSegment
                {
                    NodeId = current,
                    EdgeType = p.EdgeType,
                    Weight = p.Weight,
                }
            );
            current = p.NodeId;
        }

        path.Add(
            new PathSegment
            {
                NodeId = fromNodeId,
                EdgeType = string.Empty,
                Weight = 0.0,
            }
        );
        path.Reverse();
        return path;
    }
}
