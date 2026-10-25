// `GridSource.from`: every row is in hand, so the Window is the whole result and nothing is ever loading
// (ADR-0001). The reference implementation of what a Sort and a Filter mean (ADR-0023).
//
// Columns reach the source when the grid binds, and the declaration stays the grid's: until they arrive
// the Window is the input order, and a Sort or a Filter is refused, since it cannot come through the grid.
// A refused change leaves the source as it was: every change is computed first, and kept only once it
// is computed.
//
// Given a Row Key, it takes live data (ADR-0141): a Change Batch through `apply`, or a whole new list paired
// by key through `replaceAll`. The changes are gathered on the source's clock and reach the grid at most
// once every `gatherInterval`, from the newest version, never half a batch; the result is brought up to
// date incrementally, equal to a whole requery; the Row Sequence Version moves exactly when the result's
// sequence moved; and the source names its rows by the key and vouches that no Window holds a row twice.
// Without a key it is the source it always was.

import { textOf } from '../cells/display-text';
import { defined, itemAt } from '../must';
import type { QueryColumn } from '../query/columns';
import { dateIdentity } from '../query/dates';
import type { GridFilter } from '../query/filter';
import { applyQuery, queryPositions } from '../query/query-engine';
import type { SortSpec } from '../query/sort';
import { rowRange, type RowRange } from '../viewport/row-range';
import type { GridChangeBatch } from './change-batch';
import { ChangeGatherer } from './change-gatherer';
import { filtersEqual, filterWithout, snapshotFilter } from './filters';
import { systemClock, type GridClock } from './grid-clock';
import type { DistinctValues, GridSource, GridSourceState } from './grid-source';
import { CellChangeTimes, defaultChangeTimesKeptFor, type CellChangedAt, type ChangedPair } from './cell-change-times';
import { InMemoryRowMarks } from './in-memory-row-marks';
import { KeyedRows } from './keyed-rows';
import { liveRequery, type LiveRequeryOutcome, type RowChange } from './live-requery';
import { checkedKey, quoted, type RowKey, type RowKeyOf } from './row-key';

/** Past this many distinct values the answer is too many (ADR-0009), and the panel offers a condition,
 * Excel's own shape for a large domain. */
const distinctValueCap = 1000;

/** How `GridSource.from` takes its rows. */
export interface InMemorySourceOptions<Row> {
    /** The Row Key (ADR-0140, ADR-0141): given one, the source takes live data, and pairs a row's versions by
     * it. Every row's key is checked when the source is made, and a repeated key, or one that names no row,
     * is refused by name. */
    readonly rowKey?: RowKeyOf<Row>;
    /** The clock live changes are gathered by; the browser's unless given. Read only under a Row Key. */
    readonly clock?: GridClock;
}

/** The version of a row the result was last computed from, if one was held then. */
type Before<Row> = { readonly row: Row; readonly ordinal: number } | undefined;

/** A row changed since the Window was last computed: the version it was computed from, and, after the
 * Consumer's own edit (`replaceRow`), the version just before it, which the Change Highlight compares up
 * to, so the edit marks nothing of its own. */
interface Pending<Row> {
    readonly before: Before<Row>;
    userEdit: { readonly before: Row } | undefined;
}

/** What a batch or a list does, checked whole before any of it is taken in. A list that puts the rows in
 * another order than a batch would also carries its keys in its order (D7). */
interface ChangePlan<Row> {
    readonly added: readonly (readonly [RowKey, Row])[];
    readonly changed: readonly (readonly [RowKey, Row])[];
    readonly removed: readonly RowKey[];
    readonly order: readonly RowKey[] | undefined;
}

/** A Query as the source keeps it: the columns, and its own frozen copies of the Filter and the Sort. */
interface Query<Row> {
    readonly columns: readonly QueryColumn<Row>[];
    readonly filter: GridFilter | undefined;
    readonly sorts: readonly SortSpec[];
}

/** What a live source holds besides the state: its rows by key, its gathering, and the changes gathered. */
interface Live<Row> {
    readonly keyed: KeyedRows<Row>;
    readonly gatherer: ChangeGatherer;
    readonly times: CellChangeTimes<Row>;
    /** The rows changed since the Window was last computed, by key, recorded at the first change. What the
     * next publication pairs with what is held then. */
    readonly pending: Map<RowKey, Pending<Row>>;
    /** Each Window row's ordinal, beside it: what the incremental requery merges into. */
    ordinals: readonly number[];
    /** A whole new list put the rows in another order since the Window was last computed (D7): the next
     * publication requeries whole. */
    reordered: boolean;
}

/** A Grid Source over rows held in memory: the counterpart of C#'s `InMemoryGridSource`. */
export class InMemoryGridSource<Row> implements GridSource<Row> {
    #rows: readonly Row[];
    readonly #listeners = new Set<() => void>();
    readonly #rowKey: RowKeyOf<Row> | undefined;
    readonly #live: Live<Row> | undefined;
    readonly #marks: InMemoryRowMarks<Row>;
    #columns: readonly QueryColumn<Row>[] | undefined;
    #state: GridSourceState<Row>;

    private constructor(rows: readonly Row[], { rowKey, clock = systemClock }: InMemorySourceOptions<Row>) {
        this.#rowKey = rowKey;
        let window: readonly Row[];
        if (rowKey === undefined) {
            // A copy: the Consumer changing the list it passed cannot move the Window under the grid, nor the
            // order the next sequence is compared with.
            this.#rows = Object.freeze([...rows]);
            this.#live = undefined;
            window = this.#rows;
        } else {
            // Keys are checked before anything is kept: a repeated key, or one that names no row, is refused.
            const keyed = new KeyedRows(rows, rowKey);
            const held = keyed.snapshot();
            this.#rows = Object.freeze([]);
            this.#live = {
                keyed,
                gatherer: new ChangeGatherer(clock, () => { this.#gatherDue(); }),
                times: new CellChangeTimes(rowKey, clock),
                pending: new Map(),
                ordinals: Object.freeze(held.ordinals),
                reordered: false,
            };
            window = Object.freeze(held.rows);
        }
        this.#marks = new InMemoryRowMarks({
            current: () => this.#state,
            shown: key => this.#shownUnder(key),
            heldRows: () => this.#baseRows(),
        }, rows, rowKey);
        this.#state = Object.freeze({
            window,
            windowStart: 0,
            totalCount: window.length,
            loading: false,
            rowSequenceVersion: 0,
            sorts: Object.freeze([]),
            filter: undefined,
        });
    }

    /** A source over these rows, in this order until a Sort says otherwise; given a Row Key, a live one. */
    static from<Row>(rows: readonly Row[], options: InMemorySourceOptions<Row> = {}): InMemoryGridSource<Row> {
        return new InMemoryGridSource(rows, options);
    }

    /** The Row Key this source was given (ADR-0140, ADR-0141), or `undefined` for none. The grid pairs a
     * row's next version with the one it painted by it. */
    get rowKey(): RowKeyOf<Row> | undefined {
        return this.#rowKey;
    }

    /** True under a Row Key: the source refuses a repeated key, when it is made and in every change, so no
     * Window it hands over holds a row twice, and the grid need not check (ADR-0141). */
    get vouchesDistinctRows(): boolean {
        return this.#rowKey !== undefined;
    }

    /**
     * The shortest time, in milliseconds, between two live changes reaching the grid (ADR-0141, ADR-0067):
     * 250 unless set. The first change after a quiet interval reaches it at once; the changes within an
     * interval reach it as one, at the interval's end, from the newest version. Zero passes every change as
     * it comes. A negative interval is refused, and so is setting it without a Row Key.
     */
    get gatherInterval(): number {
        return this.#requireLive('gatherInterval').gatherer.interval;
    }

    set gatherInterval(milliseconds: number) {
        this.#requireLive('gatherInterval').gatherer.interval = milliseconds;
        if (this.#shouldPublish() && this.#publishPending()) this.#notify();
    }

    /** The Row Marks of this source (ADR-0043): kept per row it holds, by identity, or under a Row Key by
     * key. A changed row keeps its mark, and a removed row takes its mark with it when its removal is shown. */
    get marks(): InMemoryRowMarks<Row> {
        return this.#marks;
    }

    /**
     * The Change Highlight of this source (ADR-0141, on ADR-0067's rules), to hand to the grid's
     * `cellChangedAt`; `undefined` without a Row Key, which pairs nothing. One function for the source's life,
     * so only a row with a new object asks again (LV-9).
     * - A cell is marked when a changed row's displayed text differs from its previous version's; a change
     *   the format hides is not marked.
     * - Every cell of a row that comes into the result is marked.
     * - A Sort, a Filter or new columns mark nothing; neither does the Consumer's own edit through
     *   `replaceRow`.
     *
     * The displayed text is the column's `format`, or the value's own text (`textOf`). Nothing is compared
     * until this is first read. A change time answers for `changeTimesKeptFor`.
     */
    get cellChangedAt(): CellChangedAt<Row> | undefined {
        return this.#live?.times.lookup;
    }

    /**
     * How long, in milliseconds, the source keeps a cell's change time for `cellChangedAt`: a minute unless
     * set. A time older than this answers nothing, so set it to at least the grid's mark duration. Bounding it
     * is what keeps the memory of a source whose rows all change in the end to the rows that changed lately.
     * One not above zero is refused, and so is setting it without a Row Key: nothing is paired, so nothing is
     * kept.
     */
    get changeTimesKeptFor(): number {
        return this.#live === undefined ? defaultChangeTimesKeptFor : this.#live.times.keptFor;
    }

    set changeTimesKeptFor(milliseconds: number) {
        this.#requireLive('changeTimesKeptFor').times.keptFor = milliseconds;
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
     *
     * Under a Row Key it is a user's own edit, and it is not gathered (ADR-0141): it reaches the grid at
     * once, bringing the live changes gathered so far with it. `row` must be the version the source holds
     * under its key; one a live change has replaced since is refused by name rather than written over, since
     * a replacement built from it would quietly undo that change. The replacement keeps the key.
     */
    replaceRow(row: Row, replacement: Row): void {
        if (row === replacement) {
            throw new TypeError('The replacement is the row itself. A change made in place does not reach the screen: '
                + 'hand over a new object, since identity is what says a row changed (ADR-0003, ADR-0007).');
        }
        if (this.#live !== undefined) {
            this.#replaceKeyedRow(this.#live, row, replacement);
            return;
        }
        const index = this.#rows.indexOf(row);
        if (index < 0) {
            throw new TypeError('The row is not in this source. An Edit Intent carries the object the grid painted; '
                + 'another, or an earlier version, cannot be found in it (ADR-0007).');
        }
        const columns = this.#requireColumns();
        const rows = this.#rows.with(index, replacement);
        const next = applyQuery(rows, columns, this.#state.filter, this.#state.sorts);
        const { window } = this.#state;
        // The edited row standing where its earlier version stood is the same sequence, not a reorder.
        const moved = next.length !== window.length
            || next.some((each, at) => each !== window[at] && !(each === replacement && window[at] === row));
        this.#rows = Object.freeze(rows);
        this.#marks.replaced(index, row, replacement);
        this.#show(next, this.#state.rowSequenceVersion + (moved ? 1 : 0));
        this.#notify();
    }

    /**
     * Applies a Change Batch whole (ADR-0141, LV-3): the rows it changes, found by their Row Key, each keep
     * the place of the row they replace; the rows it adds go at the end; the keys it removes leave. A key
     * that repeats (within the batch, or an added key the source already holds), a changed or removed key
     * the source does not hold, a key that names no row, a changed row that is the object already held, or
     * a row whose values the Query in force refuses, refuses the whole batch by name, and nothing of it is
     * applied. It is gathered: the first change after a quiet interval reaches the grid at once, and the
     * changes after it within the interval reach it as one at its end (LV-6).
     */
    apply(batch: GridChangeBatch<Row>): void {
        const live = this.#requireLive('apply');
        const plan = this.#planOf(live.keyed, batch);
        if (isEmpty(plan)) return;
        this.#fold(live, plan);
        if (this.#shouldPublish() && this.#publishPending()) this.#notify();
    }

    /**
     * Takes a whole new list, paired with the rows held by Row Key (ADR-0141, LV-4), as ag-grid takes new
     * `rowData` under `getRowId`: a new key is a row added, a key missing is a row removed, another object
     * under a key is a row changed, and the same object is a row unchanged. Pairing compares objects, never
     * values. A key that repeats in the list refuses it by name.
     *
     * The source's own order follows the list (D7): a reload whose rows come back in a new order is shown in
     * that order, and under a Sort their ties fall in it. The Row Sequence Version moves when the result's
     * sequence of keys moved, and only then. It is gathered like a batch.
     */
    replaceAll(rows: readonly Row[]): void {
        const live = this.#requireLive('replaceAll');
        const plan = this.#pairWithHeld(live.keyed, rows);
        if (isEmpty(plan)) return;
        this.#fold(live, plan);
        if (this.#shouldPublish() && this.#publishPending()) this.#notify();
    }

    /**
     * Puts out at once the live changes gathered and not yet published (ADR-0141, ADR-0142, LV-16), so the
     * Window is the newest version this source holds: the grid calls it just before it judges a write, so a
     * `replaceRow` built on the row it then reads is taken. It is a publication like the one at an
     * interval's end: the next live change waits the interval from it. It is heard when it put something
     * out, and never when it did not. Nothing gathered, or no Row Key, and it does nothing.
     */
    publishGathered(): void {
        if (this.#live !== undefined && this.#publishPending()) this.#notify();
    }

    /** Everything is in hand, so a range needs no answer; a range past the rows is no error, since a
     * request can cross a change to the data. A range that names no rows is refused as the call is made,
     * not in the promise, since the grid does not wait on a notification. */
    needRange(range: RowRange): Promise<void> {
        checked(range);
        return Promise.resolve();
    }

    /** The rows of the result in the range, as many of them as there are (ADR-0005). */
    async rows(range: RowRange, signal: AbortSignal): Promise<readonly Row[]> {
        checked(range);
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
        const all = this.#baseRows();
        const rows = others === undefined ? all : applyQuery(all, columns, others, undefined);
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

    /** The rows held, in the source's order. */
    #baseRows(): readonly Row[] {
        return this.#live === undefined ? this.#rows : this.#live.keyed.snapshot().rows;
    }

    #requireColumns(): readonly QueryColumn<Row>[] {
        if (this.#columns === undefined) {
            throw new Error('Columns have not been received yet; call setColumns first. '
                + 'Bound to a grid this cannot happen: the grid pushes its columns when it binds.');
        }
        return this.#columns;
    }

    #requireLive(member: string): Live<Row> {
        if (this.#live === undefined) {
            throw new Error(`${member} needs a Row Key, and this source was given none: GridSource.from(rows, { rowKey }) `
                + 'takes live data, and pairs a row\'s versions by the key (ADR-0141).');
        }
        return this.#live;
    }

    #commit(columns: readonly QueryColumn<Row>[], filter: GridFilter | undefined, sorts: readonly SortSpec[]): void {
        // The same Query pushed again changes nothing and is not heard: an adapter that pushes on every
        // render and renders on every change would otherwise never stop (ADR-0023).
        const sortsChanged = !sortsEqual(sorts, this.#state.sorts);
        const filterChanged = !filtersEqual(filter, this.#state.filter);
        const columnsChanged = this.#columns === undefined || !columnsEqual(columns, this.#columns);
        if (!sortsChanged && !filterChanged && !columnsChanged) return;
        // Computed first, from what was pushed: a Query the engine refuses leaves the source as it was. Then
        // kept as copies, so the Consumer's own objects behind them can change without reaching the source
        // (ADR-0023); a part that did not change keeps the copy in force.
        const query = { columns, filter, sorts };
        const kept = (): Query<Row> => ({
            columns: Object.freeze([...columns]),
            filter: filterChanged ? snapshotFilter(filter) : this.#state.filter,
            sorts: sortsChanged ? Object.freeze(sorts.map(sort => Object.freeze({ ...sort }))) : this.#state.sorts,
        });
        const heard = this.#live === undefined ? this.#commitPlain(query, kept) : this.#commitKeyed(this.#live, query, kept);
        if (heard || sortsChanged || filterChanged) this.#notify();
    }

    /** A new Query over rows that have no key: heard when the sequence moved. Only new columns, and a result
     * as it was, keep the snapshot the same object, since nobody is told. */
    #commitPlain(pushed: Query<Row>, kept: () => Query<Row>): boolean {
        const next = applyQuery(this.#rows, pushed.columns, pushed.filter, pushed.sorts);
        const query = kept();
        const { window } = this.#state;
        const moved = next.length !== window.length || next.some((row, at) => row !== window[at]);
        this.#columns = query.columns;
        if (!moved && query.filter === this.#state.filter && query.sorts === this.#state.sorts) return false;
        this.#show(next, this.#state.rowSequenceVersion + (moved ? 1 : 0), query);
        return moved;
    }

    /**
     * A new Query under a Row Key: the changes gathered so far are published first, under the Query they
     * were gathered under; then the result is requeried whole under the new one. The version moves when the
     * result's sequence of keys differs from the one the grid had: the grid sees this whole commit as one
     * change, whatever the gathered changes did on the way.
     */
    #commitKeyed(live: Live<Row>, pushed: Query<Row>, kept: () => Query<Row>): boolean {
        const before = this.#state;
        // The rows held are already the newest version, the gathered changes among them.
        const { rows, ordinals } = live.keyed.snapshot();
        const positions = queryPositions(rows, pushed.columns, pushed.filter, pushed.sorts);
        const query = kept();
        // What was gathered is in the rows requeried here, so it reaches the grid with this commit. Under the
        // Change Highlight it is published first, under the Query it was gathered under, which is what its
        // marks are compared against; otherwise it is let go, and the marks of the rows it removed go with this
        // commit.
        const removed = live.times.wanted ? [] : this.#removedUnshown(live);
        if (live.times.wanted) this.#publishPending();
        else this.#dropPending(live);
        const next = positions.map(at => itemAt(rows, at));
        live.ordinals = Object.freeze(positions.map(at => itemAt(ordinals, at)));
        this.#columns = query.columns;
        const sequenceMoved = !this.#sameKeys(before.window, next);
        const windowChanged = !sameRows(before.window, next);
        if (!sequenceMoved && !windowChanged && query.filter === before.filter && query.sorts === before.sorts) {
            // Nothing the grid reads changed: the snapshot is the one it had, whatever was published meanwhile.
            this.#state = before;
            this.#marks.published(removed);
            return false;
        }
        // An unchanged result keeps its object, so the grid has nothing new to take.
        this.#show(windowChanged ? next : before.window, before.rowSequenceVersion + (sequenceMoved ? 1 : 0), query);
        this.#marks.published(removed);
        return sequenceMoved || windowChanged;
    }

    /** Checks a batch against what is held, all of it, before anything is taken in (LV-3). */
    #planOf(keyed: KeyedRows<Row>, batch: GridChangeBatch<Row>): ChangePlan<Row> {
        const { added = [], changed = [], removedKeys = [] } = batch;
        const seen = new Set<RowKey>();
        const once = (key: RowKey, where: string): void => {
            if (seen.has(key)) {
                throw new TypeError(`The Change Batch names the Row Key ${quoted(key)} more than once (${where}). A batch says one `
                    + 'thing of each row; nothing of it was applied (ADR-0141).');
            }
            seen.add(key);
        };
        const removed = removedKeys.map((written, i) => {
            const key = checkedKey(written, `Removed key ${i.toString()}`);
            once(key, `removed key ${i.toString()}`);
            if (!keyed.holds(key)) {
                throw new TypeError(`The Change Batch removes the Row Key ${quoted(key)}, and this source holds no row under it. `
                    + 'Nothing of the batch was applied (ADR-0141).');
            }
            return key;
        });
        const changedRows = changed.map((row, i): readonly [RowKey, Row] => {
            const key = keyed.keyOf(row, `Changed row ${i.toString()}`);
            once(key, `changed row ${i.toString()}`);
            const held = keyed.get(key);
            if (held === undefined) {
                throw new TypeError(`The Change Batch changes the row under the Row Key ${quoted(key)}, and this source holds no `
                    + 'row under it; a new row is added, not changed. Nothing of the batch was applied (ADR-0141).');
            }
            if (held.row === row) {
                throw new TypeError(`The Change Batch changes the row under the Row Key ${quoted(key)} to the object already `
                    + 'held. A row changed in place does not reach the screen; a change is a new object (ADR-0003, ADR-0141). '
                    + 'Nothing of the batch was applied.');
            }
            return [key, row];
        });
        const addedRows = added.map((row, i): readonly [RowKey, Row] => {
            const key = keyed.keyOf(row, `Added row ${i.toString()}`);
            once(key, `added row ${i.toString()}`);
            if (keyed.holds(key)) {
                throw new TypeError(`The Change Batch adds a row under the Row Key ${quoted(key)}, which this source already `
                    + 'holds; two rows under one key would be painted as one (ADR-0140). A new version is a changed row. '
                    + 'Nothing of the batch was applied (ADR-0141).');
            }
            return [key, row];
        });
        const newRows = [...changedRows, ...addedRows].map(([, row]) => row);
        this.#checkValues(newRows);
        return { added: addedRows, changed: changedRows, removed, order: undefined };
    }

    /** A whole new list, paired with what is held by key (LV-4), and whether its order is the one the batch
     * that says the same would give: the rows held in their order, the rows added after them all (D7). */
    #pairWithHeld(keyed: KeyedRows<Row>, rows: readonly Row[]): ChangePlan<Row> {
        const listed = new Map<RowKey, number>();
        const added: (readonly [RowKey, Row])[] = [];
        const changed: (readonly [RowKey, Row])[] = [];
        let lastOrdinal = -1;
        let addedBefore = false;
        let reordered = false;
        const keys: RowKey[] = [];
        for (const [i, row] of rows.entries()) {
            const key = keyed.keyOf(row, `Row ${i.toString()} of the list`);
            keys.push(key);
            const first = listed.get(key);
            if (first !== undefined) {
                throw new TypeError(`Rows ${first.toString()} and ${i.toString()} of the list both have the Row Key ${quoted(key)}; `
                    + 'two rows under one key would be painted as one (ADR-0140). Nothing of the list was taken (ADR-0141).');
            }
            listed.set(key, i);
            const held = keyed.get(key);
            if (held === undefined) {
                added.push([key, row]);
                addedBefore = true;
                continue;
            }
            // A row held that stands before one it followed, or after a row added.
            reordered ||= addedBefore || held.ordinal < lastOrdinal;
            lastOrdinal = held.ordinal;
            if (held.row !== row) changed.push([key, row]);
        }
        // Every held key the list names is kept or changed; when fewer are named than are held, the rest are
        // removed.
        const removed = listed.size - added.length < keyed.count ? [...keyed.keys()].filter(key => !listed.has(key)) : [];
        const newRows = [...changed, ...added].map(([, row]) => row);
        this.#checkValues(newRows);
        return { added, changed, removed, order: reordered ? keys : undefined };
    }

    /** The new rows' values, held to the Query in force as a requery would hold them: a value whose type
     * contradicts its column's declaration is refused now, by name, rather than when the change reaches the
     * grid (ADR-0023). */
    #checkValues(rows: readonly Row[]): void {
        if (this.#columns === undefined || rows.length === 0) return;
        queryPositions(rows, this.#columns, this.#state.filter, this.#state.sorts);
        // Under the Change Highlight each new row's text is compared at publication: a format that throws on
        // one refuses it now, before anything is taken in, rather than leaving a change held and unpublished.
        if (this.#live?.times.wanted === true) {
            for (const row of rows) for (const column of this.#columns) if (column.queryable !== false) textOf(column, row);
        }
    }

    /** Takes a checked plan in: the rows move to the newest version, and each key it touches remembers the
     * version the Window was computed from. */
    #fold(live: Live<Row>, plan: ChangePlan<Row>, byUser = false): void {
        const { keyed, pending } = live;
        const remember = (key: RowKey): Pending<Row> => {
            let gathered = pending.get(key);
            if (gathered === undefined) {
                gathered = { before: keyed.get(key), userEdit: undefined };
                pending.set(key, gathered);
            }
            return gathered;
        };
        for (const key of plan.removed) {
            remember(key);
            keyed.remove(key);
        }
        for (const [key, row] of plan.changed) {
            const gathered = remember(key);
            if (byUser) gathered.userEdit = { before: defined(keyed.get(key), `the row held under ${quoted(key)}`).row };
            keyed.change(key, row);
        }
        for (const [key, row] of plan.added) {
            remember(key);
            keyed.add(key, row);
        }
        if (plan.order !== undefined) {
            // The list's order is the source's now (D7). The rows take new ordinals, so the next publication
            // cannot place them among the Window by the old ones: it requeries whole.
            keyed.reorder(plan.order);
            live.reordered = true;
        }
    }

    /** Whether the changes waiting should be published now. */
    #shouldPublish(): boolean {
        const live = this.#live;
        if (live === undefined || (live.pending.size === 0 && !live.reordered)) return false;
        return live.gatherer.shouldAskNow();
    }

    /** The gathering timer fired. */
    #gatherDue(): void {
        this.#live?.gatherer.fired();
        if (this.#shouldPublish() && this.#publishPending()) this.#notify();
    }

    /**
     * The Window brought up to the newest version from the changes gathered, by the incremental requery; the
     * version moved when the sequence did (LV-7). True when the grid has something new to paint. Every
     * publication disarms the wait for the interval's end, which has nothing left to wait for.
     */
    #publishPending(): boolean {
        const live = this.#live;
        if (live === undefined || (live.pending.size === 0 && !live.reordered)) return false;
        const { keyed, pending, reordered } = live;
        const changes: RowChange<Row>[] = [];
        const gathered: Pending<Row>[] = [];
        for (const [key, each] of pending) {
            const old = each.before;
            const now = keyed.get(key);
            // Changed and changed back, or added and removed again, within one gathering: nothing to show.
            const unchanged = old === undefined ? now === undefined
                : now !== undefined && now.row === old.row && now.ordinal === old.ordinal;
            if (unchanged) continue;
            changes.push({ key, old, new: now });
            gathered.push(each);
        }
        // Requeried before anything gathered is let go: a requery that throws leaves the changes waiting.
        const { window, filter, sorts } = this.#state;
        const outcome = changes.length === 0 && !reordered ? undefined
            : liveRequery(window, live.ordinals, changes, { columns: this.#queryColumns(), filter, sorts }, keyed.count, () => keyed.snapshot(), reordered);
        if (outcome !== undefined) this.#recordChanges(live.times, changes, gathered, outcome);
        this.#dropPending(live);
        if (outcome === undefined) return false;
        live.ordinals = Object.freeze(outcome.ordinals);
        const shown = outcome.result !== window || outcome.moved;
        if (shown) this.#show(outcome.result, this.#state.rowSequenceVersion + (outcome.moved ? 1 : 0));
        // A removed row's mark goes when its removal is shown, here, once the Window is the new one. A key
        // removed and added again within one gathering is a changed row, and keeps its mark.
        this.#marks.published(changes.flatMap(change => change.new === undefined ? [change.key] : []));
        return shown;
    }

    /** The Change Highlight of one publication (ADR-0141, ADR-0067, LV-9): a row that stood in the result and
     * still does is compared cell by cell; one that was not in it and now is has come in, and every cell is
     * marked; a key removed is forgotten. The Consumer's own edit is compared only up to the version before
     * it, and a row it brings in is not marked. */
    #recordChanges(times: CellChangeTimes<Row>, changes: readonly RowChange<Row>[], gathered: readonly Pending<Row>[], outcome: LiveRequeryOutcome<Row>): void {
        if (!times.wanted) return;
        const pairs: ChangedPair<Row>[] = [];
        const appeared: RowKey[] = [];
        const removed: RowKey[] = [];
        changes.forEach((change, j) => {
            if (change.new === undefined) {
                removed.push(change.key);
                return;
            }
            if (itemAt(outcome.newIndex, j) < 0) return;
            const { userEdit } = itemAt(gathered, j);
            if (itemAt(outcome.oldIndex, j) >= 0 && change.old !== undefined) {
                pairs.push({ key: change.key, old: change.old.row, new: userEdit === undefined ? change.new.row : userEdit.before });
            } else if (userEdit === undefined) {
                appeared.push(change.key);
            }
        });
        times.record(this.#queryColumns(), pairs, appeared, removed);
    }

    /** The version of the row under `key` that the grid shows: until a gathered change is published, the one
     * before it. */
    #shownUnder(key: RowKey): Row | undefined {
        const live = this.#live;
        if (live === undefined) return undefined;
        const gathered = live.pending.get(key);
        return gathered === undefined ? live.keyed.get(key)?.row : gathered.before?.row;
    }

    /** The keys whose removal was gathered and not yet shown. */
    #removedUnshown(live: Live<Row>): RowKey[] {
        return [...live.pending].flatMap(([key, each]) => each.before !== undefined && live.keyed.get(key) === undefined ? [key] : []);
    }

    /** Lets go of what was gathered, which reached the grid, and the wait for it. */
    #dropPending(live: Live<Row>): void {
        if (live.pending.size === 0 && !live.reordered) return;
        live.gatherer.disarm();
        live.pending.clear();
        live.reordered = false;
        live.gatherer.shown();
    }

    /** The columns the Query in force reads. Before the grid binds none have come, and no Sort or Filter can
     * be in force either, since both name columns: the rows stand in the source's order, which no column
     * is needed to read. */
    #queryColumns(): readonly QueryColumn<Row>[] {
        return this.#columns === undefined ? noColumns : this.#columns;
    }

    /** The Consumer's own edit under a Row Key: not gathered, so it reaches the grid at once with whatever
     * was gathered. */
    #replaceKeyedRow(live: Live<Row>, row: Row, replacement: Row): void {
        const { keyed } = live;
        const key = keyed.keyOf(row, 'The row');
        const held = keyed.get(key);
        if (held === undefined) {
            throw new TypeError(`The row is not in this source: it holds no row under the Row Key ${quoted(key)}. An Edit Intent `
                + 'carries the object the grid painted (ADR-0007, ADR-0141).');
        }
        if (held.row !== row) {
            throw new TypeError(`The row under the Row Key ${quoted(key)} changed since this version of it was taken: a live change `
                + 'replaced it. A replacement built from it would quietly undo that change, so it is refused (ADR-0141, ADR-0142); '
                + 'build the edit on the row as the source holds it now.');
        }
        const replacementKey = keyed.keyOf(replacement, 'The replacement');
        if (replacementKey !== key) {
            throw new TypeError(`The replacement has the Row Key ${quoted(replacementKey)}, and the row it replaces ${quoted(key)}. `
                + 'A Row Key stays the same when a row\'s data changes (ADR-0140); a row under another key is a row removed and a '
                + 'row added, a Change Batch.');
        }
        this.#requireColumns();
        this.#checkValues([replacement]);
        this.#fold(live, { added: [], changed: [[key, replacement]], removed: [], order: undefined }, true);
        if (this.#publishPending()) this.#notify();
    }

    #sameKeys(a: readonly Row[], b: readonly Row[]): boolean {
        const rowKey = this.#rowKey;
        if (rowKey === undefined) return sameRows(a, b);
        return a.length === b.length && a.every((row, at) => {
            const other = itemAt(b, at);
            return row === other || rowKey(row) === rowKey(other);
        });
    }

    /** Replaces the state with a frozen one showing `window` under `version`, and the Query given, or the one
     * in force. */
    #show(window: readonly Row[], version: number, query: Pick<GridSourceState<Row>, 'filter' | 'sorts'> = this.#state): void {
        this.#state = Object.freeze({
            window: Object.isFrozen(window) ? window : Object.freeze([...window]),
            windowStart: 0,
            totalCount: window.length,
            loading: false,
            rowSequenceVersion: version,
            sorts: query.sorts,
            filter: query.filter,
        });
    }

    #notify(): void {
        // A copy, so a listener that subscribes another meanwhile is heard from the next change on.
        for (const listener of Array.from(this.#listeners)) listener();
    }
}

/** No columns: the Query of a source the grid has not bound yet. */
const noColumns: readonly never[] = Object.freeze([]);

function isEmpty<Row>(plan: ChangePlan<Row>): boolean {
    return plan.added.length === 0 && plan.changed.length === 0 && plan.removed.length === 0 && plan.order === undefined;
}

/** The range, refused when it names no rows: a start before the first, or fewer than one row (ADR-0001). */
function checked(range: RowRange): RowRange {
    return rowRange(range.start, range.count);
}

function sameRows<Row>(a: readonly Row[], b: readonly Row[]): boolean {
    return a.length === b.length && a.every((row, at) => row === b[at]);
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
            && column.value === other.value && column.queryable === other.queryable && column.dateType === other.dateType
            && column.format === other.format));
    });
}
