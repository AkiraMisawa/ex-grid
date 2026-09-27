using System.Collections;
using System.Collections.Immutable;
using ExGrid;

namespace ExSheet;

/// <summary>
/// One ExSheet's columns <c>A</c> … <c>XFD</c> as ExGrid is handed them: the shared
/// <see cref="SheetColumns.All"/>, with a column of its own in place of each one whose width has
/// been set (ADR-0016, ADR-0047). Immutable: setting a width gives a new list, because the list's
/// identity is what tells the grid that its columns changed (ADR-0003).
/// </summary>
/// <remarks>
/// A column's width is set two ways, and the list remembers which. The user resizes it, and
/// from then on it is the user's: an entry never widens it again. Or an entry of a number that
/// does not fit widens it while it is still at its default width, as Excel does — and it stays
/// at its default width, in that sense, until the user resizes it (ADR-0047, second round).
/// </remarks>
internal sealed class SheetColumnList : IReadOnlyList<GridColumn<SheetRow>>
{
    private readonly ImmutableDictionary<int, GridColumn<SheetRow>> _set;
    private readonly ImmutableHashSet<int> _resized;

    private SheetColumnList(ImmutableDictionary<int, GridColumn<SheetRow>> set, ImmutableHashSet<int> resized)
    {
        _set = set;
        _resized = resized;
    }

    /// <summary>Every column at its default width.</summary>
    internal static SheetColumnList Default { get; } = new(ImmutableDictionary<int, GridColumn<SheetRow>>.Empty, []);

    /// <inheritdoc />
    public GridColumn<SheetRow> this[int index] => _set.TryGetValue(index, out var column) ? column : SheetColumns.All[index];

    /// <inheritdoc />
    public int Count => SheetColumns.All.Count;

    /// <summary>The column's width in pixels, as the grid is handed it.</summary>
    internal double WidthPxOf(int column) =>
        _set.TryGetValue(column, out var set) ? set.Width.Width.FixedPx : SheetColumns.DefaultWidthPx;

    /// <summary>Whether the column is still at its default width: the user has not resized it.</summary>
    internal bool IsDefault(int column) => !_resized.Contains(column);

    /// <summary>The list with the width the user resized the column to; the column is the user's from now on.</summary>
    internal SheetColumnList ResizedByUser(int column, double widthPx) =>
        new(_set.SetItem(column, SheetColumns.At(column, widthPx)), _resized.Add(column));

    /// <summary>The list with the column widened to fit an entry; it is still at its default width.</summary>
    internal SheetColumnList WidenedTo(int column, double widthPx) =>
        new(_set.SetItem(column, SheetColumns.At(column, widthPx)), _resized);

    /// <inheritdoc />
    public IEnumerator<GridColumn<SheetRow>> GetEnumerator()
    {
        for (var i = 0; i < Count; i++) yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
