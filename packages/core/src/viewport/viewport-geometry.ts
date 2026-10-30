import { clamp, finite, finitePositive, wholeAtLeast } from './checks';
import { rowRange, type RowRange } from './row-range';

/**
 * The vertical arithmetic of the Viewport: how tall the scrollable area is, which rows a scroll offset
 * puts on screen, and where those rows sit inside it. A fixed row height makes each of these one
 * multiplication instead of a prefix sum over per-row heights (ADR-0013), and it is why the height is a
 * number the core holds: the selection overlay and the cell editor read the same numbers, so a height
 * only the stylesheet knew would drift them out of alignment.
 *
 * Two offsets live here, and above the Layout Ceiling they differ (ADR-0053): the scroll offset `s` the
 * browser holds, and the content offset `c(s) = s × k` the rows are computed from. Where the true height
 * fits under the ceiling the browser told, k is 1 and every answer is ADR-0013's, bit for bit. Every
 * reader of vertical geometry goes through this one mapping; none multiplies by k itself.
 */
export class ViewportGeometry {
    /**
     * The tallest true height a result may have, refused by name past it (VZ-8): the Layout Ceiling at a
     * display scale and page zoom of 100%. Chromium clamps any layout length just under 2^25 zoomed
     * pixels, which at scale 1 is 33,554,428 CSS px (ADR-0053). Past this the tail could not be reached
     * even at 100%, so it is refused whatever the machine's scale; under it, a smaller ceiling at a higher
     * scale or zoom is met by compressing.
     */
    static readonly maxScrollHeightPx = 33_554_428;

    /** How far short of the told ceiling a compressed spacer stops, so a ceiling that moves by a fraction
     * of a pixel under zoom cannot clamp it after all (ADR-0053). */
    static readonly layoutCeilingMarginPx = 256;

    /**
     * How far short of {@link ViewportGeometry.maxScrollTopPx} a compressed grid already shows the last
     * row flush with the bottom. The browser quantises `scrollTop` to device pixels and can stop short of
     * the arithmetic maximum (measured at 150%), and at k = 1.25 that pixel cut the last row by more than
     * one (ADR-0053).
     */
    static readonly endSlackPx = 2;

    /** The fixed height of every row (ADR-0013). */
    readonly rowHeightPx: number;
    /** The height the rows are painted in: what the Viewport has left once the Scrollbar Gutter and the
     * header band are taken out. */
    readonly viewportHeightPx: number;
    /** How many rows the scrollbar spans: the whole result, or one page under a pager (ADR-0015). */
    readonly totalRowCount: number;
    /** The true height of the rows: total rows × row height (ADR-0013). */
    readonly contentHeightPx: number;
    /** The scrollbar's length: the true height where it fits under the Layout Ceiling, the room less
     * {@link ViewportGeometry.layoutCeilingMarginPx} where it does not (ADR-0053). */
    readonly scrollHeightPx: number;
    /** k in `c(s) = s × k`: 1 where the true height fits, and above it the ratio that makes both ends
     * exact, `(H − V) / (S − V − endSlackPx)` (ADR-0053). */
    readonly compression: number;
    /** Whether scroll and content offsets differ at all (ADR-0053). */
    readonly isCompressed: boolean;

    readonly #scrollReachPx: number;

    /**
     * The arithmetic for rows of `rowHeightPx`, painted in `viewportHeightPx`, over `totalRowCount` rows,
     * where the rows' part of the scroll content may be laid out at most `scrollRoomPx` tall: the Layout
     * Ceiling the browser told, less whatever else stands in the same content. Unbounded when not given.
     * A true height that fits is used as it is; one that does not is compressed (ADR-0053). A true height
     * past {@link ViewportGeometry.maxScrollHeightPx} is refused whatever the room.
     */
    constructor({ rowHeightPx, viewportHeightPx, totalRowCount, scrollRoomPx = Number.POSITIVE_INFINITY }: {
        readonly rowHeightPx: number;
        readonly viewportHeightPx: number;
        readonly totalRowCount: number;
        readonly scrollRoomPx?: number;
    }) {
        finitePositive(rowHeightPx, 'rowHeightPx', 'RowHeight is a finite, positive number of pixels (ADR-0013).');
        finitePositive(viewportHeightPx, 'viewportHeightPx', 'ViewportHeight is a finite, positive number of pixels (ADR-0013).');
        wholeAtLeast(totalRowCount, 0, 'totalRowCount');
        if (Number.isNaN(scrollRoomPx) || scrollRoomPx <= 0) {
            throw new RangeError(
                `The room under the Layout Ceiling is a positive number of pixels, or unbounded (ADR-0053). (scrollRoomPx: ${scrollRoomPx})`);
        }
        const contentHeightPx = totalRowCount * rowHeightPx;
        if (contentHeightPx > ViewportGeometry.maxScrollHeightPx) {
            throw new Error(
                `${totalRowCount} rows at ${rowHeightPx}px need a scrollable area of ${contentHeightPx}px, past the `
                + `${ViewportGeometry.maxScrollHeightPx}px a browser can scroll: the rows beyond it could never be reached. `
                + 'Use a shorter row height, or page the result (ADR-0015).');
        }

        this.rowHeightPx = rowHeightPx;
        this.viewportHeightPx = viewportHeightPx;
        this.totalRowCount = totalRowCount;
        this.contentHeightPx = contentHeightPx;
        if (contentHeightPx <= scrollRoomPx) {
            this.scrollHeightPx = contentHeightPx;
            this.compression = 1;
            this.isCompressed = false;
            this.#scrollReachPx = 0;
            return;
        }

        // Compressed: the spacer stops a margin short of the ceiling, and k is chosen so both ends are
        // exact: s = 0 shows the first row, and the last row is flush with the bottom from endSlackPx short
        // of the largest s on.
        const spacer = scrollRoomPx - ViewportGeometry.layoutCeilingMarginPx;
        if (spacer - ViewportGeometry.endSlackPx <= viewportHeightPx) {
            throw new Error(
                `The browser lays out at most ${scrollRoomPx}px of rows here, which leaves nothing to scroll a `
                + `${viewportHeightPx}px Viewport through ${contentHeightPx}px of content (ADR-0053).`);
        }
        this.scrollHeightPx = spacer;
        this.#scrollReachPx = spacer - viewportHeightPx - ViewportGeometry.endSlackPx;
        this.compression = (contentHeightPx - viewportHeightPx) / this.#scrollReachPx;
        this.isCompressed = true;
    }

    /** How many rows one Viewport paints: what fits, plus the row straddling the bottom edge, which at
     * any offset that is not a multiple of the row height is half on screen. */
    get rowsPerViewport(): number {
        return Math.ceil(this.viewportHeightPx / this.rowHeightPx) + 1;
    }

    /** The furthest the browser scrolls the rows: the scrollbar's length less the readable height, or 0. */
    get maxScrollTopPx(): number {
        return Math.max(0, this.scrollHeightPx - this.viewportHeightPx);
    }

    /** The scroll offset from which the content offset is at its end: the maximum less
     * {@link ViewportGeometry.endSlackPx} when compressed, the maximum otherwise. */
    get scrollReachPx(): number {
        return this.isCompressed ? this.#scrollReachPx : this.maxScrollTopPx;
    }

    /** The content offset a scroll offset shows, `c(s) = s × k` (ADR-0053): the identity, unclamped,
     * when not compressed; compressed, the offset is clamped to the scroll range first, as the slice is. */
    contentOffsetAt(scrollTopPx: number): number {
        return this.isCompressed ? clamp(scrollTopPx, 0, this.#scrollReachPx) * this.compression : scrollTopPx;
    }

    /** The scroll offset that shows a content offset: the inverse of `contentOffsetAt`, unrounded. */
    scrollTopAt(contentOffsetPx: number): number {
        return this.isCompressed ? contentOffsetPx / this.compression : contentOffsetPx;
    }

    /** A scroll offset moved so that the content moves by `contentDeltaPx`: an edge auto-scroll tick or
     * a PageUp/PageDown step, counted in rows, which must move rows and not scrollbar pixels
     * (ADR-0008/0012/0053). Clamped to the scroll range. */
    scrollTopStepped(scrollTopPx: number, contentDeltaPx: number): number {
        return this.isCompressed
            ? clamp(this.scrollTopAt(this.contentOffsetAt(scrollTopPx) + contentDeltaPx), 0, this.maxScrollTopPx)
            : clamp(scrollTopPx + contentDeltaPx, 0, this.maxScrollTopPx);
    }

    /**
     * The rows a scroll offset puts on screen, or `undefined` when there are none to paint. The offset is
     * clamped, so a position past the end (the total shrank under the user, or the browser has not yet
     * clamped its own scrollTop) lands on the last full Viewport instead of painting nothing.
     */
    sliceAt(scrollTopPx: number): RowRange | undefined {
        finite(scrollTopPx, 'scrollTopPx', 'A scroll offset is a finite number of pixels.');
        if (this.totalRowCount === 0) {
            return undefined;
        }
        const perViewport = this.rowsPerViewport;
        const first = clamp(
            Math.floor(Math.max(this.contentOffsetAt(scrollTopPx), 0) / this.rowHeightPx),
            0,
            Math.max(0, this.totalRowCount - perViewport));
        return rowRange(first, Math.min(perViewport, this.totalRowCount - first));
    }

    /** Where a row sits in the content: its true offset, which is also where it sits inside the
     * scrollable area whenever the height is not compressed. */
    offsetPxOf(rowIndex: number): number {
        wholeAtLeast(rowIndex, 0, 'rowIndex');
        return rowIndex * this.rowHeightPx;
    }

    /**
     * Where a painted row is placed inside the scrollable area while the browser is scrolled to
     * `scrollTopPx`: `offsetPxOf` when not compressed; compressed, wherever puts it at `viewportTopPxOf`
     * once scrolled, so a compressed grid re-places its rows on every scroll (ADR-0053). Pass the offset
     * the browser reported, never the one asked for: `scrollTop` is quantised to device pixels.
     */
    paintedTopPxOf(rowIndex: number, scrollTopPx: number): number {
        wholeAtLeast(rowIndex, 0, 'rowIndex');
        return this.isCompressed
            ? (rowIndex * this.rowHeightPx) - this.contentOffsetAt(scrollTopPx) + scrollTopPx
            : rowIndex * this.rowHeightPx;
    }

    /** Where a row's top edge stands relative to the top of the readable area at a scroll offset:
     * `row × rowHeight − c(s)`, what popovers, edge bands and context menus are laid out from (ADR-0053). */
    viewportTopPxOf(rowIndex: number, scrollTopPx: number): number {
        wholeAtLeast(rowIndex, 0, 'rowIndex');
        return (rowIndex * this.rowHeightPx) - this.contentOffsetAt(scrollTopPx);
    }

    /**
     * The scroll offset that brings a row fully into view, moving as little as possible: "the Focus must
     * always be visible" (ADR-0012). A row already on screen returns the offset unchanged. Compressed, the
     * clamp is taken over content offsets and the answer rounded to a whole pixel towards showing the
     * whole row (ADR-0053).
     *
     * The header cancels out on this axis: it occupies the first row height of the content and covers
     * the first row height of the Viewport, so top-aligning row r is `r × rowHeight` exactly. A Pinned
     * Column covers the Viewport's edge without occupying anything ahead of the content, so do not read
     * one axis as the template for the other.
     */
    scrollTopToReveal(rowIndex: number, currentScrollTopPx: number): number {
        wholeAtLeast(rowIndex, 0, 'rowIndex');
        if (rowIndex >= this.totalRowCount) {
            throw new RangeError(`rowIndex is a row of the ${this.totalRowCount}, not ${rowIndex}.`);
        }
        finite(currentScrollTopPx, 'currentScrollTopPx', 'A scroll offset is a finite number of pixels.');

        const maxScrollTopPx = this.maxScrollTopPx;
        const current = clamp(currentScrollTopPx, 0, maxScrollTopPx);
        const alignTop = rowIndex * this.rowHeightPx;
        const alignBottom = alignTop + this.rowHeightPx - this.viewportHeightPx;
        if (!this.isCompressed) {
            // A Viewport shorter than one row cannot show a row whole; top-aligning shows where the value
            // starts.
            const offset = clamp(current, Math.min(alignBottom, alignTop), alignTop);
            return clamp(offset, 0, maxScrollTopPx);
        }

        // Compressed (ADR-0053): the same clamp, over content offsets, asked for back as s = c* / k. A row
        // already whole on screen leaves the offset where the browser put it. Otherwise the answer is a
        // whole CSS pixel, rounded towards showing the whole row (down when top-aligned, up when
        // bottom-aligned), so device-pixel quantisation costs under a pixel and a second reveal moves
        // nothing.
        const content = this.contentOffsetAt(current);
        const wanted = clamp(content, Math.min(alignBottom, alignTop), alignTop);
        if (wanted === content) {
            return current;
        }
        if (wanted >= this.contentHeightPx - this.viewportHeightPx) {
            return maxScrollTopPx;
        }
        if (wanted <= 0) {
            return 0;
        }
        const exact = this.scrollTopAt(wanted);
        const rounded = wanted < content ? Math.floor(exact) : Math.ceil(exact);
        return clamp(rounded, 0, maxScrollTopPx);
    }

    /**
     * Which row a pixel belongs to, or `undefined` when there are no rows: the inverse of `offsetPxOf`,
     * and the vertical half of turning a pointer position into a cell (ADR-0008). `contentYPx` is
     * measured from the top of the first row. A position outside the content is clamped rather than
     * refused: a drag that runs past the last row keeps extending to the last row.
     */
    rowAt(contentYPx: number): number | undefined {
        finite(contentYPx, 'contentYPx', 'A pointer position is a finite number of pixels.');
        if (this.totalRowCount === 0) {
            return undefined;
        }
        return clamp(Math.floor(contentYPx / this.rowHeightPx), 0, this.totalRowCount - 1);
    }

    /** Whether a move from one painted position to another is a fling: more than one Viewport at once, so
     * every row changes and placeholders are painted until scrolling settles (ADR-0003, ADR-0004). */
    isFling(paintedFirstRow: number, firstRow: number): boolean {
        return Math.abs(firstRow - paintedFirstRow) > this.rowsPerViewport;
    }
}
