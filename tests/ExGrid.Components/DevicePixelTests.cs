using System.Globalization;
using System.Text.RegularExpressions;
using Bunit;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The browser tells the grid its Device Pixel (ADR-0090, ADR-0021's eighth entry): the root
/// carries it inline as <c>--ex-dp</c>, and every column edge is put on it. Four columns of
/// 99px, which at 150% lie on half a Device Pixel every second edge.
/// </summary>
public class DevicePixelTests : GridTestContext
{
    private IRenderedComponent<ExGrid<TestRow>> RenderGrid()
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(5))
            .Add(g => g.Columns, TestRows.Wide(4, widthPx: 99))
            .Add(g => g.ViewportWidth, 600)
            .Add(g => g.ViewportHeight, 300));

    private static double[] CellWidths(IRenderedComponent<ExGrid<TestRow>> cut)
        => [.. cut.FindAll(".ex-row")[0].QuerySelectorAll(".ex-cell")
            .Select(cell => double.Parse(Regex.Match(cell.GetAttribute("style")!, @"width: (?<px>[-\d.E+]+)px").Groups["px"].Value, CultureInfo.InvariantCulture))];

    [Fact] // ADR-0090: untold, the stylesheet's steps stand and the columns are painted as declared
    public void Untold_the_columns_are_painted_as_declared()
    {
        var cut = RenderGrid();

        Assert.DoesNotContain("--ex-dp", cut.Find(".ex-grid").GetAttribute("style"));
        Assert.Equal([99, 99, 99, 99], CellWidths(cut));
    }

    [Fact] // ADR-0090 / VZ-16 / VZ-17: told, the root carries the Device Pixel and every edge lies on one
    public async Task Told_the_root_carries_the_device_pixel_and_edges_lie_on_it()
    {
        var cut = RenderGrid();

        await cut.InvokeAsync(() => cut.Instance.OnDevicePixelAsync(1.5));

        Assert.Contains(FormattableString.Invariant($"--ex-dp: {1 / 1.5}px"), cut.Find(".ex-grid").GetAttribute("style"));
        var widths = CellWidths(cut);
        // 148.5 Device Pixels each: the edges fall at 149, 297, 446 and 594.
        Assert.Equal([149 / 1.5, 148 / 1.5, 149 / 1.5, 148 / 1.5], widths.Select(w => Math.Round(w, 9)).ToArray(),
            new RoundedComparer());
    }

    [Fact] // ADR-0090 / ADR-0003: a change of resolution repaints the rows once, and a repeated report nothing
    public async Task A_change_of_resolution_repaints_the_rows_once()
    {
        var cut = RenderGrid();
        await cut.InvokeAsync(() => cut.Instance.OnDevicePixelAsync(1.5));
        var before = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToArray();

        await cut.InvokeAsync(() => cut.Instance.OnDevicePixelAsync(1.5));
        Assert.Equal(before, cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToArray());

        await cut.InvokeAsync(() => cut.Instance.OnDevicePixelAsync(2.25));
        Assert.All(cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).Zip(before),
            pair => Assert.Equal(pair.Second + 1, pair.First));
    }

    [Theory] // ADR-0090: a report that is not a finite, positive ratio is ignored
    [InlineData(0.0)]
    [InlineData(-2.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task A_report_that_is_not_a_ratio_is_ignored(double ratio)
    {
        var cut = RenderGrid();

        await cut.InvokeAsync(() => cut.Instance.OnDevicePixelAsync(ratio));

        Assert.DoesNotContain("--ex-dp", cut.Find(".ex-grid").GetAttribute("style"));
        Assert.Equal([99, 99, 99, 99], CellWidths(cut));
    }

    private sealed class RoundedComparer : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) < 1e-9;
        public int GetHashCode(double obj) => 0;
    }
}
