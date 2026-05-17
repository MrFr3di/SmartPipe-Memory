using System.Collections.Concurrent;

namespace SmartPipe.Memory.Caching;

/// <summary>
/// Least-recently-used cache for graph nodes.
/// Reduces repeated lookups for frequently accessed nodes.
/// Thread-safe via lock. Supports point invalidation.
/// </summary>
public sealed class NodeCache
{
    private readonly int _maxSize;
    private readonly LinkedList<string> _lruList;
    private readonly Dictionary<string, LinkedListNode<string>> _lookup;
    private readonly Dictionary<string, Graph.Node> _cache;
    private readonly object _syncRoot = new();

    /// <summary>
    /// Create a new node cache.
    /// </summary>
    /// <param name="maxSize">Maximum number of nodes to cache. Default 10000.</param>
    public NodeCache(int maxSize = 10000)
    {
        if (maxSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxSize));

        _maxSize = maxSize;
        _lruList = new LinkedList<string>();
        _lookup = new Dictionary<string, LinkedListNode<string>>(maxSize);
        _cache = new Dictionary<string, Graph.Node>(maxSize);
    }

    /// <summary>
    /// Number of nodes currently in the cache.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_syncRoot)
                return _cache.Count;
        }
    }

    /// <summary>
    /// Try to get a node from the cache.
    /// </summary>
    /// <param name="nodeId">Node identifier.</param>
    /// <param name="node">The cached node, or null if not found.</param>
    /// <returns>True if the node was found in cache.</returns>
    public bool TryGet(string nodeId, out Graph.Node? node)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);

        lock (_syncRoot)
        {
            if (_cache.TryGetValue(nodeId, out var cached))
            {
                MoveToFront(nodeId);
                node = cached;
                return true;
            }

            node = null;
            return false;
        }
    }

    /// <summary>
    /// Store a node in the cache.
    /// </summary>
    /// <param name="nodeId">Node identifier.</param>
    /// <param name="node">Node to cache.</param>
    public void Set(string nodeId, Graph.Node node)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);
        ArgumentNullException.ThrowIfNull(node);

        lock (_syncRoot)
        {
            if (_lookup.TryGetValue(nodeId, out var existingNode))
            {
                _lruList.Remove(existingNode);
            }
            else if (_cache.Count >= _maxSize)
            {
                EvictLeastRecentlyUsed();
            }

            var newNode = _lruList.AddFirst(nodeId);
            _lookup[nodeId] = newNode;
            _cache[nodeId] = node;
        }
    }

    /// <summary>
    /// Remove a node from the cache (point invalidation).
    /// </summary>
    /// <param name="nodeId">Node identifier to invalidate.</param>
    public void Invalidate(string nodeId)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);

        lock (_syncRoot)
        {
            if (_lookup.TryGetValue(nodeId, out var node))
            {
                _lruList.Remove(node);
                _lookup.Remove(nodeId);
                _cache.Remove(nodeId);
            }
        }
    }

    /// <summary>
    /// Remove all nodes from the cache.
    /// </summary>
    public void Clear()
    {
        lock (_syncRoot)
        {
            _lruList.Clear();
            _lookup.Clear();
            _cache.Clear();
        }
    }

    private void MoveToFront(string nodeId)
    {
        if (_lookup.TryGetValue(nodeId, out var node))
        {
            _lruList.Remove(node);
            var newNode = _lruList.AddFirst(nodeId);
            _lookup[nodeId] = newNode;
        }
    }

    private void EvictLeastRecentlyUsed()
    {
        var last = _lruList.Last;
        if (last is null)
            return;

        _lruList.RemoveLast();
        _lookup.Remove(last.Value);
        _cache.Remove(last.Value);
    }
}
