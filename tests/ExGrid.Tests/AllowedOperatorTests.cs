using Xunit;

namespace ExGrid.Tests;

public class AllowedOperatorTests
{
    [Fact] // ADR-0009 / ADR-0023: the core decides which operators a column offers
    public void Text_columns_offer_the_text_operator_set()
        => Assert.Equal(
            [
                FilterOperator.Equals, FilterOperator.NotEquals,
                FilterOperator.Contains, FilterOperator.DoesNotContain,
                FilterOperator.StartsWith, FilterOperator.EndsWith,
                FilterOperator.In, FilterOperator.IsBlank, FilterOperator.IsNotBlank,
            ],
            FilterOperators.AllowedFor(ColumnType.Text));

    [Fact] // ADR-0009 / ADR-0023
    public void Number_and_date_columns_offer_the_ordering_operator_set()
    {
        FilterOperator[] expected =
        [
            FilterOperator.Equals, FilterOperator.NotEquals,
            FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual,
            FilterOperator.LessThan, FilterOperator.LessThanOrEqual,
            FilterOperator.In, FilterOperator.IsBlank, FilterOperator.IsNotBlank,
        ];
        Assert.Equal(expected, FilterOperators.AllowedFor(ColumnType.Number));
        Assert.Equal(expected, FilterOperators.AllowedFor(ColumnType.Date));
    }

    [Fact] // ADR-0009 / ADR-0023: no Contains on Boolean, no ordering — but In stays (value-list mode)
    public void Boolean_columns_offer_equals_the_value_list_and_blankness()
        => Assert.Equal(
            [FilterOperator.Equals, FilterOperator.In, FilterOperator.IsBlank, FilterOperator.IsNotBlank],
            FilterOperators.AllowedFor(ColumnType.Boolean));

    [Fact] // ADR-0023: a disallowed operator is refused, not silently false
    public void A_disallowed_operator_throws_naming_the_column()
    {
        var filter = TradeColumns.FilterOn("Cleared", new FilterClause(FilterOperator.Contains, "tru"));
        var ex = Assert.Throws<InvalidOperationException>(
            () => TradeColumns.Matches(new Trade(Cleared: true), filter));
        Assert.Contains("Cleared", ex.Message);
    }
}
