using SmartPipe.Memory.Diagnostics;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Tests.Diagnostics;

public sealed class MemoryMetricsTests
{
    [Fact]
    public void RecordQuery_DoesNotThrow()
    {
        var metrics = new MemoryMetrics();
        metrics.RecordQuery();
        // Если исключения нет, тест пройден
    }

    [Fact]
    public void SetNodesTotal_DoesNotThrow()
    {
        var metrics = new MemoryMetrics();
        metrics.SetNodesTotal(100);
    }

    [Fact]
    public void RecordCacheHit_DoesNotThrow()
    {
        var metrics = new MemoryMetrics();
        metrics.RecordCacheHit();
        metrics.RecordCacheMiss();
    }
}