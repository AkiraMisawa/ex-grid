using Xunit;

namespace ExGrid.Tests;

public class BooleanOperatorTests
{
    private static bool ClearedMatches(object? cleared, FilterOperator op, object? value = null)
        => TradeColumns.Matches(new Trade(Cleared: cleared), TradeColumns.FilterOn("Cleared", new FilterClause(op, value)));

    [Fact] // ADR-0023
    public void Equals_compares_the_flag()
    {
        Assert.True(ClearedMatches(true, FilterOperator.Equals, true));
        Assert.False(ClearedMatches(false, FilterOperator.Equals, true));
    }

    [Fact] // ADR-0009 / ADR-0023: the value-list mode serialises as In on Boolean columns too
    public void In_expresses_the_boolean_value_list()
    {
        var trueAndBlanks = TradeColumns.FilterOn("Cleared",
            new FilterClause(FilterOperator.In, Values: [true, null]));

        Assert.True(TradeColumns.Matches(new Trade(Cleared: true), trueAndBlanks));
        Assert.True(TradeColumns.Matches(new Trade(Cleared: null), trueAndBlanks));
        Assert.False(TradeColumns.Matches(new Trade(Cleared: false), trueAndBlanks));
    }

    [Fact] // ADR-0023: the declared type is the law
    public void A_value_contradicting_the_declared_type_throws_naming_the_column()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ClearedMatches("yes", FilterOperator.Equals, true));
        Assert.Contains("Cleared", ex.Message);
    }
}
