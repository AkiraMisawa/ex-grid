using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ST-1: after any sequence of operations the invariants hold. A scripted randomised
/// sequence — clicks, drags, every claimed key, scrolls, sorts, filters, geometry
/// changes — asserting after each step that every selection rectangle lies inside the
/// extent, the Focus and the Extent lie inside the range holding the Focus (ADR-0052),
/// the displayed cell count is the sum of rectangle areas, the painted rows are a
/// contiguous slice, and nothing throws. The seed is in the test name's data so
/// a failure replays exactly.
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
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 140)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.SelectionChanged, (GridSelection s) => selection = s));

        string[] keys =
        [
            "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Home", "End",
            "PageUp", "PageDown", "Enter", "Tab", " ", "a", "Escape", "F2", "5",
            ".", "Backspace",
        ];
        var maxScrollTop = (totalRows * 20d) - (140 - 20);

        for (var step = 0; step < 500; step++)
        {
            var operation = random.Next(8);
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
                    break;
                case 1: // drag a step (only lands while a mousedown armed it)
                    if (cut.Find(".ex-viewport").GetAttribute("onmousemove") is not null)
                        break;
                    await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
                    break;
                case 2: // a claimed key, sometimes chorded
                    await cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
                        keys[random.Next(keys.Length)],
                        ctrl: random.Next(4) == 0,
                        shift: random.Next(3) == 0,
                        alt: false, meta: false, metaIsPrimary: false));
                    break;
                case 3: // scroll to an arbitrary offset, then let the settle timer run
                    await ScrollToAsync(cut.Find(".ex-scroller"),
                        random.NextDouble() * maxScrollTop, random.Next(0, 100));
                    await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));
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
                case 6: // a geometry change
                    cut.Render(ps => ps.Add(g => g.RowHeight, 18d + random.Next(4) * 4));
                    break;
                case 7: // the edge-band clock ticks while whatever is armed runs
                    await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(50)));
                    break;
            }

            AssertInvariants(cut, selection, source);
        }
    }

    // ---- ST-2: the grid holds no committed data ---------------------------------------------------

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
