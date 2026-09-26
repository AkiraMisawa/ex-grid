using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

/// <summary>The per-character-class estimate (ADR-0016): one number cannot serve a
/// twelve-digit amount column and a percent column at once.</summary>
public class CellTextMetricsClassTests
{
    private static readonly CellTextMetrics Metrics = new(
        wideWidthPx: 14, digitWidthPx: 9, narrowWidthPx: 5, cellHorizontalPaddingPx: 8);

    [Fact] // ADR-0016: % and € charge wide; separators charge narrow; the rest a digit
    public void Characters_charge_by_class()
    {
        Assert.Equal(14, Metrics.WidthOf('%'));
        Assert.Equal(14, Metrics.WidthOf('€'));
        Assert.Equal(5, Metrics.WidthOf('.'));
        Assert.Equal(5, Metrics.WidthOf(','));
        Assert.Equal(9, Metrics.WidthOf('7'));
        Assert.Equal(9, Metrics.WidthOf('$'));
        Assert.Equal(9, Metrics.WidthOf('-'));
    }

    [Fact] // ADR-0016: the estimate is the text charged per class, plus padding both sides
    public void The_estimate_sums_per_class()
    {
        // "12.5%" = 9 + 9 + 5 + 9 + 14 = 46, plus 16 padding.
        Assert.Equal(62, Metrics.EstimatePx("12.5%"));
        Assert.Equal(46, Metrics.TextWidthPx("12.5%"));
    }

    [Fact] // ADR-0016: the two-number form charges everything at the digit — the uniform contract
    public void The_uniform_form_charges_everything_at_the_digit()
    {
        var uniform = new CellTextMetrics(digitWidthPx: 9, cellHorizontalPaddingPx: 8);

        Assert.Equal(9, uniform.WidthOf('%'));
        Assert.Equal(9, uniform.WidthOf('.'));
        Assert.Equal(uniform.EstimatePx(5), uniform.EstimatePx("12.5%"));
    }

    [Fact] // ADR-0016: a percent value hashes when charged honestly and would not have before
    public void A_percent_column_hashes_where_the_flat_charge_lied()
    {
        // Width 60: content 44. "12.5%" honestly needs 46 — hash. The flat charge said
        // 45 at 9px per glyph... also hashes; make the honest case decisive: "1.5%" is
        // 9+5+9+14 = 37 vs flat 36. At content width 36.5 the flat estimate fits and
        // the honest one refuses.
        var flat = new CellTextMetrics(digitWidthPx: 9, cellHorizontalPaddingPx: 0);
        var honest = new CellTextMetrics(14, 9, 5, 0);

        Assert.False(OverflowRules.Decide(ColumnType.Number, "1.5%", 36.5, flat).IsHashed);
        Assert.True(OverflowRules.Decide(ColumnType.Number, "1.5%", 36.5, honest).IsHashed);
    }

    [Fact] // The class widths keep their ordering, or the classes stop meaning anything
    public void The_ordering_wide_digit_narrow_is_enforced()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CellTextMetrics(8, 9, 5, 8));   // wide < digit
        Assert.Throws<ArgumentOutOfRangeException>(() => new CellTextMetrics(14, 9, 10, 8)); // narrow > digit
        Assert.Throws<ArgumentOutOfRangeException>(() => new CellTextMetrics(14, 9, 5, 8.5, 8)); // full-width < digit
        Assert.Throws<ArgumentOutOfRangeException>(() => new CellTextMetrics(14, 9, 5, double.NaN, 8));
    }

    [Fact] // ADR-0016 (2026-09-25): − + # measured 11.731px on DejaVu Sans, so they charge wide
    public void Signs_and_the_hash_charge_wide()
    {
        Assert.Equal(14, Metrics.WidthOf('\u2212')); // − minus sign
        Assert.Equal(14, Metrics.WidthOf('+'));
        Assert.Equal(14, Metrics.WidthOf('#'));
        // The hyphen-minus was measured at 5.811px and stays a digit.
        Assert.Equal(9, Metrics.WidthOf('-'));
    }

    private static readonly CellTextMetrics WithFullWidth = new(
        wideWidthPx: 14, digitWidthPx: 9, narrowWidthPx: 5, fullWidthPx: 16, cellHorizontalPaddingPx: 8);

    [Theory] // ADR-0016 (2026-09-25): East Asian Width W or F is a fourth class, charged at 1em
    [InlineData('評')]
    [InlineData('あ')]
    [InlineData('ア')]
    [InlineData('한')]
    [InlineData('\u3000')] // ideographic space
    [InlineData('\uFF11')] // fullwidth digit one
    [InlineData('\uFF08')] // fullwidth left parenthesis
    [InlineData('\u3002')] // ideographic full stop
    public void Full_width_characters_charge_full_width(char c)
    {
        Assert.Equal(16, WithFullWidth.WidthOf(c));
    }

    [Theory] // ADR-0016: what is not full-width keeps its class
    [InlineData('A', 9)]
    [InlineData('ｱ', 9)] // halfwidth katakana is H, not W
    [InlineData('7', 9)]
    [InlineData('%', 14)]
    [InlineData('.', 5)]
    public void Other_characters_keep_their_class(char c, double expected)
    {
        Assert.Equal(expected, WithFullWidth.WidthOf(c));
    }

    [Fact] // ADR-0016: a supplementary ideograph is one glyph, charged once — not once per surrogate
    public void A_supplementary_ideograph_is_charged_once()
    {
        Assert.Equal(16, WithFullWidth.TextWidthPx("\U00020BB7")); // 𠮷
        Assert.Equal(16 + 9, WithFullWidth.TextWidthPx("\U00020BB7" + "1"));
    }

    [Fact] // ADR-0016: "評価額" is 42px at 14px in every family measured; charged as digits it was 27
    public void A_japanese_header_is_charged_at_one_em_per_character()
    {
        var at14 = new CellTextMetrics(14.028, 9.742, 6.398, 14, 0);

        Assert.Equal(42, at14.TextWidthPx("評価額"));
    }

    [Fact] // ADR-0016: a Japanese date format hashes where charging it as digits said it fit
    public void A_japanese_date_hashes_where_the_digit_charge_lied()
    {
        // "2026年9月25日": seven digits and three ideographs. As digits: 10 x 9 = 90.
        // Honestly: 7 x 9 + 3 x 16 = 111. At a 100px column the old charge fitted it and
        // the stylesheet would have cut a date; the honest one refuses.
        var noPadding = new CellTextMetrics(14, 9, 5, 16, 0);

        Assert.True(OverflowRules.Decide(ColumnType.Date, "2026年9月25日", 100, noPadding).IsHashed);
        Assert.False(OverflowRules.Decide(ColumnType.Date, "2026年9月25日", 111, noPadding).IsHashed);
    }

    [Fact] // ADR-0016: the four-width form charges full-width at twice the digit — at least an em
    public void The_four_width_form_charges_full_width_at_twice_the_digit()
    {
        Assert.Equal(18, Metrics.FullWidthPx);
        Assert.Equal(18, Metrics.WidthOf('評'));
    }

    [Fact] // ADR-0016: the uniform form stays uniform — its digit is promised to cover every glyph
    public void The_uniform_form_charges_full_width_at_the_digit()
    {
        var uniform = new CellTextMetrics(digitWidthPx: 9, cellHorizontalPaddingPx: 8);

        Assert.Equal(9, uniform.WidthOf('評'));
    }
}
