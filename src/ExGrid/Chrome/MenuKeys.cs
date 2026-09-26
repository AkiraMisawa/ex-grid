namespace ExGrid.Chrome;

/// <summary>What a key pressed on a menu's item does (ADR-0039).</summary>
public enum MenuKeyKind
{
    /// <summary>Nothing: the key has no meaning in a menu.</summary>
    None = 0,

    /// <summary>DOM focus moves to the item <see cref="MenuKey.Item"/>.</summary>
    Move,

    /// <summary>The item the key was pressed on runs, and the menu closes.</summary>
    Run,

    /// <summary>The menu closes as a Cancel, and the keyboard goes back to the root.</summary>
    Close,

    /// <summary>The keyboard moves to the first control of the filter below the commands —
    /// Tab, or E for its search box (ADR-0044). The core moves it, through the filter's
    /// <c>FocusRequest</c>; the menu's contents do nothing.</summary>
    FilterFirst,

    /// <summary>The keyboard moves to the last control of the filter below the commands —
    /// Shift+Tab, wrapping backwards (ADR-0044). The core moves it; the menu's contents do
    /// nothing.</summary>
    FilterLast,
}

/// <summary>A key's meaning in a menu: what to do, and for a move, which item.</summary>
public readonly record struct MenuKey(MenuKeyKind Kind, int Item = -1)
{
    /// <summary>The key means nothing in a menu: the <c>default</c> value, whose kind is
    /// <see cref="MenuKeyKind.None"/> and whose <see cref="Item"/> means nothing.</summary>
    public static MenuKey Nothing => default;
}

/// <summary>
/// The keys inside a menu — the column menu or the Context Menu — and what each one means
/// (ADR-0039/0044). Once a menu holds DOM focus the capture-phase gate takes Escape alone and
/// leaves every other key to the item that has it (ADR-0012), so the keys are the
/// contents' to implement. <b>What they mean is decided here</b>, once, so that the
/// built-in Chrome and a substituted one answer the same key the same way and swapping
/// Chrome still changes nothing about behaviour (ADR-0010, FN-17). Pure, so the table is
/// testable without a browser.
///
/// <list type="table">
/// <item><term>↑ / ↓</term><description>the previous / next <b>enabled</b> item, wrapping at the ends</description></item>
/// <item><term>Home / End</term><description>the first / last enabled item</description></item>
/// <item><term>Enter / Space</term><description>runs the item; the menu closes</description></item>
/// <item><term>Tab / Shift+Tab</term><description>closes the menu, as a Cancel — or, over a
/// column's filter, moves to the filter's first / last control (ADR-0044)</description></item>
/// <item><term>S / O / C</term><description>in the column menu, runs sort ascending, sort
/// descending or clear filter, when enabled — Excel's drop-down letters (ADR-0044)</description></item>
/// <item><term>E</term><description>in the column menu over a filter, moves to the filter's
/// first control, its search box</description></item>
/// </list>
///
/// Escape is not here: the grid's own gate takes it, from anywhere under the root, and
/// closes the menu as a Cancel (ADR-0012's layering). A key chorded with Control, Alt or
/// Meta means nothing — a menu has no shortcuts of its own, and a browser's or the OS's
/// chord must not be read as a move. The letters are fixed whatever language the labels are
/// in, and the Context Menu has none: it is not Excel's drop-down.
/// </summary>
public static class MenuKeys
{
    /// <summary>
    /// What <paramref name="key"/> (a <c>KeyboardEvent.key</c> value) means, pressed on the
    /// item at <paramref name="current"/> of <paramref name="commands"/>, in a menu of
    /// commands alone — the Context Menu (ADR-0036).
    /// </summary>
    public static MenuKey Resolve(
        string key, bool shift, bool controlAltOrMeta, IReadOnlyList<GridCommand> commands, int current)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (controlAltOrMeta)
            return MenuKey.Nothing;
        if (key == "Tab")
            return new(MenuKeyKind.Close);
        return Common(key, shift, commands, current);
    }

    /// <summary>
    /// What <paramref name="key"/> means pressed on the item at <paramref name="current"/>
    /// of a column menu's <paramref name="commands"/> (ADR-0044): the keys of any menu, and
    /// Excel's letters. <paramref name="filterBelow"/> says whether the column's filter
    /// stands under the commands in the same popover — Tab then moves into it rather than
    /// closing, and E reaches its search box.
    /// </summary>
    public static MenuKey ResolveInColumnMenu(
        string key, bool shift, bool controlAltOrMeta, IReadOnlyList<GridCommand> commands, int current,
        bool filterBelow)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (controlAltOrMeta)
            return MenuKey.Nothing;
        if (key == "Tab")
            return !filterBelow ? new(MenuKeyKind.Close) : new(shift ? MenuKeyKind.FilterLast : MenuKeyKind.FilterFirst);
        if (Letter(key, controlAltOrMeta, commands, filterBelow) is { Kind: not MenuKeyKind.None } letter)
            return letter;
        return Common(key, shift, commands, current);
    }

    /// <summary>
    /// Excel's letters in the column's drop-down (ADR-0044): S, O and C run sort ascending,
    /// sort descending and clear filter when they are enabled; E moves to the filter's first
    /// control, its search box, when a filter stands below. Either case, Shift or not — Caps
    /// Lock is not a different key. Answered for a key on a command and for a key on the value
    /// list alike; in a text field a letter is text, and nothing asks this.
    /// </summary>
    public static MenuKey Letter(string key, bool controlAltOrMeta, IReadOnlyList<GridCommand> commands, bool filterBelow)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (controlAltOrMeta || key is not { Length: 1 })
            return MenuKey.Nothing;
        var letter = char.ToUpperInvariant(key[0]);
        if (letter == 'E')
            return filterBelow ? new(MenuKeyKind.FilterFirst) : MenuKey.Nothing;
        for (var i = 0; i < commands.Count; i++)
        {
            if (LetterOf(commands[i].Id) == letter)
                return commands[i].Enabled ? new(MenuKeyKind.Run, i) : MenuKey.Nothing;
        }

        return MenuKey.Nothing;
    }

    /// <summary>The letter a command of the column menu answers to — S, O or C — or null for
    /// a command Excel's drop-down does not have (ADR-0044): hide, pin, unpin and Size to fit
    /// get none, since a borrowed letter would mean something an Excel user does not expect.</summary>
    public static char? LetterOf(string commandId) => commandId switch
    {
        "sort-ascending" => 'S',
        "sort-descending" => 'O',
        "clear-filter" => 'C',
        _ => null,
    };

    /// <summary>
    /// A label split around the letter it answers to, the way Excel shows it (ADR-0044):
    /// at the letter's first place in the label, either case, where the label has it, and
    /// appended as "(S)" where it does not — a label in another script keeps its letter. The
    /// Chrome underlines <c>Letter</c>.
    /// </summary>
    public static (string Before, string Letter, string After) Marked(string label, char letter)
    {
        ArgumentNullException.ThrowIfNull(label);
        var at = label.IndexOf(letter.ToString(), StringComparison.OrdinalIgnoreCase);
        return at < 0
            ? (label + "(", letter.ToString(), ")")
            : (label[..at], label.Substring(at, 1), label[(at + 1)..]);
    }

    private static MenuKey Common(string key, bool shift, IReadOnlyList<GridCommand> commands, int current)
    {
        if (shift)
            return MenuKey.Nothing;

        return key switch
        {
            "ArrowDown" => MoveTo(Next(commands, current, +1)),
            "ArrowUp" => MoveTo(Next(commands, current, -1)),
            "Home" => MoveTo(First(commands)),
            "End" => MoveTo(Last(commands)),
            "Enter" or " " => IsEnabled(commands, current) ? new(MenuKeyKind.Run, current) : MenuKey.Nothing,
            _ => MenuKey.Nothing,
        };
    }

    /// <summary>The first enabled item — where DOM focus goes when the menu opens — or -1
    /// when none is.</summary>
    public static int First(IReadOnlyList<GridCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        for (var i = 0; i < commands.Count; i++)
        {
            if (commands[i].Enabled)
                return i;
        }

        return -1;
    }

    /// <summary>The last enabled item, or -1 when none is.</summary>
    public static int Last(IReadOnlyList<GridCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        for (var i = commands.Count - 1; i >= 0; i--)
        {
            if (commands[i].Enabled)
                return i;
        }

        return -1;
    }

    private static MenuKey MoveTo(int item) => item < 0 ? MenuKey.Nothing : new(MenuKeyKind.Move, item);

    private static bool IsEnabled(IReadOnlyList<GridCommand> commands, int item)
        => item >= 0 && item < commands.Count && commands[item].Enabled;

    // The next enabled item in the direction, wrapping — from the one that has focus, or,
    // should that somehow be out of range, from the end the direction starts at.
    private static int Next(IReadOnlyList<GridCommand> commands, int current, int direction)
    {
        var count = commands.Count;
        if (count == 0)
            return -1;
        var start = current >= 0 && current < count ? current : direction > 0 ? -1 : count;
        for (var step = 1; step <= count; step++)
        {
            var candidate = ((start + (direction * step)) % count + count) % count;
            if (commands[candidate].Enabled)
                return candidate;
        }

        return -1;
    }
}
