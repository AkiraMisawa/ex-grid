using ExGrid.Rows;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The Consumer's half of Row Marks, as the bundled in-memory Source provides it
/// (ADR-0043): marks belong to the row's identity, "all" is the result as it stood at the
/// press, and a positional gesture is resolved only under the order it was made in.
/// </summary>
public class RowMarkSourceTests
{
    private sealed record Deal(string Book, decimal Amount);

    private static readonly IReadOnlyList<ColumnInfo<Deal>> Columns =
    [
        new("Book", ColumnType.Text, d => d.Book),
        new("Amount", ColumnType.Number, d => d.Amount),
    ];

    private static InMemoryGridSource<Deal> Source(out Deal[] rows)
    {
        rows = [new("Alpha", 3), new("Beta", 1), new("Gamma", 2), new("Delta", 4)];
        var source = GridSource.From(rows);
        source.OnColumnsChanged(Columns);
        return source;
    }

    private static GridFilter AmountAtLeast(decimal amount)
        => new(new Dictionary<string, FilterSpec>
        {
            ["Amount"] = new([new FilterClause(FilterOperator.GreaterThanOrEqual, amount)]),
        });

    private static RowMarkCounts CountsOf(InMemoryGridSource<Deal> source)
        => source.Marks.Counts ?? throw new InvalidOperationException("in memory, the counts are always known");

    [Fact] // ADR-0043: one checkbox marks one row, and only that row
    public async Task Marking_one_row_marks_only_that_row()
    {
        var source = Source(out var rows);

        await source.Marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.OneRow(rows[1], true));

        Assert.True(source.Marks.IsMarked(rows[1]));
        Assert.False(source.Marks.IsMarked(rows[0]));
        Assert.Equal(new RowMarkCounts(1, 4, 0), CountsOf(source));
    }

    [Fact] // ADR-0043: "all" marks every row of the current result
    public async Task Marking_all_marks_every_row_of_the_result()
    {
        var source = Source(out var rows);

        await source.Marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.AllRows(true, source.RowSequenceVersion));

        Assert.All(rows, r => Assert.True(source.Marks.IsMarked(r)));
        Assert.Equal(RowMarkHeaderState.All, RowMarkRules.HeaderState(CountsOf(source)));
    }

    [Fact] // ADR-0043: "all" never reaches rows the filter has hidden
    public async Task Marking_all_under_a_filter_leaves_the_hidden_rows_unmarked()
    {
        var source = Source(out var rows);
        source.OnFilterChanged(AmountAtLeast(3));

        await source.Marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.AllRows(true, source.RowSequenceVersion));

        Assert.True(source.Marks.IsMarked(rows[0]));   // Alpha 3
        Assert.True(source.Marks.IsMarked(rows[3]));   // Delta 4
        Assert.False(source.Marks.IsMarked(rows[1]));  // Beta 1, hidden
        Assert.False(source.Marks.IsMarked(rows[2]));  // Gamma 2, hidden
        Assert.Equal(new RowMarkCounts(2, 2, 0), CountsOf(source));
    }

    [Fact] // ADR-0043 (MK-5): marks stay on the same rows after a sort
    public async Task Marks_survive_a_sort()
    {
        var source = Source(out var rows);
        await source.Marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.OneRow(rows[2], true));

        source.OnSortChanged([new SortSpec("Amount", SortDirection.Ascending)]);

        Assert.True(source.Marks.IsMarked(rows[2]));
        Assert.Equal(1, Enumerable.Range(0, 4).Count(i => source.Marks.IsMarked(source.Window[i])));
    }

    [Fact] // ADR-0043: a value update replaces the instance, and the mark follows the row
    public async Task A_mark_follows_its_row_through_a_replacement()
    {
        var source = Source(out var rows);
        await source.Marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.OneRow(rows[0], true));
        var replacement = rows[0] with { Amount = 30 };

        source.ReplaceRow(rows[0], replacement);

        Assert.True(source.Marks.IsMarked(replacement));
        Assert.Equal(new RowMarkCounts(1, 4, 0), CountsOf(source));
    }

    [Fact] // ADR-0043 (MK-7): narrowing the filter keeps the marks and counts the ones outside it
    public async Task Marks_outside_the_filter_are_kept_and_counted()
    {
        var source = Source(out var rows);
        await source.Marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.AllRows(true, source.RowSequenceVersion));

        source.OnFilterChanged(AmountAtLeast(3));
        Assert.Equal(new RowMarkCounts(2, 2, 2), CountsOf(source));

        source.OnFilterChanged(null);
        Assert.All(rows, r => Assert.True(source.Marks.IsMarked(r)));
        Assert.Equal(new RowMarkCounts(4, 4, 0), CountsOf(source));
    }

    [Fact] // ADR-0043 (MK-8): a row that joins the result after "all" is not marked, and the header turns to "some"
    public async Task A_row_joining_the_result_after_all_is_not_marked()
    {
        var source = Source(out var rows);
        source.OnFilterChanged(AmountAtLeast(3));
        await source.Marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.AllRows(true, source.RowSequenceVersion));
        var joined = rows[1] with { Amount = 10 };

        source.ReplaceRow(rows[1], joined);

        Assert.Contains(joined, source.Window);
        Assert.False(source.Marks.IsMarked(joined));
        Assert.Equal(RowMarkHeaderState.Some, RowMarkRules.HeaderState(CountsOf(source)));
    }

    [Fact] // ADR-0043: pressing "all" from all unmarks the current result, and only it
    public async Task Unmarking_all_clears_the_current_result_only()
    {
        var source = Source(out var rows);
        await source.Marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.AllRows(true, source.RowSequenceVersion));
        source.OnFilterChanged(AmountAtLeast(3));

        await source.Marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.AllRows(false, source.RowSequenceVersion));

        Assert.False(source.Marks.IsMarked(rows[0]));
        Assert.True(source.Marks.IsMarked(rows[1]));
        Assert.Equal(new RowMarkCounts(0, 2, 2), CountsOf(source));
    }

    [Fact] // ADR-0043 (MK-1): a positional gesture over a mixed block marks all of it
    public async Task Positions_over_a_mixed_block_mark_all_of_it()
    {
        var source = Source(out var rows);
        await source.Marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.OneRow(source.Window[1], true));

        await source.Marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.Positions([new RowRange(0, 3)], source.RowSequenceVersion));

        Assert.Equal([true, true, true, false], source.Window.Select(source.Marks.IsMarked));
    }

    [Fact] // ADR-0043 (MK-1): a positional gesture over an all-marked block unmarks all of it
    public async Task Positions_over_an_all_marked_block_unmark_it()
    {
        var source = Source(out var rows);
        await source.Marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.AllRows(true, source.RowSequenceVersion));

        await source.Marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.Positions([new RowRange(1, 2)], source.RowSequenceVersion));

        Assert.Equal([true, false, false, true], source.Window.Select(source.Marks.IsMarked));
    }

    [Fact] // ADR-0043 (MK-3): positions made under another order are refused — they would name other rows
    public async Task Positions_under_a_stale_sequence_version_mark_nothing()
    {
        var source = Source(out var rows);
        var version = source.RowSequenceVersion;
        source.OnSortChanged([new SortSpec("Amount", SortDirection.Ascending)]);

        await source.Marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.Positions([new RowRange(0, 2)], version));

        Assert.All(rows, r => Assert.False(source.Marks.IsMarked(r)));
    }

    [Fact] // ADR-0043/0024: Group and Total rows are never marked and never counted
    public async Task Only_detail_rows_are_marked_and_counted()
    {
        var source = Source(out var rows);
        source.Marks.OnRowKindChanged(d => d.Book == "Gamma" ? RowKind.Total : RowKind.Detail);

        await source.Marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.AllRows(true, source.RowSequenceVersion));
        await source.Marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.OneRow(rows[2], true));

        Assert.False(source.Marks.IsMarked(rows[2]));
        Assert.Equal(new RowMarkCounts(3, 3, 0), CountsOf(source));
    }

    [Fact] // ADR-0043: each gesture is one change, reported once
    public async Task Each_gesture_raises_one_change()
    {
        var source = Source(out var rows);
        var changes = 0;
        source.Marks.Changed += () => changes++;

        await source.Marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.AllRows(true, source.RowSequenceVersion));
        await source.Marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.OneRow(rows[0], false));

        Assert.Equal(2, changes);
    }

    [Fact] // ADR-0043: the marks the action runs over are rows, never positions
    public async Task The_marked_rows_are_handed_over_as_rows()
    {
        var source = Source(out var rows);
        await source.Marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.OneRow(rows[3], true));
        await source.Marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.OneRow(rows[1], true));

        Assert.Equal([rows[1], rows[3]], source.Marks.MarkedRows);
    }
}
