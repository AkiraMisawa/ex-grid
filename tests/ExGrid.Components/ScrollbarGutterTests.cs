using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// What the browser reports its scrollbars take, and what the grid does with it
/// (ADR-0013/0021). A classic scrollbar is drawn inside the declared box, so on Windows
/// and Linux the rows and columns get about 15px less than <c>ViewportHeight</c> and
/// <c>ViewportWidth</c> say — and none of it is visible on a machine with overlay
/// scrollbars, where every measurement is 0. That is what these tests stand in for.
///
/// The geometry is the same one the virtualisation tests use: 20px rows in a 100px
/// Viewport, of which the header takes the first 20.
/// </summary>
public class ScrollbarGutterTests : GridTestContext
{
    private const double RowHeightPx = 20;
    private const double GutterPx = 15;

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        double viewportHeight = 100, double viewportWidth = 350, GridColumn<TestRow>[]? columns = null)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(200))
            .Add(g => g.TotalCount, 200)
            .Add(g => g.Columns, columns ?? TestRows.Wide(100))
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, viewportHeight)
            .Add(g => g.ViewportWidth, viewportWidth));

    private static Task ReportGutterAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, double widthPx, double heightPx)
        => cut.InvokeAsync(() => cut.Instance.OnViewportReportAsync(widthPx, heightPx, 0, 0));

    [Fact] // ADR-0013: with overlay scrollbars nothing changes — not the output, and not the render count
    public async Task A_gutter_of_zero_paints_exactly_what_it_painted_before()
    {
        var cut = RenderGrid();
        var rows = cut.FindAll(".ex-row").Count;
        var columns = cut.FindAll(".ex-header-cell").Count;
        var renders = cut.RenderCount;

        await ReportGutterAsync(cut, 0, 0);

        Assert.Equal(rows, cut.FindAll(".ex-row").Count);
        Assert.Equal(columns, cut.FindAll(".ex-header-cell").Count);
        // Not merely the same picture: no render at all. macOS reports 0/0 once when the
        // scroller is first observed, and a grid that re-rendered on that would pay for a
        // platform difference that does not exist there.
        Assert.Equal(renders, cut.RenderCount);
    }

    [Fact] // ADR-0013: the strip comes off the rows, and the slice shrinks by the row it can no longer show
    public async Task A_horizontal_scrollbar_takes_a_row_off_the_slice()
    {
        // 70px of rows under the header: three whole rows plus the one straddling the
        // bottom edge. Take 15 away and 55px is left — two whole rows plus one.
        var cut = RenderGrid(viewportHeight: 90);
        Assert.Equal(5, cut.FindAll(".ex-row").Count);

        await ReportGutterAsync(cut, 0, GutterPx);

        Assert.Equal(4, cut.FindAll(".ex-row").Count);
    }

    [Fact] // ADR-0013: the element keeps its declared size — the scrollbar is drawn inside it
    public async Task The_declared_size_still_drives_the_css()
    {
        var cut = RenderGrid();

        await ReportGutterAsync(cut, GutterPx, GutterPx);

        var style = cut.Find(".ex-scroller").GetAttribute("style")!;
        Assert.Contains("width: 350px", style);
        Assert.Contains("height: 100px", style);
    }

    [Fact] // ADR-0012: the Focus is revealed clear of the vertical scrollbar, not behind it
    public async Task Revealing_a_column_stops_short_of_the_vertical_scrollbar()
    {
        // 100 columns of 100px. Right-aligning column 9 in a 350px Viewport puts the
        // scroll offset at 1000 - 350 = 650; with 15px of it taken by the scrollbar the
        // columns only have 335, so the offset has to be 15px further right.
        var cut = RenderGrid();
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowRight", false, false, false, false, false));
        for (var i = 0; i < 9; i++)
            await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowRight", false, false, false, false, false));

        var withoutBar = Js.ScrolledTo[^1].Left;
        Assert.Equal(650, withoutBar);

        await ReportGutterAsync(cut, GutterPx, 0);
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowRight", false, false, false, false, false));

        // Column 10 right-aligned: 1100 - (350 - 15).
        Assert.Equal(765, Js.ScrolledTo[^1].Left);
    }

    [Fact] // ADR-0012: a bar that appears can cover the Focus where it already stands
    public async Task A_bar_appearing_pulls_the_focus_back_into_view()
    {
        var cut = RenderGrid();
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("End", true, false, false, false, false));
        var revealed = Js.ScrolledTo.Count;

        // The Focus is flush with the right edge of a Viewport that has just lost 15px of
        // it. Nothing moved the Focus, so nothing else would go looking.
        await ReportGutterAsync(cut, GutterPx, 0);

        Assert.Equal(revealed + 1, Js.ScrolledTo.Count);
    }

    [Fact] // ADR-0012: a bar that goes away cannot have hidden anything, so nothing is scrolled
    public async Task A_bar_disappearing_does_not_move_the_viewport()
    {
        var cut = RenderGrid();
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("End", true, false, false, false, false));
        await ReportGutterAsync(cut, GutterPx, 0);
        var scrolls = Js.ScrolledTo.Count;

        await ReportGutterAsync(cut, 0, 0);

        Assert.Equal(scrolls, Js.ScrolledTo.Count);
    }

    [Fact] // A report that says nothing new costs nothing
    public async Task An_unchanged_gutter_is_not_a_render()
    {
        var cut = RenderGrid();
        await ReportGutterAsync(cut, GutterPx, GutterPx);
        var renders = cut.RenderCount;

        await ReportGutterAsync(cut, GutterPx, GutterPx);

        Assert.Equal(renders, cut.RenderCount);
    }

    [Theory] // Nothing crossing the JS boundary is trusted: a NaN would travel into every offset
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.NaN)]
    [InlineData(double.PositiveInfinity, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public async Task A_gutter_that_is_not_a_width_is_ignored(double widthPx, double heightPx)
    {
        var cut = RenderGrid();
        var rows = cut.FindAll(".ex-row").Count;

        await ReportGutterAsync(cut, widthPx, heightPx);

        Assert.Equal(rows, cut.FindAll(".ex-row").Count);
    }

    [Fact] // Rather than be quietly wrong: a Viewport the scrollbar leaves no room in says so
    public async Task A_gutter_that_swallows_the_viewport_is_refused_by_name()
    {
        // 30px is a legal Viewport for a 20px row — until a scrollbar takes half of it.
        var cut = RenderGrid(viewportHeight: 30);

        await ReportGutterAsync(cut, 0, GutterPx);

        // It surfaces through the renderer rather than back through the JavaScript that
        // reported the gutter: a browser console is not where a Consumer looks for the
        // reason its grid stopped painting.
        Assert.True(Renderer.UnhandledException.IsCompleted);
        var error = Assert.IsType<InvalidOperationException>(await Renderer.UnhandledException);
        Assert.Contains("scrollbar", error.Message);
        Assert.Contains("overlay", error.Message);
    }

    [Fact] // ADR-0018: a report can overtake disposal, and a disposed grid has nothing to re-render
    public async Task A_report_after_disposal_is_harmless()
    {
        var cut = RenderGrid();
        var instance = cut.Instance;
        await cut.InvokeAsync(() => ((IAsyncDisposable)instance).DisposeAsync().AsTask());

        await instance.OnViewportReportAsync(GutterPx, GutterPx, 0, 0);
    }
}
