using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// What a scrollbar takes out of the Viewport (ADR-0013). The box's own arithmetic is shared cases
/// (<c>tests/cases/viewport/box.json</c>); what stays here is the geometry built from what it leaves.
/// </summary>
public class ViewportBoxTests
{
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
