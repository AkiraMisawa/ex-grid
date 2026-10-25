// The contract between a grid and where its rows come from (ADR-0001, ADR-0350 section 16). The grid neither
// holds nor executes: it tells a Grid Source the Columns, the Sort and the Filter, and reads back a
// Window. A Source is read the way React's `useSyncExternalStore` reads a store: `subscribe` to hear that
// something changed, `snapshot` to read it, the same object until it changes.

import type { QueryColumn } from '../query/columns';
import type { GridFilter } from '../query/filter';
import type { SortSpec } from '../query/sort';
import { InMemoryGridSource } from './in-memory-source';
import type { RowRange } from './row-range';

/** What a Grid Source holds at one moment, frozen. A new object whenever any of it changes, and the same
 * one until then. */
export interface GridSourceState<Row> {
    /** The rows in hand, from `windowStart` of the result on. */
    readonly window: readonly Row[];
    /** Where the Window starts in the result. */
    readonly windowStart: number;
    /** The number of rows the result holds, or `undefined` while it is not known. */
    readonly totalCount: number | undefined;
    /** Whether an answer is in flight. */
    readonly loading: boolean;
    /** Names the sequence of the rows, not their values: it moves when, and only when, the result's order
     * or membership moved (ADR-0011). */
    readonly rowSequenceVersion: number;
    /** The Sort in force, outermost first; empty for the source's own order. */
    readonly sorts: readonly SortSpec[];
    /** The Filter in force, or `undefined` for none. */
    readonly filter: GridFilter | undefined;
}

/** The distinct values a column holds under every Filter but its own (ADR-0009), in order of first
 * appearance, a Blank as `null`; or too many to list, past which the panel offers a condition instead. */
export type DistinctValues =
    | { readonly kind: 'values'; readonly values: readonly unknown[] }
    | { readonly kind: 'tooMany' };

/** Where a grid's rows come from. */
export interface GridSource<Row> {
    /** Hears every change to the snapshot; the answer stops hearing. */
    subscribe(listener: () => void): () => void;
    /** What the source holds now. */
    snapshot(): GridSourceState<Row>;
    /** Takes the grid's columns, which every Sort and Filter names. */
    setColumns(columns: readonly QueryColumn<Row>[]): void;
    /** Applies a Sort. */
    setSorts(sorts: readonly SortSpec[]): void;
    /** Applies a Filter, or none. */
    setFilter(filter: GridFilter | undefined): void;
    /** Says the grid is about to show these rows of the result. */
    needRange(range: RowRange): Promise<void>;
    /** The rows of the result in the range, for a copy that reaches past the Window (ADR-0005). */
    rows(range: RowRange, signal: AbortSignal): Promise<readonly Row[]>;
    /** A column's distinct values, for the filter's value list (ADR-0009). */
    distinctValues(column: string, signal: AbortSignal): Promise<DistinctValues>;
}

/** Makes Grid Sources. */
export const GridSource = Object.freeze({
    /** A source over rows held in memory, the reference implementation of a Sort and a Filter (ADR-0001). */
    from<Row>(rows: readonly Row[]): InMemoryGridSource<Row> {
        return InMemoryGridSource.from(rows);
    },
});
