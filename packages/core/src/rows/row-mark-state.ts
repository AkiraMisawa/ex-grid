// The Row Marks of a fetching source as the steps that made them (ADR-0043), the counterpart of C#'s
// RowMarkState: keys and snapshots, never positions, since a position names another row after a Sort, and a key
// that no longer exists fails loudly instead. What a Consumer sends to its server to count the marks or to run
// an action over them.

import type { GridFilter } from '../query/filter';
import { checkedKey, type RowKey } from '../source/row-key';

/** "The result under `filter` as it stood at `asOf`": what a header press marks on a fetching source
 * (ADR-0043). `asOf` is the server's own marker, a watermark, a timestamp or a version: whatever lets it say
 * later which rows belonged. */
export interface RowMarkSnapshot {
    /** The Filter in force when the header was pressed, or `undefined` for none. */
    readonly filter: GridFilter | undefined;
    /** The server's marker for that moment. */
    readonly asOf: unknown;
}

/**
 * One step of a `RowMarkState`: a later step covering a row overrides every earlier one.
 * - `oneKey`: one row, by the key the Consumer's adapter reads from it;
 * - `allOf`: every row of a snapshot of the result.
 */
export type RowMarkStep =
    | { readonly kind: 'oneKey'; readonly key: RowKey; readonly marked: boolean }
    | { readonly kind: 'allOf'; readonly snapshot: RowMarkSnapshot; readonly marked: boolean };

/** A key's own step and where it stands, and every snapshot with where it stands: what `isMarked` reads. */
interface StateIndex {
    readonly keys: ReadonlyMap<RowKey, { readonly at: number; readonly marked: boolean }>;
    readonly snapshots: readonly { readonly at: number; readonly step: Extract<RowMarkStep, { kind: 'allOf' }> }[];
}

// Built once per state and held beside it, so a state stays its steps and nothing else.
const indexes = new WeakMap<RowMarkState, StateIndex>();

/** The Row Marks of a fetching source as the steps that made them, oldest first (ADR-0043). Only a key's latest
 * step is kept. */
export class RowMarkState {
    /** No marks. */
    static readonly empty: RowMarkState = new RowMarkState([]);

    /** The steps, oldest first. */
    readonly steps: readonly RowMarkStep[];

    /** The steps, oldest first, taken as a frozen copy. */
    constructor(steps: readonly RowMarkStep[]) {
        const given: unknown = steps;
        if (!Array.isArray(given)) throw new TypeError('A Row Mark state is a list of steps, oldest first.');
        this.steps = Object.freeze([...steps]);
        Object.freeze(this);
    }

    /**
     * Whether the row with this key is marked: the latest step that covers it decides, and a row no step covers
     * is unmarked. The one evaluation the source, a server counting and an action resolving the marks all
     * share, so the three cannot disagree about what is marked. `belongsTo` says whether the row belonged to a
     * snapshot.
     */
    isMarked(key: RowKey, belongsTo: (snapshot: RowMarkSnapshot) => boolean): boolean {
        const checked = checkedKey(key, 'The row asked about');
        // Asked once per row by whoever counts, so it must not walk every step: the key's own step is looked up,
        // and only the snapshots taken after it are tried.
        const index = indexOf(this);
        const own = index.keys.get(checked);
        const at = own === undefined ? -1 : own.at;
        for (let i = index.snapshots.length - 1; i >= 0; i--) {
            const snapshot = index.snapshots[i];
            if (snapshot === undefined || snapshot.at <= at) break;
            if (belongsTo(snapshot.step.snapshot)) return snapshot.step.marked;
        }
        return own !== undefined && own.marked;
    }
}

function indexOf(state: RowMarkState): StateIndex {
    const held = indexes.get(state);
    if (held !== undefined) return held;
    const keys = new Map<RowKey, { readonly at: number; readonly marked: boolean }>();
    const snapshots: { readonly at: number; readonly step: Extract<RowMarkStep, { kind: 'allOf' }> }[] = [];
    state.steps.forEach((step, at) => {
        if (step.kind === 'oneKey') keys.set(step.key, { at, marked: step.marked });
        else snapshots.push({ at, step });
    });
    const index = { keys, snapshots };
    indexes.set(state, index);
    return index;
}
