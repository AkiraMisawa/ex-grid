using ExGrid.Selection;

namespace ExGrid.Keys;

/// <summary>
/// Which keys the core takes, and what each one means (ADR-0010: the core arbitrates the
/// keys; Chrome is not trusted with it). Pure, so the whole table is testable without a
/// browser — which matters here more than usual, because the other half of this seam
/// lives in JavaScript.
///
/// <para><b>The same table is read twice.</b> <see cref="TakenFor"/> — <see cref="Taken"/> and
/// the keys this grid's claims add — is handed to the JS listener at attach time, and again
/// whenever the claims change, so it can <c>preventDefault</c> <em>synchronously</em> — the
/// interop call is asynchronous, and by the time an answer came back the event would be
/// over. <see cref="Resolve(string, bool, bool, bool, bool, bool)"/> then decides what
/// the key means, from the raw fields the listener forwards. <b>This side is the
/// authority</b>: if the two ever disagree, a key is taken and nothing happens, which is
/// visible — rather than a key meaning something different in the two places, which is
/// not.</para>
///
/// <para>The canonical form is <c>[Control+][Shift+][Alt+]{key}</c>. Control is the
/// <b>primary modifier</b>: always the Control key, and the Meta key as well <b>only where
/// Meta is what a user reaches for</b> — Command on an Apple keyboard (ADR-0012). On
/// Windows and Linux the Meta key belongs to the OS, and folding it there would make
/// Win+Arrow or Super+A move the selection on the days the window manager does not grab
/// them first. <b>The mirror of this rule is in <c>ex-grid.js</c></b> — the two must move
/// together.</para>
/// </summary>
public static class GridKeys
{
    // Declared before Taken on purpose: static field initialisers run in textual order,
    // and a Taken built above this line would read a null table.
    private static readonly Dictionary<string, GridKeyAction> Table = BuildTable();

    // The keys claimed only on some grids, and what decides it. A key in Table but not
    // here is claimed on every grid; a key here is in Table too, so Resolve answers it
    // either way and only the claim is conditional.
    private static readonly Dictionary<string, Func<GridKeyClaims, bool>> Conditions = BuildConditions();

    /// <summary>
    /// The keys the core takes on every grid, in canonical form. The JS listener takes exactly
    /// these and the ones <see cref="TakenFor"/> adds, and lets everything else through — a key
    /// taken for no meaning of the grid's is a key stolen from the browser. Ctrl+F is here
    /// because the browser's own find would be a wrong answer on a virtualised grid (ADR-0055);
    /// Ctrl+R only on a grid that edits, where it is Excel's fill (ADR-0035).
    ///
    /// <para>Not here, deliberately: Ctrl+C / Ctrl+V (the clipboard rides the browser's
    /// own <c>copy</c> and <c>paste</c> events — taking the keys would suppress the very
    /// events that make the route prompt-free, ADR-0005), and F2 and printable
    /// characters, which open the Cell Editor and which the listener takes only on a grid
    /// with an editable column — a display-only grid must not take the page's keys
    /// (ADR-0010). Nor undo and redo: those are taken only while someone listens, through
    /// <see cref="TakenFor"/> (ADR-0007).</para>
    /// </summary>
    public static IReadOnlyList<string> Taken { get; } =
        [.. Table.Keys.Where(key => !Conditions.ContainsKey(key))];

    /// <summary>
    /// The keys one grid takes: <see cref="Taken"/>, plus the keys whose claim depends on
    /// what that grid can do. Delete, Backspace, Ctrl+D and Ctrl+R write, so they are taken
    /// only on a grid with an editable column — a display-only grid leaves the page its own
    /// keys (ADR-0010/0020/0035/0054). Ctrl+Z and the two redo keys are taken only when
    /// someone listens for them: the grid holds no history, and a key taken for nobody is a
    /// key stolen from the page (ADR-0007).
    /// </summary>
    public static IReadOnlyList<string> TakenFor(GridKeyClaims claims)
        => [.. Table.Keys.Where(key => !Conditions.TryGetValue(key, out var when) || when(claims))];

    /// <summary>
    /// What a key means, from the raw event fields. Anything not in the table resolves to
    /// <see cref="GridKeyKind.None"/> — including keys nobody has heard of, because the
    /// browser is free to send <c>Unidentified</c>, a dead key, or an IME's own.
    /// </summary>
    /// <param name="key">The event's <c>key</c> field as the browser reported it. Null
    /// resolves to <see cref="GridKeyAction.None"/>.</param>
    /// <param name="ctrl">Whether the Control key was held.</param>
    /// <param name="shift">Whether the Shift key was held.</param>
    /// <param name="alt">Whether the Alt key was held.</param>
    /// <param name="meta">Whether the Meta key was held — Command on an Apple keyboard,
    /// the OS's own key elsewhere.</param>
    /// <param name="metaIsPrimary">Whether the Meta key is this platform's primary
    /// modifier — true on an Apple platform, where it is Command. Only the browser can
    /// answer it, so it is asked once and passed in.</param>
    public static GridKeyAction Resolve(
        string? key, bool ctrl, bool shift, bool alt, bool meta, bool metaIsPrimary)
        => key is null ? GridKeyAction.None : Resolve(Canonical(key, ctrl, shift, alt, meta, metaIsPrimary));

    /// <summary>The canonical form of one key press. Its mirror is in <c>ex-grid.js</c>.</summary>
    public static string Canonical(string key, bool ctrl, bool shift, bool alt, bool meta, bool metaIsPrimary)
    {
        // The primary modifier folds into Control before anything else, so Cmd+A on a Mac
        // and Ctrl+A everywhere are one entry in the table rather than two that could
        // drift apart. Control is always primary — Ctrl+A works on a Mac too; Meta is
        // primary only where it is Command.
        var control = ctrl || (meta && metaIsPrimary);
        // Held where it is NOT primary, Meta still has to appear in the form, or Win+Down
        // would canonicalise to a bare "ArrowDown" and move the selection under a chord
        // aimed at the window manager. No entry in the table carries it, which is the
        // point: a key held with the OS's own modifier is not the grid's.
        var foreign = meta && !metaIsPrimary;
        if (!control && !foreign && !shift && !alt)
            return key;
        var prefix = (control ? "Control+" : "") + (foreign ? "Meta+" : "")
            + (shift ? "Shift+" : "") + (alt ? "Alt+" : "");
        return prefix + key;
    }

    private static GridKeyAction Resolve(string canonical)
        => Table.TryGetValue(canonical, out var action) ? action : GridKeyAction.None;

    /// <summary>
    /// A Consumer's declared keys, checked (ADR-0050, item 14): each in <see cref="Canonical"/>'s
    /// form, and none a key the core answers itself. The core claims a declared key whether or
    /// not an edit is open, and raises it with whether one is; a key the core already answers
    /// would then mean two things, so a declaration naming one is refused by name, with what the
    /// core does with it (ADR-0010). A key is matched exactly as declared: a letter claims the
    /// case it names, so a Consumer that wants a key under CapsLock too declares both cases, as
    /// this table does for its own letters; and a character the layout may type with or without
    /// Shift is declared in both forms.
    /// </summary>
    /// <param name="keys">The declared keys, in the canonical form; null declares none.</param>
    /// <returns>The keys, as a set; empty when none are declared.</returns>
    /// <exception cref="ArgumentException">A key is not in the canonical form — no key press
    /// would ever match it — or is one the core answers itself.</exception>
    public static IReadOnlySet<string> Declare(IEnumerable<string>? keys)
    {
        if (keys is null)
            return NoneDeclared;
        var declared = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            if (!IsCanonicalForm(key, out var control, out var meta, out var alt, out var name))
            {
                throw new ArgumentException(
                    $"The declared key '{key}' is not in the canonical form [Control+][Meta+][Shift+][Alt+]{{key}}, " +
                    "with the key as KeyboardEvent.key reports it, so no key press would ever match it (ADR-0050, item 14).",
                    nameof(keys));
            }
            if (CoreAnswer(key, control, meta, alt, name) is { } answer)
            {
                throw new ArgumentException(
                    $"The declared key '{key}' is one the core answers itself: {answer}. A declared key never takes a " +
                    "key the core answers, or the key would mean two things (ADR-0050, item 14; ADR-0010).",
                    nameof(keys));
            }
            declared.Add(key);
        }
        return declared.Count == 0 ? NoneDeclared : declared;
    }

    private static readonly IReadOnlySet<string> NoneDeclared = new HashSet<string>(StringComparer.Ordinal);

    // The keys the core answers that are not in Table: two the editor claims while an edit is
    // open (ex-grid.js's editingKeys and the F4 a Consumer's CycleReference adds), and the
    // clipboard's, which the core answers through the browser's own copy and paste events —
    // taken, the key would suppress the very event (ADR-0005).
    private static readonly Dictionary<string, string> AnsweredOutsideTable = new(StringComparer.Ordinal)
    {
        ["Control+Enter"] = "while an edit is open it fills the selection (ADR-0007)",
        ["F4"] = "while an edit is open it cycles the Reference at the caret, where CycleReference is declared (ADR-0051)",
        ["Control+c"] = "the browser's copy event, which the core answers (ADR-0005)",
        ["Control+C"] = "the browser's copy event, which the core answers (ADR-0005)",
        ["Control+Insert"] = "the browser's copy event, which the core answers (ADR-0005)",
        ["Control+v"] = "the browser's paste event, which the core answers (ADR-0014)",
        ["Control+V"] = "the browser's paste event, which the core answers (ADR-0014)",
        ["Shift+Insert"] = "the browser's paste event, which the core answers (ADR-0014)",
    };

    /// <summary>What the core does with a key, in words, or null when the core leaves it alone.</summary>
    private static string? CoreAnswer(string canonical, bool control, bool meta, bool alt, string key)
    {
        if (Table.TryGetValue(canonical, out var action))
            return $"the core's {action.Kind}";
        if (AnsweredOutsideTable.TryGetValue(canonical, out var answer))
            return answer;
        // The gate's typing rules (ADR-0010): a character, or F2, with nothing but Shift opens the
        // Cell Editor on a grid that edits, and Control with Alt together is AltGr typing one.
        if (!control && !meta && !alt && (key.Length == 1 || key == "F2"))
            return "typing, which opens the Cell Editor (ADR-0010)";
        if (control && alt && key.Length == 1)
            return "typing with AltGr, which opens the Cell Editor (ADR-0010)";
        return null;
    }

    /// <summary>Whether <paramref name="declared"/> is in <see cref="Canonical"/>'s form: the
    /// modifiers in its order, each at most once, then a key that is not a modifier. A key's own
    /// name holds no <c>+</c> unless it is <c>+</c> itself.</summary>
    private static bool IsCanonicalForm(
        string? declared, out bool control, out bool meta, out bool alt, out string key)
    {
        var rest = declared ?? "";
        control = TakePrefix(ref rest, "Control+");
        meta = TakePrefix(ref rest, "Meta+");
        var shift = TakePrefix(ref rest, "Shift+");
        alt = TakePrefix(ref rest, "Alt+");
        key = rest;
        // The Alt key's own keydown, with nothing else held, is the one modifier a Consumer may
        // declare: Alt+Alt, as a press of it canonicalises (ExSheet's KeyTips, ADR-0100; ADR-0050
        // item 14's note of 2026-10-02). Claimed, its release cannot open the browser's menu.
        if (rest == "Alt" && alt && !control && !meta && !shift)
            return true;
        return rest.Length > 0
            && (rest == "+" || !rest.Contains('+', StringComparison.Ordinal))
            && rest is not ("Control" or "Meta" or "Shift" or "Alt");
    }

    private static bool TakePrefix(ref string rest, string prefix)
    {
        if (!rest.StartsWith(prefix, StringComparison.Ordinal) || rest.Length == prefix.Length)
            return false;
        rest = rest[prefix.Length..];
        return true;
    }

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

        // PageUp / PageDown move the Focus and the Viewport by the same number of rows
        // (ADR-0012). Their Control forms are deliberately absent: the browser switches
        // tabs with them, and the capture-phase listener exists to see this grid's own
        // keys first — not to take the browser's away.
        table["PageUp"] = new(GridKeyKind.MoveByViewport, GridDirection.Up);
        table["PageDown"] = new(GridKeyKind.MoveByViewport, GridDirection.Down);
        table["Shift+PageUp"] = new(GridKeyKind.ExtendByViewport, GridDirection.Up);
        table["Shift+PageDown"] = new(GridKeyKind.ExtendByViewport, GridDirection.Down);

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

        // Excel's three keys for the active cell (ADR-0052). None is the browser's, and none
        // is taken from an open editor: while editing the listener claims only the editing
        // keys, so Backspace and Ctrl+Backspace stay the editor's (ADR-0010).
        table["Control+."] = new(GridKeyKind.MoveFocusToNextCorner);
        table["Control+Backspace"] = new(GridKeyKind.RevealFocus);
        table["Shift+Backspace"] = new(GridKeyKind.CollapseToFocus);

        // The way out of Tab's cycle. ADR-0012 says Enter and Tab never leave the
        // selection, which without an exit would trap the keyboard inside the grid —
        // against ADR-0020's own "the grid is one tab stop". With nothing to dismiss it
        // releases Tab, and the root keeps the keyboard (rewritten 2026-10-01).
        table["Escape"] = new(GridKeyKind.Leave);

        // The context menu, from the keyboard (ADR-0036). Both spellings, because the
        // platforms disagree about which one exists: a Windows keyboard has the menu key
        // and every platform has Shift+F10. Taking them is what suppresses the browser's
        // own menu — there is no `contextmenu` attribute to lean on for a key.
        table["ContextMenu"] = new(GridKeyKind.OpenContextMenu);
        table["Shift+F10"] = new(GridKeyKind.OpenContextMenu);

        // The column menu, from the keyboard (ADR-0039): Excel's key for a header's
        // drop-down, aimed at the Focus's column. Without it a keyboard-only user could
        // not reach sorting or filtering at all.
        table["Alt+ArrowDown"] = new(GridKeyKind.OpenColumnMenu);

        // Excel's editing keys (ADR-0007/0035/0054), each claimed only on the grids named
        // in BuildConditions. Both cases of every letter, for the CapsLock reason above.
        // Ctrl+Shift+Z with CapsLock on reports a lowercase z, so both of its cases too.
        foreach (var z in new[] { "z", "Z" })
        {
            table["Control+" + z] = new(GridKeyKind.Undo);
            table["Control+Shift+" + z] = new(GridKeyKind.Redo);
        }
        table["Control+y"] = new(GridKeyKind.Redo);
        table["Control+Y"] = new(GridKeyKind.Redo);
        table["Delete"] = new(GridKeyKind.Clear);
        table["Backspace"] = new(GridKeyKind.ClearAndEdit);
        table["Control+d"] = new(GridKeyKind.FillDown);
        table["Control+D"] = new(GridKeyKind.FillDown);
        table["Control+r"] = new(GridKeyKind.FillRight);
        table["Control+R"] = new(GridKeyKind.FillRight);

        // Find (ADR-0055), on every grid: the browser's own find sees only painted rows, and
        // a grid that let the key through would hand the user a search that looks complete
        // and is not — even where nothing better is wired, the refusal says so.
        table["Control+f"] = new(GridKeyKind.Find);
        table["Control+F"] = new(GridKeyKind.Find);

        return table;
    }

    private static Dictionary<string, Func<GridKeyClaims, bool>> BuildConditions()
    {
        var conditions = new Dictionary<string, Func<GridKeyClaims, bool>>(StringComparer.Ordinal);
        foreach (var (key, action) in Table)
        {
            Func<GridKeyClaims, bool>? when = action.Kind switch
            {
                GridKeyKind.Undo => claims => claims.CanUndo,
                GridKeyKind.Redo => claims => claims.CanRedo,
                GridKeyKind.Clear or GridKeyKind.ClearAndEdit
                    or GridKeyKind.FillDown or GridKeyKind.FillRight => claims => claims.CanEdit,
                _ => null,
            };
            if (when is not null)
                conditions[key] = when;
        }
        return conditions;
    }
}
