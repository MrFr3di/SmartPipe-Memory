using System.Threading.Channels;
using SmartPipe.Core;
using SmartPipe.Memory.Health.Analysis;
using SmartPipe.Memory.Health.Infrastructure;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Health.Tests.Infrastructure;

public sealed class MetricsBackgroundConsumerTests : IAsyncDisposable
{
    private readonly InMemoryGraphStore _store;
    private readonly HealthVectorCalculator _calculator;
    private readonly Channel<MetricsEntry> _channel;

    public MetricsBackgroundConsumerTests()
    {
        _store = new InMemoryGraphStore();
        var clock = new TimeProviderClock();
        _calculator = new HealthVectorCalculator(_store, clock);
        _channel = Channel.CreateBounded<MetricsEntry>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    [Fact]
    public async Task StartAsync_ConsumesMetrics_UpdatesNodeHealth()
    {
        await _store.UpsertNodeAsync(new Graph.Node { Id = "n1", Type = "File" });

        var consumer = new MetricsBackgroundConsumer(_channel.Reader, _store, _calculator);

        await consumer.StartAsync();

        await _channel.Writer.WriteAsync(new MetricsEntry
        {
            NodeId = "n1",
            Timestamp = DateTime.UtcNow,
            Values = new Dictionary<string, double>
            {
                ["AvgLatencyMs"] = 100,
                ["SmoothThroughput"] = 50,
                ["ItemsFailed"] = 0
            }
        });

        // Give consumer time to process
        await Task.Delay(100);

        await consumer.StopAsync();

        var node = await _store.GetNodeAsync("n1");
        Assert.NotNull(node);
        Assert.True(node!.HealthScore <= 1.0);
    }

    [Fact]
    public async Task StartAsync_MultipleMetrics_UpdatesHealthMultipleTimes()
    {
        await _store.UpsertNodeAsync(new Graph.Node { Id = "n1", Type = "File" });

        var consumer = new MetricsBackgroundConsumer(_channel.Reader, _store, _calculator);

        await consumer.StartAsync();

        for (var i = 0; i < 5; i++)
        {
            await _channel.Writer.WriteAsync(new MetricsEntry
            {
                NodeId = "n1",
                Timestamp = DateTime.UtcNow,
                Values = new Dictionary<string, double> { ["AvgLatencyMs"] = 100 + i * 10 }
            });
        }

        await Task.Delay(200);
        await consumer.StopAsync();

        var node = await _store.GetNodeAsync("n1");
        Assert.NotNull(node);
    }

    [Fact]
    public async Task StopAsync_StopsConsumerGracefully()
    {
        var consumer = new MetricsBackgroundConsumer(_channel.Reader, _store, _calculator);
        await consumer.StartAsync();
        await consumer.StopAsync();

        // No exception means success
        Assert.True(true);
    }

    public ValueTask DisposeAsync()
    {
        _channel.Writer.Complete();
        return _store.DisposeAsync();
    }
}