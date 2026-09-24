using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// What a scrollbar takes out of the Viewport, and what it must not take out of it
/// (ADR-0013). Every case here is invisible on macOS, where the gutter is always 0 —
/// which is the reason the invariant that a 0 gutter changes nothing is the first test
/// in the file rather than an afterthought.
/// </summary>
public class ViewportBoxTests
{
    [Fact] // ADR-0013: with overlay scrollbars the geometry is exactly what it was before the gutter existed
    public void A_gutter_of_zero_leaves_the_declared_size_untouched()
    {
        var box = new ViewportBox(800, 600, 0, 0);

        // Bit-for-bit, not approximately: this is what makes measuring the gutter a fix
        // for Windows and Linux rather than a change to how macOS behaves.
        Assert.Equal(800, box.VisibleWidthPx);
        Assert.Equal(600, box.VisibleHeightPx);
        Assert.Equal(box, ViewportBox.Declared(800, 600));
    }

    [Fact] // ADR-0013: a fractional declared size survives a zero gutter unrounded
    public void A_zero_gutter_does_not_round_a_fractional_size()
    {
        var box = ViewportBox.Declared(823.4375, 617.8125);

        Assert.Equal(823.4375, box.VisibleWidthPx);
        Assert.Equal(617.8125, box.VisibleHeightPx);
    }

    [Fact] // ADR-0013: a classic scrollbar takes its strip out of the declared box, not from beside it
    public void A_classic_scrollbar_comes_off_the_declared_size()
    {
        var box = new ViewportBox(900, 600, 15, 15);

        Assert.Equal(885, box.VisibleWidthPx);
        Assert.Equal(585, box.VisibleHeightPx);
        // The Consumer's own numbers are unchanged: ViewportWidth still means the
        // element's outer size, and the CSS keeps being written from it.
        Assert.Equal(900, box.OuterWidthPx);
        Assert.Equal(600, box.OuterHeightPx);
    }

    [Fact] // ADR-0013: the two axes are independent — one bar can be present without the other
    public void Each_axis_loses_only_its_own_bar()
    {
        var box = new ViewportBox(900, 600, 15, 0);

        Assert.Equal(885, box.VisibleWidthPx);
        Assert.Equal(600, box.VisibleHeightPx);
    }

    [Fact] // ADR-0021: an observation replaces the gutter and leaves the declared size alone
    public void Applying_an_observation_keeps_the_declared_size()
    {
        var observed = ViewportBox.Declared(900, 600).WithGutter(15, 15);

        Assert.Equal(900, observed.OuterWidthPx);
        Assert.Equal(600, observed.OuterHeightPx);
        Assert.Equal(885, observed.VisibleWidthPx);
        // And going back to no bar restores exactly the original box, which is what the
        // "the bar disappeared" notification has to be able to do.
        Assert.Equal(ViewportBox.Declared(900, 600), observed.WithGutter(0, 0));
    }

    [Theory] // ADR-0013: a Viewport is a finite, positive number of pixels
    [InlineData(0, 600)]
    [InlineData(-1, 600)]
    [InlineData(double.NaN, 600)]
    [InlineData(double.PositiveInfinity, 600)]
    [InlineData(800, 0)]
    [InlineData(800, -1)]
    [InlineData(800, double.NaN)]
    [InlineData(800, double.PositiveInfinity)]
    public void An_unusable_declared_size_is_refused(double widthPx, double heightPx)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportBox(widthPx, heightPx, 0, 0));
    }

    [Theory] // ADR-0013: a scrollbar takes a strip or takes nothing — never a negative one
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.NaN)]
    [InlineData(double.PositiveInfinity, 0)]
    [InlineData(0, double.PositiveInfinity)]
    public void A_gutter_that_is_not_a_width_is_refused(double gutterWidthPx, double gutterHeightPx)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ViewportBox(800, 600, gutterWidthPx, gutterHeightPx));
    }

    [Fact] // Rather than be quietly wrong: a Viewport with nothing left to paint in says so
    public void A_gutter_that_swallows_the_viewport_is_refused_by_name()
    {
        // Not "ViewportWidth is not positive": the Consumer wrote 12, and 12 is a
        // perfectly ordinary number until a 15px scrollbar is drawn inside it. The
        // message has to name the thing that actually went wrong, because on the
        // developer's Mac this never happens at all.
        var width = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ViewportBox(12, 600, 15, 0));
        Assert.Equal("gutterWidthPx", width.ParamName);
        Assert.Contains("scrollbar", width.Message);

        var height = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ViewportBox(800, 15, 0, 15));
        Assert.Equal("gutterHeightPx", height.ParamName);
    }

    [Fact] // ADR-0013: the geometry types take the visible size, and refuse the degenerate one themselves
    public void The_visible_size_is_what_the_geometry_is_built_from()
    {
        var box = new ViewportBox(900, 600, 15, 15);
        var rows = new ViewportGeometry(28, box.VisibleHeightPx - 28, 1000);
        var columns = new ColumnGeometry([100, 100, 100], 0, box.VisibleWidthPx);

        // 557px of rows rather than 572: the row that would have been half behind the
        // horizontal scrollbar is one the slice no longer promises is readable.
        Assert.Equal(557, rows.ViewportHeightPx);
        Assert.Equal(885, columns.ViewportWidthPx);
    }
}
