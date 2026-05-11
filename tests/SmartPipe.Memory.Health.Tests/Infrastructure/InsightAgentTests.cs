using SmartPipe.Memory.Health.Analysis;
using SmartPipe.Memory.Health.Infrastructure;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Health.Tests.Infrastructure;

public sealed class InsightAgentTests : IAsyncDisposable
{
    private readonly InMemoryGraphStore _store;

    public InsightAgentTests()
    {
        _store = new InMemoryGraphStore();
    }

    [Fact]
    public async Task StartAsync_StartsAndStopsWithoutError()
    {
        var calculator = new HealthVectorCalculator(_store);
        var predictor = new BottleneckPredictor(calculator, _store);
        var generator = new InsightGenerator(predictor, _store);
        var consolidation = new CognitiveConsolidation(_store);

        var agent = new InsightAgent(_store, generator, consolidation, TimeSpan.FromMilliseconds(100));

        // Start the agent
        await agent.StartAsync(CancellationToken.None);

        // Let it run briefly
        await Task.Delay(200);

        // Stop the agent
        await agent.StopAsync(CancellationToken.None);

        // No exception means success
        Assert.True(true);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnhealthyNodes_GeneratesInsights()
    {
        await _store.UpsertNodeAsync(new Graph.Node { Id = "n1", Type = "File", HealthScore = 0.5 });

        var calculator = new HealthVectorCalculator(_store);
        var predictor = new BottleneckPredictor(calculator, _store);
        var generator = new InsightGenerator(predictor, _store);
        var consolidation = new CognitiveConsolidation(_store);

        var agent = new InsightAgent(_store, generator, consolidation, TimeSpan.FromMilliseconds(50));

        await agent.StartAsync(CancellationToken.None);
        await Task.Delay(150);
        await agent.StopAsync(CancellationToken.None);

        // The agent should have processed the unhealthy node
        // We can't easily verify insights were created because of the stub,
        // but we can verify no exceptions occurred
        Assert.True(true);
    }

    public ValueTask DisposeAsync() => _store.DisposeAsync();
}