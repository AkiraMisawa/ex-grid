namespace ExGrid.Cells;

/// <summary>
/// The meaning a Column's rule attaches to a value — a gain, a loss — for the theme to
/// paint (ADR-0006). It is the value-derived half of "why a cell looks different":
/// decided by looking at the value, declared by the Consumer on the column, and
/// distinct from a <see cref="CellState"/>, which cannot be derived from the value and
/// is asked for from outside.
///
/// <para>A closed vocabulary painted as interned classes, like Cell State and alignment
/// (ADR-0029): a tone names a meaning, never a colour. Which colour it is belongs to the
/// theme's tokens, and the bare grid paints none — a Consumer that declares a rule and
/// sets no token sees an ordinary cell, which is Excel's default too.</para>
///
/// <para><see cref="None"/> is the default and adds nothing to the cell, so a column
/// without a rule pays nothing and a rule that mostly answers None costs one call.</para>
/// </summary>
public enum CellTone
{
    /// <summary>Nothing to say about the value. Painted as an ordinary cell.</summary>
    None = 0,

    /// <summary>A gain, an increase, a value above its reference.</summary>
    Positive,

    /// <summary>A loss, a decrease, a value below its reference. Excel's <c>[Red]</c>
    /// section, minus the colour.</summary>
    Negative,
}
