# SmartPipe.Memory.Extensions

Integration library for connecting SmartPipe.Memory with SmartPipe.Core ETL pipelines.

## Installation

```bash
dotnet add package SmartPipe.Memory.Extensions
```

## Quick Start

```csharp
using SmartPipe.Core;
using SmartPipe.Memory.Extensions;
using SmartPipe.Memory.Storage;

var store = StoreFactory.CreateInMemory();

var pipeline = new SmartPipeChannel<MyInput, MyOutput>(options);
pipeline.AddSource(mySource);
pipeline.AddTransformer(myTransformer);
pipeline.AddSink(mySink);

// Connect memory to the pipeline - auto-registers topology and streams metrics
pipeline.UseMemory(store);

await pipeline.RunAsync();
```

## Key Features

- UseMemory() – attaches a memory store to any SmartPipe pipeline
- AsGraphSource() – reads graph nodes as a pipeline source
- ToGraphSink() – writes pipeline results directly to the graph
- TransformToEdges() – converts pipeline elements to graph edges
- Automatic pipeline topology registration
- etrics streaming via channel-based buffering

## Dependencies

- SmartPipe.Memory (>= 0.1.1)
- SmartPipe.Core (>= 1.0.5)

## License
MIT