// Row Marks on the fetching source (ADR-0043): identity comes from the Consumer's key, "all" is a snapshot the
// server marks with its own token, and the counts come from the server, held back rather than guessed until it
// answers. Transcribed from the C# core's tests/ExGrid.Tests/RowMarkFetchTests.cs.
import { describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { QueryColumn } from '../src/query/columns';
import type { GridFilter } from '../src/query/filter';
import { applyQuery, filterPredicate } from '../src/query/query-engine';
import { rowMarkHeaderState, RowMarkCounts } from '../src/rows/row-marks';
import { RowMarkState, type RowMarkSnapshot } from '../src/rows/row-mark-state';
import type { FetchingRowMarks } from '../src/source/fetching-row-marks';
import type { FetchingGridSource, GridPage } from '../src/source/fetching-source';
import { GridSource } from '../src/source/grid-source';
import { rowRange } from '../src/viewport/row-range';

interface Deal {
    readonly id: number;
    readonly stamp: number;
    readonly amount: number;
}

const columns: readonly QueryColumn<Deal>[] = [
    { name: 'Id', type: 'number', value: deal => deal.id },
    { name: 'Amount', type: 'number', value: deal => deal.amount },
];

/** Lets every promise a step set going run. */
async function settle(): Promise<void> {
    await new Promise(resolve => { setTimeout(resolve, 0); });
}

/**
 * A server over a list that grows: every answer is a new object for each row, as a real fetch returns. A
 * snapshot's token is the highest stamp at the moment; a row belongs to it when it was stamped by then and
 * matches its Filter.
 */
class Server {
    readonly rows: Deal[] = [];
    heldCount: Promise<RowMarkCounts> | undefined;
    heldSnapshot: Promise<unknown> | undefined;
    counted = 0;
    pageCap = Number.MAX_SAFE_INTEGER;
    #stamp = 0;

    constructor(rows: number) {
        for (let i = 0; i < rows; i++) this.add(i);
    }

    add(amount: number): void {
        this.rows.push({ id: this.rows.length, stamp: ++this.#stamp, amount });
    }

    removeLast(count: number): void {
        this.rows.splice(this.rows.length - count, count);
    }

    async source(options: { readonly withAdapter?: boolean; readonly failures?: unknown[] } = {}): Promise<FetchingGridSource<Deal>> {
        const { withAdapter = true, failures } = options;
        const source = GridSource.fetch<Deal>(query => {
            const result = this.#result(query.filter);
            const start = Math.min(query.range.start, result.length);
            const count = Math.min(query.range.count, this.pageCap, result.length - start);
            const rows = result.slice(start, start + count).map(deal => ({ ...deal }));
            return Promise.resolve({ rows, start, totalCount: result.length });
        }, {
            readAheadRows: 0,
            ...(withAdapter ? {
                marks: {
                    key: deal => deal.id,
                    openSnapshot: () => this.heldSnapshot ?? Promise.resolve(this.#stamp),
                    belongsTo: (row, snapshot) => this.#belongsTo(row, snapshot),
                    count: (state, filter) => this.#count(state, filter),
                },
            } : {}),
            ...(failures === undefined ? {} : { onFetchFailed: ({ error }) => { failures.push(error); } }),
        });
        source.setColumns(columns);
        await settle();
        return source;
    }

    #result(filter: GridFilter | undefined): readonly Deal[] {
        return filter === undefined ? this.rows : applyQuery(this.rows, columns, filter, undefined);
    }

    #belongsTo(row: Deal, snapshot: RowMarkSnapshot): boolean {
        return row.stamp <= (snapshot.asOf as number)
            && (snapshot.filter === undefined || filterPredicate(columns, snapshot.filter)(row));
    }

    #count(state: RowMarkState, filter: GridFilter | undefined): Promise<RowMarkCounts> {
        this.counted++;
        if (this.heldCount !== undefined) return this.heldCount;
        const result = this.#result(filter);
        const isMarked = (deal: Deal): boolean => state.isMarked(deal.id, snapshot => this.#belongsTo(deal, snapshot));
        const inResult = result.filter(isMarked).length;
        const total = this.rows.filter(isMarked).length;
        return Promise.resolve(new RowMarkCounts({ markedInResult: inResult, rowsInResult: result.length, markedOutsideResult: total - inResult }));
    }
}

/** A count the test answers by hand. */
function heldCounts(): { readonly promise: Promise<RowMarkCounts>; readonly resolve: (counts: RowMarkCounts) => void } {
    let resolve: ((counts: RowMarkCounts) => void) | undefined;
    const promise = new Promise<RowMarkCounts>(settled => { resolve = settled; });
    return { promise, resolve: defined(resolve, 'the resolver a promise hands over at once') };
}

function marksOf(source: FetchingGridSource<Deal>): FetchingRowMarks<Deal> {
    return defined(source.marks, 'the marks of a source given an adapter');
}

function windowRow(source: FetchingGridSource<Deal>, id: number): Deal {
    return defined(source.snapshot().window.find(deal => deal.id === id), `row ${id} in the Window`);
}

function counts(markedInResult: number, rowsInResult: number, markedOutsideResult: number): RowMarkCounts {
    return new RowMarkCounts({ markedInResult, rowsInResult, markedOutsideResult });
}

const emptyFetch = (): Promise<GridPage<Deal>> => Promise.resolve({ rows: [], start: 0, totalCount: 0 });

const amountAtLeast = (amount: number): GridFilter => ({ columns: { Amount: { clauses: [{ operator: 'greaterThanOrEqual', value: amount }] } } });

describe('Row Marks on GridSource.fetch (ADR-0043)', () => {
    it('ADR-0043: without an adapter the fetching source keeps no marks, and a Mark Column is refused', async () => {
        const source = await new Server(5).source({ withAdapter: false });

        expect(source.marks).toBeUndefined();
    });

    it('ADR-0043: an adapter whose key differs from the Row Key given beside it is refused by name', () => {
        const adapter = { key: (deal: Deal) => deal.id, openSnapshot: () => Promise.resolve(1), belongsTo: () => true, count: () => Promise.resolve(counts(0, 0, 0)) };

        expect(() => GridSource.fetch<Deal>(emptyFetch, { rowKey: deal => deal.id, marks: adapter })).toThrow(/ADR-0140/u);
        expect(GridSource.fetch<Deal>(emptyFetch, { rowKey: adapter.key, marks: adapter }).rowKey).toBe(adapter.key);
        expect(GridSource.fetch<Deal>(emptyFetch, { marks: adapter }).vouchesDistinctRows).toBe(true);
    });

    it('ADR-0043: a mark follows the row\'s key to a new object', async () => {
        const source = await new Server(5).source();
        const marks = marksOf(source);

        await marks.onMarkIntent({ kind: 'oneRow', row: windowRow(source, 2), marked: true });
        source.setSorts([{ column: 'Amount', direction: 'descending' }]);
        await settle();

        expect(marks.isMarked(windowRow(source, 2))).toBe(true);
        expect(marks.isMarked(windowRow(source, 3))).toBe(false);
    });

    it('ADR-0043 (MK-6): after "all", a row fetched for the first time answers marked', async () => {
        const source = await new Server(1_000).source();
        const marks = marksOf(source);

        await marks.onMarkIntent({ kind: 'allRows', marked: true, rowSequenceVersion: source.snapshot().rowSequenceVersion });
        await source.needRange(rowRange(900, 20));

        expect(source.snapshot().window.every(row => marks.isMarked(row))).toBe(true);
        expect(marks.counts).toEqual(counts(1_000, 1_000, 0));
    });

    it('ADR-0043 (MK-8): a row the server adds after "all" is not marked, and the counts turn to "some"', async () => {
        const server = new Server(10);
        const source = await server.source();
        const marks = marksOf(source);
        await marks.onMarkIntent({ kind: 'allRows', marked: true, rowSequenceVersion: source.snapshot().rowSequenceVersion });

        server.add(99);
        await marks.recount();
        await source.needRange(rowRange(10, 1));

        expect(marks.isMarked(windowRow(source, 10))).toBe(false);
        expect(rowMarkHeaderState(defined(marks.counts, 'the counts'))).toBe('some');
    });

    it('ADR-0043 (MK-7): narrowing the Filter recounts, naming the marks outside it', async () => {
        const source = await new Server(10).source();
        const marks = marksOf(source);
        await marks.onMarkIntent({ kind: 'allRows', marked: true, rowSequenceVersion: source.snapshot().rowSequenceVersion });

        source.setFilter(amountAtLeast(6));
        await settle();

        expect(marks.counts).toEqual(counts(4, 4, 6));
    });

    it('ADR-0043: until the server has counted, the counts are unknown rather than guessed', async () => {
        const server = new Server(10);
        const source = await server.source();
        const marks = marksOf(source);
        const held = heldCounts();
        server.heldCount = held.promise;

        const marking = marks.onMarkIntent({ kind: 'oneRow', row: windowRow(source, 0), marked: true });
        await settle();

        expect(marks.counts).toBeUndefined();
        held.resolve(counts(1, 10, 0));
        await marking;
        expect(marks.counts).toEqual(counts(1, 10, 0));
    });

    it('ADR-0043 (MK-3): positions under a stale Row Sequence Version mark nothing', async () => {
        const source = await new Server(10).source();
        const marks = marksOf(source);
        const version = source.snapshot().rowSequenceVersion;
        source.setSorts([{ column: 'Amount', direction: 'descending' }]);
        await settle();

        await marks.onMarkIntent({ kind: 'positions', ranges: [rowRange(0, 3)], rowSequenceVersion: version });

        expect(marks.state).toBe(RowMarkState.empty);
    });

    it('ADR-0043 (MK-1): positions beyond the Window are fetched and lined up', async () => {
        const source = await new Server(1_000).source();
        const marks = marksOf(source);
        await marks.onMarkIntent({ kind: 'oneRow', row: windowRow(source, 5), marked: true });

        await marks.onMarkIntent({ kind: 'positions', ranges: [rowRange(0, 10), rowRange(500, 5)], rowSequenceVersion: source.snapshot().rowSequenceVersion });

        expect(marks.counts).toEqual(counts(15, 1_000, 0));
        await source.needRange(rowRange(500, 5));
        expect(source.snapshot().window.every(row => marks.isMarked(row))).toBe(true);
    });

    it('ADR-0043: the marks an action runs over are keys and snapshots, never positions', async () => {
        const source = await new Server(10).source();
        const marks = marksOf(source);

        await marks.onMarkIntent({ kind: 'allRows', marked: true, rowSequenceVersion: source.snapshot().rowSequenceVersion });
        await marks.onMarkIntent({ kind: 'oneRow', row: windowRow(source, 3), marked: false });

        expect(marks.state.steps).toEqual([
            { kind: 'allOf', snapshot: { filter: undefined, asOf: 10 }, marked: true },
            { kind: 'oneKey', key: 3, marked: false },
        ]);
    });

    it('ADR-0043: a key marked many times over is one step, and marking many keys stays linear', async () => {
        const source = await new Server(50_000).source();
        const marks = marksOf(source);
        const version = source.snapshot().rowSequenceVersion;

        await marks.onMarkIntent({ kind: 'positions', ranges: [rowRange(0, 49_000)], rowSequenceVersion: version });
        await marks.onMarkIntent({ kind: 'positions', ranges: [rowRange(0, 49_000)], rowSequenceVersion: version });

        // One step per key, whatever it went through: never a step per gesture.
        expect(marks.state.steps).toHaveLength(49_000);
        expect(marks.state.steps.every(step => !step.marked)).toBe(true);
    });

    it('ADR-0043/0025: a failed count reaches onFetchFailed, never the gesture that caused it', async () => {
        const server = new Server(10);
        const failures: unknown[] = [];
        const source = await server.source({ failures });
        const marks = marksOf(source);
        server.heldCount = Promise.reject(new Error('the count failed'));

        await marks.onMarkIntent({ kind: 'oneRow', row: windowRow(source, 0), marked: true });

        expect(failures).toHaveLength(1);
        expect((failures[0] as Error).message).toBe('the count failed');
        expect(source.snapshot().lastError).toBe(failures[0]);
        expect(marks.isMarked(windowRow(source, 0))).toBe(true);
        expect(marks.counts).toBeUndefined();
    });

    it('ADR-0043: a failed count with nobody hearing it is thrown back to the gesture', async () => {
        const server = new Server(10);
        const source = await server.source();
        const marks = marksOf(source);
        server.heldCount = Promise.reject(new Error('the count failed'));

        await expect(marks.onMarkIntent({ kind: 'oneRow', row: windowRow(source, 0), marked: true })).rejects.toThrow('the count failed');
    });

    it('ADR-0043: a Consumer\'s own missing row or ranges is thrown back, never reported as the server failing', async () => {
        const failures: unknown[] = [];
        const source = await new Server(10).source({ failures });
        const marks = marksOf(source);
        const version = source.snapshot().rowSequenceVersion;

        await expect(marks.onMarkIntent({ kind: 'oneRow', row: undefined as unknown as Deal, marked: true })).rejects.toThrow(TypeError);
        await expect(marks.onMarkIntent({ kind: 'positions', ranges: undefined as unknown as [], rowSequenceVersion: version })).rejects.toThrow(TypeError);

        expect(failures).toHaveLength(0);
        expect(source.snapshot().lastError).toBeUndefined();
    });

    it('ADR-0043/0005: a short answer for positions past the Window marks nothing, and says so', async () => {
        const server = new Server(1_000);
        const failures: unknown[] = [];
        const source = await server.source({ failures });
        const marks = marksOf(source);
        server.pageCap = 50;

        await marks.onMarkIntent({ kind: 'positions', ranges: [rowRange(0, 10), rowRange(500, 200)], rowSequenceVersion: source.snapshot().rowSequenceVersion });

        expect(marks.state).toBe(RowMarkState.empty);
        expect(failures).toHaveLength(1);
        expect(String(failures[0])).toContain('200');
    });

    it('ADR-0043/0011: a result that shrank under the positions is a reorder, refused quietly rather than reported', async () => {
        const server = new Server(1_000);
        const failures: unknown[] = [];
        const source = await server.source({ failures });
        const marks = marksOf(source);
        server.removeLast(50);

        await marks.onMarkIntent({ kind: 'positions', ranges: [rowRange(900, 100)], rowSequenceVersion: source.snapshot().rowSequenceVersion });

        expect(marks.state).toBe(RowMarkState.empty);
        expect(failures).toHaveLength(0);
        expect(source.snapshot().lastError).toBeUndefined();
    });

    it('ADR-0043: a range reaching past the end marks the rows that exist, as GridSource.from does', async () => {
        const source = await new Server(1_000).source();
        const marks = marksOf(source);

        await marks.onMarkIntent({ kind: 'positions', ranges: [rowRange(990, 20)], rowSequenceVersion: source.snapshot().rowSequenceVersion });

        expect(marks.state.steps).toHaveLength(10);
    });

    it('ADR-0043/0025: a failure met while marking is let go by the next marking that succeeds', async () => {
        const server = new Server(1_000);
        const source = await server.source({ failures: [] });
        const marks = marksOf(source);
        server.pageCap = 50;
        await marks.onMarkIntent({ kind: 'positions', ranges: [rowRange(500, 200)], rowSequenceVersion: source.snapshot().rowSequenceVersion });
        expect(source.snapshot().lastError).toBeDefined();

        await marks.onMarkIntent({ kind: 'oneRow', row: windowRow(source, 0), marked: true });

        expect(source.snapshot().lastError).toBeUndefined();
    });

    it('ADR-0043/0025: a marking that finishes late never lets go of a failure reported after it began', async () => {
        const server = new Server(1_000);
        const source = await server.source({ failures: [] });
        const marks = marksOf(source);
        const held = heldCounts();
        server.heldCount = held.promise;
        const slow = marks.onMarkIntent({ kind: 'oneRow', row: windowRow(source, 0), marked: true });
        await settle();

        server.heldCount = undefined;
        server.pageCap = 50;
        await marks.onMarkIntent({ kind: 'positions', ranges: [rowRange(500, 200)], rowSequenceVersion: source.snapshot().rowSequenceVersion });
        const newer = source.snapshot().lastError;
        held.resolve(counts(1, 1_000, 0));
        await slow;

        expect(newer).toBeDefined();
        expect(source.snapshot().lastError).toBe(newer);
    });

    it('ADR-0043/0025: a count a newer one overtook is nobody\'s, its failure as much as its answer', async () => {
        const server = new Server(10);
        const failures: unknown[] = [];
        const source = await server.source({ failures });
        const marks = marksOf(source);
        let fail: ((error: Error) => void) | undefined;
        server.heldCount = new Promise<RowMarkCounts>((_, reject) => { fail = reject; });
        const overtaken = marks.recount();

        server.heldCount = undefined;
        await marks.recount();
        defined(fail, 'the rejecter a promise hands over at once')(new Error('the old count failed'));
        await overtaken;

        expect(failures).toHaveLength(0);
        expect(source.snapshot().lastError).toBeUndefined();
        expect(marks.counts).toEqual(counts(0, 10, 0));
    });

    it('ADR-0043/0025: a marking the source\'s dispose cut short is nobody\'s, never an unhandled rejection', async () => {
        const failures: unknown[] = [];
        const source = GridSource.fetch<Deal>(emptyFetch, {
            onFetchFailed: ({ error }) => { failures.push(error); },
            marks: {
                key: deal => deal.id,
                openSnapshot: (_, signal) => new Promise((_resolve, reject) => {
                    signal.addEventListener('abort', () => { reject(new Error('the snapshot was called off')); }, { once: true });
                }),
                belongsTo: () => true,
                count: () => Promise.resolve(counts(0, 0, 0)),
            },
        });
        source.setColumns(columns);
        await settle();
        const marks = marksOf(source);
        const marking = marks.onMarkIntent({ kind: 'allRows', marked: true, rowSequenceVersion: source.snapshot().rowSequenceVersion });

        source.dispose();

        await expect(marking).resolves.toBeUndefined();
        expect(failures).toHaveLength(0);
        expect(marks.state).toBe(RowMarkState.empty);
    });

    it('ADR-0043 (spine 6): a step stands where its gesture was made, so a "mark all" waiting on the server never overrides a later checkbox', async () => {
        const server = new Server(10);
        const source = await server.source();
        const marks = marksOf(source);
        let open: ((asOf: unknown) => void) | undefined;
        server.heldSnapshot = new Promise(resolve => { open = resolve; });

        const all = marks.onMarkIntent({ kind: 'allRows', marked: true, rowSequenceVersion: source.snapshot().rowSequenceVersion });
        await settle();
        await marks.onMarkIntent({ kind: 'oneRow', row: windowRow(source, 5), marked: false });
        defined(open, 'the held snapshot\'s resolver')(10);
        await all;

        expect(marks.isMarked(windowRow(source, 5))).toBe(false);
        expect(marks.isMarked(windowRow(source, 4))).toBe(true);
        expect(marks.state.steps.map(step => step.kind)).toEqual(['allOf', 'oneKey']);
        expect(marks.counts).toEqual(counts(9, 10, 0));
    });

    it('ADR-0043: a new Filter asks the server to count once, not again when its first page lands', async () => {
        const server = new Server(10);
        const source = await server.source();
        const before = server.counted;

        source.setFilter(amountAtLeast(6));
        await settle();

        expect(server.counted - before).toBe(1);
    });

    it('ADR-0043/0025: a failure met while marking is held in lastError even with nobody hearing it', async () => {
        const server = new Server(10);
        const source = await server.source();
        const marks = marksOf(source);
        server.heldCount = Promise.reject(new Error('the count failed'));

        await expect(marks.onMarkIntent({ kind: 'oneRow', row: windowRow(source, 0), marked: true })).rejects.toThrow('the count failed');

        expect(source.snapshot().lastError).toBeInstanceOf(Error);
    });

    it('ADR-0043/0025: a count the Consumer asks for, landing, lets go of the failure a count before it reported', async () => {
        const server = new Server(10);
        const source = await server.source({ failures: [] });
        const marks = marksOf(source);
        server.heldCount = Promise.reject(new Error('the count failed'));
        await marks.onMarkIntent({ kind: 'oneRow', row: windowRow(source, 0), marked: true });
        expect(source.snapshot().lastError).toBeDefined();

        server.heldCount = undefined;
        await marks.recount();

        expect(source.snapshot().lastError).toBeUndefined();
        expect(marks.counts).toEqual(counts(1, 10, 0));
    });

    it('ADR-0043/0025: a count the Consumer asks for that fails is reported as a gesture\'s is, and thrown only where nobody hears it', async () => {
        const server = new Server(10);
        const failures: unknown[] = [];
        const heard = marksOf(await server.source({ failures }));
        const unheard = await server.source();
        server.heldCount = Promise.reject(new Error('the count failed'));
        await heard.recount();
        expect(failures.map(failure => (failure as Error).message)).toEqual(['the count failed']);

        await expect(marksOf(unheard).recount()).rejects.toThrow('the count failed');
        expect(unheard.snapshot().lastError).toBeInstanceOf(Error);
    });

    it('ADR-0043/0025: a page landing answers the fetch, not the count: the marks\' failure stands in lastError', async () => {
        const server = new Server(1000);
        const source = await server.source({ failures: [] });
        const marks = marksOf(source);
        server.heldCount = Promise.reject(new Error('the count failed'));
        await marks.onMarkIntent({ kind: 'oneRow', row: windowRow(source, 0), marked: true });

        await source.needRange(rowRange(900, 20));

        expect([source.snapshot().window.length > 0, (source.snapshot().lastError as Error).message]).toEqual([true, 'the count failed']);
        expect(marks.counts).toBeUndefined();
    });

    it('ADR-0043: RowMarkState reads a key\'s latest step against the snapshots taken after it', () => {
        const state = new RowMarkState([
            { kind: 'oneKey', key: 1, marked: true },
            { kind: 'allOf', snapshot: { filter: undefined, asOf: 5 }, marked: false },
            { kind: 'oneKey', key: 2, marked: true },
        ]);

        expect(state.isMarked(1, () => true)).toBe(false);
        expect(state.isMarked(1, () => false)).toBe(true);
        expect(state.isMarked(2, () => true)).toBe(true);
        expect(state.isMarked(3, () => false)).toBe(false);
    });
});
