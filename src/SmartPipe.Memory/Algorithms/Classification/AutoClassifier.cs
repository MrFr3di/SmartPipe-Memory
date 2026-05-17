namespace SmartPipe.Memory.Algorithms.Classification;

/// <summary>
/// Automatically classifies nodes by type based on their properties.
/// </summary>
public sealed class AutoClassifier
{
    /// <summary>
    /// Classify a node by its properties.
    /// Returns the detected type or "Unknown".
    /// </summary>
    public string Classify(Graph.Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.Properties.ContainsKey("hash") && node.Properties.ContainsKey("path"))
            return "File";

        if (node.Properties.ContainsKey("sql") || node.Properties.ContainsKey("connectionString"))
            return "DatabaseRecord";

        if (node.Properties.ContainsKey("stream"))
            return "Stream";

        return "Unknown";
    }

    /// <summary>
    /// Classify an edge type based on node properties and transformation steps.
    /// </summary>
    public Graph.EdgeType ClassifyEdge(Graph.Node from, Graph.Node to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        if (
            from.Properties.TryGetValue("hash", out var fromHash)
            && to.Properties.TryGetValue("hash", out var toHash)
            && fromHash.Equals(toHash)
        )
            return Graph.EdgeType.DuplicateOf;

        if (
            from.Properties.TryGetValue("path", out var fromPath)
            && to.Properties.TryGetValue("path", out var toPath)
            && AreVersions(fromPath?.ToString() ?? "", toPath?.ToString() ?? "")
        )
            return Graph.EdgeType.VersionOf;

        return Graph.EdgeType.DerivedFrom;
    }

    private static bool AreVersions(string path1, string path2)
    {
        var name1 = Path.GetFileNameWithoutExtension(path1);
        var name2 = Path.GetFileNameWithoutExtension(path2);

        return name1.StartsWith(name2) || name2.StartsWith(name1);
    }
}
