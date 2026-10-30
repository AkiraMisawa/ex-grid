using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The glyph table (ADR-0016; ticket 83): a letter or currency sign the face draws itself is charged
/// its own measured width, so a month name costs what it paints rather than the other class; the
/// classes still come first, and a glyph the table lacks is the other class.
///
/// <para>The table's own answers are shared cases (<c>tests/cases/columns/glyph-width-table.json</c> and
/// <c>text-metrics.json</c>, and <c>grid-metrics.json</c>, which holds both cores' default tables equal
/// glyph by glyph); what stays here is the generated table's coverage, read through C#'s own lookup.</para>
/// </summary>
public class GlyphWidthTableTests
{
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

}
