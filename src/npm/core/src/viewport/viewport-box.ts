/**
 * What is actually visible inside the declared Viewport: the outer size the Consumer asked for, less
 * the Scrollbar Gutter the browser took out of it (ADR-0013).
 *
 * A classic scrollbar is drawn inside the box the element declares, while overlay scrollbars take
 * nothing. The gutter cannot be assumed, computed or measured once: it is 0 until content overflows and
 * differs per platform and setting, so it is observed (the browser reports it whenever it changes,
 * ADR-0021), and this is where the subtraction happens, once, for both axes. With a gutter of 0 the
 * results are bit for bit the declared size: `x - 0` is `x` for every finite `x`.
 */
export class ViewportBox {
    /** The width the Consumer declared: the element's outer size, scrollbar included (ADR-0013). */
    readonly outerWidthPx: number;
    /** The height the Consumer declared: the element's outer size, header and scrollbar included. */
    readonly outerHeightPx: number;
    /** How much of the width a vertical scrollbar occupies. */
    readonly gutterWidthPx: number;
    /** How much of the height a horizontal scrollbar occupies. */
    readonly gutterHeightPx: number;

    constructor(options: {
        readonly outerWidthPx: number;
        readonly outerHeightPx: number;
        readonly gutterWidthPx: number;
        readonly gutterHeightPx: number;
    }) {
        check(options.outerWidthPx, 'outerWidthPx', options.gutterWidthPx, 'gutterWidthPx', 'wide', 'vertical');
        check(options.outerHeightPx, 'outerHeightPx', options.gutterHeightPx, 'gutterHeightPx', 'tall', 'horizontal');
        this.outerWidthPx = options.outerWidthPx;
        this.outerHeightPx = options.outerHeightPx;
        this.gutterWidthPx = options.gutterWidthPx;
        this.gutterHeightPx = options.gutterHeightPx;
    }

    /** A box with no scrollbar taken out of it: what overlay scrollbars report, and what the grid
     * assumes until the browser says otherwise. */
    static declared(outerWidthPx: number, outerHeightPx: number): ViewportBox {
        return new ViewportBox({ outerWidthPx, outerHeightPx, gutterWidthPx: 0, gutterHeightPx: 0 });
    }

    /** One axis alone, checked and subtracted: what a grid whose other axis stretches needs (ADR-0028). */
    static visibleWidthOf(outerWidthPx: number, gutterWidthPx: number): number {
        check(outerWidthPx, 'outerWidthPx', gutterWidthPx, 'gutterWidthPx', 'wide', 'vertical');
        return outerWidthPx - gutterWidthPx;
    }

    /** The height half of {@link ViewportBox.visibleWidthOf}. */
    static visibleHeightOf(outerHeightPx: number, gutterHeightPx: number): number {
        check(outerHeightPx, 'outerHeightPx', gutterHeightPx, 'gutterHeightPx', 'tall', 'horizontal');
        return outerHeightPx - gutterHeightPx;
    }

    /** The width the columns actually have: the browser's `clientWidth`, without a layout read. */
    get visibleWidthPx(): number {
        return this.outerWidthPx - this.gutterWidthPx;
    }

    /** The height the header and the rows actually have. */
    get visibleHeightPx(): number {
        return this.outerHeightPx - this.gutterHeightPx;
    }

    /** The same box with another gutter: how an observation is applied, the declared size untouched. */
    withGutter(gutterWidthPx: number, gutterHeightPx: number): ViewportBox {
        return new ViewportBox({ outerWidthPx: this.outerWidthPx, outerHeightPx: this.outerHeightPx, gutterWidthPx, gutterHeightPx });
    }
}

// Both axes are checked alike, and each refusal names the gutter rather than the remainder: a Consumer
// told "the width is not positive" about a width it set to 12 would have nothing to go on, because the
// number it wrote is not the number that went wrong.
function check(outerPx: number, outerName: string, gutterPx: number, gutterName: string, adjective: string, bar: string): void {
    if (!Number.isFinite(outerPx) || outerPx <= 0) {
        throw new RangeError(`The Viewport is a finite, positive number of pixels ${adjective} (ADR-0013). (${outerName}: ${outerPx})`);
    }
    if (!Number.isFinite(gutterPx) || gutterPx < 0) {
        throw new RangeError(
            `A Scrollbar Gutter is a finite, non-negative number of pixels: the ${bar} scrollbar either takes a strip out `
            + `of the declared box or takes nothing (ADR-0013). (${gutterName}: ${gutterPx})`);
    }
    if (gutterPx >= outerPx) {
        throw new RangeError(
            `The ${bar} scrollbar takes ${gutterPx}px out of a Viewport only ${outerPx}px ${adjective}, leaving nothing to `
            + `paint in. Declare a Viewport large enough to hold its own scrollbars (ADR-0013). (${gutterName}: ${gutterPx})`);
    }
}
