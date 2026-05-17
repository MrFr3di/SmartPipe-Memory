using SmartPipe.Memory.Graph;

namespace SmartPipe.Memory.Algorithms.Connectivity;

/// <summary>
/// Tarjan's algorithm for finding strongly connected components (SCC) in a directed graph.
/// Iterative implementation to avoid stack overflow on large graphs.
/// Time complexity: O(V + E). Space complexity: O(V).
/// </summary>
public static class StronglyConnectedComponents
{
    /// <summary>
    /// Finds all strongly connected components using Tarjan's iterative algorithm.
    /// </summary>
    /// <param name="nodes">All nodes in the graph.</param>
    /// <param name="outEdges">Outgoing edges keyed by source node id.</param>
    /// <returns>List of SCCs, each represented as a list of node identifiers.</returns>
    public static List<List<string>> Find(
        IReadOnlyDictionary<string, Node> nodes,
        IReadOnlyDictionary<string, IReadOnlyList<Edge>> outEdges
    )
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(outEdges);

        var indexMap = new Dictionary<string, int>();
        var lowLink = new Dictionary<string, int>();
        var onStack = new HashSet<string>();
        var stack = new Stack<string>();
        var components = new List<List<string>>();
        var currentIndex = 0;

        // Инициализация
        foreach (var nodeId in nodes.Keys)
        {
            indexMap[nodeId] = -1;
            lowLink[nodeId] = -1;
        }

        // Итеративный обход всех вершин
        foreach (var startNode in nodes.Keys)
        {
            if (indexMap[startNode] != -1)
                continue;

            var callStack = new Stack<(string node, int step, IEnumerator<Edge>? enumerator)>();
            callStack.Push((startNode, 0, null));

            while (callStack.Count > 0)
            {
                var (node, step, enumerator) = callStack.Pop();

                switch (step)
                {
                    case 0: // First visit
                        indexMap[node] = currentIndex;
                        lowLink[node] = currentIndex;
                        currentIndex++;
                        stack.Push(node);
                        onStack.Add(node);

                        if (outEdges.TryGetValue(node, out var outgoingEdges))
                        {
                            var edgeEnumerator = outgoingEdges.GetEnumerator();
                            callStack.Push((node, 1, edgeEnumerator)); // Return to process step 1
                            continue;
                        }
                        else
                        {
                            // No outgoing edges, check root immediately
                            callStack.Push((node, 2, null)); // Go to step 2 (CheckRoot)
                        }
                        break;

                    case 1: // Process next neighbor
                        var edges = enumerator;
                        if (edges != null && edges.MoveNext())
                        {
                            var neighbor = edges.Current.ToNodeId;
                            if (indexMap[neighbor] == -1)
                            {
                                // Push current state back, then process neighbor
                                callStack.Push((node, 1, edges));
                                callStack.Push((neighbor, 0, null));
                            }
                            else if (onStack.Contains(neighbor))
                            {
                                lowLink[node] = Math.Min(lowLink[node], indexMap[neighbor]);
                                callStack.Push((node, 1, edges)); // Continue with next neighbor
                            }
                            else
                            {
                                callStack.Push((node, 1, edges)); // Continue with next neighbor
                            }
                        }
                        else
                        {
                            // All neighbors processed, check root
                            callStack.Push((node, 2, null));
                        }
                        break;

                    case 2: // Check if root
                        // Update lowLink of parent (if any)
                        if (callStack.Count > 0)
                        {
                            var parentFrame = callStack.Peek();
                            if (parentFrame.step == 1 && parentFrame.node != node)
                            {
                                lowLink[parentFrame.node] = Math.Min(
                                    lowLink[parentFrame.node],
                                    lowLink[node]
                                );
                            }
                        }

                        if (lowLink[node] == indexMap[node])
                        {
                            var component = new List<string>();
                            string w;
                            do
                            {
                                w = stack.Pop();
                                onStack.Remove(w);
                                component.Add(w);
                            } while (w != node);
                            components.Add(component);
                        }
                        break;
                }
            }
        }

        return components;
    }
}
