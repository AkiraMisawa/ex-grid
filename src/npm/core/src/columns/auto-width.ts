import type { ColumnWidthSpec } from './column-width';

/**
 * The current computed width of an Auto column. It only ever grows (ADR-0016): a width that moved back
 * down as screens of short values scrolled past would make the columns judder, so not narrowing is what
 * makes it settle. Grow-only is structural: the only transition is {@link AutoWidth.observe}, which takes
 * the maximum itself. Narrowing happens only through the spec's explicit Size to fit.
 *
 * The spec lives inside the tracker: a changed column declaration means a fresh tracker, measured again
 * from the next Window.
 */
export class AutoWidth {
    readonly #spec: ColumnWidthSpec;
    /** The width the column stands at now: MinWidth until something is observed, then the widest
     * observation clamped to the spec's bounds. It never decreases. */
    readonly currentPx: number;

    private constructor(spec: ColumnWidthSpec, currentPx: number) {
        this.#spec = spec;
        this.currentPx = currentPx;
    }

    /** A fresh tracker for a column declared with `spec`, standing at its MinWidth until something is
     * observed: the first Window's values grow it from there (ADR-0016). */
    static of(spec: ColumnWidthSpec): AutoWidth {
        return new AutoWidth(spec, spec.minWidthPx);
    }

    /**
     * Folds in the full cell width one value requires: `estimatePx`'s output, padding included, in the unit
     * of the resolved column width. Not the content width, which would make a column exactly twice the
     * padding too narrow and hash the very value it was sized to. Clamped to the spec's bounds; monotone and
     * order-independent, so the same observations settle at the same width in any order.
     */
    observe(requiredWidthPx: number): AutoWidth {
        if (!Number.isFinite(requiredWidthPx) || requiredWidthPx < 0) {
            throw new RangeError(`A required width is a finite, non-negative number of pixels. (requiredWidthPx: ${requiredWidthPx})`);
        }
        return new AutoWidth(this.#spec, Math.max(this.currentPx, this.#spec.clamp(requiredWidthPx)));
    }
}
