using ExGrid;
using ExGrid.Cells;
using ExGrid.Columns;
using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// Columns <c>A</c> … <c>XFD</c>, built once for the process (ADR-0046). Every delegate they hold
/// reads from the row it is handed, never from an instance of the component, so one list serves
/// every ExSheet on every circuit. An ExSheet hands the grid a <see cref="SheetColumnList"/> over
/// this list, which changes instance only when a column's width is set, so a render of the
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

    /// <summary>
    /// The per-cell alignment (ADR-0050, item 7): the engine's, as <see cref="SheetCellText.AlignOf"/>
    /// maps it. It reads only the row it is handed, so, like <see cref="CellType"/>, it is one
    /// instance for the process and never has to be replaced: a cell whose alignment changes is
    /// named by the engine's change and arrives on a new row instance (ADR-0003).
    /// </summary>
    internal static Func<SheetRow, GridColumn<SheetRow>, CellAlign> CellAlign { get; } =
        static (row, column) => row.At(IndexOf(column))?.Align ?? global::ExGrid.Columns.CellAlign.Auto;

    /// <summary>
    /// The painted text (ADR-0050 item 11): General fitted to the column, as Excel's is
    /// (ADR-0047). The grid hands the column's content width in pixels and the Cell Metrics it
    /// judges the cell with, and the engine's text is the widest whose glyphs those metrics charge
    /// within the content, each at its own width (ADR-0016; ticket 91), as Excel fits General to
    /// what its font paints. It reads only the row it is handed, so it is one instance for the
    /// process: a cell whose text changes arrives on a new row instance, and a change of width
    /// repaints the rows on the grid's side (ADR-0003).
    /// </summary>
    internal static PaintedTextOf<SheetRow> PaintedText { get; } =
        static (row, column, contentWidthPx, metrics) => row.PaintedAt(IndexOf(column), contentWidthPx, metrics);

    /// <summary>A content width in pixels as Excel's column width: how many of the grid's digits fit in it.</summary>
    internal static double CharactersIn(double contentWidthPx, CellTextMetrics metrics) =>
        contentWidthPx <= 0 || metrics.DigitWidthPx <= 0 ? 0 : contentWidthPx / metrics.DigitWidthPx;

    /// <summary>
    /// A width the Sheet records, in characters (ADR-0047, third round), as the column's width in
    /// pixels: the characters' digits and the cell's padding on both sides — the exact inverse of
    /// <see cref="CharactersIn"/> over the column's content width. The unit stays Excel's, a digit
    /// of the default font; only what fits in it is charged glyph by glyph (ticket 91). Rounded to
    /// a millionth of a pixel, so a width that went through characters and back is the pixel width
    /// it came from.
    /// </summary>
    internal static double PxOf(double characters, CellTextMetrics metrics) =>
        Math.Round(characters * metrics.DigitWidthPx + 2 * metrics.CellHorizontalPaddingPx, 6);

    /// <summary>
    /// A column's width in pixels as the Sheet records it, in characters (ADR-0047, third round):
    /// the inverse of <see cref="PxOf"/>, bounded above by <see cref="Sheet.MaxColumnWidth"/>,
    /// Excel's limit; null for a width with no room for any character, which the Sheet cannot
    /// record (a width of 0 is how Excel hides a column, ADR-0046).
    /// </summary>
    internal static double? CharactersOfColumn(double columnWidthPx, CellTextMetrics metrics)
    {
        var characters = CharactersIn(metrics.ContentWidthPx(columnWidthPx), metrics);
        return characters > 0 ? Math.Min(characters, Sheet.MaxColumnWidth) : null;
    }

    /// <summary>The Cell Editor's opening text: the Entry (ADR-0051).</summary>
    internal static Func<SheetRow, GridColumn<SheetRow>, string?> EditorText { get; } =
        static (row, column) => row.EntryTextAt(IndexOf(column));

    /// <summary>What the Formula Bar shows dimmed for a spilled cell: its Anchor's Formula (ADR-0125).</summary>
    internal static Func<SheetRow, GridColumn<SheetRow>, string?> SpilledFormula { get; } =
        static (row, column) => row.SpilledFormulaAt(IndexOf(column));

    /// <summary>The sheet column a grid column stands for.</summary>
    internal static int IndexOf(GridColumn<SheetRow> column) =>
        CellAddress.TryParseColumn(column.Name, out var index)
            ? index
            : throw new ArgumentException($"'{column.Name}' is not one of the Sheet's columns.", nameof(column));

    /// <summary>
    /// Sheet column <paramref name="index"/> at <paramref name="widthPx"/> — what a
    /// <see cref="SheetColumnList"/> puts in place of the default column once a width is set.
    /// </summary>
    /// <remarks>
    /// A document may record a column narrower than the grid's default MinWidth, and the grid
    /// refuses a Fixed width below its column's MinWidth rather than clamping it (ADR-0016), so
    /// such a column's MinWidth is its own width: it is painted as narrow as it is recorded.
    /// </remarks>
    internal static GridColumn<SheetRow> At(int index, double widthPx) =>
        Column(index, new ColumnWidthSpec(ColumnWidth.Fixed(widthPx), minWidthPx: Math.Min(ColumnWidthSpec.DefaultMinWidthPx, widthPx)));

    private static GridColumn<SheetRow>[] Build()
    {
        var columns = new GridColumn<SheetRow>[Sheet.ColumnCount];
        for (var i = 0; i < columns.Length; i++) columns[i] = Column(i, DefaultWidth);
        return columns;
    }

    private static GridColumn<SheetRow> Column(int index, ColumnWidthSpec width)
    {
        var name = CellAddress.ColumnName(index);
        return new GridColumn<SheetRow>(
            name,
            ColumnType.Text,
            row => row.At(index),
            header: name,
            width: width,
            editable: true,
            headerAlign: global::ExGrid.Columns.CellAlign.Center,
            validate: static (row, typed) => row.Judge(typed),
            format: FormatText);
    }
}
