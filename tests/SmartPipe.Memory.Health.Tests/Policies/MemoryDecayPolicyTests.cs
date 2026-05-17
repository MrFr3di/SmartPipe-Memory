using SmartPipe.Core;
using SmartPipe.Memory.Graph;
using SmartPipe.Memory.Health.Policies;

namespace SmartPipe.Memory.Health.Tests.Policies;

public sealed class MemoryDecayPolicyTests
{
    [Fact]
    public void ComputeStrength_FreshEdge_ReturnsInitialWeight()
    {
        var clock = new TimeProviderClock();
        var policy = new MemoryDecayPolicy(clock: clock);
        var edge = new Edge { Weight = 1.0, ValidFrom = clock.UtcNow };

        var strength = policy.ComputeStrength(edge);

        Assert.True(strength > 0.999);
    }

    [Fact]
    public void ComputeStrength_OldEdge_ReturnsDecayedWeight()
    {
        var now = DateTime.UtcNow;
        var clock = new TimeProviderClock();
        var policy = new MemoryDecayPolicy(clock: clock);
        var edge = new Edge { Weight = 1.0, ValidFrom = now.AddDays(-60) }; // older than 30-day half-life

        var strength = policy.ComputeStrength(edge);

        Assert.True(strength < 0.5);
    }

    [Fact]
    public void ComputeStrength_HighAccessCount_SlowsDecay()
    {
        var now = DateTime.UtcNow;
        var clock = new TimeProviderClock();
        var policy = new MemoryDecayPolicy(clock: clock);
        var edge = new Edge { Weight = 1.0, ValidFrom = now.AddDays(-60) };

        var strengthNoAccess = policy.ComputeStrength(edge, accessCount: 0);
        var strengthWithAccess = policy.ComputeStrength(edge, accessCount: 100);

        Assert.True(strengthWithAccess > strengthNoAccess);
    }

    [Fact]
    public void IsWeakened_BelowThreshold_ReturnsTrue()
    {
        var now = DateTime.UtcNow;
        var clock = new TimeProviderClock();
        var policy = new MemoryDecayPolicy(clock: clock);
        var edge = new Edge { Weight = 0.2, ValidFrom = now.AddDays(-60) };

        Assert.True(policy.IsWeakened(edge));
    }

    [Fact]
    public void IsWeakened_AboveThreshold_ReturnsFalse()
    {
        var clock = new TimeProviderClock();
        var policy = new MemoryDecayPolicy(clock: clock);
        var edge = new Edge { Weight = 1.0, ValidFrom = clock.UtcNow };

        Assert.False(policy.IsWeakened(edge));
    }
}
