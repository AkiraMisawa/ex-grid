// @vitest-environment happy-dom
// The bridge's own tests (ADR-0350 section 13): what crosses between .NET and the Script Core. What the grid
// paints and decides is the core's, tested there; here, that .NET's values arrive as the values it held, that a
// row sent again unchanged is the same row (ADR-0003), that a row the browser let go is let go in .NET only once
// .NET has not sent it again, and that what the grid tells its source and its Consumer reaches .NET in order.
import { afterEach, describe, expect, it } from 'vitest';
import { GridPointedAt } from '@ex-grid/core';
import { attach, latestOnly, loadScripts, type BlazorGrid, type WireAttach, type WireOptions } from '../src/blazor-grid';
import { ScriptFunctionError, type LoadedScriptFunction, type WireScript } from '../src/script-functions';
import { DotNetSource, type DotNetHandle, type WireState } from '../src/dotnet-source';
import { ColumnSet, RowSet, type BlazorRow, type RowCollector, type WireColumn, type WireRow } from '../src/wire-rows';
import { awaitPointingScope, holdPointingScope, type PagePointingScope } from '../src/pointing-scopes';
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

const noFind = (): never => { throw new Error('No Find step is asked here.'); };

function sourceOf(dotnet: FakeDotNet): DotNetSource {
    const columns = new ColumnSet();
    columns.take([book, notional], 1);
    return new DotNetSource(dotnet, new RowSet(new FakeCollector().make, () => { throw new Error('Nothing is collected here.'); }), columns,
        { generation: 1, rowKey: false, vouchesDistinctRows: false, finds: false }, noFind);
}

function mount(
    init: Partial<WireAttach> = {}, dotnet = new FakeDotNet(), collector = new FakeCollector(), scripts: Readonly<Record<string, LoadedScriptFunction>> = {},
): { root: HTMLElement; grid: BlazorGrid } {
    const root = document.createElement('div');
    document.body.append(root);
    const functionOf = (script: WireScript): LoadedScriptFunction => {
        const f = scripts[script.export];
        if (f === undefined) throw new Error(`The test loaded no ${script.export}.`);
        return f;
    };
    const grid = attach(root, dotnet, JSON.stringify({
        options: options(), source: { generation: 1, rowKey: false, vouchesDistinctRows: false, finds: false }, state: state(1, [rates, credit]), ...init,
    }), collector.make, functionOf);
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
            { generation: 1, rowKey: false, vouchesDistinctRows: false, finds: false }, noFind);
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

function gate(): { readonly gate: WriteGate<string, number>; readonly taken: Sent<string, number>[] } {
    const taken: Sent<string, number>[] = [];
    return { gate: new WriteGate<string, number>(message => { taken.push(message); }), taken };
}

describe('writes of the user\'s own', () => {
    it('ADR-0142 D1 / ADR-0240: what .NET sends while it handles a write is taken in once it is handled, every state in its order', async () => {
        const { gate: held, taken } = gate();
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
        // A Window replaced by the next is still one the source held: a row command whose row it lacked lost it.
        expect(taken).toEqual([{ state: 1 }, { state: 2 }, { update: 'columns' }, { state: 3 }, { state: 4 }]);
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
            { generation: 1, rowKey: false, vouchesDistinctRows: false, finds: false }, noFind);
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
            { generation: 1, rowKey: false, vouchesDistinctRows: false, finds: false }, noFind);
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
            options: options(), source: { generation: 2, rowKey: false, vouchesDistinctRows: false, finds: false }, state: state(2, [{ i: 3, c: ['FX', 10], t: [null, '10.00'] }]),
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

describe('a Pointing Scope held in the page (ADR-0430)', () => {
    // A member a failed test leaves would stand in the page's next Scope of its name.
    const leaving: (() => void)[] = [];
    afterEach(() => { for (const leave of leaving.splice(0)) leave(); });

    it('a grid joins the Scope once a Sheet of it brings it, registers its declaration, and reads each row as the table\'s row .NET sent', async () => {
        const pointedAt = new GridPointedAt<BlazorRow>(() => Promise.resolve());
        const registered: { readonly declaration: string; readonly table: string; readonly tableColumns: Readonly<Record<string, string>>; readonly tableRow: (row: BlazorRow) => readonly unknown[] }[] = [];
        const scope: PagePointingScope = {
            registerGrid: <TRow>(declaration: string, table: string, tableColumns: Readonly<Record<string, string>>, tableRow: (row: TRow) => readonly unknown[]) => {
                // The one grid of this test registers rows of its own type.
                registered.push({ declaration, table, tableColumns, tableRow: tableRow as (row: BlazorRow) => readonly unknown[] });
                return pointedAt as unknown as GridPointedAt<TRow>;
            },
        };
        const collector = new FakeCollector();
        const { root } = mount({
            options: options({ pointedAt: { scope: 'page-scope', declaration: 'page-scope-1', table: 'Books', tableColumns: { Book: 'Name' } } }),
            state: state(1, [{ ...rates, p: ['Rates', 1500] }, { ...credit, p: ['Credit', 2500] }]),
        }, new FakeDotNet(), collector);
        expect(registered).toEqual([]);

        leaving.push(holdPointingScope('page-scope', () => scope).release);
        expect(registered.map(({ declaration, table, tableColumns }) => ({ declaration, table, tableColumns })))
            .toEqual([{ declaration: 'page-scope-1', table: 'Books', tableColumns: { Book: 'Name' } }]);
        const [{ tableRow } = { tableRow: undefined }] = registered;
        const tables: (readonly unknown[] | undefined)[] = [];
        collector.each(row => { tables.push(tableRow?.(row)); });
        expect(tables).toEqual([['Rates', 1500], ['Credit', 2500]]);

        // The declaration is the grid's: the Scope points at it, and the grid shows it.
        pointedAt.isPointedAt = true;
        await Promise.resolve();
        expect(root.classList.contains('ex-pointed-at')).toBe(true);
    });

    it('a grid that fails as it joins fails the Sheet that brought the Scope, and neither holds it; the other grids join', () => {
        const joined: string[] = [];
        const scope: PagePointingScope = {
            registerGrid: <TRow>(declaration: string) => {
                if (declaration === 'failing-1') throw new TypeError('refused');
                joined.push(declaration);
                return new GridPointedAt<TRow>(() => Promise.resolve());
            },
        };
        const failing = awaitPointingScope('failing', given => { given.registerGrid('failing-1', 'Books', {}, () => []); });
        leaving.push(failing, awaitPointingScope('failing', given => { given.registerGrid('failing-2', 'Books', {}, () => []); }));

        expect(() => holdPointingScope('failing', () => scope)).toThrow('refused');
        expect(joined).toEqual(['failing-2']);

        // The failed grid and the Sheet hold nothing: with the other grid gone, the name is a new Scope.
        failing();
        leaving.splice(0).forEach(leave => { leave(); });
        let made = 0;
        leaving.push(holdPointingScope('failing', () => { made += 1; return scope; }).release);
        expect(made).toBe(1);
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

// A press on the rows and a key where the keyboard is, as the browser lands them (the core's harness does the same).
function press(root: HTMLElement, x: number, y: number): void {
    const viewport = root.querySelector('.ex-viewport');
    if (viewport === null) throw new Error('The grid draws no viewport.');
    for (const name of ['mousedown', 'mouseup']) {
        const event = new MouseEvent(name, { bubbles: true, cancelable: true, button: 0, buttons: name === 'mouseup' ? 0 : 1 });
        Object.defineProperty(event, 'offsetX', { value: x });
        Object.defineProperty(event, 'offsetY', { value: y });
        viewport.dispatchEvent(event);
        if (name === 'mousedown' && !event.defaultPrevented) root.querySelector<HTMLElement>('.ex-scroller')?.focus();
    }
}

function type(text: string): void {
    for (const character of text) {
        const target = document.activeElement;
        if (target === null) throw new Error('Nothing holds the keyboard.');
        const event = new KeyboardEvent('keydown', { key: character, bubbles: true, cancelable: true });
        target.dispatchEvent(event);
        if (!event.defaultPrevented && target instanceof HTMLInputElement && !target.readOnly) {
            target.setRangeText(character, target.selectionStart ?? 0, target.selectionEnd ?? 0, 'end'); // oxlint-disable-line ex-grid/no-stand-in-default -- a text input that takes text has a selection
            target.dispatchEvent(new InputEvent('input', { bubbles: true, data: character, inputType: 'insertText' }));
        }
    }
}

function key(name: string, init: KeyboardEventInit = {}): void {
    const target = document.activeElement;
    if (target === null) throw new Error('Nothing holds the keyboard.');
    target.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init }));
}

const turns = async (): Promise<void> => {
    for (let i = 0; i < 6; i++) await new Promise(resolve => { setTimeout(resolve, 0); });
};

const editorOf = (root: HTMLElement): HTMLInputElement | null => root.querySelector<HTMLInputElement>('input.ex-editor');

// A pointer event on the viewport where the grid reads it: offsets in the viewport's own pixels.
function pointer(root: HTMLElement, name: string, x: number, y: number): void {
    const event = new MouseEvent(name, { bubbles: true, cancelable: true, button: 0, buttons: name === 'mouseup' ? 0 : 1 });
    Object.defineProperty(event, 'offsetX', { value: x });
    Object.defineProperty(event, 'offsetY', { value: y });
    const viewport = root.querySelector('.ex-viewport');
    if (viewport === null) throw new Error('The grid drew no viewport.');
    viewport.dispatchEvent(event);
}

// Book's first cell, then its handle (7 px at its bottom-right, Book being 120 px wide) dragged one row down.
async function fillDown(root: HTMLElement): Promise<void> {
    pointer(root, 'mousedown', 10, 5);
    pointer(root, 'mouseup', 10, 5);
    await turns();
    pointer(root, 'mousedown', 120, 24);
    pointer(root, 'mousemove', 10, 29);
    pointer(root, 'mouseup', 10, 29);
    await turns();
}

const lastRanges = (dotnet: FakeDotNet): unknown => Reflect.get(dotnet.heard('selectionChanged').at(-1) as object, 'ranges');

describe('fills (ADR-0050 item 5)', () => {
    it('ADR-0050 item 5 / ADR-0380: a fill reaches .NET as its source, target, direction and order, and once taken the Selection holds both', async () => {
        const dotnet = new FakeDotNet();
        const { root } = mount({
            options: options({ rowHeight: 24, showFillHandle: true, hears: ['fill', 'selectionChanged'] }), state: state(1, [rates, credit], { rowSequenceVersion: 4 }),
        }, dotnet);
        await turns();
        await fillDown(root);
        expect(dotnet.heard('fill')).toEqual([{
            answer: 1, source: { topRow: 0, leftColumn: 0, rowCount: 1, columnCount: 1 }, target: { topRow: 1, leftColumn: 0, rowCount: 1, columnCount: 1 },
            direction: 'down', rowSequenceVersion: 4,
        }]);
        expect(lastRanges(dotnet)).toEqual([{ topRow: 0, leftColumn: 0, rowCount: 2, columnCount: 1 }]);
    });

    it('DC-27 / ADR-0380 section 5: a fill .NET refuses leaves the Selection on the source', async () => {
        const dotnet = new FakeDotNet();
        dotnet.answer = (method, args) => (method === 'Heard' && args[0] === 'fill' ? '' : null);
        const { root } = mount({ options: options({ rowHeight: 24, showFillHandle: true, hears: ['fill', 'selectionChanged'] }) }, dotnet);
        await turns();
        await fillDown(root);
        expect(dotnet.heard('fill')).toHaveLength(1);
        expect(root.hasAttribute('data-ex-pending-commit')).toBe(false);
        expect(lastRanges(dotnet)).toEqual([{ topRow: 0, leftColumn: 0, rowCount: 1, columnCount: 1 }]);
    });
});

describe('answers .NET gives later (ADR-0380 section 5)', () => {
    const validated: WireColumn = { ...book, validates: true };

    it('ED-35: a column .NET validates asks .NET by number, and its Reject holds the editor with the text', async () => {
        const dotnet = new FakeDotNet();
        const verdicts: ((json: string) => void)[] = [];
        dotnet.answer = method => (method === 'Validate' ? new Promise(resolve => { verdicts.push(resolve); }) : null);
        const { root } = mount({ options: options({ columns: [validated, notional], rowHeight: 24, hears: ['edit'] }) }, dotnet);
        await turns();
        press(root, 10, 5);
        type('Delta');
        key('Enter');
        await turns();
        expect(dotnet.calls.filter(call => call.method === 'Validate').map(call => call.args)).toEqual([[1, 1, 1, 'Book', 'Delta']]);
        expect(root.hasAttribute('data-ex-pending-commit')).toBe(true);

        verdicts[0]?.('{"kind":"reject","message":"Not a book we trade."}');
        await turns();
        expect(dotnet.heard('edit')).toEqual([]);
        expect([editorOf(root)?.value, editorOf(root)?.getAttribute('aria-invalid')]).toEqual(['Delta', 'true']);
    });

    it('ED-34: Escape while .NET judges calls that answer off there, and the editor stays with the text', async () => {
        const dotnet = new FakeDotNet();
        dotnet.answer = method => (method === 'Validate' ? new Promise(() => { /* .NET never answers: the user cancels */ }) : null);
        const { root } = mount({ options: options({ columns: [validated, notional], rowHeight: 24, hears: ['edit'] }) }, dotnet);
        await turns();
        press(root, 10, 5);
        type('Delta');
        key('Enter');
        key('Escape');
        await turns();
        expect(dotnet.calls.filter(call => call.method === 'CancelAnswer').map(call => call.args)).toEqual([[1]]);
        expect(root.hasAttribute('data-ex-pending-commit')).toBe(false);
        expect(editorOf(root)?.value).toBe('Delta');
    });

    it('ED-35: an edit .NET refuses holds the editor with the Consumer\'s sentence, and one it takes ends the edit', async () => {
        const dotnet = new FakeDotNet();
        dotnet.answer = (method, args) => (method === 'Heard' && args[0] === 'edit'
            ? (Reflect.get(args[1] as object, 'value') === 'Delta' ? 'Trade is locked.' : null)
            : null);
        const { root } = mount({ options: options({ rowHeight: 24, hears: ['edit'] }) }, dotnet);
        await turns();
        press(root, 10, 5);
        type('Delta');
        key('Enter');
        await turns();
        expect(dotnet.heard('edit')).toHaveLength(1);
        expect(Reflect.get(dotnet.heard('edit')[0] as object, 'answer')).toBe(1);
        expect([editorOf(root)?.value, root.querySelector('.ex-announce')?.textContent]).toEqual(['Delta', 'Trade is locked.']);

        type('s');
        key('Enter');
        await turns();
        expect(dotnet.heard('edit')).toHaveLength(2);
        expect(editorOf(root)).toBeNull();
    });

    it('ED-35: a cell\'s Error comes with its row, and its message is asked of .NET as it opens', async () => {
        const dotnet = new FakeDotNet();
        dotnet.answer = (method, args) => (method === 'Message' ? `${String(args[1])} is not a book we trade.` : null);
        const { root } = mount({
            options: options({ rowHeight: 24, cellStates: true, cellMessages: 1 }),
            state: state(1, [rates, { ...credit, s: ['error', 'normal'] }]),
        }, dotnet);
        await turns();
        const cells = root.querySelectorAll('.ex-row')[1]?.querySelectorAll('.ex-cell');
        expect(cells?.[0]?.classList.contains('ex-state-error')).toBe(true);
        expect(dotnet.calls.filter(call => call.method === 'Message')).toEqual([]);

        press(root, 10, 29);
        await new Promise(resolve => { setTimeout(resolve, 350); });
        await turns();
        expect(dotnet.calls.filter(call => call.method === 'Message').map(call => call.args)).toEqual([[2, 'Book']]);
        expect(root.querySelector('.ex-message')?.textContent).toBe('Book is not a book we trade.');
    });
});

const focusHeard = (dotnet: FakeDotNet): unknown => Reflect.get(dotnet.heard('selectionChanged').at(-1) as object, 'focus');

describe('the error step .NET answers (ADR-0381 section 3)', () => {
    it('ED-38: F8 asks .NET by number, of the source the grid holds, and its answer brings the Focus there', async () => {
        const dotnet = new FakeDotNet();
        dotnet.answer = method => (method === 'FindError' ? '{"found":true,"row":1,"column":"Notional"}' : null);
        const { root } = mount({ options: options({ rowHeight: 24, findsErrors: true, hears: ['selectionChanged'] }) }, dotnet);
        await turns();
        press(root, 10, 5);
        key('F8');
        await turns();
        expect(dotnet.calls.filter(call => call.method === 'FindError').map(call => call.args)).toEqual([
            [1, 1, { backward: false, from: { row: 0, column: 0 }, columns: ['Book', 'Notional'], rowSequenceVersion: 0 }],
        ]);
        expect(focusHeard(dotnet)).toEqual({ row: 1, column: 1 });
    });

    it('ADR-0142: a step .NET answers as moved waits for the state a write holds, and the grid says the rows moved', async () => {
        const dotnet = new FakeDotNet();
        let cleared: ((answer: null) => void) | undefined;
        const { root, grid } = mount({ options: options({ rowHeight: 24, findsErrors: true, hears: ['clear', 'selectionChanged'] }) }, dotnet);
        dotnet.answer = (method, args) => {
            if (method === 'Heard' && args[0] === 'clear') return new Promise(resolve => { cleared = resolve; });
            if (method !== 'FindError') return null;
            // .NET's source moved before the step's turn: its state crosses first, and the clear in flight holds it.
            grid.state(JSON.stringify(state(2, [credit, rates], { rowSequenceVersion: 1 })));
            return '{"moved":true}';
        };
        await turns();
        press(root, 10, 5);
        key('Delete');
        key('F8');
        await turns();
        expect(dotnet.calls.filter(call => call.method === 'FindError')).toHaveLength(1);
        // Not "none found" in an order the grid no longer shows: nothing is said until the held state is in.
        expect(root.querySelector('.ex-announce')?.textContent).toBe('');

        if (cleared === undefined) throw new Error('The clear crossed to .NET.');
        cleared(null);
        await turns();
        expect(root.querySelector('.ex-announce')?.textContent)
            .toBe('The rows moved while the next error was looked for, so the step was set aside. Press F8 again.');
    });
});

describe('a Find step .NET\'s source answers (ADR-0055)', () => {
    it('ADR-0055: Ctrl+F is refused where .NET\'s source does not search, and a step is asked of .NET by number where it does', async () => {
        const refusing = new FakeDotNet();
        const refused = mount({ options: options({ rowHeight: 24, hears: ['refused'] }) }, refusing);
        await turns();
        press(refused.root, 10, 5);
        key('f', { ctrlKey: true });
        await turns();
        expect([refused.root.querySelector('.ex-popover-find'), refusing.calls.filter(call => call.method === 'Heard' && call.args[0] === 'refused').length]).toEqual([null, 1]);
        refused.grid.dispose();

        const dotnet = new FakeDotNet();
        dotnet.answer = method => (method === 'Find' ? '{"found":true,"row":1,"column":"Book"}' : null);
        const { root } = mount({
            options: options({ rowHeight: 24, hears: ['selectionChanged'] }), source: { generation: 1, rowKey: false, vouchesDistinctRows: false, finds: true },
        }, dotnet);
        await turns();
        press(root, 10, 5);
        key('f', { ctrlKey: true });
        const field = root.querySelector<HTMLInputElement>('.ex-popover-find input.ex-find-field');
        if (field === null) throw new Error('No find field.');
        field.value = 'Credit';
        field.dispatchEvent(new Event('input', { bubbles: true }));
        field.form?.requestSubmit();
        await turns();
        expect(dotnet.calls.filter(call => call.method === 'Find').map(call => call.args)).toEqual([
            [1, 1, { text: 'Credit', matchCase: false, wholeCell: false, backward: false, from: { row: 0, column: 0 }, columns: ['Book', 'Notional'], rowSequenceVersion: 0 }],
        ]);
        expect(focusHeard(dotnet)).toEqual({ row: 1, column: 0 });
    });
});

describe('the Context Menu\'s commands .NET answers (ADR-0421)', () => {
    it('ADR-0421: the menu asks .NET by number as it opens, stands with what .NET answered in its words, and runs a command there', async () => {
        const dotnet = new FakeDotNet();
        dotnet.answer = method => (method === 'ContextCommands' ? '[{"id":"exchart.chart","enabled":true,"label":"Chart"},{"id":"audit","enabled":false}]' : null);
        const { root } = mount({ options: options({ rowHeight: 24, contextCommands: true }) }, dotnet);
        await turns();
        press(root, 10, 5);
        key('F10', { shiftKey: true });
        await turns();
        expect(dotnet.calls.filter(call => call.method === 'ContextCommands').map(call => call.args)).toEqual([
            [1, 1, 1, 1, 'Book', [{ topRow: 0, leftColumn: 0, rowCount: 1, columnCount: 1 }], 0],
        ]);
        // A command the Consumer gave no words is shown as its id, as the built-in menu shows any it does not know.
        expect([...root.querySelectorAll('.ex-popover [role=menuitem]')].map(item => item.textContent)).toEqual(['Copy', 'Copy with headers', 'Chart', 'audit']);
        key('ArrowDown');
        key('ArrowDown');
        key('Enter');
        await turns();
        expect(dotnet.calls.filter(call => call.method === 'RunContextCommand').map(call => call.args)).toEqual([[1, 0]]);
        expect(root.querySelector('.ex-popover')).toBeNull();
    });

    it('ADR-0421: a menu .NET answers as moved, or a component gone answers nothing for, does not stand, and says nothing more', async () => {
        const dotnet = new FakeDotNet();
        const { root } = mount({ options: options({ rowHeight: 24, contextCommands: true }) }, dotnet);
        await turns();
        press(root, 10, 5);
        for (const answer of ['{"moved":true}', null]) {
            dotnet.answer = method => (method === 'ContextCommands' ? answer : null);
            key('F10', { shiftKey: true });
            await turns();
            expect(root.querySelector('.ex-popover')).toBeNull();
        }
        expect(dotnet.calls.filter(call => call.method === 'ContextCommands')).toHaveLength(2);
    });
});

describe('the grid\'s own words .NET sends (ADR-0440 section 4)', () => {
    it('ADR-0440: the table words the grid\'s commands, and a command the Consumer answered keeps its own words', async () => {
        const dotnet = new FakeDotNet();
        dotnet.answer = method => (method === 'ContextCommands' ? '[{"id":"audit","enabled":true,"label":"Audit"}]' : null);
        const { root } = mount({ options: options({ rowHeight: 24, contextCommands: true, commandLabels: { copy: 'Duplicate', audit: 'Not this' } }) }, dotnet);
        await turns();
        press(root, 10, 5);
        key('F10', { shiftKey: true });
        await turns();
        expect([...root.querySelectorAll('.ex-popover [role=menuitem]')].map(item => item.textContent)).toEqual(['Duplicate', 'Copy with headers', 'Audit']);
    });
});

describe('completion .NET answers (ADR-0051)', () => {
    it('ADR-0051: the editor\'s text and caret are asked of .NET as typed, and its answer is listed, Tab writing the chosen candidate', async () => {
        const dotnet = new FakeDotNet();
        dotnet.answer = (method, args) => (method === 'Complete' && args[0] === '=SU'
            ? '{"candidates":[{"label":"SUM","start":1,"length":2,"insert":"SUM("},{"label":"SUMIF","start":1,"length":2,"insert":"SUMIF("}],"hint":{"text":"SUM(number1)","emphasisStart":4,"emphasisLength":7}}'
            : null);
        const { root } = mount({ options: options({ rowHeight: 24, completesText: true, hears: ['edit'] }) }, dotnet);
        await turns();
        press(root, 10, 5);
        type('=SU');
        await turns();
        expect(dotnet.calls.filter(call => call.method === 'Complete').map(call => call.args).at(-1)).toEqual(['=SU', 3]);
        expect([...root.querySelectorAll('.ex-completion-item')].map(item => item.textContent)).toEqual(['SUM', 'SUMIF']);
        expect(root.querySelector('.ex-completion-hint')?.textContent).toBe('SUM(number1)');
        key('ArrowDown');
        key('Tab');
        await turns();
        expect(editorOf(root)?.value).toBe('=SUMIF(');
    });

    it('ADR-0051: a grid whose Consumer answers no completion asks .NET nothing', async () => {
        const dotnet = new FakeDotNet();
        const { root } = mount({ options: options({ rowHeight: 24, hears: ['edit'] }) }, dotnet);
        await turns();
        press(root, 10, 5);
        type('=SU');
        await turns();
        expect(dotnet.calls.filter(call => call.method === 'Complete')).toEqual([]);
    });
});

/** A function of ./js/formula.js, as .NET names it. */
const formulaScript = (name: string): WireScript => ({ module: './js/formula.js', export: name });

/** A Consumer's Reference text: R1C1 names. */
const r1c1 = (range: { readonly topRow: number; readonly leftColumn: number }): string => `R${String(range.topRow + 1)}C${String(range.leftColumn + 1)}`;

describe('the Consumer\'s own script for Formula entry (ADR-0431)', () => {
    const script = formulaScript;
    const pointing = {
        pointAt: script('pointAt'), referenceText: script('referenceText'), cycleReference: script('cycleReference'), referencesIn: script('referencesIn'),
    };
    const reference = r1c1;

    it('ADR-0431: an arrow typed after = points where the Consumer\'s pointAt says a Reference can go, writing its referenceText', async () => {
        const asked: unknown[][] = [];
        const { root } = mount({ options: options({ rowHeight: 24, scripts: pointing, hears: ['edit'] }) }, new FakeDotNet(), new FakeCollector(), {
            pointAt: (...args: unknown[]) => { asked.push(args); return args[0] === '='; },
            referenceText: reference,
            cycleReference: () => null,
            referencesIn: () => null,
        });
        await turns();
        press(root, 10, 5);
        type('=');
        key('ArrowDown');
        await turns();
        expect(asked).toContainEqual(['=', 1]);
        expect(editorOf(root)?.value).toBe('=R2C1');
    });

    it('ADR-0431: F4 writes what the Consumer\'s cycleReference answers, and its null leaves the text', async () => {
        let answer: unknown = { text: '=$A$1', selectionStart: 5, selectionEnd: 5 };
        const { root } = mount({ options: options({ rowHeight: 24, scripts: pointing, hears: ['edit'] }) }, new FakeDotNet(), new FakeCollector(), {
            pointAt: () => false, referenceText: reference, cycleReference: () => answer, referencesIn: () => null,
        });
        await turns();
        press(root, 10, 5);
        type('=A1');
        key('F4');
        await turns();
        expect(editorOf(root)?.value).toBe('=$A$1');
        answer = null;
        key('F4');
        await turns();
        expect(editorOf(root)?.value).toBe('=$A$1');
    });

    it('ADR-0431: a function answering what its option does not take is refused naming it, never read as another answer', async () => {
        const { root } = mount({ options: options({ rowHeight: 24, scripts: pointing, hears: ['edit'] }) }, new FakeDotNet(), new FakeCollector(), {
            pointAt: () => 'yes', referenceText: reference, cycleReference: () => null, referencesIn: () => null,
        });
        await turns();
        press(root, 10, 5);
        type('=');
        expect(() => { key('ArrowDown'); }).toThrow(new ScriptFunctionError('The script function "pointAt" of "./js/formula.js" answered "yes" as pointAt, which takes true or false.'));
        expect(editorOf(root)?.value).toBe('=');
    });

    it('ADR-0431: the colours handed out for the keys referencesIn answers reach .NET as places in the palette', async () => {
        const dotnet = new FakeDotNet();
        const { root } = mount({ options: options({ rowHeight: 24, scripts: pointing, hears: ['edit', 'referenceKeyColoursChanged'] }) }, dotnet, new FakeCollector(), {
            pointAt: () => false, referenceText: reference, cycleReference: () => null,
            referencesIn: (text: unknown) => (text === '=X' ? [{ start: 1, length: 1, key: 'Positions[PV]' }] : null),
        });
        await turns();
        press(root, 10, 5);
        type('=X');
        await turns();
        expect(dotnet.heard('referenceKeyColoursChanged').at(-1)).toEqual([{ key: 'Positions[PV]', colour: 1 }]);
        key('Escape');
        await turns();
        expect(dotnet.heard('referenceKeyColoursChanged').at(-1)).toEqual([]);
    });

    it('ADR-0420 section 3 / ADR-0431: a module addressed by data: carries the script in a string, and is refused by name as it is loaded', async () => {
        expect(await loadScripts(JSON.stringify([{ module: 'data:text/javascript,export const f = () => true', export: 'f' }])))
            .toContain('is addressed by "data:"');
    });

    it('ADR-0431: a module named by no address a page can import from is refused by name as it is loaded', async () => {
        expect(await loadScripts(JSON.stringify([{ module: 'http://[bad', export: 'f' }])))
            .toBe('The module "http://[bad" is no address a page can import a module from.');
    });

    it('ADR-0431: cycleReference and referencesIn answering nothing, or a shape their option does not take, are refused naming them', async () => {
        // What each function answers next; a function that returns nothing answers undefined.
        const answers: { cycled: unknown; found: unknown } = { cycled: undefined, found: null };
        const { root } = mount({ options: options({ rowHeight: 24, scripts: pointing, hears: ['edit'] }) }, new FakeDotNet(), new FakeCollector(), {
            pointAt: () => false, referenceText: reference, cycleReference: () => answers.cycled, referencesIn: () => answers.found,
        });
        await turns();
        press(root, 10, 5);
        type('=A1');
        await turns();
        expect(() => { key('F4'); }).toThrow(new ScriptFunctionError('The script function "cycleReference" of "./js/formula.js" answered undefined as cycleReference, '
            + 'which takes a rewrite { text, selectionStart, selectionEnd }, or null for none.'));
        answers.cycled = '=$A$1';
        expect(() => { key('F4'); }).toThrow('answered "=$A$1" as cycleReference');
        expect(editorOf(root)?.value).toBe('=A1');
        answers.found = [{ start: 1, length: 2, range: 'A1' }];
        expect(() => { type('+'); }).toThrow('answered object as referencesIn, which takes a list of References');
    });
});

describe('completion asked of .NET at machine speed (ADR-0051)', () => {
    it('ADR-0051: while a question is on its way only the latest crosses after it, and the one replaced answers nothing', async () => {
        let first: (() => void) | undefined;
        const asked: unknown[] = [];
        const completeEditorText = latestOnly((text, caret) => {
            asked.push([text, caret]);
            return asked.length === 1 ? new Promise(resolve => { first = () => { resolve(undefined); }; }) : Promise.resolve(undefined);
        });
        const answers = [completeEditorText('=', 1), completeEditorText('=S', 2), completeEditorText('=SU', 3)];
        expect(asked).toEqual([['=', 1]]);
        if (first === undefined) throw new Error('The first question crossed.');
        first();
        await Promise.all(answers);
        expect(asked).toEqual([['=', 1], ['=SU', 3]]);
    });
});

describe('Template columns (ADR-0020, ADR-0410 section 6)', () => {
    it('ADR-0410 section 6: each cell of a column .NET declares a Template asks .NET to draw it, naming the row by its id', async () => {
        const dotnet = new FakeDotNet();
        mount({ options: options({ columns: [{ ...book, editable: false, template: true }, notional] }) }, dotnet);
        await turns();
        const mounted = dotnet.calls.filter(call => call.method === 'MountSlot' && call.args[2] === 'templateCell')
            .map(call => Reflect.get(JSON.parse(String(call.args[3])) as object, 'row') as unknown);
        expect(mounted).toEqual([1, 2]);
    });
});

/** A function of ./js/grid.js, as .NET names it. */
const gridScript = (name: string): WireScript => ({ module: './js/grid.js', export: name });

describe('what .NET answers of each row, and asks of the user\'s gestures (ADR-0440)', () => {
    const toned: WireColumn = { ...notional, tone: true };
    const mark: WireColumn = { ...book, name: 'Mark', header: '', type: 'text', editable: false, queryable: false, width: { px: 40, minPx: 40, maxPx: 40 }, mark: true };

    it('ADR-0440: a row\'s kind, tones, appearance and change times come with it, and a change time is read on the browser\'s clock', () => {
        const columns = new ColumnSet();
        columns.take([book, toned], 1);
        const rows = new RowSet(new FakeCollector().make, () => { throw new Error('Nothing is collected here.'); });
        const total: WireRow = { ...rates, r: 'total', o: [null, 'negative'], a: [null, { bold: true }], h: [null, 1000] };
        const [first] = rows.read([total], 1, columns, 500);
        expect([first?.kind, first?.appearances, first?.changedAt]).toEqual(['total', [null, { bold: true }], [null, 1500]]);
        expect(columns.columns[1]?.tone?.(1500)).toBe('negative');
        // Sent again unchanged it is the row it was; a cell painted another way makes it another (ADR-0003).
        expect(rows.read([total], 2, columns, 500)[0]).toBe(first);
        expect(rows.read([{ ...total, a: [null, { italic: true }] }], 3, columns, 500)[0]).not.toBe(first);
        expect(() => rows.read([{ ...total, r: 'subtotal' }], 4, columns)).toThrow(/the kind 'subtotal'/);
    });

    it('ADR-0043 / ADR-0440: marks kept in .NET paint from the row and count from the state, and a press on a box crosses as a write naming the row', async () => {
        const dotnet = new FakeDotNet();
        const { root } = mount({
            options: options({ columns: [mark, book, notional], rowHeight: 24, marks: true }),
            state: state(1, [{ ...rates, c: [null, 'Rates', 1500], t: [null, null, '1,500.00'], m: true }, { ...credit, c: [null, 'Credit', 2500], t: [null, null, '2,500.00'], m: false }],
                { markCounts: { markedInResult: 1, rowsInResult: 2, markedOutsideResult: 0 } }),
        }, dotnet);
        await turns();
        const boxes = [...root.querySelectorAll('.ex-row .ex-mark')];
        expect(boxes.map(box => box.getAttribute('aria-checked'))).toEqual(['true', 'false']);
        expect(root.querySelector('.ex-mark-count')?.textContent).toMatch(/1/);
        const box = boxes[1];
        if (box === undefined) throw new Error('The second row has a box.');
        for (const name of ['mousedown', 'mouseup', 'click']) box.dispatchEvent(new MouseEvent(name, { bubbles: true, cancelable: true, button: 0, buttons: name === 'mousedown' ? 1 : 0 }));
        await turns();
        expect(dotnet.heard('mark')).toEqual([{ kind: 'oneRow', row: 2, marked: true }]);
    });

    it('ADR-0130 / ADR-0440: the component\'s OnSummarize is asked by number, its exact figure crossing as digits each way', async () => {
        const dotnet = new FakeDotNet();
        dotnet.answer = method => (method === 'Summarize'
            ? '{"kind":"answered","figures":{"average":{"kind":"number","number":2000},"count":{"kind":"number","number":2},"sum":{"kind":"number","number":4000,"exact":{"mantissa":"400000","scale":2}}},"texts":{"average":"2,000.00","count":"2","sum":"4,000.00"}}'
            : null);
        const { root } = mount({ options: options({ rowHeight: 24, summarizes: true, hears: ['selectionSummaryChanged'] }) }, dotnet);
        await turns();
        press(root, 130, 5);
        key('ArrowDown', { shiftKey: true });
        await turns();
        const asked = dotnet.calls.filter(call => call.method === 'Summarize').map(call => call.args);
        expect(asked).toHaveLength(1);
        expect(asked[0]?.slice(0, 2)).toEqual([1, null]);
        expect([...root.querySelectorAll('.ex-summary-figure')].map(figure => figure.textContent)).toContain('Sum: 4,000.00');
        const heard = dotnet.heard('selectionSummaryChanged').at(-1);
        expect(Reflect.get(Reflect.get(Reflect.get(Reflect.get(heard as object, 'result') as object, 'figures') as object, 'sum') as object, 'exact'))
            .toEqual({ mantissa: '400000', scale: 2 });
    });

    it('ADR-0050 item 9 / ADR-0440: a copy .NET answers is asked by number, and what it answers is written', async () => {
        const dotnet = new FakeDotNet();
        dotnet.answer = method => (method === 'Copy' ? '{"kind":"write","text":"Rates (desk)","html":"<table><tr><td>Rates (desk)</td></tr></table>"}' : null);
        const { root } = mount({ options: options({ rowHeight: 24, copies: true }) }, dotnet);
        await turns();
        press(root, 10, 5);
        const copy = new Event('copy', { bubbles: true, cancelable: true });
        Object.defineProperty(copy, 'clipboardData', { value: { setData: () => { throw new Error('A copy .NET answers later is not written on the event.'); } } });
        document.activeElement?.dispatchEvent(copy);
        await turns();
        expect(dotnet.calls.filter(call => call.method === 'Copy').map(call => call.args[0])).toEqual([1]);
        expect(await navigator.clipboard.readText()).toBe('Rates (desk)');
    });

    it('ADR-0440: a declared key crosses as a write with the selection it was pressed on', async () => {
        const dotnet = new FakeDotNet();
        const { root } = mount({ options: options({ rowHeight: 24, declaredKeys: ['F9'], hears: ['declaredKey'] }) }, dotnet);
        await turns();
        press(root, 10, 29);
        key('F9');
        await turns();
        expect(dotnet.heard('declaredKey')).toEqual([{
            key: 'F9', editOpen: false, rowSequenceVersion: 0,
            selection: expect.objectContaining({ focus: { row: 1, column: 0 } }) as unknown,
        }]);
    });

    it('ADR-0440: the Consumer\'s rowHeadings and findMatch are its own script, findMatch reading a row by its columns\' names', async () => {
        const matched: unknown[][] = [];
        const { root } = mount({
            options: options({ rowHeight: 24, rowHeadingWidth: 40, scripts: { rowHeadings: gridScript('rowHeadings'), findMatch: gridScript('findMatch') } }),
            source: { generation: 1, rowKey: false, vouchesDistinctRows: false, finds: true },
        },
            new FakeDotNet(), new FakeCollector(), {
                rowHeadings: (row: unknown) => `#${String(row)}`,
                findMatch: (...args: unknown[]) => { matched.push(args); return false; },
            });
        await turns();
        expect([...root.querySelectorAll('.ex-row-heading')].map(heading => heading.textContent)).toEqual(['#0', '#1']);
        press(root, 10, 5);
        key('f', { ctrlKey: true });
        await turns();
        type('Rates');
        await turns();
        expect(matched[0]?.slice(0, 2)).toEqual([{ Book: 'Rates', Notional: 1500 }, 'Book']);
    });
});
