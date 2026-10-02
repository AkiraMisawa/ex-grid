using Xunit;

namespace ExGrid.Tests;

public class DateOperatorTests
{
    private static bool TradedOnMatches(object? tradedOn, FilterOperator op, object? value = null)
        => TradeColumns.Matches(new Trade(TradedOn: tradedOn), TradeColumns.FilterOn("TradedOn", new FilterClause(op, value)));

    [Fact] // ADR-0023: DateTime compares by wall-clock ticks — Kind is not part of the value
    public void DateTime_kind_does_not_affect_comparison()
    {
        Assert.True(TradedOnMatches(
            new DateTime(2026, 8, 30, 9, 0, 0, DateTimeKind.Utc),
            FilterOperator.Equals,
            new DateTime(2026, 8, 30, 9, 0, 0, DateTimeKind.Local)));
    }

    [Fact] // ADR-0023: an operand of another date type in a LATER clause is refused up front too
    public void An_undeclared_date_type_in_a_later_clause_is_refused_up_front()
    {
        var filter = TradeColumns.FilterOnAny("TradedOn",
            new FilterClause(FilterOperator.Equals, new DateTime(2026, 8, 30)),
            new FilterClause(FilterOperator.Equals, new DateOnly(2026, 1, 1)));

        var ex = Assert.Throws<InvalidOperationException>(
            () => GridQueryEngine.Apply(Array.Empty<Trade>(), TradeColumns.All, filter, null));
        Assert.Contains("TradedOn", ex.Message);
        Assert.Contains("declared DateTime", ex.Message);
    }

    [Fact] // ADR-0023: an In list holding an undeclared date type is refused up front, even over an empty row set
    public void An_in_list_holding_an_undeclared_date_type_is_refused_up_front()
    {
        var filter = TradeColumns.FilterOn("TradedOn",
            new FilterClause(FilterOperator.In,
                Values: [new DateTime(2026, 8, 30), new DateOnly(2026, 1, 1)]));

        var ex = Assert.Throws<InvalidOperationException>(
            () => GridQueryEngine.Apply(Array.Empty<Trade>(), TradeColumns.All, filter, null));
        Assert.Contains("TradedOn", ex.Message);
    }

    [Fact] // ADR-0023
    public void Ordering_operators_compare_chronologically()
    {
        Assert.True(TradedOnMatches(new DateTime(2026, 8, 30), FilterOperator.GreaterThan, new DateTime(2026, 8, 29)));
        Assert.True(TradeColumns.Matches(new Trade(TradedOn: new DateOnly(2026, 8, 1)),
            TradeColumns.FilterOn("TradedOnDay", new FilterClause(FilterOperator.LessThanOrEqual, new DateOnly(2026, 8, 1)))));
        Assert.False(TradedOnMatches(new DateTime(2026, 8, 30), FilterOperator.LessThan, new DateTime(2026, 8, 30)));
    }

    [Fact] // ADR-0023: exact value equality — no implicit truncation to day granularity
    public void Equality_is_exact_and_never_truncates_to_the_day()
    {
        Assert.True(TradedOnMatches(new DateTime(2026, 8, 30), FilterOperator.Equals, new DateTime(2026, 8, 30)));
        Assert.False(TradedOnMatches(new DateTime(2026, 8, 30, 9, 15, 0), FilterOperator.Equals, new DateTime(2026, 8, 30)));
    }

    [Fact] // ADR-0023: the declared date type is the law — another is refused, not coerced
    public void An_operand_of_an_undeclared_date_type_throws_naming_the_column()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => TradedOnMatches(new DateTime(2026, 8, 30), FilterOperator.Equals, new DateOnly(2026, 8, 30)));
        Assert.Contains("TradedOn", ex.Message);
    }

    [Fact] // ADR-0023: a cell of an undeclared date type is refused regardless of which operator would compare
    public void A_mismatched_cell_type_is_refused_even_when_no_comparing_operator_reaches_it()
    {
        // Or(IsNotBlank, Equals): IsNotBlank alone would match without ever comparing,
        // which used to let the mismatched cell slip through — acceptance must not
        // depend on the operator.
        var filter = TradeColumns.FilterOnAny("TradedOn",
            new FilterClause(FilterOperator.IsNotBlank),
            new FilterClause(FilterOperator.Equals, new DateTime(2026, 8, 30)));

        var ex = Assert.Throws<InvalidOperationException>(
            () => TradeColumns.Matches(new Trade(TradedOn: new DateOnly(2026, 8, 30)), filter));
        Assert.Contains("declared DateTime", ex.Message);
    }

    [Fact] // ADR-0023: date In is set membership, agreeing with Equals
    public void In_matches_dates_by_set_membership()
    {
        var filter = TradeColumns.FilterOn("TradedOn",
            new FilterClause(FilterOperator.In,
                Values: [new DateTime(2026, 8, 29), new DateTime(2026, 8, 30)]));

        Assert.True(TradeColumns.Matches(new Trade(TradedOn: new DateTime(2026, 8, 30)), filter));
        Assert.False(TradeColumns.Matches(new Trade(TradedOn: new DateTime(2026, 8, 31)), filter));
    }
}
