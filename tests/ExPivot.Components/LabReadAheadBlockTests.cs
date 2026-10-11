using Bunit;
using ExGrid;
using Microsoft.AspNetCore.Components;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// LAB ONLY, never merged (<c>lab/remote-read-ahead</c>, ticket 14 of the live-data spec): ExPivot
/// asking its report source for aligned blocks, as ag-grid's Server-Side row model asks for its
/// <c>cacheBlockSize</c> blocks — the blocks that cover the range the grid needs widened by a margin
/// either side, its <c>rowBuffer</c>, clamped into the report. One block, or two at a boundary.
/// </summary>
public sealed class LabReadAheadBlockTests : PivotTestContext
{
    [Theory] // Ticket 14, lab: the aligned blocks that cover the range and the margin either side, clamped into the report
    // Inside one block, clear of its edges by the margin: that block.
    [InlineData(8172, 15, 100, 10, 16823, 8100, 100)]
    [InlineData(16143, 15, 100, 10, 16823, 16100, 100)]
    // The margin reaches across a boundary, above or below: two blocks.
    [InlineData(200, 15, 100, 10, 16823, 100, 200)]
    [InlineData(176, 15, 100, 10, 16823, 100, 200)]
    [InlineData(1000, 15, 100, 10, 16823, 900, 200)]
    // The range itself straddles a boundary: two blocks, margin or none.
    [InlineData(286, 15, 100, 10, 16823, 200, 200)]
    [InlineData(286, 15, 100, 0, 16823, 200, 200)]
    // Without the margin, a range inside a block is that block alone.
    [InlineData(200, 15, 100, 0, 16823, 200, 100)]
    // At either end of the report: from row 0, and to its last row.
    [InlineData(0, 15, 100, 10, 16823, 0, 100)]
    [InlineData(16808, 15, 100, 10, 16823, 16700, 123)]
    // A short report: two blocks at row 90, where the dense report's Window is placed; its last
    // block, clamped.
    [InlineData(90, 13, 100, 10, 271, 0, 200)]
    [InlineData(250, 13, 200, 10, 271, 200, 71)]
    // Other block sizes.
    [InlineData(1000, 15, 50, 10, 16823, 950, 100)]
    [InlineData(1000, 15, 200, 10, 16823, 800, 400)]
    [InlineData(8172, 15, 200, 10, 16823, 8000, 200)]
    // A range past a report that shrank under it: never short of the range.
    [InlineData(500, 15, 100, 10, 503, 400, 115)]
    public void Ticket14_lab_blocks_cover_the_range_and_the_margin_clamped_into_the_report(
        int start, int count, int block, int margin, int rows, int windowStart, int windowCount)
        => Assert.Equal(new PivotReportWindow(windowStart, windowCount),
            PivotComponent.BlocksCovering(new RowRange(start, count), block, margin, rows));

    [Theory] // Ticket 14, lab: N rows either side, clamped into the report, as before the blocks
    [InlineData(200, 15, 60, 16823, 140, 135)]
    [InlineData(10, 15, 60, 16823, 0, 85)]
    [InlineData(16808, 15, 60, 16823, 16748, 75)]
    [InlineData(200, 15, 0, 16823, 200, 15)]
    [InlineData(500, 15, 60, 503, 440, 75)]
    public void Ticket14_lab_rows_either_side_are_clamped_into_the_report(
        int start, int count, int readAhead, int rows, int windowStart, int windowCount)
        => Assert.Equal(new PivotReportWindow(windowStart, windowCount),
            PivotComponent.Widened(new RowRange(start, count), readAhead, rows));

    /// <summary>A keyed deal: each in a Region of its own.</summary>
    public sealed record Deal(long Id, string Region, decimal Amount);

    private static readonly PivotFields<Deal> DealFields = PivotFields.Of<Deal>()
        .Key("Id", d => d.Id)
        .Text("Region", d => d.Region)
        .Number("Amount", d => d.Amount);

    // 999 Regions and the Grand Total: a report of 1,000 rows.
    private static readonly PivotLayout ByRegion = new() { Rows = [P("Region")], Values = [Sum("Amount")] };

    private const double RowHeightPx = 20;

    private static readonly PivotSlicing Whole = new() { Budget = TimeSpan.FromDays(1) };

    /// <summary>A remote report of 1,000 rows, asked over JSON, every Window it is asked for recorded.</summary>
    private (IRenderedComponent<PivotComponent> Cut, List<PivotReportWindow> Asked, LocalPivotReportSource Server) RenderRemote(
        Action<ComponentParameterCollectionBuilder<PivotComponent>> lab, Func<PivotReportRequest, Task>? answerWhen = null)
    {
        var data = PivotSource.From([.. Enumerable.Range(0, 999).Select(i => new Deal(i, $"Region {i:D4}", i))], DealFields, Whole);
        var server = PivotReportSource.From(data, timeProvider: Clock, slicing: Whole);
        var asked = new List<PivotReportWindow>();
        static T Wire<T>(T value) => PivotReportJson.Read<T>(PivotReportJson.Write(value));
        var remote = PivotReportSource.Fetch(server.Fields, server.Features, server.UpdateMode,
            async (request, ct) =>
            {
                lock (asked)
                    asked.Add(request.Window);
                if (answerWhen is not null)
                    await answerWhen(request).WaitAsync(ct);
                return Wire(await server.WindowAsync(Wire(request), ct));
            },
            items: server.RawItemsAsync,
            reportItems: async (query, ct) => Wire(await server.ItemsAsync(Wire(query), ct)));
        var cut = RenderPivot(ByRegion, ps =>
        {
            ps.Add(p => p.RowHeight, RowHeightPx)
              .Add(p => p.ShowFieldList, false)
              .Add(p => p.Slicing, Whole);
            lab(ps);
        }, reportSource: remote);
        cut.WaitForAssertion(() => Assert.Equal(1000, Grid(cut).Instance.TotalCount));
        return (cut, asked, server);
    }

    private static int Count(List<PivotReportWindow> asked) { lock (asked) return asked.Count; }

    private static PivotReportWindow Last(List<PivotReportWindow> asked) { lock (asked) return asked[^1]; }

    [Fact] // Ticket 14, lab: a remote report scrolled under blocks of 100 asks two blocks at a boundary, nothing inside them, and once past them
    public async Task Ticket14_lab_a_remote_report_asks_aligned_blocks_once_a_scroll_leaves_them()
    {
        var (cut, asked, server) = RenderRemote(ps => ps.Add(p => p.ReadAheadBlockRows, 100));
        await using var _ = server;

        // Row 500 at the top: the range and 10 rows above it reach into the block before.
        var before = Count(asked);
        await ScrollReportAsync(cut, 500 * RowHeightPx);
        cut.WaitForAssertion(() => Assert.Equal(400, Grid(cut).Instance.WindowStart));
        Assert.Equal(before + 1, Count(asked));
        Assert.Equal(new PivotReportWindow(400, 200), Last(asked));
        Assert.Equal(200, Grid(cut).Instance.Window.Count);

        // Further down inside those blocks, to within a few rows of their end: nothing is asked.
        before = Count(asked);
        await ScrollReportAsync(cut, 540 * RowHeightPx);
        await ScrollReportAsync(cut, 575 * RowHeightPx);
        cut.WaitForAssertion(() => Assert.Contains("Region 0575", RowTexts(cut)[0], StringComparison.Ordinal));
        Assert.Equal(before, Count(asked));

        // Past the second block's end: one question, for that block and the next.
        await ScrollReportAsync(cut, 590 * RowHeightPx);
        cut.WaitForAssertion(() => Assert.Equal(500, Grid(cut).Instance.WindowStart));
        Assert.Equal(before + 1, Count(asked));
        Assert.Equal(new PivotReportWindow(500, 200), Last(asked));
        Assert.False(Renderer.UnhandledException.IsCompleted, "an exception reached the renderer");
    }

    [Fact] // Ticket 14, lab: with a trigger of 10, the next blocks are asked while every row on screen is still held, as ag-grid's rowBuffer asks
    public async Task Ticket14_lab_a_trigger_asks_the_next_blocks_before_the_Window_runs_out()
    {
        var (cut, asked, server) = RenderRemote(ps => ps
            .Add(p => p.ReadAheadBlockRows, 100)
            .Add(p => p.ReadAheadTriggerRows, 10));
        await using var _ = server;
        await ScrollReportAsync(cut, 500 * RowHeightPx);
        cut.WaitForAssertion(() => Assert.Equal(400, Grid(cut).Instance.WindowStart));
        Assert.Equal(new PivotReportWindow(400, 200), Last(asked));

        // Row 575 at the top: the rows on screen are all held, but 10 below them are not.
        var before = Count(asked);
        await ScrollReportAsync(cut, 540 * RowHeightPx);
        await ScrollReportAsync(cut, 575 * RowHeightPx);
        cut.WaitForAssertion(() => Assert.Equal(500, Grid(cut).Instance.WindowStart));
        Assert.Equal(before + 1, Count(asked));
        Assert.Equal(new PivotReportWindow(500, 200), Last(asked));
        Assert.Contains("Region 0575", RowTexts(cut)[0], StringComparison.Ordinal);
        Assert.False(Renderer.UnhandledException.IsCompleted, "an exception reached the renderer");
    }

    [Fact] // Ticket 14, lab: asked early, a question in flight stays in flight while its Window covers the rows on screen, the decided rule; the next is asked once it lands
    public async Task Ticket14_lab_a_trigger_keeps_a_question_in_flight_that_covers_the_rows_on_screen()
    {
        TaskCompletionSource? hold = null;
        var (cut, asked, server) = RenderRemote(ps => ps
            .Add(p => p.ReadAheadBlockRows, 100)
            .Add(p => p.ReadAheadTriggerRows, 10),
            request => hold is { } held && request.Window.Start == 500 ? held.Task : Task.CompletedTask);
        await using var _ = server;
        await ScrollReportAsync(cut, 500 * RowHeightPx);
        cut.WaitForAssertion(() => Assert.Equal(400, Grid(cut).Instance.WindowStart));
        var painted = RowTexts(cut).Length;

        // Within 10 rows of the Window's end: blocks 5 and 6 are asked for, and their answer held.
        hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var before = Count(asked);
        await ScrollReportAsync(cut, 540 * RowHeightPx);
        await ScrollReportAsync(cut, 575 * RowHeightPx);
        cut.WaitForAssertion(() => Assert.Equal(before + 1, Count(asked)));
        Assert.Equal(new PivotReportWindow(500, 200), Last(asked));

        // On to rows the question out covers, though not the 10 rows below them: it stays in flight.
        await ScrollReportAsync(cut, 630 * RowHeightPx);
        await ScrollReportAsync(cut, (697 - painted) * RowHeightPx);
        Assert.Equal(before + 1, Count(asked));

        // Once it lands, the rows below are asked for.
        hold.SetResult();
        cut.WaitForAssertion(() => Assert.Equal(before + 2, Count(asked)));
        Assert.Equal(new PivotReportWindow(600, 200), Last(asked));
        Assert.False(Renderer.UnhandledException.IsCompleted, "an exception reached the renderer");
    }
}
