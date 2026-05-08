using SmartPipe.Core;

namespace SmartPipe.Memory.Infrastructure;

/// <summary>
/// Object pools for reusing frequently created graph objects.
/// Reduces GC pressure during high-throughput pipeline execution.
/// Note: Node and Edge have init-only properties, so Reset methods
/// are not provided. Objects are returned to pool and recreated.
/// </summary>
public static class MemoryPools
{
    /// <summary>
    /// Pool for <see cref="Graph.Node"/> instances.
    /// </summary>
    public static ObjectPool<Graph.Node> NodePool { get; } = new(
        factory: () => new Graph.Node(),
        capacity: 256);

    /// <summary>
    /// Pool for <see cref="Graph.Edge"/> instances.
    /// </summary>
    public static ObjectPool<Graph.Edge> EdgePool { get; } = new(
        factory: () => new Graph.Edge(),
        capacity: 256);
}