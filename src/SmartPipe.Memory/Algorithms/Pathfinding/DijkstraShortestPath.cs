using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Algorithms.Pathfinding;

/// <summary>
/// Dijkstra's algorithm for finding the shortest weighted path
/// between two nodes. Edge weight is treated as inverse strength
/// (stronger connection = shorter path).
/// </summary>
public sealed class DijkstraShortestPath
{
    /// <summary>
    /// Find the shortest weighted path between two nodes.
    /// </summary>
    /// <param name="edges">All edges in the graph, keyed by source node id.</param>
    /// <param name="fromNodeId">Starting node identifier.</param>
    /// <param name="toNodeId">Target node identifier.</param>
    /// <param name="edgeType">Edge type to traverse.</param>
    /// <param name="maxDepth">Maximum search depth.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Ordered list of path segments, or empty list if no path found.</returns>
    public IReadOnlyList<PathSegment> FindPath(
        IReadOnlyDictionary<string, IReadOnlyList<Graph.Edge>> edges,
        string fromNodeId,
        string toNodeId,
        string edgeType,
        int maxDepth,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(edges);
        ArgumentException.ThrowIfNullOrEmpty(fromNodeId);
        ArgumentException.ThrowIfNullOrEmpty(toNodeId);
        if (maxDepth <= 0) throw new ArgumentOutOfRangeException(nameof(maxDepth));

        var distances = new Dictionary<string, double>();
        var parent = new Dictionary<string, (string NodeId, string EdgeType, double Weight)>();
        var queue = new PriorityQueue<string, double>();

        distances[fromNodeId] = 0.0;
        queue.Enqueue(fromNodeId, 0.0);

        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            var currentId = queue.Dequeue();
            var currentDistance = distances[currentId];

            if (currentId == toNodeId)
                return ReconstructPath(parent, fromNodeId, toNodeId);

            if (!edges.TryGetValue(currentId, out var outgoing))
                continue;

            foreach (var edge in outgoing)
            {
                if (edge.Type.ToString() != edgeType)
                    continue;

                var weight = 1.0 - edge.Weight;
                var newDistance = currentDistance + weight;

                if (!distances.ContainsKey(edge.ToNodeId) || newDistance < distances[edge.ToNodeId])
                {
                    distances[edge.ToNodeId] = newDistance;
                    parent[edge.ToNodeId] = (currentId, edge.Type.ToString(), edge.Weight);
                    queue.Enqueue(edge.ToNodeId, newDistance);
                }
            }
        }

        return Array.Empty<PathSegment>();
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