using SmartPipe.Memory.Graph;

namespace SmartPipe.Memory.Tests.Graph;

public sealed class NodeTests
{
    [Fact]
    public void Create_DefaultValues_AreSet()
    {
        var node = new Node { Id = "n1", Type = "File" };

        Assert.Equal("n1", node.Id);
        Assert.Equal("File", node.Type);
        Assert.Equal("", node.Label);
        Assert.NotNull(node.Properties);
        Assert.Empty(node.Properties);
        Assert.NotNull(node.Metadata);
        Assert.Empty(node.Metadata);
        Assert.Null(node.Embedding);
        Assert.Equal(1.0, node.HealthScore);
        Assert.Equal(0.0, node.FailureProbability);
        Assert.Equal(0.0, node.PredictedLatencyMs);
        Assert.Equal(0.0, node.ResourceStrain);
        Assert.Equal(1, node.Version);
        Assert.True(node.ValidFrom <= DateTime.UtcNow);
        Assert.Null(node.ValidTo);
        Assert.True(node.TxTime <= DateTime.UtcNow);
    }

    [Fact]
    public void Create_WithProperties_SetsCorrectly()
    {
        var props = new Dictionary<string, object>
        {
            ["path"] = "/data/file.txt",
            ["size"] = 1024L,
            ["hash"] = "sha256:abc123",
        };

        var node = new Node
        {
            Id = "n1",
            Type = "File",
            Label = "file.txt",
            Properties = props,
        };

        Assert.Equal("file.txt", node.Label);
        Assert.Equal(3, node.Properties.Count);
        Assert.Equal("/data/file.txt", node.Properties["path"]);
        Assert.Equal(1024L, node.Properties["size"]);
    }

    [Fact]
    public void Create_WithMetadata_SetsLineageContext()
    {
        var meta = new Dictionary<string, string>
        {
            ["lineage_source"] = "orders_db",
            ["lineage_pipeline"] = "etl_main",
        };

        var node = new Node
        {
            Id = "n1",
            Type = "Record",
            Metadata = meta,
        };

        Assert.Equal(2, node.Metadata.Count);
        Assert.Equal("orders_db", node.Metadata["lineage_source"]);
    }

    [Fact]
    public void Id_IsImmutable_AfterCreation()
    {
        var node = new Node { Id = "n1", Type = "File" };
        // Id is init-only, cannot be reassigned
        Assert.Equal("n1", node.Id);
    }

    [Fact]
    public void HealthScore_CanBeUpdated()
    {
        var node = new Node { Id = "n1", Type = "File" };

        node.HealthScore = 0.5;
        node.FailureProbability = 0.3;
        node.PredictedLatencyMs = 150.0;
        node.ResourceStrain = 0.7;

        Assert.Equal(0.5, node.HealthScore);
        Assert.Equal(0.3, node.FailureProbability);
        Assert.Equal(150.0, node.PredictedLatencyMs);
        Assert.Equal(0.7, node.ResourceStrain);
    }

    [Fact]
    public void Embedding_CanBeSet()
    {
        var node = new Node { Id = "n1", Type = "File" };
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };

        node.Embedding = embedding;

        Assert.NotNull(node.Embedding);
        Assert.Equal(3, node.Embedding.Length);
        Assert.Equal(0.2f, node.Embedding[1]);
    }

    [Fact]
    public void Bitemporal_ValidFromValidTo_WorkCorrectly()
    {
        var past = DateTime.UtcNow.AddDays(-7);
        var future = DateTime.UtcNow.AddDays(7);

        var node = new Node
        {
            Id = "n1",
            Type = "File",
            ValidFrom = past,
            ValidTo = future,
        };

        Assert.Equal(past, node.ValidFrom);
        Assert.Equal(future, node.ValidTo);
    }
}
