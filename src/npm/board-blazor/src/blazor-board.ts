// Blazor's ExBoard, the browser's half (ADR-0370 section 1): ExBoard's Script Core mounted on the element the
// component renders, given the component's parameters as options, its Board as the JSON it was read from and its data
// as the Snapshots .NET sends, and every event it raises handed to .NET in the order it was raised. It decides nothing
// and computes nothing: the layout, the links, the outputs and the Board Diff are the Script Core's.
//
// A Snapshot crosses once, as the Arrow stream ADR-0065 has it travel, as every product's Snapshots cross (the Shared
// Script's HeldSnapshots): .NET numbers each Snapshot it sends, a later message names an input's data by that number
// alone, and the browser says which numbers it no longer holds, so .NET lets them go. A source held where the data
// lives (ADR-0343) stays in .NET and is asked through it; it crosses as a number the same way. A call too large for a
// message of Blazor Server's circuit, an output, a Diff or a question with a long narrowing, is held for .NET to take as
// a stream in its turn, as every product's are (the Shared Script's crossingCalls, ADR-0005).

import { BoardDiff, createBoard, readBoard, type BoardHandle, type BoardOptions, type BoardReading, type BoardSource } from '@ex-grid/board';
import { crossingCalls, HeldMessages, HeldSnapshots, type DotNetStream } from '@ex-grid/blazor-shared';
import type { TemporalLike } from '@ex-grid/core';
import type { BoardInput, Snapshot } from '@ex-grid/data';
import type { DotNetPivotSource } from '@ex-grid/pivot-blazor';
import { dotNetBoardSource, type PivotMaking, type WireSource } from './dotnet-board-source';

export type { DotNetStream } from '@ex-grid/blazor-shared';
export type { WireSource } from './dotnet-board-source';

/** What the bridge asks of the .NET object a Blazor ExBoard hands it: Blazor's `DotNetObjectReference`. */
export interface DotNetHandle {
    invokeMethodAsync(method: string, ...args: readonly unknown[]): Promise<unknown>;
}

/** The events .NET listens to: each one the component has a handler for, and no other. */
export type HeardKind = 'output' | 'diff';

/** The component's parameters, as the Script Core's options are spelt. */
export interface WireOptions {
    readonly width: number;
    readonly locale: string;
    readonly hears: readonly HeardKind[];
}

/** What `attach` is handed once. */
export interface WireAttach {
    readonly options: WireOptions;
    /** Whether .NET is across a circuit, Blazor Server's, whose hub limits the size of a message: only then is a call
     * larger than `inlineBytes` held for .NET to take as a stream (ADR-0005, as amended 2026-10-28). */
    readonly circuit: boolean;
    /** The Board's JSON, as .NET writes the Board it was handed. */
    readonly board: string;
    /** Each input's data, by the number of the Snapshot .NET sent for it. */
    readonly data: Readonly<Record<string, number>>;
    /** The numbers of the Snapshots this message carries, in the order of its streams. */
    readonly sent: readonly number[];
    /** Each input bound to a source held where the data lives, by the number .NET asks it under. */
    readonly sources: Readonly<Record<string, WireSource>>;
    /** The Consumer's Board Diff as its JSON, or `null` for none. */
    readonly diff: string | null;
    /** How many of the Consumer's Diffs .NET has sent, this one included. */
    readonly diffsSent: number;
}

/** What `update` is handed: the parameters again, whole, and whatever else changed since. */
export interface WireUpdate {
    readonly options: WireOptions;
    readonly board?: string;
    readonly data?: Readonly<Record<string, number>>;
    readonly sent: readonly number[];
    readonly sources?: Readonly<Record<string, WireSource>>;
    /** A Diff of the Consumer's own making, or `null` for none, when the parameter moved to one. */
    readonly diff?: string | null;
    /** With `diff`. */
    readonly diffsSent?: number;
}

/** What the component asks of the Board it attached. Each call carries its data as the JSON text .NET wrote, and
 * the Snapshots it sends as streams. */
export interface BlazorBoard {
    /** Takes a {@link WireUpdate}, after every update before it. */
    update(json: string, streams: readonly DotNetStream[]): Promise<void>;
    /** Passes on what .NET heard the C# Pivot Source made under `pivot` say, a `PivotSourceChanged` in PivotJson, to
     * the one made here under that number while the browser holds it. */
    pivotChanged(pivot: number, json: string): void;
    /** The arguments of the call .NET was handed `held` in place of, as the stream it reads them from. Taken once. */
    take(held: number): Promise<Uint8Array<ArrayBuffer>>;
    /** Stops the Board and empties its element. */
    dispose(): void;
}

/** The calls .NET's `Held` makes: each one that can carry what the Board or the Consumer made, an output, a Diff or a
 * link's narrowing of thousands of values. */
const heldBoardCalls: ReadonlySet<string> = new Set(['Heard', 'Fetch', 'Thin', 'Under', 'MakePivot', 'PivotAggregate', 'PivotItems', 'PivotDetails']);

/** What tells .NET that the browser let a Pivot Source made over a source go. */
export interface PivotCollector {
    register(source: DotNetPivotSource, pivot: number): void;
}

/** The browser's collector, unless a test hands its own. */
export function browserCollector(collected: (pivot: number) => void): PivotCollector {
    const registry = new FinalizationRegistry<number>(collected);
    return { register: (source, pivot) => { registry.register(source, pivot); } };
}

/** How the Board is drawn: ExBoard's Script Core, unless a test hands its own. */
export type DrawBoard = (element: HTMLElement, options: BoardOptions) => BoardHandle;

/**
 * Mounts ExBoard's Script Core on `root`, the element the Blazor component rendered, with `dotnet` the component's
 * `DotNetObjectReference`, `json` a {@link WireAttach} and `streams` the Snapshots it sends. What it answers, once the
 * Snapshots are read, is the component's handle on the Board.
 */
export async function attach(
    root: HTMLElement, dotnet: DotNetHandle, json: string, streams: readonly DotNetStream[], draw: DrawBoard = createBoard, makeCollector = browserCollector,
): Promise<BlazorBoard> {
    const init = parsed<WireAttach>(json);
    let stopped = false;
    const messages = new HeldMessages(init.circuit);
    const crossing = crossingCalls(dotnet, messages, heldBoardCalls);
    // Once the Board is stopped .NET has let go of the bridge, and nothing more crosses: a question still on its way is
    // the stopped Board's, which reads no answer, so it is left unanswered rather than failed.
    const bridge: DotNetHandle = {
        invokeMethodAsync: (method, ...args) => (stopped ? new Promise<never>(() => undefined) : crossing.invokeMethodAsync(method, ...args)),
    };
    const send = (method: string, ...args: readonly unknown[]): void => {
        void bridge.invokeMethodAsync(method, ...args);
    };
    const held = new HeldSnapshots();
    held.hold(await HeldSnapshots.read(init.sent, streams));
    let board: BoardReading = readBoard(init.board);
    let numbers = init.data;
    let data = held.data(numbers);
    let diff = init.diff === null ? undefined : BoardDiff.read(init.diff);
    let diffsSent = init.diffsSent;
    let options = init.options;
    // Each source by its number, the same object while .NET names it so, so the Board binds again only when another
    // source is handed in.
    const asked = new Map<number, BoardSource>();
    // .NET numbers a source anew under each Board's declaration of its input, so the Board read when a number is first
    // named is the one that numbered it.
    const declared = (input: string): BoardInput => {
        const found = board.inputs.find(i => i.name === input);
        if (found === undefined) throw new Error(`Internal: .NET sent a source for input '${input}', which the Board does not declare.`);
        return found;
    };
    // An abort after the Board is stopped tells .NET nothing: .NET cancels what is still out as it lets the Board go.
    const cancel = (call: number): void => { send('Cancel', call); };
    // Each Pivot Source a pivot over a source made, by its number while the browser holds it: its questions cross as
    // the pivot's (`PivotAggregate`, …), numbered apart from the sources' own, and .NET lets go of the C# one once the
    // browser has let go of it.
    const pivotsMade = new Map<number, WeakRef<DotNetPivotSource>>();
    const collector = makeCollector(pivot => {
        pivotsMade.delete(pivot);
        send('ReleasePivot', pivot);
    });
    let lastPivot = 0;
    const pivots: PivotMaking = {
        next: () => ++lastPivot,
        calls: { invokeMethodAsync: (method, ...args) => bridge.invokeMethodAsync(`Pivot${method}`, ...args) },
        made: source => {
            pivotsMade.set(source.generation, new WeakRef(source));
            collector.register(source, source.generation);
        },
    };
    const sourcesOf = (wire: Readonly<Record<string, WireSource>>): Readonly<Record<string, BoardSource>> | undefined => {
        const entries = Object.entries(wire);
        if (entries.length === 0) return undefined;
        return Object.freeze(Object.fromEntries(entries.map(([input, source]) => {
            let made = asked.get(source.number);
            if (made === undefined) {
                made = dotNetBoardSource(bridge, cancel, declared(input), source, pivots);
                asked.set(source.number, made);
            }
            return [input, made];
        })));
    };
    let sourceNumbers = init.sources;
    let sources = sourcesOf(sourceNumbers);
    // Raised with the number of the Snapshot it was taken from, so .NET takes it from the data it sent, or of the source
    // that held its rows, so .NET reads it as the Board that numbered the source declares the input; and with the number
    // of the Consumer's Diffs sent, so .NET knows whether one of theirs reached the screen after it.
    const onOutput: NonNullable<BoardOptions['onOutput']> = output => {
        const snapshot = numbers[output.input];
        const source = sourceNumbers[output.input];
        if (snapshot === undefined && source === undefined) {
            throw new Error(`Internal: an output was taken from input '${output.input}', which neither a Snapshot sent nor a source holds.`);
        }
        if (!options.hears.includes('output')) return;
        send('Heard', 'output', { output: output.toJson(), snapshot: snapshot === undefined ? null : snapshot, source: snapshot === undefined && source !== undefined ? source.number : null });
    };
    const onDiff: NonNullable<BoardOptions['onDiff']> = raised => {
        if (!options.hears.includes('diff')) return;
        send('Heard', 'diff', { diff: raised.toJson(), diffsSent });
    };
    const optionsOf = (): BoardOptions => boardOptions(options, board, data, sources, diff, onOutput, onDiff);
    const handle = draw(root, optionsOf());
    release(held, numbers, send);
    releaseSources(asked, sourceNumbers, send);
    // One update at a time, in the order .NET sent them, each once its Snapshots are read; their reading starts at once,
    // and what is read is held only as its update applies, so an update before it lets go of nothing it carries.
    // An update that fails answers .NET its failure, through the promise `update` answers; the next waits for it to
    // end, not for it to succeed.
    let queue = Promise.resolve();
    return Object.freeze({
        update: (text: string, sent: readonly DotNetStream[]) => {
            const next = parsed<WireUpdate>(text);
            const reading = HeldSnapshots.read(next.sent, sent);
            // Awaited in its turn; a failure of the reading before then is that turn's to answer, not the page's.
            reading.catch(ended); // oxlint-disable-line ex-grid/no-catch -- the failure is answered to .NET where the reading is awaited, in its turn
            const applied = queue.then(async () => {
                const snapshots = await reading;
                if (stopped) return;
                held.hold(snapshots);
                options = next.options;
                if (next.board !== undefined) board = readBoard(next.board);
                if (next.data !== undefined) {
                    numbers = next.data;
                    data = held.data(numbers);
                }
                if (next.sources !== undefined) {
                    sourceNumbers = next.sources;
                    sources = sourcesOf(sourceNumbers);
                }
                if (next.diff !== undefined) diff = next.diff === null ? undefined : BoardDiff.read(next.diff);
                if (next.diffsSent !== undefined) diffsSent = next.diffsSent;
                handle.update(optionsOf());
                release(held, numbers, send);
                releaseSources(asked, sourceNumbers, send);
            });
            queue = applied.then(ended, ended); // oxlint-disable-line ex-grid/no-catch -- the failure is answered to .NET by `applied`; the queue waits only for it to end
            return applied;
        },
        pivotChanged: (pivot: number, text: string) => {
            if (!stopped) pivotsMade.get(pivot)?.deref()?.hear(text);
        },
        take: (number: number) => messages.take(number),
        dispose: () => {
            stopped = true;
            handle.destroy();
            held.clear();
            messages.clear();
            asked.clear();
            pivotsMade.clear();
        },
    });
}

// An update ended, whether it failed or not: what it answered was .NET's to hear.
function ended(): void {
    // Nothing: the next update waits only for this one to end.
}

// Lets go of every Snapshot the data no longer names, and tells .NET, after anything raised from it was told.
function release(held: HeldSnapshots, numbers: Readonly<Record<string, number>>, send: (method: string, ...args: readonly unknown[]) => void): void {
    const gone = held.release(numbers);
    if (gone.length > 0) send('Release', gone);
}

// Lets go of every source the message no longer names, and tells .NET, which asks it nothing more for the browser.
function releaseSources(asked: Map<number, BoardSource>, named: Readonly<Record<string, WireSource>>, send: (method: string, ...args: readonly unknown[]) => void): void {
    const kept = new Set(Object.values(named).map(source => source.number));
    const gone = [...asked.keys()].filter(number => !kept.has(number));
    for (const number of gone) asked.delete(number);
    if (gone.length > 0) send('ReleaseSources', gone);
}

function boardOptions(
    wire: WireOptions, board: BoardReading, data: Readonly<Record<string, Snapshot>>, sources: Readonly<Record<string, BoardSource>> | undefined, diff: BoardDiff | undefined,
    onOutput: NonNullable<BoardOptions['onOutput']>, onDiff: NonNullable<BoardOptions['onDiff']>,
): BoardOptions {
    // The browser's Temporal, where it has one, for a grid's date filter; without it such a grid is refused by name.
    const temporal: unknown = Reflect.get(globalThis, 'Temporal');
    return {
        board,
        data,
        sources,
        diff,
        locale: wire.locale,
        width: wire.width,
        ...(temporal === undefined ? {} : { temporal: temporal as TemporalLike }), // oxlint-disable-line typescript/no-unsafe-type-assertion -- the core checks it is a Temporal namespace as it takes it
        onOutput,
        onDiff,
    };
}

/** JSON .NET wrote, in a shape the adapter's own C# writes, member for member. */
// oxlint-disable-next-line typescript/no-unnecessary-type-parameters -- the caller names the shape .NET wrote, and so the one assertion is here
export function parsed<T>(json: string): T {
    if (typeof json !== 'string') throw new TypeError('Internal: .NET sent the bridge something other than JSON text.');
    return JSON.parse(json) as T; // oxlint-disable-line typescript/no-unsafe-type-assertion -- the shapes ExBoard.Script writes; each reader checks what it reads
}
