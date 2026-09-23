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
/// </summary>
public class AllocationTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private async Task<long> AllocatedBySteadyScrollAsync(int totalRows)
    {
        // A small Window over a large claimed total: what scales with TotalCount can
        // only be the arithmetic, and the arithmetic must not allocate per row.
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(200))
            .Add(g => g.TotalCount, totalRows)
            .Add(g => g.Columns, (GridColumn<TestRow>[])
                [new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
                 new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100)])
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350));
        var scroller = cut.Find(".ex-scroller");

        // Warm the path so one-time laziness is off the measurement.
        await ScrollToAsync(scroller, 20, 0);
        await ScrollToAsync(scroller, 0, 0);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var step = 1; step <= 100; step++)
            await ScrollToAsync(scroller, step * 20, 0);
        return GC.GetAllocatedBytesForCurrentThread() - before;
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
        // One throwaway run first: the JIT's own allocations land on whoever goes
        // first, and this measurement is about the grid, not the runtime.
        await AllocatedBySteadyScrollAsync(1_000);

        var atThousand = await AllocatedBySteadyScrollAsync(1_000);
        var atMillion = await AllocatedBySteadyScrollAsync(1_000_000);

        // Directional, deliberately: the invariant is that allocation does not GROW
        // with TotalCount, so the million-row run must not exceed the thousand-row run
        // by more than the 5%. Runtime jitter the other way — the small case measuring
        // a shade larger — is noise, not scaling, and must not flake the invariant.
        Assert.True(
            atMillion < atThousand * 1.05,
            $"steady-state scrolling allocated {atThousand:N0} bytes at 10^3 rows and {atMillion:N0} at 10^6 — " +
            "an allocation that scales with TotalCount (MEM-1).");
    }
}
