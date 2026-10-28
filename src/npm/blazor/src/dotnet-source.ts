// The Grid Source a Blazor ExGrid hands its Script Core (ADR-0350 sections 7 and 16): .NET's own source, the
// Consumer's IGridSource, seen from the browser. Its state is what .NET last sent, the same object until it
// sends another, and what the grid tells a source crosses to .NET in the order the grid tells it. The source
// stays the Consumer's: it sorts, filters and answers in .NET, and the grid neither holds nor executes.

import {
    engineFilterOf, readGridQueryJson, type DistinctValues, type GridFilter, type GridFindRequest, type GridFindResult, type GridSource,
    type GridSourceState, type QueryColumn, type RowKeyOf, type RowRange, type SortSpec,
} from '@ex-grid/core';
import { filterText, sortsText } from './wire-query';
import type { BlazorRow, ColumnSet, RowSet, WireRow } from './wire-rows';
import { valueOf } from './wire-values';

/** What the bridge asks of the .NET object a Blazor ExGrid hands it: Blazor's `DotNetObjectReference`. */
export interface DotNetHandle {
    invokeMethodAsync(method: string, ...args: readonly unknown[]): Promise<unknown>;
}

/** The source's state as .NET sends it, numbered `seq` in the order .NET sends what carries rows. `query` is the
 * Sort and Filter in force, as the C# core's `GridViewStateJson.WriteQuery` writes them. */
export interface WireState {
    readonly seq: number;
    /** The columns' number the window's rows were read by. */
    readonly cv: number;
    readonly window: readonly WireRow[];
    readonly windowStart: number;
    readonly totalCount: number | null;
    readonly loading: boolean;
    readonly rowSequenceVersion: number;
    readonly query: string;
}

/** What .NET says of its source once, when the grid is attached: whether it names its rows by a Row Key, and
 * whether it vouches that no Window holds a row twice (ADR-0140, ADR-0141). */
export interface WireSourceTraits {
    /** Which of the component's sources this is, counted from 1: what it asks of .NET names it, so .NET answers
     * a source the Consumer has since replaced with nothing it would act on. */
    readonly generation: number;
    readonly rowKey: boolean;
    readonly vouchesDistinctRows: boolean;
    /** Whether the source answers a Find step (`IGridSource.CanFind`, ADR-0055): where it does not, Ctrl+F is refused. */
    readonly finds: boolean;
}

/** A Find step asked of .NET's source, the one numbered `generation` (ADR-0055). */
export type FindOf = (generation: number, request: GridFindRequest, signal: AbortSignal) => Promise<GridFindResult>;

let requests = 0;

/** .NET's Grid Source, read in the browser. */
export class DotNetSource implements GridSource<BlazorRow> {
    readonly #dotnet: DotNetHandle;
    readonly #rows: RowSet;
    readonly #columns: ColumnSet;
    readonly #listeners = new Set<() => void>();
    /** Which of the component's sources this is, as .NET counts them. */
    readonly generation: number;
    #state: GridSourceState<BlazorRow>;
    readonly rowKey?: RowKeyOf<BlazorRow>;
    readonly vouchesDistinctRows: boolean;
    /** A Find step, answered by .NET's source where it searches: the core refuses Ctrl+F where this is absent. */
    readonly find?: (request: GridFindRequest, signal: AbortSignal) => Promise<GridFindResult>;

    constructor(dotnet: DotNetHandle, rows: RowSet, columns: ColumnSet, traits: WireSourceTraits, find: FindOf) {
        this.#dotnet = dotnet;
        this.#rows = rows;
        this.#columns = columns;
        this.generation = traits.generation;
        this.vouchesDistinctRows = traits.vouchesDistinctRows;
        if (traits.rowKey) this.rowKey = keyOf;
        if (traits.finds) this.find = (request, signal) => find(this.generation, request, signal);
        this.#state = Object.freeze({
            window: Object.freeze([]), windowStart: 0, totalCount: undefined, loading: true, rowSequenceVersion: 0, sorts: Object.freeze([]), filter: undefined,
        });
    }

    /** Takes the state .NET sent, and tells everyone who listens. */
    take(state: WireState): void {
        // .NET sends columns before any state read by them, and with one when they change.
        if (state.cv !== this.#columns.version) throw new Error(`Internal: .NET sent rows read by columns ${state.cv}, and the grid has columns ${this.#columns.version}.`);
        const query = readGridQueryJson(state.query);
        const filter = engineFilterOf(query.filter, name => this.#columns.columns.find(column => column.name === name));
        this.#state = Object.freeze({
            window: this.#rows.read(state.window, state.seq, this.#columns),
            windowStart: state.windowStart,
            totalCount: state.totalCount === null ? undefined : state.totalCount,
            loading: state.loading,
            rowSequenceVersion: state.rowSequenceVersion,
            sorts: query.sorts,
            filter,
        });
        // A listener that stops listening while it is told does not stop the others hearing.
        for (const listener of Array.from(this.#listeners)) listener();
    }

    subscribe(listener: () => void): () => void {
        this.#listeners.add(listener);
        return () => { this.#listeners.delete(listener); };
    }

    snapshot(): GridSourceState<BlazorRow> {
        return this.#state;
    }

    /** The columns are the Blazor ExGrid's own, which it hands its source in .NET whenever they change, with the
     * values .NET reads of each row there: the browser's copies have nothing to add. */
    setColumns(columns: readonly QueryColumn<BlazorRow>[]): void {
        const declared = this.#columns.columns;
        if (columns.length !== declared.length || columns.some((column, i) => column.name !== declared[i]?.name)) {
            throw new Error('Internal: the grid told its source other columns than the Blazor ExGrid declared.');
        }
    }

    setSorts(sorts: readonly SortSpec[]): void {
        void this.#dotnet.invokeMethodAsync('SetSorts', this.generation, sortsText(sorts));
    }

    setFilter(filter: GridFilter | undefined): void {
        void this.#dotnet.invokeMethodAsync('SetFilter', this.generation, filterText(filter));
    }

    async needRange(range: RowRange): Promise<void> {
        await this.#dotnet.invokeMethodAsync('NeedRange', this.generation, range.start, range.count);
    }

    // Rows .NET read by columns it has since replaced are not the grid's rows: they are taken in and given up, and
    // the question is asked again of the columns the grid now has. Rows read by columns not yet here wait for them.
    // So what the grid shows never depends on which of the two messages crossed first (the spine's sixth rule).
    async rows(range: RowRange, signal: AbortSignal): Promise<readonly BlazorRow[]> {
        for (;;) {
            const answer = await this.#asked(signal, id => this.#dotnet.invokeMethodAsync('Rows', this.generation, id, range.start, range.count));
            const { seq, cv, rows } = answer as { readonly seq: number; readonly cv: number; readonly rows: readonly WireRow[] }; // oxlint-disable-line typescript/no-unsafe-type-assertion -- the shape ScriptRows writes; RowSet checks each member it reads
            await this.#columns.reached(cv, signal);
            if (signal.aborted || cv !== this.#columns.version) {
                this.#rows.discard(rows, seq);
                signal.throwIfAborted();
                continue;
            }
            return this.#rows.read(rows, seq, this.#columns);
        }
    }

    async distinctValues(column: string, signal: AbortSignal): Promise<DistinctValues> {
        for (;;) {
            const answer = await this.#asked(signal, id => this.#dotnet.invokeMethodAsync('DistinctValues', this.generation, id, column));
            const cv: unknown = Reflect.get(answer, 'cv');
            if (typeof cv !== 'number') throw new TypeError('.NET answered distinct values without the columns it read them by.');
            await this.#columns.reached(cv, signal);
            signal.throwIfAborted();
            if (cv !== this.#columns.version) continue;
            const kind: unknown = Reflect.get(answer, 'kind');
            // The columns that left this one out are the grid's now, and its next pass closes the column's panel,
            // which gives the question up: no answer is made up for a column .NET no longer declares.
            if (kind === 'gone') {
                if (this.#columns.columns.some(declared => declared.name === column)) {
                    throw new Error(`Internal: .NET answered column '${column}' gone under columns that declare it.`);
                }
                await givenUp(signal);
            }
            if (kind === 'tooMany') return Object.freeze({ kind: 'tooMany' });
            if (kind !== 'values') throw new TypeError(`.NET answered distinct values of the kind ${String(kind)}.`);
            const wire: unknown = Reflect.get(answer, 'values');
            const texts: unknown = Reflect.get(answer, 'texts');
            if (!Array.isArray(wire)) throw new TypeError('.NET answered distinct values without their values.');
            const values = Object.freeze(wire.map(valueOf));
            if (Array.isArray(texts)) this.#columns.holdColumnTexts(column, values, texts);
            return Object.freeze({ kind: 'values', values });
        }
    }

    // A question .NET answers later: given up, it is called off in .NET too. What an answer that still arrives
    // carries is the caller's to give up.
    async #asked(signal: AbortSignal, ask: (id: number) => Promise<unknown>): Promise<object> {
        signal.throwIfAborted();
        requests += 1;
        const id = requests;
        const callOff = (): void => { void this.#dotnet.invokeMethodAsync('Cancel', id); };
        signal.addEventListener('abort', callOff, { once: true });
        try {
            const answer = answerOf(await ask(id));
            if (typeof answer !== 'object' || answer === null) throw new TypeError('.NET answered the source\'s question with no answer.');
            return answer;
        } finally {
            signal.removeEventListener('abort', callOff);
        }
    }
}

/** Settles only as `signal` gives up, with its reason. */
async function givenUp(signal: AbortSignal): Promise<never> {
    await new Promise<void>(resolve => {
        if (signal.aborted) resolve();
        signal.addEventListener('abort', () => { resolve(); }, { once: true });
    });
    signal.throwIfAborted();
    throw new Error('Internal: a signal that gave up says it did not.');
}

/** An answer .NET wrote as JSON text. */
function answerOf(answer: unknown): unknown {
    if (typeof answer !== 'string') throw new TypeError('.NET answered the source\'s question with something other than JSON text.');
    return JSON.parse(answer);
}

function keyOf(row: BlazorRow): string | number | bigint {
    if (row.key === undefined) throw new Error(`Internal: row ${row.id} came from a source with a Row Key without one.`);
    return row.key;
}
