using System.Threading.Channels;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Health;

/// <summary>
/// Buffered channel for metrics from synchronous OnMetrics callback.
/// Full implementation in v0.2.0.
/// </summary>
public sealed class MetricsChannel
{
    private readonly Channel<MetricsEntry> _channel;

    public MetricsChannel(int capacity = 10000)
    {
        _channel = Channel.CreateBounded<MetricsEntry>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    public ChannelWriter<MetricsEntry> Writer => _channel.Writer;
    public ChannelReader<MetricsEntry> Reader => _channel.Reader;
}