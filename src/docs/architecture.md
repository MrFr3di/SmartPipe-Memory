# SmartPipe.Memory Architecture v0.1.1

## Design Principles

1. Embedded‑first — no external servers, works in‑process
2. Type‑safe queries — Fluent API only, no text‑based query language
3. In‑memory traversals — all graph traversals execute in memory, SQLite is for durability only
4. Bitemporal data — every node and edge has ValidFrom, ValidTo, TxTime
5. Minimal dependencies — only SmartPipe.Core and Microsoft.Data.Sqlite
6. Observability‑first — OpenTelemetry metrics and tracing from day one
7. Modular NuGet packages — SmartPipe.Memory (graph), SmartPipe.Memory.Health (analytics), SmartPipe.Memory.Extensions (integration)

## Package Structure

SmartPipe.Memory                    Core graph engine
SmartPipe.Memory.Extensions         Integration with SmartPipe.Core
SmartPipe.Memory.Health             Predictive analytics

Dependencies:
SmartPipe.Memory -> SmartPipe.Core, Microsoft.Data.Sqlite
SmartPipe.Memory.Extensions -> SmartPipe.Memory, SmartPipe.Core, SmartPipe.Memory.Health
SmartPipe.Memory.Health -> SmartPipe.Memory, SmartPipe.Core

## Component Diagram

SmartPipe.Memory
  Graph/          Node, Edge, EdgeType, Cluster, Insight
  Model/          MemoryQuery, FilterNode, QueryResult
  Storage/        IGraphStore, InMemoryGraphStore, SqliteWALStore, GraphTraversalEngine, StoreFactory, SqliteSchema
  Query/          MemoryQueryBuilder (Fluent API), MemoryQueryExecutor
  Algorithms/
    Centrality/   PageRank, BetweennessCentrality, DegreeCentrality, GraphReorderer
    Clustering/   LeidenClusterer
    Estimation/   CardinalityEstimator
    Classification/ AutoClassifier
  Caching/        NodeCache (LRU)
  Infrastructure/ MemoryPools, MemoryDefaults, Guard
  Diagnostics/    MemoryMetrics, MemoryActivitySource, MemoryEventSource

SmartPipe.Memory.Extensions
  MemoryPipelineExtensions  UseMemory, AsGraphSource, ToGraphSink, TransformToEdges

SmartPipe.Memory.Health
  HealthVector, HealthVectorCalculator, BottleneckPredictor, InsightGenerator
  CognitiveConsolidation, MemoryDecayPolicy, ConflictResolver
  InsightAgent, MetricsBackgroundConsumer, MemoryHealthCheck

## Data Model

Node — graph node with identity, type, health metrics, and bitemporal fields.
Edge — graph edge with weight, confidence, transformation steps, and bitemporal fields.
Bitemporal fields (ValidFrom, ValidTo, TxTime) on every node and edge enable time‑travel queries.

## Storage Architecture

### InMemoryGraphStore

Primary engine for all graph traversals.

Data structures:

- ConcurrentDictionary for nodes
- ConcurrentDictionary for outgoing edges
- ReaderWriterLockSlim for write serialization
- Optional AutoClassifier for node type detection

All traversal, clustering, and centrality algorithms execute directly on in‑memory structures. No SQL generation for graph traversals.

### SqliteWALStore

SQLite for durability only.

Startup:

1. Open SQLite connection
2. Execute CreateTables (idempotent, includes insights table)
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

### GraphTraversalEngine (internal)

Shared engine for BFS‑based pathfinding and traversal. Used by both InMemoryGraphStore and SqliteWALStore. Supports:

- Node filtering (WhereNode)
- Minimum edge weight (MinWeight)
- Minimum edge confidence (MinConfidence)

## Query Execution Flow

1. MemoryQueryBuilder builds MemoryQuery object with filters, time‑travel parameters, and node/edge constraints.
2. MemoryQueryExecutor.ExecuteAsync checks QueryType.
3. FindNodes: calls IGraphStore.QueryNodesAsync or QueryNodesAsOfAsync.
4. FindPath: calls IGraphStore.FindPathAsync with node filter, min weight, min confidence.
5. Traverse: calls IGraphStore.TraverseAsync with node filter, min weight, min confidence.
6. FindClusters: calls IGraphStore.ClusterAsync which invokes LeidenClusterer.
7. FindInsights: delegates to IGraphStore.QueryInsightsAsync.
8. EstimateNeighbors / HasDegree: direct calls to CardinalityEstimator / DegreeCentrality.

Cache integration:

- MemoryQueryExecutor checks NodeCache.TryGet before query.
- On cache hit, returns cached node.
- On cache miss, queries store and calls NodeCache.Set.

## Time‑Travel Queries

MemoryQuery supports:

- AsOf(timestamp) — returns graph state at a specific point in time.
- Between(from, to) — returns changes within a time range.

Filters: ValidFrom <= AsOf AND (ValidTo IS NULL OR ValidTo > AsOf).

## Algorithms

### GraphTraversalEngine

- BFS‑based shortest path with node/edge filtering.
- BFS‑based traversal with node/edge filtering.
- Complexity: O(V + E).

### LeidenClusterer

- Community detection via modularity optimization (Newman‑Girvan).
- Complexity: O(E) per iteration.

### PageRank

- Node importance computation.
- Complexity: O(E) per iteration.

### BetweennessCentrality

- Bridge node detection (Brandes' algorithm).
- Complexity: O(V × E) full, O(V × E) subset.

### GraphReorderer

- Reorders nodes for cache locality (by community, degree, accessibility).

### DegreeCentrality

- Direct connection count.
- Complexity: O(1).

### CardinalityEstimator

- Unique neighbor estimation via HyperLogLog (SmartPipe.Core).
- Complexity: O(1) memory (~4KB), ~3% accuracy.

### Graph Connectivity

- **TopologicalSort (Kahn)**: Finds a topological order of nodes in a directed acyclic graph. Detects and reports cyclic nodes.
- **HasCycles**: Quick cycle detection based on Kahn's algorithm.
- **StronglyConnectedComponents (Tarjan)**: Finds all SCCs using an iterative stack‑based implementation.
- **WeaklyConnectedComponents (Union‑Find)**: Finds all WCCs by treating edges as undirected, with path compression and union by rank.

### AutoClassifier

- Detects node type from properties (hash, path, sql, connectionString).
- Detects edge type from nodes (DuplicateOf, VersionOf).
- Enabled via EnableAutoClassification flag.

## Predictive Analytics (SmartPipe.Memory.Health)

### HealthVector

- Aggregates predicted latency, throughput, failure probability, resource strain, percentiles.
- HealthScore formula: 1.0 - (0.35*FailureProbability + 0.35*LatencyComponent + 0.30*ResourceStrain).

### HealthVectorCalculator

- Uses AdaptiveMetrics and ExponentialHistogram from SmartPipe.Core.
- Computes HealthVector from MetricsEntry history.

### BottleneckPredictor

- Temporal comparison of current vs historical HealthVector.
- Provides confidence and estimated time to impact.

### InsightGenerator

- Creates Insight objects from predictions, retry budget exhaustion, clusters.

### CognitiveConsolidation

- Merges repeated insights into higher‑confidence consolidated insights.

### MemoryDecayPolicy

- Edge weight decay following Ebbinghaus‑like curve.
- Adaptive: access frequency slows decay.

### ConflictResolver

- Weakens existing edges on new contradictory facts instead of deletion.

### InsightAgent

- Background service that periodically analyzes unhealthy nodes and generates insights.

### MetricsBackgroundConsumer

- Reads metrics from channel, computes HealthVector, updates node health.

## Resilience

CircuitBreaker: Ready for SqliteWALStore integration.
Optimistic concurrency: Node.Version field prevents lost updates.
Graceful shutdown: DrainAsync stops writes, flushes metrics channel.

## False Sharing Prevention (v0.1.2)

`PaddedCounter64` and `PaddedCounter32` (in `Infrastructure/`) are atomic counters padded to 64 bytes (one CPU cache line). They are used in `MemoryMetrics` to eliminate false sharing between cache hit/miss counters and query execution counters, ensuring accurate performance measurements in multi‑threaded scenarios.

## WAL Checkpoint (v0.1.2)

`SqliteWALStore` runs a background timer (`System.Threading.Timer`) that periodically executes `PRAGMA wal_checkpoint(TRUNCATE)`. This keeps the write‑ahead log file size under control and prevents performance degradation during long‑running write workloads.

## Observability

### Metrics (MemoryMetrics)
- memory.nodes.total, memory.edges.total (Gauge)
- memory.queries.executed (Counter)
- memory.cache.hit_rate (Gauge)
- memory.store.latency_ms (Histogram)

### Tracing (MemoryActivitySource)
- Spans: ExecuteQuery, UpsertNode, UpsertEdge, Cluster.
- Tags: memory.query.type, memory.node.id, memory.edge.from.

### EventCounters (MemoryEventSource)
- Counters: queries‑per‑second, nodes‑total, cache‑hit‑rate.

## Future: Hybrid Search (v1.0.0)

Node.Embedding field (float[]) enables:
- Semantic search via SemanticallyCloseTo in Fluent API.
- Integration with SmartPipe.Vector for FAISS, HNSW, DiskANN backends.
- Combined graph and vector queries.

## Inspiration

AWS Neptune data lineage blog (2025) — bitemporal model
Netflix E2EGraph (2024) — temporal comparison for bottleneck prediction
Kronroe — bitemporal facts as engine primitive
Boost.Graph (C++) — Newman‑Girvan modularity for Leiden clustering
ExRam.Gremlinq — type‑safe graph queries without text DSL
LanceDB, SQLite — embedded‑first, zero‑config philosophy
OpenTelemetry .NET (Microsoft) — ActivitySource, Meter, EventCounters