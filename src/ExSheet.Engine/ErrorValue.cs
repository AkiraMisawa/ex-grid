using System.Diagnostics.CodeAnalysis;

namespace ExSheet.Engine;

/// <summary>
/// An Error Value: Excel's set, <c>#GETTING_DATA</c> with the stricter meaning ADR-0049 gives it,
/// and <c>#CIRC!</c>, the one Error Value ExSheet adds (ADR-0047). Error Values are data; they
/// propagate through every Formula that uses them.
/// </summary>
public enum ErrorValue
{
    /// <summary><c>#NULL!</c>.</summary>
    Null,

    /// <summary><c>#DIV/0!</c>: a division by zero.</summary>
    Div0,

    /// <summary><c>#VALUE!</c>: an argument of the wrong kind.</summary>
    Value,

    /// <summary><c>#REF!</c>: the cells a Reference named no longer exist.</summary>
    Ref,

    /// <summary><c>#NAME?</c>: a name ExSheet does not know, such as a function outside its declared set.</summary>
    Name,

    /// <summary><c>#NUM!</c>: a number that cannot be represented, or an impossible numeric argument.</summary>
    Num,

    /// <summary><c>#N/A</c>: a value that is not available, such as a lookup that found nothing.</summary>
    NA,

    /// <summary>
    /// <c>#GETTING_DATA</c>: the Formula reads a Linked Table whose data has not arrived. Every
    /// dependent waits too, and <c>IFERROR</c> and <c>ISERROR</c> do not treat it as an error — a
    /// deliberate difference from Excel (ADR-0049).
    /// </summary>
    GettingData,

    /// <summary>
    /// <c>#CIRC!</c>: the cell is part of a circular reference, or depends on one. Excel shows 0
    /// here; ExSheet refuses to (ADR-0047). It is the only Error Value ExSheet adds to Excel's.
    /// </summary>
    Circ,
}

/// <summary>The text of each <see cref="ErrorValue"/>, as a cell shows it and as a Formula writes it.</summary>
public static class ErrorValues
{
    private static readonly string[] Texts =
        ["#NULL!", "#DIV/0!", "#VALUE!", "#REF!", "#NAME?", "#NUM!", "#N/A", "#GETTING_DATA", "#CIRC!"];

    /// <summary>The Error Value as a cell shows it, such as <c>#DIV/0!</c>.</summary>
    public static string ToText(this ErrorValue error) => Texts[(int)error];

    /// <summary>
    /// Reads the text of one of Excel's Error Values, in either case. <c>#GETTING_DATA</c> and
    /// <c>#CIRC!</c> are not read: they describe the state of a computation, and a user cannot
    /// type one in (ADR-0047, ADR-0049). Nor are Excel's newer Error Values (<c>#SPILL!</c>,
    /// <c>#CALC!</c>, <c>#FIELD!</c>, <c>#BLOCKED!</c>, <c>#CONNECT!</c>, <c>#BUSY!</c>,
    /// <c>#UNKNOWN!</c>): the engine has none of them while spilling is out of the first version,
    /// so typed, each stays text (ADR-0047).
    /// </summary>
    public static bool TryParseTyped([NotNullWhen(true)] string? text, out ErrorValue error)
    {
        error = default;
        if (text is null) return false;
        for (var i = 0; i <= (int)ErrorValue.NA; i++)
        {
            if (string.Equals(Texts[i], text, StringComparison.OrdinalIgnoreCase))
            {
                error = (ErrorValue)i;
                return true;
            }
        }
        return false;
    }
}
