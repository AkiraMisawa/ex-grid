namespace ExSheet;

/// <summary>
/// A change to the Sheets of the Workbook an ExSheet shows (ADR-0331 §6): a Sheet added, deleted,
/// renamed, moved or copied, by a command, by the user, or by an undo or redo of one. It names the
/// Sheets in order before and after the change, so the application hears what happened without
/// keeping its own copy.
/// </summary>
public sealed class SheetsChange
{
    internal SheetsChange(IReadOnlyList<string> before, IReadOnlyList<string> after, string shown)
    {
        Before = before;
        After = after;
        Shown = shown;
    }

    /// <summary>The Sheets' names in order, before the change.</summary>
    public IReadOnlyList<string> Before { get; }

    /// <summary>The Sheets' names in order, after the change.</summary>
    public IReadOnlyList<string> After { get; }

    /// <summary>The name of the Sheet shown after the change.</summary>
    public string Shown { get; }
}
