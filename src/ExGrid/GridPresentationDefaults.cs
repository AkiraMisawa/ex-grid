using ExGrid.Columns;

namespace ExGrid;

/// <summary>
/// What a Wrapper hands down to every grid inside it (ADR-0030): the three glyph
/// widths of the font its Theme sets — stated at the size they were measured — and,
/// optionally, the Density its own density word maps onto. Cascaded, never passed:
/// the metrics-bearing obligation of ADR-0027 says whoever sets <c>--ex-font-family</c>
/// owes new Cell Metrics, and a Wrapper sets the font on an element <em>outside</em>
/// the grid, where it cannot reach the grid's parameters. Splitting the obligation
/// across two components — the font on one, the widths typed by hand on the other —
/// is how a Roboto grid ends up measured in <c>system-ui</c> widths, quietly. This
/// object keeps font and widths in the same hand.
///
/// <para>Precedence is ADR-0028's, one rung lower: an explicit parameter on the grid
/// beats this object, per value; this object beats the preset. The widths are scaled
/// to the resolved font size, so one measurement serves every preset; the padding is
/// the preset's own, because it is layout, not type.</para>
///
/// <para>A Consumer's CSS-only minimal Wrapper may cascade one just the same — the
/// type is the core's and knows no design system.</para>
/// </summary>
public sealed record GridPresentationDefaults
{
    public GridPresentationDefaults(
        double wideWidthPx, double digitWidthPx, double narrowWidthPx, double fontSizePx,
        GridDensity? density = null, bool? highlightHoverRow = null)
    {
        if (!double.IsFinite(fontSizePx) || fontSizePx <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fontSizePx), fontSizePx,
                "The size the widths were measured at is a finite, positive number of pixels.");
        }
        // The same rules the metrics themselves enforce, checked at construction so a
        // bad measurement fails where it was written rather than in a grid far away.
        _ = new CellTextMetrics(wideWidthPx, digitWidthPx, narrowWidthPx, 0);

        WideWidthPx = wideWidthPx;
        DigitWidthPx = digitWidthPx;
        NarrowWidthPx = narrowWidthPx;
        FontSizePx = fontSizePx;
        Density = density;
        HighlightHoverRow = highlightHoverRow;
    }

    /// <summary>What <c>%</c> costs — the widest glyph of the wide class (ADR-0016).</summary>
    public double WideWidthPx { get; }

    /// <summary>A tabular digit.</summary>
    public double DigitWidthPx { get; }

    /// <summary>The widest separator.</summary>
    public double NarrowWidthPx { get; }

    /// <summary>The font size the three widths are true at.</summary>
    public double FontSizePx { get; }

    /// <summary>The preset a Wrapper's density word maps onto, or null to leave the
    /// grid's own default (Compact) in charge.</summary>
    public GridDensity? Density { get; }

    /// <summary>The hover band's switch, where a design system's own word for it —
    /// <c>Hover</c> on every Material table — is set on the Wrapper's element rather
    /// than on the grid. Null leaves the grid's default (off); an explicit parameter
    /// on the grid still wins (ADR-0029: the band's contract is its token and the
    /// parameter that turns it on).</summary>
    public bool? HighlightHoverRow { get; }

    /// <summary>
    /// The metrics at the size the grid resolved — the widths scaled linearly from
    /// <see cref="FontSizePx"/>, which is how the Excel preset's 12px trio was derived
    /// from the 14px measurement (ADR-0028) — with the preset's own padding.
    /// </summary>
    public CellTextMetrics CellMetricsAt(double fontSizePx, double cellHorizontalPaddingPx)
    {
        if (!double.IsFinite(fontSizePx) || fontSizePx <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fontSizePx), fontSizePx,
                "A font size is a finite, positive number of pixels.");
        }
        var scale = fontSizePx / FontSizePx;
        return new CellTextMetrics(
            WideWidthPx * scale, DigitWidthPx * scale, NarrowWidthPx * scale, cellHorizontalPaddingPx);
    }
}
