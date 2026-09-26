using ExGrid.Columns;
using ExGrid.Selection;

namespace ExGrid;

/// <summary>
/// The horizontal arithmetic of the Viewport: how wide the scrollable area is, which
/// columns a scroll offset puts on screen, and where each one sits inside it. The
/// counterpart of <see cref="ViewportGeometry"/>, and the reason both exist is the same
/// (ADR-0013): the selection overlay and the cell editor read these numbers, so a
/// position that only CSS knew about would drift them out of alignment.
///
/// The two axes are NOT symmetric, and the difference is the whole of what makes this
/// type fiddlier than its vertical twin (ADR-0004 says as much). Pinned Columns are
/// painted over the content rather than beside it, so the pixels they cover are not
/// available to the columns underneath: a column scrolled to <c>OffsetPxOf(c)</c> would
/// land under the pinned block and be invisible. Every method here accounts for that in
/// one place so no caller has to.
///
/// Widths are per column and arbitrary, so the offsets are a prefix sum computed once —
/// the fixed row height that makes the vertical case one multiplication has no
/// horizontal equivalent.
/// </summary>
public sealed class ColumnGeometry
{
    // Count + 1 entries: _offsets[i] is column i's left edge and _offsets[Count] is the
    // total width, so a column's right edge is _offsets[i + 1] with no special case at
    // the end.
    private readonly double[] _offsets;

    // The widths as given, kept beside the offsets rather than recovered from them.
    // `_offsets[i + 1] - _offsets[i]` is the same number only for integral widths: a
    // running sum loses low bits, so a measured 8.55px column would come back an ulp
    // away from what was passed in. The caller comparing "did this width move?" would
    // then see movement on every frame and rebuild everything downstream (ADR-0003 —
    // the rows would stop skipping, silently).
    private readonly double[] _widths;

    /// <summary>The arithmetic over <paramref name="widthsPx"/> — every column's resolved
    /// width, in display order (ADR-0016) — with the first <paramref name="pinnedCount"/>
    /// pinned, laid out in <paramref name="viewportWidthPx"/>. A total width past
    /// <see cref="MaxScrollWidthPx"/> is refused rather than clamped out of reach. A
    /// Pinned block that would leave the scrollable columns less than
    /// <see cref="MinScrollableBandPx"/> is suspended (ADR-0045).</summary>
    public ColumnGeometry(IReadOnlyList<double> widthsPx, int pinnedCount, double viewportWidthPx)
    {
        ArgumentNullException.ThrowIfNull(widthsPx);
        ArgumentOutOfRangeException.ThrowIfNegative(pinnedCount);
        if (pinnedCount > widthsPx.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(pinnedCount), pinnedCount,
                $"PinnedColumnCount is the first N of {widthsPx.Count} columns; there are not that many.");
        }
        if (!double.IsFinite(viewportWidthPx) || viewportWidthPx <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewportWidthPx), viewportWidthPx,
                "ViewportWidth is a finite, positive number of pixels (ADR-0013).");
        }

        _offsets = new double[widthsPx.Count + 1];
        _widths = new double[widthsPx.Count];
        for (var i = 0; i < widthsPx.Count; i++)
        {
            var width = widthsPx[i];
            if (!double.IsFinite(width) || width < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(widthsPx), width,
                    $"Column {i} has a width of {width}px; a resolved width is finite and non-negative (ADR-0016).");
            }
            _widths[i] = width;
            _offsets[i + 1] = _offsets[i] + width;
        }

        if (_offsets[widthsPx.Count] > MaxScrollWidthPx)
        {
            throw new InvalidOperationException(
                $"{widthsPx.Count} columns need a scrollable width of {_offsets[widthsPx.Count]}px, " +
                $"past the {MaxScrollWidthPx}px a browser can scroll: the columns beyond it could never be reached. " +
                "Use narrower columns, or show fewer of them (View State decides which are visible).");
        }

        Count = widthsPx.Count;
        RequestedPinnedCount = pinnedCount;
        // A block covering the Viewport shows only pinned columns, pans nothing visible,
        // and leaves a Focus moved into a scrollable column underneath it — unseen, which
        // breaks ADR-0012 without anything looking wrong. So while it would leave less than
        // a column's default floor, every column scrolls together. The request stands:
        // this is how the grid paints, not what was asked for (ADR-0045).
        IsPinningSuspended = pinnedCount > 0 && viewportWidthPx - _offsets[pinnedCount] < MinScrollableBandPx;
        PinnedCount = IsPinningSuspended ? 0 : pinnedCount;
        ViewportWidthPx = viewportWidthPx;
    }

    /// <summary>The narrowest band the Pinned block may leave the scrollable columns
    /// before pinning is suspended — a column's default <c>MinWidth</c> (ADR-0045).</summary>
    public const double MinScrollableBandPx = ColumnWidthSpec.DefaultMinWidthPx;

    /// <summary>
    /// The same 2^25 px ceiling the vertical axis hits
    /// (<see cref="ViewportGeometry.MaxScrollHeightPx"/>) — a browser clamps either axis
    /// silently, and content past the clamp cannot be reached with nothing to show for
    /// it. 100 columns at 100px is nowhere near this; a generated ladder is what gets
    /// close, and the rule is not put on one axis only.
    /// </summary>
    public const double MaxScrollWidthPx = ViewportGeometry.MaxScrollHeightPx;

    /// <summary>How many columns there are, pinned and scrollable alike.</summary>
    public int Count { get; }

    /// <summary>How many leading columns are pinned — always painted, outside
    /// virtualisation, and costing directly (ADR-0004). Zero while pinning is suspended;
    /// every other member reads this, so they all agree on it (ADR-0045).</summary>
    public int PinnedCount { get; }

    /// <summary>How many leading columns the View State asked to pin, whether or not
    /// they are pinned now (ADR-0045).</summary>
    public int RequestedPinnedCount { get; }

    /// <summary>Whether the requested Pinned block is suspended because it would leave
    /// the scrollable columns less than <see cref="MinScrollableBandPx"/> (ADR-0045).</summary>
    public bool IsPinningSuspended { get; }

    /// <summary>The width the columns are painted in, the pinned block included. The grid
    /// passes the Viewport's width with the Scrollbar Gutter already taken out
    /// (ADR-0013).</summary>
    public double ViewportWidthPx { get; }

    /// <summary>Scrollbar length — every column, whether or not it is painted.</summary>
    public double TotalWidthPx => _offsets[Count];

    /// <summary>How much of the Viewport's left edge the Pinned Columns cover.</summary>
    public double PinnedWidthPx => _offsets[PinnedCount];

    /// <summary>The furthest right a browser will scroll this content.</summary>
    public double MaxScrollLeftPx => Math.Max(0, TotalWidthPx - ViewportWidthPx);

    /// <summary>Where a column's left edge sits in the content. Accepts
    /// <see cref="Count"/> itself, which is the content's right edge.</summary>
    public double OffsetPxOf(int columnIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(columnIndex, Count);
        return _offsets[columnIndex];
    }

    /// <summary>The width this column was built with, returned exactly — see
    /// <c>_widths</c> for why it is not recovered from the offsets.</summary>
    public double WidthPxOf(int columnIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(columnIndex, Count);
        return _widths[columnIndex];
    }

    /// <summary>
    /// The scrollable columns a scroll offset puts on screen — never the pinned ones,
    /// which are painted regardless — or null when there are none to paint. A column
    /// only partly on screen is included; one lying entirely under the pinned block is
    /// not, because painting it would put cells in the DOM that nobody can see, which is
    /// the cost this exists to avoid.
    /// </summary>
    /// <param name="scrollLeftPx">The Viewport's horizontal scroll offset. Clamped to what
    /// the browser can scroll, as the vertical one is.</param>
    /// <param name="virtualise">
    /// When false every scrollable column is returned and the caller paints them all.
    /// The switch lives here rather than in the component so that both settings travel
    /// the same rendering path: two paths would mean two ways of deciding what "the
    /// columns on screen" are, and they would drift.
    /// </param>
    public ColumnRange? ScrollableSliceAt(double scrollLeftPx, bool virtualise)
    {
        if (!double.IsFinite(scrollLeftPx))
        {
            throw new ArgumentOutOfRangeException(nameof(scrollLeftPx), scrollLeftPx,
                "A scroll offset is a finite number of pixels.");
        }
        if (PinnedCount >= Count)
            return null;
        if (!virtualise)
            return new ColumnRange(PinnedCount, Count - PinnedCount);

        // The offset is clamped for the same reason the vertical one is: a position past
        // the end — the columns narrowed under the user, or the browser has not yet
        // clamped its own scrollLeft — lands on the last full Viewport rather than
        // painting nothing.
        var left = Math.Clamp(scrollLeftPx, 0, MaxScrollLeftPx);

        // What the scrollable columns actually get: the Viewport minus the band the
        // pinned block covers. Pinned wider than the whole Viewport is legal — the user
        // sees only pinned columns — and leaves nothing to slice.
        var from = left + PinnedWidthPx;
        var to = left + ViewportWidthPx;
        if (to <= from)
            return null;

        var first = FirstEndingPast(from);
        var end = FirstStartingAtOrPast(to);
        return end > first ? new ColumnRange(first, end - first) : null;
    }

    /// <summary>
    /// The scroll offset that brings a column fully into view, moving as little as
    /// possible — the arithmetic behind "Focus must always be visible" (ADR-0012). A
    /// column already on screen returns the offset unchanged, so arrowing along a
    /// visible row does not jump the Viewport.
    ///
    /// This is the asymmetry with <see cref="ViewportGeometry.ScrollTopToReveal"/>
    /// spelled out: scrolling to <c>OffsetPxOf(c)</c> would slide column c underneath
    /// the Pinned Columns, which cover the Viewport's left edge — visibly present in the
    /// DOM and completely unreadable. The pinned width has to come off, and a pinned
    /// column itself never needs revealing at all.
    ///
    /// The browser's own <c>scrollIntoView()</c> is the wrong tool here for exactly that
    /// reason: it knows nothing of a sticky header or a pinned block and would tuck the
    /// element underneath them. Computing the offset in C# and assigning it is what the
    /// allowlist permits (ADR-0021), and it structurally cannot make that mistake.
    /// </summary>
    public double ScrollLeftToReveal(int columnIndex, double currentScrollLeftPx)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(columnIndex, Count);
        if (!double.IsFinite(currentScrollLeftPx))
        {
            throw new ArgumentOutOfRangeException(nameof(currentScrollLeftPx), currentScrollLeftPx,
                "A scroll offset is a finite number of pixels.");
        }

        var current = Math.Clamp(currentScrollLeftPx, 0, MaxScrollLeftPx);
        if (columnIndex < PinnedCount)
            return current;

        // Left-aligned just clear of the pinned block, and right-aligned against the
        // Viewport's right edge. Anything between the two shows the column whole.
        var alignLeft = _offsets[columnIndex] - PinnedWidthPx;
        var alignRight = _offsets[columnIndex + 1] - ViewportWidthPx;
        // A column wider than the space left over cannot be shown whole; left-aligning
        // it shows its start, which is where a value begins.
        var offset = Math.Clamp(current, Math.Min(alignRight, alignLeft), alignLeft);
        return Math.Clamp(offset, 0, MaxScrollLeftPx);
    }

    /// <summary>
    /// Which column a pixel belongs to, or null when there are no columns. The inverse of
    /// <see cref="OffsetPxOf"/>, and the horizontal half of turning a mouse position into
    /// a cell (ADR-0008): the grid reads the pointer's offset within the painted area and
    /// asks this what was pointed at, rather than measuring elements through JavaScript,
    /// which is deliberately not on the allowlist (ADR-0021).
    ///
    /// The scroll offset is an argument because of the same asymmetry the rest of this
    /// type carries: <paramref name="contentXPx"/> is a position in the content, but a
    /// Pinned Column covers the Viewport's left edge, so the pixels in that band belong to
    /// the pinned column drawn over them rather than to whatever content has scrolled
    /// underneath. Without the offset the two cannot be told apart — and at
    /// <c>scrollLeft = 0</c> they coincide, which is exactly why testing only there proves
    /// nothing.
    ///
    /// A position outside the content is clamped rather than refused: a drag that runs
    /// past the last column keeps extending to the last column, which is what a user
    /// dragging to the edge means.
    /// </summary>
    public int? ColumnAt(double contentXPx, double scrollLeftPx)
    {
        if (!double.IsFinite(contentXPx))
        {
            throw new ArgumentOutOfRangeException(nameof(contentXPx), contentXPx,
                "A pointer position is a finite number of pixels.");
        }
        if (!double.IsFinite(scrollLeftPx))
        {
            throw new ArgumentOutOfRangeException(nameof(scrollLeftPx), scrollLeftPx,
                "A scroll offset is a finite number of pixels.");
        }
        if (Count == 0)
            return null;

        var left = Math.Clamp(scrollLeftPx, 0, MaxScrollLeftPx);
        var viewportX = contentXPx - left;
        // Everything pinned means there is no scrollable run to fall through to, and a
        // pixel past the pinned block — the Viewport is wider than the columns, so there
        // is empty space to the right of them — has to clamp to the last pinned column
        // rather than name a column that does not exist.
        if (PinnedCount > 0 && (viewportX < PinnedWidthPx || PinnedCount >= Count))
            return IndexContaining(Math.Max(0, viewportX), 0, PinnedCount);
        return IndexContaining(contentXPx, PinnedCount, Count);
    }

    /// <summary>
    /// Whether a horizontal move is a fling — more than one Viewport at once, so every
    /// row's cells change and the row boundaries buy nothing, exactly as in the vertical
    /// case (<see cref="ViewportGeometry.IsFling"/>). Measured in pixels rather than in
    /// columns because columns have no common width; a "column count" threshold would
    /// mean something different in every grid.
    /// </summary>
    public bool IsFling(double fromScrollLeftPx, double toScrollLeftPx)
    {
        if (!double.IsFinite(fromScrollLeftPx) || !double.IsFinite(toScrollLeftPx))
            return false;
        return Math.Abs(toScrollLeftPx - fromScrollLeftPx) > ViewportWidthPx;
    }

    /// <summary>The column of <c>[lo, hi)</c> holding x, clamped to that run at both ends.
    /// A zero-width column holds no pixel and is stepped over.</summary>
    private int IndexContaining(double x, int lo, int hi)
    {
        var low = lo;
        var high = hi - 1;
        while (low < high)
        {
            var mid = low + ((high - low) / 2);
            if (_offsets[mid + 1] > x)
                high = mid;
            else
                low = mid + 1;
        }
        return low;
    }

    /// <summary>The first scrollable column whose right edge lies past x — the leftmost
    /// one with any pixel at or beyond it. <see cref="Count"/> when none does.</summary>
    private int FirstEndingPast(double x)
    {
        var lo = PinnedCount;
        var hi = Count;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) / 2);
            if (_offsets[mid + 1] > x)
                hi = mid;
            else
                lo = mid + 1;
        }
        return lo;
    }

    /// <summary>The first scrollable column starting at or past x — the exclusive end of
    /// the run that has any pixel before it.</summary>
    private int FirstStartingAtOrPast(double x)
    {
        var lo = PinnedCount;
        var hi = Count;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) / 2);
            if (_offsets[mid] >= x)
                hi = mid;
            else
                lo = mid + 1;
        }
        return lo;
    }
}
