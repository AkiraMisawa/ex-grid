namespace ExGrid.Columns;

/// <summary>
/// What the cell paints (ADR-0016). When <see cref="IsHashed"/>, <see cref="DisplayText"/>
/// is <c>#</c> repeated to fill the cell — presentation only: selection, copy, editing
/// and screen readers all deal with the real value.
/// </summary>
public readonly record struct OverflowDecision
{
    private OverflowDecision(bool isHashed, string displayText)
    {
        IsHashed = isHashed;
        DisplayText = displayText;
    }

    internal static OverflowDecision ShowValue(string formattedText) => new(false, formattedText);

    internal static OverflowDecision Hashes(string hashes) => new(true, hashes);

    /// <summary>Whether the value does not fit and the cell paints <c>####</c> instead —
    /// Number and Date only.</summary>
    public bool IsHashed { get; }

    /// <summary>What the cell paints: the formatted value, or a run of <c>#</c> filling
    /// the cell when <see cref="IsHashed"/>.</summary>
    public string DisplayText { get; }
}
