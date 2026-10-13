using Bunit;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ASY-3 (ADR-0012/0021): a burst of scroll events lands the last position, in order. A read of
/// the offset is a round trip; events that arrive while one is out set a flag rather than send
/// reads of their own, and when the answer is back the grid asks once more and applies that.
/// 1,000 rows of 20px in a 120px Viewport.
/// </summary>
public class ScrollBurstTests : GridTestContext
{
    [Fact] // ASY-3, ADR-0012: three scroll events with a read in flight: two reads, the positions painted in order, the last one standing
    public async Task ASY3_a_burst_of_scroll_events_applies_only_the_last_position_in_order()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(1000))
            .Add(g => g.TotalCount, 1000)
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350));
        var readsBefore = Js.ScrollReads;

        // What each painted frame shows: row 3 (the first event's position, 60px — an
        // ordinary step, painted at once) and row 400 (the burst's last position, 8000px).
        var frames = new List<(bool First, bool Last)>();
        cut.OnMarkupUpdated += (_, _) => frames.Add((
            cut.Markup.Contains("Row 000003", StringComparison.Ordinal) && !cut.Markup.Contains("Row 000000", StringComparison.Ordinal),
            cut.Markup.Contains("Row 000400", StringComparison.Ordinal)));
        var scroller = cut.Find(".ex-scroller");

        var firstRead = Js.UnansweredScrollRead();
        var first = cut.InvokeAsync(() => scroller.TriggerEventAsync("onscroll", EventArgs.Empty));
        await cut.InvokeAsync(() => scroller.TriggerEventAsync("onscroll", EventArgs.Empty));
        await cut.InvokeAsync(() => scroller.TriggerEventAsync("onscroll", EventArgs.Empty));
        Assert.Equal(readsBefore + 1, await ReadWhenIdleAsync(() => Js.ScrollReads)); // the burst sent one read, not three

        // The first answer lands and is painted while the burst's one further read is out. The
        // answer posts the scroll handler's continuation to the dispatcher, and the reading is taken
        // once the dispatcher is idle, whenever the process gets round to it (ReadWhenIdleAsync).
        var secondRead = Js.UnansweredScrollRead();
        await cut.InvokeAsync(() => firstRead.SetResult(new ScrollOffset(60, 0)));
        var afterFirst = await ReadWhenIdleAsync(() => (Reads: Js.ScrollReads, Frames: frames.ToArray()));
        Assert.Equal(readsBefore + 2, afterFirst.Reads);
        Assert.Contains(afterFirst.Frames, f => f.First);

        // The browser has moved on to the end of the burst; that answer is the one that stays.
        await cut.InvokeAsync(() => secondRead.SetResult(new ScrollOffset(8000, 0)));
        await first;
        // A jump this far is a fling: placeholders until the scroll settles (ADR-0004).
        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));
        var settled = await ReadWhenIdleAsync(() => (Reads: Js.ScrollReads, cut.Markup, Frames: frames.ToArray()));

        // One further read for the whole burst, never one per event, and its answer is what
        // stays painted.
        Assert.Equal(readsBefore + 2, settled.Reads);
        Assert.Contains("Row 000400", settled.Markup);
        Assert.DoesNotContain("Row 000003", settled.Markup);
        // In order: every frame showing the first position comes before every frame showing
        // the last, and none goes back.
        var lastShown = Array.FindIndex(settled.Frames, f => f.Last);
        Assert.True(lastShown > Array.FindLastIndex(settled.Frames, f => f.First));
        Assert.DoesNotContain(settled.Frames.Skip(lastShown), f => f.First);
    }
}
