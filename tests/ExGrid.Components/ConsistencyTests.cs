using Bunit;
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
/// extent, the displayed cell count is the sum of rectangle areas, the painted rows
/// are a contiguous slice, and nothing throws. The seed is in the test name's data so
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
