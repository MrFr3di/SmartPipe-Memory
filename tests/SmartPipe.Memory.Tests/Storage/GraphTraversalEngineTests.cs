using System.Collections.Concurrent;
using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Tests.Storage;

public sealed class GraphTraversalEngineTests
{
    private ConcurrentDictionary<string, Node> CreateNodes(params (string Id, double HealthScore)[] items)
    {
        var dict = new ConcurrentDictionary<string, Node>();
        foreach (var (id, health) in items)
            dict[id] = new Node { Id = id, Type = "File", HealthScore = health };
        return dict;
    }

    private ConcurrentDictionary<string, List<Edge>> CreateEdges(params (string From, string To, string Type, double Weight, double Confidence)[] items)
    {
        var dict = new ConcurrentDictionary<string, List<Edge>>();
        foreach (var (from, to, type, weight, confidence) in items)
        {
            var edge = new Edge { FromNodeId = from, ToNodeId = to, Type = Enum.Parse<EdgeType>(type), Weight = weight, Confidence = confidence };
            dict.GetOrAdd(from, _ => new List<Edge>()).Add(edge);
        }
        return dict;
    }

    [Fact]
    public void FindPath_DirectConnection_ReturnsPath()
    {
        var nodes = CreateNodes(("A", 1.0), ("B", 1.0));
        var edges = CreateEdges(("A", "B", "DerivedFrom", 1.0, 1.0));

        var path = GraphTraversalEngine.FindPath(nodes, edges, "A", "B", "DerivedFrom", 10, null, null, null, CancellationToken.None);

        Assert.Equal(2, path.Count);
        Assert.Equal("A", path[0].NodeId);
        Assert.Equal("B", path[1].NodeId);
    }

    [Fact]
    public void FindPath_NoConnection_ReturnsEmpty()
    {
        var nodes = CreateNodes(("A", 1.0), ("B", 1.0));
        var edges = new ConcurrentDictionary<string, List<Edge>>();

        var path = GraphTraversalEngine.FindPath(nodes, edges, "A", "B", "DerivedFrom", 10, null, null, null, CancellationToken.None);

        Assert.Empty(path);
    }

    [Fact]
    public void FindPath_NodeFilter_BlocksUnhealthyNodes()
    {
        var nodes = CreateNodes(("A", 1.0), ("B", 0.05), ("C", 1.0));
        var edges = CreateEdges(("A", "B", "DerivedFrom", 1.0, 1.0), ("B", "C", "DerivedFrom", 1.0, 1.0));

        var path = GraphTraversalEngine.FindPath(nodes, edges, "A", "C", "DerivedFrom", 10, node => node.HealthScore > 0.1, null, null, CancellationToken.None);

        Assert.Empty(path);
    }

    [Fact]
    public void FindPath_NodeFilter_AllowsHealthyNodes()
    {
        var nodes = CreateNodes(("A", 1.0), ("B", 0.5), ("C", 1.0));
        var edges = CreateEdges(("A", "B", "DerivedFrom", 1.0, 1.0), ("B", "C", "DerivedFrom", 1.0, 1.0));

        var path = GraphTraversalEngine.FindPath(nodes, edges, "A", "C", "DerivedFrom", 10, node => node.HealthScore > 0.1, null, null, CancellationToken.None);

        Assert.Equal(3, path.Count);
    }

    [Fact]
    public void FindPath_MinWeight_FiltersWeakEdges()
    {
        var nodes = CreateNodes(("A", 1.0), ("B", 1.0));
        var edges = CreateEdges(("A", "B", "DerivedFrom", 0.2, 1.0));

        var path = GraphTraversalEngine.FindPath(nodes, edges, "A", "B", "DerivedFrom", 10, null, 0.5, null, CancellationToken.None);

        Assert.Empty(path);
    }

    [Fact]
    public void FindPath_MinConfidence_FiltersLowConfidenceEdges()
    {
        var nodes = CreateNodes(("A", 1.0), ("B", 1.0));
        var edges = CreateEdges(("A", "B", "DerivedFrom", 1.0, 0.3));

        var path = GraphTraversalEngine.FindPath(nodes, edges, "A", "B", "DerivedFrom", 10, null, null, 0.9, CancellationToken.None);

        Assert.Empty(path);
    }

    [Fact]
    public void FindPath_MaxDepth_LimitsSearch()
    {
        var nodes = CreateNodes(("A", 1.0), ("B", 1.0), ("C", 1.0), ("D", 1.0));
        var edges = CreateEdges(("A", "B", "DerivedFrom", 1.0, 1.0), ("B", "C", "DerivedFrom", 1.0, 1.0), ("C", "D", "DerivedFrom", 1.0, 1.0));

        var path = GraphTraversalEngine.FindPath(nodes, edges, "A", "D", "DerivedFrom", 1, null, null, null, CancellationToken.None);

        Assert.Empty(path);
    }

    [Fact]
    public void FindPath_MissingNodes_ReturnsEmpty()
    {
        var nodes = CreateNodes(("A", 1.0));
        var edges = new ConcurrentDictionary<string, List<Edge>>();

        var path = GraphTraversalEngine.FindPath(nodes, edges, "A", "Z", "DerivedFrom", 10, null, null, null, CancellationToken.None);

        Assert.Empty(path);
    }

    [Fact]
    public async Task Traverse_FromStartNode_VisitsReachableNodes()
    {
        var nodes = CreateNodes(("A", 1.0), ("B", 1.0), ("C", 1.0));
        var edges = CreateEdges(("A", "B", "DerivedFrom", 1.0, 1.0), ("A", "C", "DerivedFrom", 1.0, 1.0));

        var results = new List<(Node Node, int Depth)>();
        await foreach (var item in GraphTraversalEngine.Traverse(nodes, edges, "A", "DerivedFrom", 5, 100, null, null, null, CancellationToken.None))
            results.Add(item);

        Assert.Equal(3, results.Count);
        Assert.Equal(0, results[0].Depth);
        Assert.Equal("A", results[0].Node.Id);
    }

    [Fact]
    public async Task Traverse_NodeFilter_SkipsFilteredNodes()
    {
        var nodes = CreateNodes(("A", 1.0), ("B", 0.05));
        var edges = CreateEdges(("A", "B", "DerivedFrom", 1.0, 1.0));

        var results = new List<(Node Node, int Depth)>();
        await foreach (var item in GraphTraversalEngine.Traverse(nodes, edges, "A", "DerivedFrom", 5, 100, node => node.HealthScore > 0.1, null, null, CancellationToken.None))
            results.Add(item);

        Assert.Single(results);
        Assert.Equal("A", results[0].Node.Id);
    }

    [Fact]
    public async Task Traverse_Limit_RestrictsVisitedNodes()
    {
        var nodes = CreateNodes(("A", 1.0), ("B", 1.0), ("C", 1.0), ("D", 1.0));
        var edges = CreateEdges(("A", "B", "DerivedFrom", 1.0, 1.0), ("A", "C", "DerivedFrom", 1.0, 1.0), ("A", "D", "DerivedFrom", 1.0, 1.0));

        var results = new List<(Node Node, int Depth)>();
        await foreach (var item in GraphTraversalEngine.Traverse(nodes, edges, "A", "DerivedFrom", 5, 2, null, null, null, CancellationToken.None))
            results.Add(item);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task Traverse_MinWeight_SkipsWeakEdges()
    {
        var nodes = CreateNodes(("A", 1.0), ("B", 1.0));
        var edges = CreateEdges(("A", "B", "DerivedFrom", 0.2, 1.0));

        var results = new List<(Node Node, int Depth)>();
        await foreach (var item in GraphTraversalEngine.Traverse(nodes, edges, "A", "DerivedFrom", 5, 100, null, 0.5, null, CancellationToken.None))
            results.Add(item);

        Assert.Single(results);
        Assert.Equal("A", results[0].Node.Id);
    }

    [Fact]
    public async Task Traverse_MinConfidence_SkipsLowConfidenceEdges()
    {
        var nodes = CreateNodes(("A", 1.0), ("B", 1.0));
        var edges = CreateEdges(("A", "B", "DerivedFrom", 1.0, 0.3));

        var results = new List<(Node Node, int Depth)>();
        await foreach (var item in GraphTraversalEngine.Traverse(nodes, edges, "A", "DerivedFrom", 5, 100, null, null, 0.9, CancellationToken.None))
            results.Add(item);

        Assert.Single(results);
        Assert.Equal("A", results[0].Node.Id);
    }

    [Fact]
    public async Task Traverse_MissingStartNode_ReturnsEmpty()
    {
        var nodes = new ConcurrentDictionary<string, Node>();
        var edges = new ConcurrentDictionary<string, List<Edge>>();

        var results = new List<(Node Node, int Depth)>();
        await foreach (var item in GraphTraversalEngine.Traverse(nodes, edges, "Z", "DerivedFrom", 5, 100, null, null, null, CancellationToken.None))
            results.Add(item);

        Assert.Empty(results);
    }
}