using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Model;

namespace SmartPipe.Memory.Tests.Model;

public sealed class MemoryQueryTests
{
    [Fact]
    public void NewFields_DefaultToNull()
    {
        var query = new MemoryQuery
        {
            NodeType = "File",
            Type = QueryType.FindNodes
        };

        Assert.Null(query.AsOf);
        Assert.Null(query.TimeRangeFrom);
        Assert.Null(query.TimeRangeTo);
        Assert.Null(query.MinWeight);
        Assert.Null(query.MinConfidence);
        Assert.Null(query.NodeFilter);
    }

    [Fact]
    public void AsOf_SetValue_PreservesValue()
    {
        var timestamp = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

        var query = new MemoryQuery
        {
            NodeType = "File",
            Type = QueryType.FindNodes,
            AsOf = timestamp
        };

        Assert.Equal(timestamp, query.AsOf);
    }

    [Fact]
    public void TimeRange_SetValues_PreservesValues()
    {
        var from = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc);

        var query = new MemoryQuery
        {
            NodeType = "File",
            Type = QueryType.FindNodes,
            TimeRangeFrom = from,
            TimeRangeTo = to
        };

        Assert.Equal(from, query.TimeRangeFrom);
        Assert.Equal(to, query.TimeRangeTo);
    }

    [Fact]
    public void MinWeight_SetValue_PreservesValue()
    {
        var query = new MemoryQuery
        {
            NodeType = "File",
            Type = QueryType.FindNodes,
            MinWeight = 0.5
        };

        Assert.Equal(0.5, query.MinWeight);
    }

    [Fact]
    public void MinConfidence_SetValue_PreservesValue()
    {
        var query = new MemoryQuery
        {
            NodeType = "File",
            Type = QueryType.FindNodes,
            MinConfidence = 0.9
        };

        Assert.Equal(0.9, query.MinConfidence);
    }

    [Fact]
    public void NodeFilter_SetPredicate_PreservesPredicate()
    {
        Func<Node, bool> predicate = node => node.HealthScore > 0.5;

        var query = new MemoryQuery
        {
            NodeType = "File",
            Type = QueryType.FindNodes,
            NodeFilter = predicate
        };

        Assert.NotNull(query.NodeFilter);
        Assert.True(query.NodeFilter(new Node { Id = "n1", Type = "File", HealthScore = 0.8 }));
        Assert.False(query.NodeFilter(new Node { Id = "n2", Type = "File", HealthScore = 0.3 }));
    }

    [Fact]
    public void AllFields_Combined_WorkTogether()
    {
        var query = new MemoryQuery
        {
            NodeType = "File",
            Type = QueryType.FindPath,
            StartNodeId = "n1",
            TargetNodeId = "n2",
            EdgeType = "DerivedFrom",
            MaxDepth = 5,
            Limit = 10,
            OrderBy = "HealthScore",
            OrderDesc = true,
            AsOf = DateTime.UtcNow.AddDays(-7),
            MinWeight = 0.3,
            MinConfidence = 0.8,
            NodeFilter = node => node.HealthScore > 0.2
        };

        Assert.NotNull(query.AsOf);
        Assert.Equal(0.3, query.MinWeight);
        Assert.Equal(0.8, query.MinConfidence);
        Assert.NotNull(query.NodeFilter);
        Assert.Equal(QueryType.FindPath, query.Type);
    }
}