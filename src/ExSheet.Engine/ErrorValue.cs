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

    /// <summary><c>#SPILL!</c>, one of Excel's newer Error Values: data only, typed or loaded; nothing in the engine produces it (ADR-0047, second run).</summary>
    Spill,

    /// <summary><c>#CALC!</c>, one of Excel's newer Error Values: data only; nothing in the engine produces it.</summary>
    Calc,

    /// <summary><c>#FIELD!</c>, one of Excel's newer Error Values: data only; nothing in the engine produces it.</summary>
    Field,

    /// <summary><c>#BLOCKED!</c>, one of Excel's newer Error Values: data only; nothing in the engine produces it.</summary>
    Blocked,

    /// <summary><c>#CONNECT!</c>, one of Excel's newer Error Values: data only; nothing in the engine produces it.</summary>
    Connect,

    /// <summary><c>#UNKNOWN!</c>, one of Excel's newer Error Values: data only; nothing in the engine produces it.</summary>
    Unknown,
}

/// <summary>The text of each <see cref="ErrorValue"/>, as a cell shows it and as a Formula writes it.</summary>
public static class ErrorValues
{
    private static readonly string[] Texts =
        ["#NULL!", "#DIV/0!", "#VALUE!", "#REF!", "#NAME?", "#NUM!", "#N/A", "#GETTING_DATA", "#CIRC!",
            "#SPILL!", "#CALC!", "#FIELD!", "#BLOCKED!", "#CONNECT!", "#UNKNOWN!"];

    /// <summary>The Error Value as a cell shows it, such as <c>#DIV/0!</c>.</summary>
    public static string ToText(this ErrorValue error) => Texts[(int)error];

    /// <summary>
    /// Reads the text of one of Excel's Error Values, in either case, as Excel reads one typed with
    /// real keys: <c>#spill!</c> is <c>#SPILL!</c>. <c>#GETTING_DATA</c> and <c>#CIRC!</c> are not
    /// read: they describe the state of a computation, and a user cannot type one in (ADR-0047,
    /// ADR-0049). Excel's newer Error Values (<c>#SPILL!</c>, <c>#CALC!</c>, <c>#FIELD!</c>,
    /// <c>#BLOCKED!</c>, <c>#CONNECT!</c>, <c>#UNKNOWN!</c>) are read, as Excel reads them typed;
    /// they are data only. <c>#BUSY!</c> is not: Excel keeps it as text (ADR-0047, second run).
    /// </summary>
    public static bool TryParseTyped([NotNullWhen(true)] string? text, out ErrorValue error)
    {
        error = default;
        if (text is null) return false;
        for (var i = 0; i < Texts.Length; i++)
        {
            if ((ErrorValue)i is ErrorValue.GettingData or ErrorValue.Circ) continue;
            if (string.Equals(Texts[i], text, StringComparison.OrdinalIgnoreCase))
            {
                error = (ErrorValue)i;
                return true;
            }
        }
        return false;
    }
}
