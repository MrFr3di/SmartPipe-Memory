using System.Diagnostics.Metrics;
using SmartPipe.Memory.Infrastructure;

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

    private static readonly Meter _meter = new(MeterName, "0.1.3");

    private readonly UpDownCounter<long> _nodesTotal;
    private readonly UpDownCounter<long> _edgesTotal;
    private readonly Counter<long> _queriesExecuted;
    private readonly Histogram<double> _storeLatencyMs;

    /// <summary>
    /// Padded counter for cache hits to avoid false sharing with _cacheMisses.
    /// </summary>
    private PaddedCounter64 _cacheHits;

    /// <summary>
    /// Padded counter for cache misses to avoid false sharing with _cacheHits.
    /// </summary>
    private PaddedCounter64 _cacheMisses;

    /// <summary>
    /// Create a new metrics collector.
    /// </summary>
    public MemoryMetrics()
    {
        _cacheHits = new PaddedCounter64();
        _cacheMisses = new PaddedCounter64();

        _nodesTotal = _meter.CreateUpDownCounter<long>(
            "memory.nodes.total",
            description: "Total number of nodes in the graph"
        );

        _edgesTotal = _meter.CreateUpDownCounter<long>(
            "memory.edges.total",
            description: "Total number of edges in the graph"
        );

        _queriesExecuted = _meter.CreateCounter<long>(
            "memory.queries.executed",
            description: "Number of queries executed"
        );

        // ObservableGauge does not need to be stored – Meter keeps it alive.
        _meter.CreateObservableGauge(
            "memory.cache.hit_rate",
            () => ComputeCacheHitRate(),
            description: "Cache hit rate (0..1)"
        );

        _storeLatencyMs = _meter.CreateHistogram<double>(
            "memory.store.latency_ms",
            unit: "ms",
            description: "Store operation latency in milliseconds"
        );
    }

    /// <summary>
    /// Record the current node count.
    /// </summary>
    /// <param name="count">Total node count to set.</param>
    public void SetNodesTotal(long count) => _nodesTotal.Add(count);

    /// <summary>
    /// Record the current edge count.
    /// </summary>
    /// <param name="count">Total edge count to set.</param>
    public void SetEdgesTotal(long count) => _edgesTotal.Add(count);

    /// <summary>
    /// Record a query execution.
    /// </summary>
    public void RecordQuery() => _queriesExecuted.Add(1);

    /// <summary>
    /// Record a cache hit.
    /// </summary>
    public void RecordCacheHit() => _cacheHits.Add(1);

    /// <summary>
    /// Record a cache miss.
    /// </summary>
    public void RecordCacheMiss() => _cacheMisses.Add(1);

    /// <summary>
    /// Record a store operation latency in milliseconds.
    /// </summary>
    /// <param name="latencyMs">Latency value in milliseconds.</param>
    public void RecordStoreLatency(double latencyMs) => _storeLatencyMs.Record(latencyMs);

    private double ComputeCacheHitRate()
    {
        var hits = _cacheHits.Value;
        var misses = _cacheMisses.Value;
        var total = hits + misses;
        return total == 0 ? 0.0 : (double)hits / total;
    }
}
