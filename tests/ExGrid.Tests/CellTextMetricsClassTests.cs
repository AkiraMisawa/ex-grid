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
    }
}
