using Microsoft.Extensions.DependencyInjection;
using SmartPipe.Memory.Algorithms.Classification;
using SmartPipe.Memory.Caching;
using SmartPipe.Memory.Diagnostics;
using SmartPipe.Memory.Query;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory;

/// <summary>
/// Extension methods for registering SmartPipe.Memory services
/// in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register SmartPipe.Memory with an in-memory store.
    /// Suitable for testing and development.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration callback.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSmartPipeMemory(
        this IServiceCollection services,
        Action<MemoryConfiguration>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var config = new MemoryConfiguration();
        configure?.Invoke(config);

        // Store
        services.AddSingleton<IGraphStore>(_ =>
        {
            var store = StoreFactory.CreateInMemory(config.MetricsBufferCapacity);
            if (config.EnableAutoClassification && store is InMemoryGraphStore memoryStore)
            {
                memoryStore.Classifier = new AutoClassifier();
            }
            return store;
        });

        // Cache
        services.AddSingleton(_ => new NodeCache(config.MaxCacheSize));

        // Query engine
        services.AddSingleton(sp =>
        {
            var store = sp.GetRequiredService<IGraphStore>();
            var cache = sp.GetRequiredService<NodeCache>();
            var metrics = sp.GetService<MemoryMetrics>();
            return new MemoryQueryExecutor(store, cache, metrics);
        });

        services.AddSingleton(sp =>
        {
            var executor = sp.GetRequiredService<MemoryQueryExecutor>();
            return new MemoryQueryBuilder(executor);
        });

        // Diagnostics
        services.AddSingleton<MemoryMetrics>();
        MemoryEventSource.EnableForTesting();

        return services;
    }

    /// <summary>
    /// Register SmartPipe.Memory with a SQLite-backed store.
    /// Suitable for production use.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration callback.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSmartPipeMemorySqlite(
        this IServiceCollection services,
        Action<MemoryConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var config = new MemoryConfiguration();
        configure(config);

        // Store
        services.AddSingleton<IGraphStore>(sp =>
        {
            var store = StoreFactory.CreateSqlite(
                config.ConnectionString,
                config.MetricsBufferCapacity);
            if (config.EnableAutoClassification && store is SqliteWALStore sqliteStore)
            {
                sqliteStore.Classifier = new AutoClassifier();
            }
            store.InitializeAsync().GetAwaiter().GetResult();
            return store;
        });

        // Cache
        services.AddSingleton(_ => new NodeCache(config.MaxCacheSize));

        // Query engine
        services.AddSingleton(sp =>
        {
            var store = sp.GetRequiredService<IGraphStore>();
            var cache = sp.GetRequiredService<NodeCache>();
            var metrics = sp.GetService<MemoryMetrics>();
            return new MemoryQueryExecutor(store, cache, metrics);
        });

        services.AddSingleton(sp =>
        {
            var executor = sp.GetRequiredService<MemoryQueryExecutor>();
            return new MemoryQueryBuilder(executor);
        });

        // Diagnostics
        services.AddSingleton<MemoryMetrics>();
        MemoryEventSource.EnableForTesting();

        return services;
    }
}

/// <summary>
/// Configuration options for SmartPipe.Memory.
/// </summary>
public sealed class MemoryConfiguration
{
    /// <summary>
    /// SQLite connection string. Default: "memory.db".
    /// </summary>
    public string ConnectionString { get; set; } = "memory.db";

    /// <summary>
    /// Maximum number of nodes in the LRU cache. Default: 10000.
    /// </summary>
    public int MaxCacheSize { get; set; } = 10000;

    /// <summary>
    /// Capacity of the metrics buffer channel. Default: 10000.
    /// </summary>
    public int MetricsBufferCapacity { get; set; } = 10000;

    /// <summary>
    /// Enable automatic classification of nodes on upsert.
    /// When true, nodes with empty Type will be classified using AutoClassifier.
    /// Default: false.
    /// </summary>
    public bool EnableAutoClassification { get; set; }
}