namespace ExSheet.Engine;

/// <summary>A cell's horizontal alignment, settable per cell as in Excel (ADR-0046).</summary>
public enum HorizontalAlignment
{
    /// <summary>
    /// Excel's default: numbers (dates included) to the right, text to the left, booleans and
    /// Error Values centred.
    /// </summary>
    General,

    /// <summary>Left.</summary>
    Left,

    /// <summary>Centred.</summary>
    Center,

    /// <summary>Right.</summary>
    Right,
}

/// <summary>
/// What a cell shows: its Value formatted by the cell's number format under the Sheet's culture,
/// and where it sits. Whether it fits its column is the grid's to decide (ADR-0016): a number that
/// does not fit becomes <c>####</c>, never a shorter number.
/// </summary>
/// <param name="Text">
/// The formatted text; empty for a blank cell, and empty when <paramref name="CannotShow"/> is set.
/// </param>
/// <param name="Alignment">The alignment resolved from the cell's setting: never <see cref="HorizontalAlignment.General"/>.</param>
/// <param name="IsNumber">
/// Whether the Value is a number (a date included). A number too wide for its column is shown as
/// <c>####</c>; text is not (ADR-0016).
/// </param>
/// <param name="CannotShow">
/// The number cannot be shown in its format at any width — a negative date, or a date after
/// 31 December 9999 — or, from <see cref="Sheet.GetDisplay(CellAddress, double)"/>, not in the
/// width asked, and the cell shows <c>####</c>, as Excel's does.
/// </param>
/// <param name="Colour">
/// The colour the Number Format names for the section that showed the Value (<c>[Red]</c> and the
/// seven others), kept where the number cannot be shown; <see langword="null"/> where that section
/// names none, or where no section showed it — General, booleans, Error Values, text in a format
/// with no text section, and a blank cell (ADR-0071, SH-40). <b>It takes precedence over the
/// cell's Font colour</b>, as Excel's does (a reading until the eleventh Windows run, case 2): the
/// text is painted in <see cref="NumberFormatColours.Rgb"/> of it, and in the Font colour only
/// where this is <see langword="null"/>.
/// </param>
public readonly record struct CellDisplay(string Text, HorizontalAlignment Alignment, bool IsNumber, bool CannotShow, NumberFormatColour? Colour = null);
