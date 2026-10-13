using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ST-1: after any sequence of operations the invariants hold. A scripted randomised
/// sequence — clicks, drags with the button held, every claimed key, scrolls, sorts, filters,
/// row-height changes, Viewport resizes, density steps, a pager put on, resized and turned
/// (ADR-0015), and the Layout Ceiling the browser reports moved low enough to compress the
/// scroll (ADR-0053) — asserting after each step that
/// every selection rectangle lies inside the extent, the Focus and the Extent lie inside the
/// range holding the Focus (ADR-0052), the cell count is the sum of rectangle areas and so is
/// the count the status line displays, every scroll offset the grid writes lies within
/// [0, max], the painted rows are exactly <c>ViewportGeometry.SliceAt</c> of where the browser
/// stands — the page's rows, at their absolute positions, under the ceiling's compression — the
/// page on screen is one the result holds, a page the pager turned is the next one and starts at the
/// top, the spacer is the arithmetic's height, and nothing throws. The browser stops where the
/// spacer the grid laid out stops, under the ceiling. Each seed must act often enough paged, past
/// the first page, compressed, compressed without a pager and compressed past the first page, and
/// must turn a page while compressed, or the run fails; thirty steps halfway through are spent where
/// paging and compression meet.
///
/// <para>The painted rows are judged against <c>ViewportGeometry</c> itself, so a fault inside
/// <c>ViewportGeometry</c> — <c>SliceAt</c>, <c>ContentOffsetAt</c> under compression — is invisible
/// here by construction. Layer 1 pins that arithmetic independently (<c>ViewportCompressionTests</c>,
/// <c>ViewportGeometryTests</c>); this test pins that the grid paints by it.</para> The browser is modelled: it takes every offset the grid writes,
/// clamps any position the content no longer reaches, and reports each move with a scroll
/// event. The seed is in the test name's data so a failure replays exactly.
/// </summary>
public class ConsistencyTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
        new("AsOf", ColumnType.Date, r => r.AsOf, width: Fixed100),
        new("Active", ColumnType.Boolean, r => r.Active, width: Fixed100),
    ];

    // ST-1: ≥500 randomised operations at 10³ and 10⁶ rows (§22 Step 5); the seeds are
    // recorded here. The 10⁶ case takes about a minute, and nearly all of it is the
    // reference source re-sorting and re-filtering a million rows (~0.5 s a sort) — the
    // Consumer's work, not the grid's (ADR-0001). The grid's own steps stay in
    // milliseconds at either scale. So it runs only when asked for, with
    // EXGRID_ST1_MILLION=1 (decided 2026-09-24): an ordinary run skips it by name, and
    // Step 5 is the run that sets it.
    private const string MillionSwitch = "EXGRID_ST1_MILLION";

    [Theory]
    [InlineData(20260901, 200)]
    [InlineData(424242, 200)]
    [InlineData(20260901, 1_000)]
    [InlineData(424242, 1_000)]
    [InlineData(20260901, 1_000_000)]
    public async Task Five_hundred_random_operations_leave_every_invariant_standing(int seed, int totalRows)
    {
        if (totalRows >= 1_000_000)
            Assert.SkipUnless(Environment.GetEnvironmentVariable(MillionSwitch) == "1",
                $"ST-1 at 10⁶ rows runs with {MillionSwitch}=1 (Definition of Done §22 Step 5)");

        var random = new Random(seed);
        var source = GridSource.From(TestRows.Many(totalRows));
        GridSelection selection = GridSelection.Empty;
        // The layout the grid is handed, which the browser model below reads as the grid does.
        // Row and header heights are declared, so a Density preset moves neither (ADR-0028).
        var layout = new Layout(RowHeightPx: 20, ViewportHeightPx: 140, ViewportWidthPx: 350);
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, layout.RowHeightPx)
            .Add(g => g.HeaderHeight, HeaderHeightPx)
            .Add(g => g.ViewportHeight, layout.ViewportHeightPx)
            .Add(g => g.ViewportWidth, layout.ViewportWidthPx)
            .Add(g => g.SelectionChanged, (GridSelection s) => selection = s));
        var browser = new Browser(this, cut);

        string[] keys =
        [
            "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Home", "End",
            "PageUp", "PageDown", "Enter", "Tab", " ", "a", "Escape", "F2", "5",
            ".", "Backspace",
        ];

        // How often the run stood where the two newer paths are: paged, past the first page, and
        // compressed under a Layout Ceiling — counted so a seed that never reaches them fails.
        int paged = 0, pastFirstPage = 0, compressed = 0, compressedUnpaged = 0, compressedPastFirstPage = 0, turnedWhileCompressed = 0;
        for (var step = 0; step < 500; step++)
        {
            // Halfway, the run is put where paging and compression meet — a pager of 60 rows under
            // an 800px ceiling — and acts there for thirty steps (clicks, drags, keys, scrolls, page
            // turns), so every seed reaches it whatever it drew before.
            var operation = step switch
            {
                250 => 13,
                > 250 and < 280 => new[] { 0, 1, 2, 3, 11, 11 }[random.Next(6)],
                _ => random.Next(13),
            };
            int? turnedTo = null;
            var compressedBefore = GeometryOf(layout, source, cut).IsCompressed;
            switch (operation)
            {
                case 0: // click somewhere on screen
                    await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
                    {
                        Button = 0,
                        Buttons = 1,
                        OffsetX = random.Next(0, 400),
                        OffsetY = random.Next(0, 120),
                    });
                    await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
                    break;
                case 1: // a drag: press, move with the button held, and usually release
                    await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
                    {
                        Button = 0,
                        Buttons = 1,
                        OffsetX = random.Next(0, 400),
                        OffsetY = random.Next(0, 120),
                    });
                    // The move listener stands only while a press armed a drag.
                    var (toX, toY) = (random.Next(0, 400), random.Next(0, 120));
                    if (cut.Find(".ex-viewport").GetAttribute("onmousemove") is not null)
                        await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = toX, OffsetY = toY });
                    if (random.Next(4) != 0)
                        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
                    break;
                case 2: // a claimed key, sometimes chorded
                    await cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
                        keys[random.Next(keys.Length)],
                        ctrl: random.Next(4) == 0,
                        shift: random.Next(3) == 0,
                        alt: false, meta: false, metaIsPrimary: false));
                    break;
                case 3: // the user scrolls to an arbitrary offset, which the browser clamps
                    await browser.ScrollAsync(
                        random.NextDouble() * 1.1 * MaxScrollTop(layout, source, cut),
                        random.Next(0, 100), layout, source);
                    break;
                case 4: // sort by a random column, sometimes back to none
                    source.OnSortChanged(random.Next(3) == 0
                        ? []
                        : [new SortSpec(Columns()[random.Next(2)].Name, (SortDirection)random.Next(2))]);
                    break;
                case 5: // filter on Active, or clear
                    source.OnFilterChanged(random.Next(2) == 0
                        ? null
                        : new GridFilter(new Dictionary<string, FilterSpec>
                        {
                            ["Active"] = new([new FilterClause(FilterOperator.Equals, random.Next(2) == 0)]),
                        }));
                    break;
                case 6: // a geometry change: the row height
                    layout = layout with { RowHeightPx = 18d + random.Next(4) * 4 };
                    cut.Render(ps => ps.Add(g => g.RowHeight, layout.RowHeightPx));
                    break;
                case 7: // the edge-band clock ticks while whatever is armed runs
                    await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(50)));
                    break;
                case 8: // a resize of the Viewport, on either axis
                    layout = layout with
                    {
                        ViewportHeightPx = new[] { 100d, 140d, 200d }[random.Next(3)],
                        ViewportWidthPx = new[] { 250d, 350d, 500d }[random.Next(3)],
                    };
                    cut.Render(ps => ps
                        .Add(g => g.ViewportHeight, layout.ViewportHeightPx)
                        .Add(g => g.ViewportWidth, layout.ViewportWidthPx));
                    break;
                case 9: // a density step
                    cut.Render(ps => ps.Add(g => g.Density, (GridDensity)random.Next(3)));
                    break;
                case 10: // a pager put on, taken off, or set to another page size (ADR-0015)
                    layout = layout with { PageSize = new int?[] { null, null, 25, 60 }[random.Next(4)] };
                    cut.Render(ps => ps.Add(g => g.PageSize, layout.PageSize));
                    break;
                case 11: // the pager turned a page back or on, where it can be
                    if (cut.FindAll(".ex-pager button") is { Count: 2 } pager
                        && random.Next(2) is var direction && !pager[direction].HasAttribute("disabled"))
                    {
                        // Known before the press, not read back from the pager afterwards.
                        turnedTo = PageOf(layout, source, cut).Index + (direction == 0 ? -1 : 1);
                        await pager[direction].ClickAsync(new MouseEventArgs());
                    }
                    break;
                case 13: // scripted: a pager and a low ceiling at once
                    layout = layout with { PageSize = 60, CeilingPx = 800 };
                    cut.Render(ps => ps.Add(g => g.PageSize, layout.PageSize));
                    await cut.InvokeAsync(() => cut.Instance.OnLayoutCeilingAsync(800));
                    break;
                case 12: // the browser reports another Layout Ceiling: a zoom, a scale (ADR-0053)
                    layout = layout with { CeilingPx = new[] { ViewportGeometry.MaxScrollHeightPx, 1_500d, 800d, 6_000d }[random.Next(4)] };
                    var ceiling = layout.CeilingPx;
                    await cut.InvokeAsync(() => cut.Instance.OnLayoutCeilingAsync(ceiling));
                    break;
            }

            // Whatever the grid wrote, the browser now is, and says so; and a position the
            // content no longer reaches is clamped by the browser, which says so too.
            await browser.FollowAsync(layout, source);
            AssertInvariants(cut, selection, source, layout, browser,
                $"seed {seed}, step {step}, operation {operation}, {layout}, {source.TotalCount} rows, browser at {browser.Top}");
            // A page turned by the pager is the page next to the one before, and starts at the top
            // (ADR-0015): the turn is the pager's scroll.
            if (turnedTo is { } expectedPage)
            {
                Assert.True(PageOf(layout, source, cut).Index == expectedPage && browser.Top == 0,
                    $"turned to page {PageOf(layout, source, cut).Index + 1} at {browser.Top}, expected page {expectedPage + 1} at the top: seed {seed}, step {step}");
                if (compressedBefore)
                    turnedWhileCompressed++;
            }
            // Counted where an operation acted in the state, not where the run merely stood in it.
            var acting = operation is 0 or 1 or 2 or 3 or 11;
            var compressedNow = GeometryOf(layout, source, cut).IsCompressed;
            if (acting && layout.PageSize is not null)
            {
                paged++;
                if (PageOf(layout, source, cut).Index > 0)
                    pastFirstPage++;
            }
            if (acting && compressedNow)
            {
                compressed++;
                if (layout.PageSize is null)
                    compressedUnpaged++;
                else if (PageOf(layout, source, cut).Index > 0)
                    compressedPastFirstPage++;
            }
        }

        Assert.True(paged >= 20 && pastFirstPage >= 20 && compressed >= 20 && compressedUnpaged >= 5
            && compressedPastFirstPage >= 1 && turnedWhileCompressed >= 1,
            $"seed {seed} acted paged {paged}, past the first page {pastFirstPage}, compressed {compressed}, "
            + $"compressed without a pager {compressedUnpaged}, compressed past the first page {compressedPastFirstPage}, "
            + $"turned a page compressed {turnedWhileCompressed} times");
    }

    private const double HeaderHeightPx = 20;
    private const double ColumnsWidthPx = 4 * 100;

    /// <summary>What the grid is handed and told: its geometry parameters, the page size, and the
    /// Layout Ceiling the browser reports (ADR-0053; the grid's own default until one is told).</summary>
    private sealed record Layout(double RowHeightPx, double ViewportHeightPx, double ViewportWidthPx,
        int? PageSize = null, double CeilingPx = ViewportGeometry.MaxScrollHeightPx);

    /// <summary>The page on screen, as the pager says it: where it starts and how many rows it
    /// holds — the whole result without a pager (ADR-0015).</summary>
    private static (int Index, int Start, int Rows) PageOf(Layout layout, InMemoryGridSource<TestRow> source, IRenderedComponent<ExGrid<TestRow>> cut)
    {
        var total = source.TotalCount!.Value;
        if (layout.PageSize is not { } size)
            return (0, 0, total);
        var text = cut.Find(".ex-pager span").TextContent;
        var index = int.Parse(text[..text.IndexOf(' ', StringComparison.Ordinal)], System.Globalization.CultureInfo.InvariantCulture) - 1;
        var start = index * size;
        return (index, start, Math.Clamp(total - start, 0, size));
    }

    /// <summary>The arithmetic the grid paints by, as ADR-0053 builds it: the page's rows under the
    /// header, in what the Layout Ceiling leaves them once the header band is taken from it.</summary>
    private static ViewportGeometry GeometryOf(Layout layout, InMemoryGridSource<TestRow> source, IRenderedComponent<ExGrid<TestRow>> cut)
        => new(layout.RowHeightPx, layout.ViewportHeightPx - HeaderHeightPx, PageOf(layout, source, cut).Rows,
            Math.Max(1, layout.CeilingPx - HeaderHeightPx));

    private static double MaxScrollTop(Layout layout, InMemoryGridSource<TestRow> source, IRenderedComponent<ExGrid<TestRow>> cut)
        => GeometryOf(layout, source, cut).MaxScrollTopPx;

    private static double MaxScrollLeft(Layout layout) => Math.Max(0, ColumnsWidthPx - layout.ViewportWidthPx);

    /// <summary>The scroller as a browser keeps it: where it is, every offset the grid wrote
    /// (a re-anchoring write among them) taken as the new position, and every position clamped into what the
    /// content reaches — each move reported back with a scroll event, as a browser does.</summary>
    private sealed class Browser(GridTestContext context, IRenderedComponent<ExGrid<TestRow>> cut)
    {
        private int _writesSeen;
        private int _anchorsSeen;

        public double Top { get; private set; }

        public double Left { get; private set; }

        /// <summary>Every offset the grid has written since the last look, for the bounds check.</summary>
        public List<(double Top, double Left)> NewWrites { get; } = [];

        /// <summary>Where a browser stops the scroller: the spacer the grid laid out, no taller than
        /// the Layout Ceiling, less the Viewport — read from the page, not from the arithmetic.</summary>
        private double MaxTop(Layout layout)
        {
            var style = cut.Find(".ex-spacer").GetAttribute("style")!;
            var match = System.Text.RegularExpressions.Regex.Match(style, @"height: ([0-9.]+)px");
            var spacer = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            return Math.Max(0, Math.Min(spacer, layout.CeilingPx) - layout.ViewportHeightPx);
        }

        public async Task ScrollAsync(double top, double left, Layout layout, InMemoryGridSource<TestRow> source)
        {
            _ = source;
            Top = Math.Clamp(top, 0, MaxTop(layout));
            Left = Math.Clamp(left, 0, MaxScrollLeft(layout));
            await ReportAsync();
        }

        public async Task FollowAsync(Layout layout, InMemoryGridSource<TestRow> source)
        {
            NewWrites.Clear();
            for (var round = 0; round < 4; round++)
            {
                var moved = false;
                // A re-anchoring write (ADR-0028/0053) moves the top only; a reveal's write moves
                // both and is made at the render that follows, so it lands after the anchor.
                // Counted per kind: the interop's record groups its invocations by name.
                var anchors = context.Js.Anchored;
                if (anchors.Count > _anchorsSeen)
                {
                    NewWrites.AddRange(anchors.Skip(_anchorsSeen).Select(a => (a.Top, double.NaN)));
                    _anchorsSeen = anchors.Count;
                    Top = anchors[^1].Top;
                    moved = true;
                }
                var writes = context.Js.ScrolledTo.Where(w => !double.IsNaN(w.Left)).ToList();
                if (writes.Count > _writesSeen)
                {
                    NewWrites.AddRange(writes.Skip(_writesSeen));
                    _writesSeen = writes.Count;
                    (Top, Left) = writes[^1];
                    moved = true;
                }
                _ = source;
                var top = Math.Clamp(Top, 0, MaxTop(layout));
                var left = Math.Clamp(Left, 0, MaxScrollLeft(layout));
                moved |= top != Top || left != Left;
                (Top, Left) = (top, left);
                if (!moved)
                    return;
                await ReportAsync();
            }
        }

        private async Task ReportAsync()
        {
            await context.ScrollToAsync(cut.Find(".ex-scroller"), Top, Left);
            await cut.InvokeAsync(() => context.Clock.Advance(TimeSpan.FromMilliseconds(200)));
        }
    }

    // ---- ST-2: the grid holds no committed data ---------------------------------------------------
    //
    // An editor opened with F2 and not typed into holds the cell's committed value as its text: a copy
    // of committed data, not half-typed text, so it closes when its row leaves the Window. An editor
    // with typed text stays, as ADR-0142 has it (ADR-0007's note of 2026-10-11).

    [Theory] // ADR-0001 / ADR-0007 / ST-2: after any sequence, an empty Window leaves the grid nothing to paint, and nothing throws
    [InlineData(20261011)]
    [InlineData(424242)]
    public async Task ST2_after_any_sequence_an_empty_window_leaves_nothing_to_paint(int seed)
    {
        var random = new Random(seed);
        var all = TestRows.Many(1_000);
        var asked = new List<RowRange>();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, all[..60])
            .Add(g => g.WindowStart, 0)
            .Add(g => g.TotalCount, all.Length)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 140)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.PinnedColumnCount, 1)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> _) => { })
            .Add(g => g.OnRangeNeeded, asked.Add));

        string[] keys = ["ArrowDown", "ArrowRight", "End", "PageDown", "Enter", "Tab", " ", "a", "Escape", "F2", "5", "Backspace"];
        for (var step = 0; step < 200; step++)
        {
            switch (random.Next(6))
            {
                case 0: // a press somewhere on screen
                    await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
                    {
                        Button = 0, Buttons = 1, OffsetX = random.Next(0, 400), OffsetY = random.Next(0, 120),
                    });
                    await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
                    break;
                case 1: // a key, sometimes chorded — an edit opened and typed into among them
                    await cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
                        keys[random.Next(keys.Length)], ctrl: random.Next(4) == 0, shift: random.Next(3) == 0,
                        alt: false, meta: false, metaIsPrimary: false));
                    break;
                case 2: // a scroll, and the settle that follows a fling
                    await ScrollToAsync(cut.Find(".ex-scroller"), random.NextDouble() * 18_000, random.Next(0, 100));
                    await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));
                    break;
                case 3: // the Consumer answers the last Range Request with the rows around it
                    if (asked.Count > 0)
                    {
                        var range = asked[^1];
                        var start = Math.Max(0, range.Start - 20);
                        var end = Math.Min(all.Length, range.Start + range.Count + 20);
                        cut.Render(ps => ps.Add(g => g.Window, all[start..end]).Add(g => g.WindowStart, start));
                    }
                    break;
                case 4: // a copy, whatever the selection
                    _ = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());
                    break;
                case 5: // a geometry change
                    cut.Render(ps => ps.Add(g => g.RowHeight, 18d + (random.Next(4) * 4)));
                    break;
            }
        }

        // The Consumer's state removed: nothing to show.
        cut.Render(ps => ps
            .Add(g => g.Window, Array.Empty<TestRow>())
            .Add(g => g.WindowStart, 0)
            .Add(g => g.TotalCount, 0));

        AssertNothingPainted(cut);
    }

    [Fact] // ADR-0001 / ADR-0007 / ST-2: the same through a Source — its empty Window leaves nothing behind
    public async Task ST2_a_source_pushing_an_empty_window_leaves_nothing_to_paint()
    {
        var source = new TestSource();
        source.Push(TestRows.Many(50), totalCount: 50);
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 140)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> _) => { }));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 20, OffsetY = 30 });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, true, false, false, false));
        // The Formula Bar shows the Focus cell's value: committed data, which must go with the Window.
        Assert.Equal("Row 000001", cut.Find(".ex-formula-bar-text").GetAttribute("value"));

        await cut.InvokeAsync(() => source.Push([], totalCount: 0));

        AssertNothingPainted(cut);
    }

    [Fact] // ADR-0007 / ST-2: half-typed text is the grid's to hold, and is all that stays — no committed value does
    public async Task ST2_an_open_edit_keeps_only_its_own_text_when_the_window_empties()
    {
        var source = new TestSource();
        source.Push(TestRows.Many(50), totalCount: 50);
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 140)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> _) => { }));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 20, OffsetY = 30 });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("5", false, false, false, false, false));

        await cut.InvokeAsync(() => source.Push([], totalCount: 0));

        // Uncommitted text is held by the grid, committed data by the Consumer (ADR-0007): the
        // Formula Bar may still show what the user typed, and nothing the rows held.
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".ex-row"));
            Assert.Empty(cut.FindAll(".ex-cell"));
            Assert.Contains(cut.Find(".ex-formula-bar-text").GetAttribute("value") ?? "", new[] { "", "5" });
            Assert.DoesNotContain("Row 0", cut.Markup, StringComparison.Ordinal);
        });
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    [Fact] // ADR-0007 / ST-2: an editor opened with F2 and not typed into holds the committed value as its text — and that must go with the Window
    public async Task ST2_an_edit_opened_with_f2_keeps_no_committed_value_when_the_window_empties()
    {
        var source = new TestSource();
        source.Push(TestRows.Many(50), totalCount: 50);
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 140)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> _) => { }));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 20, OffsetY = 30 });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("F2", false, false, false, false, false));
        // F2 seeds the editor with the cell's committed value: the user has typed nothing.
        Assert.Equal("Row 000001", cut.Find(".ex-formula-bar-text").GetAttribute("value"));

        await cut.InvokeAsync(() => source.Push([], totalCount: 0));

        AssertNothingPainted(cut);
    }

    [Fact] // ADR-0007 / ADR-0142 / ST-2: an editor opened with F2 and typed into holds the user's text, not committed data — it stays when its row leaves the Window
    public async Task ST2_an_edit_opened_with_f2_and_typed_into_stays_when_its_row_leaves_the_window()
    {
        var source = new TestSource();
        var rows = TestRows.Many(50);
        source.Push(rows, totalCount: 50);
        var editing = new List<bool>();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 140)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> _) => { })
            .Add(g => g.OnEditingChanged, editing.Add));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 20, OffsetY = 30 });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("F2", false, false, false, false, false));
        await cut.Find(".ex-viewport input.ex-editor").InputAsync(new ChangeEventArgs { Value = "Typed" });

        await cut.InvokeAsync(() => source.Push([], totalCount: 0));

        // The rows' data is gone; the typing is the grid's to hold, and the edit has not ended.
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".ex-row"));
            Assert.Empty(cut.FindAll(".ex-cell"));
            Assert.DoesNotContain("Row 0", cut.Markup, StringComparison.Ordinal);
        });
        Assert.Equal([true], editing);

        // The row back in the Window: the editor is still there with what was typed.
        await cut.InvokeAsync(() => source.Push(rows, totalCount: 50));
        cut.WaitForAssertion(() => Assert.Equal("Typed", cut.Find(".ex-formula-bar-text").GetAttribute("value")));
        Assert.Equal([true], editing);
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    [Fact] // ADR-0007 / ST-2: an untyped editor goes only with its row — a Window that still holds the row leaves it open
    public async Task ST2_an_edit_opened_with_f2_stays_while_the_window_holds_its_row()
    {
        var source = new TestSource();
        var rows = TestRows.Many(50);
        source.Push(rows, totalCount: 50);
        var editing = new List<bool>();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 140)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> _) => { })
            .Add(g => g.OnEditingChanged, editing.Add));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 20, OffsetY = 30 });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("F2", false, false, false, false, false));

        // Another Window that still holds row 1: a refresh, with the rows past 30 gone.
        await cut.InvokeAsync(() => source.Push(rows[..30], totalCount: 30));

        cut.WaitForAssertion(() => Assert.Equal("Row 000001", cut.Find(".ex-viewport input.ex-editor").GetAttribute("value")));
        Assert.Equal([true], editing);
    }

    [Fact] // ADR-0007 / ST-2: an untyped editor whose row a moved Window no longer holds closes, as Escape would — nothing written, nothing discarded, the edit ended
    public async Task ST2_an_edit_opened_with_f2_closes_when_a_moved_window_no_longer_holds_its_row()
    {
        var source = new TestSource();
        var rows = TestRows.Many(500);
        source.Push(rows[..60], totalCount: 500);
        var editing = new List<bool>();
        var edits = new List<GridEditIntent<TestRow>>();
        var discards = new List<EditDiscardReason>();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 140)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnEdit, edits.Add)
            .Add(g => g.OnEditDiscarded, discards.Add)
            .Add(g => g.OnEditingChanged, editing.Add));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 20, OffsetY = 30 });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("F2", false, false, false, false, false));
        Assert.Equal("Row 000001", cut.Find(".ex-viewport input.ex-editor").GetAttribute("value"));

        // The Window moves on to rows 200 to 259: row 1 is no longer in it.
        await cut.InvokeAsync(() => source.Push(rows[200..260], windowStart: 200, totalCount: 500));

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".ex-editor:not(.ex-formula-bar-text)"));
            Assert.Equal([true, false], editing);
        });
        Assert.Empty(edits);
        Assert.Empty(discards);
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    // ---- ST-2 as refined on 2026-10-11: the browser says whether nothing was typed -----------------

    /// <summary>A grid over 500 rows holding rows 0 to 59, with row 1's Book open in an editor by F2,
    /// and ST-2's question left for the test to answer.</summary>
    private async Task<(IRenderedComponent<ExGrid<TestRow>> Cut, TestSource Source, TestRow[] Rows,
        JSRuntimeInvocationHandler<bool> Question, List<bool> Editing, List<GridEditIntent<TestRow>> Edits,
        List<EditDiscardReason> Discards, List<GridSelection> Selections)> F2OverRow1Async()
    {
        var source = new TestSource();
        var rows = TestRows.Many(500);
        source.Push(rows[..60], totalCount: 500);
        var question = Js.UnansweredUntouched();
        var editing = new List<bool>();
        var edits = new List<GridEditIntent<TestRow>>();
        var discards = new List<EditDiscardReason>();
        var selections = new List<GridSelection>();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 140)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnEdit, edits.Add)
            .Add(g => g.OnEditDiscarded, discards.Add)
            .Add(g => g.OnEditingChanged, editing.Add)
            .Add(g => g.SelectionChanged, selections.Add));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 20, OffsetY = 30 });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("F2", false, false, false, false, false));
        Assert.Equal("Row 000001", cut.Find(".ex-viewport input.ex-editor").GetAttribute("value"));
        return (cut, source, rows, question, editing, edits, discards, selections);
    }

    [Theory] // ADR-0007 (ST-2, refined 2026-10-11) / ADR-0142: the ordering that used to lose typing — the Window moves off the row, then the editor's input arrives, then the browser's answer — keeps the edit with what was typed, whatever the answer says
    [InlineData(false)]
    [InlineData(true)]
    public async Task ST2_input_that_reaches_the_grid_before_the_answer_keeps_the_edit_with_its_text(bool answeredUntouched)
    {
        var (cut, source, rows, question, editing, edits, discards, _) = await F2OverRow1Async();

        // The Window moves on to rows 200 to 259: the grid asks the browser, and the editor stands
        // meanwhile — it is not taken down on the core's own copy of the text.
        await cut.InvokeAsync(() => source.Push(rows[200..260], windowStart: 200, totalCount: 500));
        cut.WaitForAssertion(() => Assert.Single(question.Invocations));
        Assert.Equal("Row 000001", (string)question.Invocations.First().Arguments[0]!);
        Assert.Single(cut.FindAll(".ex-viewport input.ex-editor"));

        // Typed in the browser before the question reached it: the input travels ahead of the answer.
        await cut.Find(".ex-viewport input.ex-editor").InputAsync(new ChangeEventArgs { Value = "Row 000001x" });
        // A browser answers "typed" here; answered "untouched" all the same, the grid holds typing
        // and the edit stays.
        await cut.InvokeAsync(() => question.SetResult(answeredUntouched));

        cut.WaitForAssertion(() => Assert.Equal("Row 000001x", cut.Find(".ex-formula-bar-text").GetAttribute("value")));
        Assert.Equal([true], editing);
        Assert.Empty(discards);
        Assert.Equal(answeredUntouched ? 1 : 0, Js.CloseAnswered.Invocations.Count);

        // The row back in the Window, Enter writes what was typed onto it.
        await cut.InvokeAsync(() => source.Push(rows[..60], totalCount: 500));
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("Enter", false, false, false, false, false));
        var edit = Assert.Single(edits);
        Assert.Same(rows[1], edit.Row);
        Assert.Equal("Row 000001x", edit.Value);
        Assert.Equal([true, false], editing);
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    [Fact] // ADR-0007 (ST-2, refined 2026-10-11) / ADR-0010: answered "untouched", the edit closes as Escape does — the gate told it is over and the keyboard handed back before the listener lets its held keys go — and a held key is then the grid's
    public async Task ST2_answered_untouched_the_edit_closes_and_the_held_keys_are_the_grids()
    {
        var (cut, source, rows, question, editing, edits, discards, selections) = await F2OverRow1Async();
        await cut.InvokeAsync(() => source.Push(rows[200..260], windowStart: 200, totalCount: 500));
        cut.WaitForAssertion(() => Assert.Single(question.Invocations));
        var before = Js.GateTold.Count;

        await cut.InvokeAsync(() => question.SetResult(true));

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".ex-editor:not(.ex-formula-bar-text)"));
            Assert.Equal([true, false], editing);
            Assert.Single(Js.CloseAnswered.Invocations);
        });
        Assert.Empty(edits);
        Assert.Empty(discards);
        // In the order the listener needs them: the gate in its new mode and the keyboard back on the
        // root, then the answer, after which the held keys go on against that mode.
        Assert.Equal(["setEditing none", "reclaimFocus", "closeAnswered"], Js.GateTold.Skip(before));

        // A held ↓, replayed against no edit: the grid's key, moving the Focus from row 1 to row 2.
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, false, false, false, false));
        Assert.Equal(new CellPosition(2, 0), selections[^1].Focus);
        Assert.Empty(cut.FindAll(".ex-editor:not(.ex-formula-bar-text)"));
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    [Fact] // ADR-0007 (ST-2, refined 2026-10-11): an "untouched" that comes after the row is back finds nothing to close — the edit stays, and the listener is still told, so what it held goes on into the edit
    public async Task ST2_answered_untouched_after_the_row_came_back_the_edit_stays()
    {
        var (cut, source, rows, question, editing, _, discards, _) = await F2OverRow1Async();
        await cut.InvokeAsync(() => source.Push(rows[200..260], windowStart: 200, totalCount: 500));
        cut.WaitForAssertion(() => Assert.Single(question.Invocations));
        await cut.InvokeAsync(() => source.Push(rows[..60], totalCount: 500));

        await cut.InvokeAsync(() => question.SetResult(true));

        cut.WaitForAssertion(() => Assert.Single(Js.CloseAnswered.Invocations));
        Assert.Equal("Row 000001", cut.Find(".ex-viewport input.ex-editor").GetAttribute("value"));
        Assert.Equal([true], editing);
        Assert.Empty(discards);
    }

    [Fact] // ADR-0007 (ST-2) / ADR-0142: an editor the grid already knows to hold typing is not asked about — it stays, as ADR-0142 has it
    public async Task ST2_an_editor_known_to_hold_typing_is_not_asked_about()
    {
        var (cut, source, rows, question, editing, _, _, _) = await F2OverRow1Async();
        await cut.Find(".ex-viewport input.ex-editor").InputAsync(new ChangeEventArgs { Value = "Typed" });

        await cut.InvokeAsync(() => source.Push(rows[200..260], windowStart: 200, totalCount: 500));

        cut.WaitForAssertion(() => Assert.Equal("Typed", cut.Find(".ex-formula-bar-text").GetAttribute("value")));
        Assert.Empty(question.Invocations);
        Assert.Equal([true], editing);
    }

    [Fact] // ADR-0007 (ST-2) / ADR-0010: an edit a typed character opened is never untyped — typing the cell's own text over it is still typing, and the editor stays when its row leaves the Window, never asked about
    public async Task ST2_an_edit_opened_by_typing_the_cells_own_text_is_not_untyped()
    {
        var source = new TestSource();
        var rows = TestRows.Many(500);
        source.Push(rows[..60], totalCount: 500);
        var question = Js.UnansweredUntouched();
        var editing = new List<bool>();
        var discards = new List<EditDiscardReason>();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 140)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> _) => { })
            .Add(g => g.OnEditDiscarded, discards.Add)
            .Add(g => g.OnEditingChanged, editing.Add));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 20, OffsetY = 30 });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
        // "R" typed onto row 1's Book opens the editor over it, and the typing goes on to the cell's
        // own text.
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("R", false, false, false, false, false));
        Assert.Equal("R", cut.Find(".ex-viewport input.ex-editor").GetAttribute("value"));
        await cut.Find(".ex-viewport input.ex-editor").InputAsync(new ChangeEventArgs { Value = "Row 000001" });

        await cut.InvokeAsync(() => source.Push(rows[200..260], windowStart: 200, totalCount: 500));

        cut.WaitForAssertion(() => Assert.Equal("Row 000001", cut.Find(".ex-formula-bar-text").GetAttribute("value")));
        Assert.Empty(question.Invocations);
        Assert.Equal([true], editing);
        Assert.Empty(discards);
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    [Fact] // ADR-0007 / ADR-0011 / ADR-0142 / ST-2: without a Row Key an untyped editor names its row by position under the order it opened under — an order move, the same rows reordered, closes it: nothing written, nothing discarded
    public async Task ST2_without_a_row_key_an_untyped_editor_closes_when_the_order_moves()
    {
        var source = new TestSource();
        var rows = TestRows.Many(50);
        source.Push(rows, totalCount: 50);
        var editing = new List<bool>();
        var edits = new List<GridEditIntent<TestRow>>();
        var discards = new List<EditDiscardReason>();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 140)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnEdit, edits.Add)
            .Add(g => g.OnEditDiscarded, discards.Add)
            .Add(g => g.OnEditingChanged, editing.Add));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 20, OffsetY = 30 });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("F2", false, false, false, false, false));
        Assert.Equal("Row 000001", cut.Find(".ex-viewport input.ex-editor").GetAttribute("value"));

        // The same rows, reordered under a new order: position 1 now holds Row 000002, and the
        // Window holds no row the position-bound editor can call its own.
        TestRow[] reordered = [rows[0], rows[2], rows[1], .. rows[3..]];
        await cut.InvokeAsync(() => source.Push(reordered, totalCount: 50, rowSequenceVersion: 1));

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".ex-editor:not(.ex-formula-bar-text)"));
            Assert.Equal([true, false], editing);
        });
        Assert.Empty(edits);
        Assert.Empty(discards);
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    [Fact] // ADR-0007 / ADR-0140 / ST-2: with a Row Key an untyped editor goes with its row by key — a Window holding another row at its position closes it, one holding its row elsewhere does not
    public async Task ST2_with_a_row_key_an_untyped_editor_goes_with_its_row_not_its_position()
    {
        var source = new TestSource();
        var rows = TestRows.Many(50);
        source.Push(rows, totalCount: 50);
        var editing = new List<bool>();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowKey, (Func<TestRow, object>)(row => row.Book))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 140)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> _) => { })
            .Add(g => g.OnEditingChanged, editing.Add));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 20, OffsetY = 30 });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("F2", false, false, false, false, false));

        // Row 000001 moved to position 3, under a new order: the editor follows it and stays.
        TestRow[] moved = [rows[0], rows[2], rows[3], rows[1], .. rows[4..]];
        await cut.InvokeAsync(() => source.Push(moved, totalCount: 50, rowSequenceVersion: 1));
        cut.WaitForAssertion(() => Assert.Equal("Row 000001", cut.Find(".ex-viewport input.ex-editor").GetAttribute("value")));
        Assert.Equal([true], editing);

        // Row 000001 gone, another row at every position it stood at: the editor closes.
        TestRow[] without = [.. moved.Where(row => !ReferenceEquals(row, rows[1]))];
        await cut.InvokeAsync(() => source.Push(without, totalCount: 49, rowSequenceVersion: 2));
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".ex-editor:not(.ex-formula-bar-text)"));
            Assert.Equal([true, false], editing);
        });
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    /// <summary>ST-2's pass condition: no row, cell, Placeholder, Focus or range is painted, the
    /// Formula Bar shows nothing, no value of the data is anywhere in the markup, and nothing was
    /// thrown on the way.</summary>
    private void AssertNothingPainted(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".ex-row"));
            Assert.Empty(cut.FindAll(".ex-cell"));
            Assert.Empty(cut.FindAll(".ex-placeholder"));
            Assert.Empty(cut.FindAll(".ex-focus"));
            Assert.Empty(cut.FindAll(".ex-range"));
            Assert.Equal("", cut.Find(".ex-formula-bar-text").GetAttribute("value") ?? "");
            Assert.DoesNotContain("Row 0", cut.Markup, StringComparison.Ordinal);
        });
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    private static void AssertInvariants(
        IRenderedComponent<ExGrid<TestRow>> cut, GridSelection selection, InMemoryGridSource<TestRow> source,
        Layout layout, Browser browser, string where)
    {
        AssertInvariants(cut, selection, source);

        // Both scroll offsets the grid wrote lie within [0, max], and so does where the browser is.
        foreach (var (top, left) in browser.NewWrites)
        {
            Assert.True(top >= 0 && top <= MaxScrollTop(layout, source, cut), $"wrote top {top}: {where}");
            Assert.True(double.IsNaN(left) || (left >= 0 && left <= MaxScrollLeft(layout)), $"wrote left {left}: {where}");
        }

        // The painted rows are exactly ViewportGeometry.SliceAt of where the browser is.
        // The spacer the grid lays out is the arithmetic's scroll height under the header band: what a
        // browser lets the scroller reach is what the slices are computed against (ADR-0053).
        var spacerStyle = cut.Find(".ex-spacer").GetAttribute("style")!;
        var spacerPx = double.Parse(System.Text.RegularExpressions.Regex.Match(spacerStyle, @"height: ([0-9.]+)px").Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(spacerPx == HeaderHeightPx + GeometryOf(layout, source, cut).ScrollHeightPx,
            $"spacer {spacerPx}, arithmetic {HeaderHeightPx + GeometryOf(layout, source, cut).ScrollHeightPx}: {where}");

        // Under a pager, the page on screen is one the result holds (ADR-0015), and the rows are the
        // page's slice, written at their absolute positions.
        var page = PageOf(layout, source, cut);
        if (layout.PageSize is { } pageSize)
        {
            var pages = Math.Max(1, (source.TotalCount!.Value + pageSize - 1) / pageSize);
            Assert.True(page.Index >= 0 && page.Index < pages, $"page {page.Index + 1} of {pages}: {where}");
        }
        var rows = cut.FindAll(".ex-row");
        var expected = GeometryOf(layout, source, cut).SliceAt(browser.Top);
        if (expected is { } slice)
        {
            var firstRow = int.Parse(cut.Find(".ex-viewport").GetAttribute("data-ex-first-row")!);
            var from = page.Start + slice.Start;
            Assert.True(slice.Count == rows.Count && from == firstRow
                && from + 1 == int.Parse(rows[0].GetAttribute("aria-rowindex")!),
                $"painted {rows.Count} rows from {firstRow}, SliceAt says {slice.Count} from {from}: {where}");
        }
        else
        {
            Assert.True(rows.Count == 0, $"painted {rows.Count} rows of none: {where}");
        }

        // The displayed count, wherever the grid shows one, is the sum of the areas.
        if (cut.FindAll(".ex-status span").FirstOrDefault() is { } status
            && status.TextContent.Contains("cells selected", StringComparison.Ordinal))
        {
            Assert.Equal(
                selection.CellCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                status.TextContent[..status.TextContent.IndexOf(' ', StringComparison.Ordinal)]);
        }
    }

    private static void AssertInvariants(
        IRenderedComponent<ExGrid<TestRow>> cut, GridSelection selection, InMemoryGridSource<TestRow> source)
    {
        var extentRows = source.TotalCount!.Value;

        // Every rectangle inside the extent, and the count is the sum of the areas.
        long area = 0;
        foreach (var range in selection.Ranges)
        {
            Assert.InRange(range.TopRow, 0, extentRows - 1);
            Assert.InRange(range.BottomRow, range.TopRow, extentRows - 1);
            Assert.InRange(range.LeftColumn, 0, 3);
            Assert.InRange(range.RightColumn, range.LeftColumn, 3);
            area += range.CellCount;
        }
        Assert.Equal(area, selection.CellCount);

        // The Focus is always inside the Selection, in the range it names, and the Extent is a
        // cell of that range — there is no detached state (ADR-0052).
        if (!selection.IsEmpty)
        {
            Assert.True(selection.FocusRange.Contains(selection.Focus));
            Assert.True(selection.FocusRange.Contains(selection.Extent));
            Assert.Contains(selection.FocusRange, selection.Ranges);
        }

        // The painted rows are one contiguous, absolutely-indexed slice.
        var rows = cut.FindAll(".ex-row");
        int? previous = null;
        foreach (var row in rows)
        {
            var index = int.Parse(row.GetAttribute("aria-rowindex")!);
            Assert.InRange(index, 1, Math.Max(1, extentRows));
            if (previous is { } p)
                Assert.Equal(p + 1, index);
            previous = index;
        }
    }
}
