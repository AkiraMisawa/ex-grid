using Bunit;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// The Sheet Day through the component (ADR-0121, ADR-0122): <c>TODAY()</c> answers a fixed day, or
/// the day in the Consumer's zone, or else the browser's, which the grid is told; it waits until one
/// is known, and moves on at midnight on the test's own clock.
/// </summary>
public class SheetDayWiringTests : SheetTestContext
{
    /// <summary>23:30 in London on 2 October 2026 (UTC+1), which is 07:30 on 3 October in Tokyo.</summary>
    private static readonly DateTimeOffset LateInLondon = new(2026, 10, 2, 22, 30, 0, TimeSpan.Zero);

    private static SheetDocument Readers()
    {
        var sheet = new Sheet(System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        sheet.Enter(CellAddress.Parse("A1"), "=TEXT(TODAY(),\"yyyy-mm-dd\")");
        return sheet.ToDocument();
    }

    private IRenderedComponent<Components.ExSheet> RenderReaders(Action<Bunit.ComponentParameterCollectionBuilder<Components.ExSheet>>? more = null)
    {
        Clock.SetUtcNow(LateInLondon);
        return RenderSheet(ps =>
        {
            ps.Add(s => s.Document, Readers());
            more?.Invoke(ps);
        });
    }

    private static Task BrowserSaysAsync(IRenderedComponent<Components.ExSheet> cut, string zone) =>
        cut.InvokeAsync(() => Grid(cut).Instance.OnTimeZoneAsync(zone));

    [Fact] // ADR-0121: before any zone is known, TODAY waits, and the server's own zone is never used
    public void Today_waits_for_a_zone()
    {
        var cut = RenderReaders();

        Assert.Equal("#GETTING_DATA", CellText(cut, "A1"));
    }

    [Fact] // ADR-0122: the grid asks the browser for its zone when attaching, because ExSheet listens
    public void The_grid_asks_the_browser_for_its_zone()
    {
        RenderReaders();

        var attach = JSInterop.Invocations.Last(i => i.Identifier == "attach");
        Assert.Equal(true, attach.Arguments[^1]);
    }

    [Fact] // ADR-0121 / ADR-0122: the browser's zone is the default, so a user in Tokyo sees Tokyo's day while London's is still the day before
    public async Task The_browsers_zone_gives_the_users_day()
    {
        var cut = RenderReaders();

        await BrowserSaysAsync(cut, "Asia/Tokyo");

        Assert.Equal("2026-10-03", CellText(cut, "A1"));
    }

    [Fact] // ADR-0121: the Consumer's zone goes before the browser's
    public async Task The_consumers_zone_goes_before_the_browsers()
    {
        var cut = RenderReaders(ps => ps.Add(s => s.TimeZone, TimeZoneInfo.FindSystemTimeZoneById("Europe/London")));

        await BrowserSaysAsync(cut, "Asia/Tokyo");

        Assert.Equal("2026-10-02", CellText(cut, "A1"));
    }

    [Fact] // ADR-0121: a fixed day goes before any zone, and a change of it repaints at once
    public async Task A_fixed_day_goes_before_any_zone()
    {
        var cut = RenderReaders(ps => ps.Add(s => s.Today, new DateOnly(2026, 9, 30)));
        await BrowserSaysAsync(cut, "Asia/Tokyo");

        Assert.Equal("2026-09-30", CellText(cut, "A1"));

        cut.Render(ps => ps.Add(s => s.Today, new DateOnly(2026, 8, 31)));

        Assert.Equal("2026-08-31", CellText(cut, "A1"));
    }

    [Fact] // ADR-0121: a zone .NET does not know leaves the day unknown, rather than take another zone's
    public async Task An_unknown_zone_keeps_today_waiting()
    {
        var cut = RenderReaders();

        await BrowserSaysAsync(cut, "Mars/Olympus_Mons");

        Assert.Equal("#GETTING_DATA", CellText(cut, "A1"));
    }

    [Fact] // ADR-0121: ExSheet moves the day on at midnight in the zone, on the clock it is given
    public async Task The_day_moves_on_at_midnight()
    {
        var cut = RenderReaders(ps => ps.Add(s => s.TimeZone, TimeZoneInfo.FindSystemTimeZoneById("Europe/London")));
        Assert.Equal("2026-10-02", CellText(cut, "A1"));

        // 23:30 to 23:59 in London: the same day, so nothing moves.
        Clock.Advance(TimeSpan.FromMinutes(29));
        Assert.Equal("2026-10-02", CellText(cut, "A1"));

        // Past midnight.
        Clock.Advance(TimeSpan.FromMinutes(2));
        await cut.InvokeAsync(() => { });

        cut.WaitForAssertion(() => Assert.Equal("2026-10-03", CellText(cut, "A1")));
    }

    [Fact] // ADR-0121: a disposed Sheet reads no clock
    public void A_disposed_sheet_reads_no_clock()
    {
        var cut = RenderReaders(ps => ps.Add(s => s.TimeZone, TimeZoneInfo.FindSystemTimeZoneById("Europe/London")));

        cut.Instance.Dispose();
        Clock.Advance(TimeSpan.FromHours(3));

        Assert.Equal("2026-10-02", CellText(cut, "A1"));
    }

    private static SheetDocument NowReader()
    {
        var sheet = new Sheet(System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        sheet.Enter(CellAddress.Parse("A1"), "=TEXT(NOW(),\"yyyy-mm-dd hh:mm\")");
        return sheet.ToDocument();
    }

    [Fact] // ADR-0124: NOW is the moment in the Sheet's zone, and moves on as the minute turns, on the clock it is given
    public async Task Now_moves_on_each_minute()
    {
        Clock.SetUtcNow(LateInLondon);
        var cut = RenderSheet(ps => ps.Add(s => s.Document, NowReader()));
        Assert.Equal("#GETTING_DATA", CellText(cut, "A1"));

        await BrowserSaysAsync(cut, "Asia/Tokyo");
        Assert.Equal("2026-10-03 07:30", CellText(cut, "A1"));

        Clock.Advance(TimeSpan.FromMinutes(1));
        await cut.InvokeAsync(() => { });

        cut.WaitForAssertion(() => Assert.Equal("2026-10-03 07:31", CellText(cut, "A1")));
    }
}
