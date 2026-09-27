namespace ExSheet.Engine;

public sealed partial class Sheet
{
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
    /// column still at its default width when the number is typed, as Excel does (ADR-0047);
    /// <see langword="null"/> when what the cell holds never widens a column: text, a boolean, an
    /// Error Value, a Formula, a blank, or a number no width can show.
    /// <list type="bullet">
    /// <item>A number in General needs the width at which it shows without scientific notation
    /// and with every integer digit — decimals are rounded to the column instead, as Microsoft
    /// documents General does — or, when General writes it scientific at any width (twelve or
    /// more digits), the width of that scientific form.</item>
    /// <item>A number in any other format, a date typed as a date among them, needs its whole
    /// text.</item>
    /// </list>
    /// The component calls it after the user's entry is done, and widens the column to the answer
    /// when the column is still at its default width and narrower than the answer. How Excel
    /// chooses the new width is observed by the case corpus; this rule is uncertain there.
    /// </summary>
    public int? GetWidthOnEntry(CellAddress address)
    {
        if (!_cells.TryGetValue(address, out var cell) || cell.Entry?.Constant is not { Kind: ValueKind.Number } constant) return null;
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
