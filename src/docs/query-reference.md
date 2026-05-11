# SmartPipe.Memory Query Reference v0.1.1

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

### Find nodes with AND filter (default)

    await foreach (var result in query
        .Nodes("File")
        .Where("HealthScore", FilterOperator.LessThan, 0.5)
        .And()
        .Where("FailureProb", FilterOperator.GreaterThan, 0.1)
        .ExecuteAsync())
    {
        Console.WriteLine($"{result.Node.Label}: at risk");
    }

### Find nodes with OR filter

    await foreach (var result in query
        .Nodes("File")
        .Where("HealthScore", FilterOperator.LessThan, 0.35)
        .Or()
        .Where("HealthScore", FilterOperator.GreaterThan, 0.8)
        .ExecuteAsync())
    {
        Console.WriteLine($"{result.Node.Label}: extreme health");
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

### Find shortest path with minimum edge weight

    await foreach (var result in query
        .ShortestPath("file1", "file2", "DerivedFrom")
        .MinWeight(0.5)
        .ExecuteAsync())
    {
        Console.WriteLine($"Path: {string.Join(" -> ", result.Path)}");
    }

### Find shortest path with minimum edge confidence

    await foreach (var result in query
        .ShortestPath("file1", "file2", "DerivedFrom")
        .MinConfidence(0.9)
        .ExecuteAsync())
    {
        Console.WriteLine($"Path: {string.Join(" -> ", result.Path)}");
    }

### Find shortest path with node filter

    await foreach (var result in query
        .ShortestPath("file1", "file2", "DerivedFrom")
        .WhereNode(node => node.HealthScore > 0.3)
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

### Traverse with node filter

    await foreach (var result in query
        .StartFrom("file1")
        .Traverse("DerivedFrom", maxDepth: 3)
        .WhereNode(node => node.HealthScore > 0.3)
        .ExecuteAsync())
    {
        Console.WriteLine($"{result.Node.Label} at depth {result.Depth}");
    }

## Time-Travel Queries

### Query graph at a point in time (AsOf)

    await foreach (var result in query
        .Nodes("File")
        .AsOf(DateTime.UtcNow.AddDays(-7))
        .ExecuteAsync())
    {
        Console.WriteLine($"{result.Node.Label} existed 7 days ago");
    }

### Query graph in a time range (Between)

    await foreach (var result in query
        .Nodes("File")
        .Between(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow)
        .ExecuteAsync())
    {
        Console.WriteLine($"{result.Node.Label} changed in last 30 days");
    }

## Clustering

### Run Leiden clustering

    await foreach (var result in query.FindClusters())
    {
        Console.WriteLine($"Cluster {result.Cluster!.Id}: {result.Cluster.Size} nodes");
    }

## Node Statistics

### Estimate unique neighbors (HyperLogLog)

    var estimate = query.EstimateNeighbors("file1");
    Console.WriteLine($"~{estimate} unique neighbors");

### Count direct outgoing edges

    var degree = query.HasDegree("file1");
    Console.WriteLine($"{degree} direct connections");

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

### AND/OR with node filters

    await foreach (var result in query
        .Nodes("File")
        .Where("HealthScore", FilterOperator.LessThan, 0.5)
        .Or()
        .Where("PredictedLatencyMs", FilterOperator.GreaterThan, 200)
        .WhereNode(node => node.ResourceStrain < 0.7)
        .ExecuteAsync())
    {
        Console.WriteLine($"{result.Node.Label}: at risk");
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

### Run clustering directly

    var clusters = await store.ClusterAsync();
    foreach (var cluster in clusters)
        Console.WriteLine($"Cluster {cluster.Id}: {cluster.Size} nodes");

## Performance Tips

1. Use NodeCache to reduce repeated lookups
2. Use Limit to restrict traversal size on large graphs
3. Use MaxDepth to prevent unbounded traversal
4. Use MinWeight to skip weak edges during pathfinding
5. Use InMemoryGraphStore for maximum query speed
6. Use SqliteWALStore with batch insert for production durability
7. Enable AutoClassifier only when needed to avoid overhead
