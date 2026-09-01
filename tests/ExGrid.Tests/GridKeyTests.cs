using ExGrid.Keys;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The key table (ADR-0010: the core arbitrates the keys). Pure, and tested here rather
/// than in a browser because the other half of this seam is JavaScript — this side is the
/// authority, so this is where the table is pinned.
/// </summary>
public class GridKeyTests
{
    private static GridKeyAction Key(
        string key, bool ctrl = false, bool shift = false, bool alt = false,
        bool meta = false, bool metaIsPrimary = true)
        => GridKeys.Resolve(key, ctrl, shift, alt, meta, metaIsPrimary);

    [Theory] // ADR-0012: the four forms of an arrow key
    [InlineData(false, false, GridKeyKind.Move)]
    [InlineData(true, false, GridKeyKind.Extend)]          // Shift
    [InlineData(false, true, GridKeyKind.MoveToEdge)]      // Ctrl
    [InlineData(true, true, GridKeyKind.ExtendToEdge)]     // Ctrl+Shift
    public void An_arrow_resolves_by_its_modifiers(bool shift, bool ctrl, GridKeyKind expected)
    {
        var action = Key("ArrowDown", ctrl: ctrl, shift: shift);

        Assert.Equal(expected, action.Kind);
        Assert.Equal(GridDirection.Down, action.Direction);
    }

    [Fact] // ADR-0012: where Meta is Command, it is the modifier a user reaches for
    public void Meta_is_primary_on_an_apple_platform()
    {
        Assert.Equal(GridKeyKind.SelectAll, Key("a", meta: true).Kind);
        Assert.Equal(GridKeyKind.MoveToEdge, Key("ArrowUp", meta: true).Kind);
        Assert.Equal(
            "Control+ArrowUp",
            GridKeys.Canonical("ArrowUp", ctrl: false, shift: false, alt: false, meta: true, metaIsPrimary: true));
    }

    [Fact] // ADR-0012: elsewhere the Meta key is the OS's, and the grid does not take it
    public void Meta_is_not_primary_anywhere_else()
    {
        // Win+ArrowUp snaps a window, Super+A opens a shell. On the days the window
        // manager does not grab them first, a grid that folded Meta into Control would
        // move the selection under a gesture aimed at the desktop.
        Assert.Equal(GridKeyKind.None, Key("a", meta: true, metaIsPrimary: false).Kind);
        Assert.Equal(GridKeyKind.None, Key("ArrowUp", meta: true, metaIsPrimary: false).Kind);
        // And it stays in the form rather than disappearing from it: canonicalised to a
        // bare "ArrowUp", Win+Down would move the selection under a chord aimed at the
        // window manager. Nothing in the table carries "Meta+".
        Assert.Equal(
            "Meta+ArrowUp",
            GridKeys.Canonical("ArrowUp", ctrl: false, shift: false, alt: false, meta: true, metaIsPrimary: false));
        Assert.Equal(GridKeyKind.None, Key("Enter", meta: true, metaIsPrimary: false).Kind);
        Assert.Equal(GridKeyKind.None, Key(" ", meta: true, metaIsPrimary: false).Kind);
    }

    [Fact] // Control is primary everywhere — Ctrl+A works on a Mac too
    public void Control_is_primary_on_every_platform()
    {
        Assert.Equal(GridKeyKind.SelectAll, Key("a", ctrl: true, metaIsPrimary: true).Kind);
        Assert.Equal(GridKeyKind.SelectAll, Key("a", ctrl: true, metaIsPrimary: false).Kind);
        Assert.Equal(GridKeyKind.MoveToEdge, Key("ArrowUp", ctrl: true, metaIsPrimary: false).Kind);
    }

    [Fact] // ADR-0012: Enter runs down columns, Tab runs across rows, Shift runs both backwards
    public void Enter_and_tab_cycle_in_their_own_orders()
    {
        Assert.Equal(new GridKeyAction(GridKeyKind.Cycle, Order: CycleOrder.ColumnMajor), Key("Enter"));
        Assert.Equal(new GridKeyAction(GridKeyKind.Cycle, Order: CycleOrder.ColumnMajor, Backward: true), Key("Enter", shift: true));
        Assert.Equal(new GridKeyAction(GridKeyKind.Cycle, Order: CycleOrder.RowMajor), Key("Tab"));
        Assert.Equal(new GridKeyAction(GridKeyKind.Cycle, Order: CycleOrder.RowMajor, Backward: true), Key("Tab", shift: true));
    }

    [Fact] // ADR-0012 (refined): Home and End are the row's edges; with Ctrl, the result's corners
    public void Home_and_end_reach_the_edges_and_the_corners()
    {
        Assert.Equal(new GridKeyAction(GridKeyKind.MoveToEdge, GridDirection.Left), Key("Home"));
        Assert.Equal(new GridKeyAction(GridKeyKind.ExtendToEdge, GridDirection.Right), Key("End", shift: true));
        Assert.Equal(GridKeyKind.MoveToCorner, Key("Home", ctrl: true).Kind);
        Assert.True(Key("Home", ctrl: true).Backward);
        Assert.False(Key("End", ctrl: true).Backward);
    }

    [Fact] // ADR-0011 / ADR-0012 / ADR-0020: the whole-selection keys and Space
    public void The_selection_keys_and_space_resolve()
    {
        Assert.Equal(GridKeyKind.SelectAll, Key("a", ctrl: true).Kind);
        Assert.Equal(GridKeyKind.SelectWholeColumns, Key(" ", ctrl: true).Kind);
        Assert.Equal(GridKeyKind.SelectWholeRows, Key(" ", shift: true).Kind);
        Assert.Equal(GridKeyKind.Engage, Key(" ").Kind);
        Assert.Equal(GridKeyKind.Leave, Key("Escape").Kind);
    }

    [Fact] // ADR-0010: everything the core does not claim reaches the browser and the cell
    public void What_the_core_does_not_take_is_left_alone()
    {
        // The clipboard and the editor are not wired yet, and the browser's own must not
        // be swallowed by a grid that happens to have focus.
        foreach (var action in new[]
        {
            Key("c", ctrl: true), Key("v", ctrl: true), Key("F2"), Key("f", ctrl: true),
            Key("x"), Key("F5"), Key("Alt+ArrowDown"),
            Key("ArrowDown", alt: true),
        })
        {
            Assert.Equal(GridKeyKind.None, action.Kind);
        }
    }

    [Theory] // ADR-0012: PageUp / PageDown move Focus and Viewport together; Shift extends
    [InlineData("PageUp", false, GridKeyKind.MoveByViewport, GridDirection.Up)]
    [InlineData("PageDown", false, GridKeyKind.MoveByViewport, GridDirection.Down)]
    [InlineData("PageUp", true, GridKeyKind.ExtendByViewport, GridDirection.Up)]
    [InlineData("PageDown", true, GridKeyKind.ExtendByViewport, GridDirection.Down)]
    public void The_viewport_keys_resolve_by_their_modifiers(
        string key, bool shift, GridKeyKind expected, GridDirection direction)
    {
        var action = Key(key, shift: shift);

        Assert.Equal(expected, action.Kind);
        Assert.Equal(direction, action.Direction);
    }

    [Fact] // ADR-0012: the Control forms are the browser's — it switches tabs with them
    public void Control_viewport_keys_are_neither_handled_nor_taken()
    {
        Assert.Equal(GridKeyKind.None, Key("PageUp", ctrl: true).Kind);
        Assert.Equal(GridKeyKind.None, Key("PageDown", ctrl: true).Kind);
        // Not in the taken set either: the listener must not preventDefault them.
        Assert.DoesNotContain("Control+PageUp", GridKeys.Taken);
        Assert.DoesNotContain("Control+PageDown", GridKeys.Taken);
    }

    [Fact] // The browser sends what it likes — Unidentified, dead keys, an IME's own
    public void An_unknown_key_is_not_an_error()
    {
        Assert.Equal(GridKeyKind.None, Key("Unidentified").Kind);
        Assert.Equal(GridKeyKind.None, Key("Dead").Kind);
        Assert.Equal(GridKeyKind.None, Key("Process", ctrl: true, shift: true, alt: true).Kind);
        Assert.Equal(GridKeyKind.None, GridKeys.Resolve(null, false, false, false, false, false).Kind);
        Assert.Equal(GridKeyKind.None, Key("").Kind);
    }

    [Fact] // ADR-0010: the set handed to the listener is exactly the set this side resolves
    public void Every_taken_key_resolves_to_something()
    {
        Assert.NotEmpty(GridKeys.Taken);
        foreach (var canonical in GridKeys.Taken)
        {
            // Split the canonical form back into fields, the way the listener will build
            // it forwards. A key in the set that resolved to None would be taken from the
            // browser and then do nothing at all.
            var ctrl = canonical.Contains("Control+", StringComparison.Ordinal);
            var shift = canonical.Contains("Shift+", StringComparison.Ordinal);
            var alt = canonical.Contains("Alt+", StringComparison.Ordinal);
            var key = canonical[(canonical.LastIndexOf('+') + 1)..];
            if (canonical.EndsWith("+ ", StringComparison.Ordinal))
                key = " ";

            Assert.NotEqual(
                GridKeyKind.None,
                GridKeys.Resolve(key, ctrl, shift, alt, meta: false, metaIsPrimary: false).Kind);
        }
    }
}
