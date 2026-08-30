using Xunit;

namespace ExGrid.Tests;

public class DateOperatorTests
{
    private static bool TradedOnMatches(object? tradedOn, FilterOperator op, object? value = null)
        => TradeColumns.Matches(new Trade(TradedOn: tradedOn), TradeColumns.FilterOn("TradedOn", new FilterClause(op, value)));

    [Fact] // ADR-0023
    public void Ordering_operators_compare_chronologically()
    {
        Assert.True(TradedOnMatches(new DateTime(2026, 8, 30), FilterOperator.GreaterThan, new DateTime(2026, 8, 29)));
        Assert.True(TradedOnMatches(new DateOnly(2026, 8, 1), FilterOperator.LessThanOrEqual, new DateOnly(2026, 8, 1)));
        Assert.False(TradedOnMatches(new DateTime(2026, 8, 30), FilterOperator.LessThan, new DateTime(2026, 8, 30)));
    }

    [Fact] // ADR-0023: exact value equality — no implicit truncation to day granularity
    public void Equality_is_exact_and_never_truncates_to_the_day()
    {
        Assert.True(TradedOnMatches(new DateTime(2026, 8, 30), FilterOperator.Equals, new DateTime(2026, 8, 30)));
        Assert.False(TradedOnMatches(new DateTime(2026, 8, 30, 9, 15, 0), FilterOperator.Equals, new DateTime(2026, 8, 30)));
    }

    [Fact] // ADR-0023: one date type per column — mixing is refused, not coerced
    public void Mixing_date_runtime_types_throws_naming_the_column()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => TradedOnMatches(new DateTime(2026, 8, 30), FilterOperator.Equals, new DateOnly(2026, 8, 30)));
        Assert.Contains("TradedOn", ex.Message);
    }
}
