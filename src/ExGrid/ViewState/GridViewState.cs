using ExGrid.Columns;

namespace ExGrid;

/// <summary>
/// The View State (<c>CONTEXT.md</c>) as one value, so that one call saves a user's view and one
/// restores it (ADR-0207): which columns show, in which order, at which widths and whether each is
/// pinned, and the sort and the filter. The grid holds none of it (ADR-0001). It arrives as the
/// parameters <c>Columns</c>, <c>PinnedColumnCount</c>, <c>Sorts</c> and <c>Filters</c> — or as a
/// Grid Source's sort and filter — and leaves as their notifications; this is the Consumer's record
/// of them, which <see cref="GridViewStateJson"/> writes as JSON and reads back.
///
/// <para>A column the user hid is one the list leaves out. A width is the intent: an Auto column is
/// held as Auto, never as the width it was painted at, so a restored Auto column measures itself
/// again (ADR-0016). The pinned columns are the leading ones (ADR-0004), and the constructor refuses
/// a pinned column after one that is not, a column named twice and a column with no name.</para>
/// </summary>
public sealed record GridViewState
{
    /// <summary>A View State of <paramref name="columns"/>, in display order, under
    /// <paramref name="sorts"/> and <paramref name="filter"/>. The lists are copied.</summary>
    /// <param name="columns">The columns shown, in display order, the pinned ones leading.</param>
    /// <param name="sorts">The sort, outermost first; empty for none.</param>
    /// <param name="filter">The filter in force, or null for none.</param>
    public GridViewState(IReadOnlyList<ColumnViewState> columns, IReadOnlyList<SortSpec> sorts, GridFilter? filter)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(sorts);
        var named = new HashSet<string>(StringComparer.Ordinal);
        var pinned = 0;
        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i] ?? throw new ArgumentException($"The View State's column {i + 1} is null.", nameof(columns));
            if (!named.Add(column.Column))
                throw new ArgumentException($"Column '{column.Column}' appears twice in the View State; a column stands in one place.", nameof(columns));
            if (column.Pinned && pinned < i)
            {
                throw new ArgumentException(
                    $"Column '{column.Column}' is pinned after a column that is not; the pinned columns are the leading ones (ADR-0004).",
                    nameof(columns));
            }
            if (column.Pinned)
                pinned++;
        }
        for (var i = 0; i < sorts.Count; i++)
        {
            if (sorts[i] is null)
                throw new ArgumentException($"The View State's sort level {i + 1} is null.", nameof(sorts));
        }
        Columns = [.. columns];
        Sorts = [.. sorts];
        Filter = filter;
        PinnedColumnCount = pinned;
    }

    /// <summary>The View State a grid is shown under: the <paramref name="columns"/> handed to its
    /// <c>Columns</c> parameter, each with its declared width, the leading
    /// <paramref name="pinnedColumnCount"/> of them pinned, and the sort and the filter in force — the
    /// grid's <c>Sorts</c> and <c>Filters</c>, or a Grid Source's <see cref="IGridSource{TRow}.Sorts"/>
    /// and <see cref="IGridSource{TRow}.Filter"/>.</summary>
    /// <param name="columns">The columns shown, in display order.</param>
    /// <param name="pinnedColumnCount">How many leading columns are pinned; refused outside
    /// <c>[0, columns.Count]</c>, as the grid refuses it (ADR-0004).</param>
    /// <param name="sorts">The sort in force; null or empty for none.</param>
    /// <param name="filter">The filter in force, or null for none.</param>
    /// <typeparam name="TRow">The grid's row type.</typeparam>
    public static GridViewState Of<TRow>(
        IReadOnlyList<GridColumn<TRow>> columns, int pinnedColumnCount, IReadOnlyList<SortSpec>? sorts, GridFilter? filter)
    {
        ArgumentNullException.ThrowIfNull(columns);
        if (pinnedColumnCount < 0 || pinnedColumnCount > columns.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(pinnedColumnCount), pinnedColumnCount,
                $"PinnedColumnCount is the first N of {columns.Count} columns; there are not that many (ADR-0004).");
        }
        var shown = new ColumnViewState[columns.Count];
        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i] ?? throw new ArgumentException($"Column {i + 1} is null.", nameof(columns));
            shown[i] = new ColumnViewState(column.Name, column.Width.Width, i < pinnedColumnCount);
        }
        return new GridViewState(shown, sorts ?? [], filter);
    }

    /// <summary>The columns shown, in display order, the pinned ones leading. A hidden column is
    /// one this leaves out.</summary>
    public IReadOnlyList<ColumnViewState> Columns { get; }

    /// <summary>The sort, outermost first; empty for none (ADR-0001).</summary>
    public IReadOnlyList<SortSpec> Sorts { get; }

    /// <summary>The filter in force, or null for none (ADR-0002).</summary>
    public GridFilter? Filter { get; }

    /// <summary>How many leading columns are pinned: what the grid's <c>PinnedColumnCount</c>
    /// takes.</summary>
    public int PinnedColumnCount { get; }
}

/// <summary>
/// One column of a <see cref="GridViewState"/>: which column, named as the grid names it, its width
/// and whether it is pinned (ADR-0207). Its place in the View State's list is its place in the
/// column order.
/// </summary>
public sealed record ColumnViewState
{
    /// <summary>The column named <paramref name="column"/> at <paramref name="width"/>.</summary>
    /// <param name="column">The column's name, as <see cref="GridColumn{TRow}.Name"/> holds it;
    /// refused when null or empty.</param>
    /// <param name="width">The width intent: Auto, or a Fixed number of pixels (ADR-0016).</param>
    /// <param name="pinned">Whether the column is pinned.</param>
    public ColumnViewState(string column, ColumnWidth width, bool pinned = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(column);
        Column = column;
        Width = width;
        Pinned = pinned;
    }

    /// <summary>The column's name.</summary>
    public string Column { get; }

    /// <summary>The width intent: Auto, or a Fixed number of pixels — never the width an Auto
    /// column was painted at (ADR-0016).</summary>
    public ColumnWidth Width { get; }

    /// <summary>Whether the column is pinned: held against the Viewport's leading edge with the
    /// columns pinned before it (ADR-0004).</summary>
    public bool Pinned { get; }
}
