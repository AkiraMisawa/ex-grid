using Bunit;
using ExGrid.Components;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// SH-57, ADR-0121: ExSheet reads the Sheet Day at most an hour apart, so a day that changes without
/// midnight passing on the clock's own timers (the wall clock set forward, a device that slept) is
/// late by at most an hour; and a reading that finds the same day repaints nothing.
/// </summary>
public class SheetDayReadingTests : SheetTestContext
{
    /// <summary>
    /// A wall clock that can be set without its timers' time moving: <see cref="GetUtcNow"/> answers
    /// the fake clock plus <see cref="Shift"/>, while timers run on the fake clock alone — a user
    /// correcting the device's clock, which no timer due at midnight hears.
    /// </summary>
    private sealed class SettableWallClock(FakeTimeProvider timers) : TimeProvider
    {
        public TimeSpan Shift { get; set; }

        public override DateTimeOffset GetUtcNow() => timers.GetUtcNow() + Shift;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            timers.CreateTimer(callback, state, dueTime, period);

        public override long GetTimestamp() => timers.GetTimestamp();

        public override long TimestampFrequency => timers.TimestampFrequency;
    }

    /// <summary>10:00 in London on 2 October 2026 (UTC+1): fourteen hours before midnight.</summary>
    private static readonly DateTimeOffset MorningInLondon = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    private readonly SettableWallClock _wall;

    public SheetDayReadingTests()
    {
        _wall = new SettableWallClock(Clock);
        Services.AddSingleton<TimeProvider>(_wall);
    }

    private static SheetDocument DayReaderAmongOthers()
    {
        var sheet = new Sheet(System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        sheet.Enter(CellAddress.Parse("A1"), "=TEXT(TODAY(),\"yyyy-mm-dd\")");
        sheet.Enter(CellAddress.Parse("A2"), "2");
        sheet.Enter(CellAddress.Parse("B3"), "=A2*3");
        return sheet.ToDocument();
    }

    private IRenderedComponent<Components.ExSheet> RenderInLondon()
    {
        Clock.SetUtcNow(MorningInLondon);
        return RenderSheet(ps => ps
            .Add(s => s.Document, DayReaderAmongOthers())
            .Add(s => s.TimeZone, London));
    }

    [Fact] // SH-57, ADR-0121: a day that changes without midnight passing on the timers shows within the hour
    public async Task SH57_a_day_changed_away_from_midnight_shows_within_an_hour()
    {
        var cut = RenderInLondon();
        Assert.Equal("2026-10-02", CellText(cut, "A1"));

        // The wall clock is set to 01:00 on 3 October; the timer due at midnight is still 14 hours away.
        _wall.Shift = TimeSpan.FromHours(15);
        Clock.Advance(TimeSpan.FromMinutes(61));
        await cut.InvokeAsync(() => { });

        cut.WaitForAssertion(() => Assert.Equal("2026-10-03", CellText(cut, "A1")));
    }

    [Fact] // SH-57, ADR-0121: hourly readings that find the same day repaint nothing (that they recalculate nothing is SheetDayTests.The_same_day_changes_nothing, layer 1), and the readings go on hour after hour
    public async Task SH57_readings_that_find_the_same_day_repaint_nothing_and_go_on_each_hour()
    {
        var cut = RenderInLondon();
        var gridRenders = Grid(cut).RenderCount;
        var before = cut.FindComponents<ExGridRow<SheetRow>>().ToDictionary(r => r.Instance.RowIndex, r => (r.Instance.Row, r.RenderCount));

        // Three readings, each an hour apart, all on 2 October.
        for (var hour = 0; hour < 3; hour++)
        {
            Clock.Advance(TimeSpan.FromMinutes(61));
            await cut.InvokeAsync(() => { });
        }

        var after = cut.FindComponents<ExGridRow<SheetRow>>().ToDictionary(r => r.Instance.RowIndex, r => (r.Instance.Row, r.RenderCount));
        Assert.Equal("2026-10-02", CellText(cut, "A1"));
        Assert.Equal(before.Keys, after.Keys);
        foreach (var (index, (row, count)) in after)
        {
            Assert.Same(before[index].Row, row);
            Assert.Equal(before[index].RenderCount, count);
        }
        Assert.Equal(gridRenders, Grid(cut).RenderCount);

        // The readings went on: a fourth, an hour after the third, finds the day the wall clock was set to.
        _wall.Shift = TimeSpan.FromHours(15);
        Clock.Advance(TimeSpan.FromMinutes(61));
        await cut.InvokeAsync(() => { });
        cut.WaitForAssertion(() => Assert.Equal("2026-10-03", CellText(cut, "A1")));
    }
}
