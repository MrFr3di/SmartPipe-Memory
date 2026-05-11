using SmartPipe.Memory.Algorithms.Classification;
using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Storage;

namespace SmartPipe.Memory.Tests.Storage;

public sealed class InMemoryGraphStoreAutoClassifierTests : IAsyncDisposable
{
    private readonly InMemoryGraphStore _store;

    public InMemoryGraphStoreAutoClassifierTests()
    {
        _store = new InMemoryGraphStore();
    }

    [Fact]
    public async Task UpsertNode_WithClassifier_ClassifiesType()
    {
        _store.Classifier = new AutoClassifier();
        var node = new Node
        {
            Id = "test",
            Type = "", // empty
            Properties = new Dictionary<string, object>
            {
                ["hash"] = "abc",
                ["path"] = "/data/file.txt"
            }
        };

        var result = await _store.UpsertNodeAsync(node);
        Assert.Equal("File", result.Type);
    }

    [Fact]
    public async Task UpsertNode_WithoutClassifier_LeavesTypeEmpty()
    {
        var node = new Node
        {
            Id = "test",
            Type = "",
            Properties = new Dictionary<string, object>
            {
                ["hash"] = "abc"
            }
        };

        var result = await _store.UpsertNodeAsync(node);
        Assert.Equal("", result.Type);
    }

    public ValueTask DisposeAsync() => _store.DisposeAsync();
}