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
    /// target is Chromium ([ADR-0017]). Past this the browser clamps the scrollable area
    /// silently: the scrollbar stops mapping to the last row and the tail of the result
    /// becomes unreachable with nothing to show for it. Rather than display a result
    /// that cannot be read to the end, say it cannot be done — at the default 28px row
    /// that is a ceiling of about 1.19 million rows, and paging carries anything longer.
    /// </summary>
    public const double MaxScrollHeightPx = 33_554_432;

    public double RowHeightPx { get; }

    public double ViewportHeightPx { get; }

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
    /// Whether a move from one painted position to another is a fling — more than one
    /// Viewport at once, so every row changes and the row boundaries buy nothing
    /// (ADR-0003's memoisation does not help here, measured). Placeholders are painted
    /// for the duration and filled in once scrolling settles (ADR-0004).
    /// </summary>
    public bool IsFling(int paintedFirstRow, int firstRow)
        => Math.Abs((long)firstRow - paintedFirstRow) > RowsPerViewport;
}
