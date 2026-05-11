using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Health.Diagnostics;

/// <summary>
/// Health check for SmartPipe.Memory.
/// Reports Healthy when the graph store is running and not faulted.
/// Reports Degraded when the store is draining.
/// Reports Unhealthy when the store is faulted.
/// </summary>
public sealed class MemoryHealthCheck
{
    private readonly IGraphStore _store;

    /// <summary>
    /// Create a new MemoryHealthCheck.
    /// </summary>
    /// <param name="store">Graph store to check.</param>
    public MemoryHealthCheck(IGraphStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// Check the health of the graph store.
    /// </summary>
    /// <returns>Health status.</returns>
    public MemoryHealthStatus Check()
    {
        return _store.State switch
        {
            StoreState.Running => MemoryHealthStatus.Healthy,
            StoreState.Draining => MemoryHealthStatus.Degraded,
            StoreState.Drained => MemoryHealthStatus.Healthy,
            StoreState.Faulted => MemoryHealthStatus.Unhealthy,
            _ => MemoryHealthStatus.Unhealthy
        };
    }

    /// <summary>
    /// Check the health of the graph store asynchronously.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Health status.</returns>
    public Task<MemoryHealthStatus> CheckAsync(CancellationToken ct = default)
    {
        return Task.FromResult(Check());
    }
}

/// <summary>
/// Health status for SmartPipe.Memory.
/// </summary>
public enum MemoryHealthStatus
{
    /// <summary>Store is running normally.</summary>
    Healthy,

    /// <summary>Store is draining, read-only mode.</summary>
    Degraded,

    /// <summary>Store is faulted.</summary>
    Unhealthy
}