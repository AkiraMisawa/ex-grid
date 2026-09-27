using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// What one cell hands ExGrid as its value: the engine's formatted text and whether it is a
/// number. ExGrid's column <c>Format</c> is handed only the value, so the value carries the text
/// the engine formatted, and the per-cell kind is read from the same object (ADR-0050, item 6).
/// Immutable: a row's cells are the row's, and a new row instance carries new ones.
/// </summary>
/// <remarks>
/// ExGrid puts a copy's <c>text/html</c> flavour together from each value's invariant form
/// (ADR-0005), which it asks of an <see cref="IFormattable"/>. That form is <see cref="Raw"/>:
/// the unformatted Value as the engine writes it for its own copy (<see cref="SheetCopy.Html"/>),
/// never the text the cell paints.
/// </remarks>
internal sealed record SheetCellText(string Text, bool IsNumber, string Raw) : IFormattable
{
    /// <summary>
    /// What a number shows when it cannot be shown in its format at any width: a run of
    /// <c>#</c> no column is wide enough for, which ExGrid's rule for a number that does not fit
    /// turns into the <c>####</c> that fills the cell (ADR-0016). Excel shows the same for a
    /// negative date.
    /// </summary>
    internal static readonly string Unshowable = new('#', 256);

    /// <summary>The cell's text from the engine's display and Value, or null for a blank cell.</summary>
    internal static SheetCellText? From(CellDisplay display, Value? value)
    {
        var raw = value?.ToString() ?? "";
        if (display.CannotShow) return new SheetCellText(Unshowable, IsNumber: true, raw);
        if (display.Text.Length == 0) return null;
        return new SheetCellText(display.Text, display.IsNumber, raw);
    }

    /// <summary>The unformatted Value, invariant: what the <c>text/html</c> flavour carries (ADR-0005).</summary>
    public string ToString(string? format, IFormatProvider? formatProvider) => Raw;

    /// <summary>The unformatted Value, invariant.</summary>
    public override string ToString() => Raw;
}
