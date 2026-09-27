namespace ExSheet.Engine;

/// <summary>
/// A width recorded on a column (ADR-0046): how wide it is, and whether the user set it.
/// </summary>
/// <remarks>
/// A width the user set — a drag, a size to fit, a command — is <b>custom</b>, and stops a column
/// from widening on entry. A width an entry widened the column to is <b>automatic</b>: it is
/// recorded, so the document reopens as the user saw it, and a longer entry widens the column
/// again. Excel keeps the same distinction in its files (<c>customWidth</c>).
/// </remarks>
/// <param name="Width">The width, in characters of the default font (Excel's unit, ADR-0047).</param>
/// <param name="IsCustom">Whether the user set it; <see langword="false"/> for a width an entry widened the column to.</param>
public readonly record struct SheetColumnWidth(double Width, bool IsCustom);
