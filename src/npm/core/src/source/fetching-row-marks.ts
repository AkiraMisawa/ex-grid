// The Row Marks of a `GridSource.fetch` source (ADR-0043), the counterpart of C#'s FetchingRowMarks. The source
// receives new row objects with every answer and cannot count beyond its Window, so the marks are kept as the
// steps that made them, keys and snapshots and never positions, and read per painted row through the Consumer's
// adapter. The counts come from the server; until it has answered they are unknown, and the grid claims nothing
// from them.

import { itemAt } from '../must';
import type { RowKind } from '../paint/row-classes';
import type { GridFilter } from '../query/filter';
import { lineUpMarks, RowMarkCounts, type RowMarkIntent, type RowMarks } from '../rows/row-marks';
import { RowMarkState, type RowMarkSnapshot, type RowMarkStep } from '../rows/row-mark-state';
import type { RowRange } from '../viewport/row-range';
import { checkedKey, type RowKey, type RowKeyOf } from './row-key';

/** What a fetching source needs from the Consumer to keep Row Marks (ADR-0043). A Mark Column over
 * `GridSource.fetch` without one is refused by name rather than painted with counts the source cannot know. */
export interface RowMarkAdapter<Row> {
    /** A row's key: equal for every object of the same row, and only for it. It is the source's Row Key
     * (ADR-0140). */
    readonly key: RowKeyOf<Row>;
    /** Asked when the header is pressed: the server's marker for "the result under this Filter, now". */
    readonly openSnapshot: (filter: GridFilter | undefined, signal: AbortSignal) => Promise<unknown>;
    /** Whether a row belonged to a snapshot. Asked for painted rows, so it answers from the row alone, a
     * creation stamp against the marker and the Filter, never with a round trip. */
    readonly belongsTo: (row: Row, snapshot: RowMarkSnapshot) => boolean;
    /** The counts for the header and the count display, answered by the server from the marks' steps and the
     * Filter in force. */
    readonly count: (state: RowMarkState, filter: GridFilter | undefined, signal: AbortSignal) => Promise<RowMarkCounts>;
}

/** What the marks read of, and report to, the source that holds them. */
export interface FetchingMarksHost<Row> {
    /** The Window, where it starts, the total, the Row Sequence Version and the Filter in force. */
    current(): {
        readonly window: readonly Row[];
        readonly windowStart: number;
        readonly totalCount: number | undefined;
        readonly rowSequenceVersion: number;
        readonly filter: GridFilter | undefined;
    };
    /** The rows of one range under the Query in force, trimmed to it, with the total the answer reported:
     * what tells a result that shrank under the range from a server that answered short. */
    fetchRange(range: RowRange, signal: AbortSignal): Promise<{ readonly rows: readonly Row[]; readonly totalCount: number }>;
    /** Aborted once the source is disposed. */
    readonly signal: AbortSignal;
    /** A failure of the Consumer's server met while keeping the marks, reported as a failed fetch is; false
     * when nobody hears it, and the caller throws it. */
    reportFailure(error: unknown): boolean;
    /** The failure the marks last reported, or `undefined`. */
    marksFailure(): unknown;
    /** A marking went through: the failure the marks had reported when it began no longer describes the
     * source, and is let go, if it is still the one held. */
    clearMarksFailure(seenAtStart: unknown): void;
}

type Entry = { readonly order: number; readonly step: RowMarkStep };
type AllOf = Extract<RowMarkStep, { kind: 'allOf' }>;

/** The Row Marks of a `GridSource.fetch` source given a `RowMarkAdapter` (ADR-0043). */
export class FetchingRowMarks<Row> implements RowMarks<Row> {
    readonly #host: FetchingMarksHost<Row>;
    readonly #adapter: RowMarkAdapter<Row>;
    readonly #listeners = new Set<() => void>();
    // Each key's one step and every snapshot, findable at once: a Space over half a million rows is half a million
    // steps, and neither adding one nor asking a painted row may walk them all. A step is ordered by when its
    // gesture was made, not when it finished: a header press waiting on the server's snapshot is older than a
    // checkbox pressed meanwhile, whatever the server's latency (spine 6). The snapshots are kept in that order.
    readonly #steps = new Map<RowKey | AllOf, Entry>();
    readonly #snapshots: { readonly order: number; readonly step: AllOf }[] = [];
    #order = 0;
    #rowKind: ((row: Row) => RowKind) | undefined;
    #state: RowMarkState = RowMarkState.empty;
    #counts: RowMarkCounts | undefined;
    #countGeneration = 0;

    /** The marks of the source behind `host`, through the Consumer's `adapter`. @internal */
    constructor(host: FetchingMarksHost<Row>, adapter: RowMarkAdapter<Row>) {
        this.#host = host;
        this.#adapter = adapter;
    }

    /** The marks as the steps that made them, oldest first: what the Consumer sends to its server to run an
     * action over them (ADR-0043). */
    get state(): RowMarkState {
        return this.#state;
    }

    /** The counts the server last answered, or `undefined` while it has not. */
    get counts(): RowMarkCounts | undefined {
        return this.#counts;
    }

    /** Whether a row is marked: the latest step covering it decides, as `RowMarkState.isMarked` does, made
     * without walking the keys. A Group or Total row never is (ADR-0024). */
    isMarked(row: Row): boolean {
        if (this.#steps.size === 0 || !this.#isDetail(row)) return false;
        const own = this.#steps.get(this.#keyOf(row));
        const order = own === undefined ? -1 : own.order;
        for (let i = this.#snapshots.length - 1; i >= 0; i--) {
            const snapshot = this.#snapshots[i];
            if (snapshot === undefined || snapshot.order <= order) break;
            if (this.#adapter.belongsTo(row, snapshot.step.snapshot)) return snapshot.step.marked;
        }
        return own !== undefined && own.step.marked;
    }

    /** The grid's Row Kind, so Group and Total rows are never marked. */
    setRowKind(rowKind: ((row: Row) => RowKind) | undefined): void {
        this.#rowKind = rowKind;
    }

    /** Hears that the marks or their counts changed; the answer stops hearing. */
    subscribe(listener: () => void): () => void {
        this.#listeners.add(listener);
        return () => this.#listeners.delete(listener);
    }

    /**
     * One marking gesture. What fails on the Consumer's server, the snapshot, a fetch past the Window or the
     * count, is reported as a failed fetch is (ADR-0025): to `onFetchFailed` where the source has it, and thrown
     * otherwise. An intent that is the caller's own mistake is thrown as the call is made, never reported as the
     * server failing.
     */
    async onMarkIntent(intent: RowMarkIntent<Row>): Promise<void> {
        checkedIntent(intent);
        const failureAtStart = this.#host.marksFailure();
        // Taken as the gesture is made: its steps stand where it was made among the others.
        const order = ++this.#order;
        try {
            let changed: boolean;
            switch (intent.kind) {
                case 'oneRow': changed = this.#markOne(intent.row, intent.marked, order); break;
                case 'allRows': changed = await this.#markResult(intent.marked, intent.rowSequenceVersion, order); break;
                case 'positions': changed = await this.#markPositions(intent.ranges, intent.rowSequenceVersion, order); break;
            }
            if (!changed) return;
            this.#state = new RowMarkState(Array.from(this.#steps.values()).toSorted((a, b) => a.order - b.order).map(entry => entry.step));
            this.#announce();
            await this.#count(failureAtStart);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- ADR-0025: a failure of the Consumer's server is reported to onFetchFailed, and thrown where nobody hears it; after dispose it is nobody's
            // The source's dispose aborts what the marks asked of the server: the gesture it ended belongs to nobody
            // now, as a detached count's does, rather than reaching the page as an unhandled rejection.
            if (this.#host.signal.aborted) return;
            if (!this.#host.reportFailure(error)) throw error;
        }
    }

    /**
     * Asks the server for the counts again: after the marks change, the Filter changes or the result grows,
     * and whenever the Consumer knows its data moved, since a row arriving on the server is news only the server
     * has. The counts are unknown until the answer lands; an answer overtaken by a newer question is discarded.
     */
    async recount(): Promise<void> {
        await this.#count(this.#host.marksFailure());
    }

    /** Counts again; landed, it lets go of the failure the marks had reported when the work it finishes began. A
     * failure reported since is newer than this success, and stands. */
    async #count(failureAtStart: unknown): Promise<void> {
        const generation = ++this.#countGeneration;
        if (this.#counts !== undefined) {
            this.#counts = undefined;
            this.#announce();
        }
        let counts: unknown;
        try {
            counts = await this.#adapter.count(this.#state, this.#host.current().filter, this.#host.signal);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- ADR-0025: a count a newer question overtook is nobody's, its failure as much as its answer
            // Reported, it would stand in lastError over the newer count's success.
            if (generation !== this.#countGeneration) return;
            throw error;
        }
        if (generation !== this.#countGeneration) return;
        if (!(counts instanceof RowMarkCounts)) {
            throw new TypeError('A Row Mark adapter\'s count answers with a RowMarkCounts, which refuses counts that contradict each other.');
        }
        this.#counts = counts;
        this.#announce();
        this.#host.clearMarksFailure(failureAtStart);
    }

    #announce(): void {
        for (const listener of Array.from(this.#listeners)) listener();
    }

    #markOne(row: Row, marked: boolean, order: number): boolean {
        if (!this.#isDetail(row)) return false;
        this.#append({ kind: 'oneKey', key: this.#keyOf(row), marked }, order);
        return true;
    }

    async #markResult(marked: boolean, version: number, order: number): Promise<boolean> {
        // "All" is the result the header was pressed over (ADR-0043): under another order or Filter it is not
        // what the user saw, so it is refused, and refused again if the Filter moved while the server was opening
        // the snapshot.
        if (version !== this.#host.current().rowSequenceVersion) return false;
        const { filter } = this.#host.current();
        const asOf = await this.#adapter.openSnapshot(filter, this.#host.signal);
        if (asOf === undefined) {
            throw new TypeError('A Row Mark adapter\'s openSnapshot answered nothing. Its marker is what later says which rows '
                + 'belonged to the result, and a snapshot with none would mark every row or none (ADR-0043).');
        }
        if (version !== this.#host.current().rowSequenceVersion) return false;
        this.#append({ kind: 'allOf', snapshot: Object.freeze({ filter, asOf }), marked }, order);
        return true;
    }

    async #markPositions(ranges: readonly RowRange[], version: number, order: number): Promise<boolean> {
        if (version !== this.#host.current().rowSequenceVersion) return false;
        // Past the Window the rows are fetched, as a copy beyond it fetches them (ADR-0005): positions become
        // keys here, under the order they were taken in.
        const rows: Row[] = [];
        for (const range of ranges) {
            const { window, windowStart, totalCount } = this.#host.current();
            if (range.start >= windowStart && range.start + range.count <= windowStart + window.length) {
                rows.push(...window.slice(range.start - windowStart, range.start + range.count - windowStart));
            } else {
                const answered = await this.#host.fetchRange(range, this.#host.signal);
                // Checked first: an answer that raced a reorder is a refusal, as every other gesture made under
                // an order that has since moved is.
                if (version !== this.#host.current().rowSequenceVersion) return false;
                // The result shrank under the range: the positions name other rows now, a reorder in all but the
                // version (ADR-0011), refused quietly as one is.
                if (totalCount !== undefined && answered.totalCount < totalCount) return false;
                // A range reaching past the end marks the rows that exist, as GridSource.from does. Fewer than
                // those is a server answering short: marking the part that came back would leave the rest of the
                // selection unmarked without saying so, so nothing is marked and the failure is reported (ADR-0043,
                // refusing whole as a copy past the Window does under ADR-0005).
                const expected = Math.max(0, Math.min(range.count, answered.totalCount - range.start));
                if (answered.rows.length < expected) {
                    throw new Error(`Marking ${expected} rows at ${range.start} needed them from the server, and it answered `
                        + `${answered.rows.length} of a result of ${answered.totalCount}. Nothing was marked: marking the part that `
                        + 'came back would leave the rest of the selection unmarked without saying so (ADR-0043, ADR-0005).');
                }
                rows.push(...answered.rows);
            }
            if (version !== this.#host.current().rowSequenceVersion) return false;
        }
        const detail = rows.filter(row => this.#isDetail(row));
        if (detail.length === 0) return false;
        const target = lineUpMarks(detail.filter(row => this.isMarked(row)).length, detail.length);
        for (const row of detail) this.#append({ kind: 'oneKey', key: this.#keyOf(row), marked: target }, order);
        return true;
    }

    /** Adds a step where its gesture was made; a key keeps only its latest step, so a row marked a thousand times
     * is one step, not a thousand, and a gesture that finished late never overrides a newer one's step. */
    #append(step: RowMarkStep, order: number): void {
        const entry = Object.freeze({ order, step: Object.freeze(step) });
        if (step.kind === 'oneKey') {
            const held = this.#steps.get(step.key);
            if (held !== undefined && held.order > order) return;
            this.#steps.set(step.key, entry);
            return;
        }
        this.#steps.set(step, entry);
        // Nearly always the newest, so the place is found from the end.
        let at = this.#snapshots.length;
        while (at > 0 && itemAt(this.#snapshots, at - 1).order > order) at--;
        this.#snapshots.splice(at, 0, { order, step });
    }

    #keyOf(row: Row): RowKey {
        return checkedKey(this.#adapter.key(row), 'A marked row');
    }

    #isDetail(row: Row): boolean {
        return this.#rowKind === undefined || this.#rowKind(row) === 'detail';
    }
}

/** The adapter as the Consumer gave it, checked where it arrives. */
export function checkedAdapter<Row>(adapter: RowMarkAdapter<Row>): RowMarkAdapter<Row> {
    const given: unknown = adapter;
    if (typeof given !== 'object' || given === null) throw new TypeError('A Row Mark adapter is an object of four functions (ADR-0043).');
    for (const member of ['key', 'openSnapshot', 'belongsTo', 'count'] as const) {
        if (typeof adapter[member] !== 'function') {
            throw new TypeError(`A Row Mark adapter's ${member} is a function (ADR-0043).`);
        }
    }
    return adapter;
}

// The Consumer's own mistakes, thrown as the call is made: a row or a list that is not there is its bug, never
// a server failure to report.
function checkedIntent<Row>(intent: RowMarkIntent<Row>): void {
    const given: unknown = intent;
    if (typeof given !== 'object' || given === null) throw new TypeError('A Row Mark intent is an object.');
    switch (intent.kind) {
        case 'oneRow':
            if (intent.row === undefined || intent.row === null) throw new TypeError('A oneRow intent names the row it marks.');
            return;
        case 'positions':
            if (!Array.isArray(intent.ranges)) throw new TypeError('A positions intent carries its ranges.');
            return;
        case 'allRows':
            return;
        default:
            throw new RangeError(`A Row Mark intent is oneRow, allRows or positions, not ${JSON.stringify((intent as { kind: unknown }).kind)}.`);
    }
}
