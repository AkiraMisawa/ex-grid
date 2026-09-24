namespace ExGrid;

/// <summary>
/// The vertical arithmetic of the Viewport: how tall the scrollable area is, which rows
/// a scroll offset puts on screen, and where those rows sit inside it. A fixed row
/// height is what makes each of these one multiplication instead of a prefix sum over
/// per-row heights (ADR-0013), and it is why the height travels as a C# value: the
/// selection overlay and the cell editor read the same numbers, so a height that only
/// CSS knew about would drift them out of alignment.
/// </summary>
public readonly record struct ViewportGeometry
{
    /// <summary>The arithmetic for rows of <paramref name="rowHeightPx"/>, painted in
    /// <paramref name="viewportHeightPx"/>, over <paramref name="totalRowCount"/> rows. A
    /// scrollable height past <see cref="MaxScrollHeightPx"/> is refused rather than
    /// clamped out of reach.</summary>
    public ViewportGeometry(double rowHeightPx, double viewportHeightPx, int totalRowCount)
    {
        if (!double.IsFinite(rowHeightPx) || rowHeightPx <= 0)
            throw new ArgumentOutOfRangeException(nameof(rowHeightPx), rowHeightPx,
                "RowHeight is a finite, positive number of pixels (ADR-0013).");
        if (!double.IsFinite(viewportHeightPx) || viewportHeightPx <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewportHeightPx), viewportHeightPx,
                "ViewportHeight is a finite, positive number of pixels (ADR-0013).");
        ArgumentOutOfRangeException.ThrowIfNegative(totalRowCount);
        if (totalRowCount * rowHeightPx > MaxScrollHeightPx)
        {
            throw new InvalidOperationException(
                $"{totalRowCount} rows at {rowHeightPx}px need a scrollable area of {totalRowCount * rowHeightPx}px, " +
                $"past the {MaxScrollHeightPx}px a browser can scroll: the rows beyond it could never be reached. " +
                "Use a shorter row height, or page the result (ADR-0015).");
        }

        RowHeightPx = rowHeightPx;
        ViewportHeightPx = viewportHeightPx;
        TotalRowCount = totalRowCount;
    }

    /// <summary>
    /// How tall an element a browser will scroll. Chromium caps it at 2^25 px, and the
    /// target is Chromium (ADR-0017). Past this the browser clamps the scrollable area
    /// silently: the scrollbar stops mapping to the last row and the tail of the result
    /// becomes unreachable with nothing to show for it. Rather than display a result
    /// that cannot be read to the end, say it cannot be done — at the default 28px row
    /// that is a ceiling of about 1.19 million rows, and paging carries anything longer.
    /// </summary>
    public const double MaxScrollHeightPx = 33_554_432;

    /// <summary>The fixed height of every row (ADR-0013).</summary>
    public double RowHeightPx { get; }

    /// <summary>The height the rows are painted in. The grid passes what the Viewport has
    /// left once the Scrollbar Gutter and the header band are taken out.</summary>
    public double ViewportHeightPx { get; }

    /// <summary>How many rows the scrollbar spans — the whole result, or one page under a
    /// pager (ADR-0015).</summary>
    public int TotalRowCount { get; }

    /// <summary>
    /// How many rows one Viewport paints: what fits, plus the row straddling the bottom
    /// edge — at any offset that is not an exact multiple of the row height there is one
    /// more row half on screen, and leaving it out would show a gap.
    /// </summary>
    public int RowsPerViewport => (int)Math.Ceiling(ViewportHeightPx / RowHeightPx) + 1;

    /// <summary>Scrollbar length — total rows × row height (ADR-0013).</summary>
    public double ScrollHeightPx => TotalRowCount * RowHeightPx;

    /// <summary>
    /// The rows a scroll offset puts on screen, or null when there are none to paint.
    /// The offset is clamped, so a position past the end — the total shrank under the
    /// user, or the browser has not yet clamped its own scrollTop — lands on the last
    /// full Viewport instead of painting nothing.
    /// </summary>
    public RowRange? SliceAt(double scrollTopPx)
    {
        if (!double.IsFinite(scrollTopPx))
            throw new ArgumentOutOfRangeException(nameof(scrollTopPx), scrollTopPx,
                "A scroll offset is a finite number of pixels.");
        if (TotalRowCount == 0)
            return null;

        var perViewport = RowsPerViewport;
        var first = Math.Clamp(
            (int)Math.Floor(Math.Max(scrollTopPx, 0) / RowHeightPx),
            0,
            Math.Max(0, TotalRowCount - perViewport));
        return new RowRange(first, Math.Min(perViewport, TotalRowCount - first));
    }

    /// <summary>Where the painted rows sit inside the scrollable area.</summary>
    public double OffsetPxOf(int rowIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowIndex);
        return rowIndex * RowHeightPx;
    }

    /// <summary>
    /// The scroll offset that brings a row fully into view, moving as little as possible
    /// — the arithmetic behind "Focus must always be visible" (ADR-0012). A row already
    /// on screen returns the offset unchanged, so arrowing down a visible column does
    /// not jump the Viewport.
    ///
    /// The header cancels out here, which is why this axis looks simpler than
    /// <see cref="ColumnGeometry.ScrollLeftToReveal"/>: the header occupies the first row
    /// height of the content AND covers the first row height of the Viewport, so the two
    /// offsets subtract away and top-aligning row r is <c>r × RowHeight</c> exactly. The
    /// horizontal axis has no such luck — a Pinned Column covers the Viewport's edge
    /// without occupying anything ahead of the content — so do not read one axis as the
    /// template for the other.
    /// </summary>
    public double ScrollTopToReveal(int rowIndex, double currentScrollTopPx)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(rowIndex, TotalRowCount);
        if (!double.IsFinite(currentScrollTopPx))
        {
            throw new ArgumentOutOfRangeException(nameof(currentScrollTopPx), currentScrollTopPx,
                "A scroll offset is a finite number of pixels.");
        }

        var maxScrollTopPx = Math.Max(0, ScrollHeightPx - ViewportHeightPx);
        var current = Math.Clamp(currentScrollTopPx, 0, maxScrollTopPx);
        var alignTop = rowIndex * RowHeightPx;
        var alignBottom = alignTop + RowHeightPx - ViewportHeightPx;
        // A Viewport shorter than one row cannot show a row whole; top-aligning shows
        // where the value starts.
        var offset = Math.Clamp(current, Math.Min(alignBottom, alignTop), alignTop);
        return Math.Clamp(offset, 0, maxScrollTopPx);
    }

    /// <summary>
    /// Which row a pixel belongs to, or null when there are no rows. The inverse of
    /// <see cref="OffsetPxOf"/>, and the vertical half of turning a mouse position into a
    /// cell (ADR-0008) — one division, for the same reason every other method here is one
    /// multiplication (ADR-0013).
    ///
    /// <paramref name="contentYPx"/> is measured from the top of the first row, not from
    /// the top of the Viewport: the caller adds the offset of whatever it painted first,
    /// which it already knows. A position outside the content is clamped rather than
    /// refused — a drag that runs past the last row keeps extending to the last row.
    /// </summary>
    public int? RowAt(double contentYPx)
    {
        if (!double.IsFinite(contentYPx))
            throw new ArgumentOutOfRangeException(nameof(contentYPx), contentYPx,
                "A pointer position is a finite number of pixels.");
        if (TotalRowCount == 0)
            return null;
        // Clamped as a double before the cast: a position far past the content would
        // otherwise overflow the conversion and land back at row 0 — the wrong end.
        return (int)Math.Clamp(Math.Floor(contentYPx / RowHeightPx), 0, TotalRowCount - 1);
    }

    /// <summary>
    /// Whether a move from one painted position to another is a fling — more than one
    /// Viewport at once, so every row changes and the row boundaries buy nothing
    /// (ADR-0003's memoisation does not help here, measured). Placeholders are painted
    /// for the duration and filled in once scrolling settles (ADR-0004).
    /// </summary>
    public bool IsFling(int paintedFirstRow, int firstRow)
        => Math.Abs((long)firstRow - paintedFirstRow) > RowsPerViewport;
}
