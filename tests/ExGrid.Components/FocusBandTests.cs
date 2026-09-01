using Bunit;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The Focus band (ADR-0008): one more overlay rectangle per layer, off by default,
/// painted beneath the ranges. 100 columns of 100px, 20px rows in a 100px Viewport.
/// </summary>
public class FocusBandTests : GridTestContext
{
    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(bool highlight, int pinned = 0)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(200))
            .Add(g => g.TotalCount, 200)
            .Add(g => g.Columns, TestRows.Wide(100))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 100)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.PinnedColumnCount, pinned)
            .Add(g => g.HighlightFocusRow, highlight));

    private static Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    [Fact] // ADR-0008: off by default, as Excel's own Focus Cell is
    public async Task The_band_is_off_by_default()
    {
        var cut = RenderGrid(highlight: false);
        await ClickAsync(cut, 150, 30);

        Assert.Empty(cut.FindAll(".ex-focus-row"));
    }

    [Fact] // ADR-0008: the band spans the full row, beneath the range
    public async Task The_band_spans_the_focus_row_and_sits_beneath_the_ranges()
    {
        var cut = RenderGrid(highlight: true);
        await ClickAsync(cut, 150, 30); // row 1, column 1

        var band = cut.Find(".ex-focus-row");
        // Row 1 of 20px rows, all 100 columns of 100px: the whole result's width.
        Assert.Equal("left: 0px; top: 20px; width: 10000px; height: 20px", band.GetAttribute("style"));
        // Painted first in its layer, so the range reads over it.
        var layer = cut.Find(".ex-selection");
        Assert.Equal("ex-focus-row", layer.Children[0].ClassName);
    }

    [Fact] // ADR-0008: the band moves with the Focus and no row re-renders for it
    public async Task The_band_follows_the_focus_without_touching_rows()
    {
        var cut = RenderGrid(highlight: true);
        await ClickAsync(cut, 150, 30);
        var counts = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList();

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, false, false, false, false));

        Assert.Contains("top: 40px", cut.Find(".ex-focus-row").GetAttribute("style"));
        Assert.Equal(counts, cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList());
    }

    [Fact] // ADR-0008: the band crosses the pinned boundary as two layers, like every rectangle
    public async Task The_band_splits_across_the_pinned_boundary()
    {
        var cut = RenderGrid(highlight: true, pinned: 2);
        await ClickAsync(cut, 250, 30);

        Assert.Equal(2, cut.FindAll(".ex-focus-row").Count);
    }
}
