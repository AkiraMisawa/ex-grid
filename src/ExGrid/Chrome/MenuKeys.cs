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
/// (ADR-0039). Once a menu holds DOM focus the capture-phase gate takes Escape alone and
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
/// <item><term>Tab / Shift+Tab</term><description>closes the menu, as a Cancel</description></item>
/// </list>
///
/// Escape is not here: the grid's own gate takes it, from anywhere under the root, and
/// closes the menu as a Cancel (ADR-0012's layering). A key chorded with Control, Alt or
/// Meta means nothing — a menu has no shortcuts of its own, and a browser's or the OS's
/// chord must not be read as a move.
/// </summary>
public static class MenuKeys
{
    /// <summary>
    /// What <paramref name="key"/> (a <c>KeyboardEvent.key</c> value) means, pressed on the
    /// item at <paramref name="current"/> of <paramref name="commands"/>.
    /// </summary>
    public static MenuKey Resolve(
        string key, bool shift, bool controlAltOrMeta, IReadOnlyList<GridCommand> commands, int current)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (controlAltOrMeta)
            return MenuKey.Nothing;
        if (key == "Tab")
            return new(MenuKeyKind.Close);
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
