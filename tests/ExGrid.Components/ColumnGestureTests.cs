using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The two header drags (ADR-0016 resize, ADR-0011/0032 reorder): the guide and the
/// indicator are one absolutely positioned element each, nothing applies until
/// release, and the grid only ever notifies. 6 columns of 100px, 350px viewport.
/// </summary>
public class ColumnGestureTests : GridTestContext
{
    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Action<ColumnWidthChange>? onWidth = null,
        Action<IReadOnlyList<string>>? onOrder = null,
        Action<IReadOnlyList<SortSpec>>? onSort = null,
        int pinned = 0)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, TestRows.Wide(6))
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.PinnedColumnCount, pinned);
            if (onWidth is not null)
                ps.Add(g => g.OnColumnWidthChanged, onWidth);
            if (onOrder is not null)
                ps.Add(g => g.OnColumnOrderChanged, onOrder);
            if (onSort is not null)
                ps.Add(g => g.OnSortChanged, onSort);
        });

    [Fact] // PF-3 / ADR-0003: a header's menu button and grip keep their handlers across renders of the root
    public void Header_buttons_keep_their_handlers_across_renders()
    {
        // Pinned and scrollable alike: the two header blocks are separate markup.
        var cut = RenderGrid(onWidth: _ => { }, onSort: _ => { }, pinned: 1);
        string?[] Handlers() =>
        [
            .. cut.FindAll(".ex-menu-button").Select(b => b.GetAttribute("blazor:onclick")),
            .. cut.FindAll(".ex-resize-grip").Select(g => g.GetAttribute("blazor:onmousedown")),
        ];
        var before = Handlers();
        Assert.Equal(2 * cut.FindAll(".ex-header-cell").Count, before.Length);
        Assert.All(before, Assert.NotNull); // or an all-null pair would compare equal
        var rows = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToArray();

        // The root renders on every scroll frame; this is one such render with nothing
        // in the header changed. The rows re-rendering is the proof that it happened
        // (the root's own RenderCount is no count of the root's renders: bUnit adds its
        // children's to it).
        cut.Render(ps => ps.Add(g => g.CellState, (_, _) => CellState.Normal));

        Assert.All(
            cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).Zip(rows),
            pair => Assert.Equal(pair.Second + 1, pair.First));
        // The same handlers, not equal ones: rebuilt per render, each would be a changed
        // attribute the diff re-registers and sends to the browser — every header button,
        // every frame.
        Assert.Equal(before, Handlers());
    }

    [Fact] // ADR-0016: grips render only when somebody listens
    public void Grips_render_only_with_a_handler()
    {
        Assert.NotEmpty(RenderGrid(onWidth: _ => { }).FindAll(".ex-resize-grip"));
        Assert.Empty(RenderGrid().FindAll(".ex-resize-grip"));
    }

    [Fact] // ADR-0016: the guide follows the pointer; the width is applied on release, as Fixed intent
    public async Task A_resize_drag_applies_on_release()
    {
        ColumnWidthChange? change = null;
        var cut = RenderGrid(onWidth: c => change = c);

        // Grab column 1's grip at clientX 200, drag 40px right.
        await cut.FindAll(".ex-resize-grip")[1].MouseDownAsync(new MouseEventArgs { Button = 0, ClientX = 200 });
        await cut.Find(".ex-grid").MouseMoveAsync(new MouseEventArgs { Buttons = 1, ClientX = 220 });

        var guide = cut.Find(".ex-resize-guide");
        Assert.Contains("left: 220px", guide.GetAttribute("style")); // 100 + 100 + 20
        Assert.Null(change); // nothing applied mid-drag

        await cut.Find(".ex-grid").MouseUpAsync(new MouseEventArgs { Button = 0, ClientX = 240 });

        Assert.Equal(new ColumnWidthChange(TestRows.ColumnName(1), 140), change);
        Assert.Empty(cut.FindAll(".ex-resize-guide"));
    }

    [Fact] // ADR-0016: bounded below by MinWidth, unbounded above
    public async Task A_resize_clamps_at_min_width_only()
    {
        ColumnWidthChange? change = null;
        var cut = RenderGrid(onWidth: c => change = c);

        await cut.FindAll(".ex-resize-grip")[1].MouseDownAsync(new MouseEventArgs { Button = 0, ClientX = 200 });
        await cut.Find(".ex-grid").MouseUpAsync(new MouseEventArgs { Button = 0, ClientX = 0 });
        Assert.Equal(ColumnWidthSpec.DefaultMinWidthPx, change!.WidthPx);

        await cut.FindAll(".ex-resize-grip")[1].MouseDownAsync(new MouseEventArgs { Button = 0, ClientX = 200 });
        await cut.Find(".ex-grid").MouseUpAsync(new MouseEventArgs { Button = 0, ClientX = 800 });
        Assert.Equal(700, change.WidthPx); // 100 + 600 — past MaxWidth, deliberately
    }

    [Fact] // ADR-0011: a header drag past the threshold reorders; the grid only notifies
    public async Task A_header_drag_reorders_on_release()
    {
        IReadOnlyList<string>? order = null;
        IReadOnlyList<SortSpec>? sorted = null;
        var cut = RenderGrid(onOrder: o => order = o, onSort: s => sorted = s);

        // Grab column 0's header and drag right past column 1's middle.
        await cut.Find(".ex-header").MouseDownAsync(new MouseEventArgs { Button = 0, OffsetX = 50, OffsetY = 10, ClientX = 50 });
        await cut.Find(".ex-grid").MouseMoveAsync(new MouseEventArgs { Buttons = 1, ClientX = 210 });
        Assert.NotNull(cut.Find(".ex-drop-indicator"));
        Assert.Contains("left: 200px", cut.Find(".ex-drop-indicator").GetAttribute("style"));

        await cut.Find(".ex-grid").MouseUpAsync(new MouseEventArgs { Button = 0, ClientX = 210 });
        await cut.Find(".ex-header").ClickAsync(new MouseEventArgs { Button = 0, OffsetX = 50, OffsetY = 10 });

        Assert.NotNull(order);
        Assert.Equal(["C01", "C00", "C02", "C03", "C04", "C05"], order);
        // The painted order did not move — the Consumer pushes back a reordered Columns.
        Assert.Equal("C00", cut.FindAll(".ex-header-cell")[0].TextContent);
        // And the click that ended the drag did not sort.
        Assert.Null(sorted);
    }

    [Fact] // ADR-0011: a click that never crosses the threshold stays a sort
    public async Task A_plain_click_still_sorts_with_reorder_wired()
    {
        IReadOnlyList<SortSpec>? sorted = null;
        var cut = RenderGrid(onOrder: _ => { }, onSort: s => sorted = s);

        await cut.Find(".ex-header").MouseDownAsync(new MouseEventArgs { Button = 0, OffsetX = 50, OffsetY = 10, ClientX = 50 });
        await cut.Find(".ex-grid").MouseUpAsync(new MouseEventArgs { Button = 0, ClientX = 52 });
        await cut.Find(".ex-header").ClickAsync(new MouseEventArgs { Button = 0, OffsetX = 50, OffsetY = 10 });

        Assert.Equal([new SortSpec("C00", SortDirection.Ascending)], sorted);
    }

    [Fact] // ADR-0011: the indicator clamps at the pinned boundary rather than promising a refused drop
    public async Task The_indicator_clamps_at_the_pinned_boundary()
    {
        IReadOnlyList<string>? order = null;
        var cut = RenderGrid(onOrder: o => order = o, pinned: 2);

        // Drag scrollable column 2 far left, toward the pinned block.
        await cut.Find(".ex-header").MouseDownAsync(new MouseEventArgs { Button = 0, OffsetX = 250, OffsetY = 10, ClientX = 250 });
        await cut.Find(".ex-grid").MouseMoveAsync(new MouseEventArgs { Buttons = 1, ClientX = 10 });

        // The nearest allowed boundary is the pinned edge at column 2 (200px).
        Assert.Contains("left: 200px", cut.Find(".ex-drop-indicator").GetAttribute("style"));

        await cut.Find(".ex-grid").MouseUpAsync(new MouseEventArgs { Button = 0, ClientX = 10 });
        // Dropping at its own edge is the no-op drag: nothing is raised.
        Assert.Null(order);
    }
}
