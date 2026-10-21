namespace ExGrid.Data;

/// <summary>
/// A Board, or the data bound to one of its inputs, that cannot be drawn (ADR-0340, ADR-0341): its
/// message names the path and the reason in one sentence, which a host returns to the AI or the
/// person that wrote it, as a Chart Spec's refusal does (ADR-0320).
/// </summary>
public sealed class BoardRefusedException : Exception
{
    /// <summary>A refusal at <paramref name="path"/>, said by <paramref name="message"/>.</summary>
    public BoardRefusedException(string path, string message)
        : base(message)
    {
        ArgumentNullException.ThrowIfNull(path);
        Path = path;
    }

    /// <summary>Where it was refused, such as <c>inputs.trades.columns[2].type</c> in a Board or
    /// <c>trades[41].notional</c> in an input's data; empty for the whole.</summary>
    public string Path { get; }
}
