using System.Globalization;
using Xunit;

namespace ExGrid.Tests;

public class TextOperatorTests
{
    private static bool BookMatches(string? book, FilterOperator op, object? value = null, IReadOnlyList<object?>? values = null)
        => TradeColumns.Matches(new Trade(Book: book), TradeColumns.FilterOn("Book", new FilterClause(op, value, values)));

    [Fact] // ADR-0023: string comparison is case-insensitive
    public void Equals_is_case_insensitive()
    {
        Assert.True(BookMatches("Rates", FilterOperator.Equals, "rates"));
        Assert.False(BookMatches("Rates", FilterOperator.Equals, "ratesx"));
    }

    [Fact] // ADR-0023
    public void Not_equals_is_case_insensitive()
    {
        Assert.False(BookMatches("Rates", FilterOperator.NotEquals, "RATES"));
        Assert.True(BookMatches("Rates", FilterOperator.NotEquals, "Credit"));
    }

    [Fact] // ADR-0023
    public void Contains_matches_regardless_of_case()
    {
        Assert.True(BookMatches("Rates Desk", FilterOperator.Contains, "DESK"));
        Assert.False(BookMatches("Rates Desk", FilterOperator.Contains, "credit"));
    }

    [Fact] // ADR-0023
    public void Does_not_contain_is_the_negation_of_contains()
    {
        Assert.False(BookMatches("Rates Desk", FilterOperator.DoesNotContain, "desk"));
        Assert.True(BookMatches("Rates Desk", FilterOperator.DoesNotContain, "credit"));
    }

    [Fact] // ADR-0023
    public void Starts_with_and_ends_with_ignore_case()
    {
        Assert.True(BookMatches("Rates Desk", FilterOperator.StartsWith, "rates"));
        Assert.False(BookMatches("Rates Desk", FilterOperator.StartsWith, "desk"));
        Assert.True(BookMatches("Rates Desk", FilterOperator.EndsWith, "DESK"));
        Assert.False(BookMatches("Rates Desk", FilterOperator.EndsWith, "rates"));
    }

    [Fact] // ADR-0023: In is how the value-list mode serialises (ADR-0009)
    public void In_matches_any_listed_value_ignoring_case()
    {
        Assert.True(BookMatches("Rates", FilterOperator.In, values: ["RATES", "Credit"]));
        Assert.False(BookMatches("Equity", FilterOperator.In, values: ["Rates", "Credit"]));
    }

    [Fact] // ADR-0023: comparison is ordinal — the machine's culture must not change a Query's meaning
    public void Comparison_is_ordinal_and_ignores_current_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
        try
        {
            // Under a Turkish culture-aware comparison 'I' would pair with 'ı', not 'i'.
            Assert.True(BookMatches("index", FilterOperator.Equals, "INDEX"));
            // And ordinal never pairs the dotted capital İ (U+0130) with 'i'.
            Assert.False(BookMatches("İSTANBUL", FilterOperator.Equals, "istanbul"));
            // The substring operators are pinned to the same collation.
            Assert.True(BookMatches("Index", FilterOperator.Contains, "IND"));
            Assert.False(BookMatches("İSTANBUL", FilterOperator.Contains, "ist"));
            Assert.True(BookMatches("Index", FilterOperator.StartsWith, "in"));
            Assert.True(BookMatches("index", FilterOperator.In, values: ["INDEX"]));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
