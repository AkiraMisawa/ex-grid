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
    [Fact] // ASY-3, ADR-0012: three scroll events with a read in flight: two reads, the last position painted, never one out of order
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

        // What each painted frame shows: rows 10 (the first event's position, 200px),
        // 200 (the second's, 4000px — never read: the browser has moved on by then)
        // and 400 (the third's and the final one, 8000px).
        var frames = new List<(bool First, bool Middle, bool Last)>();
        cut.OnMarkupUpdated += (_, _) => frames.Add((
            cut.Markup.Contains("Row 000010", StringComparison.Ordinal),
            cut.Markup.Contains("Row 000200", StringComparison.Ordinal),
            cut.Markup.Contains("Row 000400", StringComparison.Ordinal)));
        var scroller = cut.Find(".ex-scroller");

        var firstRead = Js.UnansweredScrollRead();
        var first = cut.InvokeAsync(() => scroller.TriggerEventAsync("onscroll", EventArgs.Empty));
        await cut.InvokeAsync(() => scroller.TriggerEventAsync("onscroll", EventArgs.Empty));
        await cut.InvokeAsync(() => scroller.TriggerEventAsync("onscroll", EventArgs.Empty));
        Assert.Equal(readsBefore + 1, Js.ScrollReads); // the burst sent one read, not three

        var secondRead = Js.UnansweredScrollRead();
        firstRead.SetResult(new ScrollOffset(200, 0));
        secondRead.SetResult(new ScrollOffset(8000, 0));
        await first;
        // A jump this far is a fling: placeholders until the scroll settles (ADR-0004).
        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));

        // One further read for the whole burst, and its answer is what stays painted.
        Assert.Equal(readsBefore + 2, Js.ScrollReads);
        Assert.Contains("Row 000400", cut.Markup);
        Assert.DoesNotContain("Row 000010", cut.Markup);
        // The middle position was never painted, and no frame went back after the last one.
        Assert.DoesNotContain(frames, f => f.Middle);
        var lastShown = frames.FindIndex(f => f.Last);
        Assert.True(lastShown >= 0);
        Assert.DoesNotContain(frames.Skip(lastShown), f => f.First);
    }
}
