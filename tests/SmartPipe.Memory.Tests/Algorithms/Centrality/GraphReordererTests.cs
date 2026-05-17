using SmartPipe.Memory.Algorithms.Centrality;
using SmartPipe.Memory.Graph;

namespace SmartPipe.Memory.Tests.Algorithms.Centrality;

public sealed class GraphReordererTests
{
    [Fact]
    public void ReorderByCommunity_SameCluster_Adjacent()
    {
        var reorderer = new GraphReorderer();
        var nodes = new List<Node>
        {
            new Node
            {
                Id = "A",
                Type = "File",
                HealthScore = 0.5,
            },
            new Node
            {
                Id = "B",
                Type = "File",
                HealthScore = 0.9,
            },
            new Node
            {
                Id = "C",
                Type = "File",
                HealthScore = 0.3,
            },
        };
        var clusters = new List<Cluster>
        {
            new Cluster { Id = "1", NodeIds = new[] { "A", "C" } },
            new Cluster { Id = "2", NodeIds = new[] { "B" } },
        };

        var result = reorderer.ReorderByCommunity(nodes, clusters);

        Assert.Equal(3, result.Count);
        Assert.Equal("A", result[0].Id);
        Assert.Equal("C", result[1].Id);
        Assert.Equal("B", result[2].Id);
    }

    [Fact]
    public void ReorderByDegree_HubsFirst()
    {
        var reorderer = new GraphReorderer();
        var nodes = new List<Node>
        {
            new Node { Id = "A", Type = "File" },
            new Node { Id = "B", Type = "File" },
            new Node { Id = "C", Type = "File" },
        };
        // Use IReadOnlyDictionary<string, IReadOnlyList<Edge>> to match signature
        var edges = new Dictionary<string, IReadOnlyList<Edge>>
        {
            ["B"] = new List<Edge>
            {
                new Edge
                {
                    FromNodeId = "B",
                    ToNodeId = "A",
                    Type = EdgeType.DerivedFrom,
                },
                new Edge
                {
                    FromNodeId = "B",
                    ToNodeId = "C",
                    Type = EdgeType.DerivedFrom,
                },
            },
        };

        var result = reorderer.ReorderByDegree(nodes, edges);

        Assert.Equal("B", result[0].Id);
    }

    [Fact]
    public void ReorderByAccessibility_HealthiestFirst()
    {
        var reorderer = new GraphReorderer();
        var nodes = new List<Node>
        {
            new Node
            {
                Id = "A",
                Type = "File",
                HealthScore = 0.3,
            },
            new Node
            {
                Id = "B",
                Type = "File",
                HealthScore = 0.9,
            },
        };

        var result = reorderer.ReorderByAccessibility(nodes);

        Assert.Equal("B", result[0].Id);
        Assert.Equal("A", result[1].Id);
    }
}
