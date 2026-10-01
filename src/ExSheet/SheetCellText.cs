using ExGrid.Columns;
using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// What one cell hands ExGrid as its value: the engine's formatted text, whether it is a number,
/// its alignment, and the colour its Number Format paints it in. ExGrid's column <c>Format</c> is
/// handed only the value, so the value carries the text the engine formatted, and the per-cell kind
/// (ADR-0050, item 6), alignment (item 7) and appearance (item 15) are read from the same object.
/// Immutable: a row's cells are the row's, and a new row instance carries new ones.
/// </summary>
/// <remarks>
/// ExGrid puts a copy's <c>text/html</c> flavour together from each value's invariant form
/// (ADR-0005), which it asks of an <see cref="IFormattable"/>. That form is <see cref="Raw"/>:
/// the unformatted Value as the engine writes it for its own copy (<see cref="SheetCopy.Html"/>),
/// never the text the cell paints.
/// </remarks>
internal sealed record SheetCellText(string Text, bool IsNumber, string Raw, CellAlign Align, NumberFormatColour? Colour = null) : IFormattable
{
    /// <summary>
    /// What a number shows when it cannot be shown in its format at any width: a run of
    /// <c>#</c> no column is wide enough for, which ExGrid's rule for a number that does not fit
    /// turns into the <c>####</c> that fills the cell (ADR-0016). Excel shows the same for a
    /// negative date.
    /// </summary>
    internal static readonly string Unshowable = new('#', 256);

    /// <summary>
    /// The cell's text from the engine's display, Value and alignment setting, or null for a
    /// blank cell. A <c>####</c> keeps its section's colour, as Excel's does (ADR-0063).
    /// </summary>
    internal static SheetCellText? From(CellDisplay display, Value? value, HorizontalAlignment setting)
    {
        var raw = value?.ToString() ?? "";
        var align = AlignOf(display, setting);
        if (display.CannotShow) return new SheetCellText(Unshowable, IsNumber: true, raw, align, display.Colour);
        if (display.Text.Length == 0) return null;
        return new SheetCellText(display.Text, display.IsNumber, raw, align, display.Colour);
    }

    /// <summary>
    /// The cell's alignment as the grid takes it (ADR-0050, item 7): a user's own setting as it
    /// is; General as <see cref="CellAlign.Auto"/>, so the cell's kind decides — numbers right,
    /// text left, as Excel's General does — except where the engine resolves General to the
    /// centre, which is Excel's place for booleans and Error Values and one no kind gives.
    /// </summary>
    internal static CellAlign AlignOf(CellDisplay display, HorizontalAlignment setting) => setting switch
    {
        HorizontalAlignment.Left => CellAlign.Left,
        HorizontalAlignment.Center => CellAlign.Center,
        HorizontalAlignment.Right => CellAlign.Right,
        _ => display.Alignment == HorizontalAlignment.Center ? CellAlign.Center : CellAlign.Auto,
    };

    /// <summary>The unformatted Value, invariant: what the <c>text/html</c> flavour carries (ADR-0005).</summary>
    public string ToString(string? format, IFormatProvider? formatProvider) => Raw;

    /// <summary>The unformatted Value, invariant.</summary>
    public override string ToString() => Raw;
}
