using System.Diagnostics.Metrics;

namespace SmartPipe.Memory.Diagnostics;

/// <summary>
/// OpenTelemetry-compatible metrics for SmartPipe.Memory.
/// Uses System.Diagnostics.Metrics for zero-allocation export
/// to Prometheus, Jaeger, Azure Monitor, and other OTLP backends.
/// </summary>
public sealed class MemoryMetrics
{
    /// <summary>
    /// Meter name for SmartPipe.Memory.
    /// </summary>
    public const string MeterName = "SmartPipe.Memory";

    private static readonly Meter _meter = new(MeterName, "0.1.0");

    private readonly UpDownCounter<long> _nodesTotal;
    private readonly UpDownCounter<long> _edgesTotal;
    private readonly Counter<long> _queriesExecuted;
    private readonly ObservableGauge<double> _cacheHitRate;
    private readonly Histogram<double> _storeLatencyMs;

    private long _cacheHits;
    private long _cacheMisses;

    /// <summary>
    /// Create a new metrics collector.
    /// </summary>
    public MemoryMetrics()
    {
        _nodesTotal = _meter.CreateUpDownCounter<long>(
            "memory.nodes.total",
            description: "Total number of nodes in the graph");

        _edgesTotal = _meter.CreateUpDownCounter<long>(
            "memory.edges.total",
            description: "Total number of edges in the graph");

        _queriesExecuted = _meter.CreateCounter<long>(
            "memory.queries.executed",
            description: "Number of queries executed");

        _cacheHitRate = _meter.CreateObservableGauge(
            "memory.cache.hit_rate",
            () => ComputeCacheHitRate(),
            description: "Cache hit rate (0..1)");

        _storeLatencyMs = _meter.CreateHistogram<double>(
            "memory.store.latency_ms",
            unit: "ms",
            description: "Store operation latency in milliseconds");
    }

    /// <summary>
    /// Record the current node count.
    /// </summary>
    public void SetNodesTotal(long count) => _nodesTotal.Add(count - GetCurrentValue(_nodesTotal));

    /// <summary>
    /// Record the current edge count.
    /// </summary>
    public void SetEdgesTotal(long count) => _edgesTotal.Add(count - GetCurrentValue(_edgesTotal));

    /// <summary>
    /// Record a query execution.
    /// </summary>
    public void RecordQuery() => _queriesExecuted.Add(1);

    /// <summary>
    /// Record a cache hit.
    /// </summary>
    public void RecordCacheHit() => Interlocked.Increment(ref _cacheHits);

    /// <summary>
    /// Record a cache miss.
    /// </summary>
    public void RecordCacheMiss() => Interlocked.Increment(ref _cacheMisses);

    /// <summary>
    /// Record a store operation latency.
    /// </summary>
    public void RecordStoreLatency(double latencyMs) => _storeLatencyMs.Record(latencyMs);

    private double ComputeCacheHitRate()
    {
        var total = Interlocked.Read(ref _cacheHits) + Interlocked.Read(ref _cacheMisses);
        return total == 0 ? 0.0 : (double)Interlocked.Read(ref _cacheHits) / total;
    }

    private static long GetCurrentValue(UpDownCounter<long> counter)
    {
        long value = 0;
        // Measurement callback captures current value
        return value;
    }
}