using ExSheet.Engine;

namespace ExSheet.Xlsx;

/// <summary>
/// What reading an <c>.xlsx</c> returns beside the Workbook Document (CONTEXT.md, ADR-0330 §5): what the file held that
/// ExSheet did not carry, and the cells whose Formulas name a function ExSheet does not support, each with its Sheet and
/// place. The package shows nothing; the application decides how to tell the user.
/// </summary>
public sealed class OpenReport
{
    internal OpenReport(IReadOnlyList<NotCarried> notCarried, IReadOnlyList<UnsupportedFunctionCell> unsupportedFunctions)
    {
        NotCarried = notCarried;
        UnsupportedFunctions = unsupportedFunctions;
    }

    /// <summary>
    /// What the file held that changes how a Sheet looks, or what else the file held, and that ExSheet did not carry
    /// (ADR-0330 §4's "Not carried"), in the workbook's order: what the workbook holds first, then each Sheet's in turn.
    /// Saving the Workbook again will not keep any of it.
    /// </summary>
    public IReadOnlyList<NotCarried> NotCarried { get; }

    /// <summary>
    /// The cells whose Formula names a function outside ExSheet's declared set (<see cref="DeclaredFunction"/>), in
    /// Sheet order and then row by row: each is carried, and shows <c>#NAME?</c> (ADR-0047, ADR-0330 §4).
    /// </summary>
    public IReadOnlyList<UnsupportedFunctionCell> UnsupportedFunctions { get; }

    /// <summary>Whether the file was carried whole: nothing left behind, and every function it names supported.</summary>
    public bool IsEmpty => NotCarried.Count == 0 && UnsupportedFunctions.Count == 0;
}

/// <summary>What kind of thing an <see cref="OpenReport"/> says was not carried (ADR-0330 §4).</summary>
public enum NotCarriedKind
{
    /// <summary>A worksheet that is hidden: it opens shown (ADR-0331 §4).</summary>
    HiddenSheet,

    /// <summary>A sheet that is not a worksheet — a chart sheet, a dialog or macro sheet — which holds no cells.</summary>
    ChartSheet,

    /// <summary>
    /// A defined name, which no Formula carried reads; <see cref="NotCarried.Detail"/> is its name, and
    /// <see cref="NotCarried.Sheet"/> the sheet it is local to, or null for a name of the whole workbook.
    /// </summary>
    DefinedName,

    /// <summary>Rows given heights of their own.</summary>
    RowHeights,

    /// <summary>Rows that are hidden: they open shown.</summary>
    HiddenRows,

    /// <summary>Columns that are hidden: they open shown.</summary>
    HiddenColumns,

    /// <summary>Panes frozen in the Sheet's view.</summary>
    FrozenPanes,

    /// <summary>Merged cells: each cell is carried on its own.</summary>
    MergedCells,

    /// <summary>Conditional formatting.</summary>
    ConditionalFormatting,

    /// <summary>Data validation.</summary>
    DataValidation,

    /// <summary>Notes and comments.</summary>
    NotesAndComments,

    /// <summary>Hyperlinks: the cells' text is carried.</summary>
    Hyperlinks,

    /// <summary>Pictures, charts, shapes and controls drawn over the Sheet.</summary>
    PicturesAndCharts,

    /// <summary>The Sheet's protection.</summary>
    Protection,

    /// <summary>Print settings beyond Excel's defaults: margins, a page set-up of its own, headers and footers with text, page breaks, printed gridlines or headings, a fit to pages, a print area or print titles.</summary>
    PrintSettings,

    /// <summary>An AutoFilter, and the sort state it keeps.</summary>
    AutoFilter,

    /// <summary>The structure of an Excel Table that no Formula reads: its cells are carried.</summary>
    ExcelTable,

    /// <summary>The Sheet's tab colour.</summary>
    SheetProperties,

    /// <summary>Rich-text runs inside cells: the text is carried, the runs' own formatting is not.</summary>
    RichText,

    /// <summary>A PivotTable: the cells it shows are carried as constants, the PivotTable is not.</summary>
    PivotTables,

    /// <summary>Links to other workbooks that no Formula carried reads.</summary>
    ExternalLinks,

    /// <summary>Connections to outside data, and the queries that read through them.</summary>
    DataConnections,

    /// <summary>Slicers and timelines.</summary>
    Slicers,

    /// <summary>View settings that change how the Sheet looks: hidden gridlines, headings or zeros, Formulas shown, right to left, a zoom, a page layout.</summary>
    View,

    /// <summary>Anything else a worksheet holds; <see cref="NotCarried.Detail"/> names its element, as <c>customSheetViews</c>.</summary>
    Other,

    /// <summary>
    /// Colours that name the workbook's theme: each is carried as the RGB value the file's theme gives it, and the link to the
    /// theme is not, so another theme does not recolour them (ADR-0330 §8). Said once, of the whole workbook.
    /// </summary>
    ThemeColours,

    /// <summary>A typeface or a size other than the workbook's own font's: the cells open in the Sheet's one font (ADR-0071).</summary>
    FontNamesAndSizes,

    /// <summary>Vertical alignment other than bottom.</summary>
    VerticalAlignment,

    /// <summary>Wrapped, indented, rotated or shrunk text.</summary>
    TextLayout,

    /// <summary>A horizontal alignment a Sheet does not have — fill, justify, distributed, centred across the selection: the cells open aligned as General.</summary>
    HorizontalAlignment,

    /// <summary>A double or accounting underline: the cells open with a single underline.</summary>
    Underline,

    /// <summary>A pattern or gradient fill, or a solid one of no colour: the cells open with no Fill.</summary>
    PatternFills,

    /// <summary>Diagonal borders: the cells keep their four sides.</summary>
    DiagonalBorders,

    /// <summary>A default column width of the Sheet's own: its columns without a width of their own open at the default width.</summary>
    DefaultColumnWidth,
}

/// <summary>One thing the file held that was not carried (ADR-0330 §4).</summary>
/// <param name="Kind">What it is.</param>
/// <param name="Sheet">The Sheet it was on, or <see langword="null"/> for what the workbook as a whole held.</param>
/// <param name="Detail">What names it more closely, where something does: a defined name, the first cell of rich text or of a
/// format not carried (a cell as <c>B7</c>, columns as <c>B:C</c>, rows as <c>5:5</c>), an element's name; otherwise
/// <see langword="null"/>.</param>
/// <param name="Description">One sentence an application can show as it stands.</param>
public sealed record NotCarried(NotCarriedKind Kind, string? Sheet, string? Detail, string Description);

/// <summary>
/// A cell whose Formula names a function ExSheet does not support (ADR-0330 §4, §5): the Formula is carried, and shows
/// <c>#NAME?</c>.
/// </summary>
/// <param name="Sheet">The Sheet's name.</param>
/// <param name="Cell">The cell, as <c>B7</c>.</param>
/// <param name="Functions">The functions it names that are not supported, in the Formula's order.</param>
public sealed record UnsupportedFunctionCell(string Sheet, string Cell, IReadOnlyList<string> Functions);

/// <summary>A Workbook Document read from an <c>.xlsx</c>, and its Open Report (ADR-0330 §5).</summary>
/// <param name="Document">The Workbook Document: every worksheet a Sheet, in the file's order, the one shown shown.</param>
/// <param name="Report">What was not carried, and the cells naming functions ExSheet does not support.</param>
public sealed record XlsxOpenResult(WorkbookDocument Document, OpenReport Report);
