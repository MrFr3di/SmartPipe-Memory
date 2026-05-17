using SmartPipe.Memory.Caching;
using SmartPipe.Memory.Graph;

namespace SmartPipe.Memory.Tests.Caching;

public sealed class NodeCacheTests
{
    [Fact]
    public void TryGet_NonExistent_ReturnsFalse()
    {
        var cache = new NodeCache(100);

        var found = cache.TryGet("nonexistent", out var node);

        Assert.False(found);
        Assert.Null(node);
    }

    [Fact]
    public void Set_ThenTryGet_ReturnsNode()
    {
        var cache = new NodeCache(100);
        var node = new Node { Id = "n1", Type = "File" };

        cache.Set("n1", node);
        var found = cache.TryGet("n1", out var cached);

        Assert.True(found);
        Assert.NotNull(cached);
        Assert.Equal("n1", cached!.Id);
    }

    [Fact]
    public void Invalidate_RemovesNode()
    {
        var cache = new NodeCache(100);
        cache.Set("n1", new Node { Id = "n1", Type = "File" });

        cache.Invalidate("n1");
        var found = cache.TryGet("n1", out _);

        Assert.False(found);
    }

    [Fact]
    public void Evicts_WhenFull()
    {
        var cache = new NodeCache(3);

        cache.Set("n1", new Node { Id = "n1", Type = "File" });
        cache.Set("n2", new Node { Id = "n2", Type = "File" });
        cache.Set("n3", new Node { Id = "n3", Type = "File" });
        cache.Set("n4", new Node { Id = "n4", Type = "File" });

        // n1 should be evicted (LRU)
        Assert.False(cache.TryGet("n1", out _));
        Assert.True(cache.TryGet("n2", out _));
        Assert.True(cache.TryGet("n3", out _));
        Assert.True(cache.TryGet("n4", out _));
    }

    [Fact]
    public void Count_ReflectsCacheSize()
    {
        var cache = new NodeCache(100);

        Assert.Equal(0, cache.Count);

        cache.Set("n1", new Node { Id = "n1", Type = "File" });
        Assert.Equal(1, cache.Count);

        cache.Set("n2", new Node { Id = "n2", Type = "File" });
        Assert.Equal(2, cache.Count);

        cache.Invalidate("n1");
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void Clear_RemovesAll()
    {
        var cache = new NodeCache(100);
        cache.Set("n1", new Node { Id = "n1", Type = "File" });
        cache.Set("n2", new Node { Id = "n2", Type = "File" });

        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.False(cache.TryGet("n1", out _));
        Assert.False(cache.TryGet("n2", out _));
    }

    [Fact]
    public void Access_MovesToFront()
    {
        var cache = new NodeCache(3);

        cache.Set("n1", new Node { Id = "n1", Type = "File" });
        cache.Set("n2", new Node { Id = "n2", Type = "File" });
        cache.Set("n3", new Node { Id = "n3", Type = "File" });

        // Access n1 - moves to front
        cache.TryGet("n1", out _);

        // n1 survived, n2 should be evicted
        cache.Set("n4", new Node { Id = "n4", Type = "File" });

        Assert.True(cache.TryGet("n1", out _));
        Assert.False(cache.TryGet("n2", out _));
        Assert.True(cache.TryGet("n3", out _));
        Assert.True(cache.TryGet("n4", out _));
    }
}
