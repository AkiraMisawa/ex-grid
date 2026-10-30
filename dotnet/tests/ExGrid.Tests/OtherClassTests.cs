using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The other class (ADR-0016's note of 2026-10-01, "No glyph a Number Format can emit is charged
/// below its width"; ticket 83): every glyph outside the four measured classes — letters, the
/// currency signs wider than a digit, anything not foreseen — is charged the widest of them, so a
/// glyph nobody listed errs toward <c>####</c>. A value that never hashes is charged it at the digit.
///
/// <para>Its answers are shared cases (<c>tests/cases/columns/text-metrics.json</c>); what stays here is
/// a lone UTF-16 half, and the time a default face paints wider than its digit charge.</para>
/// </summary>
public class OtherClassTests
{
    // Wide 14, digit 9, narrow 5, full-width 16, no padding; bold 15 / 10 / 6; other 13, bold other 14.
    private static readonly CellTextMetrics Metrics = new(14, 9, 5, 16, 0, 15, 10, 6, 13, 14);

    [Fact] // ADR-0016 / ticket 83: a lone surrogate is charged as the other class
    public void A_lone_surrogate_charges_the_other_class()
    {
        Assert.Equal(13, Metrics.WidthOf('\uD835'));
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

}
