using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The glyph table (ADR-0016; ticket 83): a letter or currency sign the face draws itself is charged
/// its own measured width, so a month name costs what it paints rather than the other class; the
/// classes still come first, and a glyph the table lacks is the other class.
/// </summary>
public class GlyphWidthTableTests
{
    private static readonly GlyphWidthTable Table = new(14, [("M", 13, 14), ("i", 4, 5), ("E", 20, 20), ("₩", 12, 13)]);

    // Wide 14, digit 9, narrow 5, full-width 16, no padding; bold 15 / 10 / 6; other 18, bold other 19.
    private static readonly CellTextMetrics Metrics =
        new CellTextMetrics(14, 9, 5, 16, 0, 15, 10, 6, 18, 19).WithGlyphWidths(Table, 14);

    [Fact] // ADR-0151: label geometry sent to another process includes resolved glyph scale and weight.
    public void ADR0151_Exported_glyph_widths_use_the_resolved_metrics()
    {
        var metrics = Metrics.WithGlyphWidths(Table, 28).Bold;
        var widths = metrics.ExportGlyphWidths();
        Assert.Equal(28, widths['M']);
        Assert.Equal(10, widths['i']);
        Assert.Equal(10, widths['E']);
        Assert.Empty(metrics.For(ExGrid.ColumnType.Text).ExportGlyphWidths());
    }

    [Fact] // ADR-0016 / ticket 83: a glyph the table holds is charged its own width, at either weight
    public void A_glyph_the_table_holds_is_charged_its_own_width()
    {
        Assert.Equal(13, Metrics.WidthOf('M'));
        Assert.Equal(4, Metrics.WidthOf('i'));
        Assert.Equal(12, Metrics.WidthOf('₩'));
        Assert.Equal(14, Metrics.Bold.WidthOf('M'));
        Assert.Equal(5, Metrics.Bold.WidthOf('i'));
        Assert.Equal(13 + 4, Metrics.TextWidthPx("Mi"));
    }

    [Fact] // ADR-0016, principle 1: a glyph the table lacks is the other class, the widest measured
    public void A_glyph_the_table_lacks_is_the_other_class()
    {
        Assert.Equal(18, Metrics.WidthOf('W'));
        Assert.Equal(19, Metrics.Bold.WidthOf('W'));
        Assert.Equal(18, Metrics.WidthOf('ก'));
    }

    [Fact] // ADR-0016: a class outranks the table, so a digit-class glyph keeps its class
    public void A_class_outranks_the_table()
    {
        Assert.Equal(9, Metrics.WidthOf('E'));
        Assert.Equal(16, Metrics.WidthOf('評'));
        Assert.Equal(14, Metrics.WidthOf('%'));
    }

    [Fact] // ADR-0016 / ADR-0028: the table is scaled from the size it was measured at to the metrics' own
    public void The_table_is_scaled_to_the_metrics_font_size()
    {
        var at12 = new CellTextMetrics(14, 9, 5, 16, 0, 15, 10, 6, 18, 19).WithGlyphWidths(Table, 12);

        Assert.Equal(13 * 12 / 14d, at12.WidthOf('M'), 9);
        Assert.Equal(14 * 12 / 14d, at12.Bold.WidthOf('M'), 9);
    }

    [Fact] // ADR-0016: text and labels charge letters at the digit, and bold composes either way round
    public void Text_charges_letters_at_the_digit_with_or_without_bold()
    {
        Assert.Equal(9, Metrics.For(ColumnType.Text).WidthOf('M'));
        Assert.Equal(10, Metrics.For(ColumnType.Text).Bold.WidthOf('M'));
        Assert.Equal(Metrics.For(ColumnType.Text).Bold, Metrics.Bold.For(ColumnType.Text));
        Assert.Equal(Metrics, Metrics.For(ColumnType.Date));
        Assert.Null(Metrics.For(ColumnType.Boolean).GlyphWidths);
    }

    [Fact] // ADR-0016: a null table takes the table away
    public void A_null_table_charges_by_class_again()
    {
        var without = Metrics.WithGlyphWidths(null, 14);

        Assert.Null(without.GlyphWidths);
        Assert.Equal(18, without.WidthOf('M'));
    }

    [Fact] // ADR-0016: a bad table fails where it was written
    public void A_bad_table_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new GlyphWidthTable(14, [("M", 13, 14), ("M", 12, 13)]));
        Assert.Throws<ArgumentException>(() => new GlyphWidthTable(14, [("Mi", 13, 14)]));
        Assert.Throws<ArgumentException>(() => new GlyphWidthTable(14, [("", 13, 14)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GlyphWidthTable(14, [("M", 0, 14)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GlyphWidthTable(14, [("M", 13, double.NaN)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GlyphWidthTable(0, [("M", 13, 14)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Metrics.WithGlyphWidths(Table, 0));
    }

    [Fact] // ADR-0016: a supplementary glyph is one glyph in a table
    public void A_supplementary_glyph_is_one_entry()
    {
        var table = new GlyphWidthTable(14, [("\U0001D400", 11, 12)]);
        var metrics = new CellTextMetrics(14, 9, 5, 16, 0, 15, 10, 6, 18, 19).WithGlyphWidths(table, 14);

        Assert.Equal(11, metrics.TextWidthPx("\U0001D400"));
        Assert.Equal(1, table.Count);
    }

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
