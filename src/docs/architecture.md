# SmartPipe.Memory Architecture v0.1.0

## Design Principles

1. Embedded-first — no external servers, works in-process
2. Type-safe queries — Fluent API only, no text-based query language
3. In-memory traversals — all graph traversals execute in memory, SQLite is for durability only
4. Bitemporal data — every node and edge has ValidFrom, ValidTo, TxTime
5. Minimal dependencies — only SmartPipe.Core and Microsoft.Data.Sqlite
6. Observability-first — OpenTelemetry metrics and tracing from day one
7. Modular NuGet packages — SmartPipe.Memory (graph), SmartPipe.Memory.Health (analytics), SmartPipe.Memory.Extensions (integration)

## Package Structure

SmartPipe.Memory                    Core graph engine
SmartPipe.Memory.Extensions         Integration with SmartPipe.Core
SmartPipe.Memory.Health             Predictive analytics (v0.2.0)

Dependencies:
SmartPipe.Memory -> SmartPipe.Core, Microsoft.Data.Sqlite
SmartPipe.Memory.Extensions -> SmartPipe.Memory, SmartPipe.Core
SmartPipe.Memory.Health -> SmartPipe.Memory

## Component Diagram

SmartPipe.Memory
  Graph/          Node, Edge, EdgeType, Bitemporal, Cluster, Insight
  Storage/        IGraphStore, InMemoryGraphStore, SqliteWALStore, StoreFactory
  Query/          MemoryQueryBuilder (Fluent API), MemoryQueryExecutor
  Algorithms/     BFS, Dijkstra, Leiden, DegreeCentrality, CardinalityEstimator
  Caching/        NodeCache (LRU)
  Infrastructure/ MemoryPools, MemoryDefaults, Guard, AtomicHelper
  Diagnostics/    MemoryMetrics, MemoryActivitySource, MemoryEventSource

SmartPipe.Memory.Extensions
  MemoryPipelineExtensions  UseMemory, AsGraphSource, ToGraphSink, TransformToEdges

SmartPipe.Memory.Health (v0.2.0)
  HealthVector, Insight, BottleneckPredictor, MemoryDecayPolicy

## Data Model (ALHI)

Asset-Lineage-Health-Insight model, simplified for v0.1.0:

Asset -> Node
LineagePath -> Edge with TransformationStep chain
HealthVector -> Node health fields (HealthScore, FailureProbability, etc.)
Insight -> Placeholder for v0.2.0

## Storage Architecture

### InMemoryGraphStore

Primary engine for all graph traversals.

Data structures:
- ConcurrentDictionary string, Node for nodes
- ConcurrentDictionary string, List of Edge for outgoing edges
- ReaderWriterLockSlim for write serialization
- List of Insight for insights

All BFS, Dijkstra, Leiden algorithms execute directly on in-memory structures. No SQL generation for graph traversals.

### SqliteWALStore

SQLite for durability only.

Startup:
1. Open SQLite connection
2. Execute CreateTables (idempotent)
3. Load all nodes into InMemoryGraphStore
4. Load all edges into InMemoryGraphStore

Write path:
1. Acquire AsyncLock (SemaphoreSlim)
2. Write to SQLite
3. Update InMemoryGraphStore
4. Release AsyncLock

Read path:
1. Delegates directly to InMemoryGraphStore
2. No SQLite access for reads

## Query Execution Flow

1. MemoryQueryBuilder builds MemoryQuery object
2. MemoryQueryExecutor.ExecuteAsync checks QueryType
3. FindNodes: calls IGraphStore.QueryNodesAsync
4. FindPath: calls IGraphStore.FindPathAsync
5. Traverse: calls IGraphStore.TraverseAsync
6. FindInsights: placeholder, returns empty

Cache integration:
- MemoryQueryExecutor checks NodeCache.TryGet before query
- On cache hit, returns cached node
- On cache miss, queries store and calls NodeCache.Set

## Bitemporal Model

Every node and edge has:
- ValidFrom: When the fact became true
- ValidTo: When the fact ceased (null = currently valid)
- TxTime: When recorded in the system

Enables time-aware queries in future versions.

Source: AWS Neptune data lineage (2025), Kronroe bitemporal facts.

## Resilience

CircuitBreaker: Protects SqliteWALStore from overload. Not used in v0.1.0 but ready for integration.

Optimistic concurrency: Node.Version field prevents lost updates. UpdateNodeHealthAsync checks expectedVersion.

Graceful shutdown: DrainAsync stops accepting writes, flushes MetricsChannel, sets state to Drained.

## Algorithms

### BFS
- Finds shortest path by edge count
- Uses Queue and HashSet for visited tracking
- Reconstructs path via parent dictionary
- Complexity: O(V + E)

### Dijkstra
- Finds shortest weighted path
- Uses PriorityQueue with edge weight as cost
- Complexity: O(E + V log V)

### Leiden
- Community detection via modularity optimization
- Newman-Girvan quality function
- Phases: local moving, refinement, aggregation
- Complexity: O(E) per iteration

### DegreeCentrality
- Counts direct outgoing edges
- Complexity: O(1) with dictionary lookup

### CardinalityEstimator
- Estimates unique neighbors using HyperLogLog from SmartPipe.Core
- Precision 12 gives approximately 3 percent accuracy
- Memory: O(1), approximately 4KB

## Observability

### Metrics
- System.Diagnostics.Metrics for zero-allocation export
- Meter name: SmartPipe.Memory
- Exported to Prometheus, Jaeger, Azure Monitor via OTLP

### Tracing
- System.Diagnostics.ActivitySource
- Spans: ExecuteQuery, UpsertNode, UpsertEdge, Cluster
- Tags: memory.query.type, memory.node.id, memory.edge.from

### EventCounters
- For dotnet-counters monitor
- Counters: queries-per-second, nodes-total, cache-hit-rate

## Future: Hybrid Search (v1.0.0)

Node.Embedding field (float[]) enables:
- Semantic search via SemanticallyCloseTo in Fluent API
- Integration with SmartPipe.Vector for FAISS, HNSW, DiskANN backends
- Combined graph and vector queries

## Inspiration

AWS Neptune data lineage blog (2025) — bitemporal model with valid_from/valid_to
Netflix E2EGraph (2024) — temporal comparison for bottleneck prediction
Kronroe — bitemporal facts as engine primitive
Boost.Graph (C++) — Newman-Girvan modularity for Leiden clustering
ExRam.Gremlinq — type-safe graph queries without text DSL
LanceDB, SQLite — embedded-first, zero-config philosophy
OpenTelemetry .NET (Microsoft) — ActivitySource, Meter, EventCounters