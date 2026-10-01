using ExGrid.Columns;

namespace ExGrid.MudBlazor;

/// <summary>
/// What this Wrapper hands down to the grids inside it (ADR-0030): Roboto's glyph
/// widths — the metrics-bearing obligation of ADR-0027, discharged by the same hand
/// that sets the font — and the presets MudBlazor's <c>Dense</c>, <c>Hover</c> and
/// <c>Striped</c> map onto. Cascaded by <see cref="MudExGridPaper"/>; exposed here so a Consumer that
/// wraps nothing can still pass the widths to a bare grid.
/// </summary>
public static class MudExGridPresentation
{
    /// <summary>
    /// Roboto, measured in Chrome 154 on 2026-10-01 from the Roboto the demo pages serve, at
    /// 14px with tabular digits, at every weight the grid paints regular text in: 400 for a
    /// cell, 500 for the header, 600 for group and total rows. Each class is declared over its
    /// widest glyph at any of the three, not at the boldest alone: Roboto is a variable face,
    /// and some glyphs narrow as the weight grows — <c>/</c> is 5.781px at 400 and 5.328 at
    /// 600 (ticket 82). A glyph's width is the wider of the glyph alone and the average of a
    /// hundred in a row: <c>//</c> kerns, so a run alone reads <c>/</c> short. The wide
    /// class's widest is <c>%</c>, 10.344px at 600
    /// (<c>#</c> 8.625 at 400). Declared a shade over each: overshooting errs toward an early
    /// <c>####</c>, the safe direction (ADR-0016). Stated at 14px; the core scales them to
    /// the resolved font size (ADR-0028).
    /// </summary>
    public const double RobotoWidePx = 10.4;

    /// <summary>
    /// Roboto's digit class: a tabular digit is 8.0px at 600, and <c>£</c>, the widest glyph
    /// the class holds, 8.281px at 600 (<c>₺</c> 8.188, <c>₫</c> 8.109, <c>¥</c> 7.484). The
    /// currency signs wider than it — <c>₼</c> <c>₽</c> <c>¤</c> <c>₱</c> <c>₩</c> <c>₦</c>
    /// <c>₪</c> — and the letters are the other class's (<see cref="RobotoOtherPx"/>).
    /// </summary>
    public const double RobotoDigitPx = 8.3;

    /// <summary>Roboto's widest separator, <c>/</c>, measured at 5.781px at 400 (<c>)</c>
    /// 4.906 at 500 and 600).</summary>
    public const double RobotoNarrowPx = 5.8;

    /// <summary>
    /// Roboto's other class (ADR-0016; ticket 83): every glyph outside the measured classes —
    /// letters, and the currency signs wider than a digit. Measured with the others from the
    /// corpus in <c>tests/GlyphWidths</c>, in this package's family stack, at 400, 500 and 600:
    /// the widest is <c>W</c>, 12.422px at 400 (<c>m</c> 12.281, <c>M</c> 12.250, <c>₪</c>
    /// 11.891 at 600, <c>ж</c> 11.719, <c>₦</c> 11.328). At 12px <c>W</c> is 10.656, which is
    /// 12.432 at this size, so the declaration covers both sizes the core scales it to.
    /// </summary>
    public const double RobotoOtherPx = 12.44;

    /// <summary>The font size the Roboto widths were measured at; the core scales
    /// them to the resolved font size (ADR-0028).</summary>
    public const double RobotoMeasuredAtPx = 14;

    /// <summary>
    /// Roboto bold (ADR-0050, item 15: a bold cell is judged by bold widths), measured with the
    /// regular widths, at weight 700: <c>%</c> 10.359px (<c>#</c> 8.297, <c>€</c> 8.047).
    /// Declared a shade over, as they are.
    /// </summary>
    public const double RobotoBoldWidePx = 10.4;

    /// <summary>Roboto bold's digit class: the tabular digit is 8.047px and <c>£</c>, the widest
    /// glyph the class holds, 8.328px. At 12px <c>£</c> is 7.141, which is 8.331 at this size
    /// (ticket 83), so the class is declared at 8.34.</summary>
    public const double RobotoBoldDigitPx = 8.34;

    /// <summary>Roboto bold's widest separator, <c>/</c>, measured at 5.203px (<c>)</c>
    /// 4.922).</summary>
    public const double RobotoBoldNarrowPx = 5.25;

    /// <summary>Roboto bold's other class (ticket 83): <c>M</c>, 12.266px at 700 (<c>W</c>
    /// 12.250, <c>₪</c> 12.219, <c>ж</c> 12.016); at 12px <c>M</c> is 10.516, which is 12.268
    /// at this size.</summary>
    public const double RobotoBoldOtherPx = 12.28;

    /// <summary>The widths alone — no density, no hover — for a bare grid.</summary>
    public static GridPresentationDefaults Roboto { get; } =
        new(RobotoWidePx, RobotoDigitPx, RobotoNarrowPx, RobotoMeasuredAtPx,
            RobotoBoldWidePx, RobotoBoldDigitPx, RobotoBoldNarrowPx, RobotoOtherPx, RobotoBoldOtherPx);

    // One instance per combination: an allocation-free lookup, and a cascaded value
    // that only changes when Dense, Hover or Striped do. Identity buys nothing beyond
    // that — a parent's render reaches the grid root whatever the cascaded reference is,
    // and the rows skip on value equality of what the root hands them (ADR-0003). A
    // theme change is CSS and reaches no render at all.
    private static readonly GridPresentationDefaults[] ByFlags = BuildFlags();

    private static GridPresentationDefaults[] BuildFlags()
    {
        var all = new GridPresentationDefaults[8];
        for (var flags = 0; flags < all.Length; flags++)
        {
            all[flags] = new(RobotoWidePx, RobotoDigitPx, RobotoNarrowPx, RobotoMeasuredAtPx,
                RobotoBoldWidePx, RobotoBoldDigitPx, RobotoBoldNarrowPx, RobotoOtherPx, RobotoBoldOtherPx,
                DensityFor(dense: (flags & 4) != 0), highlightHoverRow: (flags & 2) != 0,
                stripeRows: (flags & 1) != 0);
        }
        return all;
    }

    /// <summary>
    /// Material's density word mapped onto the grid's presets (ADR-0028/0030):
    /// <c>Dense</c> is <see cref="GridDensity.Compact"/>, not <see cref="GridDensity.Excel"/>
    /// — Material's dense row is not a spreadsheet row; a Consumer wanting Excel's says
    /// so on the grid, where it wins. Not dense is <see cref="GridDensity.Standard"/>,
    /// the 32px MudBlazor rows have without <c>Dense</c>.
    /// </summary>
    public static GridDensity DensityFor(bool dense) => dense ? GridDensity.Compact : GridDensity.Standard;

    /// <summary>The cascaded value for a paper's <c>Dense</c> and <c>Hover</c>, in Roboto.</summary>
    public static GridPresentationDefaults For(bool dense, bool hover) => For(dense, hover, striped: false);

    /// <summary>The cascaded value for a paper's <c>Dense</c>, <c>Hover</c> and
    /// <c>Striped</c>, in Roboto. <c>Striped</c> is the grid's Row Stripes (ADR-0038).</summary>
    public static GridPresentationDefaults For(bool dense, bool hover, bool striped)
        => ByFlags[(dense ? 4 : 0) + (hover ? 2 : 0) + (striped ? 1 : 0)];

    /// <summary>The cascaded value for another font: its widths, with the paper's flags.</summary>
    public static GridPresentationDefaults For(MudExGridFont font, bool dense, bool hover)
        => For(font, dense, hover, striped: false);

    /// <summary>The cascaded value for another font: its widths, with the paper's flags.</summary>
    public static GridPresentationDefaults For(MudExGridFont font, bool dense, bool hover, bool striped)
    {
        ArgumentNullException.ThrowIfNull(font);
        // A width the font does not state is the core's allowance over the one it does, the
        // safe direction (ADR-0050, item 15; ticket 83).
        var (boldWide, boldDigit, boldNarrow) =
            font.BoldWideWidthPx is { } wide && font.BoldDigitWidthPx is { } digit && font.BoldNarrowWidthPx is { } narrow
                ? (wide, digit, narrow)
                : (font.WideWidthPx * CellTextMetrics.BoldWidthAllowance,
                    font.DigitWidthPx * CellTextMetrics.BoldWidthAllowance,
                    font.NarrowWidthPx * CellTextMetrics.BoldWidthAllowance);
        return new GridPresentationDefaults(
            font.WideWidthPx, font.DigitWidthPx, font.NarrowWidthPx, font.MeasuredAtPx,
            boldWide, boldDigit, boldNarrow,
            font.OtherWidthPx ?? font.DigitWidthPx * CellTextMetrics.OtherWidthAllowance,
            font.BoldOtherWidthPx ?? boldDigit * CellTextMetrics.OtherWidthAllowance,
            DensityFor(dense), hover, striped);
    }
}

/// <summary>
/// A font other than Roboto, for a paper whose theme sets one: the CSS family and the
/// glyph widths measured for it, per class, in one value — so the font on screen and the
/// widths in the <c>####</c> arithmetic cannot come from different hands
/// (ADR-0027/0030). The paper writes <c>--ex-font-family</c> inline from
/// <see cref="Family"/> and cascades the widths in the same render. Each width is the widest
/// glyph of its class at every weight the grid paints regular text in, not at the boldest
/// alone: a variable face can narrow a glyph as it gets bolder, as Roboto does <c>/</c>.
/// </summary>
/// <param name="Family">The CSS <c>font-family</c> value the paper writes as
/// <c>--ex-font-family</c>.</param>
/// <param name="WideWidthPx">The wide class — <c>%</c>, <c>€</c>, <c>−</c>, <c>+</c> and
/// <c>#</c> — in this font (ADR-0016).</param>
/// <param name="DigitWidthPx">The digit class in this font: a tabular digit, or the widest
/// glyph charged as one where that is wider — <c>£</c> in Roboto (ADR-0016).</param>
/// <param name="NarrowWidthPx">The widest separator in this font.</param>
/// <param name="MeasuredAtPx">The font size the widths were measured at.</param>
/// <param name="BoldWideWidthPx">The wide class at the bold weight, or null (ADR-0050, item 15).
/// Without all three bold widths, a bold cell is charged the core's allowance over the regular
/// ones (<see cref="CellTextMetrics.BoldWidthAllowance"/>).</param>
/// <param name="BoldDigitWidthPx">The digit class at the bold weight, or null.</param>
/// <param name="BoldNarrowWidthPx">The widest separator at the bold weight, or null.</param>
/// <param name="OtherWidthPx">The other class in this font, or null (ADR-0016; ticket 83): the
/// widest glyph outside the classes above that the formats emit — letters, and currency signs
/// wider than a digit. Without it, a glyph of the class is charged the core's allowance over
/// the digit (<see cref="CellTextMetrics.OtherWidthAllowance"/>).</param>
/// <param name="BoldOtherWidthPx">The other class at the bold weight, or null: then the core's
/// allowance over the bold digit.</param>
public sealed record MudExGridFont(
    string Family, double WideWidthPx, double DigitWidthPx, double NarrowWidthPx, double MeasuredAtPx,
    double? BoldWideWidthPx = null, double? BoldDigitWidthPx = null, double? BoldNarrowWidthPx = null,
    double? OtherWidthPx = null, double? BoldOtherWidthPx = null)
{
    /// <summary>Roboto, as this package measured it.</summary>
    public static MudExGridFont Roboto { get; } = new(
        "Roboto, \"Helvetica Neue\", Arial, sans-serif",
        MudExGridPresentation.RobotoWidePx, MudExGridPresentation.RobotoDigitPx,
        MudExGridPresentation.RobotoNarrowPx, MudExGridPresentation.RobotoMeasuredAtPx,
        MudExGridPresentation.RobotoBoldWidePx, MudExGridPresentation.RobotoBoldDigitPx,
        MudExGridPresentation.RobotoBoldNarrowPx, MudExGridPresentation.RobotoOtherPx,
        MudExGridPresentation.RobotoBoldOtherPx);
}
