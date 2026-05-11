using SmartPipe.Memory.Health.Diagnostics;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Health.Tests.Diagnostics;

public sealed class MemoryHealthCheckTests
{
    [Fact]
    public void Check_Running_ReturnsHealthy()
    {
        var store = new InMemoryGraphStore();
        var check = new MemoryHealthCheck(store);

        Assert.Equal(MemoryHealthStatus.Healthy, check.Check());
    }

    [Fact]
    public async Task Check_AfterDrain_ReturnsHealthy()
    {
        var store = new InMemoryGraphStore();
        var check = new MemoryHealthCheck(store);
        await store.DrainAsync();

        Assert.Equal(MemoryHealthStatus.Healthy, check.Check());
    }
}