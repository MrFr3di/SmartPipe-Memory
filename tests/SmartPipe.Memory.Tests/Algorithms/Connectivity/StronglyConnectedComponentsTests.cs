using SmartPipe.Memory.Algorithms.Connectivity;
using SmartPipe.Memory.Graph;

namespace SmartPipe.Memory.Tests.Algorithms.Connectivity;

public sealed class StronglyConnectedComponentsTests
{
    [Fact]
    public void EmptyGraph_ReturnsEmptyList()
    {
        var nodes = new Dictionary<string, Node>();
        var edges = new Dictionary<string, IReadOnlyList<Edge>>();

        var result = StronglyConnectedComponents.Find(nodes, edges);

        Assert.Empty(result);
    }

    [Fact]
    public void SingleNode_ReturnsSingleComponent()
    {
        var nodes = new Dictionary<string, Node>
        {
            ["A"] = new Node { Id = "A", Type = "File" },
        };
        var edges = new Dictionary<string, IReadOnlyList<Edge>>();

        var result = StronglyConnectedComponents.Find(nodes, edges);

        Assert.Single(result);
        Assert.Equal("A", result[0].Single());
    }

    [Fact]
    public void SimpleCycle_ReturnsOneComponent()
    {
        var nodes = new Dictionary<string, Node>
        {
            ["A"] = new Node { Id = "A", Type = "File" },
            ["B"] = new Node { Id = "B", Type = "File" },
            ["C"] = new Node { Id = "C", Type = "File" },
        };
        var edges = new Dictionary<string, IReadOnlyList<Edge>>
        {
            ["A"] = new List<Edge>
            {
                new Edge
                {
                    FromNodeId = "A",
                    ToNodeId = "B",
                    Type = EdgeType.DerivedFrom,
                },
            },
            ["B"] = new List<Edge>
            {
                new Edge
                {
                    FromNodeId = "B",
                    ToNodeId = "C",
                    Type = EdgeType.DerivedFrom,
                },
            },
            ["C"] = new List<Edge>
            {
                new Edge
                {
                    FromNodeId = "C",
                    ToNodeId = "A",
                    Type = EdgeType.DerivedFrom,
                },
            },
        };

        var result = StronglyConnectedComponents.Find(nodes, edges);

        Assert.Single(result);
        Assert.Equal(3, result[0].Count);
    }

    [Fact]
    public void GraphWithMultipleSCCs_ReturnsCorrectComponents()
    {
        var nodes = new Dictionary<string, Node>
        {
            ["A"] = new Node { Id = "A", Type = "File" },
            ["B"] = new Node { Id = "B", Type = "File" },
            ["C"] = new Node { Id = "C", Type = "File" },
            ["D"] = new Node { Id = "D", Type = "File" },
        };
        var edges = new Dictionary<string, IReadOnlyList<Edge>>
        {
            ["A"] = new List<Edge>
            {
                new Edge
                {
                    FromNodeId = "A",
                    ToNodeId = "B",
                    Type = EdgeType.DerivedFrom,
                },
            },
            ["B"] = new List<Edge>
            {
                new Edge
                {
                    FromNodeId = "B",
                    ToNodeId = "C",
                    Type = EdgeType.DerivedFrom,
                },
            },
            ["C"] = new List<Edge>
            {
                new Edge
                {
                    FromNodeId = "C",
                    ToNodeId = "A",
                    Type = EdgeType.DerivedFrom,
                },
            },
            ["D"] = new List<Edge>
            {
                new Edge
                {
                    FromNodeId = "D",
                    ToNodeId = "C",
                    Type = EdgeType.DerivedFrom,
                },
            },
        };

        var result = StronglyConnectedComponents.Find(nodes, edges);

        Assert.Equal(2, result.Count); // {A,B,C} и {D}
    }
}
