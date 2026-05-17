namespace SmartPipe.Memory.Infrastructure;

/// <summary>
/// Default values and constants used across SmartPipe.Memory.
/// </summary>
public static class MemoryDefaults
{
    /// <summary>Default maximum cache size.</summary>
    public const int MaxCacheSize = 10000;

    /// <summary>Default maximum traversal depth.</summary>
    public const int MaxQueryDepth = 10;

    /// <summary>Default metrics buffer capacity.</summary>
    public const int MetricsBufferCapacity = 10000;

    /// <summary>Default object pool capacity.</summary>
    public const int ObjectPoolCapacity = 256;

    /// <summary>Default SQLite database file name.</summary>
    public const string DefaultDatabaseName = "memory.db";

    /// <summary>Default health score threshold for degradation.</summary>
    public const double DegradedHealthThreshold = 0.7;

    /// <summary>Default edge weight threshold for weakening.</summary>
    public const double WeakenedEdgeThreshold = 0.3;
}
