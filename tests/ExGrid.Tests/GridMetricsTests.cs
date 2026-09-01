using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

/// <summary>Resolution of the single geometry (ADR-0028): presets, per-value
/// precedence, and the bound a density menu greys out with.</summary>
public class GridMetricsTests
{
    [Fact] // ADR-0028: Compact is the default and is bit-for-bit today's numbers
    public void Compact_is_todays_numbers()
    {
        var metrics = GridMetrics.Resolve(GridDensity.Compact);

        Assert.Equal(28, metrics.RowHeightPx);
        Assert.Equal(28, metrics.HeaderHeightPx);
        Assert.Equal(14, metrics.FontSizePx);
        Assert.Equal(9.058, metrics.DigitWidthPx);
        Assert.Equal(8, metrics.CellPaddingXPx);
        // The action chrome ex-grid.css always used: 6px padding, 1px border, 4px gap.
        Assert.Equal((6 * 2) + (1 * 2) + 4, metrics.ActionButtonChromePx);
    }

    [Theory] // ADR-0028: each preset is a complete, self-consistent metric set
    [InlineData(GridDensity.Comfortable, 40, 14, 9.058, 12)]
    [InlineData(GridDensity.Standard, 32, 14, 9.058, 8)]
    [InlineData(GridDensity.Compact, 28, 14, 9.058, 8)]
    [InlineData(GridDensity.Excel, 20, 12, 7.77, 4)]
    public void Each_preset_resolves_whole(
        GridDensity density, double row, double font, double digit, double padding)
    {
        var metrics = GridMetrics.Resolve(density);

        Assert.Equal(row, metrics.RowHeightPx);
        Assert.Equal(row, metrics.HeaderHeightPx);
        Assert.Equal(font, metrics.FontSizePx);
        Assert.Equal(digit, metrics.DigitWidthPx);
        Assert.Equal(padding, metrics.CellPaddingXPx);
    }

    [Fact] // ADR-0028: explicit parameter > preset > default, per value
    public void An_explicit_value_beats_the_preset_per_value()
    {
        var metrics = GridMetrics.Resolve(GridDensity.Excel, rowHeightPx: 22);

        // A 22px row with Excel's font, padding and digit width — the ADR's own example.
        Assert.Equal(22, metrics.RowHeightPx);
        Assert.Equal(12, metrics.FontSizePx);
        Assert.Equal(7.77, metrics.DigitWidthPx);
        Assert.Equal(4, metrics.CellPaddingXPx);
    }

    [Fact] // ADR-0028: an explicit RowHeight moves the header with it — the untouched relationship
    public void Row_height_carries_the_header_unless_the_header_is_set_itself()
    {
        Assert.Equal(22, GridMetrics.Resolve(GridDensity.Compact, rowHeightPx: 22).HeaderHeightPx);
        Assert.Equal(36, GridMetrics.Resolve(GridDensity.Compact, rowHeightPx: 22, headerHeightPx: 36).HeaderHeightPx);
        // A Dense body under a comfortable header — the ordinary design-system request.
        Assert.Equal(40, GridMetrics.Resolve(GridDensity.Excel, headerHeightPx: 40).HeaderHeightPx);
    }

    [Fact] // ADR-0028: CellMetrics stays the explicit override for the measurement pair
    public void Cell_metrics_override_the_presets_pair()
    {
        var metrics = GridMetrics.Resolve(
            GridDensity.Excel, cellMetrics: new CellTextMetrics(11, 10));

        Assert.Equal(11, metrics.DigitWidthPx);
        Assert.Equal(10, metrics.CellPaddingXPx);
        Assert.Equal(12, metrics.FontSizePx); // the rest stays the preset's
    }

    [Fact] // ADR-0028: the bound a density menu greys out with, instead of re-deriving it
    public void The_largest_row_height_is_exposed()
    {
        var largest = GridMetrics.LargestRowHeightFor(1_000_000, headerHeightPx: 28);

        Assert.Equal((ViewportGeometry.MaxScrollHeightPx - 28) / 1_000_000, largest);
        // 1,000,000 rows fit at 28px and do not fit at Comfortable's 40px.
        Assert.True(largest > 28);
        Assert.True(largest < 40);
    }

    [Fact] // A non-positive resolved height is refused at resolution, naming the value
    public void Non_positive_heights_are_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GridMetrics.Resolve(GridDensity.Compact, rowHeightPx: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridMetrics.Resolve(GridDensity.Compact, headerHeightPx: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridMetrics.Resolve(GridDensity.Compact, rowHeightPx: double.NaN));
    }
}
