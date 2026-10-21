namespace ExGrid.Keys;

/// <summary>
/// The keys the core claims while a cell is being edited, one set for each editing mode
/// (ADR-0010, ADR-0051, ADR-0058). Like <see cref="GridKeys.TakenFor"/> they are built here and
/// handed to the listener at attach; the listener looks a key up in the set for the mode it is
/// told or reads off the edit's surfaces, and names none of them itself (KB-2).
/// </summary>
internal static class GridEditingKeys
{
    /// <summary>
    /// Claimed in every mode: commit, cancel, Tab's moves and F2's switch. Ctrl+F too, taken and
    /// answered with nothing: Find is disabled while a cell is being edited, and the browser's own
    /// find would search only the painted rows (ADR-0055). Ctrl+H likewise: Replace is Find's panel, and
    /// the browser's key is its history (ADR-0332 §2).
    /// </summary>
    public static IReadOnlyList<string> Editing { get; } =
    [
        "Escape", "Enter", "Shift+Enter", "Control+Enter", "Tab", "Shift+Tab", "F2",
        "Control+f", "Control+F", "Control+h", "Control+H",
    ];

    /// <summary>
    /// Pointing (ADR-0051's second round): the arrows, Home and End, the four Shift+arrows and
    /// Shift+Home, which move or extend the outline rather than select text in the input (Shift+Home
    /// observed in Excel: the readings of 2026-10-12, R1 (viii)), and the Primary Modifier's arrows
    /// with Shift or without, which move or extend it to the data edge rather than move the caret by
    /// a word (observed in Excel, run 18a: =, Ctrl+↓ and =, Ctrl+Shift+↓).
    /// </summary>
    public static IReadOnlyList<string> Point { get; } =
    [
        .. Editing,
        "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Home", "End",
        "Shift+ArrowUp", "Shift+ArrowDown", "Shift+ArrowLeft", "Shift+ArrowRight", "Shift+Home",
        "Control+ArrowUp", "Control+ArrowDown", "Control+ArrowLeft", "Control+ArrowRight",
        "Control+Shift+ArrowUp", "Control+Shift+ArrowDown", "Control+Shift+ArrowLeft", "Control+Shift+ArrowRight",
    ];

    /// <summary>
    /// Overwrite, Excel's Enter mode (ADR-0010, ED-2): Point's keys, which commit and move or extend
    /// the Selection — or point, where a Reference can go — rather than move the caret or select text
    /// in the input; the Primary Modifier's arrows, with Shift or without, commit and move or extend
    /// to the edge. Observed in Excel: Shift+Home, Shift+→, Shift+↓, Shift+←, Ctrl+→ and Ctrl+Shift+→
    /// (the seventeenth Windows run, Part C, case 7; R1 (vii)), and Shift+↑, Ctrl+←, Ctrl+↑, Ctrl+↓,
    /// Ctrl+Shift+←, Ctrl+Shift+↑ and Ctrl+Shift+↓ (run 18a). End's commit and move is the grid's, not
    /// Excel's: in Enter mode Excel's End turns on an End Mode (ADR-0058), which the grid does not
    /// have. Shift+End stays the editor's (decided with the user, 2026-10-13): Excel's commits and
    /// turns on an End Mode (R1 (vii)), which the grid does not have (ADR-0058, Q53).
    /// </summary>
    public static IReadOnlyList<string> Overwrite { get; } = Point;

    /// <summary>
    /// Pointing from outside (ADR-0058, "The keyboard"): while what Point wrote was written for a
    /// press on another grid, and the Consumer that wrote it hears the arrows, Point's keys, the
    /// Primary Modifier's arrows among them, which the Consumer refuses rather than let them move
    /// the caret.
    /// </summary>
    public static IReadOnlyList<string> Pointed { get; } = Point;

    /// <summary>While a completion list is open, only its ↑ and ↓ beside the editing keys (Tab,
    /// Escape): ←, →, Home, End and the Shift+arrows are the editor's (ADR-0051, ADR-0058: the tenth
    /// Windows run, and Part B of the ninth).</summary>
    public static IReadOnlyList<string> Completion { get; } = [.. Editing, "ArrowUp", "ArrowDown"];

    /// <summary>A list open over Point (ADR-0058, Q51): ←, →, Home, End and the four Shift+arrows
    /// beside the list's own keys, to point, as they do without the list; nothing else.</summary>
    public static IReadOnlyList<string> CompletionOverPoint { get; } =
    [
        .. Completion,
        "ArrowLeft", "ArrowRight", "Home", "End",
        "Shift+ArrowUp", "Shift+ArrowDown", "Shift+ArrowLeft", "Shift+ArrowRight",
    ];

    /// <summary>
    /// What the listener is handed at attach: each set under the name of the mode the core tells
    /// it (<c>setEditing</c>) or marks on the edit's surfaces, and <c>editing</c> for every other
    /// mode, Caret's among them. A mode change is a different set (ADR-0010).
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ByMode { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["editing"] = Editing,
            ["overwrite"] = Overwrite,
            ["point"] = Point,
            ["pointed"] = Pointed,
            ["completion"] = Completion,
            ["completionOverPoint"] = CompletionOverPoint,
        };
}
