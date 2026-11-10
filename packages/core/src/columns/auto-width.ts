import { checkedRequiredPx, type ColumnWidthSpec } from './column-width';

/**
 * The current computed width of an Auto column. What its values taught it only ever grows (ADR-0016): a
 * width that moved back down as screens of short values scrolled past would make the columns judder, so not
 * narrowing is what makes it settle. Grow-only is structural: the only transition a value makes is
 * {@link AutoWidth.observe}, which takes the maximum itself. Below that, a column narrows only through the
 * spec's explicit Size to fit.
 *
 * What the column's header needs is held apart from what its values taught it (ADR-0500): a header's need is
 * a fact of how the header is drawn, never an observation, so {@link AutoWidth.underHeader} replaces it,
 * and the values' part stands. A column its header decides follows the header; one its values decide does
 * not move.
 *
 * The spec lives inside the tracker: a changed column declaration means a fresh tracker, measured again
 * from the next Window.
 */
export class AutoWidth {
    readonly #spec: ColumnWidthSpec;
    /** What the header needs, clamped to the spec's bounds: MinWidth until one is given. */
    readonly #headerPx: number;
    /** The widest value observed, clamped to the spec's bounds: MinWidth until one is. It never decreases. */
    readonly #valuesPx: number;
    /** The width the column stands at now: MinWidth until something is observed, then the wider of what its
     * header needs and its widest observation, each clamped to the spec's bounds. Observations never decrease it. */
    readonly currentPx: number;

    private constructor(spec: ColumnWidthSpec, headerPx: number, valuesPx: number) {
        this.#spec = spec;
        this.#headerPx = headerPx;
        this.#valuesPx = valuesPx;
        this.currentPx = Math.max(headerPx, valuesPx);
    }

    /** A fresh tracker for a column declared with `spec`, standing at its MinWidth until something is
     * observed: the first Window's values grow it from there (ADR-0016). */
    static of(spec: ColumnWidthSpec): AutoWidth {
        return new AutoWidth(spec, spec.minWidthPx, spec.minWidthPx);
    }

    /** Whether `other` tracks the same declaration, under a header that needs the same, having learned the same
     * from its values: two that stand at one width for different reasons are not equal, since another header
     * would part them. */
    equals(other: AutoWidth): boolean {
        return other instanceof AutoWidth && this.#spec.equals(other.#spec) && this.#headerPx === other.#headerPx && this.#valuesPx === other.#valuesPx;
    }

    /**
     * Folds in the full cell width one value requires: `estimatePx`'s output, padding included, in the unit
     * of the resolved column width. Not the content width, which would make a column exactly twice the
     * padding too narrow and hash the very value it was sized to. Clamped to the spec's bounds; monotone and
     * order-independent, so the same observations settle at the same width in any order.
     */
    observe(requiredWidthPx: number): AutoWidth {
        return new AutoWidth(this.#spec, this.#headerPx, Math.max(this.#valuesPx, this.#spec.clamp(checkedRequiredPx(requiredWidthPx))));
    }

    /**
     * The same tracker under a header that needs `requiredWidthPx`, in place of what its header needed before
     * (ADR-0500): the one way an Auto width comes down without a Size to fit, and only as far as its values let
     * it. Every observation made stands.
     */
    underHeader(requiredWidthPx: number): AutoWidth {
        return new AutoWidth(this.#spec, this.#spec.clamp(checkedRequiredPx(requiredWidthPx)), this.#valuesPx);
    }
}
