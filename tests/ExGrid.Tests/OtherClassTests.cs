using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The other class (ADR-0016's note of 2026-10-01, "No glyph a Number Format can emit is charged
/// below its width"; ticket 83): every glyph outside the four measured classes — letters, the
/// currency signs wider than a digit, anything not foreseen — is charged the widest of them, so a
/// glyph nobody listed errs toward <c>####</c>. A value that never hashes is charged it at the digit.
/// </summary>
public class OtherClassTests
{
    // Wide 14, digit 9, narrow 5, full-width 16, no padding; bold 15 / 10 / 6; other 13, bold other 14.
    private static readonly CellTextMetrics Metrics = new(14, 9, 5, 16, 0, 15, 10, 6, 13, 14);

    [Theory] // ADR-0016, principle 1: letters and the currency signs wider than a digit charge the other class
    [InlineData("M")]
    [InlineData("m")]
    [InlineData("W")]
    [InlineData("A")] // AM, PM
    [InlineData("ж")] // Cyrillic, in ru-RU's and uk-UA's months
    [InlineData("מ")] // Hebrew, painted by a fallback face
    [InlineData("พ")] // Thai, painted by a fallback face
    [InlineData("₩")]
    [InlineData("₪")]
    [InlineData("₦")]
    [InlineData("₱")]
    [InlineData("₽")]
    [InlineData("₼")]
    [InlineData("₴")]
    [InlineData("¤")]
    [InlineData("฿")]
    [InlineData("‰")] // no built-in format emits it, and it is charged the other class all the same
    [InlineData("\U0001D400")] // nothing foreseen: a mathematical bold capital
    public void A_glyph_outside_the_measured_classes_charges_the_other_class(string glyph)
    {
        Assert.Equal(13, Metrics.TextWidthPx(glyph));
        Assert.Equal(14, Metrics.Bold.TextWidthPx(glyph));
    }

    [Theory] // ADR-0016 / ticket 83: the glyphs measured at a digit's width or under in every face stay digits
    [InlineData('7')]
    [InlineData('$')]
    [InlineData('£')]
    [InlineData('¥')]
    [InlineData('₹')]
    [InlineData('₺')]
    [InlineData('₫')]
    [InlineData('E')]        // the exponent: 1.23E+05 is an ordinary number
    [InlineData('-')]
    [InlineData('\'')]
    [InlineData('’')]  // de-CH's group separator
    [InlineData(' ')]  // a group separator
    [InlineData(' ')]  // fr-FR's group separator
    [InlineData('‎')]  // he-IL's left-to-right mark before a sign
    public void The_glyphs_measured_at_a_digit_stay_in_the_digit_class(char glyph)
    {
        Assert.Equal(9, Metrics.WidthOf(glyph));
        Assert.Equal(10, Metrics.Bold.WidthOf(glyph));
    }

    [Fact] // ADR-0016 / ticket 83: a lone surrogate is charged as the other class, a pair as its one character
    public void A_lone_surrogate_charges_the_other_class()
    {
        Assert.Equal(13, Metrics.WidthOf('\uD835'));
        Assert.Equal(13, Metrics.TextWidthPx("\U0001D400"));
    }

    [Fact] // ADR-0016, principle 1: DejaVu Sans Bold paints "11:11 AM" at 74.219px; charged as digits it fitted in 71.25
    public void A_time_with_am_is_hashed_where_the_digit_charge_cut_it()
    {
        var core = GridMetrics.Resolve(GridDensity.Compact).CellMetrics;
        const string time = "11:11 AM";
        const double painted = 74.2188; // tests/GlyphWidths/dejavu-sans.macos.json, 14px, weight 600
        var lettersAsDigits = core.For(ColumnType.Text);

        Assert.True(lettersAsDigits.TextWidthPx(time) < painted);
        Assert.True(core.TextWidthPx(time) >= painted);
        var width = lettersAsDigits.EstimatePx(time);
        Assert.True(OverflowRules.Decide(ColumnType.Date, time, width, core).IsHashed);
        Assert.False(OverflowRules.Decide(ColumnType.Date, time, core.EstimatePx(time), core).IsHashed);
    }

    [Theory] // ADR-0016: a value that can become #### is charged the other class; one that cannot, the digit
    [InlineData(ColumnType.Number, 13)]
    [InlineData(ColumnType.Date, 13)]
    [InlineData(ColumnType.Text, 9)]
    [InlineData(ColumnType.Boolean, 9)]
    public void The_other_class_is_charged_only_where_a_value_can_hash(ColumnType type, double charged)
    {
        var metrics = Metrics.For(type);

        Assert.Equal(charged, metrics.WidthOf('M'));
        // Every other class is the same either way.
        Assert.Equal(14, metrics.WidthOf('%'));
        Assert.Equal(9, metrics.WidthOf('7'));
        Assert.Equal(5, metrics.WidthOf('.'));
        Assert.Equal(16, metrics.WidthOf('評'));
    }

    [Fact] // ADR-0016 / ADR-0050 item 15: text and bold compose in either order
    public void Text_and_bold_compose_either_way_round()
    {
        Assert.Equal(Metrics.For(ColumnType.Text).Bold, Metrics.Bold.For(ColumnType.Text));
        Assert.Equal(10, Metrics.For(ColumnType.Text).Bold.WidthOf('M'));
        Assert.Equal(Metrics, Metrics.For(ColumnType.Number));
    }

    [Fact] // ADR-0016 / ticket 83: metrics built without the other class charge it twice the digit, erring early
    public void Metrics_without_other_widths_charge_it_by_the_allowance()
    {
        var threeClass = new CellTextMetrics(14, 9, 5, 4);
        var fourClass = new CellTextMetrics(14, 9, 5, 14, 4);
        var withBold = new CellTextMetrics(14, 9, 5, 14, 4, 15, 10, 6);

        Assert.Equal(2, CellTextMetrics.OtherWidthAllowance);
        Assert.Equal(18, threeClass.OtherWidthPx);
        Assert.Equal(18, fourClass.OtherWidthPx);
        Assert.Equal(18, withBold.OtherWidthPx);
        Assert.Equal(20, withBold.BoldOtherWidthPx);
        // The widest glyph measured outside the classes, W and ₩ in DejaVu Sans Bold, against its digit.
        Assert.True(9.75 * CellTextMetrics.OtherWidthAllowance >= 15.453);
    }

    [Fact] // ADR-0016: the uniform form charges every character, the other class included, at the digit
    public void The_uniform_form_charges_the_other_class_at_the_digit()
    {
        var uniform = new CellTextMetrics(digitWidthPx: 9, cellHorizontalPaddingPx: 4);

        Assert.Equal(9, uniform.WidthOf('M'));
        Assert.Equal(uniform.BoldDigitWidthPx, uniform.Bold.WidthOf('M'));
    }

    [Fact] // ADR-0050 item 15 / ticket 83: replacing the bold widths re-derives the bold other class from the new bold digit
    public void With_bold_widths_derives_the_bold_other_class_again()
    {
        var replaced = Metrics.WithBoldWidths(16, 11, 7);

        Assert.Equal(13, replaced.OtherWidthPx);
        Assert.Equal(22, replaced.BoldOtherWidthPx);
    }

    [Fact] // ADR-0016: an other width narrower than its digit is refused, as the wide one is
    public void An_other_width_below_its_digit_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CellTextMetrics(14, 9, 5, 14, 4, 15, 10, 6, 8, 14));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CellTextMetrics(14, 9, 5, 14, 4, 15, 10, 6, 13, 9));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CellTextMetrics(14, 9, 5, 14, 4, 15, 10, 6, double.NaN, 14));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CellTextMetrics(14, 9, 5, 14, 4, 15, 10, 6, 13, double.PositiveInfinity));
    }

    [Fact] // ADR-0016 (2026-09-25): a header is charged as text — its em of slack bounds the letters
    public void A_header_and_the_name_box_are_charged_as_text()
    {
        var metrics = GridMetrics.Resolve(GridDensity.Compact);
        var asText = metrics.CellMetrics.For(ColumnType.Text);

        Assert.Equal(asText.EstimatePx("Amount") + 14, metrics.HeaderRequiredPx("Amount", menuButton: false, sortable: false));
        Assert.Equal(asText.EstimatePx("XFD1048576"), metrics.NameBoxWidthPx);
    }
}
