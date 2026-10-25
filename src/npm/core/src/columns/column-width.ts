import type { AutoWidth } from './auto-width';

/**
 * A column's declared width: Auto (computed from content) or a Fixed number of pixels (ADR-0016). A width
 * is View State, which a Consumer persists: Auto as the intent, never the width an Auto column was painted
 * at, and a Fixed width as its pixels.
 */
export class ColumnWidth {
    /** Computed from content, and never narrowing on its own (ADR-0016). */
    static readonly auto: ColumnWidth = new ColumnWidth(0);

    // 0 is Auto; a Fixed width is always positive.
    readonly #px: number;

    private constructor(px: number) {
        this.#px = px;
    }

    /** A fixed number of pixels, finite and positive: declared, or the user's intent after a drag or Size to
     * fit (ADR-0016). */
    static fixed(px: number): ColumnWidth {
        if (!Number.isFinite(px) || px <= 0) {
            throw new RangeError(`A Fixed width is a finite, positive number of pixels. (px: ${px})`);
        }
        return new ColumnWidth(px);
    }

    /** Whether this is an Auto width. */
    get isAuto(): boolean {
        return this.#px === 0;
    }

    /** The Fixed width in pixels. Throws when Auto: the grid resolves an Auto width from the values it has
     * painted, and it is never read from here (ADR-0016). */
    get fixedPx(): number {
        if (this.isAuto) {
            throw new Error('An Auto width has no fixed pixel value; the grid resolves it from the values it has painted (ADR-0016).');
        }
        return this.#px;
    }

    /** Whether `other` is the same width: both Auto, or Fixed at the same pixels. */
    equals(other: ColumnWidth): boolean {
        return other instanceof ColumnWidth && this.#px === other.#px;
    }

    /** `Auto` or `Fixed(120px)`, for a message or a log. */
    toString(): string {
        return this.isAuto ? 'Auto' : `Fixed(${this.#px}px)`;
    }
}

/**
 * The width declaration a column carries: Auto or Fixed, with a {@link ColumnWidthSpec.minWidthPx} under
 * everything and a {@link ColumnWidthSpec.maxWidthPx} over what the grid computes (ADR-0016). MaxWidth is
 * what gives an Auto column's `####` meaning: without an upper bound the column would keep growing and
 * overflow could never occur. It does not bound a Fixed width, which is the user's: a drag goes past it,
 * and the Consumer records the drag as a Fixed width. A Fixed width below MinWidth is refused by the column
 * it is given to, which can say which column it is (FN-12).
 */
export class ColumnWidthSpec {
    /** The MinWidth when none is declared: three `#` glyphs at typical metrics (ADR-0016). */
    static readonly defaultMinWidthPx = 40;

    /** The MaxWidth when none is declared: roughly 48 digits at typical metrics, so an untouched Auto column
     * effectively never hashes (ADR-0016). */
    static readonly defaultMaxWidthPx = 400;

    /** The declared width: Auto or Fixed. */
    readonly width: ColumnWidth;
    /** Also the lower bound for dragging: a column cannot be crushed until it disappears (ADR-0016). */
    readonly minWidthPx: number;
    /** The upper bound on what the grid computes, the Auto width and Size to fit. It does not stop a
     * user's drag, and it does not bound a Fixed width (ADR-0016). */
    readonly maxWidthPx: number;

    /** A width with its bounds, checked here. A Fixed width outside them is not this spec's to refuse
     * (FN-12). */
    constructor(options: { readonly width: ColumnWidth; readonly minWidthPx?: number; readonly maxWidthPx?: number }) {
        const {
            width,
            minWidthPx = ColumnWidthSpec.defaultMinWidthPx,
            maxWidthPx = ColumnWidthSpec.defaultMaxWidthPx,
        } = options;
        if (!(width instanceof ColumnWidth)) {
            throw new TypeError(`A width is a ColumnWidth: ColumnWidth.auto or ColumnWidth.fixed(px). (width: ${String(width)})`);
        }
        if (!Number.isFinite(minWidthPx) || minWidthPx <= 0) {
            throw new RangeError(`MinWidth is a finite, positive number of pixels. (minWidthPx: ${minWidthPx})`);
        }
        if (!Number.isFinite(maxWidthPx) || maxWidthPx < minWidthPx) {
            throw new RangeError(`MaxWidth is finite and at least MinWidth. (maxWidthPx: ${maxWidthPx})`);
        }
        this.width = width;
        this.minWidthPx = minWidthPx;
        this.maxWidthPx = maxWidthPx;
    }

    /** Whether `other` declares the same width and bounds. A changed declaration means a fresh Auto
     * tracker, so an equal one must not be mistaken for a change (ADR-0016). */
    equals(other: ColumnWidthSpec): boolean {
        return other instanceof ColumnWidthSpec && this.width.equals(other.width)
            && this.minWidthPx === other.minWidthPx && this.maxWidthPx === other.maxWidthPx;
    }

    /** A width brought within [MinWidth, MaxWidth]. @internal */
    clamp(px: number): number {
        return Math.min(Math.max(px, this.minWidthPx), this.maxWidthPx);
    }

    /**
     * Size to fit: fixes the width at the content of this moment, clamped, an approximation over the
     * fetched rows (ADR-0016). Takes the widest value's full required cell width (`estimatePx`, padding
     * included: the unit {@link AutoWidth.observe} takes). Always Fixed, and it may be narrower than the
     * current width: the only way an Auto column narrows.
     *
     * @internal
     */
    sizeToFit(requiredWidthPx: number): ColumnWidth {
        if (!Number.isFinite(requiredWidthPx) || requiredWidthPx < 0) {
            throw new RangeError(`A required width is a finite, non-negative number of pixels. (requiredWidthPx: ${requiredWidthPx})`);
        }
        return ColumnWidth.fixed(this.clamp(requiredWidthPx));
    }

    /** The one place the Auto-or-Fixed branch lives, so no caller derives which width applies (ADR-0016).
     * @internal */
    resolveWidthPx(current: AutoWidth): number {
        return this.width.isAuto ? current.currentPx : this.width.fixedPx;
    }
}
