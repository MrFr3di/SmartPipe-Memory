using System.Collections.Concurrent;
using SmartPipe.Memory.Algorithms.Centrality;
using SmartPipe.Memory.Graph;

namespace SmartPipe.Memory.Tests.Algorithms.Centrality;

public sealed class PageRankTests
{
    [Fact]
    public void Compute_EmptyGraph_ReturnsEmpty()
    {
        var pagerank = new PageRank();
        var nodes = new Dictionary<string, Node>();
        var edges = new ConcurrentDictionary<string, List<Edge>>();

        var result = pagerank.Compute(nodes, edges);

        Assert.Empty(result);
    }

    [Fact]
    public void Compute_TwoNodes_EqualRanks()
    {
        var pagerank = new PageRank();
        var nodes = new Dictionary<string, Node>
        {
            ["A"] = new Node { Id = "A", Type = "File" },
            ["B"] = new Node { Id = "B", Type = "File" }
        };
        var edges = new ConcurrentDictionary<string, List<Edge>>();
        edges.TryAdd("A", new List<Edge> { new Edge { FromNodeId = "A", ToNodeId = "B", Type = EdgeType.DerivedFrom } });
        edges.TryAdd("B", new List<Edge> { new Edge { FromNodeId = "B", ToNodeId = "A", Type = EdgeType.DerivedFrom } });

        var result = pagerank.Compute(nodes, edges);

        Assert.Equal(2, result.Count);
        Assert.True(Math.Abs(result["A"] - result["B"]) < 0.01);
    }

    [Fact]
    public void Compute_DanglingNode_NoError()
    {
        var pagerank = new PageRank();
        var nodes = new Dictionary<string, Node>
        {
            ["A"] = new Node { Id = "A", Type = "File" },
            ["B"] = new Node { Id = "B", Type = "File" }
        };
        var edges = new ConcurrentDictionary<string, List<Edge>>();
        edges.TryAdd("A", new List<Edge> { new Edge { FromNodeId = "A", ToNodeId = "B", Type = EdgeType.DerivedFrom } });

        var result = pagerank.Compute(nodes, edges);

        Assert.Equal(2, result.Count);
        Assert.True(result["A"] > 0);
        Assert.True(result["B"] > 0);
    }

    [Fact]
    public void Compute_StabilityAfterConvergence()
    {
        var pagerank = new PageRank();
        var nodes = new Dictionary<string, Node>();
        var edges = new ConcurrentDictionary<string, List<Edge>>();
        for (var i = 0; i < 10; i++)
        {
            nodes[$"{i}"] = new Node { Id = $"{i}", Type = "File" };
            edges.TryAdd($"{i}", new List<Edge>());
        }
        for (var i = 0; i < 9; i++)
            edges[$"{i}"].Add(new Edge { FromNodeId = $"{i}", ToNodeId = $"{i + 1}", Type = EdgeType.DerivedFrom });

        var result1 = pagerank.Compute(nodes, edges, maxIterations: 50);
        var result2 = pagerank.Compute(nodes, edges, maxIterations: 100);

        foreach (var key in nodes.Keys)
            Assert.True(Math.Abs(result1[key] - result2[key]) < 1e-4);
    }
}