using System.Diagnostics;

namespace SmartPipe.Memory.Diagnostics;

/// <summary>
/// OpenTelemetry tracing for SmartPipe.Memory operations.
/// Creates spans for queries, mutations, and clustering.
/// </summary>
public static class MemoryActivitySource
{
    /// <summary>
    /// Activity source name for SmartPipe.Memory.
    /// </summary>
    public const string SourceName = "SmartPipe.Memory";

    /// <summary>
    /// Activity source instance.
    /// </summary>
    public static readonly ActivitySource Source = new(SourceName, "0.1.0");

    /// <summary>
    /// Start a span for a query execution.
    /// </summary>
    /// <param name="queryType">Type of query being executed.</param>
    /// <returns>A new activity, or null if no listeners.</returns>
    public static Activity? StartQuery(string queryType)
    {
        var activity = Source.StartActivity("ExecuteQuery", ActivityKind.Internal);
        activity?.SetTag("memory.query.type", queryType);
        return activity;
    }

    /// <summary>
    /// Start a span for a node upsert.
    /// </summary>
    /// <param name="nodeId">Node identifier being upserted.</param>
    /// <returns>A new activity, or null if no listeners.</returns>
    public static Activity? StartUpsertNode(string nodeId)
    {
        var activity = Source.StartActivity("UpsertNode", ActivityKind.Internal);
        activity?.SetTag("memory.node.id", nodeId);
        return activity;
    }

    /// <summary>
    /// Start a span for an edge upsert.
    /// </summary>
    /// <param name="fromNodeId">Source node identifier.</param>
    /// <param name="toNodeId">Target node identifier.</param>
    /// <returns>A new activity, or null if no listeners.</returns>
    public static Activity? StartUpsertEdge(string fromNodeId, string toNodeId)
    {
        var activity = Source.StartActivity("UpsertEdge", ActivityKind.Internal);
        activity?.SetTag("memory.edge.from", fromNodeId);
        activity?.SetTag("memory.edge.to", toNodeId);
        return activity;
    }

    /// <summary>
    /// Start a span for a clustering operation.
    /// </summary>
    /// <param name="nodeCount">Number of nodes being clustered.</param>
    /// <returns>A new activity, or null if no listeners.</returns>
    public static Activity? StartClustering(int nodeCount)
    {
        var activity = Source.StartActivity("Cluster", ActivityKind.Internal);
        activity?.SetTag("memory.nodes.count", nodeCount);
        return activity;
    }
}
