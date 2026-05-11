# Getting Started with SmartPipe.Memory v0.1.1

## Installation

dotnet add package SmartPipe.Memory
dotnet add package SmartPipe.Memory.Extensions
dotnet add package SmartPipe.Memory.Health

## 1. Create a Memory Store

In-memory (testing):
    var store = StoreFactory.CreateInMemory();

SQLite (production):
    var store = StoreFactory.CreateSqlite("myapp.db");
    await store.InitializeAsync();

## 2. Add Nodes

    await store.UpsertNodeAsync(new Node
    {
        Id = "file1",
        Type = "File",
        Label = "document.pdf",
        Properties = new Dictionary<string, object>
        {
            ["path"] = "/docs/document.pdf",
            ["size"] = 2_500_000L,
            ["hash"] = "sha256:abc123..."
        }
    });

## 3. Add Edges

    await store.UpsertEdgeAsync(new Edge
    {
        FromNodeId = "file1",
        ToNodeId = "file2",
        Type = EdgeType.DuplicateOf,
        Weight = 0.98,
        Confidence = 1.0
    });

## 4. Query the Graph

    var cache = new NodeCache();
    var executor = new MemoryQueryExecutor(store, cache);
    var query = new MemoryQueryBuilder(executor);

    // Find all files
    await foreach (var result in query.Nodes("File").ExecuteAsync())
    {
        Console.WriteLine(result.Node.Label);
    }

    // Find files with low health
    await foreach (var result in query
        .Nodes("File")
        .Where("HealthScore", FilterOperator.LessThan, 0.5)
        .ExecuteAsync())
    {
        Console.WriteLine($"{result.Node.Label}: {result.Node.HealthScore}");
    }

    // Find files with compound filter (AND/OR)
    await foreach (var result in query
        .Nodes("File")
        .Where("HealthScore", FilterOperator.LessThan, 0.5)
        .Or()
        .Where("FailureProb", FilterOperator.GreaterThan, 0.1)
        .ExecuteAsync())
    {
        Console.WriteLine($"{result.Node.Label}: at risk");
    }

## 5. Find Shortest Path

    await foreach (var result in query
        .ShortestPath("file1", "file2", "DuplicateOf")
        .ExecuteAsync())
    {
        Console.WriteLine($"Path: {string.Join(" -> ", result.Path)}");
    }

## 6. Time-Travel Query

    // See what the graph looked like 7 days ago
    await foreach (var result in query
        .Nodes("File")
        .AsOf(DateTime.UtcNow.AddDays(-7))
        .ExecuteAsync())
    {
        Console.WriteLine($"{result.Node.Label} existed 7 days ago");
    }

## 7. Clustering

    // Find communities in the graph
    await foreach (var result in query.FindClusters())
    {
        Console.WriteLine($"Cluster {result.Cluster!.Id}: {result.Cluster.Size} nodes");
    }

## 8. Node Statistics

    // Estimate unique neighbors
    var estimate = query.EstimateNeighbors("file1");
    Console.WriteLine($"~{estimate} unique neighbors");

    // Count direct connections
    var degree = query.HasDegree("file1");
    Console.WriteLine($"{degree} direct connections");

## 9. Integrate with SmartPipe Pipeline

    var pipeline = new SmartPipeChannel<MyInput, MyOutput>(options);
    pipeline.AddSource(mySource);
    pipeline.AddTransformer(myTransformer);
    pipeline.AddSink(mySink);
    pipeline.UseMemory(store);
    await pipeline.RunAsync();

## 10. Auto‑Classification

    // Enable auto‑classification to detect node types automatically
    builder.Services.AddSmartPipeMemory(options =>
    {
        options.EnableAutoClassification = true;
    });

    // Nodes with empty Type will be classified based on Properties:
    //   hash + path → "File"
    //   sql + connectionString → "DatabaseRecord"

## 11. Dependency Injection

In‑memory:
    builder.Services.AddSmartPipeMemory();

SQLite:
    builder.Services.AddSmartPipeMemorySqlite(options =>
    {
        options.ConnectionString = "flowkeep.db";
        options.MaxCacheSize = 10_000;
    });

## Next Steps

- Read the API Reference for all public methods
- Read the Query Reference for advanced queries
- Read the Architecture for design decisions
