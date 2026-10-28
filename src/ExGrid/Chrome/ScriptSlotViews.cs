namespace ExGrid.Chrome;

/// <summary>
/// The Chrome slots an <see cref="IGridChrome"/> fills with Razor when the Script Core draws the grid
/// (<c>ExGrid.Script.ExGrid</c>, ADR-0410): each named here is asked for through its view overload, and
/// the Script Core draws its own part for every slot not named.
/// </summary>
/// <remarks>
/// It stands beside the C# core's contexts until the C# core is removed (ADR-0350 section 7, ADR-0410
/// section 4's note of 2026-10-28): a Chrome answers both while both cores draw.
/// </remarks>
[Flags]
public enum ScriptSlots
{
    /// <summary>No slot: the Script Core draws every part.</summary>
    None = 0,

    /// <summary>The column menu's commands, through <see cref="IGridChrome.ColumnMenu(GridMenuView)"/>.</summary>
    ColumnMenu = 1,

    /// <summary>The Context Menu, through <see cref="IGridChrome.ContextMenu(GridMenuView)"/>.</summary>
    ContextMenu = 2,

    /// <summary>The loading indicator, through <see cref="IGridChrome.LoadingIndicator(GridLoadingView)"/>.</summary>
    LoadingIndicator = 4,
}

/// <summary>
/// The attributes a slot's Razor writes on its controls when the Script Core draws the grid (ADR-0410
/// section 2): the adapter's script wires each control it finds by them, in the browser, so nothing a key
/// means waits on the circuit. The contents carry no handler of their own for what an attribute says.
/// </summary>
public static class ScriptSlotAttributes
{
    /// <summary>
    /// On a menu's control for one command, the command's index in <see cref="GridMenuView.Items"/>,
    /// once for every item. The script gives the control the menu's role, takes it out of the tab order,
    /// marks it disabled where its command is, answers every key on it against the place the core keeps
    /// for the menu (ADR-0039), runs its command on a press, and focuses the first enabled one as the menu
    /// asks.
    /// </summary>
    public const string MenuItem = "data-ex-menu-item";
}

/// <summary>One command of a menu, as the Script Core hands it (ADR-0010, ADR-0036).</summary>
/// <param name="Id">The command's id, the one <see cref="GridCommand.Id"/> carries under the C# core.</param>
/// <param name="Label">The command's words, the Consumer's <c>commandLabel</c> else the built-in English.
/// A Wrapper with words of its own may show those instead.</param>
/// <param name="Enabled">Whether the command runs; a disabled one is shown and runs nothing.</param>
public sealed record GridMenuItemView(string Id, string Label, bool Enabled);

/// <summary>
/// A column menu's or the Context Menu's contents when the Script Core draws the grid (ADR-0410 section 3):
/// data only. The Razor draws one control per item, in order, each marked with
/// <see cref="ScriptSlotAttributes.MenuItem"/>, and handles none of its keys, presses or focus: the script
/// does, in the browser.
/// </summary>
/// <param name="Column">The <c>Name</c> of the column the menu is for, or was opened on.</param>
/// <param name="Type">That column's type.</param>
/// <param name="Items">The commands, in the core's order.</param>
/// <remarks>Where the keyboard goes in the menu is the script's: a view carries no focus request, and the
/// first enabled item, or the one a key typed as the menu opened moved to, takes the keyboard as the
/// contents arrive (ADR-0410 section 5).</remarks>
public sealed record GridMenuView(string Column, ColumnType Type, IReadOnlyList<GridMenuItemView> Items);

/// <summary>The loading indicator's view when the Script Core draws the grid (ADR-0010): data only.</summary>
/// <param name="IsLoading">Whether rows are on their way.</param>
/// <param name="PlaceholderRowCount">How many of the painted rows are Placeholders, which stay the core's.</param>
public sealed record GridLoadingView(bool IsLoading, int PlaceholderRowCount);
