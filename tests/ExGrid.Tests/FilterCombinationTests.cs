using Xunit;

namespace ExGrid.Tests;

public class FilterCombinationTests
{
    [Fact] // ADR-0009: columns combine with And
    public void Columns_combine_with_and()
    {
        var filter = new GridFilter(new Dictionary<string, FilterSpec>
        {
            ["Book"] = new([new FilterClause(FilterOperator.Equals, "Rates")]),
            ["Amount"] = new([new FilterClause(FilterOperator.GreaterThan, 100)]),
        });

        Assert.True(TradeColumns.Matches(new Trade(Book: "Rates", Amount: 150), filter));
        Assert.False(TradeColumns.Matches(new Trade(Book: "Rates", Amount: 50), filter));
        Assert.False(TradeColumns.Matches(new Trade(Book: "Credit", Amount: 150), filter));
    }

    [Fact] // ADR-0009: Or exists only within a column
    public void Clauses_within_a_column_can_combine_with_or()
    {
        var filter = TradeColumns.FilterOnAny("Book",
            new FilterClause(FilterOperator.Equals, "Rates"),
            new FilterClause(FilterOperator.Equals, "Credit"));

        Assert.True(TradeColumns.Matches(new Trade(Book: "Credit"), filter));
        Assert.True(TradeColumns.Matches(new Trade(Book: "Rates"), filter));
        Assert.False(TradeColumns.Matches(new Trade(Book: "Equity"), filter));
    }

    [Fact] // ADR-0023: there is no Between operator — it is And of two comparisons
    public void Between_is_expressed_as_and_of_two_comparisons()
    {
        var filter = TradeColumns.FilterOn("Amount",
            new FilterClause(FilterOperator.GreaterThanOrEqual, 100),
            new FilterClause(FilterOperator.LessThanOrEqual, 200));

        Assert.True(TradeColumns.Matches(new Trade(Amount: 150), filter));
        Assert.False(TradeColumns.Matches(new Trade(Amount: 250), filter));
        Assert.False(TradeColumns.Matches(new Trade(Amount: 50), filter));
    }

    [Fact] // ADR-0023: the Opaque Filter's meaning is Consumer-only; the reference engine ignores it
    public void The_opaque_filter_is_ignored()
    {
        var filter = new GridFilter(
            new Dictionary<string, FilterSpec> { ["Book"] = new([new FilterClause(FilterOperator.Equals, "Rates")]) },
            Opaque: "only-my-consumer-understands-this");

        Assert.True(TradeColumns.Matches(new Trade(Book: "Rates"), filter));
    }

    [Fact] // ADR-0023: refuse rather than guess
    public void An_unknown_column_throws_naming_it()
    {
        var filter = TradeColumns.FilterOn("Notional", new FilterClause(FilterOperator.Equals, 1));
        var ex = Assert.Throws<InvalidOperationException>(
            () => TradeColumns.Matches(new Trade(), filter));
        Assert.Contains("Notional", ex.Message);
    }

    [Fact] // ADR-0023: an empty clause list is ambiguous — refuse it
    public void A_filter_spec_without_clauses_throws()
    {
        var filter = new GridFilter(new Dictionary<string, FilterSpec> { ["Book"] = new([]) });
        Assert.Throws<InvalidOperationException>(() => TradeColumns.Matches(new Trade(Book: "Rates"), filter));
    }

    [Fact] // ADR-0023: the whole Query is validated up front — a malformed one is refused even when no row exercises it
    public void A_malformed_query_is_refused_before_any_row_is_looked_at()
    {
        var wrongTypedOperand = TradeColumns.FilterOn("Amount", new FilterClause(FilterOperator.Equals, "ten"));

        // Over an empty row set…
        Assert.Throws<InvalidOperationException>(
            () => GridQueryEngine.Apply([], TradeColumns.All, wrongTypedOperand, null));
        // …and over rows that are Blank in that column.
        Assert.Throws<InvalidOperationException>(
            () => GridQueryEngine.Apply([new Trade(Amount: null)], TradeColumns.All, wrongTypedOperand, null));
    }

    [Fact] // ADR-0023: an operator without its operand is refused, and null is not a stand-in for IsBlank
    public void A_missing_operand_throws()
    {
        Assert.Throws<InvalidOperationException>(() => TradeColumns.Matches(
            new Trade(Book: "Rates"),
            TradeColumns.FilterOn("Book", new FilterClause(FilterOperator.Equals))));
        Assert.Throws<InvalidOperationException>(() => TradeColumns.Matches(
            new Trade(Book: "Rates"),
            TradeColumns.FilterOn("Book", new FilterClause(FilterOperator.In))));
        Assert.Throws<InvalidOperationException>(() => TradeColumns.Matches(
            new Trade(Book: "Rates"),
            TradeColumns.FilterOn("Book", new FilterClause(FilterOperator.IsBlank, "operand"))));
    }
}
