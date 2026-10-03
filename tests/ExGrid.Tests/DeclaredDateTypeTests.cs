using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The declared date type is the law for cells and operands alike (ADR-0023, section of
/// 2026-10-02; ticket 98): a value of another date type is refused naming the column, eagerly,
/// whichever operator would compare.
/// </summary>
public class DeclaredDateTypeTests
{
    private static GridFilter On(string column, FilterOperator op, object? value = null)
        => TradeColumns.FilterOn(column, new FilterClause(op, value));

    [Fact] // ADR-0023: a DateOnly column filters by its day
    public void A_date_only_column_filters_by_its_day()
    {
        Assert.True(TradeColumns.Matches(new Trade(TradedOn: new DateOnly(2026, 8, 30)),
            On("TradedOnDay", FilterOperator.Equals, new DateOnly(2026, 8, 30))));
        Assert.False(TradeColumns.Matches(new Trade(TradedOn: new DateOnly(2026, 8, 31)),
            On("TradedOnDay", FilterOperator.LessThan, new DateOnly(2026, 8, 30))));
    }

    [Fact] // ADR-0023: a DateTimeOffset column sorts by the instant
    public void A_date_time_offset_column_sorts_by_the_instant()
    {
        var sorted = GridQueryEngine.Apply(
            [
                new Trade(Book: "later", TradedOn: new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero)),
                new Trade(Book: "earlier", TradedOn: new DateTimeOffset(2026, 8, 30, 18, 0, 0, TimeSpan.FromHours(9))),
            ],
            TradeColumns.All, null, [new SortSpec("TradedAt", SortDirection.Ascending)]);

        Assert.Equal(["earlier", "later"], sorted.Select(t => t.Book));
    }

    [Theory] // ADR-0023: a cell of another date type than the declared one is refused, naming the column — no comparison needed
    [InlineData("TradedOn", "DateOnly")]
    [InlineData("TradedOnDay", "DateTime")]
    [InlineData("TradedAt", "DateTime")]
    public void A_cell_of_another_date_type_is_refused_naming_the_column(string column, string cellType)
    {
        object cell = cellType == "DateOnly" ? new DateOnly(2026, 8, 30) : new DateTime(2026, 8, 30);

        var refused = Assert.Throws<InvalidOperationException>(
            () => TradeColumns.Matches(new Trade(TradedOn: cell), On(column, FilterOperator.IsNotBlank)));

        Assert.Contains($"'{column}'", refused.Message);
        Assert.Contains(cellType, refused.Message);
    }

    [Fact] // ADR-0023: a sort reads every cell against the declaration, even one no tie reaches
    public void A_sort_refuses_a_cell_of_another_date_type()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => GridQueryEngine.Apply(
            [new Trade(Book: "a", TradedOn: new DateOnly(2026, 8, 30))],
            TradeColumns.All, null, [new SortSpec("TradedOn", SortDirection.Ascending)]));

        Assert.Contains("'TradedOn'", refused.Message);
    }

    [Fact] // ADR-0023, ADR-0002: an operand of another date type is refused up front, even over no rows — never converted
    public void An_operand_of_another_date_type_is_refused_up_front()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => GridQueryEngine.Apply(
            Array.Empty<Trade>(), TradeColumns.All, On("TradedOnDay", FilterOperator.Equals, new DateTime(2026, 8, 30)), null));

        Assert.Contains("'TradedOnDay'", refused.Message);
        Assert.Contains("DateTime", refused.Message);
    }

    [Fact] // ADR-0023: an In candidate of another date type is refused up front too
    public void An_in_candidate_of_another_date_type_is_refused_up_front()
    {
        var filter = TradeColumns.FilterOn("TradedAt",
            new FilterClause(FilterOperator.In, Values: [new DateTimeOffset(2026, 8, 30, 0, 0, 0, TimeSpan.Zero), new DateTime(2026, 8, 30)]));

        var refused = Assert.Throws<InvalidOperationException>(
            () => GridQueryEngine.Apply(Array.Empty<Trade>(), TradeColumns.All, filter, null));

        Assert.Contains("'TradedAt'", refused.Message);
    }
}
