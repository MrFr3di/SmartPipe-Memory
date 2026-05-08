# SmartPipe.Memory API Reference v0.1.0

## SmartPipe.Memory.Graph

### Node

| Member | Type | Description |
|:---|:---|:---|
| `Id` | `string` | Unique identifier. Init-only. |
| `Type` | `string` | Node type. Init-only. |
| `Label` | `string` | Human-readable name. Init-only. |
| `Properties` | `Dictionary<string,object>` | Arbitrary metadata. Init-only. |
| `Metadata` | `Dictionary<string,string>` | LineageContext. Init-only. |
| `Embedding` | `float[]?` | Vector embedding for hybrid search. Mutable. |
| `HealthScore` | `double` | Aggregated health 0..1. Mutable. |
| `FailureProbability` | `double` | Failure probability 0..1. Mutable. |
| `PredictedLatencyMs` | `double` | Predicted latency. Mutable. |
| `ResourceStrain` | `double` | Resource strain 0..1. Mutable. |
| `ValidFrom` | `DateTime` | Start of validity. Init-only. |
| `ValidTo` | `DateTime?` | End of validity. Mutable. |
| `TxTime` | `DateTime` | Transaction time. Init-only. |
| `Version` | `int` | Optimistic concurrency. Init-only. |

### Edge

| Member | Type | Description |
|:---|:---|:---|
| `Id` | `long` | Unique identifier. Init-only. |
| `FromNodeId` | `string` | Source node. Init-only. |
| `ToNodeId` | `string` | Target node. Init-only. |
| `Type` | `EdgeType` | Relationship type. Init-only. |
| `Weight` | `double` | Edge strength 0..1. Mutable. |
| `Confidence` | `double` | Confidence 0..1. Init-only. |
| `SourceType` | `string` | LOG, STATIC, GENAI. Init-only. |
| `Steps` | `List<TransformationStep>` | Transformation chain. Init-only. |
| `ValidFrom` | `DateTime` | Start of validity. Init-only. |
| `ValidTo` | `DateTime?` | End of validity. Mutable. |
| `TxTime` | `DateTime` | Transaction time. Init-only. |

### EdgeType

| Value | Description |
|:---|:---|
| `DerivedFrom` | Target was derived from source |
| `DuplicateOf` | Target is a duplicate of source |
| `VersionOf` | Target is a version of source |
| `AggregatedFrom` | Target was aggregated from source |
| `FilteredFrom` | Target was filtered from source |
| `FeedsInto` | Pipeline component feeds into another |

### TransformationStep

| Member | Type | Description |
|:---|:---|:---|
| `TransformerName` | `string` | Name of the transformer |
| `ExecutedAt` | `DateTime` | When executed |
| `Duration` | `TimeSpan` | Execution duration |
| `Metadata` | `Dictionary<string,string>?` | Additional data |

### Cluster

| Member | Type | Description |
|:---|:---|:---|
| `Id` | `string` | Cluster identifier |
| `NodeIds` | `IReadOnlyList<string>` | Nodes in this cluster |
| `Size` | `int` | Number of nodes |
| `Modularity` | `double` | Modularity score 0..1 |
| `ComputedAt` | `DateTime` | When computed |

### Insight

| Member | Type | Description |
|:---|:---|:---|
| `Id` | `string` | Unique identifier |
| `Type` | `string` | Insight type string |
| `Title` | `string` | Human-readable title |
| `Description` | `string?` | Detailed description |
| `RelatedNodeIds` | `IReadOnlyList<string>` | Related nodes |
| `Confidence` | `double` | Confidence 0..1 |
| `Severity` | `string` | Info, Warning, Critical |
| `GeneratedAt` | `DateTime` | When generated |

## SmartPipe.Memory.Storage

### IGraphStore

| Method | Returns | Description |
|:---|:---|:---|
| `State` | `StoreState` | Current store state |
| `IsDraining` | `bool` | Whether draining |
| `DrainAsync(ct)` | `Task` | Graceful shutdown |
| `UpsertNodeAsync(node, ct)` | `Task<Node>` | Insert or update node |
| `BatchUpsertNodesAsync(nodes, ct)` | `Task` | Batch insert nodes |
| `GetNodeAsync(id, ct)` | `Task<Node?>` | Get node by id |
| `DeleteNodeAsync(id, ct)` | `Task` | Delete node |
| `UpsertEdgeAsync(edge, ct)` | `Task<Edge>` | Insert or update edge |
| `DeleteEdgeAsync(id, ct)` | `Task` | Delete edge |
| `QueryNodesAsync(query, ct)` | `IAsyncEnumerable<Node>` | Stream matching nodes |
| `FindPathAsync(from, to, type, depth, ct)` | `Task<IReadOnlyList<PathSegment>>` | Find shortest path |
| `TraverseAsync(start, type, depth, limit, ct)` | `IAsyncEnumerable<(Node, int)>` | Traverse graph |
| `QueryInsightsAsync(query, ct)` | `IAsyncEnumerable<Edge>` | Query insights |
| `GetWeakenedEdgesFromAsync(id, ct)` | `Task<IReadOnlyList<Edge>>` | Get edges with weight below 0.3 |
| `InsertInsightAsync(insight, ct)` | `Task` | Save insight |
| `UpdateNodeHealthAsync(...)` | `Task` | Update health with version check |
| `MetricsChannel` | `ChannelWriter<MetricsEntry>` | Metrics buffer |

### StoreState

| Value | Description |
|:---|:---|
| `Running` | Normal operation |
| `Draining` | Shutting down |
| `Drained` | Shutdown complete |
| `Faulted` | Error state |

### PathSegment

| Member | Type | Description |
|:---|:---|:---|
| `NodeId` | `string` | Node identifier |
| `EdgeType` | `string` | Edge type traversed |
| `Weight` | `double` | Edge weight |

### MetricsEntry

| Member | Type | Description |
|:---|:---|:---|
| `NodeId` | `string` | Node identifier |
| `Timestamp` | `DateTime` | When recorded |
| `Values` | `IReadOnlyDictionary<string,double>` | Metric values |

### StoreFactory

| Method | Returns | Description |
|:---|:---|:---|
| `CreateInMemory(capacity)` | `IGraphStore` | In-memory store |
| `CreateSqlite(path, capacity)` | `SqliteWALStore` | SQLite store |

### InMemoryGraphStore

Implements `IGraphStore`. All operations in memory.

### SqliteWALStore

Implements `IGraphStore`. Requires `InitializeAsync()` after creation.

### SqliteSchema

| Member | Type | Description |
|:---|:---|:---|
| `CreateTables` | `string` | Complete DDL |

### SchemaVersion

| Member | Type | Description |
|:---|:---|:---|
| `Current` | `SchemaVersion` | Current version (0.1) |
| `Major` | `int` | Major version |
| `Minor` | `int` | Minor version |

## SmartPipe.Memory.Query

### MemoryQueryBuilder

| Method | Returns | Description |
|:---|:---|:---|
| `Nodes(type)` | `MemoryQueryBuilder` | Filter by node type |
| `Where(prop, op, val)` | `MemoryQueryBuilder` | Filter by property |
| `ConnectedVia(type)` | `MemoryQueryBuilder` | Filter by edge type |
| `StartFrom(id)` | `MemoryQueryBuilder` | Start traversal |
| `To(id)` | `MemoryQueryBuilder` | Target node |
| `ShortestPath(from, to, via)` | `MemoryQueryBuilder` | Find shortest path |
| `Traverse(type, depth)` | `MemoryQueryBuilder` | Traverse graph |
| `MaxDepth(depth)` | `MemoryQueryBuilder` | Set max depth |
| `Limit(n)` | `MemoryQueryBuilder` | Limit results |
| `OrderBy(prop, desc)` | `MemoryQueryBuilder` | Sort results |
| `ExecuteAsync(ct)` | `IAsyncEnumerable<QueryResult>` | Execute query |

### MemoryQueryExecutor

| Constructor | Description |
|:---|:---|
| `MemoryQueryExecutor(store, cache)` | Create executor |

| Method | Returns | Description |
|:---|:---|:---|
| `ExecuteAsync(query, ct)` | `IAsyncEnumerable<QueryResult>` | Execute query |

### MemoryQuery

| Member | Type | Description |
|:---|:---|:---|
| `NodeType` | `string?` | Filter by type |
| `Filter` | `FilterNode?` | Filter tree |
| `EdgeType` | `string?` | Filter by edge |
| `StartNodeId` | `string?` | Start node |
| `TargetNodeId` | `string?` | Target node |
| `MaxDepth` | `int?` | Max depth |
| `Limit` | `int?` | Max results |
| `OrderBy` | `string?` | Sort property |
| `OrderDesc` | `bool` | Sort descending |
| `Type` | `QueryType` | Query type |

### QueryType

| Value | Description |
|:---|:---|
| `FindNodes` | Find matching nodes |
| `FindPath` | Find shortest path |
| `Traverse` | Traverse graph |
| `FindInsights` | Find insights |

### FilterNode

| Type | Description |
|:---|:---|
| `PropertyFilter` | Filter by property, operator, value |
| `And` | Logical AND (v0.2.0) |
| `Or` | Logical OR (v0.2.0) |

### FilterOperator

| Value | Description |
|:---|:---|
| `LessThan` | Property < value |
| `GreaterThan` | Property > value |
| `Equals` | Property == value |

### QueryResult

| Member | Type | Description |
|:---|:---|:---|
| `Type` | `ResultType` | Node, Edge, Path, Cluster |
| `Node` | `Node?` | Node result |
| `Edge` | `Edge?` | Edge result |
| `Path` | `IReadOnlyList<string>?` | Path result |
| `TotalWeight` | `double?` | Path total weight |
| `Cluster` | `Cluster?` | Cluster result |
| `Depth` | `int` | Traversal depth |

### ResultType

| Value | Description |
|:---|:---|
| `Node` | Node result |
| `Edge` | Edge result |
| `Path` | Path result |
| `Cluster` | Cluster result |

## SmartPipe.Memory.Caching

### NodeCache

| Constructor | Description |
|:---|:---|
| `NodeCache(maxSize)` | Create cache |

| Method | Returns | Description |
|:---|:---|:---|
| `Count` | `int` | Number of cached nodes |
| `TryGet(id)` | `bool, Node?` | Get from cache |
| `Set(id, node)` | `void` | Store in cache |
| `Invalidate(id)` | `void` | Remove from cache |
| `Clear()` | `void` | Clear all |

## SmartPipe.Memory.Algorithms

### BreadthFirstSearch

| Method | Returns | Description |
|:---|:---|:---|
| `FindPath(edges, from, to, type, depth, ct)` | `IReadOnlyList<PathSegment>` | Find shortest path |

### DijkstraShortestPath

| Method | Returns | Description |
|:---|:---|:---|
| `FindPath(edges, from, to, type, depth, ct)` | `IReadOnlyList<PathSegment>` | Find weighted shortest path |

### LeidenClusterer

| Method | Returns | Description |
|:---|:---|:---|
| `Cluster(nodes, edges, iterations, improvement, ct)` | `IReadOnlyList<Cluster>` | Run clustering |
| `CurrentQuality` | `double` | Current modularity |

### DegreeCentrality

| Method | Returns | Description |
|:---|:---|:---|
| `Compute(edges, nodeId)` | `int` | Count direct connections |

### CardinalityEstimator

| Constructor | Description |
|:---|:---|
| `CardinalityEstimator(precision)` | Create estimator |

| Method | Returns | Description |
|:---|:---|:---|
| `Add(nodeId)` | `void` | Add node hash |
| `Estimate()` | `double` | Get estimate |
| `EstimateNeighbors(edges, nodeId)` | `double` | Estimate unique neighbors |

## SmartPipe.Memory.Infrastructure

### MemoryPools

| Member | Type | Description |
|:---|:---|:---|
| `NodePool` | `ObjectPool<Node>` | Pool for nodes |
| `EdgePool` | `ObjectPool<Edge>` | Pool for edges |

### MemoryDefaults

| Constant | Value | Description |
|:---|:---|:---|
| `MaxCacheSize` | 10000 | Default cache size |
| `MaxQueryDepth` | 10 | Default query depth |
| `MetricsBufferCapacity` | 10000 | Metrics buffer size |
| `ObjectPoolCapacity` | 256 | Pool capacity |
| `DefaultDatabaseName` | "memory.db" | Default DB path |
| `DegradedHealthThreshold` | 0.7 | Health degradation |
| `WeakenedEdgeThreshold` | 0.3 | Weak edge threshold |

## SmartPipe.Memory.Diagnostics

### MemoryMetrics

| Method | Description |
|:---|:---|
| `SetNodesTotal(count)` | Record node count |
| `SetEdgesTotal(count)` | Record edge count |
| `RecordQuery()` | Record query execution |
| `RecordCacheHit()` | Record cache hit |
| `RecordCacheMiss()` | Record cache miss |
| `RecordStoreLatency(ms)` | Record latency |

### MemoryActivitySource

| Method | Returns | Description |
|:---|:---|:---|
| `StartQuery(type)` | `Activity?` | Start query span |
| `StartUpsertNode(id)` | `Activity?` | Start upsert span |
| `StartUpsertEdge(from, to)` | `Activity?` | Start edge span |
| `StartClustering(count)` | `Activity?` | Start cluster span |

### MemoryEventSource

| Method | Description |
|:---|:---|
| `RecordQuery()` | Record query |
| `SetNodesTotal(count)` | Set node count |
| `SetCacheHitRate(rate)` | Set hit rate |
| `EnableForTesting()` | Force init |

## SmartPipe.Memory.Extensions

### MemoryPipelineExtensions

| Method | Returns | Description |
|:---|:---|:---|
| `UseMemory(pipeline, store)` | `SmartPipeChannel<TIn,TOut>` | Connect memory to pipeline |
| `AsGraphSource(store, type)` | `ISource<Node>` | Create graph source |
| `ToGraphSink(store, factory)` | `ISink<T>` | Create graph sink |
| `TransformToEdges(store, factory)` | `ITransformer<T,T>` | Create edge transformer |

## SmartPipe.Memory

### ServiceCollectionExtensions

| Method | Description |
|:---|:---|
| `AddSmartPipeMemory(configure)` | Register in-memory store |
| `AddSmartPipeMemorySqlite(configure)` | Register SQLite store |

### MemoryConfiguration

| Property | Type | Default | Description |
|:---|:---|:---|:---|
| `ConnectionString` | `string` | "memory.db" | SQLite path |
| `MaxCacheSize` | `int` | 10000 | LRU cache size |
| `MetricsBufferCapacity` | `int` | 10000 | Metrics buffer |