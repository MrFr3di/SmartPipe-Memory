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

    /// <summary>Creates a bounded metrics channel with DropOldest policy.</summary>
    /// <param name="capacity">Maximum number of pending metrics entries. Default 10000.</param>
    public MetricsChannel(int capacity = 10000)
    {
        _channel = Channel.CreateBounded<MetricsEntry>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    /// <summary>Writer for enqueuing metrics entries.</summary>
    public ChannelWriter<MetricsEntry> Writer => _channel.Writer;
    
    /// <summary>Reader for consuming metrics entries.</summary>
    public ChannelReader<MetricsEntry> Reader => _channel.Reader;
}