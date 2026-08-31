using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// Pins the DateTimeOffset semantics ADR-0023 refined: comparison and equality follow
/// the type's own .NET semantics — the instant, with the offset as presentation.
/// </summary>
public class DateTimeOffsetSemanticsTests
{
    private static readonly DateTimeOffset TokyoMorning = new(2026, 8, 30, 18, 0, 0, TimeSpan.FromHours(9));
    private static readonly DateTimeOffset SameInstantUtc = new(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);

    [Fact] // ADR-0023: DateTimeOffset compares by the instant — the offset is presentation
    public void Same_instant_with_different_offsets_is_equal()
    {
        Assert.True(TradeColumns.Matches(
            new Trade(TradedOn: TokyoMorning),
            TradeColumns.FilterOn("TradedOn", new FilterClause(FilterOperator.Equals, SameInstantUtc))));
    }

    [Fact] // ADR-0023: ordering follows the instant, not the wall clock
    public void Ordering_follows_the_instant()
    {
        // 10:00+09:00 is instant 01:00Z — before 09:00Z, despite the later wall clock.
        Assert.True(TradeColumns.Matches(
            new Trade(TradedOn: new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.FromHours(9))),
            TradeColumns.FilterOn("TradedOn", new FilterClause(FilterOperator.LessThan, SameInstantUtc))));
    }

    [Fact] // ADR-0023: same-instant values tie in sort, and the stable sort keeps input order
    public void Same_instant_values_tie_and_keep_input_order()
    {
        var first = new Trade(Book: "first", TradedOn: TokyoMorning);
        var second = new Trade(Book: "second", TradedOn: SameInstantUtc);

        var sorted = GridQueryEngine.Apply(
            new[] { first, second }, TradeColumns.All, null,
            [new SortSpec("TradedOn", SortDirection.Ascending)]);

        Assert.Equal(new[] { first, second }, sorted);
    }

    [Fact] // ADR-0023: In membership is by instant too — the set agrees with Equals
    public void In_matches_by_instant()
    {
        Assert.True(TradeColumns.Matches(
            new Trade(TradedOn: TokyoMorning),
            TradeColumns.FilterOn("TradedOn", new FilterClause(FilterOperator.In, Values: [SameInstantUtc]))));
        Assert.False(TradeColumns.Matches(
            new Trade(TradedOn: TokyoMorning.AddMinutes(1)),
            TradeColumns.FilterOn("TradedOn", new FilterClause(FilterOperator.In, Values: [SameInstantUtc]))));
    }
}
