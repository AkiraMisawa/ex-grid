using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

public sealed partial class Sheet
{
    /// <summary>
    /// The widest a column can be, in characters: 255, as Microsoft documents Excel's limit on a
    /// column's width.
    /// </summary>
    public const double MaxColumnWidth = 255;

    /// <summary>
    /// The widths recorded on columns, in characters, and whether the user set each, by column; a
    /// column absent is at the default width and records nothing (ADR-0046).
    /// </summary>
    private Dictionary<int, SheetColumnWidth> _columnWidths = [];

    /// <summary>
    /// The width recorded on the column, in characters (Excel's unit, ADR-0047), and whether the
    /// user set it (<see cref="SheetColumnWidth.IsCustom"/>), or <see langword="null"/> when none is:
    /// the column is at the default width, which the component chooses and the Sheet Document does
    /// not record (ADR-0046).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The column is outside <c>A</c> to <c>XFD</c>.</exception>
    public SheetColumnWidth? GetColumnWidth(int column) => _columnWidths.TryGetValue(CheckColumn(column), out var width) ? width : null;

    /// <summary>
    /// Sets the width of every column <paramref name="columns"/> spans, in characters, as Excel's
    /// <c>Range.ColumnWidth</c> sets the columns of a range, as a width the user set: custom, so an
    /// entry never widens the columns (ADR-0046). <see langword="null"/> puts them back at the
    /// default width, recording nothing. No Value changes; the change names the columns whose
    /// width changed (<see cref="SheetChange.Columns"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The width is not more than 0 and at most <see cref="MaxColumnWidth"/>. A width of 0 is how
    /// Excel hides a column, and hiding columns is not part of the first version (ADR-0046).
    /// </exception>
    public SheetChange SetColumnWidth(CellRange columns, double? width) =>
        ApplyColumnWidth(columns, CheckColumnWidth(width) is { } w ? new SheetColumnWidth(w, IsCustom: true) : null).Change;

    /// <summary>
    /// Sets the width of every column <paramref name="columns"/> spans, in characters, as a width
    /// an entry widened them to: automatic, so a longer entry widens them again (ADR-0046, ADR-0047
    /// second round). It is recorded like any other width, so the document reopens as the user saw
    /// it. The change names the columns whose width, or whose origin, changed.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The width is not more than 0 and at most <see cref="MaxColumnWidth"/>.</exception>
    public SheetChange SetAutomaticColumnWidth(CellRange columns, double width) =>
        ApplyColumnWidth(columns, new SheetColumnWidth(CheckColumnWidth(width)!.Value, IsCustom: false)).Change;

    internal static double? CheckColumnWidth(double? width, string name = "width") =>
        width is null or (> 0 and <= MaxColumnWidth)
            ? width
            : throw new ArgumentOutOfRangeException(name, width, $"A column's width is more than 0 and at most {MaxColumnWidth} characters; a hidden column (width 0) is not supported.");

    /// <summary>A width set on columns, and the widths before it for undoing it.</summary>
    internal (SheetChange Change, Dictionary<int, SheetColumnWidth> Before) ApplyColumnWidth(CellRange columns, SheetColumnWidth? width)
    {
        var before = new Dictionary<int, SheetColumnWidth>(_columnWidths);
        for (var column = columns.First.Column; column <= columns.Last.Column; column++)
        {
            if (width is { } w) _columnWidths[column] = w;
            else _columnWidths.Remove(column);
        }
        return (new SheetChange([], [], [], WidthsChangedSince(before)), before);
    }

    /// <summary>Puts every column's width back to <paramref name="widths"/>, and says which columns that changed.</summary>
    internal SheetChange RestoreColumnWidths(Dictionary<int, SheetColumnWidth> widths)
    {
        var now = _columnWidths;
        _columnWidths = new Dictionary<int, SheetColumnWidth>(widths);
        var columns = WidthsChangedSince(now);
        return columns.Count == 0 ? SheetChange.None : new SheetChange([], [], [], columns);
    }

    /// <summary>A copy of the widths recorded now.</summary>
    internal Dictionary<int, SheetColumnWidth> ColumnWidthsNow() => new(_columnWidths);

    /// <summary>The columns whose width or its origin differs between <paramref name="before"/> and now, ascending.</summary>
    private List<int> WidthsChangedSince(Dictionary<int, SheetColumnWidth> before) =>
        [.. before.Keys.Union(_columnWidths.Keys)
            .Where(c => !(before.TryGetValue(c, out var was) && _columnWidths.TryGetValue(c, out var now) && was == now))
            .Order()];

    /// <summary>
    /// The widths after an insertion or deletion of columns: moved with their columns, those
    /// deleted or pushed off the right edge dropped, and inserted columns given the width of the
    /// column to their left, automatic or custom as it is, when <paramref name="formatInserted"/>,
    /// as they take its formats (ADR-0046).
    /// </summary>
    private void ShiftColumnWidths(StructuralEdit edit, bool formatInserted)
    {
        if (edit.Axis != SheetAxis.Columns) return;
        var shifted = new Dictionary<int, SheetColumnWidth>();
        foreach (var (column, width) in _columnWidths)
        {
            if (edit.Move(new CellAddress(0, column)) is { } moved) shifted[moved.Column] = width;
        }
        if (edit.IsInsert && formatInserted && edit.Start > 0 && _columnWidths.TryGetValue(edit.Start - 1, out var left))
        {
            for (var i = 0; i < edit.Count; i++) shifted[edit.Start + i] = left;
        }
        _columnWidths = shifted;
    }

    /// <summary>The widths as a Sheet Document records them: adjacent columns of one width and one origin are one run.</summary>
    private IReadOnlyList<SheetDocumentColumnWidth> WidthRuns()
    {
        var runs = new List<SheetDocumentColumnWidth>();
        foreach (var column in _columnWidths.Keys.Order())
        {
            var width = _columnWidths[column];
            if (runs.Count > 0 && runs[^1] is var last && last.Last == column - 1 && last.Width == width.Width && last.IsCustom == width.IsCustom) runs[^1] = last with { Last = column };
            else runs.Add(new SheetDocumentColumnWidth(column, column, width.Width, width.IsCustom));
        }
        return runs;
    }

    /// <summary>
    /// Excel's default column width, in characters: 8.43 (ADR-0047). Microsoft documents column
    /// width as "the number of characters of the default font that fit in a cell", and 8.43 as the
    /// default.
    /// </summary>
    public const double DefaultColumnWidth = 8.43;

    /// <summary>
    /// What the cell shows in a column <paramref name="width"/> characters wide (ADR-0047,
    /// ADR-0016). The width is in Excel's unit, the number of digits of the font that fit
    /// between the cell's paddings — Excel's default column is <see cref="DefaultColumnWidth"/> —
    /// and every character of a number's text is charged one digit width, so a column holds
    /// <c>⌊width⌋</c> characters. A component converts from its resolved pixel width as
    /// <c>(columnPx − 2 × cellPaddingPx) / digitWidthPx</c>, the inverse of how ExSheet sizes its
    /// default column.
    /// <list type="bullet">
    /// <item>A number in General is fitted as Excel's General fits it: decimals rounded to the
    /// width, scientific notation where the integer part does not fit or has twelve or more
    /// digits, and never more than eleven characters besides a minus sign. Where not even the
    /// shortest scientific form fits, the cell cannot be shown (<see cref="CellDisplay.CannotShow"/>).</item>
    /// <item>A number in any other format whose text is longer than the width cannot be shown,
    /// as Excel's <c>####</c> (ADR-0016).</item>
    /// <item>Text, booleans and Error Values are as <see cref="GetDisplay(CellAddress)"/> gives
    /// them.</item>
    /// </list>
    /// Only the text depends on the width; the Value is untouched.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The width is negative or not finite.</exception>
    public CellDisplay GetDisplay(CellAddress address, double width)
    {
        var characters = Characters(width);
        if (!_cells.TryGetValue(address, out var cell) || cell.Value is not { } value)
        {
            return new CellDisplay("", Resolve(GetAlignment(address), null), false, false);
        }
        var (text, cannotShow) = GetFormat(address).Format(value, Culture, characters);
        return new CellDisplay(cannotShow ? "" : text, Resolve(GetAlignment(address), value.Kind), value.Kind == ValueKind.Number, cannotShow);
    }

    /// <summary>
    /// The width, in characters, that the number typed into this cell needs, for widening a
    /// column whose width the user has not set when the number is typed, as Excel does (ADR-0047);
    /// a Formula whose Value is a number needs its Value's width, as Excel was observed to widen a
    /// column for one (WD-007). <see langword="null"/> when what the cell holds never widens a
    /// column: text, a boolean, an Error Value, a Formula whose Value is not a number, a blank,
    /// or a number no width can show.
    /// <list type="bullet">
    /// <item>A number in General needs the width at which it shows without scientific notation
    /// and with every integer digit — decimals are rounded to the column instead, as Microsoft
    /// documents General does — or, when General writes it scientific at any width (twelve or
    /// more digits), the width of that scientific form.</item>
    /// <item>A number in any other format, a date typed as a date among them, needs its whole
    /// text.</item>
    /// </list>
    /// The component calls it after the user's entry is done, and widens the column to the answer
    /// when the column is at its default width or an automatic one (<see cref="SetAutomaticColumnWidth"/>),
    /// never a custom one, and narrower than the answer (ADR-0046). How Excel
    /// chooses the new width is observed by the case corpus; this rule is uncertain there.
    /// </summary>
    public int? GetWidthOnEntry(CellAddress address)
    {
        if (!_cells.TryGetValue(address, out var cell) || cell.Entry is not { } entry) return null;
        // A Formula's number widens the column as a typed one does, as Excel was observed to
        // widen it for =123456789*10 (WD-007).
        var shown = entry.IsFormula ? cell.Value : entry.Constant;
        if (shown is not { Kind: ValueKind.Number } constant) return null;
        var number = constant.Number;
        var format = GetFormat(address);
        if (!format.ShowsNumbersAsGeneral)
        {
            var (text, cannotShow) = format.Format(constant, Culture);
            return cannotShow ? null : text.Length;
        }
        var widest = NumberText.GeneralLimit + 1;
        if (NumberText.IsScientific(number, widest) == true) return NumberText.General(number, widest, Culture)!.Length;
        for (var characters = 1; characters <= widest; characters++)
        {
            if (NumberText.IsScientific(number, characters) == false) return characters;
        }
        return null;
    }

    private static int Characters(double width)
    {
        if (!double.IsFinite(width) || width < 0) throw new ArgumentOutOfRangeException(nameof(width), width, "A column width is a finite number of characters, zero or more.");
        // A width typed as 8 may arrive as 7.9999999 after a round trip through pixels.
        return (int)Math.Min(int.MaxValue, Math.Floor(width + 1e-9));
    }
}
