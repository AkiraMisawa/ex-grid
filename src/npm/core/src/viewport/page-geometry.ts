import { wholeAtLeast } from './checks';
import { rowRange, type RowRange } from './row-range';
import { ViewportGeometry } from './viewport-geometry';

/**
 * The vertical arithmetic of one page under a pager (ADR-0015): {@link ViewportGeometry} over the page's rows
 * alone, so the scrollbar spans the page and the page's edge is where a drag stops, answered in positions in the
 * whole result. A row is told and answered as the result counts it, and the page-local row the arithmetic runs
 * over is that less the page's first row.
 */
export class PageGeometry extends ViewportGeometry {
    /** The position in the whole result of the page's first row. */
    readonly firstRow: number;

    /** The arithmetic of the page that starts at `firstRow` and holds `totalRowCount` rows, as
     * {@link ViewportGeometry}'s constructor takes the rest. */
    constructor(firstRow: number, options: ConstructorParameters<typeof ViewportGeometry>[0]) {
        super(options);
        wholeAtLeast(firstRow, 0, 'firstRow');
        this.firstRow = firstRow;
    }

    override sliceAt(scrollTopPx: number): RowRange | undefined {
        const local = super.sliceAt(scrollTopPx);
        return local === undefined ? undefined : rowRange(this.firstRow + local.start, local.count);
    }

    override offsetPxOf(rowIndex: number): number {
        return super.offsetPxOf(this.#local(rowIndex));
    }

    override paintedTopPxOf(rowIndex: number, scrollTopPx: number): number {
        return super.paintedTopPxOf(this.#local(rowIndex), scrollTopPx);
    }

    override viewportTopPxOf(rowIndex: number, scrollTopPx: number): number {
        return super.viewportTopPxOf(this.#local(rowIndex), scrollTopPx);
    }

    override scrollTopToReveal(rowIndex: number, currentScrollTopPx: number): number {
        return super.scrollTopToReveal(this.#local(rowIndex), currentScrollTopPx);
    }

    override rowAt(contentYPx: number): number | undefined {
        const local = super.rowAt(contentYPx);
        return local === undefined ? undefined : this.firstRow + local;
    }

    override isFling(paintedFirstRow: number, firstRow: number): boolean {
        return super.isFling(paintedFirstRow - this.firstRow, firstRow - this.firstRow);
    }

    // The page-local row: the base refuses one before the page by name, and answers one past it as it answers
    // a row past the end, below the rows.
    #local(rowIndex: number): number {
        return rowIndex - this.firstRow;
    }
}
