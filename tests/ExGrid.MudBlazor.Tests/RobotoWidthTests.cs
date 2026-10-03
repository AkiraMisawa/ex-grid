using ExGrid.Columns;
using Xunit;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// Roboto's character classes cover the widest glyph each holds (ADR-0016, "The estimate charges
/// per character class"; ADR-0030: the widths are the Wrapper's metrics-bearing obligation), so a
/// number they judge to fit is never cut. The numbers are ticket 82's measurement: Chrome 154,
/// 14px, tabular digits, the Roboto the demo pages serve, each glyph the wider of itself alone and
/// a hundred in a row. Regular is the widest at 400, 500 and 600, the weights the grid paints
/// regular text in; bold is 700.
/// </summary>
public class RobotoWidthTests
{
    // The widths at the size they were measured at, with Standard's 8px padding.
    private static readonly CellTextMetrics Roboto = MudExGridPresentation.Roboto.CellMetricsAt(14, 8);

    [Fact] // ADR-0016 / ADR-0030: a £ amount that fits only with £ charged as an 8.0px digit is ####, not cut
    public void A_pound_amount_that_fits_only_at_the_old_digit_width_is_hashed()
    {
        const string amount = "£1,234,567.50";
        // The old widths charged £ as a digit, at 8.0px. This column holds the amount exactly at
        // them, and is 0.281px short of it with £ at its measured 8.281px.
        var old = new CellTextMetrics(10.4, 8.0, 4.95, 8);
        var width = old.EstimatePx(amount);
        var withPoundAsMeasured = width - 8.0 + 8.281;
        Assert.False(OverflowRules.Decide(ColumnType.Number, amount, width, old).IsHashed);

        Assert.True(Roboto.EstimatePx(amount) >= withPoundAsMeasured);
        Assert.True(OverflowRules.Decide(ColumnType.Number, amount, width, Roboto).IsHashed);
        Assert.True(OverflowRules.Decide(ColumnType.Number, amount, width, Roboto.Bold).IsHashed);
    }

    [Theory] // ADR-0016 / ADR-0030: each regular class is at least its widest glyph at 400, 500 and 600
    [InlineData('0', 8.000)] // every digit, and $, at 600
    [InlineData('£', 8.281)] // 600
    [InlineData('₺', 8.188)] // 600
    [InlineData('₫', 8.109)] // 400
    [InlineData('¥', 7.484)] // 600
    [InlineData('-', 5.141)] // 600
    [InlineData('%', 10.344)] // 600
    [InlineData('€', 8.000)] // 600
    [InlineData('#', 8.625)] // 400: narrower as it gets bolder
    [InlineData('−', 8.016)] // 400
    [InlineData('+', 7.953)] // 400
    [InlineData('/', 5.781)] // 400: 5.328 at 600
    [InlineData(')', 4.906)] // 500
    [InlineData('(', 4.875)] // 600
    [InlineData('.', 3.984)] // 600
    [InlineData(',', 3.297)] // 600
    [InlineData(':', 3.828)] // 600
    [InlineData(' ', 3.484)]
    public void Each_regular_class_covers_its_widest_glyph(char glyph, double measuredPx)
        => Assert.True(Roboto.WidthOf(glyph) >= measuredPx, $"'{glyph}' is {measuredPx}px and is charged {Roboto.WidthOf(glyph)}px");

    [Theory] // ADR-0016 / ADR-0030 / ADR-0071: each bold class is at least its widest glyph at 700
    [InlineData('0', 8.047)]
    [InlineData('£', 8.328)]
    [InlineData('₺', 8.297)]
    [InlineData('₫', 8.094)]
    [InlineData('¥', 7.516)]
    [InlineData('-', 5.516)]
    [InlineData('%', 10.359)]
    [InlineData('€', 8.047)]
    [InlineData('#', 8.297)]
    [InlineData('−', 7.781)]
    [InlineData('+', 7.641)]
    [InlineData('/', 5.203)]
    [InlineData(')', 4.922)]
    [InlineData('(', 4.906)]
    [InlineData('.', 4.063)]
    [InlineData(',', 3.453)]
    [InlineData(':', 3.953)]
    [InlineData(' ', 3.484)]
    public void Each_bold_class_covers_its_widest_glyph(char glyph, double measuredPx)
        => Assert.True(Roboto.Bold.WidthOf(glyph) >= measuredPx, $"'{glyph}' is {measuredPx}px and is charged {Roboto.Bold.WidthOf(glyph)}px");

    [Theory] // ADR-0016 / ADR-0030: text Chrome painted wider than the old widths' estimate is charged at least what it paints
    [InlineData("£0", 16.281)] // 600; the old widths charged 16.0
    [InlineData("₺0", 16.172)] // 600; 16.0
    [InlineData("1/1/2026", 58.766)] // 400; 57.9
    [InlineData("9/30/2026", 66.641)] // 400; 65.9
    [InlineData("12/31/2026", 74.641)] // 600; 73.9
    public void Text_the_old_widths_cut_is_charged_at_least_its_painted_width(string text, double paintedPx)
        => Assert.True(Roboto.TextWidthPx(text) >= paintedPx, $"\"{text}\" paints {paintedPx}px and is charged {Roboto.TextWidthPx(text)}px");
}
