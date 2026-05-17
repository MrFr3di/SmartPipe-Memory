using SmartPipe.Core;
using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Health.Analysis;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Health.Tests.Analysis;

public sealed class HealthVectorCalculatorTests : IAsyncDisposable
{
    private readonly InMemoryGraphStore _store;
    private readonly HealthVectorCalculator _calculator;

    public HealthVectorCalculatorTests()
    {
        _store = new InMemoryGraphStore();
        var clock = new TimeProviderClock();
        _calculator = new HealthVectorCalculator(_store, clock);
    }

    [Fact]
    public async Task ComputeAsync_EmptyHistory_ReturnsDefaultHealth()
    {
        var health = await _calculator.ComputeAsync("n1", Array.Empty<MetricsEntry>(), 3);

        Assert.Equal(1.0, health.HealthScore);
        Assert.Equal(0, health.PredictedLatencyMs);
    }

    [Fact]
    public async Task ComputeAsync_SingleSnapshot_ComputesCorrectly()
    {
        var snapshot = new MetricsEntry
        {
            NodeId = "n1",
            Timestamp = DateTime.UtcNow,
            Values = new Dictionary<string, double>
            {
                ["AvgLatencyMs"] = 100,
                ["SmoothThroughput"] = 50,
                ["ItemsFailed"] = 0,
            },
        };

        var health = await _calculator.ComputeAsync("n1", new[] { snapshot }, 3);

        Assert.True(health.P50LatencyMs > 0);
        Assert.True(health.P95LatencyMs > 0);
        Assert.True(health.P99LatencyMs > 0);
        Assert.Equal(0, health.FailureProbability);
    }

    [Fact]
    public async Task ComputeAsync_WithFailures_FailureProbabilityAboveZero()
    {
        var snapshots = new[]
        {
            new MetricsEntry
            {
                NodeId = "n1",
                Timestamp = DateTime.UtcNow,
                Values = new Dictionary<string, double> { ["ItemsFailed"] = 1 },
            },
            new MetricsEntry
            {
                NodeId = "n1",
                Timestamp = DateTime.UtcNow,
                Values = new Dictionary<string, double> { ["ItemsFailed"] = 1 },
            },
            new MetricsEntry
            {
                NodeId = "n1",
                Timestamp = DateTime.UtcNow,
                Values = new Dictionary<string, double> { ["ItemsFailed"] = 0 },
            },
        };

        var health = await _calculator.ComputeAsync("n1", snapshots, 3);

        Assert.True(health.FailureProbability > 0);
    }

    [Fact]
    public async Task ComputeAsync_WithWeakenedEdges_ResourceStrainAboveZero()
    {
        await _store.UpsertNodeAsync(new Node { Id = "n1", Type = "File" });
        await _store.UpsertNodeAsync(new Node { Id = "n2", Type = "File" });
        await _store.UpsertEdgeAsync(
            new Edge
            {
                FromNodeId = "n1",
                ToNodeId = "n2",
                Type = EdgeType.DerivedFrom,
                Weight = 0.1,
            }
        );

        var snapshot = new MetricsEntry
        {
            NodeId = "n1",
            Timestamp = DateTime.UtcNow,
            Values = new Dictionary<string, double> { ["AvgLatencyMs"] = 100 },
        };

        var health = await _calculator.ComputeAsync("n1", new[] { snapshot }, 3);

        Assert.True(health.ResourceStrain > 0);
    }

    [Fact]
    public async Task ComputeAsync_MultipleSnapshots_PercentilesCorrect()
    {
        var snapshots = new[]
        {
            new MetricsEntry
            {
                NodeId = "n1",
                Timestamp = DateTime.UtcNow,
                Values = new Dictionary<string, double> { ["AvgLatencyMs"] = 10 },
            },
            new MetricsEntry
            {
                NodeId = "n1",
                Timestamp = DateTime.UtcNow,
                Values = new Dictionary<string, double> { ["AvgLatencyMs"] = 20 },
            },
            new MetricsEntry
            {
                NodeId = "n1",
                Timestamp = DateTime.UtcNow,
                Values = new Dictionary<string, double> { ["AvgLatencyMs"] = 30 },
            },
            new MetricsEntry
            {
                NodeId = "n1",
                Timestamp = DateTime.UtcNow,
                Values = new Dictionary<string, double> { ["AvgLatencyMs"] = 40 },
            },
            new MetricsEntry
            {
                NodeId = "n1",
                Timestamp = DateTime.UtcNow,
                Values = new Dictionary<string, double> { ["AvgLatencyMs"] = 50 },
            },
        };

        var health = await _calculator.ComputeAsync("n1", snapshots, 3);

        Assert.True(health.P50LatencyMs > 0);
        Assert.True(health.P95LatencyMs > 0);
        Assert.True(health.P99LatencyMs > 0);
    }

    public ValueTask DisposeAsync() => _store.DisposeAsync();
}
