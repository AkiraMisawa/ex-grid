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
        Assert.Equal(9.742, metrics.DigitWidthPx);
        Assert.Equal(8, metrics.CellPaddingXPx);
        // The action chrome ex-grid.css always used: 6px padding, 1px border, 4px gap.
        Assert.Equal((6 * 2) + (1 * 2) + 4, metrics.ActionButtonChromePx);
    }

    [Theory] // ADR-0028: each preset is a complete, self-consistent metric set
    [InlineData(GridDensity.Comfortable, 40, 14, 9.742, 12)]
    [InlineData(GridDensity.Standard, 32, 14, 9.742, 8)]
    [InlineData(GridDensity.Compact, 28, 14, 9.742, 8)]
    [InlineData(GridDensity.Excel, 20, 12, 8.351, 4)]
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
        Assert.Equal(8.351, metrics.DigitWidthPx);
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

    [Theory] // ADR-0016 (2026-09-25): each default is the widest measured for its class on any platform
    [InlineData(GridDensity.Compact, 14.028, 9.742, 6.398, 14)]
    [InlineData(GridDensity.Standard, 14.028, 9.742, 6.398, 14)]
    [InlineData(GridDensity.Comfortable, 14.028, 9.742, 6.398, 14)]
    [InlineData(GridDensity.Excel, 12.024, 8.351, 5.484, 12)]
    public void The_default_widths_cover_the_widest_platform_measured(
        GridDensity density, double wide, double digit, double narrow, double fullWidth)
    {
        var metrics = GridMetrics.Resolve(density).CellMetrics;

        Assert.Equal(wide, metrics.WideWidthPx);
        Assert.Equal(digit, metrics.DigitWidthPx);
        Assert.Equal(narrow, metrics.NarrowWidthPx);
        Assert.Equal(fullWidth, metrics.FullWidthPx);
    }

    [Fact] // ADR-0016: DejaVu Sans Bold paints "123,456,789,012.50" at 157.66px; the default must not under-charge it
    public void The_default_estimate_covers_a_bold_amount_on_linux()
    {
        var metrics = GridMetrics.Resolve(GridDensity.Compact).CellMetrics;

        Assert.True(metrics.TextWidthPx("123,456,789,012.50") >= 157.66);
    }

    [Fact] // ADR-0016 (2026-09-25): a header is its label, one em of slack, the menu band and the sort room
    public void A_header_requires_its_label_slack_menu_band_and_sort_room()
    {
        var metrics = GridMetrics.Resolve(GridDensity.Compact);
        var label = metrics.CellMetrics.EstimatePx("Amount");

        Assert.Equal(label + 14, metrics.HeaderRequiredPx("Amount", menuButton: false, sortable: false));
        Assert.Equal(label + 14 + metrics.MenuButtonBandPx,
            metrics.HeaderRequiredPx("Amount", menuButton: true, sortable: false));
        // The sort mark's box: an em for ▲, which is ambiguous-width and drawn at an em
        // in a CJK family, and a 6px gap — the box the stylesheet sizes it with.
        Assert.Equal(20, metrics.SortMarkWidthPx);
        Assert.Equal(label + 14 + 20,
            metrics.HeaderRequiredPx("Amount", menuButton: false, sortable: true), 9);
    }

    [Fact] // ADR-0016: DejaVu Sans Bold paints "Amount" at 61.7px and "MARKET VALUE" at 120.5px; neither may be cut
    public void The_measured_short_headers_fit_their_required_width()
    {
        var metrics = GridMetrics.Resolve(GridDensity.Compact);
        var padding = 2 * metrics.CellPaddingXPx;

        Assert.True(metrics.HeaderRequiredPx("Amount", false, false) - padding >= 61.7);
        Assert.True(metrics.HeaderRequiredPx("MARKET VALUE", false, false) - padding >= 120.5);
        Assert.True(metrics.HeaderRequiredPx("評価額", false, false) - padding >= 42.0);
    }

    [Fact] // ADR-0016: a full-width character is an em, so the grid charges explicit metrics at least the font size for it
    public void Explicit_metrics_are_charged_an_em_for_full_width_characters()
    {
        var uniform = GridMetrics.Resolve(GridDensity.Compact, cellMetrics: new CellTextMetrics(9, 8));
        var generous = GridMetrics.Resolve(GridDensity.Compact, cellMetrics: new CellTextMetrics(20, 12, 6, 30, 8));

        Assert.Equal(14, uniform.CellMetrics.FullWidthPx);
        Assert.Equal(9, uniform.CellMetrics.DigitWidthPx); // nothing else moves
        Assert.Equal(30, generous.CellMetrics.FullWidthPx); // wider than an em is the theme's to say
    }
}
