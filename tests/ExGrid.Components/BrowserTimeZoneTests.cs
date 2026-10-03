using Bunit;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The browser tells the grid its time zone (ADR-0122, ADR-0021's ninth entry): asked for only when
/// the Consumer listens, passed on as it came, and checked as anything crossing the JavaScript
/// boundary is.
/// </summary>
public class BrowserTimeZoneTests : GridTestContext
{
    private readonly List<string> _told = [];

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(bool listening)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(5))
              .Add(g => g.Columns, TestRows.Wide(2, widthPx: 99))
              .Add(g => g.ViewportWidth, 600)
              .Add(g => g.ViewportHeight, 300);
            if (listening) ps.Add(g => g.OnBrowserTimeZone, EventCallback.Factory.Create<string>(this, zone => _told.Add(zone)));
        });

    private bool AskedForTheZone => (bool)JSInterop.Invocations.Last(i => i.Identifier == "attach").Arguments[^1]!;

    [Fact] // ADR-0122: a grid no one asks does not ask the browser
    public void A_grid_no_one_asks_does_not_ask_the_browser()
    {
        RenderGrid(listening: false);

        Assert.False(AskedForTheZone);
    }

    [Fact] // ADR-0122: a listening Consumer is told the zone as the browser reported it
    public async Task A_listening_consumer_is_told_the_zone()
    {
        var cut = RenderGrid(listening: true);
        Assert.True(AskedForTheZone);

        await cut.InvokeAsync(() => cut.Instance.OnTimeZoneAsync("Asia/Tokyo"));

        Assert.Equal(["Asia/Tokyo"], _told);
    }

    [Theory] // ADR-0122 / ADR-0021: what crosses the boundary is checked, and nothing that fails the check is passed on
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_report_that_is_no_name_is_dropped(string zone)
    {
        var cut = RenderGrid(listening: true);

        await cut.InvokeAsync(() => cut.Instance.OnTimeZoneAsync(zone));
        await cut.InvokeAsync(() => cut.Instance.OnTimeZoneAsync(new string('x', 101)));

        Assert.Empty(_told);
    }
}
