// `GridSource.from`: every row is in hand, so the Window is the whole result and nothing is ever loading
// (ADR-0001). The reference implementation of what a Sort and a Filter mean (ADR-0023).
//
// Columns reach the source when the grid binds, and the declaration stays the grid's: until they arrive
// the Window is the input order, and a Sort or a Filter is refused, since it cannot come through the grid.
// A refused change leaves the source as it was: every change is computed first, and kept only once it
// is computed.

import type { QueryColumn } from '../query/columns';
import type { GridFilter } from '../query/filter';
import { applyQuery } from '../query/query-engine';
import type { SortSpec } from '../query/sort';
import { filtersEqual, filterWithout, snapshotFilter, dateIdentity } from './filters';
import type { DistinctValues, GridSource, GridSourceState } from './grid-source';
import { checkRange, type RowRange } from './row-range';

/** Past this many distinct values the answer is too many (ADR-0009), and the panel offers a condition,
 * Excel's own shape for a large domain. */
const distinctValueCap = 1000;

/** A Grid Source over rows held in memory: the counterpart of C#'s `InMemoryGridSource`. */
export class InMemoryGridSource<Row> implements GridSource<Row> {
    #rows: readonly Row[];
    readonly #listeners = new Set<() => void>();
    #columns: readonly QueryColumn<Row>[] | undefined;
    #state: GridSourceState<Row>;

    private constructor(rows: readonly Row[]) {
        // A copy: the Consumer changing the list it passed cannot move the Window under the grid, nor the
        // order the next sequence is compared with.
        this.#rows = Object.freeze([...rows]);
        this.#state = Object.freeze({
            window: Object.freeze([...rows]),
            windowStart: 0,
            totalCount: rows.length,
            loading: false,
            rowSequenceVersion: 0,
            sorts: Object.freeze([]),
            filter: undefined,
        });
    }

    /** A source over these rows, in this order until a Sort says otherwise. */
    static from<Row>(rows: readonly Row[]): InMemoryGridSource<Row> {
        return new InMemoryGridSource(rows);
    }

    /** Hears every change to the snapshot; the answer stops hearing. */
    subscribe(listener: () => void): () => void {
        this.#listeners.add(listener);
        return () => this.#listeners.delete(listener);
    }

    /** What the source holds now: the whole result, from its start. */
    snapshot(): GridSourceState<Row> {
        return this.#state;
    }

    /** Takes the grid's columns and requeries under the Filter and Sort in force. The same columns again
     * are no change; new ones are heard only when the result's sequence moved, since the grid that pushed
     * them already knows. */
    setColumns(columns: readonly QueryColumn<Row>[]): void {
        this.#commit(columns, this.#state.filter, this.#state.sorts);
    }

    /** Applies a Sort, moving the Row Sequence Version only when the result's sequence moved. */
    setSorts(sorts: readonly SortSpec[]): void {
        this.#commit(this.#requireColumns(), this.#state.filter, sorts);
    }

    /** Applies a Filter, or none, moving the Row Sequence Version only when the result's sequence moved. */
    setFilter(filter: GridFilter | undefined): void {
        this.#commit(this.#requireColumns(), filter, this.#state.sorts);
    }

    /**
     * The Consumer's half of an Edit Intent (ADR-0007): the edited row is replaced by a new object, since
     * identity, not a change in place, is what says a row changed (ADR-0003), and the result is requeried.
     * The Row Sequence Version moves only when the result's sequence did, so an ordinary edit keeps the
     * Selection. `row` is the object the grid painted, found by identity, never by value.
     */
    replaceRow(row: Row, replacement: Row): void {
        if (row === replacement) {
            throw new TypeError('The replacement is the row itself. A change made in place does not reach the screen: '
                + 'hand over a new object, since identity is what says a row changed (ADR-0003, ADR-0007).');
        }
        const index = this.#rows.indexOf(row);
        if (index < 0) {
            throw new TypeError('The row is not in this source. An Edit Intent carries the object the grid painted; '
                + 'another, or an earlier version, cannot be found in it (ADR-0007).');
        }
        const columns = this.#requireColumns();
        const rows = this.#rows.with(index, replacement);
        const next = applyQuery(rows, columns, this.#state.filter, this.#state.sorts);
        this.#rows = rows;
        const { window } = this.#state;
        // The edited row standing where its earlier version stood is the same sequence, not a reorder.
        const moved = next.length !== window.length
            || next.some((each, at) => each !== window[at] && !(each === replacement && window[at] === row));
        this.#publish({ window: next, moved, columns, filter: this.#state.filter, sorts: this.#state.sorts }, true);
    }

    /** Everything is in hand, so a range needs no answer; a range past the rows is no error, since a
     * request can cross a change to the data. */
    async needRange(range: RowRange): Promise<void> {
        checkRange(range);
    }

    /** The rows of the result in the range, as many of them as there are (ADR-0005). */
    async rows(range: RowRange, signal: AbortSignal): Promise<readonly Row[]> {
        checkRange(range);
        signal.throwIfAborted();
        return Object.freeze(this.#state.window.slice(range.start, range.start + range.count));
    }

    /** The reference semantics of the value list (ADR-0009): the column's distinct values under every
     * Filter but its own, in order of first appearance, a Blank as `null` (ADR-0023). */
    async distinctValues(column: string, signal: AbortSignal): Promise<DistinctValues> {
        signal.throwIfAborted();
        const columns = this.#requireColumns();
        const named = columns.find(each => each.name === column);
        if (named === undefined) throw new Error(`No column is named '${column}'.`);
        const others = filterWithout(this.#state.filter, column);
        const rows = others === undefined ? this.#rows : applyQuery(this.#rows, columns, others, undefined);
        const seen = new Set<unknown>();
        const values: unknown[] = [];
        for (const row of rows) {
            signal.throwIfAborted();
            const read = named.value(row);
            const value = read === undefined ? null : read;
            // A date is one value however many objects hold it, as a C# DateTime is.
            const identity = dateIdentity(value) ?? value;
            if (seen.has(identity)) continue;
            seen.add(identity);
            values.push(value);
            if (values.length > distinctValueCap) return Object.freeze({ kind: 'tooMany' });
        }
        return Object.freeze({ kind: 'values', values: Object.freeze(values) });
    }

    #requireColumns(): readonly QueryColumn<Row>[] {
        if (this.#columns === undefined) {
            throw new Error('Columns have not been received yet; call setColumns first. '
                + 'Bound to a grid this cannot happen: the grid pushes its columns when it binds.');
        }
        return this.#columns;
    }

    #commit(columns: readonly QueryColumn<Row>[], filter: GridFilter | undefined, sorts: readonly SortSpec[]): void {
        // The same Query pushed again changes nothing and is not heard: an adapter that pushes on every
        // render and renders on every change would otherwise never stop (ADR-0023).
        const sortsChanged = !sortsEqual(sorts, this.#state.sorts);
        const filterChanged = !filtersEqual(filter, this.#state.filter);
        const columnsChanged = this.#columns === undefined || !columnsEqual(columns, this.#columns);
        if (!sortsChanged && !filterChanged && !columnsChanged) return;
        // Computed first: a Query the engine refuses leaves the source as it was.
        const next = applyQuery(this.#rows, columns, filter, sorts);
        const { window } = this.#state;
        const moved = next.length !== window.length || next.some((row, at) => row !== window[at]);
        this.#publish({ window: next, moved, columns, filter, sorts }, moved || sortsChanged || filterChanged);
    }

    #publish(
        next: { window: Row[]; moved: boolean; columns: readonly QueryColumn<Row>[]; filter: GridFilter | undefined; sorts: readonly SortSpec[] },
        heard: boolean,
    ): void {
        // Copies of what was applied, so the Consumer's own objects behind them can change without reaching
        // the source (ADR-0023).
        this.#columns = Object.freeze([...next.columns]);
        this.#state = Object.freeze({
            window: Object.freeze(next.window),
            windowStart: 0,
            totalCount: next.window.length,
            loading: false,
            rowSequenceVersion: this.#state.rowSequenceVersion + (next.moved ? 1 : 0),
            sorts: Object.freeze(next.sorts.map(sort => Object.freeze({ ...sort }))),
            filter: snapshotFilter(next.filter),
        });
        // A copy, so a listener that subscribes another meanwhile is heard from the next change on.
        if (heard) for (const listener of Array.from(this.#listeners)) listener();
    }
}

function sortsEqual(a: readonly SortSpec[], b: readonly SortSpec[]): boolean {
    return a.length === b.length && a.every((sort, at) => {
        const other = b[at];
        return other !== undefined && sort.column === other.column && sort.direction === other.direction;
    });
}

/** Two column lists are the same columns when each says the same: a grid that keeps its column objects
 * pushes the same ones, and one that rebuilds them from the same declaration pushes equal ones. */
function columnsEqual<Row>(a: readonly QueryColumn<Row>[], b: readonly QueryColumn<Row>[]): boolean {
    return a.length === b.length && a.every((column, at) => {
        const other = b[at];
        return other !== undefined && (column === other || (column.name === other.name && column.type === other.type
            && column.value === other.value && column.queryable === other.queryable && column.dateType === other.dateType));
    });
}
