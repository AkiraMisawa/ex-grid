namespace ExSheet.Engine;

/// <summary>
/// A Formula that cannot be read. Excel refuses to accept such a Formula into a cell, and so does
/// a Sheet: nothing is recorded and no Value changes.
/// </summary>
public sealed class FormulaSyntaxException : FormatException
{
    /// <summary>Creates the refusal.</summary>
    /// <param name="formula">The Formula text as given.</param>
    /// <param name="position">The zero-based position in <paramref name="formula"/> where reading stopped.</param>
    /// <param name="reason">Why, in words.</param>
    public FormulaSyntaxException(string formula, int position, string reason)
        : base($"The Formula '{formula}' cannot be read at position {position}: {reason}")
    {
        Formula = formula;
        Position = position;
        Reason = reason;
    }

    /// <summary>The Formula text as given.</summary>
    public string Formula { get; }

    /// <summary>The zero-based position in <see cref="Formula"/> where reading stopped.</summary>
    public int Position { get; }

    /// <summary>Why, in words.</summary>
    public string Reason { get; }
}
