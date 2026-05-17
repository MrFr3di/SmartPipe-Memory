namespace SmartPipe.Memory.Storage;

/// <summary>
/// Creates <see cref="IGraphStore"/> instances based on configuration.
/// </summary>
public static class StoreFactory
{
    /// <summary>
    /// Create an in-memory store for testing and development.
    /// </summary>
    /// <param name="metricsCapacity">Capacity of the metrics buffer channel.</param>
    /// <returns>An initialized <see cref="InMemoryGraphStore"/>.</returns>
    public static IGraphStore CreateInMemory(int metricsCapacity = 10000)
    {
        return new InMemoryGraphStore(metricsCapacity);
    }

    /// <summary>
    /// Create a SQLite-backed store for production.
    /// Call <see cref="SqliteWALStore.InitializeAsync"/> after creation.
    /// </summary>
    /// <param name="connectionString">Path to the SQLite database file.</param>
    /// <param name="metricsCapacity">Capacity of the metrics buffer channel.</param>
    /// <returns>A new <see cref="SqliteWALStore"/>. Requires initialization before use.</returns>
    public static SqliteWALStore CreateSqlite(
        string connectionString = "memory.db",
        int metricsCapacity = 10000
    )
    {
        return new SqliteWALStore(connectionString, metricsCapacity);
    }
}
