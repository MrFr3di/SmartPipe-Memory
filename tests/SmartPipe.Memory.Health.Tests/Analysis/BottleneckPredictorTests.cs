using SmartPipe.Core;
using SmartPipe.Memory.Health.Analysis;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Health.Tests.Analysis;

public sealed class BottleneckPredictorTests : IAsyncDisposable
{
    private readonly InMemoryGraphStore _store;
    private readonly HealthVectorCalculator _calculator;
    private readonly BottleneckPredictor _predictor;

    public BottleneckPredictorTests()
    {
        _store = new InMemoryGraphStore();
        var clock = new TimeProviderClock();
        _calculator = new HealthVectorCalculator(_store, clock);
        _predictor = new BottleneckPredictor(
            _calculator,
            _store,
            latencyThresholdMs: 500,
            healthScoreThreshold: 0.3,
            clock
        );
    }

    [Fact]
    public async Task PredictAsync_HealthyNode_NotBottleneck()
    {
        var current = new MetricsEntry
        {
            NodeId = "n1",
            Timestamp = DateTime.UtcNow,
            Values = new Dictionary<string, double> { ["AvgLatencyMs"] = 100 },
        };
        var historical = new[]
        {
            new MetricsEntry
            {
                NodeId = "n1",
                Timestamp = DateTime.UtcNow.AddSeconds(-10),
                Values = new Dictionary<string, double> { ["AvgLatencyMs"] = 90 },
            },
        };

        var prediction = await _predictor.PredictAsync(
            "n1",
            current,
            historical,
            historical[0].Timestamp
        );

        Assert.False(prediction.IsBottleneck);
    }

    [Fact]
    public async Task PredictAsync_HighLatency_IsBottleneck()
    {
        var current = new MetricsEntry
        {
            NodeId = "n1",
            Timestamp = DateTime.UtcNow,
            Values = new Dictionary<string, double> { ["AvgLatencyMs"] = 600 },
        };
        var historical = new[]
        {
            new MetricsEntry
            {
                NodeId = "n1",
                Timestamp = DateTime.UtcNow.AddSeconds(-10),
                Values = new Dictionary<string, double> { ["AvgLatencyMs"] = 100 },
            },
        };

        var prediction = await _predictor.PredictAsync(
            "n1",
            current,
            historical,
            historical[0].Timestamp
        );

        Assert.True(prediction.IsBottleneck);
    }

    [Fact]
    public async Task PredictAsync_LowHealthScore_IsBottleneck()
    {
        var current = new MetricsEntry
        {
            NodeId = "n1",
            Timestamp = DateTime.UtcNow,
            Values = new Dictionary<string, double>
            {
                ["AvgLatencyMs"] = 600,
                ["ItemsFailed"] = 10,
            },
        };
        var historical = new[]
        {
            new MetricsEntry
            {
                NodeId = "n1",
                Timestamp = DateTime.UtcNow.AddSeconds(-10),
                Values = new Dictionary<string, double> { ["AvgLatencyMs"] = 90 },
            },
        };

        var prediction = await _predictor.PredictAsync(
            "n1",
            current,
            historical,
            historical[0].Timestamp
        );

        Assert.True(prediction.IsBottleneck);
    }

    [Fact]
    public async Task PredictAsync_StableLatency_InfiniteTimeToImpact()
    {
        var current = new MetricsEntry
        {
            NodeId = "n1",
            Timestamp = DateTime.UtcNow,
            Values = new Dictionary<string, double> { ["AvgLatencyMs"] = 100 },
        };
        var historical = new[]
        {
            new MetricsEntry
            {
                NodeId = "n1",
                Timestamp = DateTime.UtcNow.AddSeconds(-10),
                Values = new Dictionary<string, double> { ["AvgLatencyMs"] = 100 },
            },
        };

        var prediction = await _predictor.PredictAsync(
            "n1",
            current,
            historical,
            historical[0].Timestamp
        );

        Assert.True(double.IsPositiveInfinity(prediction.TimeToImpactMs));
    }

    public ValueTask DisposeAsync() => _store.DisposeAsync();
}
