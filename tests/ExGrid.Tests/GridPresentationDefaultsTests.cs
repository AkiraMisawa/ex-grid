using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// A Wrapper's cascaded presentation defaults (ADR-0030): the glyph widths of its
/// font, scaled to the preset's size, standing between an explicit CellMetrics and
/// the preset's own (ADR-0028's precedence, one rung lower).
/// </summary>
public class GridPresentationDefaultsTests
{
    // Roboto at 14px, 600 weight, measured in Chrome (see ExGrid.MudBlazor): declared
    // a shade over the measurement, the safe direction (ADR-0016).
    private static readonly GridPresentationDefaults Roboto = new(10.4, 8.0, 4.95, 14);

    [Fact] // ADR-0030: the defaults' widths replace the preset's, the preset keeps its padding
    public void The_defaults_supply_the_widths_and_the_preset_keeps_its_padding()
    {
        var metrics = GridMetrics.Resolve(GridDensity.Compact, defaults: Roboto);

        Assert.Equal(8.0, metrics.DigitWidthPx);
        Assert.Equal(10.4, metrics.CellMetrics.WideWidthPx);
        Assert.Equal(4.95, metrics.CellMetrics.NarrowWidthPx);
        Assert.Equal(8, metrics.CellPaddingXPx);
        Assert.Equal(28, metrics.RowHeightPx);
    }

    [Fact] // ADR-0030/0028: the widths follow the preset's font size, as the Excel trio does
    public void The_widths_are_scaled_to_the_presets_font_size()
    {
        var metrics = GridMetrics.Resolve(GridDensity.Excel, defaults: Roboto);

        Assert.Equal(12, metrics.FontSizePx);
        Assert.Equal(8.0 * 12 / 14, metrics.DigitWidthPx, precision: 9);
        Assert.Equal(10.4 * 12 / 14, metrics.CellMetrics.WideWidthPx, precision: 9);
        Assert.Equal(4, metrics.CellPaddingXPx);
    }

    [Fact] // ADR-0028: an explicit CellMetrics still beats the cascaded defaults
    public void Explicit_metrics_beat_the_defaults()
    {
        var explicitMetrics = new CellTextMetrics(12, 9, 5, 6);

        var metrics = GridMetrics.Resolve(GridDensity.Compact, cellMetrics: explicitMetrics, defaults: Roboto);

        Assert.Equal(explicitMetrics, metrics.CellMetrics);
    }

    [Fact] // ADR-0030: no defaults means the preset's own widths, unchanged
    public void Without_defaults_the_preset_stands()
    {
        var metrics = GridMetrics.Resolve(GridDensity.Compact);

        Assert.Equal(9.058, metrics.DigitWidthPx);
    }

    [Theory] // ADR-0016: a bad measurement fails where it was written
    [InlineData(7.0, 8.0, 4.0, 14)]     // wide narrower than the digit
    [InlineData(10.0, 8.0, 9.0, 14)]    // narrow wider than the digit
    [InlineData(10.0, 0, 4.0, 14)]      // no digit
    [InlineData(10.0, 8.0, 4.0, 0)]     // no size
    [InlineData(10.0, 8.0, 4.0, double.NaN)]
    public void A_bad_measurement_is_refused_at_construction(double wide, double digit, double narrow, double font)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridPresentationDefaults(wide, digit, narrow, font));
    }

    [Fact] // ADR-0030: the Density it carries is optional and read by the component, not here
    public void Density_is_carried_and_optional()
    {
        Assert.Null(Roboto.Density);
        Assert.Equal(GridDensity.Standard, new GridPresentationDefaults(10.4, 8.0, 4.95, 14, GridDensity.Standard).Density);
    }
}
