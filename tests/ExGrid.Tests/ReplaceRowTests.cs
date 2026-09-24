using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The Consumer's half of an Edit Intent, provided by the library (ADR-0007):
/// replacing a row by a new instance requeries under the Filter and Sorts in force,
/// and the Row Sequence Version moves only when the visible sequence did.
/// </summary>
public class ReplaceRowTests
{
    private sealed record Trade(string Book, decimal Amount);

    private static InMemoryGridSource<Trade> Source(out Trade[] rows)
    {
        rows = [new("Alpha", 3), new("Beta", 1), new("Gamma", 2)];
        var source = GridSource.From(rows);
        source.OnColumnsChanged(
        [
            new ColumnInfo<Trade>("Book", ColumnType.Text, t => t.Book),
            new ColumnInfo<Trade>("Amount", ColumnType.Number, t => t.Amount),
        ]);
        return source;
    }

    [Fact] // ADR-0007/0011: a value edit that moves nothing keeps the version — and the selection with it
    public void A_value_edit_keeps_the_sequence_version()
    {
        var source = Source(out var rows);
        var version = source.RowSequenceVersion;
        var events = 0;
        source.StateChanged += () => events++;

        source.ReplaceRow(rows[1], rows[1] with { Amount = 99 });

        Assert.Equal(version, source.RowSequenceVersion);
        Assert.Equal(1, events);
        Assert.Equal(99, source.Window[1].Amount);
    }

    [Fact] // ADR-0011: an edit to the sorted column that moves the row is a reorder — the version moves
    public void An_edit_that_moves_the_row_bumps_the_version()
    {
        var source = Source(out var rows);
        source.OnSortChanged([new SortSpec("Amount", SortDirection.Ascending)]);
        var version = source.RowSequenceVersion;
        // Sorted: Beta(1), Gamma(2), Alpha(3). Push Beta past everything.
        var beta = source.Window[0];

        source.ReplaceRow(beta, beta with { Amount = 50 });

        Assert.Equal(version + 1, source.RowSequenceVersion);
        Assert.Equal(["Gamma", "Alpha", "Beta"], source.Window.Select(t => t.Book));
    }

    [Fact] // ADR-0011: an edit the filter no longer matches removes the row — a reorder too
    public void An_edit_the_filter_excludes_bumps_the_version()
    {
        var source = Source(out var rows);
        source.OnFilterChanged(new GridFilter(new Dictionary<string, FilterSpec>
        {
            ["Amount"] = new([new FilterClause(FilterOperator.LessThan, 10m)]),
        }));
        var version = source.RowSequenceVersion;
        var alpha = source.Window[0];

        source.ReplaceRow(alpha, alpha with { Amount = 100 });

        Assert.Equal(version + 1, source.RowSequenceVersion);
        Assert.Equal(2, source.Window.Count);
    }

    [Fact] // ADR-0003/0007: an in-place rewrite is the trap this API exists to close — refused by name
    public void The_same_instance_is_refused()
    {
        var source = Source(out var rows);

        var refusal = Assert.Throws<ArgumentException>(() => source.ReplaceRow(rows[0], rows[0]));

        Assert.Contains("identity", refusal.Message);
    }

    [Fact] // ADR-0007: a stale or foreign instance cannot be resolved onto the base
    public void A_row_not_in_the_source_is_refused()
    {
        var source = Source(out _);

        Assert.Throws<ArgumentException>(
            () => source.ReplaceRow(new Trade("Nowhere", 0), new Trade("Nowhere", 1)));
    }


    [Fact] // ADR-0003/0007: located by identity — a value-equal twin is not "the same row"
    public void The_edit_lands_on_the_edited_instance_not_a_value_equal_twin()
    {
        var source = GridSource.From<Trade>([new("Dup", 1), new("Dup", 1), new("Tail", 2)]);
        source.OnColumnsChanged(
        [
            new ColumnInfo<Trade>("Book", ColumnType.Text, t => t.Book),
            new ColumnInfo<Trade>("Amount", ColumnType.Number, t => t.Amount),
        ]);
        var version = source.RowSequenceVersion;
        var second = source.Window[1];

        source.ReplaceRow(second, second with { Amount = 9 });

        Assert.Equal(1m, source.Window[0].Amount);
        Assert.Equal(9m, source.Window[1].Amount);
        // Nothing reordered, so the selection survives (ADR-0011) — the value-equality
        // bug also bumped the version here, through its spurious identity diff.
        Assert.Equal(version, source.RowSequenceVersion);
    }

    [Fact] // ADR-0007: a stale value-equal copy is a different instance, and identity refuses it
    public void A_value_equal_stranger_is_refused()
    {
        var source = Source(out _);

        Assert.Throws<ArgumentException>(
            () => source.ReplaceRow(new Trade("Alpha", 3), new Trade("Alpha", 4)));
    }
}
