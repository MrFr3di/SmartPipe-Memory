using Microsoft.Extensions.Hosting;
using SmartPipe.Memory.Health.Analysis;
using SmartPipe.Memory.Storage;


namespace SmartPipe.Memory.Health.Infrastructure;

/// <summary>
/// Background service that periodically analyzes node health
/// and generates insights without explicit user requests.
/// </summary>
public sealed class InsightAgent : BackgroundService
{
    private readonly IGraphStore _store;
    private readonly InsightGenerator _insightGenerator;
    private readonly CognitiveConsolidation _consolidation;
    private readonly TimeSpan _interval;

    /// <summary>
    /// Create a new InsightAgent.
    /// </summary>
    /// <param name="store">Graph store for retrieving metrics and persisting insights.</param>
    /// <param name="insightGenerator">Insight generator.</param>
    /// <param name="consolidation">Cognitive consolidation for repeated insights.</param>
    /// <param name="interval">Interval between analysis cycles. Default: 30 seconds.</param>
    public InsightAgent(
        IGraphStore store,
        InsightGenerator insightGenerator,
        CognitiveConsolidation consolidation,
        TimeSpan? interval = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _insightGenerator = insightGenerator ?? throw new ArgumentNullException(nameof(insightGenerator));
        _consolidation = consolidation ?? throw new ArgumentNullException(nameof(consolidation));
        _interval = interval ?? TimeSpan.FromSeconds(30);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AnalyzeUnhealthyNodesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // Log and continue
                await Task.Delay(_interval, stoppingToken);
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task AnalyzeUnhealthyNodesAsync(CancellationToken ct)
    {
        var query = new Model.MemoryQuery
        {
            Type = Model.QueryType.FindNodes
        };

        await foreach (var node in _store.QueryNodesAsync(query, ct))
        {
            ct.ThrowIfCancellationRequested();

            if (node.HealthScore >= 0.7)
                continue;
            _ = await _store.GetWeakenedEdgesFromAsync(node.Id, ct);

            var snapshot = new MetricsEntry
            {
                NodeId = node.Id,
                Timestamp = DateTime.UtcNow,
                Values = new Dictionary<string, double>
                {
                    ["AvgLatencyMs"] = node.PredictedLatencyMs,
                    ["SmoothThroughput"] = 0,
                    ["ItemsFailed"] = 0
                }
            };

            var insight = await _insightGenerator.AnalyzeNodeAsync(
                node.Id, snapshot, [snapshot], ct);

            if (insight is not null)
            {
                var existingInsights = await GetAllInsightsForNodeAsync(node.Id, ct);
                await _consolidation.ConsolidateAsync(insight, existingInsights, ct);
            }
        }
    }

    private async Task<IReadOnlyList<Graph.Insight>> GetAllInsightsForNodeAsync(
        string nodeId,
        CancellationToken ct)
    {
        var insights = new List<Graph.Insight>();
        var query = new Model.MemoryQuery
        {
            Type = Model.QueryType.FindInsights
        };

        await foreach (var edge in _store.QueryInsightsAsync(query, ct))
        {
            if (edge.FromNodeId == nodeId || edge.ToNodeId == nodeId)
            {
                insights.Add(new Graph.Insight
                {
                    Id = edge.ToNodeId,
                    Type = edge.Type.ToString(),
                    RelatedNodeIds = [edge.FromNodeId],
                    Confidence = edge.Confidence,
                    GeneratedAt = edge.ValidFrom
                });
            }
        }

        return insights;
    }
}