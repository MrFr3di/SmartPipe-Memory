using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Health;

/// <summary>
/// Background service that consumes metrics from the channel and writes to the store.
/// Full implementation in v0.2.0.
/// </summary>
public sealed class MetricsBackgroundConsumer
{
    private readonly MetricsChannel _channel;
    private readonly IGraphStore _store;

    public MetricsBackgroundConsumer(MetricsChannel channel, IGraphStore store)
    {
        _channel = channel;
        _store = store;
    }

    // Full implementation in v0.2.0
}