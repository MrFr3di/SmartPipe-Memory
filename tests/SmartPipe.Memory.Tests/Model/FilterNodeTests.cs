using SmartPipe.Memory.Model;

namespace SmartPipe.Memory.Tests.Model;

public sealed class FilterNodeTests
{
    [Fact]
    public void PropertyFilter_Create_WithAllValues()
    {
        var filter = new FilterNode.PropertyFilter
        {
            Property = "HealthScore",
            Operator = FilterOperator.LessThan,
            Value = 0.5,
        };

        Assert.Equal("HealthScore", filter.Property);
        Assert.Equal(FilterOperator.LessThan, filter.Operator);
        Assert.Equal(0.5, filter.Value);
        Assert.IsType<FilterNode.PropertyFilter>(filter);
    }

    [Fact]
    public void And_Filter_CombinesTwoFilters()
    {
        var left = new FilterNode.PropertyFilter
        {
            Property = "HealthScore",
            Operator = FilterOperator.LessThan,
            Value = 0.5,
        };
        var right = new FilterNode.PropertyFilter
        {
            Property = "FailureProb",
            Operator = FilterOperator.GreaterThan,
            Value = 0.1,
        };

        var combined = new FilterNode.And(left, right);

        Assert.IsType<FilterNode.And>(combined);
        Assert.Same(left, ((FilterNode.And)combined).Left);
        Assert.Same(right, ((FilterNode.And)combined).Right);
    }

    [Fact]
    public void Or_Filter_CombinesTwoFilters()
    {
        var left = new FilterNode.PropertyFilter
        {
            Property = "HealthScore",
            Operator = FilterOperator.LessThan,
            Value = 0.5,
        };
        var right = new FilterNode.PropertyFilter
        {
            Property = "ResourceStrain",
            Operator = FilterOperator.GreaterThan,
            Value = 0.7,
        };

        var combined = new FilterNode.Or(left, right);

        Assert.IsType<FilterNode.Or>(combined);
        Assert.Same(left, ((FilterNode.Or)combined).Left);
        Assert.Same(right, ((FilterNode.Or)combined).Right);
    }

    [Fact]
    public void And_WithNullLeft_ThrowsArgumentNullException()
    {
        var right = new FilterNode.PropertyFilter
        {
            Property = "HealthScore",
            Operator = FilterOperator.LessThan,
            Value = 0.5,
        };

        Assert.Throws<ArgumentNullException>(() => new FilterNode.And(null!, right));
    }

    [Fact]
    public void Or_WithNullRight_ThrowsArgumentNullException()
    {
        var left = new FilterNode.PropertyFilter
        {
            Property = "HealthScore",
            Operator = FilterOperator.LessThan,
            Value = 0.5,
        };

        Assert.Throws<ArgumentNullException>(() => new FilterNode.Or(left, null!));
    }

    [Fact]
    public void Nested_AndOr_WorksCorrectly()
    {
        var health = new FilterNode.PropertyFilter
        {
            Property = "HealthScore",
            Operator = FilterOperator.LessThan,
            Value = 0.5,
        };
        var failure = new FilterNode.PropertyFilter
        {
            Property = "FailureProb",
            Operator = FilterOperator.GreaterThan,
            Value = 0.1,
        };
        var latency = new FilterNode.PropertyFilter
        {
            Property = "PredictedLatencyMs",
            Operator = FilterOperator.GreaterThan,
            Value = 200,
        };

        var nested = new FilterNode.And(health, new FilterNode.Or(failure, latency));

        Assert.IsType<FilterNode.And>(nested);
        var inner = ((FilterNode.And)nested).Right;
        Assert.IsType<FilterNode.Or>(inner);
    }
}
