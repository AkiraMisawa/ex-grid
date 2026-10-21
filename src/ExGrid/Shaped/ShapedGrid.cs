using System.Globalization;
using ExGrid.Columns;
using ExGrid.Data;

namespace ExGrid;

/// <summary>
/// A grid built at run time from a source's shape (ADR-0342): the columns a grid view's spec shows,
/// over a Snapshot, with no row type of the Consumer's. A row is a position in the Snapshot
/// (<see cref="ShapedRow"/>), and the sort and the filter are executed by
/// <see cref="GridSource.From{TRow}(IReadOnlyList{TRow})"/>, with its semantics. Hand
/// <see cref="Columns"/>, <see cref="Source"/> and <see cref="PinnedColumnCount"/> to an
/// <c>ExGrid</c> of <c>TRow="ShapedRow"</c>.
/// <para>Each column's text comes from its format code: the one the spec writes, or the kind's
/// default — an integer <c>#,##0</c>, a date <c>yyyy-mm-dd</c>, or <c>yyyy-mm-dd hh:mm:ss</c> when
/// the Snapshot's column holds a time of day, so a time is never hidden — and text and booleans as
/// they are.</para>
/// </summary>
public sealed class ShapedGrid
{
    /// <summary>The format an integer column shows in when the spec writes none.</summary>
    public const string IntegerFormat = "#,##0";

    /// <summary>The format a date column shows in when the spec writes none.</summary>
    public const string DateFormat = "yyyy-mm-dd";

    /// <summary>The format a date column holding a time of day shows in when the spec writes none.</summary>
    public const string DateTimeFormat = "yyyy-mm-dd hh:mm:ss";

    private ShapedGrid(Snapshot snapshot, GridViewSpecReading spec, IReadOnlyList<GridColumn<ShapedRow>> columns, ShapedRow[] rows, InMemoryGridSource<ShapedRow> source)
    {
        Snapshot = snapshot;
        Rows = Array.AsReadOnly(rows);
        Spec = spec;
        Columns = columns;
        Source = source;
        PinnedColumnCount = spec.ViewState.PinnedColumnCount;
    }

    /// <summary>The Snapshot shown.</summary>
    public Snapshot Snapshot { get; }

    /// <summary>The spec it was built from.</summary>
    public GridViewSpecReading Spec { get; }

    /// <summary>The columns, in the spec's order at its widths.</summary>
    public IReadOnlyList<GridColumn<ShapedRow>> Columns { get; }

    /// <summary>Every row of the Snapshot, in its base order, each made once (<see cref="ShapedRow"/>).</summary>
    public IReadOnlyList<ShapedRow> Rows { get; }

    /// <summary>The source, already under the spec's sort and filter. Each row is its own Row Key, so
    /// <see cref="InMemoryGridSource{TRow}.ReplaceAll"/> with some of <see cref="Rows"/>, in their
    /// order, narrows it and keeps the sort and the filter the user has set.</summary>
    public InMemoryGridSource<ShapedRow> Source { get; }

    /// <summary>How many leading columns the spec pins.</summary>
    public int PinnedColumnCount { get; }

    /// <summary>
    /// The grid <paramref name="spec"/> describes, over <paramref name="snapshot"/>, its text written
    /// in <paramref name="culture"/> (the invariant culture by default, as a column's default text is,
    /// so the same value reads the same on every host).
    /// </summary>
    /// <exception cref="BoardRefusedException">The Snapshot lacks a column the spec shows, or holds it
    /// of another kind.</exception>
    public static ShapedGrid Of(Snapshot snapshot, GridViewSpecReading spec, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(spec);
        culture ??= CultureInfo.InvariantCulture;
        var widths = spec.ViewState.Columns.ToDictionary(c => c.Column, c => c.Width, StringComparer.Ordinal);
        var columns = new GridColumn<ShapedRow>[spec.Columns.Count];
        for (var i = 0; i < columns.Length; i++)
        {
            var shown = spec.Columns[i];
            if (!snapshot.TryGetColumn(shown.Column, out var held) || held.Kind != shown.Kind)
            {
                throw new BoardRefusedException(spec.Data, held is null
                    ? $"Input '{spec.Data}' declares column '{shown.Column}', which its source does not have."
                    : $"Input '{spec.Data}', column '{shown.Column}': declared {BoardInput.NameOf(shown.Kind)}, and its source holds {BoardInput.NameOf(held.Kind)}.");
            }
            columns[i] = Column(shown, held is DateColumn { HasTime: true }, widths[shown.Column], culture, row => snapshot.ValueAt(row.At, held));
        }
        var rows = ShapedRow.Of(snapshot);
        var source = GridSource.From<ShapedRow>(rows, row => row);
        source.OnColumnsChanged([.. columns.Select(c => c.Info)]);
        source.OnSortChanged(spec.ViewState.Sorts);
        source.OnFilterChanged(spec.ViewState.Filter);
        return new ShapedGrid(snapshot, spec, columns, rows, source);
    }

    /// <summary>
    /// The grid <paramref name="spec"/> describes, its rows fetched a page at a time from a source held
    /// where the data lives (ADR-0343): each fetch is handed the grid's query, its range, sort and filter
    /// (ADR-0025), and answered with the rows as a Snapshot. Each page is checked as a whole Snapshot is,
    /// and one lacking a column the spec shows, or holding it of another kind, fails that fetch. A date
    /// column the spec writes no format for shows its time (<see cref="DateTimeFormat"/>): a shape does not
    /// say whether a page will hold one.
    /// </summary>
    /// <param name="spec">The grid view's spec, read against the input's declared shape.</param>
    /// <param name="fetch">Answers a query with a page of rows. Called again, with the same query, when
    /// the source is told its data moved on (<see cref="FetchingGridSource{TRow}.NotifyChanged()"/>).</param>
    /// <param name="culture">The culture the text is written in; the invariant culture by default.</param>
    /// <param name="readAheadRows">How many rows past the Window each fetch asks for.</param>
    /// <exception cref="BoardRefusedException">A format code the spec writes cannot be read.</exception>
    public static FetchedShapedGrid Fetch(GridViewSpecReading spec, Func<GridQuery, CancellationToken, ValueTask<ShapedPage>> fetch,
        CultureInfo? culture = null, int readAheadRows = 60)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(fetch);
        culture ??= CultureInfo.InvariantCulture;
        var widths = spec.ViewState.Columns.ToDictionary(c => c.Column, c => c.Width, StringComparer.Ordinal);
        var columns = new GridColumn<ShapedRow>[spec.Columns.Count];
        for (var i = 0; i < columns.Length; i++)
        {
            var shown = spec.Columns[i];
            var name = shown.Column;
            // A page's column found once per page, not once per cell: the rows of one page share it.
            (Snapshot Page, SnapshotColumn Column)? last = null;
            columns[i] = Column(shown, hasTime: true, widths[name], culture, row =>
            {
                var held = last is { } known && ReferenceEquals(known.Page, row.Page) ? known.Column : row.Page[name];
                last = (row.Page, held);
                return row.Page.ValueAt(row.At, held);
            });
        }
        var source = GridSource.Fetch<ShapedRow>(async (query, cancellation) =>
        {
            var page = await fetch(query, cancellation).ConfigureAwait(false);
            Check(page.Rows, spec);
            return new GridPage<ShapedRow>(ShapedRow.Of(page.Rows, page.Start), page.Start, page.TotalCount);
        }, readAheadRows);
        // The query first, so the first page is fetched under it, never once without it and again.
        source.OnSortChanged(spec.ViewState.Sorts);
        source.OnFilterChanged(spec.ViewState.Filter);
        source.OnColumnsChanged([.. columns.Select(c => c.Info)]);
        return new FetchedShapedGrid(spec, columns, source);
    }

    private static void Check(Snapshot page, GridViewSpecReading spec)
    {
        foreach (var shown in spec.Columns)
        {
            if (!page.TryGetColumn(shown.Column, out var held) || held.Kind != shown.Kind)
            {
                throw new BoardRefusedException(spec.Data, held is null
                    ? $"Input '{spec.Data}' declares column '{shown.Column}', which a page its source answered does not have."
                    : $"Input '{spec.Data}', column '{shown.Column}': declared {BoardInput.NameOf(shown.Kind)}, and a page its source answered holds {BoardInput.NameOf(held.Kind)}.");
            }
        }
    }

    private static GridColumn<ShapedRow> Column(ShapedColumn shown, bool hasTime, ColumnWidth width, CultureInfo culture, Func<ShapedRow, object?> value)
    {
        var code = shown.Format ?? shown.Kind switch
        {
            SnapshotKind.Integer => IntegerFormat,
            SnapshotKind.Date => hasTime ? DateTimeFormat : DateFormat,
            _ => null,
        };
        Func<object, string>? format = null;
        if (code is not null)
        {
            if (!FormatCode.TryParse(code, out var parsed, out var reason))
                throw new BoardRefusedException(shown.Column, $"Column '{shown.Column}': the format code \"{code}\" {reason}.");
            format = value => parsed.Format(value, culture);
        }
        var type = shown.Kind switch
        {
            SnapshotKind.Text => ColumnType.Text,
            SnapshotKind.Date => ColumnType.Date,
            SnapshotKind.Boolean => ColumnType.Boolean,
            _ => ColumnType.Number,
        };
        return new GridColumn<ShapedRow>(
            shown.Column, type, value, header: shown.Header,
            width: new ColumnWidthSpec(width), format: format,
            dateType: type == ColumnType.Date ? DateType.DateTime : null);
    }
}

/// <summary>
/// A row of a grid built from a source's shape (ADR-0342): where it stands in the Snapshot, and
/// nothing else. One is made per row when the grid is built and kept, so a row keeps its reference
/// and its render is skipped while it does not change (ADR-0003).
/// </summary>
public sealed class ShapedRow
{
    private ShapedRow(Snapshot page, SnapshotRow at, int position)
    {
        Page = page;
        At = at;
        Position = position;
    }

    /// <summary>The Snapshot the row is stored in: the grid's whole Snapshot, or, in a grid whose rows
    /// are fetched (<see cref="ShapedGrid.Fetch"/>), the page it came back in.</summary>
    public Snapshot Page { get; }

    /// <summary>Where the row is stored in <see cref="Page"/>.</summary>
    public SnapshotRow At { get; }

    /// <summary>The row's position, from 0: in the Snapshot's base order, or, in a grid whose rows are
    /// fetched, in the order of the query it was fetched for.</summary>
    public int Position { get; }

    internal static ShapedRow[] Of(Snapshot snapshot, int start = 0)
    {
        var rows = new ShapedRow[snapshot.RowCount];
        var all = snapshot.Rows;
        for (var i = 0; i < rows.Length; i++)
            rows[i] = new ShapedRow(snapshot, all[i], start + i);
        return rows;
    }
}

/// <summary>
/// A grid built from a source's shape whose rows are fetched a page at a time (ADR-0343): made by
/// <see cref="ShapedGrid.Fetch"/>. Hand <see cref="Columns"/>, <see cref="Source"/> and
/// <see cref="PinnedColumnCount"/> to an <c>ExGrid</c> of <c>TRow="ShapedRow"</c>.
/// </summary>
public sealed class FetchedShapedGrid
{
    internal FetchedShapedGrid(GridViewSpecReading spec, IReadOnlyList<GridColumn<ShapedRow>> columns, FetchingGridSource<ShapedRow> source)
    {
        Spec = spec;
        Columns = columns;
        Source = source;
        PinnedColumnCount = spec.ViewState.PinnedColumnCount;
    }

    /// <summary>The spec it was built from.</summary>
    public GridViewSpecReading Spec { get; }

    /// <summary>The columns, in the spec's order at its widths.</summary>
    public IReadOnlyList<GridColumn<ShapedRow>> Columns { get; }

    /// <summary>The source, already under the spec's sort and filter, which fetches each page.</summary>
    public FetchingGridSource<ShapedRow> Source { get; }

    /// <summary>How many leading columns the spec pins.</summary>
    public int PinnedColumnCount { get; }
}

/// <summary>
/// One answer of a source held where the data lives to a fetched grid's query (ADR-0343): the rows as
/// a Snapshot, the position the first holds in the query's order, and how many rows the whole filtered
/// result holds. Refused, as a <see cref="GridPage{TRow}"/> is, when the rows run past that count.
/// </summary>
public sealed record ShapedPage
{
    /// <summary>An answer of <paramref name="rows"/> starting at <paramref name="start"/> of a result
    /// <paramref name="totalCount"/> rows long.</summary>
    public ShapedPage(Snapshot rows, int start, int totalCount)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);
        if (start + rows.RowCount > totalCount)
            throw new ArgumentOutOfRangeException(nameof(rows), rows.RowCount, $"A page of {rows.RowCount} rows at {start} runs past a TotalCount of {totalCount}.");
        Rows = rows;
        Start = start;
        TotalCount = totalCount;
    }

    /// <summary>The rows, in the query's order; their <see cref="Snapshot.Version"/> is the version of
    /// the data they were read from.</summary>
    public Snapshot Rows { get; }

    /// <summary>The position of the first row in the query's order.</summary>
    public int Start { get; }

    /// <summary>How many rows the whole filtered result holds.</summary>
    public int TotalCount { get; }
}
