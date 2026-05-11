# Changelog

All notable changes to the SmartPipe.Memory project.

## [0.1.2] — 2026-05-11

### Added

- **Topological sort (Kahn's algorithm)** via `MemoryQueryBuilder.TopologicalSort()`.
- **Cycle detection** via `MemoryQueryBuilder.HasCycles()`.
- **Strongly connected components (Tarjan's algorithm)** via `MemoryQueryBuilder.FindSCC()`.
- **Weakly connected components (Union‑Find)** via `MemoryQueryBuilder.FindWCC()`.
- `GetAllNodes()` method on `IGraphStore` and implementations.
- `PaddedCounter64` and `PaddedCounter32` to prevent false sharing in `MemoryMetrics`.
- Periodic WAL checkpoint in `SqliteWALStore` (`PRAGMA wal_checkpoint(TRUNCATE)`) to keep the write‑ahead log under control.
- Iterative version of Tarjan's SCC to prevent `StackOverflowException` on large graphs.
- Connectivity stress test in sandbox (`dotnet run --project sandbox -- connectivity`).

### Changed

- `MemoryMetrics` now uses `PaddedCounter64` for cache hit/miss counters, eliminating false sharing.
- `SqliteWALStore.DisposeAsync` performs a final checkpoint and clears connection pool.
- `StronglyConnectedComponents` now uses iterative stack instead of recursion.

### Fixed

- `DateTime` UTC normalization in `SqliteWALStore` – `ValidFrom`/`ValidTo` now correctly stored and compared as UTC.
- File deletion after `DisposeAsync` in `SqliteWALStore` (requires pool cleanup and delay on Windows).

## [0.1.1] — 2026-05-11

### Added

- **AND/OR composable filters** in `MemoryQueryBuilder` (#13).
- **Time-travel queries**: `AsOf(timestamp)` and `Between(from, to)` methods on `MemoryQueryBuilder`, with full bitemporal support in `InMemoryGraphStore` and `SqliteWALStore` (#14-#17).
- **Node filtering during traversal** (`WhereNode`) — prevents BFS/Dijkstra from visiting unhealthy nodes (#23).
- **Edge filtering in pathfinding**: `MinWeight(weight)` and `MinConfidence(confidence)` methods on `MemoryQueryBuilder` (#24).
- **Leiden clustering API**: `FindClusters()` method on `MemoryQueryBuilder`, accessible via `IGraphStore.ClusterAsync()`.
- **Cardinality estimation**: `EstimateNeighbors(nodeId)` method on `MemoryQueryBuilder` using HyperLogLog.
- **Degree centrality**: `HasDegree(nodeId)` method on `MemoryQueryBuilder`.
- **PageRank** algorithm in `Algorithms/Centrality/`.
- **BetweennessCentrality** (Brandes' algorithm) in `Algorithms/Centrality/`.
- **GraphReorderer** for cache-locality optimization.
- **HealthVector** and **HealthVectorCalculator** — full predictive health scoring.
- **BottleneckPredictor** with temporal comparison of current vs historical health.
- **InsightGenerator** — creates `Insight` objects from predictions.
- **CognitiveConsolidation** — merges repeated insights into higher-confidence consolidated insights.
- **MemoryDecayPolicy** — adaptive edge weight decay (Ebbinghaus-like curve).
- **ConflictResolver** — weakens existing edges on contradictory facts.
- **InsightAgent** — background service for periodic health analysis.
- **MetricsBackgroundConsumer** — async channel-based metrics persistence.
- **MemoryHealthCheck** — reports Healthy/Degraded/Unhealthy store state.
- **AutoClassifier** — automatic node type detection from properties (moved into `SmartPipe.Memory.Algorithms.Classification`).
- **OpenTelemetry integration**: `MemoryMetrics`, `MemoryActivitySource`, `MemoryEventSource` now wired into `MemoryQueryExecutor` and `InMemoryGraphStore`.
- **Insert insight SQL logic** in `SqliteSchema` (new `insights` table).
- **`GetOutEdges()` method** on `IGraphStore` for algorithm access.
- **Batch node upsert** (`BatchUpsertNodesAsync`) in `IGraphStore`.
- **`SqliteConnection.ClearPool` and `PRAGMA wal_checkpoint(TRUNCATE)`** on `DisposeAsync` for clean file release.
- **DateTime normalization to UTC** in `SqliteWALStore.ReadNode/ReadEdge` (fixes `AsOf` queries).
- **Sandbox project** with stress, temporal, chaos, and health scenarios.
- **185 tests** (up from 123)

### Changed

- `FilterNode` changed from `abstract record` to `abstract class` for AOT compatibility.
- `MemoryQueryBuilder` constructor changed to `internal`; factory method `Create(store, cache)` added.
- `IGraphStore.FindPathAsync` and `TraverseAsync` now accept optional `nodeFilter`, `minWeight`, `minConfidence` parameters.
- `MemoryQuery` extended with `AsOf`, `TimeRangeFrom`, `TimeRangeTo`, `MinWeight`, `MinConfidence`, `NodeFilter` fields.

### Removed

- `LineageQL` parser and lexer — replaced entirely by Fluent API.
- Standalone `BreadthFirstSearch` and `DijkstraShortestPath` classes — logic moved to `GraphTraversalEngine`.

### Fixed

- `DateTime.Parse` replaced with `DateTimeOffset.Parse.UtcDateTime` for correct UTC timestamps in SQLite.
- File deletion after `DisposeAsync` resolved with proper pool cleanup.
- AND/OR filter application logic in `InMemoryGraphStore`.

## [0.1.0] — 2026-05-08

Initial release.
