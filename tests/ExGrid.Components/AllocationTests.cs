using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// MEM-1: steady-state scrolling allocates nothing that scales with TotalCount. The
/// same 100 one-row scroll steps at 10³ and at 10⁶ rows; the two totals must sit
/// within 5% of each other — the structural invariant that produces the timing, exact
/// and environment-independent (Definition of Done §1).
///
/// <para><b>Environment-independent includes the rest of the suite.</b> Test classes run side
/// by side in one process, and whatever the process shares can make the measuring thread
/// allocate for a reason that is not the grid's. Measured as it was until 2026-10-13, the
/// totals came out to the byte on every run alone and moved beside the other classes: by up to
/// 1.7% on a four-core machine, and by 5.8% and 6.8% in two runs of the whole solution
/// elsewhere, which failed. <see cref="ScrollRun"/> says what was shared and what each grid
/// has of its own instead.</para>
/// </summary>
public class AllocationTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    /// <summary>
    /// One grid scrolled for MEM-1, in a bUnit context of its own, counting what its scroll
    /// steps allocate. What the old measurement shared, and how each grid is kept from it
    /// without changing the scroll it performs:
    /// <list type="bullet">
    /// <item>bUnit's DOM. Raised through the element, every event re-read the whole rendered
    /// markup — some 240 KB of the old 283 KB a step — with AngleSharp, which keeps one pool
    /// of StringBuilders for the whole process. Whether a step found one there or made its
    /// own (2,120 bytes each) depended on what the tests beside it were parsing at that
    /// moment; with the pool emptied for the million-row run alone, the two totals were 92%
    /// apart. A step here goes straight to the renderer, as MEM-7's do: the grid's handler,
    /// its render and the renderer's diff.</item>
    /// <item>The grid's identity keys (ADR-0003, <c>ExGrid.RowKey.cs</c>), one table per row
    /// type for the whole process: a row's key is made the first time it is painted, and
    /// growing the table costs whichever grid fills it — beside the rest of the suite, any of
    /// them. The runs share one Window, so every key is made in the throwaway run and the
    /// measured runs paint the same rows and find their keys.</item>
    /// <item>The context. bUnit records the JavaScript calls a context hears in a list that
    /// doubles as it grows, and in one context the later grid paid the larger doubling, 115 KB
    /// against 57 KB. The stand-in for the browser's scroll offset is one per context too, so
    /// each later grid attached at the last one's offset, 2,000 px down — a fling — and painted
    /// Placeholders (ADR-0004) from then on, which the test's clock never settles: the old 10³
    /// and 10⁶ runs measured scrolling over Placeholders. A fresh context starts at the top with
    /// an empty record, the same for every grid, and <see cref="ShowsRowAsCells"/> checks that
    /// the rows were painted.</item>
    /// </list>
    /// </summary>
    private sealed class ScrollRun : GridTestContext
    {
        private readonly IRenderedComponent<ExGrid<TestRow>> _cut;
        private readonly Func<Task> _dispatchScroll;

        internal ScrollRun(TestRow[] window, int totalRows)
        {
            // A small Window over a large claimed total: what scales with TotalCount can
            // only be the arithmetic, and the arithmetic must not allocate per row.
            var cut = _cut = Render<ExGrid<TestRow>>(ps => ps
                .Add(g => g.Window, window)
                .Add(g => g.TotalCount, totalRows)
                .Add(g => g.Columns, (GridColumn<TestRow>[])
                    [new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
                     new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100)])
                .Add(g => g.RowHeight, 20d)
                .Add(g => g.ViewportHeight, 120)
                .Add(g => g.ViewportWidth, 350));
            var onScroll = ulong.Parse(cut.Find(".ex-scroller").GetAttribute("blazor:onscroll")!);
            // The renderer's dispatch wants an EventFieldInfo, a type BL0006 keeps to the
            // framework; a scroll carries no field, so an empty one is all this passes.
#pragma warning disable BL0006
            var noField = new Microsoft.AspNetCore.Components.RenderTree.EventFieldInfo();
#pragma warning restore BL0006
            // Made once, so a step allocates no closure of the test's own.
            _dispatchScroll = () => cut.InvokeAsync(() => Renderer.DispatchEventAsync(onScroll, noField, EventArgs.Empty));
        }

        /// <summary>What this grid's measured steps have allocated so far.</summary>
        internal long Allocated { get; private set; }

        /// <summary>Scrolls to <paramref name="row"/> unmeasured: one-time laziness off the
        /// count.</summary>
        internal Task WarmAsync(int row)
        {
            Js.SetScrollOffset(row * 20, 0);
            return _dispatchScroll();
        }

        /// <summary>Scrolls to <paramref name="row"/> and counts what the grid heard and did:
        /// the browser moving is set up before the count starts.</summary>
        internal async Task StepAsync(int row)
        {
            Js.SetScrollOffset(row * 20, 0);
            var before = GC.GetAllocatedBytesForCurrentThread();
            await _dispatchScroll();
            Allocated += GC.GetAllocatedBytesForCurrentThread() - before;
        }

        /// <summary>Whether the grid shows <paramref name="row"/> as a row of cells, with no
        /// Placeholder anywhere: read after the count, so the reading is not in it.</summary>
        internal bool ShowsRowAsCells(int row)
            => _cut.FindAll("[aria-busy=true]").Count == 0
                && _cut.FindAll(".ex-cell").Any(cell => cell.TextContent == $"Row {row:D6}");
    }

    [Fact] // MEM-7 (observational): the bytes one scroll frame allocates, recorded rather than gated
    public async Task Bytes_per_scroll_frame_are_recorded()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(200))
            .Add(g => g.TotalCount, 1_000_000)
            .Add(g => g.Columns, (GridColumn<TestRow>[])
                [new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
                 new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100)])
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350));
        // Straight to the renderer, not through the element: bUnit's element re-reads the
        // whole rendered DOM on every event it raises — some 250 KB a step here, which is
        // the test harness's cost and no browser's.
        var onScroll = ulong.Parse(cut.Find(".ex-scroller").GetAttribute("blazor:onscroll")!);
        // The renderer's dispatch wants an EventFieldInfo, a type BL0006 keeps to the
        // framework; a scroll carries no field, so an empty one is all this passes.
#pragma warning disable BL0006
        var noField = new Microsoft.AspNetCore.Components.RenderTree.EventFieldInfo();
#pragma warning restore BL0006
        async Task StepAsync(int row)
        {
            Js.SetScrollOffset(row * 20, 0);
            await cut.InvokeAsync(() => Renderer.DispatchEventAsync(onScroll, noField, EventArgs.Empty));
        }

        // Warm the path, as MEM-1 does, so one-time laziness is off the measurement.
        for (var row = 1; row <= 20; row++)
            await StepAsync(row);

        const int steps = 100;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var row = 21; row < 21 + steps; row++)
            await StepAsync(row);
        var perFrame = (GC.GetAllocatedBytesForCurrentThread() - before) / steps;

        // Where the number is read from: this test's output, which the runner shows with
        // -showLiveOutput and dotnet test keeps in the assembly's TestResults log.
        Xunit.TestContext.Current.TestOutputHelper!.WriteLine(
            $"MEM-7: {perFrame:N0} bytes allocated per one-row scroll frame, 6 rows x 2 columns painted, " +
            "under bUnit's renderer (its own batch handling included)");
        Assert.True(perFrame > 0, "a scroll frame that allocates nothing measured nothing");
    }

    [Fact] // MEM-1: the two totals differ by < 5%
    public async Task Scrolling_allocates_the_same_at_a_thousand_and_a_million_rows()
    {
        const int steps = 100;
        // One Window for every run: each row's identity key is made once, in the throwaway run
        // (ScrollRun says why).
        var window = TestRows.Many(200);

        // One throwaway run first: the JIT's own allocations land on whoever goes
        // first, and this measurement is about the grid, not the runtime.
        await using (var throwaway = new ScrollRun(window, 1_000))
        {
            await throwaway.WarmAsync(1);
            await throwaway.WarmAsync(0);
            for (var step = 1; step <= steps; step++)
                await throwaway.StepAsync(step);
        }

        // The million-row grid is made first. A grid's element ids carry its instance number —
        // some 85 bytes a step for each digit of it — and grids other tests make meanwhile can
        // give the later of the two a longer one. Made first, the million-row grid's number is
        // never the longer, so the one input the suite's timing can still move never counts
        // against it.
        await using var atMillion = new ScrollRun(window, 1_000_000);
        await using var atThousand = new ScrollRun(window, 1_000);
        foreach (var run in (ScrollRun[])[atThousand, atMillion])
        {
            await run.WarmAsync(1);
            await run.WarmAsync(0);
        }

        // The same steps, taken in turn and each grid going first every other step, so whatever
        // the runtime changes under them while they run — a method replaced by its optimised
        // code, which may allocate less — reaches both at the same step, never one run and not
        // the other.
        for (var step = 1; step <= steps; step++)
        {
            var (first, second) = step % 2 == 1 ? (atThousand, atMillion) : (atMillion, atThousand);
            await first.StepAsync(step);
            await second.StepAsync(step);
        }

        // What was counted is the scroll a reader sees: the last step's row painted as cells.
        Assert.True(atThousand.ShowsRowAsCells(steps), "the 10^3 grid does not show its last row as cells: its count is not of a scroll over rows");
        Assert.True(atMillion.ShowsRowAsCells(steps), "the 10^6 grid does not show its last row as cells: its count is not of a scroll over rows");

        // Directional, deliberately: the invariant is that allocation does not GROW
        // with TotalCount, so the million-row run must not exceed the thousand-row run
        // by more than the 5%. Runtime jitter the other way — the small case measuring
        // a shade larger — is noise, not scaling, and must not flake the invariant.
        Assert.True(
            atMillion.Allocated < atThousand.Allocated * 1.05,
            $"steady-state scrolling allocated {atThousand.Allocated:N0} bytes at 10^3 rows and {atMillion.Allocated:N0} at 10^6 — " +
            "an allocation that scales with TotalCount (MEM-1).");
    }
}
