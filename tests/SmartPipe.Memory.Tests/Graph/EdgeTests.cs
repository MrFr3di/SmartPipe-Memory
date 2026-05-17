using SmartPipe.Memory.Graph;

namespace SmartPipe.Memory.Tests.Graph;

public sealed class EdgeTests
{
    [Fact]
    public void Create_DefaultValues()
    {
        var edge = new Edge
        {
            FromNodeId = "n1",
            ToNodeId = "n2",
            Type = EdgeType.DerivedFrom,
        };

        Assert.Equal("n1", edge.FromNodeId);
        Assert.Equal("n2", edge.ToNodeId);
        Assert.Equal(EdgeType.DerivedFrom, edge.Type);
        Assert.Equal(1.0, edge.Weight);
        Assert.Equal(1.0, edge.Confidence);
        Assert.Equal("LOG", edge.SourceType);
        Assert.NotNull(edge.Steps);
        Assert.Empty(edge.Steps);
        Assert.Equal(0, edge.Id);
    }

    [Fact]
    public void Create_WithSteps()
    {
        var steps = new List<TransformationStep>
        {
            new("FileHasher", DateTime.UtcNow.AddSeconds(-5), TimeSpan.FromMilliseconds(12), null),
            new("DuplicateDetector", DateTime.UtcNow, TimeSpan.FromMilliseconds(8), null),
        };

        var edge = new Edge
        {
            FromNodeId = "n1",
            ToNodeId = "n2",
            Type = EdgeType.DuplicateOf,
            Steps = steps,
        };

        Assert.Equal(2, edge.Steps.Count);
        Assert.Equal("FileHasher", edge.Steps[0].TransformerName);
        Assert.Equal(12, edge.Steps[0].Duration.TotalMilliseconds);
    }

    [Theory]
    [InlineData(EdgeType.DerivedFrom)]
    [InlineData(EdgeType.DuplicateOf)]
    [InlineData(EdgeType.VersionOf)]
    [InlineData(EdgeType.AggregatedFrom)]
    [InlineData(EdgeType.FilteredFrom)]
    [InlineData(EdgeType.FeedsInto)]
    public void AllEdgeTypes_AreDefined(EdgeType type)
    {
        var edge = new Edge
        {
            FromNodeId = "n1",
            ToNodeId = "n2",
            Type = type,
        };

        Assert.Equal(type, edge.Type);
        Assert.Equal(type.ToString(), edge.Type.ToString());
    }

    [Fact]
    public void Weight_CanDecay()
    {
        var edge = new Edge
        {
            FromNodeId = "n1",
            ToNodeId = "n2",
            Type = EdgeType.DerivedFrom,
            Weight = 1.0,
        };

        edge.Weight = 0.5;
        Assert.Equal(0.5, edge.Weight);

        edge.Weight = 0.1;
        Assert.Equal(0.1, edge.Weight);
    }

    [Fact]
    public void Bitemporal_Fields()
    {
        var past = DateTime.UtcNow.AddDays(-30);
        var now = DateTime.UtcNow;

        var edge = new Edge
        {
            FromNodeId = "n1",
            ToNodeId = "n2",
            Type = EdgeType.DerivedFrom,
            ValidFrom = past,
            ValidTo = now,
        };

        Assert.Equal(past, edge.ValidFrom);
        Assert.Equal(now, edge.ValidTo);
        Assert.True(edge.TxTime <= DateTime.UtcNow);
    }

    [Fact]
    public void Confidence_DefaultsToOne()
    {
        var edge = new Edge
        {
            FromNodeId = "n1",
            ToNodeId = "n2",
            Type = EdgeType.DerivedFrom,
        };

        Assert.Equal(1.0, edge.Confidence);
    }
}
