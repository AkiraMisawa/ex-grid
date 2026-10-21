namespace ExGrid.Data.Xlsx;

/// <summary>What a cell's value is, as its <c>t</c> attribute writes it (ECMA-376 <c>ST_CellType</c>).</summary>
public enum XlsxCellType
{
    /// <summary>A number (<c>t="n"</c>, or no <c>t</c>): the value is its decimal text, invariant. A cell with no value is a number with none.</summary>
    Number,

    /// <summary>Text held in the shared strings (<c>t="s"</c>): the value is its index, and <see cref="XlsxCell.Text"/> the text.</summary>
    SharedString,

    /// <summary>Text written in the cell itself (<c>t="inlineStr"</c>): <see cref="XlsxCell.Text"/> is the text.</summary>
    InlineString,

    /// <summary>A Formula's text result (<c>t="str"</c>): the value is the text.</summary>
    FormulaString,

    /// <summary>A boolean (<c>t="b"</c>): the value is <c>0</c> or <c>1</c>.</summary>
    Boolean,

    /// <summary>An Error Value (<c>t="e"</c>): the value is its text, as <c>#DIV/0!</c>.</summary>
    Error,

    /// <summary>A date in ISO 8601 (<c>t="d"</c>), which Excel itself never writes.</summary>
    Date,
}

/// <summary>How a Formula is written (ECMA-376 <c>ST_CellFormulaType</c>).</summary>
public enum XlsxFormulaKind
{
    /// <summary>A Formula of the cell's own.</summary>
    Normal,

    /// <summary>
    /// A shared formula: the master carries the text, the range it covers (<see cref="XlsxFormula.Reference"/>) and its
    /// index (<see cref="XlsxFormula.SharedIndex"/>); every other cell of the group carries only the index, and its
    /// Formula is the master's moved to it.
    /// </summary>
    Shared,

    /// <summary>
    /// An array formula over <see cref="XlsxFormula.Reference"/>: a dynamic array that spills when
    /// <see cref="XlsxFormula.IsDynamicArray"/>, and a legacy multi-cell array formula otherwise.
    /// </summary>
    Array,

    /// <summary>A What-If data table.</summary>
    DataTable,
}

/// <summary>A cell's Formula as the file writes it: its text, without the leading <c>=</c>, and how it is shared.</summary>
/// <param name="Text">The Formula's text as stored, Excel's <c>_xlfn.</c> and <c>_xlws.</c> prefixes included; empty for a cell
/// that takes a shared formula from its master.</param>
/// <param name="Kind">How it is written.</param>
/// <param name="Reference">The range a shared formula's master or an array formula covers, as <c>A1:A10</c>; otherwise null.</param>
/// <param name="SharedIndex">A shared formula's group (<c>si</c>); otherwise null.</param>
/// <param name="IsDynamicArray">Whether an array formula is a dynamic array that spills (Excel 365), as the
/// workbook's cell metadata marks one; false for any other.</param>
public sealed record XlsxFormula(string Text, XlsxFormulaKind Kind = XlsxFormulaKind.Normal, string? Reference = null, int? SharedIndex = null, bool IsDynamicArray = false);

/// <summary>
/// One cell as a worksheet writes it (ADR-0330 §9): its place, its type, its value as written, its Formula and its
/// style. Nothing is interpreted: a number is its text, a Formula its stored text, a style its index into
/// <see cref="XlsxStyles.CellFormats"/>.
/// </summary>
/// <param name="Row">The cell's row, from zero (row 1 is 0).</param>
/// <param name="Column">The cell's column, from zero (column A is 0).</param>
/// <param name="Type">What the value is.</param>
/// <param name="Value">The value as written (<c>&lt;v&gt;</c>), or null for a cell with none: a cell that carries only a style,
/// or an inline string.</param>
/// <param name="Text">The text of a <see cref="XlsxCellType.SharedString"/> or <see cref="XlsxCellType.InlineString"/>; otherwise null.
/// Writing a shared string takes this text, not <paramref name="Value"/>.</param>
/// <param name="Formula">The Formula, or null.</param>
/// <param name="StyleIndex">The index into <see cref="XlsxStyles.CellFormats"/> (<c>s</c>); 0 when the cell names none.</param>
/// <param name="IsRichText">Whether the text came in runs of their own formatting, which are not carried; the text is.</param>
public readonly record struct XlsxCell(
    int Row, int Column, XlsxCellType Type, string? Value, string? Text = null, XlsxFormula? Formula = null, int StyleIndex = 0, bool IsRichText = false)
{
    /// <summary>The cell's place as <c>B7</c>.</summary>
    public string Reference => XlsxCellReference.Format(Row, Column);
}

/// <summary>A column range's width, as <c>&lt;col&gt;</c> writes it.</summary>
/// <param name="First">The first column of the range, from zero.</param>
/// <param name="Last">The last column of the range, from zero.</param>
/// <param name="Width">The width in Excel's own unit (characters of the default font's widest digit, with padding), or null when
/// the range sets none.</param>
/// <param name="Hidden">Whether the columns are hidden.</param>
/// <param name="StyleIndex">The style the columns' empty cells take, or 0.</param>
public sealed record XlsxColumn(int First, int Last, double? Width, bool Hidden = false, int StyleIndex = 0);

/// <summary>A worksheet as the workbook part lists it.</summary>
/// <param name="Name">Its name, as its tab shows it.</param>
/// <param name="SheetId">Its <c>sheetId</c>.</param>
/// <param name="Visibility">Whether it is shown.</param>
/// <param name="Part">The package part that holds it, as <c>xl/worksheets/sheet1.xml</c>, or null for a sheet that is not a
/// worksheet (a chart sheet, a dialog sheet, a macro sheet).</param>
/// <param name="Kind">What kind of sheet it is.</param>
public sealed record XlsxSheetInfo(string Name, int SheetId, XlsxSheetVisibility Visibility, string? Part, XlsxSheetKind Kind = XlsxSheetKind.Worksheet);

/// <summary>Whether a sheet is shown.</summary>
public enum XlsxSheetVisibility
{
    /// <summary>Shown.</summary>
    Visible,

    /// <summary>Hidden, and listed in Excel's Unhide.</summary>
    Hidden,

    /// <summary>Hidden, and not listed in Excel's Unhide.</summary>
    VeryHidden,
}

/// <summary>What kind of sheet a workbook lists.</summary>
public enum XlsxSheetKind
{
    /// <summary>A worksheet of cells.</summary>
    Worksheet,

    /// <summary>A chart sheet, or any other sheet that holds no cells.</summary>
    Other,
}

/// <summary>A defined name, as the workbook part writes it.</summary>
/// <param name="Name">The name, as <c>Rate</c>, or a built-in one such as <c>_xlnm.Print_Area</c>.</param>
/// <param name="Text">What it refers to, as a Formula's text.</param>
/// <param name="LocalSheet">The index of the sheet it is local to, or null for a name of the whole workbook.</param>
/// <param name="Hidden">Whether it is hidden.</param>
public sealed record XlsxDefinedName(string Name, string Text, int? LocalSheet = null, bool Hidden = false);
