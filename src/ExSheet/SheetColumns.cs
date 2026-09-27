using ExGrid;
using ExGrid.Columns;
using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// Columns <c>A</c> … <c>XFD</c>, built once for the process (ADR-0046). Every delegate they hold
/// reads from the row it is handed, never from an instance of the component, so one list serves
/// every ExSheet on every circuit — and because the list instance never changes, a render of the
/// component never looks to a row like a change of columns (ADR-0003).
/// </summary>
internal static class SheetColumns
{
    /// <summary>
    /// Excel's default column width, 8.43 digits, in the grid's own Cell Metrics: the width that
    /// holds 8.43 of its digits and the cell's padding, rounded up to a whole pixel. Excel's 64 px
    /// is that width for Calibri 11; the grid's font has wider digits, and a column narrower
    /// than Excel's in characters would turn numbers Excel shows into <c>####</c>.
    /// </summary>
    internal static readonly double DefaultWidthPx = Math.Ceiling(
        8.43 * global::ExGrid.Components.ExGrid<SheetRow>.DefaultCellMetrics.DigitWidthPx
        + 2 * global::ExGrid.Components.ExGrid<SheetRow>.DefaultCellMetrics.CellHorizontalPaddingPx);

    private static readonly ColumnWidthSpec DefaultWidth = new(ColumnWidth.Fixed(DefaultWidthPx));

    private static readonly Func<object, string> FormatText = static value => ((SheetCellText)value).Text;

    /// <summary>All 16,384 columns, in sheet order: the column at index <c>i</c> is sheet column <c>i</c>.</summary>
    internal static IReadOnlyList<GridColumn<SheetRow>> All { get; } = Build();

    /// <summary>
    /// The per-cell kind (ADR-0050, item 6): a number (a date included) is a Number, which
    /// right-aligns it and turns it into <c>####</c> when it does not fit (ADR-0016); anything
    /// else is Text. It reads only the row it is handed, so it never has to be replaced: a
    /// cell whose kind changes arrives on a new row instance (ADR-0003).
    /// </summary>
    internal static Func<SheetRow, GridColumn<SheetRow>, ColumnType> CellType { get; } =
        static (row, column) => row.At(IndexOf(column)) is { IsNumber: true } ? ColumnType.Number : ColumnType.Text;

    /// <summary>The Cell Editor's opening text: the Entry (ADR-0051).</summary>
    internal static Func<SheetRow, GridColumn<SheetRow>, string?> EditorText { get; } =
        static (row, column) => row.EntryTextAt(IndexOf(column));

    /// <summary>The sheet column a grid column stands for.</summary>
    internal static int IndexOf(GridColumn<SheetRow> column) =>
        CellAddress.TryParseColumn(column.Name, out var index)
            ? index
            : throw new ArgumentException($"'{column.Name}' is not one of the Sheet's columns.", nameof(column));

    private static GridColumn<SheetRow>[] Build()
    {
        var columns = new GridColumn<SheetRow>[Sheet.ColumnCount];
        for (var i = 0; i < columns.Length; i++)
        {
            var index = i;
            var name = CellAddress.ColumnName(i);
            columns[i] = new GridColumn<SheetRow>(
                name,
                ColumnType.Text,
                row => row.At(index),
                header: name,
                width: DefaultWidth,
                editable: true,
                headerAlign: CellAlign.Center,
                validate: static (row, typed) => row.Judge(typed),
                format: FormatText);
        }
        return columns;
    }
}
