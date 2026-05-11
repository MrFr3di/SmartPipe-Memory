using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using SmartPipe.Memory.Graph;

namespace SmartPipe.Memory.Storage;

/// <summary>
/// Shared graph traversal logic used by InMemoryGraphStore and DiskBackedGraphStore.
/// Provides BFS-based pathfinding and traversal with support for node filtering,
/// minimum edge weight, and minimum edge confidence.
/// </summary>
internal static class GraphTraversalEngine
{
    /// <summary>
    /// Find the shortest path between two nodes using BFS.
    /// </summary>
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
        CancellationToken ct)
    {
        if (!nodes.ContainsKey(fromNodeId) || !nodes.ContainsKey(toNodeId))
            return Array.Empty<PathSegment>();

        var parent = new Dictionary<string, (string NodeId, string EdgeType, double Weight)>();
        var queue = new Queue<(string NodeId, int Depth)>();
        var visited = new HashSet<string>();

        queue.Enqueue((fromNodeId, 0));
        visited.Add(fromNodeId);

        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            var (currentId, depth) = queue.Dequeue();

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

                if (nodeFilter is not null && nodes.TryGetValue(edge.ToNodeId, out var targetNode))
                {
                    if (!nodeFilter(targetNode))
                        continue;
                }

                if (!visited.Add(edge.ToNodeId))
                    continue;

                parent[edge.ToNodeId] = (currentId, edge.Type.ToString(), edge.Weight);
                queue.Enqueue((edge.ToNodeId, depth + 1));
            }
        }

        return Array.Empty<PathSegment>();
    }

    /// <summary>
    /// Traverse the graph from a starting node.
    /// </summary>
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
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (!nodes.TryGetValue(startNodeId, out var startNode))
            yield break;

        var visited = new HashSet<string>();
        var queue = new Queue<(string NodeId, int Depth)>();
        queue.Enqueue((startNodeId, 0));
        visited.Add(startNodeId);

        var count = 0;
        while (queue.Count > 0 && count < limit)
        {
            ct.ThrowIfCancellationRequested();

            var (currentId, depth) = queue.Dequeue();

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

                if (nodeFilter is not null && nodes.TryGetValue(edge.ToNodeId, out var targetNode))
                {
                    if (!nodeFilter(targetNode))
                        continue;
                }

                if (visited.Add(edge.ToNodeId))
                    queue.Enqueue((edge.ToNodeId, depth + 1));
            }
        }
    }

    private static List<PathSegment> ReconstructPath(
        Dictionary<string, (string NodeId, string EdgeType, double Weight)> parent,
        string fromNodeId,
        string toNodeId)
    {
        var path = new List<PathSegment>();
        var current = toNodeId;

        while (current != fromNodeId && parent.TryGetValue(current, out var p))
        {
            path.Add(new PathSegment { NodeId = current, EdgeType = p.EdgeType, Weight = p.Weight });
            current = p.NodeId;
        }

        path.Add(new PathSegment { NodeId = fromNodeId, EdgeType = string.Empty, Weight = 0.0 });
        path.Reverse();
        return path;
    }
}