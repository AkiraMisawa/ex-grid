namespace ExGrid.Chrome;

/// <summary>Why a typed filter operand was refused (ADR-0023; ADR-0006, note of 2026-10-01).</summary>
public enum OperandRefusal
{
    /// <summary>Not refused: the text read as the column's type, or nothing was typed.</summary>
    None,

    /// <summary>The text is not the column's type as the culture writes it: not a number, or a
    /// number written with another culture's separators, or grouped where the culture does not
    /// group. Read in another culture, it could read as a different number, quietly.</summary>
    NotReadable,

    /// <summary>The text reads as two different numbers, and neither is guessed (principle 1):
    /// under a culture that groups thousands with a dot, <c>1.234</c> reads as 1234 there and as
    /// 1.234 where the dot is the decimal point — the invariant reading, and the one the grid's
    /// panels used to take.</summary>
    ReadsTwoWays,
}

/// <summary>
/// What a condition's typed operand reads as (ADR-0023, ticket 96): the value, typed as the
/// column's values are, or why the text was refused. Both Chromes' panels read their text through
/// <see cref="FilterPanelChoices.ReadOperand"/>, so the same text means the same thing, or is
/// refused for the same reason, in either.
/// </summary>
/// <param name="Value">The operand, or null where nothing was typed or the text was refused.</param>
/// <param name="Refusal">Why the text was refused; <see cref="OperandRefusal.None"/> where it was not.</param>
/// <param name="OtherValue">For <see cref="OperandRefusal.ReadsTwoWays"/>, the culture's reading
/// and the other, in that order; null otherwise.</param>
public readonly record struct OperandReading(object? Value, OperandRefusal Refusal, (object CultureReading, object OtherReading)? OtherValue = null)
{
    /// <summary>Whether the text was refused, and the panel must say why rather than apply.</summary>
    public bool IsRefused => Refusal != OperandRefusal.None;
}
