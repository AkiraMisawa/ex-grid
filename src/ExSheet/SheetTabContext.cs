namespace ExSheet;

/// <summary>
/// What a Sheet Tab shows (ADR-0331 §1, §4), for a Chrome that draws its face
/// (<see cref="ISheetChrome.SheetTab"/>). ExSheet keeps the tab itself — the element that takes the
/// keyboard, says it is a tab to assistive technology, and hears the press, the double click, the
/// context menu and the drag — and the Chrome draws what stands inside it, which takes no keyboard and
/// hears nothing.
/// </summary>
/// <param name="Name">The Sheet's name, which the face shows as its only text.</param>
/// <param name="Selected">Whether the Sheet is the one shown.</param>
public sealed record SheetTabContext(string Name, bool Selected);

/// <summary>The commands a Sheet Tab's menu offers (ADR-0331 §4), in the order it offers them.</summary>
public enum SheetTabCommandKind
{
    /// <summary>Edits the Sheet's name on its tab.</summary>
    Rename,

    /// <summary>Deletes the Sheet. Ctrl+Z gives it back (ADR-0331 §3).</summary>
    Delete,

    /// <summary>Moves the Sheet one place towards the first.</summary>
    MoveLeft,

    /// <summary>Moves the Sheet one place towards the last.</summary>
    MoveRight,

    /// <summary>Copies the Sheet, placing the copy just after it, named as Excel names one: <c>Name (2)</c>.</summary>
    Copy,
}

/// <summary>
/// One command on a Sheet Tab's menu (ADR-0331 §4), as a Chrome draws it
/// (<see cref="ISheetChrome.SheetTabMenu"/>). What it does is ExSheet's: the Chrome draws it and calls
/// <see cref="Invoke"/> when it is pressed.
/// </summary>
/// <param name="Id">The element id the Chrome gives the command's element, which the menu names as the
/// active command while the keyboard moves among them (<c>aria-activedescendant</c>).</param>
/// <param name="Kind">Which command it is.</param>
/// <param name="Label">What the command is called, as Excel calls it.</param>
/// <param name="Enabled">Whether it can be done: Delete cannot on the Workbook's only Sheet, Move Left on the
/// first, Move Right on the last.</param>
/// <param name="DisabledReason">Why it cannot be done; null when it can.</param>
/// <param name="Active">Whether the keyboard is on this command.</param>
/// <param name="Invoke">Does the command, in its place in the Sheet's Input Order (ADR-0240 section 6),
/// and closes the menu. A command that cannot be done closes nothing and does nothing.</param>
public sealed record SheetTabCommand(
    string Id,
    SheetTabCommandKind Kind,
    string Label,
    bool Enabled,
    string? DisabledReason,
    bool Active,
    Func<Task> Invoke);

/// <summary>
/// A Sheet Tab's menu (ADR-0331 §4), for a Chrome that draws it (<see cref="ISheetChrome.SheetTabMenu"/>).
/// ExSheet keeps the element around it, which holds the keyboard, names the active command and answers
/// the keys — ↑ and ↓, Home and End, Enter or Space, Escape — and the Chrome draws the menu's surface and
/// its commands inside it.
/// </summary>
/// <param name="Sheet">The name of the Sheet the menu acts on.</param>
/// <param name="Commands">The commands, in the order the menu offers them.</param>
public sealed record SheetTabMenuContext(string Sheet, IReadOnlyList<SheetTabCommand> Commands)
{
    /// <inheritdoc />
    public bool Equals(SheetTabMenuContext? other) =>
        other is not null && Sheet == other.Sheet && Commands.SequenceEqual(other.Commands);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Sheet, Commands.Count);
}
