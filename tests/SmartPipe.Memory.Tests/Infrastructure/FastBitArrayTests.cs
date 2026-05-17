using SmartPipe.Memory.Infrastructure;

namespace SmartPipe.Memory.Tests.Infrastructure;

public sealed class FastBitArrayTests
{
    [Fact]
    public void SetAndCheck_WorksCorrectly()
    {
        var array = new FastBitArray(100);
        Assert.False(array.IsSet(50));
        array.Set(50);
        Assert.True(array.IsSet(50));
        Assert.False(array.IsSet(49));
    }

    [Fact]
    public void Clear_ResetsAllBits()
    {
        var array = new FastBitArray(64);
        array.Set(0);
        array.Set(63);
        array.Clear();
        Assert.False(array.IsSet(0));
        Assert.False(array.IsSet(63));
    }
}
