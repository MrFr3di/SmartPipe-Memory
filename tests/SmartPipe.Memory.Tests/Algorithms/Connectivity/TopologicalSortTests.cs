using SmartPipe.Memory.Algorithms.Connectivity;
using SmartPipe.Memory.Graph;

namespace SmartPipe.Memory.Tests.Algorithms.Connectivity;

public sealed class TopologicalSortTests
{
    [Fact]
    public void EmptyGraph_EmptyResult()
    {
        var nodes = new Dictionary<string, Node>();
        var edges = new Dictionary<string, IReadOnlyList<Edge>>();

        var result = TopologicalSort.KahnSort(nodes, edges);

        Assert.Empty(result.Sorted);
        Assert.False(result.HasCycles);
        Assert.Empty(result.CyclicNodes);
    }

    [Fact]
    public void SimplePath_ReturnsTopologicalOrder()
    {
        var nodes = new Dictionary<string, Node>
        {
            ["A"] = new Node { Id = "A", Type = "File" },
            ["B"] = new Node { Id = "B", Type = "File" },
            ["C"] = new Node { Id = "C", Type = "File" }
        };
        var edges = new Dictionary<string, IReadOnlyList<Edge>>
        {
            ["A"] = new List<Edge> { new Edge { FromNodeId = "A", ToNodeId = "B", Type = EdgeType.DerivedFrom } },
            ["B"] = new List<Edge> { new Edge { FromNodeId = "B", ToNodeId = "C", Type = EdgeType.DerivedFrom } }
        };

        var result = TopologicalSort.KahnSort(nodes, edges);

        Assert.Equal(new[] { "A", "B", "C" }, result.Sorted);
        Assert.False(result.HasCycles);
    }

    [Fact]
    public void GraphWithCycle_DetectsCycle()
    {
        var nodes = new Dictionary<string, Node>
        {
            ["A"] = new Node { Id = "A", Type = "File" },
            ["B"] = new Node { Id = "B", Type = "File" },
            ["C"] = new Node { Id = "C", Type = "File" }
        };
        var edges = new Dictionary<string, IReadOnlyList<Edge>>
        {
            ["A"] = new List<Edge> { new Edge { FromNodeId = "A", ToNodeId = "B", Type = EdgeType.DerivedFrom } },
            ["B"] = new List<Edge> { new Edge { FromNodeId = "B", ToNodeId = "C", Type = EdgeType.DerivedFrom } },
            ["C"] = new List<Edge> { new Edge { FromNodeId = "C", ToNodeId = "A", Type = EdgeType.DerivedFrom } }
        };

        var result = TopologicalSort.KahnSort(nodes, edges);

        Assert.True(result.HasCycles);
        Assert.Equal(3, result.CyclicNodes.Count);
    }

    [Fact]
    public void DisconnectedGraph_HandlesCorrectly()
    {
        var nodes = new Dictionary<string, Node>
        {
            ["A"] = new Node { Id = "A", Type = "File" },
            ["B"] = new Node { Id = "B", Type = "File" }
        };
        var edges = new Dictionary<string, IReadOnlyList<Edge>>();

        var result = TopologicalSort.KahnSort(nodes, edges);

        Assert.Equal(2, result.Sorted.Count);
        Assert.False(result.HasCycles);
    }
}