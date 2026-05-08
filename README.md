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
| `SmartPipe.Memory` | Core graph engine | v0.1.0 |
| `SmartPipe.Memory.Extensions` | Integration with SmartPipe.Core | v0.1.0 |
| `SmartPipe.Memory.Health` | Predictive analytics | v0.2.0 (planned) |

## Installation

```csharp
dotnet add package SmartPipe.Memory
dotnet add package SmartPipe.Memory.Extensions
```

## Quick Start

```csharp
var store = StoreFactory.CreateInMemory();

await store.UpsertNodeAsync(new Node { Id = "f1", Type = "File", Label = "doc.pdf" });
await store.UpsertNodeAsync(new Node { Id = "f2", Type = "File", Label = "copy.pdf" });
await store.UpsertEdgeAsync(new Edge { FromNodeId = "f1", ToNodeId = "f2", Type = EdgeType.DuplicateOf });

var query = new MemoryQueryBuilder(new MemoryQueryExecutor(store, new NodeCache()));

await foreach (var r in query.ShortestPath("f1", "f2", "DuplicateOf").ExecuteAsync())
    Console.WriteLine(string.Join(" -> ", r.Path));
```


## Key Features

- Type-safe Fluent API for graph queries
- In-memory traversal engine (BFS, Dijkstra)
- Leiden community detection
- HyperLogLog cardinality estimation
- SQLite WAL persistence
- Bitemporal data model (ValidFrom/ValidTo/TxTime)
- OpenTelemetry metrics and tracing
- Zero external servers — embedded in-process
- 84 tests, 0 failures

## Documentation

| Document | Description |
|:---|:---|
| [Features](docs/features.md) | Complete feature reference |
| [Getting Started](docs/getting-started.md) | Quick start guide |
| [API Reference](docs/api-reference.md) | All public types and methods |
| [Query Reference](docs/query-reference.md) | Fluent API usage guide |
| [Architecture](docs/architecture.md) | Design decisions |

## Dependencies

- SmartPipe.Core >= 1.0.5
- Microsoft.Data.Sqlite >= 9.0.0

## License

MIT
