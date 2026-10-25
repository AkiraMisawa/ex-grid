// The TypeScript engine's side of the protocol (protocol.ts): answers what engine-worker.ts answers, from
// ts-engine/ instead of .NET, so the pages and render-bench's driver are unchanged. It serves whatever
// port it is given: a Worker's scope (ts-engine-worker.ts) or a MessagePort on the page itself
// (ts-engine-page.ts), where its time is the page's.
//
// Each answer is JSON in the C# exports' shapes (EngineHost/Program.cs), with the same version numbers
// and the same engineMs. The requests are taken one at a time, in the order posted; every call here is
// synchronous, so the order holds without a queue.

import type { EngineCalls, EngineMessage, EngineRequest } from '../protocol';
import { Sheet, columnCount, rowCount } from './sheet';
import { Pivot } from './pivot';
import { documentJson, makeTrades, books, type Trades } from '../core-data';

/** Where requests arrive and answers go. */
export interface EnginePort {
    postMessage(message: EngineMessage): void;
    addEventListener(type: 'message', listener: (event: MessageEvent<EngineRequest>) => void): void;
}

let sheet: Sheet | undefined;
let sheetVersion = 0;
let pivot: Pivot | undefined;

const openSheet = (): Sheet => {
    if (sheet === undefined) throw new Error('No Sheet is open.');
    return sheet;
};
const openPivot = (): Pivot => {
    if (pivot === undefined) throw new Error('No trades are open.');
    return pivot;
};

const sheetWindow = (of: Sheet, start: number, count: number, columns: number): string => {
    const started = performance.now();
    const end = Math.min(rowCount, start + count);
    const rows: (string | null)[][] = [];
    for (let row = start; row < end; row++) {
        const texts: (string | null)[] = [];
        for (let column = 0; column < columns; column++) texts.push(of.displayAt(row, column));
        rows.push(texts);
    }
    const json = JSON.stringify(rows);
    return `"start":${start},"rows":${json},"windowMs":${performance.now() - started}`;
};

/** Sorted rows as runs of consecutive ones, as the C# export answers changedRows. */
const runs = (rows: readonly number[]): [number, number][] => {
    const sorted = [...rows].sort((a, b) => a - b);
    const answer: [number, number][] = [];
    for (const row of sorted) {
        const last = answer[answer.length - 1];
        if (last !== undefined && row <= last[1] + 1) last[1] = Math.max(last[1], row);
        else answer.push([row, row]);
    }
    return answer;
};

const wrapped = (json: string, engineMs: number): string =>
    // The TypeScript engine writes a Window's JSON as it builds it: engineMs holds both, jsonMs is 0.
    `{"engineMs":${engineMs},"jsonMs":0,"update":${json}}`;

type Handlers = { [K in keyof EngineCalls]: (...args: EngineCalls[K]) => string };

const handlers: Handlers = {
    SheetOpen: (json) => {
        const started = performance.now();
        sheet = Sheet.open(json);
        sheetVersion = 0;
        return JSON.stringify({ version: sheetVersion, rowCount, columnCount, engineMs: performance.now() - started });
    },
    SheetEnter: (address, typed, start, count, columns) => {
        const of = openSheet();
        const started = performance.now();
        const rows = of.enter(address, typed);
        const engineMs = performance.now() - started;
        sheetVersion++;
        return `{"version":${sheetVersion},"engineMs":${engineMs},"changedRows":${JSON.stringify(runs(rows))},${sheetWindow(of, start, count, columns)}}`;
    },
    SheetWindow: (start, count, columns) => `{"version":${sheetVersion},${sheetWindow(openSheet(), start, count, columns)}}`,
    PivotOpen: (json) => {
        const started = performance.now();
        pivot = new Pivot(JSON.parse(json) as Trades);
        return JSON.stringify({ trades: pivot.tradeCount, engineMs: performance.now() - started });
    },
    PivotWindow: (across, start, count) => {
        const started = performance.now();
        const json = openPivot().window(across, start, count);
        return wrapped(json, performance.now() - started);
    },
    PivotAmend: (index, delta, across, start, count) => {
        const of = openPivot();
        const started = performance.now();
        of.amend(index, delta);
        const json = of.window(across, start, count);
        return wrapped(json, performance.now() - started);
    },
    ProbeRun: (runtime, repeats) => probe(runtime, repeats),
};

/** EngineProbe's steps (EngineProbe/Probe.cs) that the TypeScript engine has, under the same names where
 * the step is the same; its open is one step, where the C# engine's is FromJson and Workbook.Open. */
const probe = (runtime: string, repeats: number): string => {
    const steps: Record<string, number[]> = {};
    const time = <T>(step: string, act: () => T): T => {
        const started = performance.now();
        const result = act();
        (steps[step] ??= []).push(performance.now() - started);
        return result;
    };
    for (let round = 0; round < repeats; round++) {
        const json = time('sheet: document to JSON', documentJson);
        const of = time('sheet: open from JSON (TypeScript: parse and first recalculation)', () => Sheet.open(json));
        for (let edit = 0; edit < 3; edit++) {
            const rows = time('sheet: edit A1 (20,000 recalculated)', () => of.enter('A1', String(1000 + 100 * round + edit)));
            (steps['sheet: edit A1, rows named'] ??= []).push(rows.length);
            time('sheet: read the painted cells', () => {
                let characters = 0;
                for (let row = 0; row < 22; row++) for (let column = 0; column < 17; column++) characters += of.displayAt(row, column)?.length ?? 0;
                return characters;
            });
            time('sheet: edit L1 (nothing reads it)', () => of.enter('L1', String(5000 + 100 * round + edit)));
        }
        const trades = time('pivot: make the trades', makeTrades);
        const report = time('pivot: source over the trades', () => new Pivot(trades));
        time('pivot: first report Window', () => report.window('Currency', 0, 80));
        const at = trades.book.findIndex((book) => book === books[0]);
        if (at < 0) throw new Error('Internal: no trade of the first book.');
        for (let amend = 0; amend < 3; amend++) {
            time('pivot: Change Batch applied', () => { report.amend(at, 1_000_003); });
            time('pivot: Window after one trade', () => report.window('Currency', 0, 80));
        }
        time('pivot: Window after the columns swapped', () => report.window('Product', 0, 80));
        report.window('Currency', 0, 80);
    }
    return JSON.stringify({ runtime, steps });
};

/** Answers every request posted to <port>, and says it is ready; <scriptMs> is when its script started. */
export const serve = (port: EnginePort, scriptMs: number): void => {
    port.addEventListener('message', (event) => {
        const request = event.data;
        const started = performance.now();
        try {
            const handler = handlers[request.method] as (...args: readonly unknown[]) => string;
            const json = handler(...request.args);
            port.postMessage({ kind: 'answer', id: request.id, json, workerMs: performance.now() - started });
        } catch (error) {
            port.postMessage({ kind: 'refused', id: request.id, error: String(error instanceof Error ? error.stack ?? error.message : error) });
        }
    });
    const readyMs = performance.now();
    port.postMessage({ kind: 'ready', scriptMs, createdMs: readyMs, exportsMs: readyMs, files: [] });
};
