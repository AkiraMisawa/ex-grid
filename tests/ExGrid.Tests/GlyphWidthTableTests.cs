using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The glyph table (ADR-0016; ticket 83): a letter or currency sign the face draws itself is charged
/// its own measured width, so a month name costs what it paints rather than the other class; the
/// classes still come first, and a glyph the table lacks is the other class.
///
/// <para>The table's own answers are shared cases (<c>tests/cases/columns/glyph-width-table.json</c> and
/// <c>text-metrics.json</c>); what stays here reads the resolved Grid Metrics.</para>
/// </summary>
public class GlyphWidthTableTests
{
    private static readonly GlyphWidthTable Table = new(14, [("M", 13, 14), ("i", 4, 5), ("E", 20, 20), ("₩", 12, 13)]);

    [Theory] // ADR-0016 / ticket 83: the core's defaults carry the table measured at their font size
    [InlineData(GridDensity.Compact, 14)]
    [InlineData(GridDensity.Standard, 14)]
    [InlineData(GridDensity.Comfortable, 14)]
    [InlineData(GridDensity.Excel, 12)]
    public void The_core_defaults_carry_the_table_of_their_size(GridDensity density, double measuredAt)
    {
        var metrics = GridMetrics.Resolve(density).CellMetrics;

        Assert.NotNull(metrics.GlyphWidths);
        Assert.Equal(measuredAt, metrics.GlyphWidths!.MeasuredAtPx);
        // Every Latin letter is in it: DejaVu Sans draws them all.
        Assert.All("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz",
            c => Assert.True(metrics.GlyphWidths.TryGetWidthPx(c, bold: false, out _) || c == 'E', $"'{c}'"));
        // A narrow letter now costs less than a digit, and a wide one more.
        Assert.True(metrics.WidthOf('i') < metrics.DigitWidthPx);
        Assert.True(metrics.WidthOf('M') > metrics.DigitWidthPx);
        Assert.True(metrics.WidthOf('M') < metrics.OtherWidthPx);
    }

    [Fact] // ADR-0028: a Consumer's explicit metrics are taken as given, with no table of the core's
    public void Explicit_metrics_carry_no_table_of_the_cores()
    {
        var metrics = GridMetrics.Resolve(GridDensity.Compact, cellMetrics: new CellTextMetrics(14, 9, 5, 4)).CellMetrics;

        Assert.Null(metrics.GlyphWidths);
        Assert.Equal(18, metrics.WidthOf('M'));
    }

    [Fact] // ADR-0030 / ticket 83: a Wrapper's table travels in its defaults and is scaled to the preset's size
    public void A_wrappers_table_is_scaled_to_the_preset()
    {
        var defaults = new GridPresentationDefaults(10.4, 8.3, 5.8, 14, 10.4, 8.34, 5.25, 12.44, 12.28, glyphWidths: Table);

        var at14 = GridMetrics.Resolve(GridDensity.Compact, defaults: defaults).CellMetrics;
        var at12 = GridMetrics.Resolve(GridDensity.Excel, defaults: defaults).CellMetrics;

        Assert.Same(Table, at14.GlyphWidths);
        Assert.Equal(13, at14.WidthOf('M'), 9);
        Assert.Equal(13 * 12 / 14d, at12.WidthOf('M'), 9);
        Assert.Equal(12.44, at14.WidthOf('W'), 9);
    }
}
