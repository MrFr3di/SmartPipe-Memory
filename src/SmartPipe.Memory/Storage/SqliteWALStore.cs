using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Data.Sqlite;
using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Model;
using SmartPipe.Memory.Algorithms.Classification;


namespace SmartPipe.Memory.Storage;

/// <summary>
/// SQLite-backed graph store with WAL mode for persistence.
/// All graph traversals execute in memory via InMemoryGraphStore.
/// SQLite is used only for durability (write-ahead log).
/// </summary>
public sealed class SqliteWALStore : IGraphStore
{
    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _asyncLock = new(1, 1);
    private readonly InMemoryGraphStore _memoryStore;

    private readonly Channel<MetricsEntry> _metricsChannel;
    private readonly List<Insight> _insights = new();
    private StoreState _state = StoreState.Running;

    /// <summary>
    /// Create a new SQLite-backed graph store.
    /// </summary>
    /// <param name="connectionString">Path to the SQLite database file.</param>
    /// <param name="metricsCapacity">Capacity of the metrics buffer channel.</param>
    public SqliteWALStore(string connectionString = "memory.db", int metricsCapacity = 10000)
    {
        _connection = new SqliteConnection($"Data Source={connectionString}");
        _memoryStore = new InMemoryGraphStore(metricsCapacity);
        _metricsChannel = Channel.CreateBounded<MetricsEntry>(new BoundedChannelOptions(metricsCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    /// <inheritdoc />
    public StoreState State => _state;

    /// <inheritdoc />
    public bool IsDraining => _state is StoreState.Draining or StoreState.Drained;

    /// <summary>
    /// Optional classifier for nodes. Delegates to the in-memory store.
    /// </summary>
    public AutoClassifier? Classifier
    {
        get => _memoryStore.Classifier;
        set => _memoryStore.Classifier = value;
    }

    /// <inheritdoc />
    public ChannelWriter<MetricsEntry> MetricsChannel => _metricsChannel.Writer;

    /// <summary>
    /// Open connection, create schema, load all data into memory.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _connection.OpenAsync(ct);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = SqliteSchema.CreateTables;
        await cmd.ExecuteNonQueryAsync(ct);

        await LoadAllNodesAsync(ct);
        await LoadAllEdgesAsync(ct);
    }

    // -- Nodes --

    /// <inheritdoc />
    public async Task<Node> UpsertNodeAsync(Node node, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ThrowIfNotRunning();

        await _asyncLock.WaitAsync(ct);
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO nodes (id, type, label, properties, health_score, failure_prob, predicted_latency_ms, resource_strain, valid_from, valid_to, tx_time, version)
                VALUES (@id, @type, @label, @properties, @healthScore, @failureProb, @predictedLatencyMs, @resourceStrain, @validFrom, @validTo, @txTime, @version)
                ON CONFLICT(id) DO UPDATE SET
                    type = excluded.type,
                    label = excluded.label,
                    properties = excluded.properties,
                    health_score = excluded.health_score,
                    failure_prob = excluded.failure_prob,
                    predicted_latency_ms = excluded.predicted_latency_ms,
                    resource_strain = excluded.resource_strain,
                    valid_to = excluded.valid_to,
                    tx_time = excluded.tx_time,
                    version = excluded.version";

            cmd.Parameters.AddWithValue("@id", node.Id);
            cmd.Parameters.AddWithValue("@type", node.Type);
            cmd.Parameters.AddWithValue("@label", node.Label);
            cmd.Parameters.AddWithValue("@properties", JsonSerializer.Serialize(node.Properties));
            cmd.Parameters.AddWithValue("@healthScore", node.HealthScore);
            cmd.Parameters.AddWithValue("@failureProb", node.FailureProbability);
            cmd.Parameters.AddWithValue("@predictedLatencyMs", node.PredictedLatencyMs);
            cmd.Parameters.AddWithValue("@resourceStrain", node.ResourceStrain);
            cmd.Parameters.AddWithValue("@validFrom", node.ValidFrom.ToUniversalTime().ToString("O"));
            cmd.Parameters.AddWithValue("@validTo", node.ValidTo?.ToUniversalTime().ToString("O") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@txTime", DateTime.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("@version", node.Version + 1);

            await cmd.ExecuteNonQueryAsync(ct);

            return await _memoryStore.UpsertNodeAsync(node, ct);
        }
        finally
        {
            _asyncLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task BatchUpsertNodesAsync(IAsyncEnumerable<Node> nodes, CancellationToken ct = default)
    {
        ThrowIfNotRunning();

        await _asyncLock.WaitAsync(ct);
        try
        {
            var batch = new List<Node>(100);

            await foreach (var node in nodes.WithCancellation(ct))
            {
                batch.Add(node);

                if (batch.Count >= 100)
                {
                    await InsertBatchAsync(batch, ct);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
                await InsertBatchAsync(batch, ct);
        }
        finally
        {
            _asyncLock.Release();
        }
    }

    private async Task InsertBatchAsync(List<Node> batch, CancellationToken ct)
    {
        using var cmd = _connection.CreateCommand();
        var values = new List<string>();

        for (var i = 0; i < batch.Count; i++)
        {
            var node = batch[i];
            values.Add($"(@id{i}, @type{i}, @label{i}, @props{i}, @health{i}, @fail{i}, @pred{i}, @strain{i}, @vfrom{i}, @vto{i}, @txtime{i}, @ver{i})");

            cmd.Parameters.AddWithValue($"@id{i}", node.Id);
            cmd.Parameters.AddWithValue($"@type{i}", node.Type);
            cmd.Parameters.AddWithValue($"@label{i}", node.Label);
            cmd.Parameters.AddWithValue($"@props{i}", JsonSerializer.Serialize(node.Properties));
            cmd.Parameters.AddWithValue($"@health{i}", node.HealthScore);
            cmd.Parameters.AddWithValue($"@fail{i}", node.FailureProbability);
            cmd.Parameters.AddWithValue($"@pred{i}", node.PredictedLatencyMs);
            cmd.Parameters.AddWithValue($"@strain{i}", node.ResourceStrain);
            cmd.Parameters.AddWithValue($"@vfrom{i}", node.ValidFrom.ToString("O"));
            cmd.Parameters.AddWithValue($"@vto{i}", node.ValidTo?.ToString("O") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue($"@txtime{i}", DateTime.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue($"@ver{i}", node.Version);
        }

        cmd.CommandText = $@"
            INSERT INTO nodes (id, type, label, properties, health_score, failure_prob, predicted_latency_ms, resource_strain, valid_from, valid_to, tx_time, version)
            VALUES {string.Join(", ", values)}";

        await cmd.ExecuteNonQueryAsync(ct);

        foreach (var node in batch)
            await _memoryStore.UpsertNodeAsync(node, ct);
    }

    /// <inheritdoc />
    public Task<Node?> GetNodeAsync(string nodeId, CancellationToken ct = default)
        => _memoryStore.GetNodeAsync(nodeId, ct);

    /// <inheritdoc />
    public async Task DeleteNodeAsync(string nodeId, CancellationToken ct = default)
    {
        ThrowIfNotRunning();

        await _asyncLock.WaitAsync(ct);
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM nodes WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", nodeId);
            await cmd.ExecuteNonQueryAsync(ct);

            await _memoryStore.DeleteNodeAsync(nodeId, ct);
        }
        finally
        {
            _asyncLock.Release();
        }
    }

    // -- Edges --

    /// <inheritdoc />
    public async Task<Edge> UpsertEdgeAsync(Edge edge, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(edge);
        ThrowIfNotRunning();

        await _asyncLock.WaitAsync(ct);
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO edges (from_node_id, to_node_id, type, weight, confidence, steps, valid_from, valid_to, tx_time)
                VALUES (@fromId, @toId, @type, @weight, @confidence, @steps, @validFrom, @validTo, @txTime)
                ON CONFLICT(from_node_id, to_node_id, type) DO UPDATE SET
                    weight = excluded.weight,
                    confidence = excluded.confidence,
                    steps = excluded.steps,
                    valid_to = excluded.valid_to,
                    tx_time = excluded.tx_time";

            cmd.Parameters.AddWithValue("@fromId", edge.FromNodeId);
            cmd.Parameters.AddWithValue("@toId", edge.ToNodeId);
            cmd.Parameters.AddWithValue("@type", edge.Type.ToString());
            cmd.Parameters.AddWithValue("@weight", edge.Weight);
            cmd.Parameters.AddWithValue("@confidence", edge.Confidence);
            cmd.Parameters.AddWithValue("@steps", JsonSerializer.Serialize(edge.Steps));
            cmd.Parameters.AddWithValue("@validFrom", edge.ValidFrom.ToString("O"));
            cmd.Parameters.AddWithValue("@validTo", edge.ValidTo?.ToString("O") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@txTime", DateTime.UtcNow.ToString("O"));

            await cmd.ExecuteNonQueryAsync(ct);

            return await _memoryStore.UpsertEdgeAsync(edge, ct);
        }
        finally
        {
            _asyncLock.Release();
        }
    }

    /// <inheritdoc />
    public Task DeleteEdgeAsync(long edgeId, CancellationToken ct = default)
        => _memoryStore.DeleteEdgeAsync(edgeId, ct);

    // -- Queries (delegate to in-memory) --

    /// <inheritdoc />
    public IAsyncEnumerable<Node> QueryNodesAsync(MemoryQuery query, CancellationToken ct = default)
        => _memoryStore.QueryNodesAsync(query, ct);

    /// <inheritdoc />
    public IAsyncEnumerable<Node> QueryNodesAsOfAsync(MemoryQuery query, DateTime asOf, CancellationToken ct = default)
        => _memoryStore.QueryNodesAsOfAsync(query, asOf, ct);

    /// <inheritdoc />
    public IAsyncEnumerable<Edge> QueryEdgesAsOfAsync(MemoryQuery query, DateTime asOf, CancellationToken ct = default)
        => _memoryStore.QueryEdgesAsOfAsync(query, asOf, ct);

    /// <inheritdoc />
    public Task<IReadOnlyList<PathSegment>> FindPathAsync(
        string fromNodeId, string toNodeId, string edgeType, int maxDepth,
        Func<Node, bool>? nodeFilter = null,
        double? minWeight = null,
        double? minConfidence = null,
        CancellationToken ct = default)
        => _memoryStore.FindPathAsync(fromNodeId, toNodeId, edgeType, maxDepth, nodeFilter, minWeight, minConfidence, ct);

    /// <inheritdoc />
    public IAsyncEnumerable<(Node Node, int Depth)> TraverseAsync(
        string startNodeId, string edgeType, int maxDepth, int limit,
        Func<Node, bool>? nodeFilter = null,
        double? minWeight = null,
        double? minConfidence = null,
        CancellationToken ct = default)
        => _memoryStore.TraverseAsync(startNodeId, edgeType, maxDepth, limit, nodeFilter, minWeight, minConfidence, ct);

    /// <inheritdoc />
    public IAsyncEnumerable<Edge> QueryInsightsAsync(MemoryQuery query, CancellationToken ct = default)
        => _memoryStore.QueryInsightsAsync(query, ct);

    /// <inheritdoc />
    public Task<IReadOnlyList<Cluster>> ClusterAsync(CancellationToken ct = default)
        => _memoryStore.ClusterAsync(ct);
    
    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyList<Edge>> GetOutEdges()
    => _memoryStore.GetOutEdges();

    /// <inheritdoc />
    public Task<IReadOnlyList<Edge>> GetWeakenedEdgesFromAsync(string nodeId, CancellationToken ct = default)
        => _memoryStore.GetWeakenedEdgesFromAsync(nodeId, ct);

    /// <inheritdoc />
    public Task InsertInsightAsync(Insight insight, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(insight);
        _insights.Add(insight);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateNodeHealthAsync(
        string nodeId, double healthScore, double failureProb,
        double predictedLatencyMs, double resourceStrain, int expectedVersion, CancellationToken ct = default)
        => _memoryStore.UpdateNodeHealthAsync(nodeId, healthScore, failureProb,
            predictedLatencyMs, resourceStrain, expectedVersion, ct);

    // -- Lifecycle --

    /// <inheritdoc />
    public Task DrainAsync(CancellationToken ct = default)
    {
        _state = StoreState.Draining;
        _metricsChannel.Writer.Complete();
        _state = StoreState.Drained;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
{
        // Flush WAL to main database and truncate the wal file
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
            await cmd.ExecuteNonQueryAsync();
        }

        SqliteConnection.ClearPool(_connection);
        SqliteConnection.ClearAllPools();
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
        _asyncLock.Dispose();
        await _memoryStore.DisposeAsync();
    }

    // -- Private helpers --

    private async Task LoadAllNodesAsync(CancellationToken ct)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM nodes WHERE valid_to IS NULL";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var node = ReadNode(reader);
            await _memoryStore.UpsertNodeAsync(node, ct);
        }
    }

    private async Task LoadAllEdgesAsync(CancellationToken ct)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM edges WHERE valid_to IS NULL";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var edge = ReadEdge(reader);
            await _memoryStore.UpsertEdgeAsync(edge, ct);
        }
    }

    private static Node ReadNode(SqliteDataReader reader)
    {
        return new Node
        {
            Id = reader.GetString(0),
            Type = reader.GetString(1),
            Label = reader.GetString(2),
            Properties = JsonSerializer.Deserialize<Dictionary<string, object>>(reader.GetString(3)) ?? [],
            HealthScore = reader.GetDouble(4),
            FailureProbability = reader.GetDouble(5),
            PredictedLatencyMs = reader.GetDouble(6),
            ResourceStrain = reader.GetDouble(7),
            ValidFrom = DateTimeOffset.Parse(reader.GetString(8)).UtcDateTime,
            ValidTo = reader.IsDBNull(9) ? null : DateTimeOffset.Parse(reader.GetString(9)).UtcDateTime,
            TxTime = DateTimeOffset.Parse(reader.GetString(10)).UtcDateTime,
            Version = reader.GetInt32(11)
        };
    }

    private static Edge ReadEdge(SqliteDataReader reader)
    {
        return new Edge
        {
            Id = reader.GetInt64(0),
            FromNodeId = reader.GetString(1),
            ToNodeId = reader.GetString(2),
            Type = Enum.Parse<EdgeType>(reader.GetString(3)),
            Weight = reader.GetDouble(4),
            Confidence = reader.GetDouble(5),
            Steps = JsonSerializer.Deserialize<List<TransformationStep>>(reader.GetString(6)) ?? [],
            ValidFrom = DateTimeOffset.Parse(reader.GetString(7)).UtcDateTime,
            ValidTo = reader.IsDBNull(8) ? null : DateTimeOffset.Parse(reader.GetString(8)).UtcDateTime,
            TxTime = DateTimeOffset.Parse(reader.GetString(9)).UtcDateTime,
        };
    }

    private void ThrowIfNotRunning()
    {
        if (_state != StoreState.Running)
            throw new InvalidOperationException($"Store is not running. Current state: {_state}");
    }
}