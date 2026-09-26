using ExGrid.Columns;

namespace ExGrid;

/// <summary>
/// The single resolved geometry of an instance (ADR-0028): every number the
/// virtualisation arithmetic, the overlays, the editor placement or the width
/// estimates read, resolved once — and the Geometry Tokens the DOM lays out with are
/// emitted from the same object, so arithmetic and paint cannot disagree.
///
/// <para>Precedence is per value, not all-or-nothing: an explicit parameter beats the
/// <see cref="GridDensity"/> preset, which beats the core default (Compact). The
/// preset numbers are declared, not measured — recorded so the first measurement knows
/// what it is changing.</para>
/// </summary>
public sealed record GridMetrics
{
    private GridMetrics(
        double rowHeightPx, double headerHeightPx, double fontSizePx,
        CellTextMetrics cellMetrics,
        double actionPaddingXPx, double actionBorderPx, double actionGapPx,
        double menuButtonWidthPx, double menuButtonInsetPx, double sortMarkWidthPx)
    {
        RowHeightPx = rowHeightPx;
        HeaderHeightPx = headerHeightPx;
        FontSizePx = fontSizePx;
        CellMetrics = cellMetrics;
        ActionPaddingXPx = actionPaddingXPx;
        ActionBorderPx = actionBorderPx;
        ActionGapPx = actionGapPx;
        MenuButtonWidthPx = menuButtonWidthPx;
        MenuButtonInsetPx = menuButtonInsetPx;
        SortMarkWidthPx = sortMarkWidthPx;
    }

    /// <summary>The fixed height of every row (ADR-0013) — what the virtualisation
    /// arithmetic, the overlays and the editor box read.</summary>
    public double RowHeightPx { get; }

    /// <summary>No longer hard-wired to the row height (ADR-0028): the sticky header's
    /// cancellation holds for any header height, and a Dense body under a comfortable
    /// header is an ordinary design-system request. One tier's height under Header
    /// Groups (ADR-0032).</summary>
    public double HeaderHeightPx { get; }

    /// <summary>States the size <see cref="CellMetrics"/>' digit width is true at.</summary>
    public double FontSizePx { get; }

    /// <summary>The measurement pair, as the pure width/overflow layer of ADR-0016
    /// reads it — <c>CellTextMetrics</c> is a projection of this object.</summary>
    public CellTextMetrics CellMetrics { get; }

    /// <summary>One action button's own horizontal padding, per side (ADR-0020).</summary>
    public double ActionPaddingXPx { get; }

    /// <summary>One action button's border, per side.</summary>
    public double ActionBorderPx { get; }

    /// <summary>The gap between neighbouring action buttons.</summary>
    public double ActionGapPx { get; }

    /// <summary>A cell's horizontal padding on one side, read from
    /// <see cref="CellMetrics"/>; a cell pays it twice.</summary>
    public double CellPaddingXPx => CellMetrics.CellHorizontalPaddingPx;

    /// <summary>The width of one digit, read from <see cref="CellMetrics"/> — the unit
    /// <c>####</c> is counted in (ADR-0016).</summary>
    public double DigitWidthPx => CellMetrics.DigitWidthPx;

    /// <summary>One action button's box beside its text: padding both sides, border
    /// both sides, and the gap to the next button — what an Auto Action Column's width
    /// is measured from (ADR-0016/0020).</summary>
    public double ActionButtonChromePx => (ActionPaddingXPx * 2) + (ActionBorderPx * 2) + ActionGapPx;

    /// <summary>The column-menu button's own width in the header cell (ADR-0010).</summary>
    public double MenuButtonWidthPx { get; }

    /// <summary>The gap between the menu button and the header cell's right edge.</summary>
    public double MenuButtonInsetPx { get; }

    /// <summary>What the menu button takes off the header text's room — width plus the
    /// inset — which an Auto column's header estimate must include, or a short header
    /// stands with its label under the ▾ (ADR-0016/0028).</summary>
    public double MenuButtonBandPx => MenuButtonWidthPx + MenuButtonInsetPx;

    /// <summary>The box the sort mark (▲ / ▼) stands in at the end of a sorted header —
    /// an em for the glyph and a gap before it. Emitted as a Geometry Token the stylesheet
    /// sizes the mark with, so the header estimate and the paint read one number, as the
    /// menu button's do (ADR-0016/0029).</summary>
    public double SortMarkWidthPx { get; }

    /// <summary>
    /// The full cell width a header needs (ADR-0016, 2026-09-25): its label, one
    /// full-width em of slack, the menu button's band when there is one, and the sort
    /// mark's box when the column can be sorted — whether or not it is sorted now,
    /// so a header click never chops its label nor moves the columns to its right. The
    /// slack is there because a label is proportional letters no per-class charge can
    /// bound (<c>W</c> is 15.44px where <c>i</c> is 4). Auto and Size to fit both read
    /// this, so they agree on what a header needs.
    /// </summary>
    public double HeaderRequiredPx(string label, bool menuButton, bool sortable)
    {
        ArgumentNullException.ThrowIfNull(label);
        return CellMetrics.EstimatePx(label)
            + CellMetrics.FullWidthPx
            + (menuButton ? MenuButtonBandPx : 0)
            + (sortable ? SortMarkWidthPx : 0);
    }

    /// <summary>
    /// The tallest row a result of <paramref name="totalRowCount"/> rows can carry
    /// under the browser's scroll ceiling (ADR-0013/0028) — what a Consumer offering a
    /// density menu greys choices out with, instead of re-deriving the bound. The
    /// exception remains for the one who did not ask.
    /// </summary>
    public static double LargestRowHeightFor(int totalRowCount, double headerHeightPx)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(totalRowCount, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(headerHeightPx);
        return (ViewportGeometry.MaxScrollHeightPx - headerHeightPx) / totalRowCount;
    }

    /// <summary>
    /// Resolution, once, at parameter time (ADR-0028): a null means "not supplied", so
    /// the preset's value stands; a value wins over the preset. There is no way to hold
    /// two opinions. A Wrapper's cascaded <paramref name="defaults"/> sits between the
    /// two for the glyph widths (ADR-0030): explicit metrics beat it, it beats the
    /// preset's, and its widths are scaled to the preset's font size.
    /// </summary>
    public static GridMetrics Resolve(
        GridDensity density,
        double? rowHeightPx = null,
        double? headerHeightPx = null,
        CellTextMetrics? cellMetrics = null,
        GridPresentationDefaults? defaults = null)
    {
        // The preset table (ADR-0028), with the character-class widths of ADR-0016:
        // wide/digit/narrow are the widest measured for each class on any supported
        // platform at the stylesheet's system-ui 14px, 600 weight — DejaVu Sans on Linux
        // (14.028 / 9.742 / 6.398), which is wider than the first machine measured
        // (13.836 / 9.058 / 5.63). Full-width is the em: the font size. Excel's 12px set
        // is that measurement scaled, rounded up, provisional the way the whole preset is.
        // The action chrome is the 6px/1px/4px ex-grid.css always used, scaled down only
        // where the whole preset is — and the menu button's 16px/6px and the sort mark's
        // box (an em and a 6px gap) travel the same way, so the header estimate and the
        // stylesheet read one number (the pairing ADR-0027/0028 dissolved).
        var (row, header, font, wide, digit, narrow, padding, actionPad, actionBorder, actionGap, menuWidth, menuInset, sortMark)
            = density switch
        {
            GridDensity.Comfortable => (40d, 40d, 14d, 14.028, 9.742, 6.398, 12d, 6d, 1d, 4d, 16d, 6d, 20d),
            GridDensity.Standard => (32d, 32d, 14d, 14.028, 9.742, 6.398, 8d, 6d, 1d, 4d, 16d, 6d, 20d),
            GridDensity.Compact => (28d, 28d, 14d, 14.028, 9.742, 6.398, 8d, 6d, 1d, 4d, 16d, 6d, 20d),
            GridDensity.Excel => (20d, 20d, 12d, 12.024, 8.351, 5.484, 4d, 4d, 1d, 2d, 14d, 4d, 16d),
            _ => throw new ArgumentOutOfRangeException(nameof(density), density, "Unknown density preset."),
        };

        var resolvedRow = rowHeightPx ?? row;
        // An explicit RowHeight moves the header with it unless the header was set
        // itself: HeaderHeight = RowHeight is the default relationship an untouched
        // grid always had, and RowHeight="22" alone should not open a 28px header gap.
        var resolvedHeader = headerHeightPx ?? rowHeightPx ?? header;
        if (!double.IsFinite(resolvedRow) || resolvedRow <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowHeightPx), resolvedRow,
                "RowHeight is a finite, positive number of pixels (ADR-0013).");
        }
        if (!double.IsFinite(resolvedHeader) || resolvedHeader <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(headerHeightPx), resolvedHeader,
                "HeaderHeight is a finite, positive number of pixels (ADR-0028).");
        }

        return new GridMetrics(
            resolvedRow,
            resolvedHeader,
            font,
            // A full-width character is an em whatever the family, and the em is the font
            // size this grid emits, so explicit metrics too are charged at least that for
            // it — the uniform and three-class forms cannot know it (ADR-0016).
            (cellMetrics ?? defaults?.CellMetricsAt(font, padding) ?? new CellTextMetrics(wide, digit, narrow, font, padding))
                .WithFullWidthAtLeast(font),
            actionPad, actionBorder, actionGap,
            menuWidth, menuInset, sortMark);
    }
}
