# SmartPipe.Memory

**Embedded graph memory layer for the SmartPipe ecosystem.**

[![NuGet](https://img.shields.io/nuget/v/SmartPipe.Memory)](https://www.nuget.org/packages/SmartPipe.Memory)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com)

## Overview

SmartPipe.Memory is an embedded graph memory layer with predictive analytics, designed for ETL pipelines and AI agents. It provides a type-safe Fluent API for graph queries, in-memory traversal engine, and SQLite-backed persistence.

## Packages

| Package | Description | Version |
|:---|:---|:---|
| `SmartPipe.Memory` | Core graph engine | v0.1.1 |
| `SmartPipe.Memory.Extensions` | Integration with SmartPipe.Core | v0.1.1 |
| `SmartPipe.Memory.Health` | Predictive analytics & health monitoring | v0.1.1 |

## Installation

```csharp
dotnet add package SmartPipe.Memory
dotnet add package SmartPipe.Memory.Extensions
dotnet add package SmartPipe.Memory.Health
```

## Quick Start

```csharp
var store = StoreFactory.CreateInMemory();

await store.UpsertNodeAsync(new Node { Id = "f1", Type = "File", Label = "doc.pdf" });
await store.UpsertNodeAsync(new Node { Id = "f2", Type = "File", Label = "copy.pdf" });
await store.UpsertEdgeAsync(new Edge { FromNodeId = "f1", ToNodeId = "f2", Type = EdgeType.DuplicateOf });

var query = MemoryQueryBuilder.Create(store, new NodeCache());

await foreach (var r in query.ShortestPath("f1", "f2", "DuplicateOf").ExecuteAsync())
    Console.WriteLine(string.Join(" -> ", r.Path));
```

## Key Features

- Type-safe Fluent API — no text-based query language
- In-memory traversal engine (BFS, Dijkstra, Leiden clustering)
- Predictive analytics — HealthVector, BottleneckPredictor, MemoryDecayPolicy
- Time-travel queries — AsOf and Between with full bitemporal support
- AND/OR composable filters with WhereNode, MinWeight, MinConfidence
- Centrality algorithms — PageRank, BetweennessCentrality, DegreeCentrality
- Graph reordering for cache locality
- Auto-classification of nodes and edges
- SQLite WAL persistence with automatic recovery
- OpenTelemetry metrics and tracing — ActivitySource, Meter, EventCounters
- Dependency injection — AddSmartPipeMemory() / AddSmartPipeMemorySqlite()
- Zero external servers — embedded in-process library
- 185 tests, 0 failures

## Documentation

| Document | Description |
|:---|:---|
| [Features](docs/features.md) | Complete feature reference |
| [Getting Started](docs/getting-started.md) | Quick start guide |
| [API Reference](docs/api-reference.md) | All public types and methods |
| [Query Reference](docs/query-reference.md) | Fluent API usage guide |
| [Architecture](docs/architecture.md) | Design decisions |
| [Changelog](docs/changelog.md) | Version history |

## Dependencies

- SmartPipe.Core >= 1.0.5
- Microsoft.Data.Sqlite >= 9.0.0

## License

MIT
