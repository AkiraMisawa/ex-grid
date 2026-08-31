using Bunit;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// "Go to the beginning" is not the same request as "make this cell visible", and Pinned
/// Columns are where the two come apart (ADR-0012 / ADR-0004).
///
/// A pinned column covers the Viewport's left edge, so it is already whole on screen and
/// revealing it moves nothing — correct for an arrow key, wrong for Home and Ctrl+Home,
/// which would then leave the Viewport parked halfway along a hundred columns. Measured
/// in a browser before this was fixed: the same Ctrl+Home left the offset at 8170px with
/// two columns pinned and at 0px with none, so pinning was changing what the key did.
/// Every case here is therefore run both ways, and the point is that they agree.
///
/// 100 columns of 100px in a 350px Viewport, 20px rows in a 100px Viewport, 200 rows.
/// </summary>
public class GoToStartTests : GridTestContext
{
    private const double RowHeightPx = 20;

    /// <summary>The last column right-aligned: 100 columns of 100px, less the Viewport.
    /// The pinned pair covers the Viewport rather than the content, so it does not move
    /// where the content's right edge lands.</summary>
    private const double FarRightPx = 10_000 - 350;

    /// <summary>The last row, bottom-aligned in what the header leaves.</summary>
    private const double FarBottomPx = (200 * RowHeightPx) - (100 - RowHeightPx);

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(int pinnedColumnCount)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(200))
            .Add(g => g.TotalCount, 200)
            .Add(g => g.Columns, TestRows.Wide(100))
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, 100)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.PinnedColumnCount, pinnedColumnCount));

    private static Task PressAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, false, false, false));

    /// <summary>
    /// Puts the Focus on the last cell and the Viewport at the far corner — the state
    /// every case here starts from.
    ///
    /// The scroll is fed back because that is the real loop: the grid asks the browser to
    /// scroll, the browser answers with a scroll event, and the grid's own offsets come
    /// from that answer. Without it the model still believes it is at 0,0 and the next
    /// reveal computes from the wrong place.
    /// </summary>
    private async Task GoToTheFarCornerAsync(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        // On an empty selection the first key only places the Focus (ADR-0012), so this
        // is what gives the grid something to move from.
        await PressAsync(cut, "ArrowDown");
        await PressAsync(cut, "End", ctrl: true);

        Assert.Equal((FarBottomPx, FarRightPx), Js.ScrolledTo[^1]);
        await ScrollToAsync(cut.Find(".ex-scroller"), FarBottomPx, FarRightPx);
    }

    [Theory] // ADR-0012: Ctrl+Home is the start of the result, and pinning must not change that
    [InlineData(0)]
    [InlineData(2)]
    public async Task Control_home_returns_the_viewport_to_the_start(int pinnedColumnCount)
    {
        var cut = RenderGrid(pinnedColumnCount);
        await GoToTheFarCornerAsync(cut);

        await PressAsync(cut, "Home", ctrl: true);

        Assert.Equal((0d, 0d), Js.ScrolledTo[^1]);
    }

    [Theory] // ADR-0012: Home is the start of the row — the same rule on one axis
    [InlineData(0)]
    [InlineData(2)]
    public async Task Home_returns_the_viewport_to_the_start_of_the_row(int pinnedColumnCount)
    {
        var cut = RenderGrid(pinnedColumnCount);
        await GoToTheFarCornerAsync(cut);

        await PressAsync(cut, "Home");

        // The row does not change, so only the horizontal axis moves.
        Assert.Equal((FarBottomPx, 0d), Js.ScrolledTo[^1]);
    }

    [Theory] // ADR-0012: Shift+Home extends to the same edge, so it lands in the same place
    [InlineData(0)]
    [InlineData(2)]
    public async Task Shift_home_goes_to_the_start_too(int pinnedColumnCount)
    {
        var cut = RenderGrid(pinnedColumnCount);
        await GoToTheFarCornerAsync(cut);

        await PressAsync(cut, "Home", shift: true);

        Assert.Equal(0d, Js.ScrolledTo[^1].Left);
    }

    [Theory] // ADR-0012: Ctrl+Left names the first column, exactly as Home does
    [InlineData(0)]
    [InlineData(2)]
    public async Task Control_left_returns_the_viewport_to_the_start_of_the_row(int pinnedColumnCount)
    {
        var cut = RenderGrid(pinnedColumnCount);
        await GoToTheFarCornerAsync(cut);

        await PressAsync(cut, "ArrowLeft", ctrl: true);

        Assert.Equal(0d, Js.ScrolledTo[^1].Left);
    }

    [Theory] // ADR-0012: the other end is unchanged — Ctrl+End right-aligns, pinned or not
    [InlineData(0)]
    [InlineData(2)]
    public async Task Control_end_still_right_aligns_the_last_column(int pinnedColumnCount)
    {
        var cut = RenderGrid(pinnedColumnCount);

        await GoToTheFarCornerAsync(cut);

        Assert.Equal((FarBottomPx, FarRightPx), Js.ScrolledTo[^1]);
    }

    [Fact] // ADR-0004: a Pinned Column is already visible, so an ordinary move must not chase it
    public async Task A_vertical_move_on_a_pinned_cell_leaves_the_viewport_where_it_is()
    {
        // The trap the fix had to avoid. Making ScrollLeftToReveal answer 0 for a pinned
        // column would satisfy every case above and break this one: reveal runs for EVERY
        // keyboard move, including the ones that name no column at all, so a user who had
        // scrolled right and pressed Down would be yanked back to the first column.
        var cut = RenderGrid(pinnedColumnCount: 2);
        await GoToTheFarCornerAsync(cut);
        await PressAsync(cut, "Home", ctrl: true);          // Focus onto a pinned column
        await ScrollToAsync(cut.Find(".ex-scroller"), 0, 5_000);
        var scrolls = Js.ScrolledTo.Count;

        await PressAsync(cut, "ArrowDown");

        Assert.Equal(scrolls, Js.ScrolledTo.Count);
    }

    [Fact] // ADR-0012: a step towards the start is a step, not a jump to it
    public async Task A_plain_left_arrow_does_not_go_to_the_start()
    {
        var cut = RenderGrid(pinnedColumnCount: 2);
        await GoToTheFarCornerAsync(cut);

        await PressAsync(cut, "ArrowLeft");

        // Column 98 begins at 9800 and the pinned pair covers the Viewport's first 200px,
        // so showing it whole means 9600 — one column's worth of movement, not a jump to
        // the beginning of the row.
        Assert.Equal(9_600d, Js.ScrolledTo[^1].Left);
    }
}
