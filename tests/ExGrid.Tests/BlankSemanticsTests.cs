using Xunit;

namespace ExGrid.Tests;

public class BlankSemanticsTests
{
    private static readonly Trade BlankBook = new(Book: null);

    private static bool BlankMatches(FilterOperator op, object? value = null, IReadOnlyList<object?>? values = null)
        => TradeColumns.Matches(BlankBook, TradeColumns.FilterOn("Book", new FilterClause(op, value, values)));

    [Fact] // ADR-0023: a Blank matches only IsBlank
    public void Is_blank_matches_a_blank_and_nothing_else_does()
    {
        Assert.True(BlankMatches(FilterOperator.IsBlank));
        Assert.False(BlankMatches(FilterOperator.IsNotBlank));
        Assert.False(BlankMatches(FilterOperator.Equals, "Rates"));
        Assert.False(BlankMatches(FilterOperator.Contains, "a"));
        Assert.False(BlankMatches(FilterOperator.StartsWith, "a"));
        Assert.False(BlankMatches(FilterOperator.EndsWith, "a"));
    }

    [Fact] // ADR-0023: the deliberate deviation from Excel's custom filters — negations do not sweep in Blanks
    public void Not_equals_does_not_match_blank()
        => Assert.False(BlankMatches(FilterOperator.NotEquals, "Done"));

    [Fact] // ADR-0023
    public void Does_not_contain_does_not_match_blank()
        => Assert.False(BlankMatches(FilterOperator.DoesNotContain, "a"));

    [Fact] // ADR-0023: the value-list's "(Blanks)" checkbox serialises as an explicit null entry
    public void In_matches_blank_only_when_null_is_listed()
    {
        Assert.True(BlankMatches(FilterOperator.In, values: ["Rates", null]));
        Assert.False(BlankMatches(FilterOperator.In, values: ["Rates", "Credit"]));
    }

    [Fact] // ADR-0023
    public void Is_not_blank_matches_a_value()
        => Assert.True(TradeColumns.Matches(
            new Trade(Book: "Rates"),
            TradeColumns.FilterOn("Book", new FilterClause(FilterOperator.IsNotBlank))));

    [Fact] // ADR-0023 (CONTEXT.md "Blank"): an empty string is a value, not a Blank
    public void An_empty_string_is_a_value_not_a_blank()
    {
        Assert.False(TradeColumns.Matches(
            new Trade(Book: ""),
            TradeColumns.FilterOn("Book", new FilterClause(FilterOperator.IsBlank))));
        Assert.True(TradeColumns.Matches(
            new Trade(Book: ""),
            TradeColumns.FilterOn("Book", new FilterClause(FilterOperator.IsNotBlank))));
    }

    [Fact] // ADR-0023: Blank semantics hold for every column type, not just Text
    public void A_blank_number_matches_only_is_blank()
    {
        var blankAmount = new Trade(Amount: null);
        Assert.True(TradeColumns.Matches(blankAmount, TradeColumns.FilterOn("Amount", new FilterClause(FilterOperator.IsBlank))));
        Assert.False(TradeColumns.Matches(blankAmount, TradeColumns.FilterOn("Amount", new FilterClause(FilterOperator.GreaterThan, 0))));
        Assert.False(TradeColumns.Matches(blankAmount, TradeColumns.FilterOn("Amount", new FilterClause(FilterOperator.NotEquals, 0))));
    }
}
