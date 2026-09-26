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

    // The column menu as the grid builds it (ADR-0044), on a column with no filter in force:
    // clear filter and unpin are disabled.
    private static readonly GridCommand[] Column =
    [
        Item("sort-ascending"), Item("sort-descending"), Item("clear-filter", enabled: false),
        Item("hide"), Item("pin"), Item("unpin", enabled: false), Item("size-to-fit"),
    ];

    private static MenuKey PressInColumn(
        string key, int current = 0, bool shift = false, bool chord = false, bool filterBelow = true,
        GridCommand[]? commands = null)
        => MenuKeys.ResolveInColumnMenu(key, shift, chord, commands ?? Column, current, filterBelow);

    [Fact] // ADR-0044 / FL-15: S and O run the sorts from any command, in either case
    public void S_and_o_run_the_sorts()
    {
        Assert.Equal(new MenuKey(MenuKeyKind.Run, 0), PressInColumn("s", current: 4));
        Assert.Equal(new MenuKey(MenuKeyKind.Run, 0), PressInColumn("S", current: 6));
        Assert.Equal(new MenuKey(MenuKeyKind.Run, 1), PressInColumn("o"));
        Assert.Equal(new MenuKey(MenuKeyKind.Run, 1), PressInColumn("O", shift: true));
    }

    [Fact] // ADR-0044 / FL-15 / FL-16: C clears the filter, and only while there is one to clear
    public void C_clears_the_filter_only_while_it_is_enabled()
    {
        Assert.Equal(MenuKey.Nothing, PressInColumn("c"));

        GridCommand[] filtered = [.. Column];
        filtered[2] = Item("clear-filter");
        Assert.Equal(new MenuKey(MenuKeyKind.Run, 2), PressInColumn("c", commands: filtered));
    }

    [Fact] // ADR-0044 / FL-15: a letter whose command is disabled does nothing
    public void A_letter_on_a_disabled_sort_does_nothing()
    {
        GridCommand[] unsortable = [.. Column];
        unsortable[0] = Item("sort-ascending", enabled: false);
        Assert.Equal(MenuKey.Nothing, PressInColumn("s", commands: unsortable));
    }

    [Fact] // ADR-0044 / FL-15: E moves to the field a search is typed into — when a filter stands below
    public void E_moves_to_the_filter_when_there_is_one()
    {
        Assert.Equal(new MenuKey(MenuKeyKind.FilterSearch), PressInColumn("e"));
        Assert.Equal(new MenuKey(MenuKeyKind.FilterSearch), PressInColumn("E"));
        Assert.Equal(MenuKey.Nothing, PressInColumn("e", filterBelow: false));
    }

    [Fact] // ADR-0044 / FL-12: over a filter, Tab moves to its first control and Shift+Tab to its last
    public void Tab_moves_into_the_filter_below()
    {
        Assert.Equal(new MenuKey(MenuKeyKind.FilterFirst), PressInColumn("Tab", current: 3));
        Assert.Equal(new MenuKey(MenuKeyKind.FilterLast), PressInColumn("Tab", current: 3, shift: true));
    }

    [Fact] // ADR-0044 / KB-30: a column that cannot be filtered shows the commands alone, and Tab closes them
    public void Tab_closes_a_column_menu_with_no_filter()
    {
        Assert.Equal(new MenuKey(MenuKeyKind.Close), PressInColumn("Tab", filterBelow: false));
        Assert.Equal(new MenuKey(MenuKeyKind.Close), PressInColumn("Tab", shift: true, filterBelow: false));
    }

    [Fact] // ADR-0044: hide, pin, unpin and Size to fit borrow no letter; a chorded letter is nobody's
    public void Other_letters_and_chords_mean_nothing()
    {
        Assert.Equal(MenuKey.Nothing, PressInColumn("h"));
        Assert.Equal(MenuKey.Nothing, PressInColumn("p"));
        Assert.Equal(MenuKey.Nothing, PressInColumn("s", chord: true));
        Assert.Equal(MenuKey.Nothing, PressInColumn("e", chord: true));
        Assert.Equal(MenuKey.Nothing, PressInColumn("Tab", chord: true));
    }

    [Fact] // ADR-0044 / KB-30: the arrows, Home, End, Enter and Space are the column menu's as any menu's
    public void The_menu_keys_hold_in_the_column_menu()
    {
        Assert.Equal(new MenuKey(MenuKeyKind.Move, 3), PressInColumn("ArrowDown", current: 1));
        Assert.Equal(new MenuKey(MenuKeyKind.Move, 6), PressInColumn("End"));
        Assert.Equal(new MenuKey(MenuKeyKind.Run, 4), PressInColumn("Enter", current: 4));
        Assert.Equal(MenuKey.Nothing, PressInColumn("ArrowDown", shift: true));
    }

    [Fact] // ADR-0044 / FL-15: on the value list the letters act, and nothing else is the grid's
    public void The_value_list_answers_the_letters_alone()
    {
        Assert.Equal(new MenuKey(MenuKeyKind.Run, 0), MenuKeys.Letter("s", false, Column, filterBelow: true));
        Assert.Equal(new MenuKey(MenuKeyKind.FilterSearch), MenuKeys.Letter("e", false, Column, filterBelow: true));
        Assert.Equal(MenuKey.Nothing, MenuKeys.Letter("ArrowDown", false, Column, filterBelow: true));
        Assert.Equal(MenuKey.Nothing, MenuKeys.Letter(" ", false, Column, filterBelow: true));
        Assert.Equal(MenuKey.Nothing, MenuKeys.Letter("Tab", false, Column, filterBelow: true));
    }

    [Fact] // ADR-0036 / ADR-0044: the Context Menu is not Excel's drop-down, and has no letters
    public void The_context_menu_has_no_letters()
    {
        GridCommand[] context = [Item("copy"), Item("copy-with-headers"), Item("sort-ascending")];
        Assert.Equal(MenuKey.Nothing, MenuKeys.Resolve("s", false, false, context, 0));
        Assert.Equal(MenuKey.Nothing, MenuKeys.Resolve("c", false, false, context, 0));
    }

    [Fact] // ADR-0044 / FL-15: only the sorts and clear filter answer to a letter
    public void The_letters_belong_to_three_commands()
    {
        Assert.Equal('S', MenuKeys.LetterOf("sort-ascending"));
        Assert.Equal('O', MenuKeys.LetterOf("sort-descending"));
        Assert.Equal('C', MenuKeys.LetterOf("clear-filter"));
        Assert.Null(MenuKeys.LetterOf("hide"));
        Assert.Null(MenuKeys.LetterOf("size-to-fit"));
        Assert.Null(MenuKeys.LetterOf("copy"));
    }

    [Fact] // ADR-0044 / FL-15: a label shows its letter where it has it, as English Excel; appended, as Japanese Excel
    public void A_label_is_marked_at_its_letter_or_given_it()
    {
        Assert.Equal(("", "S", "ort ascending"), MenuKeys.SplitAtLetter("Sort ascending", 'S'));
        Assert.Equal(("S", "o", "rt descending"), MenuKeys.SplitAtLetter("Sort descending", 'O'));
        Assert.Equal(("", "C", "lear filter"), MenuKeys.SplitAtLetter("Clear filter", 'C'));
        Assert.Equal(("昇順(", "S", ")"), MenuKeys.SplitAtLetter("昇順", 'S'));
    }
}
