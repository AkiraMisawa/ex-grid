using ExGrid.Cells;
using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// A bold cell is judged by bold widths (ADR-0050 item 15; ADR-0071, "Bold, and what fits"; DC-58):
/// <c>CellTextMetrics</c> carries each character class at the bold weight, the core's defaults are
/// measured as §21.7a measured the regular ones, and a bold number that fits at the regular widths
/// and not at the bold ones is <c>####</c>.
///
/// <para>Its answers are shared cases (<c>tests/cases/columns/text-metrics.json</c> and
/// <c>overflow.json</c>, <c>grid-metrics.json</c>); what stays here is C#'s printing and the cell
/// appearance's own values.</para>
/// </summary>
public class BoldWidthTests
{
    // Regular 9 / bold 10 per digit, padding 4: "12345" needs 45 + 8 = 53 regular, 50 + 8 = 58 bold.
    private static readonly CellTextMetrics Metrics = new(14, 9, 5, 14, 4, 15, 10, 6);

    [Fact] // ADR-0050 item 15: Bold is metrics of the same type, so the generated ToString recursed through it forever
    public void Metrics_print_their_widths_without_following_Bold()
    {
        var text = Metrics.ToString();

        Assert.StartsWith("CellTextMetrics {", text);
        Assert.Contains("DigitWidthPx = 9", text);
        Assert.Contains("BoldDigitWidthPx = 10", text);
        Assert.DoesNotContain("Bold = ", text);
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
