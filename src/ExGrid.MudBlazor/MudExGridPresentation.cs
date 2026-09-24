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
    /// Roboto, measured in Chrome on 2026-09-01 at 14px and the 600 weight the grid's
    /// group and total rows paint (ADR-0016's rule: the boldest weight the grid itself
    /// paints): a tabular digit is 7.998px, <c>%</c> 10.33px (<c>£</c> 8.279, <c>€</c>
    /// 7.998), and the widest separator — <c>(</c> — 4.909px. Declared a shade over
    /// each: overshooting errs toward an early <c>####</c>, the safe direction. Stated
    /// at 14px; the core scales them to the resolved font size (ADR-0028).
    /// </summary>
    public const double RobotoWidePx = 10.4;

    public const double RobotoDigitPx = 8.0;

    public const double RobotoNarrowPx = 4.95;

    public const double RobotoMeasuredAtPx = 14;

    /// <summary>The widths alone — no density, no hover — for a bare grid.</summary>
    public static GridPresentationDefaults Roboto { get; } =
        new(RobotoWidePx, RobotoDigitPx, RobotoNarrowPx, RobotoMeasuredAtPx);

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
        return new GridPresentationDefaults(
            font.WideWidthPx, font.DigitWidthPx, font.NarrowWidthPx, font.MeasuredAtPx,
            DensityFor(dense), hover, striped);
    }
}

/// <summary>
/// A font other than Roboto, for a paper whose theme sets one: the CSS family and the
/// three glyph widths measured for it, in one value — so the font on screen and the
/// widths in the <c>####</c> arithmetic cannot come from different hands
/// (ADR-0027/0030). The paper writes <c>--ex-font-family</c> inline from
/// <see cref="Family"/> and cascades the widths in the same render.
/// </summary>
public sealed record MudExGridFont(
    string Family, double WideWidthPx, double DigitWidthPx, double NarrowWidthPx, double MeasuredAtPx)
{
    /// <summary>Roboto, as this package measured it.</summary>
    public static MudExGridFont Roboto { get; } = new(
        "Roboto, \"Helvetica Neue\", Arial, sans-serif",
        MudExGridPresentation.RobotoWidePx, MudExGridPresentation.RobotoDigitPx,
        MudExGridPresentation.RobotoNarrowPx, MudExGridPresentation.RobotoMeasuredAtPx);
}
