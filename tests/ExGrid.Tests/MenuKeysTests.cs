using ExGrid.Chrome;
using Xunit;

namespace ExGrid.Tests;

/// <summary>ADR-0039's table for a menu, transcribed: what each key means on an item.</summary>
public class MenuKeysTests
{
    private static GridCommand Item(string id, bool enabled = true) => new(id, enabled, () => Task.CompletedTask);

    // copy, (paste disabled), sort, (filter disabled), clear — so the enabled items are 0, 2, 4.
    private static readonly GridCommand[] Menu =
        [Item("copy"), Item("paste", enabled: false), Item("sort"), Item("filter", enabled: false), Item("clear")];

    private static MenuKey Press(string key, int current, bool shift = false, bool chord = false)
        => MenuKeys.Resolve(key, shift, chord, Menu, current);

    [Fact] // ADR-0039 / KB-30: ↓ moves to the next enabled item, stepping over a disabled one
    public void Down_moves_to_the_next_enabled_item()
    {
        Assert.Equal(new MenuKey(MenuKeyKind.Move, 2), Press("ArrowDown", 0));
        Assert.Equal(new MenuKey(MenuKeyKind.Move, 4), Press("ArrowDown", 2));
    }

    [Fact] // ADR-0039 / KB-30: ↓ on the last enabled item wraps to the first
    public void Down_wraps_at_the_end()
        => Assert.Equal(new MenuKey(MenuKeyKind.Move, 0), Press("ArrowDown", 4));

    [Fact] // ADR-0039 / KB-30: ↑ moves back over disabled items and wraps at the top
    public void Up_moves_back_and_wraps()
    {
        Assert.Equal(new MenuKey(MenuKeyKind.Move, 2), Press("ArrowUp", 4));
        Assert.Equal(new MenuKey(MenuKeyKind.Move, 4), Press("ArrowUp", 0));
    }

    [Fact] // ADR-0039 / KB-30: Home and End go to the first and last ENABLED items
    public void Home_and_end_go_to_the_first_and_last_enabled_items()
    {
        GridCommand[] fenced = [Item("a", enabled: false), Item("b"), Item("c"), Item("d", enabled: false)];

        Assert.Equal(new MenuKey(MenuKeyKind.Move, 1), MenuKeys.Resolve("Home", false, false, fenced, 2));
        Assert.Equal(new MenuKey(MenuKeyKind.Move, 2), MenuKeys.Resolve("End", false, false, fenced, 1));
        Assert.Equal(1, MenuKeys.First(fenced));
        Assert.Equal(2, MenuKeys.Last(fenced));
    }

    [Fact] // ADR-0039 / KB-30: Enter and Space run the item they are pressed on
    public void Enter_and_space_run_the_item()
    {
        Assert.Equal(new MenuKey(MenuKeyKind.Run, 2), Press("Enter", 2));
        Assert.Equal(new MenuKey(MenuKeyKind.Run, 4), Press(" ", 4));
    }

    [Fact] // ADR-0039 / ADR-0010: a disabled item never runs, however the key reached it
    public void A_disabled_item_does_not_run()
        => Assert.Equal(MenuKey.Nothing, Press("Enter", 1));

    [Fact] // ADR-0039 / KB-30: Tab and Shift+Tab close the menu as a Cancel
    public void Tab_and_shift_tab_close()
    {
        Assert.Equal(new MenuKey(MenuKeyKind.Close), Press("Tab", 2));
        Assert.Equal(new MenuKey(MenuKeyKind.Close), Press("Tab", 2, shift: true));
    }

    [Fact] // ADR-0039: a chord means nothing — the menu has no shortcuts, and the browser's are not moves
    public void A_chord_means_nothing()
    {
        Assert.Equal(MenuKey.Nothing, Press("ArrowDown", 0, chord: true));
        Assert.Equal(MenuKey.Nothing, Press("Enter", 0, chord: true));
        Assert.Equal(MenuKey.Nothing, Press("Tab", 0, chord: true));
        Assert.Equal(MenuKey.Nothing, Press("ArrowDown", 0, shift: true));
    }

    [Fact] // ADR-0039 / ADR-0012: Escape is the gate's, and a letter is nobody's
    public void Escape_and_other_keys_mean_nothing_here()
    {
        Assert.Equal(MenuKey.Nothing, Press("Escape", 0));
        Assert.Equal(MenuKey.Nothing, Press("c", 0));
        Assert.Equal(MenuKey.Nothing, Press("F2", 0));
    }

    [Fact] // ADR-0039: a menu with nothing enabled has nowhere to move
    public void A_menu_with_nothing_enabled_has_nowhere_to_move()
    {
        GridCommand[] none = [Item("a", enabled: false), Item("b", enabled: false)];

        Assert.Equal(MenuKey.Nothing, MenuKeys.Resolve("ArrowDown", false, false, none, 0));
        Assert.Equal(MenuKey.Nothing, MenuKeys.Resolve("Home", false, false, none, 0));
        Assert.Equal(-1, MenuKeys.First(none));
        Assert.Equal(-1, MenuKeys.Last(none));
    }

    [Fact] // ADR-0039: a single enabled item is its own next and previous
    public void A_single_enabled_item_wraps_onto_itself()
    {
        GridCommand[] one = [Item("a", enabled: false), Item("b")];

        Assert.Equal(new MenuKey(MenuKeyKind.Move, 1), MenuKeys.Resolve("ArrowDown", false, false, one, 1));
        Assert.Equal(new MenuKey(MenuKeyKind.Move, 1), MenuKeys.Resolve("ArrowUp", false, false, one, 1));
    }
}
