# SmartPipe.Memory.Health

Predictive analytics and health monitoring for SmartPipe.Memory graph stores.

## Installation

```bash
dotnet add package SmartPipe.Memory.Health
```

## Quick Start

```csharp
using SmartPipe.Core;
using SmartPipe.Memory.Health;
using SmartPipe.Memory.Storage;

var store = StoreFactory.CreateInMemory();

// Compute health vector for a node
var calculator = new HealthVectorCalculator(store);
var health = await calculator.ComputeAsync("node1", metricHistory);

// Predict bottlenecks
var predictor = new BottleneckPredictor(calculator, store);
var prediction = await predictor.PredictAsync("node1", currentMetrics, historicalMetrics, timestamp);

// Start background health monitoring
var agent = new InsightAgent(store, insightGenerator, consolidation);
await agent.StartAsync(ct);
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

- SmartPipe.Memory (>= 0.1.1)
- SmartPipe.Core (>= 1.0.5)

## License
MIT