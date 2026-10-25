import { elementAt } from '../must';
import { clamp, finite, wholeAtLeast } from '../viewport/checks';
import { ViewportGeometry } from '../viewport/viewport-geometry';

/** A run of visible columns: the first one's index and how many follow. */
export interface ColumnRange {
    /** The first column's index, from 0. */
    readonly start: number;
    /** How many columns the run holds. */
    readonly count: number;
}

/** The whole Device Pixels from the content's left edge to a declared position. */
function devicePixelsAt(px: number, ratio: number): number {
    return Math.floor((px * ratio) + 0.5);
}

/**
 * The horizontal arithmetic of the Viewport: how wide the scrollable area is, which columns a scroll
 * offset puts on screen, and where each sits inside it. The counterpart of {@link ViewportGeometry}, for
 * the same reason (ADR-0013): the selection overlay and the cell editor read these numbers.
 *
 * The axes are not symmetric (ADR-0004). Pinned Columns are painted over the content rather than beside
 * it, so the pixels they cover are not available to the columns underneath: a column scrolled to
 * `offsetPxOf(c)` would land under the pinned block, unseen. Every method here accounts for that in one
 * place. Widths are per column, so the offsets are a prefix sum computed once.
 */
export class ColumnGeometry {
    /** The narrowest band the Pinned block may leave the scrollable columns before pinning is suspended:
     * a column's default minimum width (ADR-0045). */
    static readonly minScrollableBandPx = 40;

    /** The scale-1 Layout Ceiling the vertical axis is refused at: a browser clamps either axis silently,
     * and content past the clamp cannot be reached (ADR-0053). */
    static readonly maxScrollWidthPx = ViewportGeometry.maxScrollHeightPx;

    /** A length put on the nearest Device Pixel at `devicePixelRatio` (ADR-0090), or the length itself
     * while the ratio is not told. */
    static onDevicePixel(px: number, devicePixelRatio?: number): number {
        return devicePixelRatio === undefined ? px : devicePixelsAt(px, devicePixelRatio) / devicePixelRatio;
    }

    // Count + 1 entries: #offsets[i] is column i's left edge and #offsets[count] the total width.
    readonly #offsets: readonly number[];
    // The painted widths, kept beside the offsets rather than recovered from them: a running sum loses
    // low bits, so a measured 8.55px column would come back an ulp away, and a caller comparing "did
    // this width move?" would see movement on every frame (ADR-0003).
    readonly #widths: readonly number[];
    // The widths as declared, before their edges were put on Device Pixels (ADR-0090).
    readonly #declared: readonly number[];

    /** The Device Pixel ratio the edges were put on, or `undefined` while the browser has not told it,
     * when the edges are the declared ones (ADR-0090). */
    readonly devicePixelRatio: number | undefined;
    /** The band held against the Viewport's left edge ahead of every column, the Row Headings
     * (ADR-0050), or zero. Column 0's offset is this, and the pinned width includes it. */
    readonly leadWidthPx: number;
    /** How many columns there are, pinned and scrollable alike. */
    readonly count: number;
    /** How many leading columns are pinned: zero while pinning is suspended, and every other member reads
     * this, so they all agree on it (ADR-0045). */
    readonly pinnedCount: number;
    /** How many leading columns the View State asked to pin, whether or not they are pinned now. */
    readonly requestedPinnedCount: number;
    /** Whether the requested Pinned block is suspended because it would leave the scrollable columns less
     * than {@link ColumnGeometry.minScrollableBandPx} (ADR-0045). */
    readonly isPinningSuspended: boolean;
    /** The width the columns are painted in, the pinned block included, with the Scrollbar Gutter
     * already taken out (ADR-0013). */
    readonly viewportWidthPx: number;

    /**
     * The arithmetic over `widthsPx`, every column's resolved width in display order (ADR-0016), with the
     * first `pinnedCount` pinned, laid out in `viewportWidthPx`, behind a lead band of `leadWidthPx` (the
     * Row Headings, ADR-0050). With `devicePixelRatio`, every column edge is its declared position rounded
     * to the nearest Device Pixel, and a painted width the distance to the next edge (ADR-0090). A total
     * width past {@link ColumnGeometry.maxScrollWidthPx} is refused; a Pinned block that would leave the
     * scrollable columns less than {@link ColumnGeometry.minScrollableBandPx} is suspended (ADR-0045).
     */
    constructor({ widthsPx, pinnedCount, viewportWidthPx, leadWidthPx = 0, devicePixelRatio }: {
        readonly widthsPx: readonly number[];
        readonly pinnedCount: number;
        readonly viewportWidthPx: number;
        readonly leadWidthPx?: number;
        readonly devicePixelRatio?: number;
    }) {
        if (devicePixelRatio !== undefined && (!Number.isFinite(devicePixelRatio) || devicePixelRatio <= 0)) {
            throw new RangeError(`A Device Pixel ratio is a finite, positive number (ADR-0090). (devicePixelRatio: ${devicePixelRatio})`);
        }
        if (!Number.isFinite(leadWidthPx) || leadWidthPx < 0) {
            throw new RangeError(`The Row Headings' width is a finite, non-negative number of pixels (ADR-0050). (leadWidthPx: ${leadWidthPx})`);
        }
        wholeAtLeast(pinnedCount, 0, 'pinnedCount');
        if (pinnedCount > widthsPx.length) {
            throw new RangeError(`PinnedColumnCount is the first N of ${widthsPx.length} columns; there are not that many. (pinnedCount: ${pinnedCount})`);
        }
        if (!Number.isFinite(viewportWidthPx) || viewportWidthPx <= 0) {
            throw new RangeError(`ViewportWidth is a finite, positive number of pixels (ADR-0013). (viewportWidthPx: ${viewportWidthPx})`);
        }

        const offsets: number[] = [];
        const widths: number[] = [];
        // Each edge is counted in whole Device Pixels from the declared position: an edge is its count
        // over the ratio and a width the difference of two counts, so neither carries the noise of a
        // running sum (ADR-0090). Untold, the declared widths are painted, summed.
        let declaredEdge = leadWidthPx;
        let paintedEdge = devicePixelRatio === undefined ? 0 : devicePixelsAt(declaredEdge, devicePixelRatio);
        let offset = devicePixelRatio === undefined ? leadWidthPx : paintedEdge / devicePixelRatio;
        offsets.push(offset);
        widthsPx.forEach((width, i) => {
            if (!Number.isFinite(width) || width < 0) {
                throw new RangeError(`Column ${i} has a width of ${width}px; a resolved width is finite and non-negative (ADR-0016).`);
            }
            if (devicePixelRatio === undefined) {
                widths.push(width);
                offset += width;
                offsets.push(offset);
                return;
            }
            declaredEdge += width;
            const next = devicePixelsAt(declaredEdge, devicePixelRatio);
            widths.push((next - paintedEdge) / devicePixelRatio);
            offsets.push(next / devicePixelRatio);
            paintedEdge = next;
        });

        const total = elementAt(offsets, widthsPx.length);
        if (total > ColumnGeometry.maxScrollWidthPx) {
            throw new Error(
                `${widthsPx.length} columns need a scrollable width of ${total}px, past the ${ColumnGeometry.maxScrollWidthPx}px a `
                + 'browser can scroll: the columns beyond it could never be reached. Use narrower columns, or show fewer of them '
                + '(View State decides which are visible).');
        }

        this.#offsets = offsets;
        this.#widths = widths;
        this.#declared = [...widthsPx];
        this.count = widthsPx.length;
        this.leadWidthPx = elementAt(offsets, 0);
        this.devicePixelRatio = devicePixelRatio;
        this.requestedPinnedCount = pinnedCount;
        // A block covering the Viewport shows only pinned columns and leaves a Focus moved into a
        // scrollable column underneath it, unseen (ADR-0012). So while it would leave less than a
        // column's default floor, every column scrolls together; the request stands (ADR-0045).
        this.isPinningSuspended = pinnedCount > 0 && viewportWidthPx - elementAt(offsets, pinnedCount) < ColumnGeometry.minScrollableBandPx;
        this.pinnedCount = this.isPinningSuspended ? 0 : pinnedCount;
        this.viewportWidthPx = viewportWidthPx;
    }

    /** Scrollbar length: every column, whether or not it is painted. */
    get totalWidthPx(): number {
        return elementAt(this.#offsets, this.count);
    }

    /** How much of the Viewport's left edge the Pinned Columns (and the lead band) cover. */
    get pinnedWidthPx(): number {
        return elementAt(this.#offsets, this.pinnedCount);
    }

    /** The furthest right a browser will scroll this content. */
    get maxScrollLeftPx(): number {
        return Math.max(0, this.totalWidthPx - this.viewportWidthPx);
    }

    /** The width this column declared, before its edges were put on Device Pixels (ADR-0090). */
    declaredWidthPxOf(columnIndex: number): number {
        return elementAt(this.#declared, this.#column(columnIndex));
    }

    /** Whether a pixel lies in the lead band, which stays at the Viewport's left edge whatever the scroll
     * offset (ADR-0050). Always false without one. */
    isInLead(contentXPx: number, scrollLeftPx: number): boolean {
        if (this.leadWidthPx <= 0 || !Number.isFinite(contentXPx) || !Number.isFinite(scrollLeftPx)) {
            return false;
        }
        const viewportX = contentXPx - clamp(scrollLeftPx, 0, this.maxScrollLeftPx);
        return viewportX >= 0 && viewportX < this.leadWidthPx;
    }

    /** Where a column's left edge sits in the content. Takes `count` itself, the content's right edge. */
    offsetPxOf(columnIndex: number): number {
        wholeAtLeast(columnIndex, 0, 'columnIndex');
        if (columnIndex > this.count) {
            throw new RangeError(`columnIndex is at most the ${this.count} columns, not ${columnIndex}.`);
        }
        return elementAt(this.#offsets, columnIndex);
    }

    /** The width this column is painted at, exactly: the width it was built with until its edges are put
     * on Device Pixels (ADR-0090). */
    widthPxOf(columnIndex: number): number {
        return elementAt(this.#widths, this.#column(columnIndex));
    }

    /**
     * The scrollable columns a scroll offset puts on screen (never the pinned ones, which are painted
     * regardless), or `undefined` when there are none to paint. A column only partly on screen is
     * included; one lying entirely under the pinned block is not. With `virtualise` false every
     * scrollable column is returned, through the same path, so "the columns on screen" means one thing.
     */
    scrollableSliceAt(scrollLeftPx: number, virtualise: boolean): ColumnRange | undefined {
        finite(scrollLeftPx, 'scrollLeftPx', 'A scroll offset is a finite number of pixels.');
        if (this.pinnedCount >= this.count) {
            return undefined;
        }
        if (!virtualise) {
            return Object.freeze({ start: this.pinnedCount, count: this.count - this.pinnedCount });
        }
        // Clamped as the vertical offset is: a position past the end lands on the last full Viewport.
        const left = clamp(scrollLeftPx, 0, this.maxScrollLeftPx);
        // The scrollable columns get the Viewport less the band the pinned block covers; pinned wider than
        // the Viewport is legal, and leaves nothing to slice.
        const from = left + this.pinnedWidthPx;
        const to = left + this.viewportWidthPx;
        if (to <= from) {
            return undefined;
        }
        const first = this.#firstEndingPast(from);
        const end = this.#firstStartingAtOrPast(to);
        return end > first ? Object.freeze({ start: first, count: end - first }) : undefined;
    }

    /**
     * The scroll offset that brings a column fully into view, moving as little as possible (ADR-0012). A
     * column already on screen returns the offset unchanged. Scrolling to `offsetPxOf(c)` would slide the
     * column under the Pinned Columns, so the pinned width comes off, and a pinned column never needs
     * revealing. The browser's `scrollIntoView()` knows nothing of a pinned block, which is why the offset
     * is computed here and assigned (ADR-0021).
     */
    scrollLeftToReveal(columnIndex: number, currentScrollLeftPx: number): number {
        this.#column(columnIndex);
        finite(currentScrollLeftPx, 'currentScrollLeftPx', 'A scroll offset is a finite number of pixels.');
        const current = clamp(currentScrollLeftPx, 0, this.maxScrollLeftPx);
        if (columnIndex < this.pinnedCount) {
            return current;
        }
        // Left-aligned just clear of the pinned block, and right-aligned against the Viewport's right
        // edge. A column wider than the space left cannot be shown whole; left-aligning shows its start.
        const alignLeft = elementAt(this.#offsets, columnIndex) - this.pinnedWidthPx;
        const alignRight = elementAt(this.#offsets, columnIndex + 1) - this.viewportWidthPx;
        const offset = clamp(current, Math.min(alignRight, alignLeft), alignLeft);
        return clamp(offset, 0, this.maxScrollLeftPx);
    }

    /**
     * Which column a pixel belongs to, or `undefined` when there are no columns: the horizontal half of
     * turning a pointer position into a cell (ADR-0008), without measuring elements (ADR-0021). A Pinned
     * Column covers the Viewport's left edge, so the pixels in that band belong to the pinned column drawn
     * over them; at `scrollLeft = 0` the two coincide, which is why testing only there proves nothing. A
     * position outside the content is clamped: a drag past the last column keeps extending to it.
     */
    columnAt(contentXPx: number, scrollLeftPx: number): number | undefined {
        finite(contentXPx, 'contentXPx', 'A pointer position is a finite number of pixels.');
        finite(scrollLeftPx, 'scrollLeftPx', 'A scroll offset is a finite number of pixels.');
        if (this.count === 0) {
            return undefined;
        }
        const left = clamp(scrollLeftPx, 0, this.maxScrollLeftPx);
        let viewportX = contentXPx - left;
        let contentX = contentXPx;
        // The lead band names no column, and what has scrolled under it is no more readable than what lies
        // under a Pinned Column: a pixel there clamps to the first column clear of the band (ADR-0050).
        if (viewportX < this.leadWidthPx) {
            viewportX = this.leadWidthPx;
            contentX = left + this.leadWidthPx;
        }
        // Everything pinned leaves no scrollable run to fall through to, and a pixel past the pinned block
        // clamps to the last pinned column rather than naming one that does not exist.
        if (this.pinnedCount > 0 && (viewportX < this.pinnedWidthPx || this.pinnedCount >= this.count)) {
            return this.#indexContaining(Math.max(0, viewportX), 0, this.pinnedCount);
        }
        return this.#indexContaining(contentX, this.pinnedCount, this.count);
    }

    /** Whether a horizontal move is a fling: more than one Viewport at once, in pixels, because columns
     * have no common width (ADR-0004). */
    isFling(fromScrollLeftPx: number, toScrollLeftPx: number): boolean {
        if (!Number.isFinite(fromScrollLeftPx) || !Number.isFinite(toScrollLeftPx)) {
            return false;
        }
        return Math.abs(toScrollLeftPx - fromScrollLeftPx) > this.viewportWidthPx;
    }

    /** A column index this geometry has; anything else is refused. */
    #column(columnIndex: number): number {
        wholeAtLeast(columnIndex, 0, 'columnIndex');
        if (columnIndex >= this.count) {
            throw new RangeError(`columnIndex is a column of the ${this.count}, not ${columnIndex}.`);
        }
        return columnIndex;
    }

    /** The column of `[lo, hi)` holding x, clamped to that run at both ends. A zero-width column holds no
     * pixel and is stepped over. */
    #indexContaining(x: number, lo: number, hi: number): number {
        let low = lo;
        let high = hi - 1;
        while (low < high) {
            const mid = low + Math.floor((high - low) / 2);
            if (elementAt(this.#offsets, mid + 1) > x) {
                high = mid;
            } else {
                low = mid + 1;
            }
        }
        return low;
    }

    /** The first scrollable column whose right edge lies past x; `count` when none does. */
    #firstEndingPast(x: number): number {
        let lo = this.pinnedCount;
        let hi = this.count;
        while (lo < hi) {
            const mid = lo + Math.floor((hi - lo) / 2);
            if (elementAt(this.#offsets, mid + 1) > x) {
                hi = mid;
            } else {
                lo = mid + 1;
            }
        }
        return lo;
    }

    /** The first scrollable column starting at or past x: the exclusive end of the run with any pixel
     * before it. */
    #firstStartingAtOrPast(x: number): number {
        let lo = this.pinnedCount;
        let hi = this.count;
        while (lo < hi) {
            const mid = lo + Math.floor((hi - lo) / 2);
            if (elementAt(this.#offsets, mid) >= x) {
                hi = mid;
            } else {
                lo = mid + 1;
            }
        }
        return lo;
    }
}
