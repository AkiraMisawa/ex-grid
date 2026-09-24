using Bunit;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The hover band (ADR-0029, ADR-0021's fifth entry): the browser reports the pointer's
/// offsets when it moves onto another row, the core resolves the cell, and the band is
/// painted as the Focus band is — one overlay rectangle per layer, no row told. Off by
/// default, and while off the browser is not asked to report rows at all. 100 columns
/// of 100px, 20px rows in a 100px Viewport.
/// </summary>
public class HoverBandTests : GridTestContext
{
    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(bool hover, int pinned = 0)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(200))
            .Add(g => g.TotalCount, 200)
            .Add(g => g.Columns, TestRows.Wide(100))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 100)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.PinnedColumnCount, pinned)
            .Add(g => g.HighlightHoverRow, hover));

    /// <summary>What the browser sends: the offsets of a move that crossed a row.</summary>
    private static Task ReportAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.InvokeAsync(() => cut.Instance.OnPointerRowAsync(x, y));

    private static List<int> RowRenderCounts(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList();

    private static bool RowsReported(Bunit.JSRuntimeInvocation invocation) => (bool)invocation.Arguments[0]!;

    [Fact] // ADR-0021: off by default, the browser is never asked to report rows
    public void Off_by_default_the_browser_is_not_asked()
    {
        var cut = RenderGrid(hover: false);

        Assert.DoesNotContain(Js.PointerReporting.Invocations, i => RowsReported(i));
    }

    [Fact] // ADR-0021: a report that arrives while the band is off paints nothing
    public async Task A_report_while_off_paints_nothing()
    {
        var cut = RenderGrid(hover: false);
        await ReportAsync(cut, 150, 30);

        Assert.Empty(cut.FindAll(".ex-hover-row"));
    }

    [Fact] // ADR-0021: on, the browser is told to report rows, once
    public void On_the_browser_is_told_once()
    {
        var cut = RenderGrid(hover: true);

        Assert.Single(Js.PointerReporting.Invocations, i => RowsReported(i));
    }

    [Fact] // ADR-0029: the band spans the row the offsets resolve to, and no row re-renders for it
    public async Task The_band_follows_the_report_without_touching_rows()
    {
        var cut = RenderGrid(hover: true);
        var counts = RowRenderCounts(cut);

        await ReportAsync(cut, 350, 30); // row 1, column 3

        var band = cut.Find(".ex-hover-row");
        Assert.Equal("left: 0px; top: 20px; width: 10000px; height: 20px", band.GetAttribute("style"));
        Assert.Equal(counts, RowRenderCounts(cut));
        // Nothing is selected: the band stands alone, no Focus outline is invented.
        Assert.Empty(cut.FindAll(".ex-focus"));
    }

    [Fact] // RR-11: the same cell again renders nothing at all
    public async Task The_same_cell_again_renders_nothing()
    {
        var cut = RenderGrid(hover: true);
        await ReportAsync(cut, 350, 50);
        var renders = cut.RenderCount;

        await ReportAsync(cut, 360, 55);

        Assert.Equal(renders, cut.RenderCount);
    }

    [Fact] // ADR-0021: a column change alone moves no band and renders nothing
    public async Task A_column_change_alone_renders_nothing()
    {
        var cut = RenderGrid(hover: true);
        await ReportAsync(cut, 350, 50);
        var renders = cut.RenderCount;

        await ReportAsync(cut, 450, 50);

        Assert.Equal(renders, cut.RenderCount);
        Assert.Contains("top: 40px", cut.Find(".ex-hover-row").GetAttribute("style"));
    }

    [Fact] // ADR-0029: onto the header there is no row under the pointer — the band goes, and only the band
    public async Task Onto_the_header_only_the_band_goes()
    {
        var cut = RenderGrid(hover: true);
        await ReportAsync(cut, 350, 50);

        await cut.InvokeAsync(() => cut.Instance.OnPointerAwayAsync(true));

        Assert.Empty(cut.FindAll(".ex-hover-row"));
    }

    [Fact] // ADR-0029: the pointer leaving takes the band with it
    public async Task The_band_goes_when_the_pointer_leaves()
    {
        var cut = RenderGrid(hover: true);
        await ReportAsync(cut, 350, 50);

        await cut.InvokeAsync(() => cut.Instance.OnPointerAwayAsync(false));

        Assert.Empty(cut.FindAll(".ex-hover-row"));
    }

    [Fact] // ADR-0029: the rows moved under a still pointer — the band goes with them
    public async Task The_band_goes_when_the_rows_scroll_under_it()
    {
        var cut = RenderGrid(hover: true);
        await ReportAsync(cut, 350, 50);

        await ScrollToAsync(cut.Find(".ex-scroller"), 400);

        Assert.Empty(cut.FindAll(".ex-hover-row"));
        // And the browser's memory goes with it, so a move that was reported between
        // the scroll event and this paint is not held against the next one.
        Assert.Single(Js.PointerForgotten.Invocations);
    }

    [Fact] // ADR-0008: the band crosses the pinned boundary as two layers, like every rectangle
    public async Task The_band_splits_across_the_pinned_boundary()
    {
        var cut = RenderGrid(hover: true, pinned: 2);

        await ReportAsync(cut, 250, 30);

        Assert.Equal(2, cut.FindAll(".ex-hover-row").Count);
    }

    [Fact] // ADR-0008: the band sits beneath the selection when both are painted
    public async Task The_band_sits_beneath_the_selection()
    {
        var cut = RenderGrid(hover: true);
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 150, OffsetY = 30 });
        await ReportAsync(cut, 150, 70);

        var layer = cut.Find(".ex-selection");
        Assert.Equal("ex-hover-row", layer.Children[0].ClassName);
        Assert.NotEmpty(cut.FindAll(".ex-focus"));
    }

    [Fact] // ADR-0021: turning the band off tells the browser to stop, and drops the band
    public async Task Turning_it_off_stops_the_report()
    {
        var cut = RenderGrid(hover: true);
        await ReportAsync(cut, 350, 50);

        cut.Render(ps => ps.Add(g => g.HighlightHoverRow, false));

        Assert.Contains(Js.PointerReporting.Invocations, i => !RowsReported(i));
        Assert.Empty(cut.FindAll(".ex-hover-row"));
    }
}
