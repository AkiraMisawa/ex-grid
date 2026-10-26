// `GridSource.fetch` hears that the data moved on (ADR-0141, LV-8): told so, it reads its Window again, gathered
// on its clock by the rules `GridSource.from` gathers by (LV-6), pairs the answer's rows with the painted ones by
// Row Key, marks the cells whose displayed text changed (LV-9), refuses an answer that repeats a key (LV-10), and
// moves the Row Sequence Version when the server's order token differs, or with every change when it sends none.
// The server is a fake that answers from a list it holds; the clock is a fake the tests drive. The C# core's
// counterpart is tests/ExGrid.Tests/LiveFetchTests.cs.
import { describe, expect, it } from 'vitest';
import type { QueryColumn } from '../src/query/columns';
import type { CellChangedAt } from '../src/source/cell-change-times';
import type { FetchingGridSource, GridPage, GridQuery } from '../src/source/fetching-source';
import { GridSource } from '../src/source/grid-source';
import { rowRange } from '../src/viewport/row-range';
import { FakeClock } from './fake-clock';

interface Deal {
    readonly id: string;
    readonly book: string;
    readonly amount: number;
}

const idColumn: QueryColumn<Deal> = { name: 'Id', type: 'text', value: deal => deal.id };
const bookColumn: QueryColumn<Deal> = { name: 'Book', type: 'text', value: deal => deal.book };
const amountColumn: QueryColumn<Deal> = {
    name: 'Amount', type: 'number', value: deal => deal.amount,
    format: value => (typeof value === 'number' ? value.toFixed(0) : String(value)),
};
const columns = [idColumn, bookColumn, amountColumn];

/** A server holding its rows in order, answering a range of them with its order token. Held, it keeps each
 * question until the test answers it. */
class Server {
    readonly rows: Deal[] = [];
    token: string | undefined = 'order-0';
    /** Every answer's rows as new objects, as rows read from JSON are. */
    fresh = false;
    holds = false;
    readonly asked: GridQuery[] = [];
    readonly held: { readonly query: GridQuery; readonly answer: (page: GridPage<Deal>) => void }[] = [];

    constructor(count: number) {
        for (let i = 0; i < count; i++) this.rows.push({ id: `T${String(i).padStart(4, '0')}`, book: 'Rates', amount: i });
    }

    readonly fetch = async (query: GridQuery): Promise<GridPage<Deal>> => {
        this.asked.push(query);
        if (!this.holds) return this.answer(query);
        return new Promise(resolve => { this.held.push({ query, answer: resolve }); });
    };

    answer(query: GridQuery): GridPage<Deal> {
        const start = Math.min(query.range.start, this.rows.length);
        const count = Math.min(query.range.count, this.rows.length - start);
        const rows = this.rows.slice(start, start + count).map(row => (this.fresh ? { ...row } : row));
        return { rows, start, totalCount: this.rows.length, ...(this.token === undefined ? {} : { orderToken: this.token }) };
    }

    answerHeld(): void {
        for (const { query, answer } of this.held.splice(0)) answer(this.answer(query));
    }

    change(at: number, change: (deal: Deal) => Deal): void {
        this.rows[at] = change(this.rows[at] as Deal);
    }

    /** A row booked at `at`, moving the order. */
    book(at: number, id: string): void {
        this.rows.splice(at, 0, { id, book: 'FX', amount: 5 });
        this.token = `order-${id}`;
    }
}

/** Lets the answers handed over so far land. */
async function settle(): Promise<void> {
    await new Promise(resolve => { setTimeout(resolve, 0); });
}

async function live(server: Server, clock: FakeClock): Promise<FetchingGridSource<Deal>> {
    const source = GridSource.fetch(server.fetch, { readAheadRows: 0, rowKey: deal => deal.id, clock });
    source.setColumns(columns);
    await settle();
    return source;
}

const window = (source: FetchingGridSource<Deal>): readonly Deal[] => source.snapshot().window;
const at = (source: FetchingGridSource<Deal>, index: number): Deal => {
    const row = window(source).at(index);
    if (row === undefined) throw new Error(`No row at ${index} of the Window.`);
    return row;
};

function expectWhole(changedAt: CellChangedAt<Deal>, row: Deal, time: number): void {
    expect(columns.map(column => changedAt(row, column.name))).toEqual([time, time, time]);
}

function expectUnmarked(changedAt: CellChangedAt<Deal>, row: Deal): void {
    expect(columns.map(column => changedAt(row, column.name))).toEqual([undefined, undefined, undefined]);
}

describe('a fetching source over live data', () => {
    it('ADR-0141 LV-8: without a Row Key a notice is refused by name, and there is no Change Highlight', () => {
        const source = GridSource.fetch(new Server(10).fetch);
        expect(() => { source.notifyChanged(); }).toThrow(/Row Key/);
        expect([source.cellChangedAt, source.vouchesDistinctRows]).toEqual([undefined, false]);
        expect(() => { source.gatherInterval = 0; }).toThrow(/Row Key/);
    });

    it('ADR-0141 LV-8: a notice reads the Window again, and the cells whose displayed text changed are marked', async () => {
        const clock = new FakeClock();
        const server = new Server(300);
        server.fresh = true;
        const source = await live(server, clock);
        const changedAt = source.cellChangedAt as CellChangedAt<Deal>;
        clock.advance(5000);
        server.change(3, deal => ({ ...deal, amount: 77 }));
        // A change the format hides: 4 becomes 4.2, displayed "4" either way.
        server.change(4, deal => ({ ...deal, amount: 4.2 }));

        source.notifyChanged();
        await settle();

        expect(server.asked.at(-1)?.range).toEqual(rowRange(0, 100));
        expect([at(source, 3).amount, changedAt(at(source, 3), 'Amount'), changedAt(at(source, 3), 'Book')]).toEqual([77, 5000, undefined]);
        expect(changedAt(at(source, 4), 'Amount')).toBeUndefined();
        // Every row is a new object, as from JSON: only the displayed text decides.
        expect(changedAt(at(source, 5), 'Amount')).toBeUndefined();
    });

    it('ADR-0141 LV-8 ADR-0011: the order token moves the version when it differs, and every change moves it when the server sends none', async () => {
        const server = new Server(300);
        const source = await live(server, new FakeClock());
        const version = source.snapshot().rowSequenceVersion;
        server.change(3, deal => ({ ...deal, amount: 77 }));
        source.notifyChanged();
        await settle();
        expect(source.snapshot().rowSequenceVersion).toBe(version);

        // A trade cancelled after the Window and another booked at the end: the Window's rows are the same,
        // and only the server knows the order moved. A scroll's answer is judged by the token as well.
        server.rows.splice(250, 1);
        server.book(server.rows.length, 'T9999');
        void source.needRange(rowRange(150, 20));
        await settle();
        expect(source.snapshot().rowSequenceVersion).toBe(version + 1);

        const tokenless = new Server(300);
        tokenless.token = undefined;
        const clock = new FakeClock();
        const plain = await live(tokenless, clock);
        tokenless.change(3, deal => ({ ...deal, amount: 77 }));
        plain.notifyChanged();
        await settle();
        expect(plain.snapshot().rowSequenceVersion).toBe(1);
        // A scroll is not a change of data, and moves nothing (ADR-0025).
        void plain.needRange(rowRange(150, 20));
        await settle();
        expect(plain.snapshot().rowSequenceVersion).toBe(1);
    });

    it('ADR-0141 LV-6: the first notice after a quiet interval reads at once, the ones within it once at its end, and the interval is settable', async () => {
        const clock = new FakeClock();
        const server = new Server(300);
        const source = await live(server, clock);
        const asked = server.asked.length;

        source.notifyChanged();
        expect(server.asked.length).toBe(asked + 1);
        await settle();
        clock.advance(100);
        source.notifyChanged();
        source.notifyChanged();
        clock.advance(149);
        expect(server.asked.length).toBe(asked + 1);
        clock.advance(1);
        expect(server.asked.length).toBe(asked + 2);
        await settle();

        source.gatherInterval = 0;
        clock.advance(1000);
        source.notifyChanged();
        await settle();
        source.notifyChanged();
        await settle();
        expect(server.asked.length).toBe(asked + 4);
        expect(() => { source.gatherInterval = -1; }).toThrow(RangeError);
    });

    it('ADR-0141 ADR-0067: a question out is not called off for a notice, and a read for newer data does not raise loading', async () => {
        const clock = new FakeClock();
        const server = new Server(300);
        const source = await live(server, clock);
        server.holds = true;
        void source.needRange(rowRange(150, 20));
        source.notifyChanged();
        expect(server.held).toHaveLength(1);

        clock.advance(1000);
        server.answerHeld();
        await settle();
        // The scroll landed, and the notice is asked for now, the interval having passed, without a loading flag.
        expect([server.held.map(held => held.query.range), source.snapshot().loading]).toEqual([[rowRange(150, 20)], false]);
    });

    it('ADR-0141 LV-10: an answer that repeats a key is refused by name, and the Window stays', async () => {
        const server = new Server(10);
        const failures: unknown[] = [];
        const source = GridSource.fetch(server.fetch, { readAheadRows: 0, rowKey: deal => deal.id, clock: new FakeClock(), onFetchFailed: ({ error }) => failures.push(error) });
        source.setColumns(columns);
        await settle();
        server.rows[5] = { ...(server.rows[2] as Deal), amount: 9 };

        source.notifyChanged();
        await settle();

        expect(String(failures[0])).toMatch(/Rows 2 and 5 .*'T0002'/);
        expect([window(source).length, at(source, 5).amount, source.vouchesDistinctRows]).toEqual([10, 5, true]);
    });

    it('ADR-0141 LV-9: a row that appears between rows painted before is marked whole, and one at the Window\'s edge is not', async () => {
        const clock = new FakeClock();
        const server = new Server(300);
        const source = await live(server, clock);
        const changedAt = source.cellChangedAt as CellChangedAt<Deal>;
        clock.advance(10);

        // Two rows cancelled near the top and one booked between T0009 and T0010: every row after moves up one,
        // and T0100 slides in at the Window's foot.
        server.rows.splice(2, 2);
        server.book(8, 'T0009b');
        source.notifyChanged();
        await settle();

        expect(at(source, 8).id).toBe('T0009b');
        expectWhole(changedAt, at(source, 8), 10);
        expect(at(source, -1).id).toBe('T0100');
        expectUnmarked(changedAt, at(source, -1));
    });

    it('ADR-0141 LV-9: a row added past the end both Windows reached is marked whole, and a mark comes back with its row', async () => {
        const clock = new FakeClock();
        const server = new Server(20);
        server.fresh = true;
        const source = await live(server, clock);
        const changedAt = source.cellChangedAt as CellChangedAt<Deal>;
        clock.advance(10);
        server.rows.push({ id: 'T0100', book: 'FX', amount: 5 });
        server.change(3, deal => ({ ...deal, amount: 77 }));
        server.token = 'order-1';
        source.notifyChanged();
        await settle();
        expect(changedAt(at(source, -1), 'Book')).toBe(10);

        void source.needRange(rowRange(15, 5));
        await settle();
        void source.needRange(rowRange(0, 5));
        await settle();
        expect([changedAt(at(source, 3), 'Amount'), changedAt(at(source, 2), 'Amount')]).toEqual([10, undefined]);
    });

    it('ADR-0141 LV-9: a Sort after a notice brings the change and marks nothing, and nothing more is read at the interval\'s end', async () => {
        const clock = new FakeClock();
        const server = new Server(300);
        const source = await live(server, clock);
        const changedAt = source.cellChangedAt as CellChangedAt<Deal>;
        source.notifyChanged();
        await settle();
        server.change(3, deal => ({ ...deal, amount: 77 }));
        source.notifyChanged();

        source.setSorts([{ column: 'Amount', direction: 'ascending' }]);
        await settle();
        expect(window(source).every(row => changedAt(row, 'Amount') === undefined)).toBe(true);
        const asked = server.asked.length;
        clock.advance(1000);
        expect(server.asked.length).toBe(asked);
    });

    it('ADR-0141 LV-9 (D6): a named key is marked whole wherever it lands; with names given, a key not named is not, and a painted row is compared', async () => {
        const clock = new FakeClock();
        const server = new Server(300);
        const source = await live(server, clock);
        const changedAt = source.cellChangedAt as CellChangedAt<Deal>;
        clock.advance(10);
        // Booked as the Window's last row, where a row may also have slid in from below.
        server.book(99, 'T0098b');
        source.notifyChanged(['T0098b']);
        await settle();
        expect(at(source, 99).id).toBe('T0098b');
        expectWhole(changedAt, at(source, 99), 10);

        clock.advance(1000);
        server.rows.splice(2, 2);
        server.book(8, 'T0009b');
        server.change(3, deal => ({ ...deal, amount: 77 }));
        source.notifyChanged([]);
        await settle();
        expect(at(source, 8).id).toBe('T0009b');
        expectUnmarked(changedAt, at(source, 8));
        // T0005 was painted before and moved up: its cells are compared.
        expect([at(source, 3).id, changedAt(at(source, 3), 'Amount'), changedAt(at(source, 3), 'Book')]).toEqual(['T0005', 1010, undefined]);
    });

    it('ADR-0141 LV-9 (D6): a nameless notice keeps the guess beside a named one, and a name the read does not show is let go', async () => {
        const clock = new FakeClock();
        const server = new Server(300);
        const source = await live(server, clock);
        const changedAt = source.cellChangedAt as CellChangedAt<Deal>;
        source.notifyChanged([]);
        await settle();
        clock.advance(100);

        server.book(8, 'T0007b');
        source.notifyChanged();
        server.book(99, 'T0097b');
        source.notifyChanged(['T0097b']);
        // Booked just past the Window's foot: the read does not show it.
        server.book(100, 'Z');
        source.notifyChanged(['Z']);
        clock.advance(150);
        await settle();
        expectWhole(changedAt, at(source, 8), 250);
        expectWhole(changedAt, at(source, 99), 250);
        expect(window(source).some(row => row.id === 'Z')).toBe(false);

        // A row cancelled at the top: Z slides into the Window's foot, and its name was let go.
        clock.advance(1000);
        server.rows.splice(0, 1);
        server.token = 'order-cancelled';
        source.notifyChanged([]);
        await settle();
        expect(at(source, 99).id).toBe('Z');
        expectUnmarked(changedAt, at(source, 99));
    });

    it('ADR-0141 LV-9 (D6): a name heard while a question is out applies to the answer that shows it', async () => {
        const clock = new FakeClock();
        const server = new Server(300);
        server.holds = true;
        const source = await live(server, clock);
        server.answerHeld();
        await settle();
        const changedAt = source.cellChangedAt as CellChangedAt<Deal>;

        // Not shown by the answer out, which the server made before it booked the row: the read after it
        // carries the name.
        source.notifyChanged([]);
        source.notifyChanged(['Q']);
        server.answerHeld();
        await settle();
        expect(window(source).some(row => row.id === 'Q')).toBe(false);
        server.book(60, 'Q');
        clock.advance(250);
        server.answerHeld();
        await settle();
        expectWhole(changedAt, at(source, 60), 250);
    });

    it('ADR-0141 LV-9 (D6): a key that names no row is refused, and nothing is heard', async () => {
        const server = new Server(10);
        const source = await live(server, new FakeClock());
        const asked = server.asked.length;
        expect(() => { source.notifyChanged(['T0001', null as unknown as string]); }).toThrow(/Added key 1/);
        expect(server.asked.length).toBe(asked);
    });
});
