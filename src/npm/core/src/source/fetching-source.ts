// `GridSource.fetch`: the bundled Grid Source for a result that lives somewhere else (ADR-0025), the
// counterpart of C#'s `FetchingGridSource`. It holds one Window at a time, asks for the next one when the
// grid says it needs rows, and takes on the three things ADR-0001 left to "the Consumer, or GridSource":
// read-ahead, coalescing while scrolling, and discarding an answer that arrived after the question changed.
//
// It fetches the first page itself. The grid cannot ask for rows in a result it has been told is empty
// ("nothing to show is not a range", ADR-0001), so a source that starts with nothing breaks its own cold
// start, when the grid binds its columns.
//
// Given a Row Mark adapter it keeps Row Marks (ADR-0043) as keys and snapshots, and asks the server for their
// counts whenever what they are made of may have moved: the Filter, the marks, or the size of the result.
//
// Given a Row Key it hears that the data moved on (ADR-0141): the Consumer calls `notifyChanged` however it
// learns it (a WebSocket, SignalR, polling: the transport is the app's) and the source reads its Window again, gathered on its clock by
// the rules `GridSource.from` gathers by. It pairs every answer's rows with the ones it painted by key and
// marks the cells whose displayed text changed (`cellChangedAt`); it refuses an answer that repeats a key, by
// name, and so vouches that no Window holds a row twice. Only the server knows whether the order of the whole
// result moved: an answer may carry its order token, and the Row Sequence Version moves when it differs from
// the previous answer's, and with every change when the server sends none.

import type { QueryColumn } from '../query/columns';
import type { GridFilter } from '../query/filter';
import { checkedSorts, sortsEqual, type SortSpec } from '../query/sort';
import { defined } from '../must';
import { rowRange, type RowRange } from '../viewport/row-range';
import type { GridFindRequest, GridFindResult } from '../finding/find';
import type { GridSummaryRequest, GridSummaryResult } from '../summary/grid-summary';
import { checkedAdapter, FetchingRowMarks, type FetchingMarksHost, type RowMarkAdapter } from './fetching-row-marks';
import { filtersEqual, filterWithout, snapshotFilter } from './filters';
import type { DistinctValues, GridSource, GridSourceState } from './grid-source';
import { CellChangeTimes, defaultChangeTimesKeptFor, type CellChangedAt, type ChangedPair } from './cell-change-times';
import { ChangeGatherer, defaultGatherInterval } from './change-gatherer';
import { systemClock, type GridClock } from './grid-clock';
import { checkedKey, quoted, type RowKey, type RowKeyOf } from './row-key';

/** How many rows the first fetch asks for, before the grid has said how tall it is: wide enough for an
 * ordinary Viewport. The grid's own request replaces it as soon as one arrives. */
const defaultPageRows = 100;

/** Rows fetched either side of what the grid asks for, unless the Consumer says otherwise. */
const defaultReadAheadRows = 60;

/** The whole of what the grid asks a server for (`CONTEXT.md`, Query): which range, under which Sort, under
 * which Filter. Every part is data, so it can cross to a server unchanged (ADR-0002). */
export interface GridQuery {
    /** The rows wanted, read-ahead included. */
    readonly range: RowRange;
    /** The Sort in force, outermost first; empty for the server's own order. */
    readonly sorts: readonly SortSpec[];
    /** The Filter in force, or `undefined` for none. */
    readonly filter: GridFilter | undefined;
}

/** The Sort and the Filter in force, which a question beside the rows (Find, a Summary) is answered under. */
export type QueryInForce = Omit<GridQuery, 'range'>;

/**
 * One answer to a Query (ADR-0001): the rows, the position they start at, and how many rows the whole
 * filtered result holds. The position is part of the answer, since a server may answer with less than it
 * was asked for, a shrinking result or a clamp at the end, and every offset on screen depends on it. A page
 * whose rows run past its own total contradicts itself, and is refused.
 */
export interface GridPage<Row> {
    /** The rows, in the Query's order; possibly fewer than were asked for (ADR-0025). */
    readonly rows: readonly Row[];
    /** The position of `rows[0]` in the whole filtered result. */
    readonly start: number;
    /** The rows of the whole filtered result: what the scrollbar is made of. */
    readonly totalCount: number;
    /** The server's order token (ADR-0141): a value naming the order of the whole result under this Query,
     * which changes whenever a row anywhere in it was added, removed or moved. An answer whose token differs
     * from the previous answer's moves the Row Sequence Version. Compared as a string. */
    readonly orderToken?: string | undefined;
}

/** A failed fetch, as `onFetchFailed` hears it. */
export interface FetchFailedEvent {
    /** What the fetch failed with. */
    readonly error: unknown;
}

/** How `GridSource.fetch` reaches the Consumer's server beside the pages themselves. */
export interface FetchingSourceOptions<Row> {
    /** Rows fetched either side of what the grid asks for (ADR-0001): 60 unless given. */
    readonly readAheadRows?: number;
    /** A column's value list, a server's DISTINCT under every Filter but the column's own, which it is
     * handed already removed (ADR-0009). Without it the answer is too many, and the panel offers a condition
     * rather than enumerate a result the source cannot see. */
    readonly distinctValues?: (column: string, filter: GridFilter | undefined, signal: AbortSignal) => Promise<DistinctValues>;
    /** A Find step (ADR-0055), answered under the Sort and the Filter in force; `findStep` is the reference
     * for what a match is. Without it the source does not find. */
    readonly find?: (request: GridFindRequest, query: QueryInForce, signal: AbortSignal) => Promise<GridFindResult>;
    /** A Selection Summary (ADR-0130), answered under the Sort and the Filter in force as `summaryOf` would
     * over that result, or declined. Without it the source does not summarise. */
    readonly summarize?: (request: GridSummaryRequest, query: QueryInForce, signal: AbortSignal) => Promise<GridSummaryResult>;
    /** The Row Key (ADR-0140): given one, every answer is checked for a repeated or missing key and refused
     * by name, so the source vouches that no Window holds a row twice (ADR-0141). The Row Mark adapter's key
     * is one, and serves when this is left out; both given must be the same function. */
    readonly rowKey?: RowKeyOf<Row>;
    /** What the source needs from the Consumer to keep Row Marks (ADR-0043): a row's key, and the server's
     * answers about snapshots and counts. Without it the source keeps none, and a Mark Column bound to it is
     * refused by name. */
    readonly marks?: RowMarkAdapter<Row>;
    /** The clock the gathering of live changes and their change times run on (ADR-0141); the system's unless
     * given, as a test gives its own. */
    readonly clock?: GridClock;
    /** Hears a failed fetch. Without it a failure is not swallowed: the grid that asked for the rows has
     * its promise rejected, and a fetch nobody asked for, the cold start and a restart, rejects unheard,
     * which reaches the host's own error path. Either way `lastError` holds it until an answer lands. A
     * failure left unseen would leave the rows on screen looking current (ADR-0025). */
    readonly onFetchFailed?: (event: FetchFailedEvent) => void;
}

/** What a fetching source holds at one moment: a Grid Source's state, and the last failure. */
export interface FetchingSourceState<Row> extends GridSourceState<Row> {
    /** The last failure, kept so a Consumer can show it without hearing it; `undefined` once an answer
     * lands. */
    readonly lastError: unknown;
}

/** A Grid Source over a result a server holds: the counterpart of C#'s `FetchingGridSource`. */
export class FetchingGridSource<Row> implements GridSource<Row> {
    readonly #fetch: (query: GridQuery, signal: AbortSignal) => Promise<GridPage<Row>>;
    readonly #readAheadRows: number;
    readonly #distinctValues: FetchingSourceOptions<Row>['distinctValues'];
    readonly #onFetchFailed: FetchingSourceOptions<Row>['onFetchFailed'];
    readonly #rowKey: RowKeyOf<Row> | undefined;
    readonly #listeners = new Set<() => void>();
    readonly #marks: FetchingRowMarks<Row> | undefined;
    // Aborted on dispose: what the marks ask the server for belongs to no one question, so it lives as long as
    // the source does.
    readonly #lifetime = new AbortController();
    // The failure the marks last reported, so the marks' next success can let go of it, and only it: a failed
    // fetch stays until an answer lands.
    #marksFailure: unknown;
    #state: FetchingSourceState<Row>;

    // The question in flight: what it asked for, its promise, the answer it waits for, and how to call it
    // off. A superseded answer is told by its generation, not by its abort: aborting is a courtesy to the
    // server, and the generation is what makes it correct.
    #inFlight: RowRange | undefined;
    // The rows the grid last said it needs of the question in flight: what its answer has to reach. A range
    // asked again that widens to the one in flight joins it, and its need is the one the answer is held to.
    #inFlightNeeded: RowRange | undefined;
    #current: Promise<void> = Promise.resolve();
    #generation = 0;
    #controller: AbortController | undefined;
    #pageRows = defaultPageRows;
    #started = false;
    #disposed = false;
    // The order token of the last answer, and whether one has landed since the last restart.
    #orderToken: string | undefined;
    #answered = false;

    // ---- Live data, under a Row Key (ADR-0141). Undefined without one. ----------------------------------
    readonly #live: { readonly gatherer: ChangeGatherer; readonly times: CellChangeTimes<Row> } | undefined;
    // The columns the grid paints by: what the displayed text of a row is read from.
    #columns: readonly QueryColumn<Row>[] = [];
    // The data moved on, and no question asked since carries it.
    #changeWaiting = false;
    // Whether the question in flight was asked after the data moved on, so its answer carries it.
    #inFlightCarries = false;
    // The rows the grid last said it needs: what a Window read again must still reach.
    #lastNeeded: RowRange | undefined;
    // The range the question that brought the Window asked for; undefined while there is no Window.
    #windowAsked: RowRange | undefined;
    // The notices heard, counted; how many the carrying question in flight was asked after; how many an answer
    // that landed has carried; and the last notice that named no added keys (0 for none). What the Change
    // Highlight of an answer takes a row not painted before to be (D6).
    #notices = 0;
    #noticesAsked = 0;
    #noticesShown = 0;
    #lastNameless = 0;
    // The keys named as added, each with the last notice that named it, not yet let go: a carrying answer that
    // lands applies them all, and lets go of the ones its question was asked after.
    #named: Map<RowKey, number> | undefined;

    // Declared, never defined as fields: a source given no `find` has no such member at all, which is how a
    // Grid Source says what it cannot do (ADR-0350 §16).
    /** A Find step through the Consumer's server (ADR-0055); absent when it was given no `find`. */
    declare readonly find?: (request: GridFindRequest, signal: AbortSignal) => Promise<GridFindResult>;
    /** A Selection Summary through the Consumer's server (ADR-0130); absent when it was given no
     * `summarize`. */
    declare readonly summarize?: (request: GridSummaryRequest, signal: AbortSignal) => Promise<GridSummaryResult>;

    private constructor(fetch: (query: GridQuery, signal: AbortSignal) => Promise<GridPage<Row>>, options: FetchingSourceOptions<Row>) {
        if (typeof fetch !== 'function') throw new TypeError('GridSource.fetch takes a function that answers a Query with a page.');
        const { readAheadRows = defaultReadAheadRows, distinctValues, find, summarize, rowKey, marks, onFetchFailed, clock = systemClock } = options;
        if (!Number.isSafeInteger(readAheadRows) || readAheadRows < 0) {
            throw new RangeError(`readAheadRows is a whole number of at least 0, not ${readAheadRows}.`);
        }
        if (marks !== undefined) checkedAdapter(marks);
        // The adapter's key is a Row Key (ADR-0140): one value names a row for its marks, for a source and for the
        // grid. Two different ones would let the marks follow one key and the grid pair rows by another.
        if (rowKey !== undefined && marks !== undefined && rowKey !== marks.key) {
            throw new TypeError('A Row Key and a Row Mark adapter with a key of its own were both given. The adapter\'s key is the '
                + 'Row Key (ADR-0140): give it once, as the adapter\'s, or hand the same function to both.');
        }
        this.#fetch = fetch;
        this.#readAheadRows = readAheadRows;
        this.#distinctValues = distinctValues;
        this.#onFetchFailed = onFetchFailed;
        this.#rowKey = rowKey ?? marks?.key;
        this.#live = this.#rowKey === undefined ? undefined : {
            gatherer: new ChangeGatherer(clock, () => { this.#gatherDue(); }),
            times: new CellChangeTimes(this.#rowKey, clock),
        };
        this.#marks = marks === undefined ? undefined : new FetchingRowMarks(this.#marksHost(), marks);
        if (find !== undefined) {
            this.find = async (request, signal) => {
                this.#requireUndisposed();
                return find(request, this.#inForce(), signal);
            };
        }
        if (summarize !== undefined) {
            this.summarize = async (request, signal) => {
                this.#requireUndisposed();
                return summarize(request, this.#inForce(), signal);
            };
        }
        this.#state = Object.freeze({
            window: Object.freeze([]),
            windowStart: 0,
            // Zero rather than unknown until the first answer: unknown would say this empty Window is the
            // whole result, and a later answer would contradict it (ADR-0001).
            totalCount: 0,
            loading: false,
            rowSequenceVersion: 0,
            sorts: Object.freeze([]),
            filter: undefined,
            lastError: undefined,
        });
    }

    /** A source that asks `fetch` for each page. */
    static fetch<Row>(fetch: (query: GridQuery, signal: AbortSignal) => Promise<GridPage<Row>>, options: FetchingSourceOptions<Row> = {}): FetchingGridSource<Row> {
        return new FetchingGridSource(fetch, options);
    }

    /** The Row Key the source refuses a repeated one by, where it was given one. */
    get rowKey(): RowKeyOf<Row> | undefined {
        return this.#rowKey;
    }

    /** True under a Row Key: an answer that repeats a key is refused by name, so no Window this source
     * hands over holds a row twice (ADR-0141). */
    get vouchesDistinctRows(): boolean {
        return this.#rowKey !== undefined;
    }

    /**
     * The Change Highlight of this source (ADR-0141, on ADR-0067's rules), to hand to the grid's
     * `cellChangedAt`; `undefined` without a Row Key. One function for the source's life.
     * - Every answer's rows are paired by key with the rows the source painted before it, and a cell is marked
     *   when its displayed text differs; a change the format hides is not marked.
     * - On an answer that carries a change of data, a row not painted before under a key the Consumer named as
     *   added (`notifyChanged(addedKeys)`) is marked whole, wherever it stands. When every notice heard since the
     *   last answer that carried a change named its added keys, a row under a key none named came into view by
     *   moving, and is not marked (D6).
     * - When a notice named nothing, the source guesses: a row not painted before is marked whole where it
     *   cannot have come into view by moving, between two rows painted before, or past an end of the result that
     *   the Window painted before also reached. A row at an edge of the Window may have slid in from beyond it,
     *   so it is not marked: a mark never appears on a value that did not change (ADR-0067).
     * - A scroll, a Sort or a Filter marks nothing new: a Sort or a Filter drops the Window.
     *
     * Nothing is compared until this is first read. A change time answers for `changeTimesKeptFor`, by key, so
     * a mark comes back with its row when the grid scrolls away and back.
     */
    get cellChangedAt(): CellChangedAt<Row> | undefined {
        return this.#live?.times.lookup;
    }

    /** How long, in milliseconds, a cell's change time is kept for `cellChangedAt`: a minute unless set; set it
     * to at least the grid's `changeHighlightDuration`. A fetching source never learns that a row it painted
     * was removed, it only stops painting it, so this bound is what keeps its memory to the rows that changed
     * lately. One not above zero is refused, and so is setting it without a Row Key. */
    get changeTimesKeptFor(): number {
        return this.#live === undefined ? defaultChangeTimesKeptFor : this.#live.times.keptFor;
    }

    set changeTimesKeptFor(milliseconds: number) {
        this.#requireLive('changeTimesKeptFor').times.keptFor = milliseconds;
    }

    /** The shortest time, in milliseconds, between two reads of the Window for data that moved on (ADR-0141,
     * ADR-0067): 250 unless set. Zero reads it for every notice, one question at a time. A negative interval
     * is refused, and so is setting it without a Row Key. */
    get gatherInterval(): number {
        return this.#live === undefined ? defaultGatherInterval : this.#live.gatherer.interval;
    }

    set gatherInterval(milliseconds: number) {
        const { gatherer } = this.#requireLive('gatherInterval');
        gatherer.interval = milliseconds;
        if (this.#changeWaiting && !this.#disposed && this.#inFlight === undefined && gatherer.shouldAskNow()) this.#askForChange();
    }

    /**
     * The Consumer says that the data behind this source moved on (ADR-0141, LV-8), however it learned it. The
     * source reads its Window again: at once when the data has been quiet for `gatherInterval`, else once at
     * the interval's end for every notice within it. A question already out is never called off for it; the
     * change is read when that one lands. A question for newer data does not raise `loading`: four reads a
     * second would flicker it over data that is still the newest the grid has (ADR-0067).
     *
     * `addedKeys` names the Row Keys of the rows the Consumer knows were added, a trade booked, and so that no
     * other key was (D6): a row under a named key that the read shows, not painted before, is marked whole
     * wherever it lands, an edge included. An empty list says nothing was added. Left out, the source guesses
     * at a row it was not painting (see `cellChangedAt`). Refused without a Row Key, and for a key that names
     * no row; nothing is heard then.
     */
    notifyChanged(addedKeys?: readonly RowKey[]): void {
        this.#requireLive('notifyChanged');
        // Taken now, and checked before anything is heard: the Consumer's list may change once this returns.
        const keys = addedKeys === undefined ? undefined : Array.from(addedKeys, (key, at) => checkedKey(key, `Added key ${at}`));
        if (this.#disposed) return;
        this.#notices++;
        if (keys === undefined) {
            this.#lastNameless = this.#notices;
        } else {
            const named = this.#named ?? new Map<RowKey, number>();
            this.#named = named;
            for (const key of keys) named.set(key, this.#notices);
        }
        this.#changeWaiting = true;
        this.#askIfDue();
    }

    #requireLive(member: string): { readonly gatherer: ChangeGatherer; readonly times: CellChangeTimes<Row> } {
        if (this.#live === undefined) {
            throw new Error(`${member} needs a Row Key, and this source was given none: GridSource.fetch(fetch, { rowKey }) takes one, `
                + 'or a Row Mark adapter\'s key serves. Without one, rows read again cannot be paired with the rows painted (ADR-0141).');
        }
        return this.#live;
    }

    // A change waits: asked for now when the gatherer says so and no question is out, whose landing asks again.
    #askIfDue(): void {
        const live = this.#live;
        if (live === undefined || !this.#changeWaiting || this.#disposed || this.#inFlight !== undefined) return;
        if (live.gatherer.shouldAskNow()) this.#askForChange();
    }

    #gatherDue(): void {
        this.#live?.gatherer.fired();
        this.#askIfDue();
    }

    /** Reads the Window again for data that moved on: the range the Window was asked as, wider than what came
     * back when the result ended inside it, so a row added at the end comes into view; or the first page when
     * there is no Window. Before the cold start there is nothing to read again: the first fetch reads the data
     * as it is then. */
    #askForChange(): void {
        if (!this.#started || this.#disposed) return;
        const wanted = this.#windowAsked ?? rowRange(0, this.#pageRows);
        const last = this.#lastNeeded;
        const needed = last !== undefined && last.start >= wanted.start && last.start < wanted.start + wanted.count ? last : rowRange(wanted.start, 1);
        this.#startDetached(wanted, needed, false, true);
    }

    /** The Row Marks of this source, where it was given a `RowMarkAdapter` (ADR-0043); otherwise `undefined`,
     * and a Mark Column bound to it is refused by name: the source receives new objects with every answer and
     * cannot count beyond its Window, so without the Consumer's key and server it could only guess. */
    get marks(): FetchingRowMarks<Row> | undefined {
        return this.#marks;
    }

    /** Hears every change to the snapshot; the answer stops hearing. */
    subscribe(listener: () => void): () => void {
        this.#listeners.add(listener);
        return () => this.#listeners.delete(listener);
    }

    /** What the source holds now: the rows of the last answer that landed, and where they start. */
    snapshot(): FetchingSourceState<Row> {
        return this.#state;
    }

    /** The columns are the grid's declaration, not part of the Query: a server is asked for a range under a
     * Sort and a Filter. Binding is when the first page is worth fetching, so the first binding fetches it,
     * unless a Sort or a Filter set before it already has. */
    setColumns(columns: readonly QueryColumn<Row>[]): void {
        const given: readonly QueryColumn<Row>[] = columns;
        if (!Array.isArray(columns)) throw new TypeError('setColumns takes the grid\'s columns.');
        // Kept for the Change Highlight, which compares the text the columns display; new columns mark nothing by
        // themselves (ADR-0067).
        this.#columns = Object.freeze(given.slice());
        if (this.#started || this.#disposed) return;
        this.#started = true;
        const first = rowRange(0, this.#pageRows);
        this.#startDetached(first, first, false);
        this.#recount();
    }

    /** Takes a new Sort. One equal to the Sort in force is no change; any other drops the Window, moves the
     * Row Sequence Version and fetches the first page under the new Query (ADR-0025). */
    setSorts(sorts: readonly SortSpec[]): void {
        checkedSorts(sorts);
        if (this.#disposed || sortsEqual(sorts, this.#state.sorts)) return;
        const kept = Object.freeze(sorts.map(sort => Object.freeze({ ...sort })));
        this.#restart({ sorts: kept });
    }

    /** Takes a new Filter, compared by what it says and kept as a copy, so the Consumer's own objects behind
     * it can change without reaching the source (ADR-0023). An equal one is no change; any other restarts,
     * as a new Sort does. */
    setFilter(filter: GridFilter | undefined): void {
        const given: unknown = filter;
        if (given !== undefined && (typeof given !== 'object' || given === null || typeof (given as { columns?: unknown }).columns !== 'object')) {
            throw new TypeError('A Filter is an object of columns, or undefined for none.');
        }
        if (this.#disposed || filtersEqual(filter, this.#state.filter)) return;
        this.#restart({ filter: snapshotFilter(filter) });
    }

    /** The Query's meaning changed outside its Sort and Filter: the fetch reads state of the Consumer's own
     * that has changed (ADR-0343). Drops the Window, moves the Row Sequence Version, so the grid drops its
     * selection (ADR-0011), and fetches the first page again. */
    requery(): void {
        if (this.#disposed) return;
        this.#restart({});
    }

    /**
     * The grid needs these rows. A range the Window already covers asks nothing; otherwise the range
     * widened by the read-ahead is fetched and supersedes whatever was in flight, unless it is that same
     * range, whose promise is handed back rather than asked again (ADR-0025). The promise rejects with a
     * failure nobody hears through `onFetchFailed`. A range that names no rows is refused as the call is
     * made.
     */
    needRange(range: RowRange): Promise<void> {
        const needed = rowRange(range.start, range.count);
        // The grid asks for what is on screen, so this is also how the source learns how tall the Viewport
        // is: what a restart fetches to fill it.
        this.#pageRows = Math.max(this.#pageRows, needed.count);
        const cold = !this.#started;
        this.#started = true;
        this.#lastNeeded = needed;
        if (this.#disposed || this.#covers(needed)) return Promise.resolve();
        // A grid that asks for rows before it binds its columns starts the source here, and the counts with it.
        if (cold) this.#recount();
        const wanted = this.#expand(needed);
        if (this.#inFlight !== undefined && this.#inFlight.start === wanted.start && this.#inFlight.count === wanted.count) {
            this.#inFlightNeeded = needed;
            return this.#current;
        }
        // Both ranges travel together: the widened one is what the server is asked for, and the grid's own
        // is what the answer has to reach. Judged by the widened start, a page reaching none of the rows the
        // grid needs would be taken.
        return this.#start(wanted, needed, false);
    }

    /** The rows of the result in the range, for a copy beyond the Window (ADR-0005): a one-off fetch under
     * the Query in force, trimmed to the range, and kept nowhere, so it cannot go stale as a Window can. */
    async rows(range: RowRange, signal: AbortSignal): Promise<readonly Row[]> {
        const asked = rowRange(range.start, range.count);
        const page = await this.#fetchOnce(asked, signal);
        // A server free to widen its page is not free to make the caller guess which rows were asked for, nor
        // to answer from elsewhere: a copy of the wrong rows would look like the right ones. An answer with no
        // rows because the result ended before the range is the truth, and a short one is the caller's to
        // judge, which a copy refuses (ADR-0005).
        if (!reaches(page, asked)) throw new Error(missed(asked, page));
        const skip = asked.start - page.start;
        return Object.freeze(page.rows.slice(skip, skip + asked.count));
    }

    /** A column's value list, answered by the Consumer's `distinctValues` under every Filter but the
     * column's own (ADR-0009), or too many without one. */
    async distinctValues(column: string, signal: AbortSignal): Promise<DistinctValues> {
        if (typeof column !== 'string' || column === '') throw new TypeError('A value list is asked of a column, by name.');
        this.#requireUndisposed();
        if (this.#distinctValues === undefined) return Object.freeze({ kind: 'tooMany' });
        return this.#distinctValues(column, filterWithout(this.#state.filter, column), signal);
    }

    /** Stops fetching and lets go of what is in flight: an answer that lands afterwards reaches nobody. The
     * grid does not own its source, so the Consumer that made it disposes it (ADR-0025). */
    dispose(): void {
        if (this.#disposed) return;
        this.#disposed = true;
        this.#controller?.abort();
        this.#lifetime.abort();
        this.#live?.gatherer.disarm();
    }

    /** One fetch under the Query in force, kept nowhere: for a copy beyond the Window, and for the marks. */
    async #fetchOnce(range: RowRange, signal: AbortSignal): Promise<GridPage<Row>> {
        this.#requireUndisposed();
        return checkedPage(await this.#fetch(Object.freeze({ range, ...this.#inForce() }), signal));
    }

    /** What the marks read of this source, and report to it. */
    #marksHost(): FetchingMarksHost<Row> {
        return {
            current: () => this.#state,
            fetchRange: async (range, signal) => {
                const page = await this.#fetchOnce(range, signal);
                // Rows the marks name by key are held to the key as a Window is: two rows under one would be marked
                // as one (ADR-0141).
                if (this.#rowKey !== undefined) checkKeys(page, this.#rowKey);
                // Trimmed to the asked range: a server free to widen its page is not free to make the caller guess
                // which of its rows were the ones asked for. One that does not reach the range answered none of it,
                // which the marks refuse as a short answer, naming how many it needed.
                const skip = range.start - page.start;
                const rows = skip < 0 || skip > page.rows.length ? [] : page.rows.slice(skip, skip + range.count);
                return Object.freeze({ rows: Object.freeze(rows), totalCount: page.totalCount });
            },
            signal: this.#lifetime.signal,
            reportFailure: error => this.#reportMarksFailure(error),
            marksFailure: () => this.#marksFailure,
            clearMarksFailure: seenAtStart => this.#clearMarksFailure(seenAtStart),
        };
    }

    /** The counts depend on the Filter and on what the server holds: asked for again, detached, whenever either
     * may have moved. A failure surfaces as a fetch's does, and the counts stay unknown rather than stale. */
    #recount(): void {
        const marks = this.#marks;
        if (marks === undefined || this.#disposed) return;
        // The recount reports its own failure; one nobody hears rejects unheard, and reaches the host.
        void marks.recount();
    }

    /** A failure of the Consumer's server met while keeping the marks, reported as a failed fetch is: held in
     * `lastError` either way, and handed to `onFetchFailed`. False when nobody hears it, and the caller throws it:
     * the failure must reach the host rather than vanish (ADR-0025). */
    #reportMarksFailure(error: unknown): boolean {
        if (this.#disposed) return false;
        this.#marksFailure = error;
        this.#state = Object.freeze({ ...this.#state, lastError: error });
        this.#notify();
        if (this.#onFetchFailed === undefined) return false;
        this.#onFetchFailed(Object.freeze({ error }));
        return true;
    }

    /** A marking went through: the failure the marks had reported when it began no longer describes the source.
     * Only that one: a failure reported while this marking was still counting is newer than its success, and
     * stands. */
    #clearMarksFailure(seenAtStart: unknown): void {
        if (seenAtStart === undefined || this.#marksFailure !== seenAtStart) return;
        this.#marksFailure = undefined;
        if (this.#state.lastError !== seenAtStart) return;
        this.#state = Object.freeze({ ...this.#state, lastError: undefined });
        this.#notify();
    }

    #inForce(): QueryInForce {
        return Object.freeze({ sorts: this.#state.sorts, filter: this.#state.filter });
    }

    #requireUndisposed(): void {
        if (this.#disposed) throw new Error('This source was disposed, and fetches nothing more (ADR-0025).');
    }

    #covers(range: RowRange): boolean {
        const { windowStart, window } = this.#state;
        return range.start >= windowStart && range.start + range.count <= windowStart + window.length;
    }

    #expand(range: RowRange): RowRange {
        const start = Math.max(0, range.start - this.#readAheadRows);
        let end = range.start + range.count + this.#readAheadRows;
        const total = this.#state.totalCount;
        if (total !== undefined && total > 0) end = Math.min(end, total);
        return rowRange(start, Math.max(1, end - start));
    }

    /**
     * A Sort, a Filter or a requery invalidates everything: the rows, their order and the total, which the
     * new Query has not counted yet. The Window is dropped, since under a new Filter the old rows are the
     * wrong ones, and the first page is fetched from the top. Heard whatever the loading flag was doing: a
     * change left to the loading flip lands invisibly while a fetch is in flight, which while scrolling it
     * usually is, and the old rows stay on screen under the new Query.
     */
    #restart(query: Partial<QueryInForce>): void {
        // A restart is the cold start as far as binding is concerned: a Sort restored before the grid binds
        // is fetched once, not again by the binding.
        this.#started = true;
        this.#orderToken = undefined;
        this.#answered = false;
        this.#windowAsked = undefined;
        this.#state = Object.freeze({
            ...this.#state,
            ...query,
            window: Object.freeze([]),
            windowStart: 0,
            totalCount: 0,
            rowSequenceVersion: this.#state.rowSequenceVersion + 1,
        });
        const first = rowRange(0, this.#pageRows);
        this.#startDetached(first, first, true);
        this.#recount();
    }

    /** A fetch nobody awaits, the source's own. A failure nobody hears rejects unheard, and so reaches the
     * host's error path rather than vanish. */
    #startDetached(wanted: RowRange, needed: RowRange, notify: boolean, quiet = false): void {
        void this.#start(wanted, needed, notify, quiet);
    }

    /** `quiet` is a read of the Window for data that moved on, which does not raise `loading`. */
    #start(wanted: RowRange, needed: RowRange, notify: boolean, quiet = false): Promise<void> {
        this.#controller?.abort();
        const controller = new AbortController();
        this.#controller = controller;
        // Any question asked after the data moved on answers with the newer data, so it carries the change, a
        // scroll or a Sort as much as a read of the Window for it (ADR-0067). One that supersedes a question that
        // carried it carries it on.
        const carries = this.#changeWaiting || (this.#inFlightCarries && this.#inFlight !== undefined);
        this.#inFlightCarries = carries;
        // Every notice heard so far is carried by it: what its landing lets go of (D6).
        if (carries) this.#noticesAsked = this.#notices;
        if (this.#changeWaiting) {
            this.#changeWaiting = false;
            this.#live?.gatherer.disarm();
        }
        this.#inFlight = wanted;
        const generation = ++this.#generation;
        // Heard only when the loading flag flips: a second range asked for while the first is in flight
        // moves nothing the grid paints (ADR-0023).
        const wasLoading = this.#state.loading;
        if (!quiet && !wasLoading) this.#state = Object.freeze({ ...this.#state, loading: true });
        this.#inFlightNeeded = needed;
        // Set going before anyone hears of it: a listener that asks for another range meanwhile supersedes
        // this question, and must find its own in flight once it returns, not this one.
        const run = this.#run(wanted, generation, controller, carries);
        this.#current = run;
        if (notify || (!quiet && !wasLoading)) this.#notify();
        return run;
    }

    async #run(wanted: RowRange, generation: number, controller: AbortController, carries: boolean): Promise<void> {
        try {
            const query: GridQuery = Object.freeze({ range: wanted, ...this.#inForce() });
            const page = await this.#fetch(query, controller.signal);
            // Late and superseded: a newer question was asked, and this answers the old one. Taken, it would
            // put rows on screen that nothing asked for (ADR-0001).
            if (generation !== this.#generation || this.#disposed) return;
            this.#apply(defined(this.#inFlightNeeded, 'the rows the question in flight needs'), checkedPage(page), carries);
            this.#windowAsked = wanted;
            this.#landed(carries);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- ADR-0025: a superseded failure is nobody's, and a heard one is reported and held in lastError
            if (generation !== this.#generation || this.#disposed) return;
            this.#inFlight = undefined;
            this.#inFlightNeeded = undefined;
            this.#state = Object.freeze({ ...this.#state, loading: false, lastError: error });
            this.#notify();
            // A change of data that could not be shown reached the screen as far as gathering is concerned
            // (ADR-0067): the next read waits the interval from here. Nothing retries it on its own (ADR-0025).
            this.#landed(carries);
            if (this.#onFetchFailed !== undefined) {
                this.#onFetchFailed(Object.freeze({ error }));
                return;
            }
            throw error;
        } finally {
            if (this.#controller === controller) this.#controller = undefined;
        }
    }

    /** Takes an answer that reaches `asked`, the rows the grid needs; `carries` when its question was asked
     * after the data moved on. */
    #apply(asked: RowRange, page: GridPage<Row>, carries: boolean): void {
        // A server free to clamp is not free to answer somewhere else: a page that does not reach the row
        // asked for leaves the Viewport on Placeholders, and the grid asks again for the same range and gets
        // the same answer. Refused by name rather than spun on. Answering short because the result shrank
        // past the asked position is legitimate, and says so by its own total.
        if (!reaches(page, asked)) throw new Error(missed(asked, page));
        const keys = this.#rowKey === undefined ? undefined : checkKeys(page, this.#rowKey);
        // A result that shrank means the positions name other rows, so the selection goes, as for a reorder
        // (ADR-0011); growing is safe. Only the server knows whether the order of the whole result moved,
        // and its order token says so (ADR-0141).
        let moved = this.#state.totalCount !== undefined && page.totalCount < this.#state.totalCount;
        // Only between answers to one Query: the first answer after a start or a restart moves the total off the 0
        // the restart left, and the restart has asked for the counts already.
        const totalMoved = this.#answered && this.#state.totalCount !== page.totalCount;
        if (this.#answered && page.orderToken !== this.#orderToken) moved = true;
        // A server that sends no token is taken to have moved the order with every change: comparing the Window
        // alone would miss a row cancelled after it.
        if (this.#answered && carries && page.orderToken === undefined) moved = true;
        if (keys !== undefined) this.#recordChanges(page, keys, carries);
        if (carries) this.#letGoOfNotices();
        this.#orderToken = page.orderToken;
        this.#answered = true;
        this.#inFlight = undefined;
        this.#inFlightNeeded = undefined;
        this.#state = Object.freeze({
            ...this.#state,
            window: Object.freeze([...page.rows]),
            windowStart: page.start,
            totalCount: page.totalCount,
            loading: false,
            // A page answers the fetch, not a count: a failure the marks reported stands until a count lands.
            lastError: this.#marksFailure,
            rowSequenceVersion: moved ? this.#state.rowSequenceVersion + 1 : this.#state.rowSequenceVersion,
        });
        this.#notify();
        // A result that grew or shrank changed what the counts are made of.
        if (totalMoved) this.#recount();
    }

    /** A question landed, answered or failed. One that carried a change of data brought it to the screen, or
     * found it unshowable, and the next read waits `gatherInterval` from here; a change heard meanwhile is asked
     * for then. */
    #landed(carried: boolean): void {
        const live = this.#live;
        if (live === undefined || this.#disposed) return;
        if (carried) live.gatherer.shown();
        this.#askIfDue();
    }

    /** The Change Highlight of one answer (ADR-0141, ADR-0067): its rows paired by key with the Window it
     * replaces, and the cells whose displayed text differs marked; on an answer that carries a change of data,
     * a row not painted before marked whole where it cannot have slid into view (see `cellChangedAt`). */
    #recordChanges(page: GridPage<Row>, keys: readonly RowKey[], carries: boolean): void {
        const live = this.#live;
        const rowKey = this.#rowKey;
        const { window, windowStart, totalCount } = this.#state;
        if (live === undefined || rowKey === undefined || !live.times.wanted || window.length === 0) return;
        const painted = new Map<RowKey, Row>();
        for (const row of window) {
            const key = rowKey(row);
            if (!painted.has(key)) painted.set(key, row);
        }
        const pairs: ChangedPair<Row>[] = [];
        const appeared: RowKey[] = [];
        // Whether the Window painted before reached an end of the result: nothing could slide in past that end,
        // so a row there was not painted because it was not there.
        const reachedTop = windowStart === 0 && page.start === 0;
        const reachedBottom = windowStart + window.length === totalCount && page.start + page.rows.length === page.totalCount;
        // What a row not painted before is taken to be (D6): named as added by a notice heard before this answer
        // landed, it appeared, wherever it stands. Otherwise it is guessed at only when a notice heard since the
        // last carrying answer named nothing; when every such notice named its keys, a key none named came into
        // view by moving.
        const named = carries ? this.#named : undefined;
        const guess = carries && this.#lastNameless > this.#noticesShown;
        let run = -1;
        for (let i = 0; i <= keys.length; i++) {
            const key = keys[i];
            if (key !== undefined && !painted.has(key)) {
                if (named?.has(key) === true) appeared.push(key);
                if (run < 0) run = i;
                continue;
            }
            if (key !== undefined) {
                // A row painted before is compared, named as added or not: the screen showed it before and after,
                // as GridSource.from compares a key removed and added again within one gathering.
                const old = defined(painted.get(key), 'the row painted before under its key');
                const row = defined(page.rows[i], 'the answer\'s row at its key');
                if (old !== row) pairs.push(Object.freeze({ key, old, new: row }));
            }
            if (run >= 0) {
                // A run of rows not painted before, from `run` to i - 1, and whether rows painted before stand on
                // both sides of it, or an end of the result both Windows reached. Its named rows are marked already.
                const above = run > 0 || reachedTop;
                const below = i < keys.length || reachedBottom;
                if (guess && above && below) {
                    for (const each of keys.slice(run, i)) {
                        if (named?.has(each) !== true) appeared.push(each);
                    }
                }
                run = -1;
            }
        }
        live.times.record(this.#columns, pairs, appeared, []);
    }

    /** A carrying answer landed: the notices its question was asked after are shown, and the names they gave
     * are let go. Names heard while it was out stay for the next read, which shows them if this one did not. */
    #letGoOfNotices(): void {
        this.#noticesShown = Math.max(this.#noticesShown, this.#noticesAsked);
        const named = this.#named;
        if (named === undefined) return;
        for (const [key, heard] of named) {
            if (heard <= this.#noticesShown) named.delete(key);
        }
        if (named.size === 0) this.#named = undefined;
    }

    #notify(): void {
        // A copy, so a listener that subscribes another meanwhile is heard from the next change on.
        for (const listener of Array.from(this.#listeners)) listener();
    }
}

/** An answer as the Consumer's server gave it, checked where it arrives: a page that contradicts itself
 * would be painted at a position nothing else agrees with (ADR-0001). */
function checkedPage<Row>(page: GridPage<Row>): GridPage<Row> {
    const answer: unknown = page;
    if (typeof answer !== 'object' || answer === null) throw new TypeError('A fetch answers with a page: its rows, their start and the total.');
    const { rows, start, totalCount, orderToken } = page;
    if (!Array.isArray(rows)) throw new TypeError('A page\'s rows are an array.');
    if (!Number.isSafeInteger(start) || start < 0) throw new RangeError(`A page starts at a whole number of at least 0, not ${String(start)}.`);
    if (!Number.isSafeInteger(totalCount) || totalCount < 0) throw new RangeError(`A page's total is a whole number of at least 0, not ${String(totalCount)}.`);
    if (start + rows.length > totalCount) {
        throw new RangeError(`A page of ${rows.length} rows at ${start} claims rows up to ${start + rows.length - 1}, past a total of ${totalCount}.`);
    }
    const token: unknown = orderToken;
    if (token !== undefined && typeof token !== 'string') throw new TypeError('A page\'s order token is a string.');
    return page;
}

/** Whether an answer reaches the first row asked for, or has none because the result ended before it. */
function reaches<Row>(page: GridPage<Row>, asked: RowRange): boolean {
    return page.start <= asked.start && (asked.start >= page.totalCount || page.start + page.rows.length > asked.start);
}

function missed<Row>(asked: RowRange, page: GridPage<Row>): string {
    return `The grid needs ${asked.count} rows at ${asked.start} and the answer is ${page.rows.length} rows at ${page.start} of `
        + `${page.totalCount}, which does not reach it. A source may answer with fewer rows than the read-ahead asked for, or `
        + 'with none when the result has shrunk past that position, but the rows the grid is about to paint have to be in it '
        + '(ADR-0025). A server that caps its pages below the read-ahead window will trip this.';
}

/** Each row's Row Key, in order; an answer in which a key repeats, or a row has none, is refused (ADR-0140,
 * ADR-0141). */
function checkKeys<Row>(page: GridPage<Row>, rowKey: RowKeyOf<Row>): readonly RowKey[] {
    const seen = new Map<RowKey, number>();
    const keys: RowKey[] = [];
    page.rows.forEach((row, at) => {
        const key = checkedKey(rowKey(row), `Row ${page.start + at} of the answer`);
        const before = seen.get(key);
        if (before !== undefined) {
            throw new Error(`Rows ${page.start + before} and ${page.start + at} of the answer both have the Row Key ${quoted(key)}. `
                + 'Two rows under one key would be painted as one, so the answer is refused (ADR-0140, ADR-0141).');
        }
        seen.set(key, at);
        keys.push(key);
    });
    return keys;
}
