namespace ExSheet.Engine;

/// <summary>
/// What recorded a column's width (ADR-0046, 2026-09-28; SH-26). A column at the default width
/// records none, and has no kind.
/// </summary>
public enum SheetColumnWidthKind
{
    /// <summary>
    /// Set by an entry that widened the column (<see cref="Sheet.SetAutomaticColumnWidth"/>): a
    /// longer entry widens it again, as Excel was observed to widen it (CW-028, third run).
    /// </summary>
    WidenedByEntry,

    /// <summary>
    /// Set by the user — a drag, a size to fit, a command (<see cref="Sheet.SetColumnWidth"/>): an
    /// entry never widens it.
    /// </summary>
    SetByUser,
}

/// <summary>
/// A width recorded on a column (ADR-0046): how wide it is, and what recorded it.
/// </summary>
/// <remarks>
/// A recorded width is one of two kinds (<see cref="SheetColumnWidthKind"/>); a third, the default
/// width, is not recorded. Both kinds are marked custom, as Excel's file marks a column an entry
/// widened as well as one the user set (<c>customWidth</c>; CW-018, CW-027..029). Only a width the
/// user set stops a column from widening on entry; a width set by the user replaces one widened by
/// entry, and an entry never turns the user's back into the other kind (ADR-0046, 2026-09-28).
/// </remarks>
/// <param name="Width">The width, in characters of the default font (Excel's unit, ADR-0047).</param>
/// <param name="Kind">What recorded it: an entry that widened the column, or the user.</param>
public readonly record struct SheetColumnWidth(double Width, SheetColumnWidthKind Kind)
{
    /// <summary>
    /// Whether Excel's file marks the width custom (<c>customWidth</c>): every recorded width is,
    /// whichever its kind (ADR-0046, 2026-09-28). Whether an entry may widen the column is
    /// <see cref="IsSetByUser"/>.
    /// </summary>
    public bool IsCustom => true;

    /// <summary>Whether the user set the width, so an entry never widens the column (<see cref="SheetColumnWidthKind.SetByUser"/>).</summary>
    public bool IsSetByUser => Kind == SheetColumnWidthKind.SetByUser;
}
