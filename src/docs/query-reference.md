# SmartPipe.Memory Query Reference v0.1.0

## Overview

SmartPipe.Memory uses a type-safe Fluent API for all queries. No text-based query language. Every query is validated by the C# compiler.

## Basic Queries

### Find all nodes of a type

    await foreach (var result in query
        .Nodes("File")
        .ExecuteAsync())
    {
        Console.WriteLine(result.Node.Label);
    }

### Find nodes with health filter

    await foreach (var result in query
        .Nodes("File")
        .Where("HealthScore", FilterOperator.LessThan, 0.5)
        .ExecuteAsync())
    {
        Console.WriteLine($"{result.Node.Label}: {result.Node.HealthScore}");
    }

### Find nodes with multiple filters

    await foreach (var result in query
        .Nodes("File")
        .Where("HealthScore", FilterOperator.LessThan, 0.5)
        .Where("FailureProb", FilterOperator.GreaterThan, 0.1)
        .ExecuteAsync())
    {
        Console.WriteLine($"{result.Node.Label}: at risk");
    }

### Limit results

    await foreach (var result in query
        .Nodes("File")
        .Limit(10)
        .ExecuteAsync())
    {
        Console.WriteLine(result.Node.Label);
    }

### Order results

    await foreach (var result in query
        .Nodes("File")
        .OrderBy("HealthScore", descending: false)
        .ExecuteAsync())
    {
        Console.WriteLine($"{result.Node.Label}: {result.Node.HealthScore}");
    }

## Pathfinding

### Find shortest path by edge count (BFS)

    await foreach (var result in query
        .ShortestPath("file1", "file2", "DerivedFrom")
        .ExecuteAsync())
    {
        Console.WriteLine($"Path: {string.Join(" -> ", result.Path)}");
        Console.WriteLine($"Total weight: {result.TotalWeight}");
    }

### Find shortest path with max depth

    await foreach (var result in query
        .ShortestPath("file1", "file2", "DerivedFrom")
        .MaxDepth(5)
        .ExecuteAsync())
    {
        Console.WriteLine($"Path: {string.Join(" -> ", result.Path)}");
    }

## Traversal

### Traverse from a node

    await foreach (var result in query
        .StartFrom("file1")
        .Traverse("DerivedFrom", maxDepth: 3)
        .ExecuteAsync())
    {
        Console.WriteLine($"Depth {result.Depth}: {result.Node.Label}");
    }

### Traverse with limit

    await foreach (var result in query
        .StartFrom("file1")
        .Traverse("VersionOf", maxDepth: 5)
        .Limit(20)
        .ExecuteAsync())
    {
        Console.WriteLine($"{result.Node.Label} at depth {result.Depth}");
    }

## Filter Reference

### Available Properties

| Property | Description |
|:---|:---|
| `HealthScore` | Aggregated health 0..1 |
| `FailureProb` | Failure probability 0..1 |
| `ResourceStrain` | Resource strain 0..1 |
| `PredictedLatencyMs` | Predicted latency in ms |

### Available Operators

| Operator | Description |
|:---|:---|
| `FilterOperator.LessThan` | Property < value |
| `FilterOperator.GreaterThan` | Property > value |
| `FilterOperator.Equals` | Property == value |

### OrderBy Properties

| Property | Description |
|:---|:---|
| `HealthScore` | Sort by health |
| `CreatedAt` | Sort by ValidFrom timestamp |

## Edge Types

| EdgeType | String value | Description |
|:---|:---|:---|
| `EdgeType.DerivedFrom` | "DerivedFrom" | Target derived from source |
| `EdgeType.DuplicateOf` | "DuplicateOf" | Target is duplicate |
| `EdgeType.VersionOf` | "VersionOf" | Target is version |
| `EdgeType.AggregatedFrom` | "AggregatedFrom" | Target is aggregation |
| `EdgeType.FilteredFrom` | "FilteredFrom" | Target passed filter |
| `EdgeType.FeedsInto` | "FeedsInto" | Pipeline component connection |

## Result Types

| ResultType | Contains | When |
|:---|:---|:---|
| `Node` | `result.Node` | FindNodes, Traverse |
| `Path` | `result.Path`, `result.TotalWeight` | FindPath |
| `Edge` | `result.Edge` | QueryInsights |
| `Cluster` | `result.Cluster` | Clustering |

## Combining Queries

### StartFrom with ConnectedVia

    await foreach (var result in query
        .StartFrom("file1")
        .ConnectedVia("DuplicateOf")
        .ExecuteAsync())
    {
        Console.WriteLine(result.Node.Label);
    }

### Nodes with OrderBy and Limit

    await foreach (var result in query
        .Nodes("File")
        .Where("HealthScore", FilterOperator.LessThan, 0.7)
        .OrderBy("HealthScore", descending: false)
        .Limit(5)
        .ExecuteAsync())
    {
        Console.WriteLine($"{result.Node.Label}: {result.Node.HealthScore}");
    }

## Direct Store Access

### Get a single node

    var node = await store.GetNodeAsync("file1");
    if (node is not null)
        Console.WriteLine(node.Label);

### Find path directly

    var path = await store.FindPathAsync("A", "B", "DerivedFrom", maxDepth: 10);
    foreach (var segment in path)
        Console.WriteLine(segment.NodeId);

### Traverse directly

    await foreach (var (node, depth) in store.TraverseAsync("A", "DerivedFrom", 5, 100))
        Console.WriteLine($"{node.Label} at depth {depth}");

## Performance Tips

1. Use NodeCache to reduce repeated lookups
2. Use Limit to restrict traversal size on large graphs
3. Use MaxDepth to prevent unbounded traversal
4. Use InMemoryGraphStore for maximum query speed
5. Use SqliteWALStore with batch insert for production durability
