namespace ExSheet;

/// <summary>
/// Excel's keys for the Sheet Tabs, as ExSheet declares them to its grid (ADR-0331 §4): Ctrl+PageUp
/// shows the Sheet before the one shown, Ctrl+PageDown the one after it, and neither goes past the
/// first or the last, as in Excel. Shift+F11 adds a Sheet before the one shown, as Excel's does
/// (exsheet-workbook ticket 06). Declared, they are claimed from the browser, whose own meaning for
/// them moves among its tabs.
/// </summary>
internal static class SheetTabKeys
{
    internal const string Previous = "Control+PageUp";
    internal const string Next = "Control+PageDown";
    internal const string Insert = "Shift+F11";

    /// <summary>The keys ExSheet declares to its grid for the Sheet Tabs, in the grid's canonical form.</summary>
    internal static IReadOnlyCollection<string> Declared { get; } = [Previous, Next, Insert];

    /// <summary>How far the key moves among the Sheets: -1, 1, or null for a key that is not one of these.</summary>
    internal static int? StepOf(string key) => key switch
    {
        Previous => -1,
        Next => 1,
        _ => null,
    };
}
