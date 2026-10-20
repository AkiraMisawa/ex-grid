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

    private ShapedGrid(Snapshot snapshot, GridViewSpecReading spec, IReadOnlyList<GridColumn<ShapedRow>> columns, InMemoryGridSource<ShapedRow> source)
    {
        Snapshot = snapshot;
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

    /// <summary>The source, already under the spec's sort and filter.</summary>
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
            columns[i] = Column(snapshot, held, shown, widths[shown.Column], culture);
        }
        var source = GridSource.From(ShapedRow.Of(snapshot));
        source.OnColumnsChanged([.. columns.Select(c => c.Info)]);
        source.OnSortChanged(spec.ViewState.Sorts);
        source.OnFilterChanged(spec.ViewState.Filter);
        return new ShapedGrid(snapshot, spec, columns, source);
    }

    private static GridColumn<ShapedRow> Column(Snapshot snapshot, SnapshotColumn held, ShapedColumn shown, ColumnWidth width, CultureInfo culture)
    {
        var code = shown.Format ?? shown.Kind switch
        {
            SnapshotKind.Integer => IntegerFormat,
            SnapshotKind.Date => held is DateColumn { HasTime: true } ? DateTimeFormat : DateFormat,
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
            shown.Column, type, row => snapshot.ValueAt(row.At, held), header: shown.Header,
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
    private ShapedRow(SnapshotRow at, int position)
    {
        At = at;
        Position = position;
    }

    /// <summary>Where the row is stored in the Snapshot.</summary>
    public SnapshotRow At { get; }

    /// <summary>The row's position in the Snapshot's base order, from 0.</summary>
    public int Position { get; }

    internal static ShapedRow[] Of(Snapshot snapshot)
    {
        var rows = new ShapedRow[snapshot.RowCount];
        var all = snapshot.Rows;
        for (var i = 0; i < rows.Length; i++)
            rows[i] = new ShapedRow(all[i], i);
        return rows;
    }
}
