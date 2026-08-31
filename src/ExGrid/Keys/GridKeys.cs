using ExGrid.Selection;

namespace ExGrid.Keys;

/// <summary>
/// Which keys the core takes, and what each one means (ADR-0010: the core arbitrates the
/// keys; Chrome is not trusted with it). Pure, so the whole table is testable without a
/// browser — which matters here more than usual, because the other half of this seam
/// lives in JavaScript.
///
/// <para><b>The same table is read twice.</b> <see cref="Taken"/> is handed to the JS
/// listener at attach time so it can <c>preventDefault</c> <em>synchronously</em> — the
/// interop call is asynchronous, and by the time an answer came back the event would be
/// over. <see cref="Resolve"/> then decides what the key means, from the raw fields the
/// listener forwards. <b>This side is the authority</b>: if the two ever disagree, a key
/// is taken and nothing happens, which is visible — rather than a key meaning something
/// different in the two places, which is not.</para>
///
/// <para>The canonical form is <c>[Control+][Shift+][Alt+]{key}</c>, with Meta folded into
/// Control because the gesture a Mac user makes for "add a range" is Cmd (ADR-0012) and
/// both platforms are Chromium (ADR-0017). <b>The mirror of this rule is in
/// <c>ex-grid.js</c></b> — the two must move together.</para>
/// </summary>
public static class GridKeys
{
    /// <summary>
    /// The keys the core takes. Handed to the JS listener, which takes exactly these and
    /// lets everything else through — a grid that swallowed Ctrl+F or Cmd+R would be
    /// taking the browser's keys, not its own.
    ///
    /// <para>Not here, deliberately: Ctrl+C / Ctrl+V (the clipboard is not wired yet,
    /// ADR-0005), F2 and printable characters (no editor yet, ADR-0010), PageUp /
    /// PageDown (no ADR specifies them, and inventing the behaviour here is what the
    /// project's rules refuse).</para>
    /// </summary>
    // Declared before Taken on purpose: static field initialisers run in textual order,
    // and a Taken built above this line would read a null table.
    private static readonly Dictionary<string, GridKeyAction> Table = BuildTable();

    public static IReadOnlyList<string> Taken { get; } = [.. Table.Keys];

    /// <summary>
    /// What a key means, from the raw event fields. Anything not in the table resolves to
    /// <see cref="GridKeyKind.None"/> — including keys nobody has heard of, because the
    /// browser is free to send <c>Unidentified</c>, a dead key, or an IME's own.
    /// </summary>
    public static GridKeyAction Resolve(string? key, bool ctrl, bool shift, bool alt, bool meta)
        => key is null ? GridKeyAction.None : Resolve(Canonical(key, ctrl, shift, alt, meta));

    /// <summary>The canonical form of one key press. Its mirror is in <c>ex-grid.js</c>.</summary>
    public static string Canonical(string key, bool ctrl, bool shift, bool alt, bool meta)
    {
        // Meta folds into Control before anything else, so Cmd+A and Ctrl+A are one entry
        // in the table rather than two that could drift apart.
        var control = ctrl || meta;
        if (!control && !shift && !alt)
            return key;
        var prefix = (control ? "Control+" : "") + (shift ? "Shift+" : "") + (alt ? "Alt+" : "");
        return prefix + key;
    }

    private static GridKeyAction Resolve(string canonical)
        => Table.TryGetValue(canonical, out var action) ? action : GridKeyAction.None;

    private static Dictionary<string, GridKeyAction> BuildTable()
    {
        var table = new Dictionary<string, GridKeyAction>(StringComparer.Ordinal);

        // Arrows: the four forms of ADR-0012's first table.
        (string Key, GridDirection Direction)[] arrows =
        [
            ("ArrowUp", GridDirection.Up),
            ("ArrowDown", GridDirection.Down),
            ("ArrowLeft", GridDirection.Left),
            ("ArrowRight", GridDirection.Right),
        ];
        foreach (var (key, direction) in arrows)
        {
            table[key] = new(GridKeyKind.Move, direction);
            table["Shift+" + key] = new(GridKeyKind.Extend, direction);
            table["Control+" + key] = new(GridKeyKind.MoveToEdge, direction);
            table["Control+Shift+" + key] = new(GridKeyKind.ExtendToEdge, direction);
        }

        // Home / End. ADR-0012 never mentions them; they are added there as a refinement,
        // reading as Excel's does — the row's first and last cell, and with Control the
        // whole result's first and last.
        table["Home"] = new(GridKeyKind.MoveToEdge, GridDirection.Left);
        table["End"] = new(GridKeyKind.MoveToEdge, GridDirection.Right);
        table["Shift+Home"] = new(GridKeyKind.ExtendToEdge, GridDirection.Left);
        table["Shift+End"] = new(GridKeyKind.ExtendToEdge, GridDirection.Right);
        table["Control+Home"] = new(GridKeyKind.MoveToCorner, Backward: true);
        table["Control+End"] = new(GridKeyKind.MoveToCorner);

        // Enter runs down columns, Tab runs across rows; Shift runs both backwards.
        table["Enter"] = new(GridKeyKind.Cycle, Order: CycleOrder.ColumnMajor);
        table["Shift+Enter"] = new(GridKeyKind.Cycle, Order: CycleOrder.ColumnMajor, Backward: true);
        table["Tab"] = new(GridKeyKind.Cycle, Order: CycleOrder.RowMajor);
        table["Shift+Tab"] = new(GridKeyKind.Cycle, Order: CycleOrder.RowMajor, Backward: true);

        table["Control+a"] = new(GridKeyKind.SelectAll);
        // With CapsLock on the browser reports an uppercase key and no Shift, which would
        // otherwise be a Ctrl+A that does nothing. Ctrl+Shift+A is a different key press
        // and canonicalises to "Control+Shift+A", which the core does not claim.
        table["Control+A"] = new(GridKeyKind.SelectAll);
        table["Control+ "] = new(GridKeyKind.SelectWholeColumns);
        table["Shift+ "] = new(GridKeyKind.SelectWholeRows);
        table[" "] = new(GridKeyKind.Engage);

        // The way out of Tab's cycle. ADR-0012 says Enter and Tab never leave the
        // selection, which without an exit would trap the keyboard inside the grid —
        // against ADR-0020's own "the grid is one tab stop".
        table["Escape"] = new(GridKeyKind.Leave);

        return table;
    }
}
