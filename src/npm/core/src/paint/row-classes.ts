import { elementAt } from '../must';

// The class vocabulary a row is painted with, one string per combination: the row's counterpart to
// cell-classes.ts, and like it the seam a replacement Chrome and a theme paint against (ADR-0010).

/**
 * What a row represents: the role it plays in the result, not its depth in one (ADR-0024). A group row is
 * an ordinary row of ordinary height that looks different, so the row height stays fixed (ADR-0013). The
 * grid neither computes the aggregate nor decides what is grouped (ADR-0001); it is told which role each
 * row plays and paints it.
 * - `detail`: an ordinary row of the result;
 * - `group`: a row standing for a group of rows, whose expansion is the Consumer's state;
 * - `total`: a subtotal or a grand total, computed by the Consumer.
 */
export type RowKind = 'detail' | 'group' | 'total';

/** What a row's class attribute is composed from. */
export interface RowClassOptions {
    /** The row's role. `detail` adds nothing, so an ordinary row is painted as it was before Row Kind. */
    readonly kind: RowKind;
    /** Whether the row is a Placeholder. A Placeholder keeps its Kind: a group row that blinked into a
     * detail row mid-fling would be the landmark lying about itself (ADR-0004). */
    readonly placeholder: boolean;
    /** The Row Stripe the grid decided from the row's absolute position (ADR-0038). False unless given. */
    readonly stripe?: boolean;
}

const kinds: readonly RowKind[] = ['detail', 'group', 'total'];
const kindClass: Readonly<Record<RowKind, string>> = { detail: '', group: ' ex-row-group', total: ' ex-row-total' };

let composed: readonly string[] | undefined;

/** Every combination, composed on first asking: nothing runs at module load (`sideEffects: false`). */
function table(): readonly string[] {
    if (composed !== undefined) return composed;
    composed = Object.freeze(kinds.flatMap(kind => [
        `ex-row${kindClass[kind]}`,
        `ex-row${kindClass[kind]} ex-row-stripe`,
        `ex-row ex-placeholder${kindClass[kind]}`,
        `ex-row ex-placeholder${kindClass[kind]} ex-row-stripe`,
    ]));
    return composed;
}

/** The full class attribute for one row: one string per combination, never composed on the render path
 * (ADR-0027 P5). A kind nobody declared is refused rather than painted as a detail row, for the reason an
 * undeclared Cell State is (ADR-0006 / ADR-0024). */
export function rowClasses(options: RowClassOptions): string {
    const { kind, placeholder, stripe = false } = options;
    const place = kinds.indexOf(kind);
    if (place < 0) throw new RangeError(`kind is one of ${kinds.join(', ')}, not ${kind}.`);
    return elementAt(table(), place * 4 + (placeholder ? 2 : 0) + (stripe ? 1 : 0));
}
