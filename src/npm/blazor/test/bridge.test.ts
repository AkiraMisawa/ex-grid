// @vitest-environment happy-dom
// The bridge's own tests (ADR-0350 section 13): what crosses between .NET and the Script Core. What the grid
// paints and decides is the core's, tested there; here, that .NET's values arrive as the values it held, that a
// row sent again unchanged is the same row (ADR-0003), that a row the browser let go is let go in .NET only once
// .NET has not sent it again, and that what the grid tells its source and its Consumer reaches .NET in order.
import { afterEach, describe, expect, it } from 'vitest';
import { attach, type BlazorGrid, type WireAttach, type WireOptions } from '../src/blazor-grid';
import { DotNetSource, type DotNetHandle, type WireState } from '../src/dotnet-source';
import { ColumnSet, RowSet, type BlazorRow, type RowCollector, type WireColumn, type WireRow } from '../src/wire-rows';
import { operandOf } from '../src/wire-query';
import { valueKey, valueOf } from '../src/wire-values';
import { WriteGate, type Sent } from '../src/write-gate';

/** .NET, as the bridge calls it: every call recorded, and answered by `answer` where the test gives one. */
class FakeDotNet implements DotNetHandle {
    readonly calls: { readonly method: string; readonly args: readonly unknown[] }[] = [];
    answer: (method: string, args: readonly unknown[]) => unknown = () => null;

    invokeMethodAsync(method: string, ...args: readonly unknown[]): Promise<unknown> {
        this.calls.push({ method, args });
        return Promise.resolve(this.answer(method, args));
    }

    heard(kind: string): readonly unknown[] {
        return this.calls.filter(call => call.method === 'Heard' && call.args[0] === kind).map(call => call.args[1]);
    }
}

/** A collector the test drives: `collect` says the browser collected a row. */
class FakeCollector implements RowCollector {
    readonly #rows: { readonly row: WeakRef<BlazorRow>; readonly held: number }[] = [];
    #collected: (held: number) => void = held => { throw new Error(`Row object ${held} collected before the collector was made.`); };

    make = (collected: (held: number) => void): RowCollector => {
        this.#collected = collected;
        return this;
    };

    register(row: BlazorRow, held: number): void {
        this.#rows.push({ row: new WeakRef(row), held });
    }

    /** Hands `see` every row registered and not yet collected. */
    each(see: (row: BlazorRow) => void): void {
        for (const held of this.#rows) {
            const row = held.row.deref();
            if (row !== undefined) see(row);
        }
    }

    collect(row: BlazorRow): void {
        const at = this.#rows.findIndex(held => held.row.deref() === row);
        if (at < 0) throw new Error('The row was never registered.');
        const [registered] = this.#rows.splice(at, 1);
        if (registered === undefined) throw new Error('The row was never registered.');
        this.#collected(registered.held);
    }
}

const book: WireColumn = {
    name: 'Book', type: 'text', header: 'Book', width: { px: 120, minPx: 40, maxPx: 400 }, editable: true, align: 'auto', headerAlign: 'auto',
    filterUi: 'condition', dateType: 'dateTime', queryable: true, format: null,
};
const notional: WireColumn = { ...book, name: 'Notional', header: 'Notional', type: 'number', format: 'N2 en-GB', editable: false };

const options = (overrides: Partial<WireOptions> = {}): WireOptions => ({
    columns: [book, notional], columnsVersion: 1, locale: 'en-GB', viewportHeight: 240, viewportWidth: 600, hideColumnMenu: true, hears: [], ...overrides,
});

const query = '{"version":1,"sorts":[],"range":{"start":0,"count":1}}';

const state = (seq: number, window: readonly WireRow[], overrides: Partial<WireState> = {}): WireState => ({
    seq, cv: 1, window, windowStart: 0, totalCount: window.length, loading: false, rowSequenceVersion: 0, query, ...overrides,
});

const rates: WireRow = { i: 1, c: ['Rates', 1500], t: [null, '1,500.00'] };
const credit: WireRow = { i: 2, c: ['Credit', 2500], t: [null, '2,500.00'] };

const attached: BlazorGrid[] = [];

function sourceOf(dotnet: FakeDotNet): DotNetSource {
    const columns = new ColumnSet();
    columns.take([book, notional], 1);
    return new DotNetSource(dotnet, new RowSet(new FakeCollector().make, () => { throw new Error('Nothing is collected here.'); }), columns,
        { generation: 1, rowKey: false, vouchesDistinctRows: false });
}

function mount(init: Partial<WireAttach> = {}, dotnet = new FakeDotNet(), collector = new FakeCollector()): { root: HTMLElement; grid: BlazorGrid } {
    const root = document.createElement('div');
    document.body.append(root);
    const grid = attach(root, dotnet, JSON.stringify({
        options: options(), source: { generation: 1, rowKey: false, vouchesDistinctRows: false }, state: state(1, [rates, credit]), ...init,
    }), collector.make);
    attached.push(grid);
    return { root, grid };
}

afterEach(() => {
    for (const grid of attached.splice(0)) grid.dispose();
    document.body.replaceChildren();
});

describe('values', () => {
    it('ADR-0350 section 16: a DateTimeOffset shows in the offset it holds and compares as the instant it names', () => {
        const nine = valueOf({ $: 'o', v: '638000000000000000', m: 540 });
        const utc = valueOf({ $: 'o', v: String(638_000_000_000_000_000n - 540n * 600_000_000n), m: 0 });
        expect(Reflect.get(nine as object, 'offset')).toBe('+09:00');
        expect(Reflect.get(nine as object, 'epochNanoseconds')).toBe(Reflect.get(utc as object, 'epochNanoseconds'));
        // The same instant at another offset shows another text, so it is another value to a format.
        expect(valueKey(nine)).not.toBe(valueKey(utc));
    });
});

describe('rows', () => {
    it('ADR-0003: a row sent again with the same values is the same object, and one whose values changed is a new one', () => {
        const columns = new ColumnSet();
        columns.take([book, notional], 1);
        const rows = new RowSet(new FakeCollector().make, () => { throw new Error('Nothing is collected here.'); });
        const [first] = rows.read([rates], 1, columns);
        const [again] = rows.read([rates], 2, columns);
        const [changed] = rows.read([{ ...rates, c: ['Rates', 1600], t: [null, '1,600.00'] }], 3, columns);
        expect(again).toBe(first);
        expect(changed).not.toBe(first);
        expect(changed?.id).toBe(1);
    });

    it('a row is let go to .NET once no object of it is left, with the last message the bridge read', async () => {
        const columns = new ColumnSet();
        columns.take([book, notional], 1);
        const collector = new FakeCollector();
        const released: { readonly ids: readonly number[]; readonly seen: number }[] = [];
        const rows = new RowSet(collector.make, (ids, seen) => { released.push({ ids, seen }); });
        const [first] = rows.read([rates], 1, columns);
        const [changed] = rows.read([{ ...rates, c: ['Rates', 1600], t: [null, '1,600.00'] }], 2, columns);
        if (first === undefined || changed === undefined) throw new Error('Both rows were read.');
        collector.collect(first);
        await Promise.resolve();
        // The changed row still names the id, so .NET keeps it.
        expect(released).toEqual([]);
        collector.collect(changed);
        await Promise.resolve();
        expect(released).toEqual([{ ids: [1], seen: 2 }]);
    });

    it('ADR-0006: a formatted column shows .NET\'s text of each value, and refuses two texts for one value', () => {
        const columns = new ColumnSet();
        columns.take([book, notional], 1);
        const rows = new RowSet(new FakeCollector().make, () => { throw new Error('Nothing is collected here.'); });
        rows.read([rates], 1, columns);
        const format = columns.columns[1]?.format;
        expect(format?.(1500)).toBe('1,500.00');
        expect(() => rows.read([{ i: 3, c: ['FX', 1500], t: [null, '1,500.0'] }], 2, columns)).toThrow(/formats one value two ways/);
    });

    it('ADR-0016: a column declared again unchanged is the object it was, and one moved to another place is not', () => {
        const columns = new ColumnSet();
        columns.take([book, notional], 1);
        const [bookColumn, notionalColumn] = columns.columns;
        columns.take([book, notional], 1);
        expect(columns.columns[0]).toBe(bookColumn);
        columns.take([notional, book], 2);
        expect(columns.columns[0]).not.toBe(notionalColumn);
    });
});

describe('rows let go', () => {
    it('ADR-0350 section 7: a row is let go only up to the last message before which every message was taken in', async () => {
        const columns = new ColumnSet();
        columns.take([book, notional], 1);
        const collector = new FakeCollector();
        const released: { readonly ids: readonly number[]; readonly seen: number }[] = [];
        const rows = new RowSet(collector.make, (ids, seen) => { released.push({ ids, seen }); });
        // Message 2 crosses before message 1.
        const [early] = rows.read([rates], 2, columns);
        if (early === undefined) throw new Error('The row was read.');
        collector.collect(early);
        await Promise.resolve();
        expect(released).toEqual([{ ids: [1], seen: 0 }]);
        rows.read([credit], 1, columns);
        const [again] = rows.read([rates], 3, columns);
        if (again === undefined) throw new Error('The row was read.');
        collector.collect(again);
        await Promise.resolve();
        expect(released.at(-1)).toEqual({ ids: [1], seen: 3 });
    });

    it('ADR-0350 section 7: the rows of an answer given up are let go, but not one the grid still holds', async () => {
        const columns = new ColumnSet();
        columns.take([book, notional], 1);
        const released: { readonly ids: readonly number[]; readonly seen: number }[] = [];
        const rows = new RowSet(new FakeCollector().make, (ids, seen) => { released.push({ ids, seen }); });
        const held = rows.read([rates], 1, columns);
        rows.discard([rates, credit], 2);
        await Promise.resolve();
        expect(released).toEqual([{ ids: [2], seen: 2 }]);
        expect(held).toHaveLength(1);
    });

    it('ADR-0006: a column whose format .NET changed takes its new texts, and the old ones are gone', () => {
        const columns = new ColumnSet();
        columns.take([book, notional], 1);
        const rows = new RowSet(new FakeCollector().make, () => { throw new Error('Nothing is collected here.'); });
        rows.read([rates], 1, columns);
        columns.take([book, { ...notional, format: 'N0 en-GB' }], 2);
        rows.read([{ ...rates, t: [null, '1,500'] }], 2, columns);
        expect(columns.columns[1]?.format?.(1500)).toBe('1,500');
    });

    it('the spine\'s sixth rule: rows read by columns .NET has since replaced are asked for again', async () => {
        const dotnet = new FakeDotNet();
        const columns = new ColumnSet();
        columns.take([book, notional], 1);
        const source = new DotNetSource(dotnet, new RowSet(new FakeCollector().make, () => { /* collected rows are not this test's */ }), columns,
            { generation: 1, rowKey: false, vouchesDistinctRows: false });
        let asked = 0;
        dotnet.answer = method => {
            if (method !== 'Rows') return null;
            asked += 1;
            // The first answer was read by the columns before the second declaration.
            return asked === 1 ? JSON.stringify({ seq: 1, cv: 1, rows: [rates] }) : JSON.stringify({ seq: 2, cv: 2, rows: [{ i: 1, c: ['Rates'] }] });
        };
        const answered = source.rows({ start: 0, count: 1 }, new AbortController().signal);
        columns.take([book], 2);
        const [row] = await answered;
        expect(asked).toBe(2);
        expect(row?.cells).toEqual(['Rates']);
    });
});

describe('texts', () => {
    it('ADR-0006: a text is kept while a row that holds its value is, and goes with the last of them', () => {
        const columns = new ColumnSet();
        columns.take([book, notional], 1);
        const collector = new FakeCollector();
        const rows = new RowSet(collector.make, () => { /* the ids let go are not this test's */ });
        const [first] = rows.read([rates, credit], 1, columns);
        const [again] = rows.read([{ ...rates, i: 3 }], 2, columns);
        if (first === undefined || again === undefined) throw new Error('The rows were read.');
        expect(columns.textCount('Notional')).toBe(2);
        collector.collect(first);
        // Row 3 holds 1500 still.
        expect(columns.columns[1]?.format?.(1500)).toBe('1,500.00');
        collector.collect(again);
        expect(columns.textCount('Notional')).toBe(1);
        expect(() => columns.columns[1]?.format?.(1500)).toThrow(/no row it holds/);
    });

    it('ADR-0006: a column\'s value list holds its texts until the next list of that column', () => {
        const columns = new ColumnSet();
        columns.take([book, notional], 1);
        columns.holdColumnTexts('Notional', [1, 2], ['1.00', '2.00']);
        columns.holdColumnTexts('Notional', [2, 3], ['2.00', '3.00']);
        expect(columns.textCount('Notional')).toBe(2);
        expect(columns.columns[1]?.format?.(3)).toBe('3.00');
    });
});

function gate(): { readonly gate: WriteGate<string, number>; readonly taken: Sent<string, number>[]; readonly discarded: number[] } {
    const taken: Sent<string, number>[] = [];
    const discarded: number[] = [];
    return { gate: new WriteGate<string, number>(message => { taken.push(message); }, given => { discarded.push(given); }), taken, discarded };
}

describe('writes of the user\'s own', () => {
    it('ADR-0142 D1: what .NET sends while it handles a write is taken in once it is handled, the last state of a run alone', async () => {
        const { gate: held, taken, discarded } = gate();
        const settle: (() => void)[] = [];
        const handling = held.handling(new Promise<void>(resolve => { settle.push(resolve); }));
        held.receive({ state: 1 });
        held.receive({ state: 2 });
        held.receive({ update: 'columns' });
        held.receive({ state: 3 });
        held.receive({ state: 4 });
        expect(taken).toEqual([]);
        settle[0]?.();
        await handling;
        expect(taken).toEqual([{ state: 2 }, { update: 'columns' }, { state: 4 }]);
        expect(discarded).toEqual([1, 3]);
        held.receive({ state: 5 });
        expect(taken.at(-1)).toEqual({ state: 5 });
    });

    it('ADR-0142 D1: two writes handled at once hold what .NET sends until both are', async () => {
        const { gate: held, taken } = gate();
        const settle: (() => void)[] = [];
        const first = held.handling(new Promise<void>(resolve => { settle.push(resolve); }));
        const second = held.handling(new Promise<void>(resolve => { settle.push(resolve); }));
        held.receive({ state: 1 });
        settle[0]?.();
        await first;
        expect(taken).toEqual([]);
        settle[1]?.();
        await second;
        expect(taken).toEqual([{ state: 1 }]);
    });

    it('ADR-0350 section 7: a grid disposed lets nothing go to .NET and takes nothing in', async () => {
        const collector = new FakeCollector();
        const dotnet = new FakeDotNet();
        const { grid } = mount({}, dotnet, collector);
        const read: BlazorRow[] = [];
        collector.each(row => { read.push(row); });
        grid.dispose();
        attached.splice(attached.indexOf(grid), 1);
        for (const row of read) collector.collect(row);
        await Promise.resolve();
        expect(dotnet.calls.filter(call => call.method === 'Release')).toEqual([]);
    });
});

describe('questions', () => {
    it('the spine\'s sixth rule: rows read by columns not yet here wait for them, and are not asked again', async () => {
        const dotnet = new FakeDotNet();
        const columns = new ColumnSet();
        columns.take([book, notional], 1);
        const source = new DotNetSource(dotnet, new RowSet(new FakeCollector().make, () => { /* collected rows are not this test's */ }), columns,
            { generation: 1, rowKey: false, vouchesDistinctRows: false });
        dotnet.answer = method => (method === 'Rows' ? JSON.stringify({ seq: 1, cv: 2, rows: [{ i: 1, c: ['Rates'] }] }) : null);
        const answered = source.rows({ start: 0, count: 1 }, new AbortController().signal);
        await new Promise(resolve => { setTimeout(resolve, 0); });
        columns.take([book], 2);
        const [row] = await answered;
        expect(dotnet.calls.filter(call => call.method === 'Rows')).toHaveLength(1);
        expect(row?.cells).toEqual(['Rates']);
    });

    it('ADR-0009: a column .NET answers gone is asked of no more, and its question ends as the panel gives it up', async () => {
        const dotnet = new FakeDotNet();
        const columns = new ColumnSet();
        columns.take([book, notional], 1);
        const source = new DotNetSource(dotnet, new RowSet(new FakeCollector().make, () => { /* collected rows are not this test's */ }), columns,
            { generation: 1, rowKey: false, vouchesDistinctRows: false });
        dotnet.answer = method => (method === 'DistinctValues' ? JSON.stringify({ cv: 2, kind: 'gone' }) : null);
        const asked = new AbortController();
        const values = source.distinctValues('Notional', asked.signal);
        columns.take([book], 2);
        await new Promise(resolve => { setTimeout(resolve, 0); });
        asked.abort(new Error('The panel closed.'));
        await expect(values).rejects.toThrow(/panel closed/);
        expect(dotnet.calls.filter(call => call.method === 'DistinctValues')).toHaveLength(1);
    });

    it('ADR-0207: a filter\'s number is the decimal the C# core writes, and one a decimal cannot hold is refused', () => {
        expect(operandOf(1500.1)).toEqual({ type: 'decimal', value: { mantissa: 15001n, scale: 1 } });
        expect(operandOf(1e21)).toEqual({ type: 'decimal', value: { mantissa: 10n ** 21n, scale: 0 } });
        expect(operandOf(-1.5e-7)).toEqual({ type: 'decimal', value: { mantissa: -15n, scale: 8 } });
        expect(operandOf(0)).toEqual({ type: 'decimal', value: { mantissa: 0n, scale: 0 } });
        expect(() => operandOf(1e-30)).toThrow(/too small/);
        expect(() => operandOf(1e30)).toThrow(/outside the range/);
        expect(operandOf(2n ** 70n)).toEqual({ type: 'decimal', value: { mantissa: 2n ** 70n, scale: 0 } });
        expect(operandOf(2n ** 62n)).toEqual({ type: 'int64', value: 2n ** 62n });
        expect(() => operandOf(2n ** 100n)).toThrow(/outside the range/);
    });
});

describe('the grid', () => {
    it('ADR-0350 section 7: the grid is drawn on the component\'s element, from .NET\'s columns and its source\'s count', () => {
        const { root } = mount();
        expect(root.getAttribute('role')).toBe('grid');
        expect([...root.querySelectorAll('.ex-header-cell')].map(cell => cell.textContent)).toEqual(['Book', 'Notional']);
        expect(root.getAttribute('aria-rowcount')).toBe('3');
    });

    it('ADR-0350 section 7: a state .NET sends is the source\'s next snapshot, read by the grid', async () => {
        const { root, grid } = mount();
        grid.state(JSON.stringify(state(2, [rates, credit, { i: 3, c: ['FX', 10], t: [null, '10.00'] }])));
        await Promise.resolve();
        expect(root.getAttribute('aria-rowcount')).toBe('4');
    });

    it('ADR-0142: another source .NET sends replaces the one the grid reads, with its own state', async () => {
        const { root, grid } = mount();
        grid.update(JSON.stringify({
            options: options(), source: { generation: 2, rowKey: false, vouchesDistinctRows: false }, state: state(2, [{ i: 3, c: ['FX', 10], t: [null, '10.00'] }]),
        }));
        await Promise.resolve();
        expect(root.getAttribute('aria-rowcount')).toBe('2');
    });

    it('ADR-0001: a sort and a filter the grid applies reach .NET as the C# core\'s query text', () => {
        const dotnet = new FakeDotNet();
        const source = sourceOf(dotnet);
        source.setSorts([{ column: 'Book', direction: 'descending' }]);
        source.setFilter({ columns: { Book: { clauses: [{ operator: 'equals', value: 'Rates' }] } } });
        expect(dotnet.calls.map(call => [call.method, call.args[0], call.args[1]])).toEqual([
            ['SetSorts', 1, '{"version":1,"range":{"start":0,"count":1},"sorts":[{"column":"Book","direction":"Descending"}]}'],
            ['SetFilter', 1, '{"version":1,"range":{"start":0,"count":1},"sorts":[],"filter":{"columns":{"Book":{"combinator":"And","clauses":'
                + '[{"operator":"Equals","value":{"type":"String","value":"Rates"}}]}}}}'],
        ]);
    });

    it('ADR-0005: rows asked for past the Window are .NET\'s answer, and a question given up is called off there', async () => {
        const dotnet = new FakeDotNet();
        const answers: ((value: unknown) => void)[] = [];
        dotnet.answer = method => (method === 'Rows' ? new Promise(resolve => { answers.push(resolve); }) : null);
        const source = sourceOf(dotnet);
        const asked = new AbortController();
        const rows = source.rows({ start: 0, count: 2 }, asked.signal);
        asked.abort(new Error('Given up.'));
        for (const answer of answers) answer(JSON.stringify({ seq: 2, cv: 1, rows: [rates, credit] }));
        await expect(rows).rejects.toThrow(/Given up/);
        const cancel = dotnet.calls.find(call => call.method === 'Cancel');
        const ask = dotnet.calls.find(call => call.method === 'Rows');
        expect(cancel?.args[0]).toBe(ask?.args[1]);
    });
});

describe('keyboard', () => {
    it('DC-61 / ADR-0070: a control the grid holds the keyboard for is handed it, and the keyboard comes back from it', () => {
        const { root, grid } = mount();
        const close = document.createElement('button');
        document.body.append(close);
        grid.focus();

        grid.handKeyboardTo(close, false);
        expect(document.activeElement).toBe(close);
        grid.returnKeyboard(close);
        expect(root.contains(document.activeElement)).toBe(true);
    });

    it('DC-61 / ADR-0070: a control a render removed before the request landed arrives as null, and the keyboard stays', () => {
        const { root, grid } = mount();
        grid.focus();
        const holder = document.activeElement;

        grid.handKeyboardTo(null, false);
        expect(document.activeElement).toBe(holder);
        expect(root.contains(holder)).toBe(true);
    });
});
