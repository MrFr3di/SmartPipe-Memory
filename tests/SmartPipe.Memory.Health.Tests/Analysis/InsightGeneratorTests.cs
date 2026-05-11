using SmartPipe.Core;
using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Health.Analysis;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Health.Tests.Analysis;

public sealed class InsightGeneratorTests : IAsyncDisposable
{
    private readonly InMemoryGraphStore _store;
    private readonly HealthVectorCalculator _calculator;
    private readonly BottleneckPredictor _predictor;
    private readonly InsightGenerator _generator;

    public InsightGeneratorTests()
    {
        _store = new InMemoryGraphStore();
        var clock = new TimeProviderClock();
        _calculator = new HealthVectorCalculator(_store, clock);
        _predictor = new BottleneckPredictor(_calculator, _store, clock: clock);
        _generator = new InsightGenerator(_predictor, _store);
    }

    [Fact]
    public async Task GenerateFromPredictionAsync_Bottleneck_CreatesInsight()
    {
        var prediction = new BottleneckPrediction
        {
            NodeId = "n1",
            IsBottleneck = true,
            Confidence = 0.9,
            TimeToImpactMs = 5000,
            HealthDelta = 0.2,
            LatencyDelta = 100
        };

        var insight = await _generator.GenerateFromPredictionAsync(prediction);

        Assert.NotNull(insight);
        Assert.Equal("BottleneckPrediction", insight.Type);
        Assert.Equal("Critical", insight.Severity);
        Assert.Contains("n1", insight.RelatedNodeIds);
    }

    [Fact]
    public async Task AnalyzeNodeAsync_UnhealthyNode_ReturnsInsight()
    {
        var current = new MetricsEntry
        {
            NodeId = "n1",
            Timestamp = DateTime.UtcNow,
            Values = new Dictionary<string, double> { ["AvgLatencyMs"] = 600 }
        };
        var historical = new[]
        {
            new MetricsEntry { NodeId = "n1", Timestamp = DateTime.UtcNow.AddSeconds(-10), Values = new Dictionary<string, double> { ["AvgLatencyMs"] = 100 } }
        };

        var insight = await _generator.AnalyzeNodeAsync("n1", current, historical);

        Assert.NotNull(insight);
    }

    [Fact]
    public async Task AnalyzeNodeAsync_HealthyNode_ReturnsNull()
    {
        var current = new MetricsEntry
        {
            NodeId = "n1",
            Timestamp = DateTime.UtcNow,
            Values = new Dictionary<string, double> { ["AvgLatencyMs"] = 100 }
        };
        var historical = new[]
        {
            new MetricsEntry { NodeId = "n1", Timestamp = DateTime.UtcNow.AddSeconds(-10), Values = new Dictionary<string, double> { ["AvgLatencyMs"] = 90 } }
        };

        var insight = await _generator.AnalyzeNodeAsync("n1", current, historical);

        Assert.Null(insight);
    }

    [Fact]
    public async Task GenerateRetryBudgetExhaustedAsync_CreatesInsight()
    {
        var insight = await _generator.GenerateRetryBudgetExhaustedAsync("n1", 0);

        Assert.Equal("RetryBudgetExhausted", insight.Type);
        Assert.Equal("Warning", insight.Severity);
    }

    [Fact]
    public async Task GenerateClusterDiscoveredAsync_CreatesInsight()
    {
        var cluster = new Cluster { Id = "1", NodeIds = new[] { "A", "B", "C" }, Modularity = 0.8 };

        var insight = await _generator.GenerateClusterDiscoveredAsync(cluster);

        Assert.Equal("ClusterDiscovered", insight.Type);
        Assert.Equal("Info", insight.Severity);
        Assert.Equal(3, insight.RelatedNodeIds.Count);
    }

    public ValueTask DisposeAsync() => _store.DisposeAsync();
}