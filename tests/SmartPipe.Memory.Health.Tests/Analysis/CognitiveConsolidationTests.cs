using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Health.Analysis;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Health.Tests.Analysis;

public sealed class CognitiveConsolidationTests : IAsyncDisposable
{
    private readonly InMemoryGraphStore _store;
    private readonly CognitiveConsolidation _consolidation;

    public CognitiveConsolidationTests()
    {
        _store = new InMemoryGraphStore();
        _consolidation = new CognitiveConsolidation(_store, minOccurrences: 5);
    }

    [Fact]
    public async Task ConsolidateAsync_LessThanMinOccurrences_ReturnsOriginalInsight()
    {
        var insight = new Insight
        {
            Id = "i1",
            Type = "BottleneckPrediction",
            Title = "Test",
            RelatedNodeIds = new[] { "n1" },
            Confidence = 0.8,
            Severity = "Warning",
        };
        var existing = new List<Insight>
        {
            new Insight
            {
                Id = "i2",
                Type = "BottleneckPrediction",
                RelatedNodeIds = new[] { "n1" },
                Confidence = 0.7,
            },
        };

        var result = await _consolidation.ConsolidateAsync(insight, existing);

        Assert.Equal("i1", result.Id);
    }

    [Fact]
    public async Task ConsolidateAsync_EnoughOccurrences_CreatesConsolidatedInsight()
    {
        var insight = new Insight
        {
            Id = "i1",
            Type = "BottleneckPrediction",
            Title = "Test",
            RelatedNodeIds = new[] { "n1" },
            Confidence = 0.8,
            Severity = "Warning",
        };
        var existing = new List<Insight>();
        for (var i = 0; i < 5; i++)
            existing.Add(
                new Insight
                {
                    Id = $"e{i}",
                    Type = "BottleneckPrediction",
                    RelatedNodeIds = new[] { "n1" },
                    Confidence = 0.7,
                }
            );

        var result = await _consolidation.ConsolidateAsync(insight, existing);

        Assert.StartsWith("consolidated_", result.Id);
        Assert.True(result.Confidence > 0.8);
    }

    [Fact]
    public async Task ConsolidateAllByTypeAsync_EnoughOccurrences_ReturnsConsolidated()
    {
        var all = new List<Insight>();
        for (var i = 0; i < 6; i++)
            all.Add(
                new Insight
                {
                    Id = $"e{i}",
                    Type = "HealthDegradation",
                    RelatedNodeIds = new[] { "n1" },
                    Confidence = 0.7,
                }
            );

        var results = await _consolidation.ConsolidateAllByTypeAsync("HealthDegradation", all);

        Assert.Single(results);
    }

    public ValueTask DisposeAsync() => _store.DisposeAsync();
}
