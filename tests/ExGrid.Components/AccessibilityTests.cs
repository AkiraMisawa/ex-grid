using Bunit;
using Microsoft.AspNetCore.Components;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The accessibility surface (ADR-0033): the root owns it, the counts and indices are
/// the absolute ones, and no element anywhere carries aria-selected. What needs a real
/// browser — one tab stop, the live region against a screen reader — is layer 3's.
/// </summary>
public class AccessibilityTests : GridTestContext
{
    private const double RowHeightPx = 20;

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        int rows = 200, int? totalCount = 200, Action<GridSelection>? onSelectionChanged = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(rows))
              .Add(g => g.TotalCount, totalCount)
              .Add(g => g.Columns, TestRows.Wide(100))
              .Add(g => g.RowHeight, RowHeightPx)
              .Add(g => g.ViewportHeight, 100)
              .Add(g => g.ViewportWidth, 350);
            if (onSelectionChanged is not null)
                ps.Add(g => g.SelectionChanged, onSelectionChanged);
        });

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    [Fact] // A11Y-1: the grid pattern's roles, on every painted element of each kind
    public void The_roles_are_present()
    {
        var cut = RenderGrid();

        Assert.Equal("grid", cut.Find(".ex-grid").GetAttribute("role"));
        Assert.Equal("true", cut.Find(".ex-grid").GetAttribute("aria-multiselectable"));
        Assert.All(cut.FindAll(".ex-header-cell"), h => Assert.Equal("columnheader", h.GetAttribute("role")));
        Assert.All(cut.FindAll(".ex-row"), r => Assert.Equal("row", r.GetAttribute("role")));
        Assert.All(cut.FindAll(".ex-cell"), c => Assert.Equal("gridcell", c.GetAttribute("role")));
    }

    [Fact] // A11Y-2: the counts state the total, not the DOM slice
    public void The_counts_are_the_totals()
    {
        var cut = RenderGrid();

        Assert.Equal("200", cut.Find(".ex-grid").GetAttribute("aria-rowcount"));
        Assert.Equal("100", cut.Find(".ex-grid").GetAttribute("aria-colcount"));
        // And the DOM holds far fewer than 200 rows.
        Assert.True(cut.FindAll(".ex-row").Count < 20);
    }

    [Fact] // A11Y-3: indices are absolute on both axes, 1-based as ARIA counts
    public async Task Indices_are_absolute_after_scrolling()
    {
        var cut = RenderGrid();

        await ScrollToAsync(cut.Find(".ex-scroller"), 100 * RowHeightPx, 0);
        // A hundred rows at once is a fling; the real cells return on settle (ADR-0004).
        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));

        var first = cut.FindAll(".ex-row")[0];
        Assert.Equal("101", first.GetAttribute("aria-rowindex"));
        Assert.Equal("1", first.QuerySelectorAll(".ex-cell")[0].GetAttribute("aria-colindex"));
    }

    [Fact] // A11Y-5: aria-activedescendant names the Focus cell, instance-prefixed, and resolves
    public async Task Activedescendant_names_the_focus_cell()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 150, 30); // row 1, column 1

        var id = cut.Find(".ex-grid").GetAttribute("aria-activedescendant");

        Assert.NotNull(id);
        Assert.EndsWith("r1c1", id);
        Assert.NotNull(cut.Find($"[id='{id}']"));
    }

    [Fact] // A11Y-5: two instances on one page never share an id
    public void Two_grids_carry_distinct_id_prefixes()
    {
        var rowsA = TestRows.Window();
        var rowsB = TestRows.Window();
        var columns = TestRows.Columns();
        var cut = Render(builder =>
        {
            builder.OpenComponent<ExGrid<TestRow>>(0);
            builder.AddComponentParameter(1, "Window", (IReadOnlyList<TestRow>)rowsA);
            builder.AddComponentParameter(2, "Columns", (IReadOnlyList<GridColumn<TestRow>>)columns);
            builder.CloseComponent();
            builder.OpenComponent<ExGrid<TestRow>>(3);
            builder.AddComponentParameter(4, "Window", (IReadOnlyList<TestRow>)rowsB);
            builder.AddComponentParameter(5, "Columns", (IReadOnlyList<GridColumn<TestRow>>)columns);
            builder.CloseComponent();
        });

        var ids = cut.FindAll(".ex-cell[id]").Select(c => c.GetAttribute("id")!).ToList();
        Assert.NotEmpty(ids);
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact] // A11Y-6's layer-2 half: the Focus scrolled out of the Window clears the attribute
    public async Task Activedescendant_clears_when_the_focus_leaves_the_paint()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 10); // row 0

        await ScrollToAsync(cut.Find(".ex-scroller"), 100 * RowHeightPx, 0);
        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));

        Assert.Null(cut.Find(".ex-grid").GetAttribute("aria-activedescendant"));

        await ScrollToAsync(cut.Find(".ex-scroller"), 0, 0);
        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));
        Assert.EndsWith("r0c0", cut.Find(".ex-grid").GetAttribute("aria-activedescendant"));
    }

    [Fact] // A11Y-7: a #### cell's accessible name is the real value
    public void A_hashed_cells_accessible_name_is_the_value()
    {
        GridColumn<TestRow>[] columns =
        [
            new("Amount", ColumnType.Number, r => r.Amount,
                width: new Columns.ColumnWidthSpec(Columns.ColumnWidth.Fixed(45))),
        ];
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, (TestRow[])[new() { Amount = 123456789m }])
            .Add(g => g.Columns, columns));

        var cell = cut.Find(".ex-cell");
        Assert.StartsWith("#", cell.TextContent);
        Assert.Equal("123456789", cell.GetAttribute("aria-label"));
    }

    [Fact] // FN-20 / A11Y-7's sibling: the focused cell's full value is available behind ####
    public async Task The_focused_value_is_the_raw_value_never_the_hashes()
    {
        GridColumn<TestRow>[] columns =
        [
            new("Amount", ColumnType.Number, r => r.Amount,
                width: new Columns.ColumnWidthSpec(Columns.ColumnWidth.Fixed(45))),
        ];
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, (TestRow[])[new() { Amount = 123456789m }])
            .Add(g => g.Columns, columns));
        Assert.Null(cut.Instance.GetFocusedValue());

        await cut.Find(".ex-viewport").MouseDownAsync(
            new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 20, OffsetY = 10 });

        Assert.StartsWith("#", cut.Find(".ex-cell").TextContent);
        Assert.Equal(123456789m, cut.Instance.GetFocusedValue());
    }

    [Fact] // A11Y-8: no element carries aria-selected, in any selection state
    public async Task Nothing_carries_aria_selected()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 10);
        // A 20×2 drag across the viewport.
        await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = 150, OffsetY = 90 });

        Assert.DoesNotContain("aria-selected", cut.Markup);
    }

    [Fact] // A11Y-11: rows without data carry aria-busy and read no values
    public void Placeholder_rows_carry_aria_busy()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, (TestRow[])[])
            .Add(g => g.TotalCount, 200)
            .Add(g => g.Columns, TestRows.Wide(3))
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, 100)
            .Add(g => g.ViewportWidth, 350));

        var placeholders = cut.FindAll(".ex-placeholder");
        Assert.NotEmpty(placeholders);
        Assert.All(placeholders, p => Assert.Equal("true", p.GetAttribute("aria-busy")));
    }

    [Fact] // A11Y-12: the overlay is paint — aria-hidden, never an unnamed child of the grid
    public async Task The_selection_overlay_is_aria_hidden()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 10);

        Assert.Equal("true", cut.Find(".ex-selection").GetAttribute("aria-hidden"));
        Assert.Equal("true", cut.Find(".ex-selection-pinned").GetAttribute("aria-hidden"));
    }

    [Fact] // A11Y-9/10: one announcement per settled selection; a bare Focus move stays silent
    public async Task The_live_region_announces_once_per_settled_selection()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 10);
        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));

        // A single cell is the Focus by another name: nothing is announced.
        Assert.Equal("", cut.Find(".ex-announce").TextContent);

        // A drag across many rectangles announces the destination once.
        for (var y = 20; y <= 90; y += 10)
            await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = 150, OffsetY = y });
        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));

        var sentence = cut.Find(".ex-announce").TextContent;
        Assert.Contains("5 rows by 2 columns selected", sentence);

        // Arrow keys without Shift collapse to the Focus: the region is unchanged.
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, false, false, false, false));
        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));
        Assert.Equal(sentence, cut.Find(".ex-announce").TextContent);
    }

    [Fact] // A11Y-14: announcing costs no row render — the counts match a drag without it
    public async Task Announcing_costs_no_row_render()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 10);
        var counts = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList();

        await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = 150, OffsetY = 90 });
        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));

        Assert.Equal(counts, cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList());
    }

    [Fact] // ADR-0033 / A11Y-4: the scroller is out of the tab sequence, and focus reaching it goes to the root
    public async Task The_scroller_is_no_tab_stop_and_hands_focus_to_the_root()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, TestRows.Columns()));
        var root = cut.Find(".ex-grid").GetAttribute("blazor:elementreference");
        var scroller = cut.Find(".ex-scroller");

        Assert.Equal("-1", scroller.GetAttribute("tabindex"));

        await scroller.FocusAsync(new Microsoft.AspNetCore.Components.Web.FocusEventArgs());

        var focused = JSInterop.Invocations
            .Where(i => i.Identifier == "Blazor._internal.domWrapper.focus")
            .Select(i => ((ElementReference)i.Arguments[0]!).Id)
            .ToArray();
        Assert.Equal(root, focused.Last());
    }
}
