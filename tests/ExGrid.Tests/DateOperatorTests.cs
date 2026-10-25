using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// What C# alone can say about a Date column. Its semantics are shared cases
/// (<c>tests/cases/query/dates.json</c> and <c>operators.json</c>), which the Script Core runs too;
/// a DateTime's Kind has no counterpart there.
/// </summary>
public class DateOperatorTests
{
    [Fact] // ADR-0023: DateTime compares by wall-clock ticks — Kind is not part of the value
    public void DateTime_kind_does_not_affect_comparison()
    {
        Assert.True(TradeColumns.Matches(
            new Trade(TradedOn: new DateTime(2026, 8, 30, 9, 0, 0, DateTimeKind.Utc)),
            TradeColumns.FilterOn("TradedOn", new FilterClause(FilterOperator.Equals, new DateTime(2026, 8, 30, 9, 0, 0, DateTimeKind.Local)))));
    }
}
