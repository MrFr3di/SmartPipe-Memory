# SmartPipe.Memory v0.1.1 — Complete Feature Reference

## Overview

SmartPipe.Memory is an embedded graph memory layer for the SmartPipe ecosystem. It provides a type‑safe graph database with predictive analytics, designed for ETL pipelines and AI agents.

## Architecture

SmartPipe.Memory                    # Core graph engine
SmartPipe.Memory.Extensions         # Integration with SmartPipe.Core
SmartPipe.Memory.Health             # Predictive analytics

## Core Engine

### Graph Model

Node — Graph node: any object like file, record, stream, or pipeline component
Edge — Graph edge: relationship with full transformation history
EdgeType — DerivedFrom, DuplicateOf, VersionOf, AggregatedFrom, FilteredFrom, FeedsInto
Bitemporal — ValidFrom, ValidTo, TxTime on every node and edge
Cluster — Result of Leiden community detection
Insight — Predictive analytics insight

### Node Properties

Id — Unique identifier (string)
Type — Node type: "File", "Record", "Transformer", "Source", "Sink" (string)
Label — Human-readable name (string)
Properties — Arbitrary metadata (Dictionary string, object)
Metadata — LineageContext: source, pipeline, enteredAt, transform (Dictionary string, string)
HealthScore — Aggregated health 0..1 (double)
FailureProbability — From CircuitBreaker EWMA 0..1 (double)
PredictedLatencyMs — From AdaptiveMetrics (double)
ResourceStrain — From weak connections 0..1 (double)
Embedding — Vector embedding, v1.0.0 (float[] or null)
ValidFrom — Start of validity period, bitemporal (DateTime)
ValidTo — End of validity period (DateTime or null)
TxTime — Transaction time, immutable after creation (DateTime)
Version — Optimistic concurrency (int)

### Edge Properties

Id — Unique identifier (long)
FromNodeId — Source node (string)
ToNodeId — Target node (string)
Type — Relationship type (EdgeType)
Weight — Edge strength 0..1, decays over time (double)
Confidence — 1.0 = deterministic, less than 0.7 = inference (double)
SourceType — "LOG", "STATIC", "GENAI" (string)
Steps — Chain of transformation steps (List of TransformationStep)
ValidFrom — Start of validity period (DateTime)
ValidTo — End of validity period (DateTime or null)
TxTime — Transaction time (DateTime)

### TransformationStep

TransformerName — Name of the transformer (string)
ExecutedAt — When executed (DateTime)
Duration — Execution duration (TimeSpan)
Metadata — Additional data (Dictionary string, string or null)

## Storage

### InMemoryGraphStore

In‑memory graph store for testing and development. All traversals execute in memory for maximum performance.

Features:
- ConcurrentDictionary for nodes and edges
- ReaderWriterLockSlim for write operations
- All algorithms execute here (traversal, clustering, centrality)
- Thread-safe concurrent reads and writes
- Optional AutoClassifier for automatic node type detection

Use when: Testing, CI, development, graphs up to 100K nodes.

### SqliteWALStore

SQLite‑backed store with WAL mode for production.

Features:
- Loads all data into InMemoryGraphStore at startup
- Writes to SQLite for durability
- AsyncLock for exclusive SQLite access
- Automatic schema creation (includes insights table)
- Supports batch insert, 100 nodes per batch
- Data survives restarts
- Optional AutoClassifier (delegates to in‑memory store)

Use when: Production, FlowKeep, graphs of any size.

### StoreFactory

CreateInMemory — Creates InMemoryGraphStore for testing
CreateSqlite — Creates SqliteWALStore for production, requires InitializeAsync after creation

### SqliteSchema

Tables: nodes, edges, insights
Indexes: by type, by health_score, by from_node_id and type, by to_node_id, by insights type
Views: v_degraded_nodes — nodes with health_score below 0.7

## Query Engine

### Fluent API

Type‑safe C# API for building queries. No text‑based query language. Safety guaranteed by the compiler.

### MemoryQueryBuilder Methods

Nodes(type) — Filter by node type: "File", "Record", "Transformer"
Where(property, operator, value) — Filter by HealthScore, FailureProb, ResourceStrain, PredictedLatencyMs
And() — Combine next filter with logical AND (default)
Or() — Combine next filter with logical OR
ConnectedVia(edgeType) — Filter by edge type
StartFrom(nodeId) — Start traversal from this node
To(nodeId) — Target node for pathfinding
ShortestPath(from, to, via) — Find shortest path between nodes
Traverse(edgeType, maxDepth) — Traverse graph from current start node
MaxDepth(depth) — Maximum traversal depth
Limit(n) — Maximum number of results
OrderBy(property, descending) — Sort results
AsOf(timestamp) — Time‑travel: return graph state at a point in time
Between(from, to) — Time‑travel: return changes in a time range
WhereNode(predicate) — Filter nodes during traversal
MinWeight(weight) — Minimum edge weight for pathfinding
MinConfidence(confidence) — Minimum edge confidence for pathfinding
FindClusters() — Run Leiden clustering and return clusters
EstimateNeighbors(nodeId) — Estimate unique neighbors using HyperLogLog
HasDegree(nodeId) — Count direct outgoing edges
ExecuteAsync() — Execute query and stream results

### FilterNode

PropertyFilter — Filter by property name, operator, and value
And — Logical AND of two filters
Or — Logical OR of two filters

### FilterOperator

LessThan, GreaterThan, Equals

### QueryType

FindNodes — Find nodes matching filters
FindPath — Find shortest path between two nodes
Traverse — Traverse graph from starting node
FindInsights — Find generated insights

### QueryResult

Type — Node, Edge, Path, or Cluster
Node — Node result for FindNodes queries
Edge — Edge result for Traverse queries
Path — Path as list of node identifiers for FindPath queries
TotalWeight — Total weight of the path
Cluster — Cluster result for clustering queries
Depth — Depth in traversal for Traverse queries

## Algorithms

### GraphTraversalEngine (internal)

Shared engine for BFS‑based pathfinding and traversal. Supports node filters, minimum edge weight, and minimum edge confidence.

### LeidenClusterer

Leiden community detection. Optimizes Newman‑Girvan modularity.
Complexity: O(E) per iteration
Accessible via FindClusters() in MemoryQueryBuilder.

### PageRank

Computes node importance.
Complexity: O(E) per iteration

### BetweennessCentrality

Identifies bridge nodes using Brandes' algorithm.
Complexity: O(V × E) for full computation, O(V × E) for subset.

### GraphReorderer

Reorders nodes for better cache locality during traversals. Supports reordering by community, degree, and accessibility.

### DegreeCentrality

Counts direct connections of a node.
Complexity: O(1) with indexes
Accessible via HasDegree() in MemoryQueryBuilder.

### CardinalityEstimator

Estimates unique neighbors using HyperLogLog from SmartPipe.Core.
Complexity: O(1) memory, approximately 4KB
Accuracy: approximately 3 percent with precision 12
Accessible via EstimateNeighbors() in MemoryQueryBuilder.

### AutoClassifier

Automatically classifies nodes by type based on properties (hash, path, sql, connectionString).
Classifies edges based on hash equality and version patterns.
Enabled via EnableAutoClassification in MemoryConfiguration.

## Predictive Analytics (SmartPipe.Memory.Health)

### HealthVector

Contains predicted latency, smoothed throughput, failure probability, resource strain, and percentiles.
HealthScore formula: 1.0 - (0.35 * FailureProbability + 0.35 * LatencyComponent + 0.30 * ResourceStrain)

### HealthVectorCalculator

Computes HealthVector from metric history using AdaptiveMetrics and ExponentialHistogram.

### BottleneckPredictor

Predicts bottlenecks by comparing current HealthVector with historical state.
Provides confidence and estimated time to impact.

### InsightGenerator

Generates Insight objects from bottleneck predictions and graph state.
Supports bottleneck, retry budget exhausted, and cluster discovered insights.

### CognitiveConsolidation

Groups repeated insights into higher‑confidence consolidated insights when the same pattern appears multiple times.

### MemoryDecayPolicy

Computes edge weight decay over time using an Ebbinghaus‑like forgetting curve.
Adaptive: frequently accessed edges decay slower.

### ConflictResolver

Weakens existing edges when new contradictory facts are added, instead of deleting them.

### InsightAgent

Background service that periodically analyzes node health and generates insights.

### MetricsBackgroundConsumer

Consumes metrics from a channel and persists them to the graph store for health analysis.

### MemoryHealthCheck

Reports Healthy, Degraded, or Unhealthy based on store state.

## Infrastructure

### NodeCache

LRU cache for frequently accessed nodes.
Max size: 10000 by default.
Point invalidation: only changed nodes are evicted.
Thread‑safe via lock.

Methods: TryGet, Set, Invalidate, Clear

### MemoryPools

ObjectPool for Node and ObjectPool for Edge from SmartPipe.Core.
Reduces GC pressure during high‑throughput pipeline execution.
Capacity: 256 items each.

### MemoryDefaults

MaxCacheSize = 10000
MaxQueryDepth = 10
MetricsBufferCapacity = 10000
ObjectPoolCapacity = 256
DefaultDatabaseName = "memory.db"
DegradedHealthThreshold = 0.7
WeakenedEdgeThreshold = 0.3

## Integration with SmartPipe.Core

### UseMemory

Connects memory to any SmartPipe pipeline. Automatically registers pipeline topology, streams metrics, and starts the metrics background consumer for health analysis.

### AsGraphSource

Creates an ISource of Node from the graph store.

### ToGraphSink

Creates an ISink of T that writes pipeline results to the graph as nodes.

### TransformToEdges

Creates an ITransformer of T, T that converts elements to edges in the graph.

## Observability

### OpenTelemetry Metrics

memory.nodes.total — Gauge, total number of nodes
memory.edges.total — Gauge, total number of edges
memory.queries.executed — Counter, queries executed
memory.cache.hit_rate — Gauge, cache hit rate 0..1
memory.store.latency_ms — Histogram, store operation latency in milliseconds

### Tracing

Spans created for: ExecuteQuery, UpsertNode, UpsertEdge, Cluster.
Tags: memory.query.type, memory.node.id, memory.edge.from, memory.edge.to, memory.nodes.count.

### EventCounters

Usage: dotnet-counters monitor --process-id PID SmartPipe.Memory
Counters: queries‑per‑second, nodes‑total, cache‑hit‑rate.

## Dependency Injection

AddSmartPipeMemory — Register in‑memory store
AddSmartPipeMemorySqlite — Register SQLite store

## Configuration

ConnectionString — SQLite database path, default "memory.db"
MaxCacheSize — Maximum LRU cache size, default 10000
MetricsBufferCapacity — Metrics channel capacity, default 10000
EnableAutoClassification — Automatically classify nodes on upsert, default false

## Dependencies

SmartPipe.Core version 1.0.5 or higher
Microsoft.Data.Sqlite version 9.0.0 or higher
Transitive: SQLitePCLRaw.bundle_e_sqlite3
