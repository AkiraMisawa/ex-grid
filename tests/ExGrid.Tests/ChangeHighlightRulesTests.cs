using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The Change Highlight's rule and its class (ADR-0067): a cell is marked while the current
/// time is before its change time plus the duration, and a marked cell wears one more interned
/// class, <c>ex-changed</c>.
/// </summary>
public class ChangeHighlightRulesTests
{
    private static readonly DateTimeOffset ChangedAt = new(2026, 9, 30, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    [Fact] // ADR-0067: a mark lasts one second unless the Consumer says otherwise
    public void The_default_duration_is_one_second()
        => Assert.Equal(TimeSpan.FromSeconds(1), ChangeHighlightRules.DefaultDuration);

    [Fact] // ADR-0067: a cell is marked while the current time is before its change time plus the duration
    public void A_cell_is_marked_until_its_change_time_plus_the_duration()
    {
        Assert.Equal(ChangedAt + Second, ChangeHighlightRules.ShowingUntil(ChangedAt, Second, ChangedAt));
        Assert.Equal(ChangedAt + Second, ChangeHighlightRules.ShowingUntil(ChangedAt, Second, ChangedAt + Second - TimeSpan.FromTicks(1)));
        // The end itself is already unmarked, and so is everything after it.
        Assert.Null(ChangeHighlightRules.ShowingUntil(ChangedAt, Second, ChangedAt + Second));
        Assert.Null(ChangeHighlightRules.ShowingUntil(ChangedAt, Second, ChangedAt + TimeSpan.FromDays(1)));
    }

    [Fact] // ADR-0067: a cell nobody said changed is not marked
    public void No_answer_is_no_mark()
        => Assert.Null(ChangeHighlightRules.ShowingUntil(null, Second, ChangedAt));

    [Fact] // ADR-0067: a change time still to come is taken as given, and marked until it plus the duration
    public void A_change_time_in_the_future_is_marked_until_it_plus_the_duration()
    {
        var ahead = ChangedAt + TimeSpan.FromSeconds(5);

        Assert.Equal(ahead + Second, ChangeHighlightRules.ShowingUntil(ahead, Second, ChangedAt));
    }

    [Fact] // ADR-0067: a zero duration marks only a change time still to come
    public void A_zero_duration_marks_only_a_change_still_to_come()
    {
        Assert.Null(ChangeHighlightRules.ShowingUntil(ChangedAt, TimeSpan.Zero, ChangedAt));
        Assert.Equal(ChangedAt, ChangeHighlightRules.ShowingUntil(ChangedAt, TimeSpan.Zero, ChangedAt - Second));
    }

    [Fact] // ADR-0067: a mark cannot end before the change it marks — refused by name
    public void A_negative_duration_is_refused()
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(() => ChangeHighlightRules.EndOf(ChangedAt, -Second));

        Assert.Equal("duration", refusal.ParamName);
    }

    [Fact] // ADR-0067: the end is an instant, the same whatever offset the Consumer wrote its time in
    public void The_end_is_the_same_instant_whatever_the_offset()
    {
        var tokyo = ChangedAt.ToOffset(TimeSpan.FromHours(9));

        Assert.Equal(ChangedAt + Second, ChangeHighlightRules.EndOf(tokyo, Second));
        Assert.Equal(TimeSpan.Zero, ChangeHighlightRules.EndOf(tokyo, Second).Offset);
    }

    [Fact] // ADR-0067: an end past the calendar's last instant is held there — never an overflow thrown out of a render
    public void An_end_past_the_calendar_is_held_at_its_last_instant()
    {
        Assert.Equal(DateTimeOffset.MaxValue, ChangeHighlightRules.EndOf(DateTimeOffset.MaxValue, Second));
        Assert.Equal(DateTimeOffset.MaxValue, ChangeHighlightRules.EndOf(ChangedAt, TimeSpan.MaxValue));
        // A local time at the calendar's end, west of UTC, is no overflow either.
        Assert.Equal(DateTimeOffset.MaxValue,
            ChangeHighlightRules.EndOf(DateTimeOffset.MaxValue.ToOffset(TimeSpan.FromHours(-14)), Second));
        Assert.Equal(DateTimeOffset.MaxValue, ChangeHighlightRules.ShowingUntil(DateTimeOffset.MaxValue, Second, ChangedAt));
    }

    [Fact] // ADR-0067 / ADR-0029: the mark is one more class on the cell, joining the others and replacing none
    public void The_mark_joins_the_cell_classes_as_ex_changed()
    {
        Assert.Equal("ex-cell ex-changed", CellClasses.For(numeric: false, pinned: false, CellState.Normal, changed: true));
        Assert.Equal("ex-cell ex-cell-numeric ex-pinned ex-align-right ex-tone-negative ex-state-stale ex-changed",
            CellClasses.For(numeric: true, pinned: true, CellState.Stale, CellAlign.Right, CellTone.Negative, changed: true));
    }

    [Fact] // ADR-0067 / DC-1: a cell that is not marked is painted exactly as it was before the mark existed
    public void An_unmarked_cell_is_painted_as_before()
    {
        Assert.Same(
            CellClasses.For(numeric: true, pinned: true, CellState.Error, CellAlign.Left, CellTone.Positive),
            CellClasses.For(numeric: true, pinned: true, CellState.Error, CellAlign.Left, CellTone.Positive, changed: false));
        Assert.DoesNotContain("ex-changed", CellClasses.For(numeric: false, pinned: false, CellState.Normal), StringComparison.Ordinal);
    }

    [Fact] // ADR-0067 / ADR-0027 P5: the marked class is interned — the same instance on every call, never composed on the render path
    public void The_marked_class_is_interned()
        => Assert.Same(
            CellClasses.For(numeric: true, pinned: false, CellState.Missing, changed: true),
            CellClasses.For(numeric: true, pinned: false, CellState.Missing, changed: true));

    [Fact] // ADR-0067: every combination with and without the mark is a string of its own, so no cell is painted as another
    public void Every_combination_with_and_without_the_mark_is_distinct()
    {
        var all =
            (from changed in new[] { false, true }
             from tone in new[] { CellTone.None, CellTone.Positive, CellTone.Negative }
             from state in Enum.GetValues<CellState>()
             from align in Enum.GetValues<CellAlign>()
             from numeric in new[] { false, true }
             from pinned in new[] { false, true }
             select (changed, CellClasses.For(numeric, pinned, state, align, tone, changed))).ToList();

        Assert.Equal(480, all.Count);
        Assert.Equal(480, all.Select(c => c.Item2).Distinct(StringComparer.Ordinal).Count());
        Assert.All(all, c => Assert.Equal(c.changed, c.Item2.EndsWith(" ex-changed", StringComparison.Ordinal)));
    }
}
