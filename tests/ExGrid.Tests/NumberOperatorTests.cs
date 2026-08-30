using Xunit;

namespace ExGrid.Tests;

public class NumberOperatorTests
{
    private static bool AmountMatches(object? amount, FilterOperator op, object? value = null, IReadOnlyList<object?>? values = null)
        => TradeColumns.Matches(new Trade(Amount: amount), TradeColumns.FilterOn("Amount", new FilterClause(op, value, values)));

    [Fact] // ADR-0023
    public void Ordering_operators_compare_numerically()
    {
        Assert.True(AmountMatches(10, FilterOperator.GreaterThan, 9));
        Assert.False(AmountMatches(10, FilterOperator.GreaterThan, 10));
        Assert.True(AmountMatches(10, FilterOperator.GreaterThanOrEqual, 10));
        Assert.True(AmountMatches(10, FilterOperator.LessThan, 11));
        Assert.True(AmountMatches(10, FilterOperator.LessThanOrEqual, 10));
        Assert.False(AmountMatches(10, FilterOperator.LessThan, 10));
    }

    [Fact] // ADR-0023: every numeric runtime type is normalised to decimal before comparing
    public void Mixed_numeric_runtime_types_compare_as_one_number_line()
    {
        Assert.True(AmountMatches(10, FilterOperator.Equals, 10.0));
        Assert.True(AmountMatches(10.5, FilterOperator.Equals, 10.5m));
        Assert.True(AmountMatches(2L, FilterOperator.GreaterThan, 1.5));
        Assert.True(AmountMatches(0.1, FilterOperator.Equals, 0.1m));
    }

    [Fact] // ADR-0023: a float keeps its own precision — no phantom digits from a double detour
    public void Float_values_do_not_gain_phantom_precision()
    {
        Assert.True(AmountMatches(0.1f, FilterOperator.Equals, 0.1m));
        Assert.True(AmountMatches(0.1f, FilterOperator.Equals, 0.1));
        Assert.True(AmountMatches(1, FilterOperator.In, values: [1f, 2f]));
    }

    [Fact] // ADR-0023
    public void In_matches_any_listed_number()
    {
        Assert.True(AmountMatches(10, FilterOperator.In, values: [5, 10.0m, 15]));
        Assert.False(AmountMatches(7, FilterOperator.In, values: [5, 10, 15]));
    }

    [Fact] // ADR-0023: NaN and infinity are refused, never quietly ordered somewhere
    public void A_non_finite_number_throws_naming_the_column()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => AmountMatches(double.NaN, FilterOperator.Equals, 1));
        Assert.Contains("Amount", ex.Message);
        Assert.Throws<InvalidOperationException>(
            () => AmountMatches(1, FilterOperator.Equals, double.PositiveInfinity));
    }

    [Fact] // ADR-0023: too small for decimal is refused too — never quietly flattened to zero
    public void A_non_zero_value_that_would_flatten_to_zero_throws_naming_the_column()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => AmountMatches(1e-30, FilterOperator.Equals, 0));
        Assert.Contains("Amount", ex.Message);
        Assert.Throws<InvalidOperationException>(
            () => AmountMatches(-1e-30, FilterOperator.Equals, 0));
        Assert.Throws<InvalidOperationException>(
            () => AmountMatches(1e-40f, FilterOperator.Equals, 0));

        Assert.True(AmountMatches(0.0, FilterOperator.Equals, 0)); // a true zero is a value
    }

    [Fact] // ADR-0023: finite but beyond decimal's range is refused the same way as NaN
    public void A_finite_number_beyond_the_representable_range_throws_naming_the_column()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => AmountMatches(1e300, FilterOperator.Equals, 1));
        Assert.Contains("Amount", ex.Message);
    }

    [Fact] // ADR-0023: the declared type is the law — text in a Number column is refused
    public void A_value_contradicting_the_declared_type_throws_naming_the_column()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => AmountMatches("ten", FilterOperator.Equals, 10));
        Assert.Contains("Amount", ex.Message);
        Assert.Throws<InvalidOperationException>(
            () => AmountMatches(10, FilterOperator.Equals, "ten"));
    }
}
