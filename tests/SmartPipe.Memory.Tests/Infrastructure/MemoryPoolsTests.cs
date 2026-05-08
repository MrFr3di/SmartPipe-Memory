using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Infrastructure;

namespace SmartPipe.Memory.Tests.Infrastructure;

public sealed class MemoryPoolsTests
{
    [Fact]
    public void NodePool_Rent_ReturnsNewNode()
    {
        var node = MemoryPools.NodePool.Rent();

        Assert.NotNull(node);
        Assert.Equal("", node.Id);
        Assert.Equal(1.0, node.HealthScore);
    }

    [Fact]
    public void NodePool_Return_RentsAgain()
    {
        var node = MemoryPools.NodePool.Rent();
        node.HealthScore = 0.5;

        MemoryPools.NodePool.Return(node);

        var rented = MemoryPools.NodePool.Rent();
        Assert.NotNull(rented);
    }

    [Fact]
    public void EdgePool_Rent_ReturnsNewEdge()
    {
        var edge = MemoryPools.EdgePool.Rent();

        Assert.NotNull(edge);
        Assert.Equal(0, edge.Id);
        Assert.Equal(1.0, edge.Weight);
    }

    [Fact]
    public void EdgePool_Return_RentsAgain()
    {
        var edge = MemoryPools.EdgePool.Rent();
        edge.Weight = 0.5;

        MemoryPools.EdgePool.Return(edge);

        var rented = MemoryPools.EdgePool.Rent();
        Assert.NotNull(rented);
    }
}