using System.Collections;
using System.Collections.Immutable;
using ExGrid;
using ExGrid.Columns;
using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// One ExSheet's columns <c>A</c> … <c>XFD</c> as ExGrid is handed them: the shared
/// <see cref="SheetColumns.All"/>, with a column of its own in place of each one whose width the
/// Sheet records (ADR-0046, ADR-0047). Immutable: a change of width gives a new list, because the
/// list's identity is what tells the grid that its columns changed (ADR-0003).
/// </summary>
/// <remarks>
/// The widths are the Sheet Document's, not this list's (ADR-0046): the list only mirrors what
/// <see cref="Sheet.GetColumnWidth"/> answers, converted from characters to the grid's pixels
/// (<see cref="SheetColumns.PxOf"/>). It is refreshed for the columns an engine change names
/// (<see cref="SheetChange.Columns"/>), and rebuilt when a Sheet is opened or the Cell Metrics
/// the conversion reads change.
/// </remarks>
internal sealed class SheetColumnList : IReadOnlyList<GridColumn<SheetRow>>
{
    private readonly ImmutableDictionary<int, GridColumn<SheetRow>> _set;

    private SheetColumnList(ImmutableDictionary<int, GridColumn<SheetRow>> set, CellTextMetrics? metrics)
    {
        _set = set;
        Metrics = metrics;
    }

    /// <summary>Every column at its default width.</summary>
    internal static SheetColumnList Default { get; } = new(ImmutableDictionary<int, GridColumn<SheetRow>>.Empty, null);

    /// <summary>The Cell Metrics the recorded widths were converted with; null when none is recorded.</summary>
    internal CellTextMetrics? Metrics { get; }

    /// <summary>Every width the Sheet records, converted with <paramref name="metrics"/>.</summary>
    internal static SheetColumnList Of(Sheet sheet, CellTextMetrics metrics) =>
        new SheetColumnList(ImmutableDictionary<int, GridColumn<SheetRow>>.Empty, metrics)
            .Refreshed(sheet, Enumerable.Range(0, Sheet.ColumnCount), metrics);

    /// <inheritdoc />
    public GridColumn<SheetRow> this[int index] => _set.TryGetValue(index, out var column) ? column : SheetColumns.All[index];

    /// <inheritdoc />
    public int Count => SheetColumns.All.Count;

    /// <summary>The column's width in pixels, as the grid is handed it.</summary>
    internal double WidthPxOf(int column) =>
        _set.TryGetValue(column, out var set) ? set.Width.Width.FixedPx : SheetColumns.DefaultWidthPx;

    /// <summary>
    /// The list with <paramref name="columns"/> as the Sheet now records them: a column whose
    /// width is set gets a column of its own at that width, one back at the default width goes
    /// back to the shared column. Answers this same list when nothing it hands the grid changed,
    /// and keeps every other column's instance (ADR-0003).
    /// </summary>
    internal SheetColumnList Refreshed(Sheet sheet, IEnumerable<int> columns, CellTextMetrics metrics)
    {
        var set = _set;
        foreach (var column in columns)
        {
            if (sheet.GetColumnWidth(column)?.Width is { } characters)
            {
                var px = SheetColumns.PxOf(characters, metrics);
                if (!(set.TryGetValue(column, out var held) && held.Width.Width.FixedPx == px)) set = set.SetItem(column, SheetColumns.At(column, px));
            }
            else
            {
                set = set.Remove(column);
            }
        }
        return ReferenceEquals(set, _set) && Equals(metrics, Metrics) ? this : new SheetColumnList(set, metrics);
    }

    /// <inheritdoc />
    public IEnumerator<GridColumn<SheetRow>> GetEnumerator()
    {
        for (var i = 0; i < Count; i++) yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
