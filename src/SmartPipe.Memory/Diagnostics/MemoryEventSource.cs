using System.Diagnostics.Tracing;

namespace SmartPipe.Memory.Diagnostics;

/// <summary>
/// EventCounters for monitoring SmartPipe.Memory without an OTLP collector.
/// Usage: dotnet-counters monitor --process-id PID SmartPipe.Memory
/// </summary>
[EventSource(Name = "SmartPipe.Memory")]
public sealed class MemoryEventSource : EventSource
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly MemoryEventSource Log = new();

    private readonly IncrementingPollingCounter _queriesPerSecondCounter;
    private readonly PollingCounter _nodesTotalCounter;
    private readonly PollingCounter _cacheHitRateCounter;

    private long _queriesPerSecond;
    private long _nodesTotal;
    private double _cacheHitRate;

    private MemoryEventSource()
    {
        _queriesPerSecondCounter = new IncrementingPollingCounter(
            "queries-per-second",
            this,
            () => _queriesPerSecond
        )
        {
            DisplayName = "Queries per second",
            DisplayUnits = "ops/sec",
        };

        _nodesTotalCounter = new PollingCounter("nodes-total", this, () => _nodesTotal)
        {
            DisplayName = "Total nodes",
            DisplayUnits = "nodes",
        };

        _cacheHitRateCounter = new PollingCounter("cache-hit-rate", this, () => _cacheHitRate)
        {
            DisplayName = "Cache hit rate",
            DisplayUnits = "%",
        };
    }

    /// <summary>
    /// Record a query execution.
    /// </summary>
    public void RecordQuery() => Interlocked.Increment(ref _queriesPerSecond);

    /// <summary>
    /// Set the current node count.
    /// </summary>
    public void SetNodesTotal(long count) => Interlocked.Exchange(ref _nodesTotal, count);

    /// <summary>
    /// Set the current cache hit rate.
    /// </summary>
    public void SetCacheHitRate(double hitRate) => Interlocked.Exchange(ref _cacheHitRate, hitRate);

    /// <summary>
    /// Enable for testing purposes.
    /// </summary>
    public static void EnableForTesting()
    {
        // Forces initialization of EventCounters
    }
}
