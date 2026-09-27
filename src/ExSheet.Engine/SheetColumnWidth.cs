namespace ExSheet.Engine;

/// <summary>
/// A width recorded on a column (ADR-0046): how wide it is, and whether the user set it.
/// </summary>
/// <remarks>
/// A width the user set — a drag, a size to fit, a command — is <b>custom</b>, and stops a column
/// from widening on entry; so is a width a typed entry widened the column to, as Excel's file marks
/// it (ADR-0047, "What the second observation settled", CW-018). An <b>automatic</b> width is left
/// to documents that already hold one (ADR-0046): it is recorded, so the document reopens as the
/// user saw it, and a longer entry still widens the column. Excel keeps the same distinction in
/// its files (<c>customWidth</c>).
/// </remarks>
/// <param name="Width">The width, in characters of the default font (Excel's unit, ADR-0047).</param>
/// <param name="IsCustom">Whether it is custom; <see langword="false"/> for an automatic width, which a longer entry widens.</param>
public readonly record struct SheetColumnWidth(double Width, bool IsCustom);
