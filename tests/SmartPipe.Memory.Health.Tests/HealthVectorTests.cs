using SmartPipe.Memory.Health;

namespace SmartPipe.Memory.Health.Tests;

public sealed class HealthVectorTests
{
    [Fact]
    public void Create_DefaultWeights_ComputesHealthScore()
    {
        var health = HealthVector.Create(
            predictedLatencyMs: 500,
            smoothThroughput: 100,
            failureProbability: 0.2,
            resourceStrain: 0.3,
            p50LatencyMs: 10,
            p95LatencyMs: 50,
            p99LatencyMs: 200);

        Assert.Equal(500, health.PredictedLatencyMs);
        Assert.Equal(0.2, health.FailureProbability);
        Assert.Equal(0.3, health.ResourceStrain);
        Assert.Equal(10, health.P50LatencyMs);
        Assert.Equal(50, health.P95LatencyMs);
        Assert.Equal(200, health.P99LatencyMs);
        Assert.Equal(100, health.SmoothThroughput);
        Assert.Equal(3, health.RetryBudget);
        Assert.True(health.HealthScore is >= 0.0 and <= 1.0);
    }

    [Fact]
    public void Create_PerfectHealth_HealthScoreIsOne()
    {
        var health = HealthVector.Create(0, 0, 0, 0, 0, 0, 0);

        Assert.Equal(1.0, health.HealthScore);
    }

    [Fact]
    public void Create_WorstCase_HealthScoreIsLow()
    {
        var health = HealthVector.Create(
            predictedLatencyMs: 2000,
            smoothThroughput: 0,
            failureProbability: 1.0,
            resourceStrain: 1.0,
            p50LatencyMs: 2000,
            p95LatencyMs: 2000,
            p99LatencyMs: 2000);

        Assert.True(health.HealthScore < 0.3);
    }

    [Fact]
    public void CreateWithWeights_CustomWeights_ComputesCorrectly()
    {
        var health = HealthVector.CreateWithWeights(
            predictedLatencyMs: 500,
            smoothThroughput: 100,
            failureProbability: 0.2,
            resourceStrain: 0.3,
            p50LatencyMs: 10,
            p95LatencyMs: 50,
            p99LatencyMs: 200,
            retryBudget: 5,
            failureWeight: 0.5,
            latencyWeight: 0.3,
            strainWeight: 0.2);

        Assert.Equal(5, health.RetryBudget);
        Assert.True(health.HealthScore is >= 0.0 and <= 1.0);
    }

    [Fact]
    public void CreateWithWeights_WeightsDoNotSumToOne_Throws()
    {
        Assert.Throws<ArgumentException>(() => HealthVector.CreateWithWeights(
            0, 0, 0, 0, 0, 0, 0, 3,
            failureWeight: 0.5, latencyWeight: 0.3, strainWeight: 0.1));
    }
}