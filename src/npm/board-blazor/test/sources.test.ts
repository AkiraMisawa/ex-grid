// @vitest-environment happy-dom
// A source held where the data lives, seen from the browser (ADR-0343, ADR-0344, ADR-0370 section 1): the C#
// BoardSource stays in .NET and is asked through it under its number. Here, that what is asked crosses as JSON with
// every value of the narrowing written exactly as its column is declared, that what .NET answers is read as the input
// declares its columns, that an abort tells .NET which question no longer waits, and that a source keeps its object
// while .NET names it by the same number and is let go, .NET told, once no message names it.
import { describe, expect, it } from 'vitest';
import { BoardOutput, LinkCondition, type BoardHandle, type BoardOptions, type BoardSource } from '@ex-grid/board';
import { createSnapshot, decimalFromText, decimalText, ticksFromText, writeArrowSnapshot } from '@ex-grid/data';
import { PivotJson, RecordPivotSource, dateItemKey, pivotQuery, pivotRecordField, readPivotLayoutJson, textItemKey } from '@ex-grid/pivot-engine';
import { attach, type DotNetHandle, type PivotCollector, type WireAttach, type WireUpdate } from '../src/blazor-board';
import type { WireSource } from '../src/dotnet-board-source';

const boardJson = `{"version": 1, "inputs": {"trades": {"columns": [{"name": "day", "type": "date"}, {"name": "desk", "type": "text"}, {"name": "pnl", "type": "decimal"}, {"name": "count", "type": "integer"}]}},
  "views": [{"id": "blotter", "grid": {"data": "trades", "columns": [{"column": "desk"}, {"column": "pnl", "format": "#,##0.00"}]}}],
  "layout": [{"views": ["blotter"]}]}`;

/** .NET, as the bridge calls it: every call recorded, and each method answered as the test says. */
class FakeDotNet implements DotNetHandle {
    readonly calls: { readonly method: string; readonly args: readonly unknown[] }[] = [];
    readonly answers = new Map<string, (...args: readonly unknown[]) => unknown>();

    invokeMethodAsync(method: string, ...args: readonly unknown[]): Promise<unknown> {
        this.calls.push({ method, args });
        const answer = this.answers.get(method);
        return Promise.resolve(answer === undefined ? null : answer(...args));
    }

    of(method: string): readonly (readonly unknown[])[] {
        return this.calls.filter(call => call.method === method).map(call => call.args);
    }
}

class Drawn {
    readonly options: BoardOptions[] = [];

    readonly draw = (_: HTMLElement, options: BoardOptions): BoardHandle => {
        this.options.push(options);
        return { update: next => { this.options.push(next); }, destroy: () => { this.options.length = 0; }, bound: undefined };
    };

    source(input: string): BoardSource {
        const source = this.options.at(-1)?.sources?.[input];
        if (source === undefined) throw new Error(`No source for '${input}'.`);
        return source;
    }
}

function attachJson(sources: WireAttach['sources'], hears: WireAttach['options']['hears'] = [], circuit = false): string {
    const init: WireAttach = { options: { width: 1200, locale: 'en-GB', hears }, circuit, board: boardJson, data: {}, sent: [], sources, diff: null, diffsSent: 0 };
    return JSON.stringify(init);
}

function updateJson(update: WireUpdate): string {
    return JSON.stringify(update);
}

/** The one call made. */
function only(calls: readonly (readonly unknown[])[]): readonly unknown[] {
    const [call, ...more] = calls;
    if (call === undefined || more.length > 0) throw new Error(`${calls.length} calls, not one.`);
    return call;
}

/** A Decimal as its text; anything else is the test's failure. */
function text(value: unknown): string {
    if (typeof value !== 'object' || value === null || !('scale' in value)) throw new Error(`Not a Decimal: ${String(value)}.`);
    return decimalText(value as Parameters<typeof decimalText>[0]);
}

/** A held call's arguments as .NET reads them: gzip-compressed where the browser compresses. */
async function unpacked(bytes: Uint8Array<ArrayBuffer>): Promise<string> {
    if (bytes[0] !== 0x1f) return new TextDecoder().decode(bytes);
    return new Response(new Blob([bytes]).stream().pipeThrough(new DecompressionStream('gzip'))).text();
}

const anyQuery = { range: { start: 0, count: 50 }, sorts: [], filter: undefined } as const;

describe('a source held where the data lives, asked through .NET (ADR-0343, ADR-0370 section 1)', () => {
    it('asks its shape and reads the columns .NET answers', async () => {
        const drawn = new Drawn();
        const dotnet = new FakeDotNet();
        dotnet.answers.set('Shape', () => '{"columns":[{"name":"desk","type":"text"},{"name":"pnl","type":"decimal"}]}');
        await attach(document.createElement('div'), dotnet, attachJson({ trades: { number: 3, thin: false, under: false, pivot: null } }), [], drawn.draw);

        const shape = await drawn.source('trades').shape();
        expect(shape.columns).toEqual([{ name: 'desk', kind: 'text' }, { name: 'pnl', kind: 'decimal' }]);
        expect(dotnet.of('Shape')).toEqual([[3, expect.any(Number)]]);
        expect('thin' in drawn.source('trades')).toBe(false);
    });

    it('sends a fetch with the narrowing written exactly as each column is declared, and reads the page from its Arrow stream', async () => {
        const drawn = new Drawn();
        const dotnet = new FakeDotNet();
        const rows = createSnapshot({ version: 7, columns: [{ name: 'desk', kind: 'text', values: ['FX'] }, { name: 'pnl', kind: 'decimal', values: [decimalFromText('12.50')] }] });
        dotnet.answers.set('Fetch', () => ({
            start: 4,
            totalCount: 9,
            rows: writeArrowSnapshot(rows),
        }));
        await attach(document.createElement('div'), dotnet, attachJson({ trades: { number: 3, thin: false, under: false, pivot: null } }), [], drawn.draw);

        const narrowing = [new LinkCondition(['day', 'pnl', 'count'], [[ticksFromText('2026-10-16'), decimalFromText('12345678901234567890.25'), 9007199254740993n]])];
        const page = await drawn.source('trades').fetch({ view: 'blotter', grid: anyQuery, narrowing }, new AbortController().signal);

        expect([page.start, page.totalCount, page.rows.version]).toEqual([4, 9, 7n]);
        expect(text(page.rows.column('pnl').valueAt(0))).toBe('12.5');
        const [number, , json] = only(dotnet.of('Fetch'));
        expect(number).toBe(3);
        expect(json).toBe('{"view":"blotter","grid":' + JSON.stringify('{"version":1,"range":{"start":0,"count":50},"sorts":[]}')
            + ',"narrowing":[{"on":["day","pnl","count"],"values":[["2026-10-16",12345678901234567890.25,9007199254740993]]}]}');
    });

    it('tells .NET which question no longer waits when the Board aborts it', async () => {
        const drawn = new Drawn();
        const dotnet = new FakeDotNet();
        let answer: ((value: unknown) => void) | undefined;
        dotnet.answers.set('Fetch', () => new Promise(resolve => { answer = resolve; }));
        await attach(document.createElement('div'), dotnet, attachJson({ trades: { number: 3, thin: false, under: false, pivot: null } }), [], drawn.draw);

        const abort = new AbortController();
        const fetched = drawn.source('trades').fetch({ view: 'blotter', grid: anyQuery, narrowing: [] }, abort.signal);
        await Promise.resolve();
        abort.abort();
        const [, call] = only(dotnet.of('Fetch'));
        expect(dotnet.of('Cancel')).toEqual([[call]]);
        answer?.(null);
        await expect(fetched).rejects.toThrow('something other than a page');
    });

    it('reads a chart\'s points as the input declares its columns, with what each thinned pixel held', async () => {
        const drawn = new Drawn();
        const dotnet = new FakeDotNet();
        dotnet.answers.set('Thin', () => JSON.stringify({ version: '12', points: '[{"count":3,"pnl":12345678901234567890.25},{"count":4,"pnl":null}]', thinned: [[3, 4, 1.5, 2, 6]] }));
        await attach(document.createElement('div'), dotnet, attachJson({ trades: { number: 3, thin: true, under: false, pivot: null } }), [], drawn.draw);

        const source = drawn.source('trades');
        if (source.thin === undefined) throw new Error('No thin.');
        const answer = await source.thin({ view: 'daily', chart: { data: 'trades', x: 'count', y: 'pnl', values: 'numbers', from: null, to: null, pixels: 300 }, narrowing: [] }, new AbortController().signal);

        expect(answer.version).toBe(12n);
        expect(answer.points.columns).toEqual(['count', 'pnl']);
        expect([answer.points.value('count', 0), text(answer.points.value('pnl', 0)), answer.points.value('pnl', 1)]).toEqual([3n, '12345678901234567890.25', null]);
        expect(answer.thinned).toEqual([{ from: 3, to: 4, min: 1.5, max: 2, count: 6 }]);
        expect(JSON.parse(String(only(dotnet.of('Thin'))[2]))).toEqual({
            view: 'daily', chart: { data: 'trades', x: 'count', y: 'pnl', values: 'numbers', from: null, to: null, pixels: 300 }, narrowing: [],
        });
    });

    it('keeps a source\'s object while .NET names it by the same number, and lets go of one no message names, telling .NET', async () => {
        const drawn = new Drawn();
        const dotnet = new FakeDotNet();
        const board = await attach(document.createElement('div'), dotnet, attachJson({ trades: { number: 3, thin: false, under: false, pivot: null } }), [], drawn.draw);
        const first = drawn.source('trades');

        await board.update(updateJson({ options: { width: 1200, locale: 'en-GB', hears: [] }, sent: [], sources: { trades: { number: 3, thin: false, under: false, pivot: null } } }), []);
        expect(drawn.source('trades')).toBe(first);
        expect(dotnet.of('ReleaseSources')).toEqual([]);

        await board.update(updateJson({ options: { width: 1200, locale: 'en-GB', hears: [] }, sent: [], sources: { trades: { number: 4, thin: false, under: false, pivot: null } } }), []);
        expect(drawn.source('trades')).not.toBe(first);
        expect(dotnet.of('ReleaseSources')).toEqual([[[3]]]);
    });

    it('holds a narrowing too long for one message of the circuit for .NET to take, and tells .NET nothing once the Board is stopped', async () => {
        const drawn = new Drawn();
        const dotnet = new FakeDotNet();
        let answer: ((value: unknown) => void) | undefined;
        dotnet.answers.set('Held', () => new Promise(resolve => { answer = resolve; }));
        const board = await attach(document.createElement('div'), dotnet, attachJson({ trades: { number: 3, thin: false, under: false, pivot: null } }, [], true), [], drawn.draw);

        const desks = Array.from({ length: 3000 }, (_, i) => [`Desk ${i}`]);
        const abort = new AbortController();
        const fetched = drawn.source('trades').fetch({ view: 'blotter', grid: anyQuery, narrowing: [new LinkCondition(['desk'], desks)] }, abort.signal);
        await Promise.resolve();
        expect(dotnet.of('Fetch')).toEqual([]);
        const [method, crossed] = only(dotnet.of('Held'));
        expect(method).toBe('Fetch');
        if (typeof crossed !== 'object' || crossed === null || !('held' in crossed) || typeof crossed.held !== 'number') throw new Error('Not a held call.');
        const [number, , json] = JSON.parse(await unpacked(await board.take(crossed.held))) as unknown[];
        expect(number).toBe(3);
        expect(json).toContain('"values":[["Desk 0"],["Desk 1"],');

        board.dispose();
        abort.abort();
        expect(dotnet.of('Cancel')).toEqual([]);
        answer?.(null);
        await expect(fetched).rejects.toThrow('something other than a page');
    });

    it('hands .NET an output taken from rows the source held with the source\'s number, for .NET to read as the input declares them', async () => {
        const drawn = new Drawn();
        const dotnet = new FakeDotNet();
        await attach(document.createElement('div'), dotnet, attachJson({ trades: { number: 3, thin: false, under: false, pivot: null } }, ['output']), [], drawn.draw);
        const page = createSnapshot({ version: 7, columns: [{ name: 'desk', kind: 'text', values: ['FX'] }] });

        drawn.options.at(-1)?.onOutput?.(new BoardOutput('chosen', 'trades', page, new LinkCondition(['desk'], [['FX']]), ['text']));

        expect(dotnet.of('Heard')).toEqual([['output', { output: '{"output":"chosen","input":"trades","version":7,"on":["desk"],"values":[["FX"]]}', snapshot: null, source: 3 }]]);
    });

    it('makes each Pivot Source over a narrowing, telling .NET what it was made over, and asks it as the pivot under its own number', async () => {
        const drawn = new Drawn();
        const dotnet = new FakeDotNet();
        // .NET's C# source, as the bundled source over the same rows answers.
        const inner = new RecordPivotSource([{ desk: 'FX', pnl: 3 }, { desk: 'Rates', pnl: 4 }], [
            pivotRecordField<{ desk: string; pnl: number }>({ name: 'desk', type: 'text', value: t => t.desk }),
            pivotRecordField<{ desk: string; pnl: number }>({ name: 'pnl', type: 'number', value: t => t.pnl }),
        ]);
        dotnet.answers.set('PivotAggregate', (...args) => PivotJson.writeAnswer(inner.aggregateNow(PivotJson.readQuery(String(args[2])))));
        let collect: ((pivot: number) => void) | undefined;
        const collector = (collected: (pivot: number) => void): PivotCollector => {
            collect = collected;
            return { register: () => undefined };
        };
        const pivot: WireSource['pivot'] = {
            fields: [
                { name: 'desk', type: 'text', caption: 'desk', orderKey: false, format: null, itemOrder: [], column: null, datePart: null },
                { name: 'pnl', type: 'number', caption: 'pnl', orderKey: false, format: null, itemOrder: [], column: null, datePart: null },
            ],
            aggregations: inner.features.aggregations,
            canRefresh: false,
            countsItems: false,
            answersLevels: false,
        };
        const board = await attach(document.createElement('div'), dotnet, attachJson({ trades: { number: 3, thin: false, under: false, pivot } }), [], drawn.draw, collector);
        const trades = drawn.source('trades');
        if (trades.pivot === undefined) throw new Error('No pivot over the source.');

        const whole = trades.pivot([]);
        const fx = trades.pivot([new LinkCondition(['desk'], [['FX']])]);
        expect(dotnet.of('MakePivot')).toEqual([[3, 1, '[]'], [3, 2, '[{"on":["desk"],"values":[["FX"]]}]']]);
        expect(whole.fields.map(field => field.name)).toEqual(['desk', 'pnl']);

        const answer = await fx.aggregate(pivotQuery({ rows: [{ field: 'desk' }], values: [{ field: 'pnl', parts: ['sum'] }] }));
        const [number] = only(dotnet.of('PivotAggregate'));
        expect(number).toBe(2);
        expect(answer.rows.length).toBeGreaterThan(0);

        // What .NET heard the C# source say reaches the one made under that number alone.
        const heard: (string | undefined)[] = [];
        if (fx.subscribe === undefined || whole.subscribe === undefined) throw new Error('A Pivot Source made over a source hears its notices.');
        fx.subscribe(changed => { heard.push(changed.sourceVersion); });
        whole.subscribe(() => { heard.push('whole'); });
        board.pivotChanged(2, PivotJson.writeSourceChanged({ sourceVersion: '8' }));
        expect(heard).toEqual(['8']);

        collect?.(1);
        expect(dotnet.of('ReleasePivot')).toEqual([[1]]);
    });

    it('asks the values under a pivot\'s cells with each path and value exact, and reads the answer as the input declares its columns', async () => {
        const drawn = new Drawn();
        const dotnet = new FakeDotNet();
        dotnet.answers.set('Under', () => '[{"desk":"FX","pnl":12345678901234567890.25},{"desk":"Rates","pnl":null}]');
        await attach(document.createElement('div'), dotnet, attachJson({ trades: { number: 3, thin: false, under: true, pivot: null } }), [], drawn.draw);
        const trades = drawn.source('trades');
        if (trades.under === undefined) throw new Error('No values under cells.');

        const layout = readPivotLayoutJson('{"version":1,"rows":[{"field":"desk","hiddenItems":[{"kind":"text","value":"Credit"}]}]}');
        const condition = await trades.under({
            view: 'byDesk', layout, cells: [{ row: [{ field: 'desk', item: textItemKey('FX') }], column: [{ field: 'day', item: dateItemKey(ticksFromText('2026-10-16')) }] }],
            on: ['desk', 'pnl'], narrowing: [],
        }, new AbortController().signal);

        const [number, , json] = only(dotnet.of('Under'));
        expect(number).toBe(3);
        const question = JSON.parse(String(json)) as { cells: string[]; layout: string; on: string[] };
        expect(question.on).toEqual(['desk', 'pnl']);
        expect(readPivotLayoutJson(question.layout).rows[0]?.hiddenItems.length).toBe(1);
        const [path, ...more] = question.cells;
        if (path === undefined || more.length > 0) throw new Error(`${question.cells.length} cells, not one.`);
        const cell = PivotJson.readDetailsQuery(path);
        expect([cell.rowItems.length, cell.columnItems.length]).toEqual([1, 1]);
        expect(condition.on).toEqual(['desk', 'pnl']);
        expect(condition.values.map(([desk, pnl]) => [desk, pnl === null ? null : text(pnl)])).toEqual([['FX', '12345678901234567890.25'], ['Rates', null]]);
    });
});
