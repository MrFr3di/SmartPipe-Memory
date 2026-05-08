# Getting Started with SmartPipe.Memory

## Installation

dotnet add package SmartPipe.Memory
dotnet add package SmartPipe.Memory.Extensions

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

    await foreach (var result in query
        .Nodes("File")
        .Where("HealthScore", FilterOperator.LessThan, 0.5)
        .ExecuteAsync())
    {
        Console.WriteLine($"{result.Node.Label}: {result.Node.HealthScore}");
    }

## 5. Find Shortest Path

    await foreach (var result in query
        .ShortestPath("file1", "file2", "DuplicateOf")
        .ExecuteAsync())
    {
        Console.WriteLine($"Path: {string.Join(" -> ", result.Path)}");
    }

## 6. Integrate with SmartPipe Pipeline

    var pipeline = new SmartPipeChannel<MyInput, MyOutput>(options);
    pipeline.AddSource(mySource);
    pipeline.AddTransformer(myTransformer);
    pipeline.AddSink(mySink);
    pipeline.UseMemory(store);
    await pipeline.RunAsync();

## 7. Dependency Injection

In-memory:
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