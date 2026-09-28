namespace ExGrid;

/// <summary>
/// The vertical arithmetic of the Viewport: how tall the scrollable area is, which rows
/// a scroll offset puts on screen, and where those rows sit inside it. A fixed row
/// height is what makes each of these one multiplication instead of a prefix sum over
/// per-row heights (ADR-0013), and it is why the height travels as a C# value: the
/// selection overlay and the cell editor read the same numbers, so a height that only
/// CSS knew about would drift them out of alignment.
///
/// <para>Two offsets live here, and above the Layout Ceiling they differ (ADR-0053): the
/// <em>scroll offset</em> <c>s</c> the browser holds, and the <em>content offset</em>
/// <c>c(s) = s × k</c> the rows are computed from. Where the true height fits under the
/// ceiling the browser told, k is 1 and every answer is ADR-0013's, bit for bit — the
/// compressed branch is not even entered. Every consumer of vertical geometry reads
/// through this one mapping; none of them multiplies by k itself.</para>
/// </summary>
public readonly record struct ViewportGeometry
{
    /// <summary>The arithmetic for rows of <paramref name="rowHeightPx"/>, painted in
    /// <paramref name="viewportHeightPx"/>, over <paramref name="totalRowCount"/> rows,
    /// with no Layout Ceiling below the refusal. A scrollable height past
    /// <see cref="MaxScrollHeightPx"/> is refused rather than clamped out of reach.</summary>
    public ViewportGeometry(double rowHeightPx, double viewportHeightPx, int totalRowCount)
        : this(rowHeightPx, viewportHeightPx, totalRowCount, double.PositiveInfinity)
    {
    }

    /// <summary>The arithmetic for rows of <paramref name="rowHeightPx"/>, painted in
    /// <paramref name="viewportHeightPx"/>, over <paramref name="totalRowCount"/> rows,
    /// where the rows' part of the scroll content may be laid out at most
    /// <paramref name="scrollRoomPx"/> tall — the Layout Ceiling the browser told, less
    /// whatever else stands in the same content (the header band). A true height that
    /// fits is used as it is; one that does not is compressed (ADR-0053). A true height
    /// past <see cref="MaxScrollHeightPx"/> is refused whatever the room.</summary>
    public ViewportGeometry(double rowHeightPx, double viewportHeightPx, int totalRowCount, double scrollRoomPx)
    {
        if (!double.IsFinite(rowHeightPx) || rowHeightPx <= 0)
            throw new ArgumentOutOfRangeException(nameof(rowHeightPx), rowHeightPx,
                "RowHeight is a finite, positive number of pixels (ADR-0013).");
        if (!double.IsFinite(viewportHeightPx) || viewportHeightPx <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewportHeightPx), viewportHeightPx,
                "ViewportHeight is a finite, positive number of pixels (ADR-0013).");
        ArgumentOutOfRangeException.ThrowIfNegative(totalRowCount);
        if (double.IsNaN(scrollRoomPx) || scrollRoomPx <= 0)
            throw new ArgumentOutOfRangeException(nameof(scrollRoomPx), scrollRoomPx,
                "The room under the Layout Ceiling is a positive number of pixels, or unbounded (ADR-0053).");
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
        ContentHeightPx = totalRowCount * rowHeightPx;
        if (ContentHeightPx <= scrollRoomPx)
        {
            ScrollHeightPx = ContentHeightPx;
            Compression = 1;
            return;
        }

        // Compressed: the spacer stops a margin short of the ceiling, and k is chosen so
        // both ends are exact — s = 0 shows the first row, and the last row is flush with
        // the bottom from EndSlackPx short of the largest s on.
        var spacer = scrollRoomPx - LayoutCeilingMarginPx;
        if (spacer - EndSlackPx <= viewportHeightPx)
        {
            throw new InvalidOperationException(
                $"The browser lays out at most {scrollRoomPx}px of rows here, which leaves nothing to scroll a " +
                $"{viewportHeightPx}px Viewport through {ContentHeightPx}px of content (ADR-0053).");
        }
        ScrollHeightPx = spacer;
        _scrollReachPx = spacer - viewportHeightPx - EndSlackPx;
        Compression = (ContentHeightPx - viewportHeightPx) / _scrollReachPx;
        IsCompressed = true;
    }

    /// <summary>
    /// The tallest true height a result may have, refused by name past it (VZ-8): the
    /// Layout Ceiling at a display scale and page zoom of 100%. Chromium clamps any layout
    /// length just under 2^25 zoomed pixels, which at scale 1 is 33,554,428 CSS px — four
    /// short of 2^25 (ADR-0053). The target is Chromium (ADR-0017). Past this the tail of
    /// the result could not be reached even at 100%, so it is refused whatever the
    /// machine's scale, and what is refused never depends on where the grid is shown.
    /// Under it, a smaller ceiling at a higher scale or zoom is met by compressing, not by
    /// refusing. At the default 28px row that is about 1.19 million rows, and paging
    /// carries anything longer.
    /// </summary>
    public const double MaxScrollHeightPx = 33_554_428;

    /// <summary>How far short of the told ceiling a compressed spacer stops, so a ceiling
    /// that moves by a fraction of a pixel under zoom cannot clamp it after all
    /// (ADR-0053). Spent only when compressing: a height that fits is never shortened.</summary>
    public const double LayoutCeilingMarginPx = 256;

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

    /// <summary>The true height of the rows — total rows × row height (ADR-0013).</summary>
    public double ContentHeightPx { get; }

    /// <summary>The scrollbar's length: how tall the rows' part of the scroll content is
    /// laid out. The true height where it fits under the Layout Ceiling; the room less
    /// <see cref="LayoutCeilingMarginPx"/> where it does not (ADR-0053).</summary>
    public double ScrollHeightPx { get; }

    /// <summary>k in <c>c(s) = s × k</c>: 1 where the true height fits, and above it the
    /// ratio that makes both ends exact, <c>(H − V) / (S − V − </c><see cref="EndSlackPx"/><c>)</c>
    /// (ADR-0053).</summary>
    public double Compression { get; }

    /// <summary>Whether the scroll height is compressed — whether scroll and content
    /// offsets differ at all (ADR-0053).</summary>
    public bool IsCompressed { get; }

    /// <summary>The furthest the browser scrolls the rows: the scrollbar's length less
    /// the readable height, or 0.</summary>
    public double MaxScrollTopPx => Math.Max(0, ScrollHeightPx - ViewportHeightPx);

    /// <summary>
    /// How far short of <see cref="MaxScrollTopPx"/> a compressed grid already shows the
    /// last row flush with the bottom. The browser quantises <c>scrollTop</c> to device
    /// pixels and can stop short of the arithmetic maximum — measured at 150%: asked for
    /// 22,368,777, it held 22,368,776 — and at k = 1.25 that pixel cut the last row by more
    /// than one. Uncompressed, a pixel short is a pixel short and nothing needs absorbing.
    /// </summary>
    public const double EndSlackPx = 2;

    /// <summary>The scroll offset from which the content offset is at its end: the
    /// maximum less <see cref="EndSlackPx"/> when compressed, the maximum otherwise.</summary>
    public double ScrollReachPx => IsCompressed ? _scrollReachPx : MaxScrollTopPx;

    private readonly double _scrollReachPx;

    /// <summary>
    /// The content offset a scroll offset shows — <c>c(s) = s × k</c> (ADR-0053). The
    /// identity when not compressed, returned unclamped so nothing downstream changes;
    /// compressed, the offset is clamped to the scroll range first, as the slice is.
    /// </summary>
    public double ContentOffsetAt(double scrollTopPx)
        => IsCompressed ? Math.Clamp(scrollTopPx, 0, _scrollReachPx) * Compression : scrollTopPx;

    /// <summary>The scroll offset that shows a content offset: the inverse of
    /// <see cref="ContentOffsetAt"/>, unrounded (ADR-0053).</summary>
    public double ScrollTopAt(double contentOffsetPx)
        => IsCompressed ? contentOffsetPx / Compression : contentOffsetPx;

    /// <summary>
    /// A scroll offset moved so that the content moves by <paramref name="contentDeltaPx"/>
    /// — an edge auto-scroll tick or a PageUp/PageDown step, which are counted in rows and
    /// must move rows, not scrollbar pixels (ADR-0008/0012/0053). Clamped to the scroll
    /// range.
    /// </summary>
    public double ScrollTopStepped(double scrollTopPx, double contentDeltaPx)
        => IsCompressed
            ? Math.Clamp(ScrollTopAt(ContentOffsetAt(scrollTopPx) + contentDeltaPx), 0, MaxScrollTopPx)
            : Math.Clamp(scrollTopPx + contentDeltaPx, 0, MaxScrollTopPx);

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
            (int)Math.Floor(Math.Max(ContentOffsetAt(scrollTopPx), 0) / RowHeightPx),
            0,
            Math.Max(0, TotalRowCount - perViewport));
        return new RowRange(first, Math.Min(perViewport, TotalRowCount - first));
    }

    /// <summary>Where a row sits in the content: its true offset, which is also where it
    /// sits inside the scrollable area whenever the height is not compressed.</summary>
    public double OffsetPxOf(int rowIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowIndex);
        return rowIndex * RowHeightPx;
    }

    /// <summary>
    /// Where a painted row is placed inside the scrollable area while the browser is
    /// scrolled to <paramref name="scrollTopPx"/>: <see cref="OffsetPxOf"/> when not
    /// compressed; compressed, wherever puts it at <see cref="ViewportTopPxOf"/> once
    /// scrolled — which depends on the offset, so a compressed grid re-places its rows
    /// on every scroll (ADR-0053). Pass the offset the browser reported, never the one
    /// asked for: <c>scrollTop</c> is quantised to device pixels.
    /// </summary>
    public double PaintedTopPxOf(int rowIndex, double scrollTopPx)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowIndex);
        return IsCompressed
            ? (rowIndex * RowHeightPx) - ContentOffsetAt(scrollTopPx) + scrollTopPx
            : rowIndex * RowHeightPx;
    }

    /// <summary>Where a row's top edge stands relative to the top of the readable area
    /// at a scroll offset: <c>row × RowHeight − c(s)</c>. What the popovers, the edge
    /// bands and a context menu are laid out from (ADR-0053).</summary>
    public double ViewportTopPxOf(int rowIndex, double scrollTopPx)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowIndex);
        return (rowIndex * RowHeightPx) - ContentOffsetAt(scrollTopPx);
    }

    /// <summary>
    /// The scroll offset that brings a row fully into view, moving as little as possible
    /// — the arithmetic behind "Focus must always be visible" (ADR-0012). A row already
    /// on screen returns the offset unchanged, so arrowing down a visible column does
    /// not jump the Viewport.
    ///
    /// Compressed, the clamp is taken over content offsets and the answer rounded to a
    /// whole pixel towards showing the whole row (ADR-0053).
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

        var maxScrollTopPx = MaxScrollTopPx;
        var current = Math.Clamp(currentScrollTopPx, 0, maxScrollTopPx);
        var alignTop = rowIndex * RowHeightPx;
        var alignBottom = alignTop + RowHeightPx - ViewportHeightPx;
        if (!IsCompressed)
        {
            // A Viewport shorter than one row cannot show a row whole; top-aligning shows
            // where the value starts.
            var offset = Math.Clamp(current, Math.Min(alignBottom, alignTop), alignTop);
            return Math.Clamp(offset, 0, maxScrollTopPx);
        }

        // Compressed (ADR-0053): the same clamp, taken over content offsets, and asked for
        // back as s = c* / k. A row already whole on screen leaves the offset exactly where
        // the browser put it. Otherwise the answer is a whole CSS pixel, rounded towards
        // showing the whole row — down when the row is top-aligned, up when it is
        // bottom-aligned — so the quantisation of scrollTop to device pixels costs under a
        // pixel, and a second reveal from there finds the row visible and moves nothing.
        var content = ContentOffsetAt(current);
        var wanted = Math.Clamp(content, Math.Min(alignBottom, alignTop), alignTop);
        if (wanted == content)
            return current;
        if (wanted >= ContentHeightPx - ViewportHeightPx)
            return maxScrollTopPx;
        if (wanted <= 0)
            return 0;
        var exact = ScrollTopAt(wanted);
        var rounded = wanted < content ? Math.Floor(exact) : Math.Ceiling(exact);
        return Math.Clamp(rounded, 0, maxScrollTopPx);
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
