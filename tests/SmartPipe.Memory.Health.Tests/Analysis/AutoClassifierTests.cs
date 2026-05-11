using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Algorithms.Classification;

namespace SmartPipe.Memory.Health.Tests.Analysis;

public sealed class AutoClassifierTests
{
    private readonly AutoClassifier _classifier = new();

    [Fact]
    public void Classify_WithHashAndPath_ReturnsFile()
    {
        var node = new Node
        {
            Id = "f1",
            Type = "",
            Properties = new Dictionary<string, object>
            {
                ["hash"] = "abc123",
                ["path"] = "/docs/file.txt"
            }
        };

        var result = _classifier.Classify(node);

        Assert.Equal("File", result);
    }

    [Fact]
    public void Classify_WithSql_ReturnsDatabaseRecord()
    {
        var node = new Node
        {
            Id = "r1",
            Type = "",
            Properties = new Dictionary<string, object>
            {
                ["sql"] = "SELECT * FROM table",
                ["connectionString"] = "..."
            }
        };

        var result = _classifier.Classify(node);

        Assert.Equal("DatabaseRecord", result);
    }

    [Fact]
    public void Classify_NoMatchingProperties_ReturnsUnknown()
    {
        var node = new Node { Id = "x1", Type = "", Properties = new Dictionary<string, object>() };

        var result = _classifier.Classify(node);

        Assert.Equal("Unknown", result);
    }

    [Fact]
    public void ClassifyEdge_SameHash_ReturnsDuplicateOf()
    {
        var from = new Node { Properties = new Dictionary<string, object> { ["hash"] = "abc" } };
        var to = new Node { Properties = new Dictionary<string, object> { ["hash"] = "abc" } };

        var result = _classifier.ClassifyEdge(from, to);

        Assert.Equal(EdgeType.DuplicateOf, result);
    }

    [Fact]
    public void ClassifyEdge_SimilarNames_ReturnsVersionOf()
    {
        var from = new Node { Properties = new Dictionary<string, object> { ["path"] = "/docs/report_v1.pdf" } };
        var to = new Node { Properties = new Dictionary<string, object> { ["path"] = "/docs/report_v2.pdf" } };

        var result = _classifier.ClassifyEdge(from, to);

        Assert.Equal(EdgeType.DerivedFrom, result);
    }

    [Fact]
    public void ClassifyEdge_NoMatch_ReturnsDerivedFrom()
    {
        var from = new Node { Properties = new Dictionary<string, object>() };
        var to = new Node { Properties = new Dictionary<string, object>() };

        var result = _classifier.ClassifyEdge(from, to);

        Assert.Equal(EdgeType.DerivedFrom, result);
    }
}