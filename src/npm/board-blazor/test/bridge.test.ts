// @vitest-environment happy-dom
// The bridge's own tests (ADR-0370 section 1): what crosses between .NET and ExBoard's Script Core. What the Board
// draws and decides is the Script Core's, tested there; here, that a Snapshot crosses once as its Arrow stream and is
// named by its number after, that a number no longer named is let go and .NET told so after what was raised from it,
// that updates apply in the order .NET sent them, and that an output and a Diff cross only when .NET listens.
import { describe, expect, it } from 'vitest';
import {
    BoardDiff, BoardOutput, LinkCondition, type BoardHandle, type BoardOptions,
} from '@ex-grid/board';
import { createSnapshot, decimalFromText, decimalText, writeArrowSnapshot, type Snapshot } from '@ex-grid/data';
import { attach, type DotNetHandle, type DotNetStream, type WireAttach, type WireUpdate } from '../src/blazor-board';

const boardJson = `{"version": 1, "inputs": {"trades": {"columns": [{"name": "desk", "type": "text"}, {"name": "pnl", "type": "decimal"}]}},
  "views": [{"id": "blotter", "grid": {"data": "trades", "columns": [{"column": "desk"}, {"column": "pnl", "format": "#,##0.00"}]}}],
  "layout": [{"views": ["blotter"]}]}`;

function trades(version: number, rows: readonly (readonly [string, string])[]): Snapshot {
    return createSnapshot({
        version,
        columns: [
            { name: 'desk', kind: 'text', values: rows.map(([desk]) => desk) },
            { name: 'pnl', kind: 'decimal', values: rows.map(([, pnl]) => decimalFromText(pnl)) },
        ],
    });
}

const first = trades(1, [['Rates', '100'], ['FX', '-50']]);
const second = trades(2, [['Rates', '75']]);

/** A Snapshot as Blazor's DotNetStreamReference hands it, read when `open` lets it. */
function stream(snapshot: Snapshot, open: Promise<void> = Promise.resolve()): DotNetStream {
    return {
        arrayBuffer: async () => {
            await open;
            const bytes = writeArrowSnapshot(snapshot);
            const copy = new ArrayBuffer(bytes.byteLength);
            new Uint8Array(copy).set(bytes);
            return copy;
        },
    };
}

/** A promise the test settles: `open` lets go what awaits it. */
function gate(): { readonly promise: Promise<void>; readonly open: () => void } {
    let open: (() => void) | undefined;
    const promise = new Promise<void>(resolve => { open = resolve; });
    if (open === undefined) throw new Error('A promise runs its executor at once.');
    return { promise, open };
}

/** .NET, as the bridge calls it: every call recorded. */
class FakeDotNet implements DotNetHandle {
    readonly calls: { readonly method: string; readonly args: readonly unknown[] }[] = [];

    invokeMethodAsync(method: string, ...args: readonly unknown[]): Promise<unknown> {
        this.calls.push({ method, args });
        return Promise.resolve(null);
    }

    of(method: string): readonly (readonly unknown[])[] {
        return this.calls.filter(call => call.method === method).map(call => call.args);
    }
}

/** The Script Core's stand-in: each options it was handed, in order. */
class Drawn {
    readonly options: BoardOptions[] = [];
    destroyed = false;

    readonly draw = (_: HTMLElement, options: BoardOptions): BoardHandle => {
        this.options.push(options);
        return {
            update: next => { this.options.push(next); },
            destroy: () => { this.destroyed = true; },
            bound: undefined,
        };
    };

    get last(): BoardOptions {
        const options = this.options.at(-1);
        if (options === undefined) throw new Error('Nothing was drawn.');
        return options;
    }
}

function attachJson(overrides: Partial<WireAttach> = {}): string {
    const init: WireAttach = {
        options: { width: 1200, locale: 'en-GB', hears: ['output', 'diff'] },
        circuit: false,
        board: boardJson,
        data: { trades: 1 },
        sent: [1],
        sources: {},
        diff: null,
        diffsSent: 0,
        razorSlots: null,
        ...overrides,
    };
    return JSON.stringify(init);
}

function updateJson(update: WireUpdate): string {
    return JSON.stringify(update);
}

function rowsOf(snapshot: Snapshot | undefined): unknown[] {
    if (snapshot === undefined) throw new Error('No data.');
    return Array.from({ length: snapshot.rowCount }, (_, row) => snapshot.columns.map(column => {
        const value = column.valueAt(row);
        return value !== null && typeof value === 'object' ? decimalText(value) : value;
    }));
}

describe('Blazor ExBoard seen from the browser (ADR-0370 section 1, ADR-0065)', () => {
    it('reads each Snapshot from its Arrow stream and binds the input that names its number', async () => {
        const drawn = new Drawn();
        await attach(document.createElement('div'), new FakeDotNet(), attachJson({ options: { width: 900, locale: 'en-GB', hears: [] } }), [stream(first)], drawn.draw);

        expect(drawn.options).toHaveLength(1);
        expect(drawn.last.board.views.map(view => view.id)).toEqual(['blotter']);
        expect(drawn.last.data['trades']?.version).toBe(1n);
        expect(rowsOf(drawn.last.data['trades'])).toEqual([['Rates', '100'], ['FX', '-50']]);
        expect(drawn.last.width).toBe(900);
        expect(drawn.last.locale).toBe('en-GB');
        expect(drawn.last.diff).toBeUndefined();
    });

    it('names a Snapshot sent before by its number alone, and lets go of one no longer named after telling .NET', async () => {
        const drawn = new Drawn();
        const dotnet = new FakeDotNet();
        const board = await attach(document.createElement('div'), dotnet, attachJson(), [stream(first)], drawn.draw);

        // Only the options moved: the data is the Snapshot already held, not read again.
        await board.update(updateJson({ options: { width: 1200, locale: 'de-DE', hears: [] }, sent: [] }), []);
        expect(drawn.last.data['trades']?.version).toBe(1n);
        expect(drawn.last.locale).toBe('de-DE');
        expect(dotnet.of('Release')).toEqual([]);

        await board.update(updateJson({ options: { width: 1200, locale: 'de-DE', hears: [] }, data: { trades: 2 }, sent: [2] }), [stream(second)]);
        expect(drawn.last.data['trades']?.version).toBe(2n);
        expect(dotnet.of('Release')).toEqual([[[1]]]);

        // Snapshot 1 is gone: naming it again is the bridge's own defect, said, never a Board drawn on nothing.
        await expect(board.update(updateJson({ options: { width: 1200, locale: 'de-DE', hears: [] }, data: { trades: 1 }, sent: [] }), []))
            .rejects.toThrow("input 'trades' names Snapshot 1, which .NET never sent or the browser let go");
    });

    it('applies updates in the order .NET sent them, whichever Snapshot is read first', async () => {
        const drawn = new Drawn();
        const board = await attach(document.createElement('div'), new FakeDotNet(), attachJson(), [stream(first)], drawn.draw);
        const slow = gate();
        const third = trades(3, [['FX', '1']]);

        const earlier = board.update(updateJson({ options: { width: 1200, locale: 'en-GB', hears: [] }, data: { trades: 2 }, sent: [2] }), [stream(second, slow.promise)]);
        const later = board.update(updateJson({ options: { width: 1200, locale: 'en-GB', hears: [] }, data: { trades: 3 }, sent: [3] }), [stream(third)]);
        await Promise.resolve();
        expect(drawn.options).toHaveLength(1);

        slow.open();
        await Promise.all([earlier, later]);
        expect(drawn.options.map(options => options.data['trades']?.version)).toEqual([1n, 2n, 3n]);
    });

    it('answers a failed update its failure, and applies the next', async () => {
        const drawn = new Drawn();
        const board = await attach(document.createElement('div'), new FakeDotNet(), attachJson(), [stream(first)], drawn.draw);

        await expect(board.update(updateJson({ options: { width: 1200, locale: 'en-GB', hears: [] }, data: { trades: 9 }, sent: [] }), []))
            .rejects.toThrow("input 'trades' names Snapshot 9");
        await board.update(updateJson({ options: { width: 1200, locale: 'en-GB', hears: [] }, data: { trades: 2 }, sent: [2] }), [stream(second)]);

        expect(drawn.last.data['trades']?.version).toBe(2n);
    });

    it('hands .NET an output with the number of the Snapshot it was taken from, and a Diff with the Diffs it was sent', async () => {
        const drawn = new Drawn();
        const dotnet = new FakeDotNet();
        await attach(document.createElement('div'), dotnet, attachJson({ diff: BoardDiff.empty.toJson(), diffsSent: 4 }), [stream(first)], drawn.draw);
        const held = drawn.last.data['trades'];
        if (held === undefined) throw new Error('No data.');

        const output = new BoardOutput('chosen', 'trades', held, new LinkCondition(['desk'], [['FX']]), ['text']);
        drawn.last.onOutput?.(output);
        drawn.last.onDiff?.(BoardDiff.empty);

        expect(drawn.last.diff?.sameAs(BoardDiff.empty)).toBe(true);
        expect(dotnet.of('Heard')).toEqual([
            ['output', { output: '{"output":"chosen","input":"trades","version":1,"on":["desk"],"values":[["FX"]]}', snapshot: 1, source: null }],
            ['diff', { diff: BoardDiff.empty.toJson(), diffsSent: 4 }],
        ]);
    });

    it('hands .NET only what it listens to, and nothing once the Board is stopped', async () => {
        const drawn = new Drawn();
        const dotnet = new FakeDotNet();
        const element = document.createElement('div');
        const board = await attach(element, dotnet, attachJson({ options: { width: 1200, locale: 'en-GB', hears: ['diff'] } }), [stream(first)], drawn.draw);
        const held = drawn.last.data['trades'];
        if (held === undefined) throw new Error('No data.');

        drawn.last.onOutput?.(new BoardOutput('chosen', 'trades', held, new LinkCondition(['desk'], [['FX']]), ['text']));
        expect(dotnet.of('Heard')).toEqual([]);

        board.dispose();
        drawn.last.onDiff?.(BoardDiff.empty);
        expect(drawn.destroyed).toBe(true);
        expect(dotnet.calls).toEqual([]);
    });
});
