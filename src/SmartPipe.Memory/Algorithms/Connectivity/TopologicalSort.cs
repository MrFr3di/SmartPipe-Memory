using SmartPipe.Memory.Graph;

namespace SmartPipe.Memory.Algorithms.Connectivity;

/// <summary>
/// Kahn's algorithm for topological sort and cycle detection in directed graphs.
/// Time complexity: O(V + E). Space complexity: O(V).
/// </summary>
public static class TopologicalSort
{
    /// <summary>
    /// Represents the result of a topological sort operation.
    /// </summary>
    public sealed class Result
    {
        /// <summary>Nodes in topological order. If the graph has cycles, this is a partial order.</summary>
        public IReadOnlyList<string> Sorted { get; init; } = Array.Empty<string>();

        /// <summary>Whether the graph contains at least one cycle.</summary>
        public bool HasCycles { get; init; }

        /// <summary>Nodes that are part of cycles (not present in sorted order).</summary>
        public IReadOnlyList<string> CyclicNodes { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Performs topological sort using Kahn's algorithm.
    /// Returns the sorted nodes and any nodes involved in cycles.
    /// </summary>
    /// <param name="nodes">All nodes in the graph.</param>
    /// <param name="outEdges">Outgoing edges keyed by source node id.</param>
    /// <returns>The result containing sorted nodes and cycle information.</returns>
    public static Result KahnSort(
        IReadOnlyDictionary<string, Node> nodes,
        IReadOnlyDictionary<string, IReadOnlyList<Edge>> outEdges)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(outEdges);

        var inDegrees = new Dictionary<string, int>();
        foreach (var nodeId in nodes.Keys)
            inDegrees[nodeId] = 0;

        foreach (var (_, outgoing) in outEdges)
        {
            foreach (var edge in outgoing)
            {
                if (inDegrees.ContainsKey(edge.ToNodeId))
                    inDegrees[edge.ToNodeId]++;
            }
        }

        var queue = new Queue<string>();
        foreach (var (nodeId, degree) in inDegrees)
        {
            if (degree == 0)
                queue.Enqueue(nodeId);
        }

        var sorted = new List<string>(nodes.Count);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            sorted.Add(current);

            if (outEdges.TryGetValue(current, out var outgoing))
            {
                foreach (var edge in outgoing)
                {
                    inDegrees[edge.ToNodeId]--;
                    if (inDegrees[edge.ToNodeId] == 0)
                        queue.Enqueue(edge.ToNodeId);
                }
            }
        }

        var hasCycles = sorted.Count < nodes.Count;
        IReadOnlyList<string> cyclicNodes = hasCycles
            ? nodes.Keys.Except(sorted).ToList()
            : Array.Empty<string>();

        return new Result
        {
            Sorted = sorted,
            HasCycles = hasCycles,
            CyclicNodes = cyclicNodes
        };
    }
}