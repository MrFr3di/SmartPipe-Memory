using System.Collections.Concurrent;
using SmartPipe.Memory.Algorithms.Centrality;
using SmartPipe.Memory.Graph;

namespace SmartPipe.Memory.Tests.Algorithms.Centrality;

public sealed class BetweennessCentralityTests
{
    [Fact]
    public void Compute_EmptyGraph_ReturnsEmpty()
    {
        var bc = new BetweennessCentrality();
        var nodes = new Dictionary<string, Node>();
        var edges = new ConcurrentDictionary<string, List<Edge>>();

        var result = bc.Compute(nodes, edges);

        Assert.Empty(result);
    }

    [Fact]
    public void Compute_LinearGraph_CentralNodeHasHigherBetweenness()
    {
        var bc = new BetweennessCentrality();
        var nodes = new Dictionary<string, Node>
        {
            ["A"] = new Node { Id = "A", Type = "File" },
            ["B"] = new Node { Id = "B", Type = "File" },
            ["C"] = new Node { Id = "C", Type = "File" }
        };
        var edges = new ConcurrentDictionary<string, List<Edge>>();
        edges.TryAdd("A", new List<Edge> { new Edge { FromNodeId = "A", ToNodeId = "B", Type = EdgeType.DerivedFrom } });
        edges.TryAdd("B", new List<Edge> { new Edge { FromNodeId = "B", ToNodeId = "C", Type = EdgeType.DerivedFrom } });

        var result = bc.Compute(nodes, edges);

        Assert.Equal(3, result.Count);
        Assert.True(result["B"] > result["A"]);
        Assert.True(result["B"] > result["C"]);
    }

    [Fact]
    public void ComputeForSubset_OnlySubsetNodesComputed()
    {
        var bc = new BetweennessCentrality();
        var nodes = new Dictionary<string, Node>
        {
            ["A"] = new Node { Id = "A", Type = "File" },
            ["B"] = new Node { Id = "B", Type = "File" },
            ["C"] = new Node { Id = "C", Type = "File" }
        };
        var edges = new ConcurrentDictionary<string, List<Edge>>();
        edges.TryAdd("A", new List<Edge> { new Edge { FromNodeId = "A", ToNodeId = "B", Type = EdgeType.DerivedFrom } });
        edges.TryAdd("B", new List<Edge> { new Edge { FromNodeId = "B", ToNodeId = "C", Type = EdgeType.DerivedFrom } });

        var result = bc.ComputeForSubset(nodes, edges, new[] { "A", "B" });

        Assert.True(result["B"] > 0);
        Assert.True(result["A"] >= 0);
        Assert.True(result["C"] == 0);
    }
}