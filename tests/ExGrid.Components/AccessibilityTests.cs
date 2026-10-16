using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The accessibility surface (ADR-0033): the root owns it, the counts and indices are
/// the absolute ones, and no element anywhere carries aria-selected. What needs a real
/// browser — one tab stop and the accessibility tree — is layer 3's.
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

    [Fact] // ADR-0033 / A11Y-20: a Prerendered grid says it is not ready, and takes no tab stop
    public void A_prerendered_grid_is_busy_and_out_of_the_tab_sequence()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));

        var root = RenderGrid().Find(".ex-grid");

        Assert.False(root.HasAttribute("tabindex"));
        Assert.Equal("true", root.GetAttribute("aria-busy"));
        Assert.Contains("ex-loading", root.ClassList);   // ADR-0029: the loading state
    }

    private sealed class NoListenerYet : BunitContext
    {
        // Interactive, but the module has not come back: on a circuit that is a module
        // import and a call away. Every call is left unanswered.
        public NoListenerYet()
        {
            Services.AddSingleton<Microsoft.JSInterop.IJSRuntime>(new Unanswered());
            SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        }

        private sealed class Unanswered : Microsoft.JSInterop.IJSRuntime
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
                new(new TaskCompletionSource<TValue>().Task);

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
                new(new TaskCompletionSource<TValue>().Task);
        }
    }

    [Fact] // ADR-0033 / A11Y-20: interactive but not yet listening is still Prerendered — a key would be lost
    public void An_interactive_grid_without_its_listener_is_still_busy()
    {
        using var context = new NoListenerYet();

        var root = context.Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(20))
            .Add(g => g.Columns, TestRows.Wide(3))).Find(".ex-grid");

        Assert.False(root.HasAttribute("tabindex"));
        Assert.Equal("true", root.GetAttribute("aria-busy"));
    }

    [Fact] // ADR-0033 / A11Y-20: once its listener is attached, the grid is one tab stop and not busy
    public void An_interactive_grid_is_one_tab_stop_and_not_busy()
    {
        var root = RenderGrid().Find(".ex-grid");

        Assert.Equal("0", root.GetAttribute("tabindex"));
        Assert.False(root.HasAttribute("aria-busy"));
        Assert.DoesNotContain("ex-loading", root.ClassList);
    }

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

    [Theory] // ADR-0033 / ADR-0015: header rows add to the full result's count and absolute indices, beyond Int32.
    [InlineData(0, true, false)]
    [InlineData(1, true, false)]
    [InlineData(2, true, false)]
    [InlineData(2, false, false)]
    [InlineData(2, false, true)]
    public async Task Header_rows_do_not_overflow_the_largest_paged_result(int headerRows, bool loaded, bool headings)
    {
        // Page the result as ADR-0015 requires: only one data row occupies scroll height.
        // Its absolute index still belongs to the whole result, including a Placeholder.
        const int last = int.MaxValue - 1;
        var cut = Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(loaded ? 1 : 0))
              .Add(g => g.WindowStart, last)
              .Add(g => g.TotalCount, int.MaxValue)
              .Add(g => g.Columns, TestRows.Wide(3))
              .Add(g => g.PageSize, 1)
              .Add(g => g.HideHeader, headerRows == 0)
              .Add(g => g.ViewportHeight, 200);
            if (headerRows == 2)
                ps.Add(g => g.HeaderGroups, (Columns.HeaderGroup[])[
                    new("Group", [TestRows.ColumnName(1), TestRows.ColumnName(2)])]);
            if (headings) ps.Add(g => g.RowHeadings, row => (row + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        });
        Assert.True(await cut.InvokeAsync(() => cut.Instance.PlaceSelectionAsync(
            new SelectionRange(last, 0, 1, 1), new CellPosition(last, 0), 0)));

        var expected = ((long)int.MaxValue + headerRows).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var row = Assert.Single(cut.FindAll(".ex-viewport .ex-row"));
        Assert.Multiple(
            () => Assert.Equal(expected, cut.Find(".ex-grid").GetAttribute("aria-rowcount")),
            () => Assert.Equal(expected, row.GetAttribute("aria-rowindex")),
            () => Assert.Equal(!loaded, row.ClassList.Contains("ex-placeholder")),
            () => Assert.Equal(headings, row.QuerySelector(".ex-row-heading") is not null));
    }

    private const int Million = 1_000_000;

    /// <summary>A million rows, 100 columns of 100px, 40 rows on screen, and a Window of
    /// <paramref name="count"/> rows from <paramref name="windowStart"/>.</summary>
    private IRenderedComponent<ExGrid<TestRow>> RenderMillion(
        int windowStart = 0, int count = 60, IReadOnlyList<Columns.HeaderGroup>? groups = null, bool hideHeader = false)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(count))
              .Add(g => g.WindowStart, windowStart)
              .Add(g => g.TotalCount, Million)
              .Add(g => g.Columns, TestRows.Wide(100))
              .Add(g => g.RowHeight, RowHeightPx)
              .Add(g => g.HeaderHeight, RowHeightPx)
              .Add(g => g.ViewportHeight, RowHeightPx + (40 * RowHeightPx))
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.HideHeader, hideHeader);
            if (groups is not null)
                ps.Add(g => g.HeaderGroups, groups);
        });

    /// <summary>Scrolls to <paramref name="row"/> and <paramref name="column"/> and lets the
    /// fling settle, so the real cells are painted (ADR-0004).</summary>
    private async Task ScrollToRowAsync(IRenderedComponent<ExGrid<TestRow>> cut, int row, int column = 0)
    {
        await ScrollToAsync(cut.Find(".ex-scroller"), row * RowHeightPx, column * 100);
        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));
    }

    /// <summary>The header's rows as ARIA reads them: each one's aria-rowindex, and the text of
    /// the column headers it holds.</summary>
    private static List<(string Index, string[] Headers)> HeaderRows(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        var header = cut.Find(".ex-header");
        var rows = header.GetAttribute("role") == "row" ? [header] : header.QuerySelectorAll("[role=row]").ToList();
        return [.. rows.Select(row => (row.GetAttribute("aria-rowindex")!,
            row.QuerySelectorAll("[role=columnheader]").Select(h => h.TextContent).ToArray()))];
    }

    /// <summary>The painted rows' aria-rowindex, top to bottom, Placeholders included.</summary>
    private static string[] PaintedIndices(IRenderedComponent<ExGrid<TestRow>> cut)
        => [.. cut.FindAll(".ex-viewport [role=row]").Select(row => row.GetAttribute("aria-rowindex")!)];

    private static string[] Numbers(int from, int count)
        => [.. Enumerable.Range(from, count).Select(n => n.ToString(System.Globalization.CultureInfo.InvariantCulture))];

    [Fact] // ADR-0033 / A11Y-2: aria-rowcount is the data rows' total and the header's row, not the DOM slice
    public void The_row_count_is_the_total_with_the_header_row()
    {
        var cut = RenderMillion();

        Assert.Equal("1000001", cut.Find(".ex-grid").GetAttribute("aria-rowcount"));
        Assert.Equal("100", cut.Find(".ex-grid").GetAttribute("aria-colcount"));
        // And the DOM holds a few dozen rows of the million.
        Assert.InRange(cut.FindAll(".ex-row").Count, 40, 60);
    }

    [Fact] // ADR-0033 / A11Y-3: at the top, the header row is 1 and the first data row is 2
    public void At_the_top_the_header_row_is_one_and_the_data_rows_follow_it()
    {
        var cut = RenderMillion();

        var header = Assert.Single(HeaderRows(cut));
        Assert.Equal("1", header.Index);
        Assert.Equal("row", cut.Find(".ex-header").GetAttribute("role"));
        var painted = PaintedIndices(cut);
        Assert.Equal(Numbers(2, painted.Length), painted);
        // The first row's first cell is its column's, 1-based.
        Assert.Equal("1", cut.FindAll(".ex-row")[0].QuerySelectorAll(".ex-cell")[0].GetAttribute("aria-colindex"));
    }

    [Fact] // ADR-0033 / A11Y-3: far down, a row's index is its position plus the header's row, absolute on both axes
    public async Task Far_down_a_rows_index_is_its_position_after_the_header_row()
    {
        // The Window holds rows 499,995 to 500,004: the rows below it are Placeholders, and
        // they are counted the same way.
        var cut = RenderMillion(windowStart: 499_995, count: 10);

        await ScrollToRowAsync(cut, 500_000, column: 30);

        var painted = PaintedIndices(cut);
        Assert.Equal("500002", painted[0]);
        Assert.Equal(Numbers(500_002, painted.Length), painted);
        Assert.Contains(cut.FindAll(".ex-viewport [role=row]"), row => row.GetAttribute("aria-busy") == "true");
        // The cell at row 500,000 and column 30 keeps its id; its row and column indices are ARIA's.
        var cell = cut.Find("[id$='r500000c30']");
        Assert.Equal("31", cell.GetAttribute("aria-colindex"));
        Assert.Equal("500002", cell.ParentElement!.GetAttribute("aria-rowindex"));
        Assert.Equal("1000001", cut.Find(".ex-grid").GetAttribute("aria-rowcount"));
    }

    [Fact] // ADR-0033 / ADR-0032: a Header Group band is a row of its own before the header row, and the data rows count after both
    public async Task A_header_group_band_is_a_row_before_the_header_row()
    {
        Columns.HeaderGroup[] groups = [new("CVA", [TestRows.ColumnName(1), TestRows.ColumnName(2), TestRows.ColumnName(3)])];
        var cut = RenderMillion(windowStart: 499_990, count: 60, groups: groups);

        Assert.Equal("rowgroup", cut.Find(".ex-header").GetAttribute("role"));
        Assert.False(cut.Find(".ex-header").HasAttribute("aria-rowindex"));
        var rows = HeaderRows(cut);
        Assert.Equal(["1", "2"], rows.Select(r => r.Index));
        Assert.Equal(["CVA"], rows[0].Headers);
        Assert.Contains(TestRows.ColumnName(0), rows[1].Headers);
        // The band's cell names where it stands among the columns, and how many it spans.
        var cva = cut.Find(".ex-header-group");
        Assert.Equal("2", cva.GetAttribute("aria-colindex"));
        Assert.Equal("3", cva.GetAttribute("aria-colspan"));
        Assert.False(cva.HasAttribute("aria-rowspan"));
        Assert.Equal("1000002", cut.Find(".ex-grid").GetAttribute("aria-rowcount"));

        await ScrollToRowAsync(cut, 500_000);

        var painted = PaintedIndices(cut);
        Assert.Equal(Numbers(500_003, painted.Length), painted);
    }

    [Fact] // ADR-0033 / ADR-0032: each tier of Header Groups is a row, top first; a rectangle over two tiers spans two rows
    public void Each_tier_is_a_row_top_first()
    {
        Columns.HeaderGroup[] groups =
        [
            new("CVA", [TestRows.ColumnName(1), TestRows.ColumnName(2), TestRows.ColumnName(3)]),
            new("Adjustments", [TestRows.ColumnName(1), TestRows.ColumnName(2), TestRows.ColumnName(3), TestRows.ColumnName(4)], tier: 2),
            new("Tall", [TestRows.ColumnName(5), TestRows.ColumnName(6)], tier: 2, tierSpan: 2),
        ];
        var cut = RenderMillion(groups: groups);

        var rows = HeaderRows(cut);
        Assert.Equal(["1", "2", "3"], rows.Select(r => r.Index));
        Assert.Equal(["Adjustments", "Tall"], rows[0].Headers);
        Assert.Equal(["CVA"], rows[1].Headers);
        Assert.Contains(TestRows.ColumnName(0), rows[2].Headers);
        Assert.Equal("2", Assert.Single(cut.FindAll(".ex-header-group"), g => g.TextContent == "Tall").GetAttribute("aria-rowspan"));
        Assert.Equal("4", PaintedIndices(cut)[0]);
        Assert.Equal("1000003", cut.Find(".ex-grid").GetAttribute("aria-rowcount"));
    }

    [Fact] // ADR-0033: a band declared after the first render moves every painted row's index with it
    public void A_band_declared_later_moves_every_rows_index()
    {
        var cut = RenderMillion();
        Assert.Equal("2", PaintedIndices(cut)[0]);

        cut.Render(ps => ps.Add(g => g.HeaderGroups, (Columns.HeaderGroup[])[
            new("CVA", [TestRows.ColumnName(1), TestRows.ColumnName(2)])]));

        var painted = PaintedIndices(cut);
        Assert.Equal(Numbers(3, painted.Length), painted);
        Assert.Equal("1000002", cut.Find(".ex-grid").GetAttribute("aria-rowcount"));
    }

    [Fact] // ADR-0033 / ADR-0050: with the header hidden there is no header row; the first data row is 1
    public async Task With_the_header_hidden_the_first_data_row_is_one()
    {
        var cut = RenderMillion(windowStart: 499_990, count: 60, hideHeader: true);

        Assert.Empty(cut.FindAll(".ex-header"));
        Assert.Equal("1000000", cut.Find(".ex-grid").GetAttribute("aria-rowcount"));
        Assert.Equal("1", PaintedIndices(cut)[0]);

        await ScrollToRowAsync(cut, 500_000);

        Assert.Equal("500001", PaintedIndices(cut)[0]);
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

        // A bare Focus move from one cell to another announces nothing: the region is
        // unchanged (A11Y-10).
        await ClickCellAsync(cut, 50, 10);
        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));
        var afterCollapse = cut.Find(".ex-announce").TextContent;
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, false, false, false, false));
        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));
        Assert.Equal(afterCollapse, cut.Find(".ex-announce").TextContent);
    }

    [Theory] // ADR-0033: a collapse to one cell announces nothing, and leaves no sentence naming the range it replaced
    [InlineData("arrow")]
    [InlineData("click")]
    public async Task A_collapse_to_one_cell_empties_the_range_sentence(string how)
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 10);
        await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = 150, OffsetY = 50 });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, OffsetX = 150, OffsetY = 50 });
        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));
        Assert.Contains("3 rows by 2 columns selected", cut.Find(".ex-announce").TextContent);

        if (how == "arrow")
            await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, false, false, false, false));
        else
            await ClickCellAsync(cut, 250, 90);

        // Emptied at once, not after the settle: the sentence is untrue from this render on.
        Assert.Equal("", cut.Find(".ex-announce").TextContent);
        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));
        Assert.Equal("", cut.Find(".ex-announce").TextContent);
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
        var root = Js.RootReferenceId;
        var scroller = cut.Find(".ex-scroller");

        Assert.Equal("-1", scroller.GetAttribute("tabindex"));

        await scroller.FocusAsync(new Microsoft.AspNetCore.Components.Web.FocusEventArgs());

        var focused = Js.Focused;
        Assert.Equal(root, focused.Last());
    }

    [Fact] // ADR-0021/0018: the keyboard goes back to the root only through the handle's conditional reclaim, never Blazor's unconditional focus
    public async Task The_root_is_asked_for_through_the_conditional_reclaim_only()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, TestRows.Columns()));
        var root = Js.RootReferenceId;

        await cut.Find(".ex-scroller").FocusAsync(new Microsoft.AspNetCore.Components.Web.FocusEventArgs());

        // The browser decides whether DOM focus is still this grid's to take (a second grid
        // pressed a round trip earlier keeps it); the core only asks.
        Assert.Single(Js.FocusReclaimed.Invocations);
        Assert.DoesNotContain(JSInterop.Invocations, i => i.Identifier == GridJSInterop.BlazorFocus
            && ((ElementReference)i.Arguments[0]!).Id == root);
    }

    [Fact] // ADR-0033 (note of 2026-10-03) / A11Y-20: focus reaching the scroller before the root is a tab stop — a press on rows painted before the attach's render — goes to the root once a render has made it one, and is asked for once
    public async Task Focus_reaching_the_scroller_before_the_root_is_a_tab_stop_goes_to_it_once_it_is()
    {
        var listening = Js.UnansweredMetaIsPrimary();
        var tabIndexAtReclaim = new List<string?>();
        IRenderedComponent<ExGrid<TestRow>>? cut = null;
        Js.OnFocusReclaimed(() => tabIndexAtReclaim.Add(cut!.Find(".ex-grid").GetAttribute("tabindex")));
        cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, TestRows.Columns()));
        Assert.Null(cut.Find(".ex-grid").GetAttribute("tabindex"));

        await cut.Find(".ex-scroller").FocusAsync(new FocusEventArgs());

        // The root could not take it yet: its own focus would do nothing, so nothing is asked...
        Assert.Empty(Js.FocusReclaimed.Invocations);

        // ...until the attach has finished and its render has made the root a tab stop. The answer
        // posts the rest of the attach to the dispatcher, and the reading is taken once the
        // dispatcher is idle: once, whenever the process gets round to it, never against a clock.
        await cut.InvokeAsync(() => listening.SetResult(false));
        var reclaimed = await ReadWhenIdleAsync(() => (
            Asked: Js.FocusReclaimed.Invocations.Select(i => (bool)i.Arguments[0]!).ToArray(),
            TabIndex: tabIndexAtReclaim.ToArray(),
            Focused: Js.Focused.ToArray(),
            Root: Js.RootReferenceId));
        // Asked through the hand-back's condition, never by the root's own focus (ADR-0018).
        Assert.Equal([false], reclaimed.Asked);
        Assert.Equal("0", Assert.Single(reclaimed.TabIndex));
        Assert.Equal(reclaimed.Root, reclaimed.Focused[^1]);

        // Owed once: the renders after it ask nothing more.
        cut.Render();
        Assert.Single(await ReadWhenIdleAsync(() => Js.FocusReclaimed.Invocations.ToArray()));
    }
}
