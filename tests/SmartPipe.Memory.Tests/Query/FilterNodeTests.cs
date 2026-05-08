using SmartPipe.Memory.Model;

namespace SmartPipe.Memory.Tests.Query;

public sealed class FilterNodeTests
{
    [Fact]
    public void PropertyFilter_Create_WithAllValues()
    {
        var filter = new FilterNode.PropertyFilter
        {
            Property = "HealthScore",
            Operator = FilterOperator.LessThan,
            Value = 0.5
        };

        Assert.Equal("HealthScore", filter.Property);
        Assert.Equal(FilterOperator.LessThan, filter.Operator);
        Assert.Equal(0.5, filter.Value);
    }

    [Fact]
    public void And_Filter_CombinesTwoFilters()
    {
        var left = new FilterNode.PropertyFilter
        {
            Property = "HealthScore",
            Operator = FilterOperator.LessThan,
            Value = 0.5
        };

        var right = new FilterNode.PropertyFilter
        {
            Property = "FailureProb",
            Operator = FilterOperator.GreaterThan,
            Value = 0.1
        };

        var combined = new FilterNode.And(left, right);

        Assert.IsType<FilterNode.And>(combined);
        Assert.Equal(left, ((FilterNode.And)combined).Left);
        Assert.Equal(right, ((FilterNode.And)combined).Right);
    }

    [Fact]
    public void Or_Filter_CombinesTwoFilters()
    {
        var left = new FilterNode.PropertyFilter
        {
            Property = "HealthScore",
            Operator = FilterOperator.LessThan,
            Value = 0.5
        };

        var right = new FilterNode.PropertyFilter
        {
            Property = "ResourceStrain",
            Operator = FilterOperator.GreaterThan,
            Value = 0.7
        };

        var combined = new FilterNode.Or(left, right);

        Assert.IsType<FilterNode.Or>(combined);
        Assert.Equal(left, ((FilterNode.Or)combined).Left);
        Assert.Equal(right, ((FilterNode.Or)combined).Right);
    }

    [Theory]
    [InlineData(FilterOperator.LessThan)]
    [InlineData(FilterOperator.GreaterThan)]
    [InlineData(FilterOperator.Equals)]
    public void PropertyFilter_AllOperators_Supported(FilterOperator op)
    {
        var filter = new FilterNode.PropertyFilter
        {
            Property = "PredictedLatencyMs",
            Operator = op,
            Value = 100.0
        };

        Assert.Equal(op, filter.Operator);
    }
}