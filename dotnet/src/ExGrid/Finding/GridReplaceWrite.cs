namespace ExGrid.Finding;

/// <summary>
/// The Consumer's write of one replaced cell, for <see cref="GridReplace.Plan{TRow}"/> and
/// <see cref="InMemoryGridSource{TRow}.Replace"/> (ADR-0332 §3): a new row instance holding the new
/// value — identity is the change signal (ADR-0003) — or a refusal in the Consumer's words, as an
/// Edit Verdict's Reject is (ADR-0034).
/// </summary>
public readonly record struct GridReplaceWrite<TRow>
{
    private GridReplaceWrite(TRow? row, string? refusal)
    {
        Row = row;
        Refusal = refusal;
    }

    /// <summary>The cell is written: <paramref name="row"/> is a new instance carrying it.</summary>
    public static GridReplaceWrite<TRow> To(TRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new(row, null);
    }

    /// <summary>The cell cannot hold the replaced text — an unparseable number, a value a rule
    /// rejects — and the whole Replace All is refused, naming it with <paramref name="message"/>.</summary>
    public static GridReplaceWrite<TRow> Refuse(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new(default, message);
    }

    /// <summary>The new row, or default when refused.</summary>
    public TRow? Row { get; }

    /// <summary>Why the cell cannot be written, or null when it is.</summary>
    public string? Refusal { get; }

    /// <summary>Whether the cell is refused.</summary>
    public bool IsRefused => Refusal is not null;
}

/// <summary>One row a Replace All rewrote: the instance it read, and the new one holding every
/// replaced cell of that row (ADR-0332 §3).</summary>
/// <param name="Before">The row as it was read.</param>
/// <param name="After">The new instance.</param>
public readonly record struct GridReplacedRow<TRow>(TRow Before, TRow After);

/// <summary>
/// The reference Replace All's answer (ADR-0332 §3): the <see cref="Result"/> the grid is handed, and
/// the rows it rewrote — empty when refused — which the Consumer keeps as one step of its undo stack.
/// </summary>
public sealed class GridReplaceAnswer<TRow>
{
    internal GridReplaceAnswer(GridReplaceResult result, IReadOnlyList<GridReplacedRow<TRow>> changes)
    {
        Result = result;
        Changes = changes;
    }

    /// <summary>The answer the grid is handed.</summary>
    public GridReplaceResult Result { get; }

    /// <summary>The rows rewritten, each once, in the order read; empty when nothing was replaced.</summary>
    public IReadOnlyList<GridReplacedRow<TRow>> Changes { get; }
}
