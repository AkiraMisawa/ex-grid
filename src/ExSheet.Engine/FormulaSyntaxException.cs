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

    /// <summary>
    /// Whether the Formula is one Excel reads, using syntax the engine does not implement (the
    /// intersection of two References): typed without <c>=</c>, such text is refused by name
    /// rather than kept as text, since Excel makes it a Formula (ADR-0047, third run).
    /// </summary>
    internal bool IsUnimplemented { get; init; }

    /// <summary>
    /// Whether the Formula is refused for its size alone: functions nested past Excel's 64 levels, a
    /// Formula nested past the engine's 256 levels, or one the host's stack cannot hold (ADR-0047).
    /// Such text is still a Formula: typed without <c>=</c> after a sign, it is refused by name, not
    /// kept as text, as <see cref="IsUnimplemented"/> is.
    /// </summary>
    internal bool IsBeyondLimit { get; init; }
}
