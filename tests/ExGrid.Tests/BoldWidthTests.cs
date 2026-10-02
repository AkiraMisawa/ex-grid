using ExGrid.Cells;
using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// A bold cell is judged by bold widths (ADR-0050 item 15; ADR-0071, "Bold, and what fits"; DC-58):
/// <c>CellTextMetrics</c> carries each character class at the bold weight, the core's defaults are
/// measured as §21.7a measured the regular ones, and a bold number that fits at the regular widths
/// and not at the bold ones is <c>####</c>.
/// </summary>
public class BoldWidthTests
{
    // Regular 9 / bold 10 per digit, padding 4: "12345" needs 45 + 8 = 53 regular, 50 + 8 = 58 bold.
    private static readonly CellTextMetrics Metrics = new(14, 9, 5, 14, 4, 15, 10, 6);

    [Fact] // ADR-0050 item 15: the bold metrics put the bold widths in the regular ones' place
    public void Bold_metrics_charge_the_bold_widths()
    {
        var bold = Metrics.Bold;

        Assert.Equal(15, bold.WidthOf('%'));
        Assert.Equal(10, bold.WidthOf('7'));
        Assert.Equal(6, bold.WidthOf('.'));
        Assert.Equal(14, bold.WidthOf('評'));
        Assert.Equal(Metrics.CellHorizontalPaddingPx, bold.CellHorizontalPaddingPx);
        Assert.Equal(bold, bold.Bold);
    }

    [Fact] // DC-58 / ADR-0016: a bold number that fits at the regular widths and not at the bold ones is ####
    public void A_bold_number_that_fits_only_at_the_regular_widths_is_hashed()
    {
        Assert.False(OverflowRules.Decide(ColumnType.Number, "12345", 55, Metrics).IsHashed);
        Assert.True(OverflowRules.Decide(ColumnType.Number, "12345", 55, Metrics.Bold).IsHashed);
        Assert.False(OverflowRules.Decide(ColumnType.Number, "12345", 58, Metrics.Bold).IsHashed);
    }

    [Fact] // ADR-0050 item 15: metrics built without bold widths derive them, erring early
    public void Metrics_without_bold_widths_derive_them_by_the_allowance()
    {
        var regular = new CellTextMetrics(14, 9, 5, 14, 4);

        Assert.Equal(14 * CellTextMetrics.BoldWidthAllowance, regular.BoldWideWidthPx, 9);
        Assert.Equal(9 * CellTextMetrics.BoldWidthAllowance, regular.BoldDigitWidthPx, 9);
        Assert.Equal(5 * CellTextMetrics.BoldWidthAllowance, regular.BoldNarrowWidthPx, 9);
        // The widest growth measured from weight 600 to bold, `(` in system-ui on macOS.
        Assert.True(5.633 * CellTextMetrics.BoldWidthAllowance >= 5.852);
    }

    [Fact] // ADR-0050 item 15: the uniform form's bold metrics stay valid although its em is the digit
    public void The_uniform_forms_bold_metrics_are_valid()
    {
        var uniform = new CellTextMetrics(digitWidthPx: 9, cellHorizontalPaddingPx: 4);

        var bold = uniform.Bold;

        Assert.Equal(9 * CellTextMetrics.BoldWidthAllowance, bold.DigitWidthPx, 9);
        Assert.True(bold.FullWidthPx >= bold.DigitWidthPx);
    }

    [Fact] // ADR-0050 item 15: bold widths are refused by the rules the regular ones are
    public void Bold_widths_keep_the_class_ordering()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CellTextMetrics(14, 9, 5, 14, 4, 9, 10, 6));   // wide < digit
        Assert.Throws<ArgumentOutOfRangeException>(() => new CellTextMetrics(14, 9, 5, 14, 4, 15, 10, 11));  // narrow > digit
        Assert.Throws<ArgumentOutOfRangeException>(() => new CellTextMetrics(14, 9, 5, 14, 4, 15, 0, 0));    // non-positive
        Assert.Throws<ArgumentOutOfRangeException>(() => new CellTextMetrics(14, 9, 5, 14, 4, 15, double.NaN, 6));
    }

    [Fact] // ADR-0050 item 15: replacing the bold widths keeps every regular one
    public void With_bold_widths_replaces_only_the_bold_ones()
    {
        var replaced = Metrics.WithBoldWidths(16, 11, 7);

        Assert.Equal(Metrics.WideWidthPx, replaced.WideWidthPx);
        Assert.Equal(Metrics.DigitWidthPx, replaced.DigitWidthPx);
        Assert.Equal(16, replaced.BoldWideWidthPx);
        Assert.Equal(11, replaced.BoldDigitWidthPx);
        Assert.Equal(7, replaced.BoldNarrowWidthPx);
    }

    [Theory] // ADR-0050 item 15 / §21.7a; ticket 83: the defaults are the widest measured at weight 700 on any platform
    [InlineData(GridDensity.Compact, 14.36, 9.75, 6.41, 15.46)]
    [InlineData(GridDensity.Standard, 14.36, 9.75, 6.41, 15.46)]
    [InlineData(GridDensity.Comfortable, 14.36, 9.75, 6.41, 15.46)]
    [InlineData(GridDensity.Excel, 12.44, 8.36, 5.49, 13.26)]
    public void The_default_bold_widths_cover_the_widest_platform_measured(
        GridDensity density, double wide, double digit, double narrow, double other)
    {
        var metrics = GridMetrics.Resolve(density).CellMetrics;

        Assert.Equal(wide, metrics.BoldWideWidthPx);
        Assert.Equal(digit, metrics.BoldDigitWidthPx);
        Assert.Equal(narrow, metrics.BoldNarrowWidthPx);
        Assert.Equal(other, metrics.BoldOtherWidthPx);
    }

    [Fact] // §21.7a, measured 2026-10-01 (ticket 83, tests/GlyphWidths): system-ui on macOS paints a bold % at 14.359px
    public void The_default_bold_widths_cover_system_ui_on_macos_and_dejavu_sans_bold()
    {
        var metrics = GridMetrics.Resolve(GridDensity.Compact).CellMetrics;

        Assert.True(metrics.BoldWideWidthPx >= 14.359);     // `%`, system-ui on macOS
        Assert.True(metrics.BoldDigitWidthPx >= 9.75);      // a digit alone, DejaVu Sans Bold
        Assert.True(metrics.BoldNarrowWidthPx >= 6.406);    // `(`, DejaVu Sans Bold
        Assert.True(metrics.BoldOtherWidthPx >= 15.453);    // `W` and `₩`, DejaVu Sans Bold
        Assert.True(metrics.Bold.TextWidthPx("123,456,789,012.50") >= 157.66);
    }

    [Fact] // ADR-0030 / ADR-0050 item 15: a Wrapper's bold widths are scaled to the preset's size as its regular ones are
    public void A_wrappers_bold_widths_follow_the_resolved_font_size()
    {
        var defaults = new GridPresentationDefaults(10.4, 8, 4.95, 14, 10.4, 8.33, 4.95);

        var metrics = GridMetrics.Resolve(GridDensity.Excel, defaults: defaults).CellMetrics;

        Assert.Equal(8.33 * 12 / 14, metrics.BoldDigitWidthPx, 9);
        Assert.Equal(10.4 * 12 / 14, metrics.BoldWideWidthPx, 9);
        Assert.Equal(4.95 * 12 / 14, metrics.BoldNarrowWidthPx, 9);
    }

    [Fact] // ADR-0030: a Wrapper that states no bold widths gets the allowance over its regular ones
    public void A_wrapper_without_bold_widths_gets_the_allowance()
    {
        var defaults = new GridPresentationDefaults(10.4, 8, 4.95, 14);

        Assert.Equal(8 * CellTextMetrics.BoldWidthAllowance, defaults.BoldDigitWidthPx, 9);
    }

    [Fact] // ADR-0050 item 15: a Border with no line keeps no colour, and a colour is six hex digits
    public void A_border_and_a_colour_say_what_they_paint()
    {
        Assert.Equal(Border.None, new Border(BorderStyle.None, RgbColour.FromRgb(0xFF0000)));
        Assert.True(new Border(BorderStyle.None).IsNone);
        Assert.Equal("#ff0000", RgbColour.FromRgb(0xFF0000).ToString());
        Assert.Equal(RgbColour.FromRgb(0x123456), RgbColour.FromRgb(0x12, 0x34, 0x56));
        Assert.Equal(RgbColour.Black, default);
        Assert.Throws<ArgumentOutOfRangeException>(() => RgbColour.FromRgb(0x1000000));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Border((BorderStyle)99));
        Assert.Equal(CellAppearance.None, default);
    }
}
