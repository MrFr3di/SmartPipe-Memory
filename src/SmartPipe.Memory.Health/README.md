# SmartPipe.Memory.Health

Predictive analytics and health monitoring for SmartPipe.Memory graph stores.

## Installation

```bash
dotnet add package SmartPipe.Memory.Health
```

## Quick Start

```csharp
using SmartPipe.Core;
using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Storage;
using SmartPipe.Memory.Health;
using SmartPipe.Memory.Health.Analysis;
using SmartPipe.Memory.Health.Infrastructure;

// 1. Create an in‑memory store and populate a node
var store = StoreFactory.CreateInMemory();
await store.UpsertNodeAsync(new Node { Id = "node1", Type = "Transformer" });

// 2. Build metric history (normally streamed from a pipeline)
var metricHistory = new List<MetricsEntry>
{
    new()
    {
        NodeId = "node1",
        Timestamp = DateTime.UtcNow.AddSeconds(-30),
        Values = new Dictionary<string, double>
        {
            ["AvgLatencyMs"] = 50,
            ["SmoothThroughput"] = 100,
            ["ItemsFailed"] = 0
        }
    },
    new()
    {
        NodeId = "node1",
        Timestamp = DateTime.UtcNow,
        Values = new Dictionary<string, double>
        {
            ["AvgLatencyMs"] = 120,
            ["SmoothThroughput"] = 95,
            ["ItemsFailed"] = 2
        }
    }
};

// 3. Compute the health vector
var calculator = new HealthVectorCalculator(store);
var health = await calculator.ComputeAsync("node1", metricHistory, retryBudget: 3);
Console.WriteLine($"HealthScore: {health.HealthScore:F2}");

// 4. Predict bottlenecks using temporal comparison
var predictor = new BottleneckPredictor(calculator, store);
var prediction = await predictor.PredictAsync(
    "node1",
    metricHistory[^1],               // current snapshot
    metricHistory,                   // full history
    metricHistory[0].Timestamp);     // timestamp of the first snapshot

Console.WriteLine($"Is bottleneck: {prediction.IsBottleneck}");
Console.WriteLine($"Time to impact: {prediction.TimeToImpactMs:F0} ms");

// 5. Generate insights and start a background agent
var generator = new InsightGenerator(predictor, store);
var consolidation = new CognitiveConsolidation(store);
var agent = new InsightAgent(store, generator, consolidation, TimeSpan.FromSeconds(30));

await agent.StartAsync(CancellationToken.None);

// Let the agent run for a while ...
await Task.Delay(TimeSpan.FromSeconds(5));

await agent.StopAsync(CancellationToken.None);
Console.WriteLine("Agent stopped.");
```

## Key Features

- HealthVector – predictive health scoring with configurable weights
- BottleneckPredictor – temporal comparison for bottleneck detection
- InsightGenerator – generates actionable insights from predictions
- CognitiveConsolidation – merges repeated insights for higher confidence
- MemoryDecayPolicy – adaptive edge weight decay over time
- ConflictResolver – weakens old edges on contradictory facts
- InsightAgent – background service for periodic health analysis
- MetricsBackgroundConsumer – async channel-based metrics persistence

## Dependencies

- SmartPipe.Memory (>= 0.1.3)
- SmartPipe.Core (>= 1.0.6)

## License

MIT
