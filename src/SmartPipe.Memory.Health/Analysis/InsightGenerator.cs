using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Health.Analysis;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Health.Analysis;

/// <summary>
/// Generates Insight objects from bottleneck predictions and graph state.
/// </summary>
public sealed class InsightGenerator
{
    private readonly BottleneckPredictor _predictor;
    private readonly IGraphStore _store;

    /// <summary>
    /// Create a new InsightGenerator.
    /// </summary>
    /// <param name="predictor">Bottleneck predictor for generating predictions.</param>
    /// <param name="store">Graph store for persisting insights.</param>
    public InsightGenerator(BottleneckPredictor predictor, IGraphStore store)
    {
        _predictor = predictor ?? throw new ArgumentNullException(nameof(predictor));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// Generate an insight from a bottleneck prediction.
    /// </summary>
    /// <param name="prediction">Bottleneck prediction result.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The generated insight.</returns>
    public async Task<Insight> GenerateFromPredictionAsync(
        BottleneckPrediction prediction,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(prediction);

        var insight = new Insight
        {
            Id = $"insight_{Guid.NewGuid():N}",
            Type = prediction.IsBottleneck ? "BottleneckPrediction" : "HealthDegradation",
            Title = prediction.IsBottleneck
                ? $"Bottleneck predicted for {prediction.NodeId}"
                : $"Health degrading for {prediction.NodeId}",
            Description = prediction.IsBottleneck
                ? $"Node {prediction.NodeId} is predicted to become a bottleneck. "
                    + $"Current latency: {prediction.CurrentHealth?.PredictedLatencyMs:F1}ms. "
                    + $"Health score: {prediction.CurrentHealth?.HealthScore:F2}. "
                    + $"Estimated time to impact: {prediction.TimeToImpactMs:F0}ms."
                : $"Node {prediction.NodeId} health is degrading. "
                    + $"Health delta: {prediction.HealthDelta:F2}. "
                    + $"Latency delta: {prediction.LatencyDelta:F1}ms.",
            RelatedNodeIds = new[] { prediction.NodeId },
            Confidence = prediction.Confidence,
            Severity = prediction.IsBottleneck ? "Critical" : "Warning",
            GeneratedAt = DateTime.UtcNow,
        };

        await _store.InsertInsightAsync(insight, ct);
        return insight;
    }

    /// <summary>
    /// Analyze a node and generate insights if problems are detected.
    /// </summary>
    /// <param name="nodeId">Node identifier to analyze.</param>
    /// <param name="currentMetrics">Current metrics snapshot.</param>
    /// <param name="historicalMetrics">Historical metrics for comparison.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Generated insight, or null if no problems detected.</returns>
    public async Task<Insight?> AnalyzeNodeAsync(
        string nodeId,
        MetricsEntry currentMetrics,
        IReadOnlyList<MetricsEntry> historicalMetrics,
        CancellationToken ct = default
    )
    {
        var historicalTimestamp =
            historicalMetrics.Count > 0 ? historicalMetrics[^1].Timestamp : DateTime.UtcNow;

        var prediction = await _predictor.PredictAsync(
            nodeId,
            currentMetrics,
            historicalMetrics,
            historicalTimestamp,
            ct
        );

        if (!prediction.IsBottleneck && prediction.HealthDelta <= 0.1)
            return null;

        return await GenerateFromPredictionAsync(prediction, ct);
    }

    /// <summary>
    /// Generate an insight for exhausted retry budget.
    /// </summary>
    /// <param name="nodeId">Node identifier.</param>
    /// <param name="retryBudget">Remaining retry budget.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The generated insight.</returns>
    public async Task<Insight> GenerateRetryBudgetExhaustedAsync(
        string nodeId,
        int retryBudget,
        CancellationToken ct = default
    )
    {
        var insight = new Insight
        {
            Id = $"insight_{Guid.NewGuid():N}",
            Type = "RetryBudgetExhausted",
            Title = $"Retry budget exhausted for {nodeId}",
            Description =
                $"Node {nodeId} has exhausted its retry budget. Remaining: {retryBudget}.",
            RelatedNodeIds = new[] { nodeId },
            Confidence = 1.0,
            Severity = "Warning",
            GeneratedAt = DateTime.UtcNow,
        };

        await _store.InsertInsightAsync(insight, ct);
        return insight;
    }

    /// <summary>
    /// Generate an insight for discovered clusters.
    /// </summary>
    /// <param name="cluster">Discovered cluster.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The generated insight.</returns>
    public async Task<Insight> GenerateClusterDiscoveredAsync(
        Cluster cluster,
        CancellationToken ct = default
    )
    {
        var insight = new Insight
        {
            Id = $"insight_{Guid.NewGuid():N}",
            Type = "ClusterDiscovered",
            Title = $"Cluster discovered with {cluster.Size} nodes",
            Description =
                $"A cluster of {cluster.Size} nodes was discovered. Modularity: {cluster.Modularity:F3}.",
            RelatedNodeIds = cluster.NodeIds,
            Confidence = cluster.Modularity,
            Severity = "Info",
            GeneratedAt = DateTime.UtcNow,
        };

        await _store.InsertInsightAsync(insight, ct);
        return insight;
    }
}
