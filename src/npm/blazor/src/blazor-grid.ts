// Blazor's ExGrid, the browser's half (ADR-0350 section 7, ADR-0370 section 1): the Script Core mounted on the
// element the component renders, given the component's parameters as options and its source's state as data,
// and every event it raises handed to .NET in the order it was raised. It decides nothing and computes nothing:
// what the grid shows and what each input means is the core's, and the rows, their order and what an intent
// does to them are the Consumer's, in .NET.

import {
    createGrid, type CellPosition, type CellState, type EditVerdict, type GridColumn, type GridErrorStepRequest, type GridFindResult,
    type GridHandle, type GridOptions, type GridRefusal, type GridSelection, type SelectionRange, type TemporalLike,
} from '@ex-grid/core';
import { DotNetSource, type DotNetHandle, type WireSourceTraits, type WireState } from './dotnet-source';
import { ColumnSet, RowSet, type BlazorRow, type RowCollector, type VerdictOf, type WireColumn } from './wire-rows';
import { WriteGate } from './write-gate';

/** The events .NET listens to: each one the component has a handler for, and no other, since a grid given a
 * handler can behave otherwise than one without (a grid without `onEdit` edits nothing). */
export type HeardKind =
    | 'selectionChanged' | 'edit' | 'paste' | 'fill' | 'clear' | 'undo' | 'redo' | 'refused' | 'columnWidthChanged' | 'columnOrderChanged'
    | 'columnHidden' | 'pinnedCountChanged' | 'headerGroupToggled' | 'editingChanged' | 'editDiscarded' | 'cellDoubleClick' | 'leave' | 'overwriteNotice'
    | 'action';

/** The component's parameters, as the core's options are spelt. .NET sends only what the Consumer set, and the
 * core's own default stands for the rest, so a default is written once, in the core. */
export interface WireOptions extends Partial<Pick<GridOptions<BlazorRow>, PassedOption>> {
    readonly columns: readonly WireColumn[];
    /** The columns' number, which .NET moves each time it declares other ones. */
    readonly columnsVersion: number;
    readonly locale: string;
    readonly hears: readonly HeardKind[];
    /** Whether the Consumer answers Cell States, which come with each row (ADR-0006). */
    readonly cellStates?: true;
    /** Where the Consumer answers a cell's message, which .NET is asked for as the popover opens (ADR-0034): the
     * number of its judgement, which moves when it hands another, so a message shown is asked again. */
    readonly cellMessages?: number;
    /** Whether the Consumer answers the error step, F8 and Shift+F8, which .NET is asked as it is taken (ADR-0381). */
    readonly findsErrors?: true;
}

/** The options .NET sets as they are, with no function or object of the browser's in them. */
type PassedOption =
    | 'density' | 'rowHeight' | 'headerHeight' | 'viewportHeight' | 'viewportWidth' | 'pinnedColumnCount' | 'virtualiseColumns' | 'stripeRows'
    | 'highlightFocusRow' | 'highlightHoverRow' | 'hideColumnMenu' | 'hideCopyWithHeaders' | 'headerClickSelects' | 'pasteMaySpill' | 'findsHeaders'
    | 'showSelectionSummary' | 'copyCellCap' | 'pasteByteCap' | 'headerGroups' | 'showFillHandle';

const passedOptions: readonly PassedOption[] = Object.freeze([
    'density', 'rowHeight', 'headerHeight', 'viewportHeight', 'viewportWidth', 'pinnedColumnCount', 'virtualiseColumns', 'stripeRows',
    'highlightFocusRow', 'highlightHoverRow', 'hideColumnMenu', 'hideCopyWithHeaders', 'headerClickSelects', 'pasteMaySpill', 'findsHeaders',
    'showSelectionSummary', 'copyCellCap', 'pasteByteCap', 'headerGroups', 'showFillHandle',
]);

/** What the component asks of the grid it attached. Each call carries its data as the JSON text .NET wrote, read
 * here in one parse rather than by Blazor's serialiser member by member. */
export interface BlazorGrid {
    /** Takes a {@link WireUpdate}: the parameters again, whole, and with them another source or its state. */
    update(json: string): void;
    /** Takes the source's {@link WireState}, which .NET sends whenever its source says it changed. */
    state(json: string): void;
    /** Places the Selection, as the core's handle does (ADR-0050 item 4). */
    placeSelection(range: SelectionRange, focus: CellPosition, rowSequenceVersion: number): boolean;
    /** Hands the keyboard to the grid (ADR-0080). */
    focus(): void;
    /** Gives the keyboard back to the grid, from the frame `from` when .NET names one, as the core's handle does
     * (DC-61, ADR-0070). */
    returnKeyboard(from: Element | null): void;
    /** Hands the keyboard the grid holds on to a control of the Consumer's own, as the core's handle does (DC-61,
     * ADR-0070). A control a render removed before the request landed reaches here as null, and keeps nothing to
     * hand on, as the core does with one no longer in the document. */
    handKeyboardTo(control: HTMLElement | null, preventScroll: boolean): void;
    /** Stops the grid and empties its element. */
    dispose(): void;
}

/** What `attach` is handed once: the parameters, what the source is, and its state then. */
export interface WireAttach {
    readonly options: WireOptions;
    readonly source: WireSourceTraits;
    readonly state: WireState;
}

/** What `update` is handed. `source` is another source of .NET's, the Consumer's next, which the grid hears as a
 * source replaced (ADR-0142): what the one before it answers from then on is never read. `state` comes with it,
 * and with columns that changed, since a row the browser holds was read by the old ones. */
export interface WireUpdate {
    readonly options: WireOptions;
    readonly source?: WireSourceTraits;
    readonly state?: WireState;
}

/** The browser's collector, unless a test hands its own. */
export function browserCollector(collected: (held: number) => void): RowCollector {
    const registry = new FinalizationRegistry<number>(collected);
    return { register: (row, held) => { registry.register(row, held); } };
}

/**
 * Mounts the Script Core on `root`, the element the Blazor component rendered, with `dotnet` the component's
 * `DotNetObjectReference` and `json` a {@link WireAttach}. What it answers is the component's handle on the grid.
 */
export function attach(root: HTMLElement, dotnet: DotNetHandle, json: string, makeCollector = browserCollector): BlazorGrid {
    const init = parsed<WireAttach>(json);
    // Each answer .NET is asked to give later is numbered, so the user's cancel calls that one off there (ADR-0380).
    let answers = 0;
    const asking = <T>(signal: AbortSignal, ask: (number: number) => Promise<T>): Promise<T> => {
        // Given up before it was asked: .NET is not asked, so no handler of its writes what the user took back.
        signal.throwIfAborted();
        answers += 1;
        const number = answers;
        const cancel = (): void => { void dotnet.invokeMethodAsync('CancelAnswer', number); };
        signal.addEventListener('abort', cancel, { once: true });
        return ask(number).finally(() => { signal.removeEventListener('abort', cancel); });
    };
    // Judged by the columns as the grid holds them now, which .NET keeps by their number (ADR-0380 section 3).
    const verdict: VerdictOf = (row, column, text, signal) => asking(signal, async number => {
        const answer = await dotnet.invokeMethodAsync('Validate', number, columns.version, row.id, column, text);
        // Cancelled meanwhile: .NET gave no verdict, and the grid reads none.
        signal.throwIfAborted();
        return verdictOf(answer, column);
    });
    const columns = new ColumnSet(verdict);
    columns.take(init.options.columns, init.options.columnsVersion);
    const rows = new RowSet(makeCollector, (ids, seen) => { void dotnet.invokeMethodAsync('Release', ids, seen); });
    const sourceOf = (traits: WireSourceTraits, state: WireState): DotNetSource => {
        const made = new DotNetSource(dotnet, rows, columns, traits);
        made.take(state);
        return made;
    };
    let source = sourceOf(init.source, init.state);

    const update = (next: WireUpdate): void => {
        // The columns first, so the rows that come with them are read by them.
        columns.take(next.options.columns, next.options.columnsVersion);
        if (next.source !== undefined) {
            if (next.state === undefined) throw new TypeError('Internal: .NET sent another source without its state.');
            source = sourceOf(next.source, next.state);
        } else if (next.state !== undefined) {
            source.take(next.state);
        }
        grid.update(gridOptions(next.options, columns, source, heard, paint, findError));
    };

    const gate = new WriteGate<WireUpdate, WireState>(message => {
        if ('update' in message) update(message.update);
        else source.take(message.state);
    }, held => { rows.discard(held.window, held.seq); });
    const write = (kind: HeardKind, payload: unknown): Promise<unknown> => gate.handling(dotnet.invokeMethodAsync('Heard', kind, payload));
    const heard = handlers(dotnet, write, asking);
    const paint = cellPaint(dotnet, columns);
    // Asked of the source the grid holds now, by its number. .NET sends what its source owes before it answers, and
    // answers a step asked of a source or an order it no longer holds as moved: once what a write of the user's holds
    // meanwhile is taken in, the core holds the newer one, and says the rows moved (ADR-0381, ADR-0142).
    const findError: FindErrorOf = (request, signal) => asking(signal, async number => {
        const askedOf = source;
        const answer = await dotnet.invokeMethodAsync('FindError', number, askedOf.generation, request);
        signal.throwIfAborted();
        const found = findResultOf(answer);
        if (found !== 'moved') return found;
        await gate.released();
        // A "none found" the core would read as said of the rows it shows: .NET moved without sending what moved.
        if (source === askedOf && source.snapshot().rowSequenceVersion === request.rowSequenceVersion) {
            throw new Error('Internal: .NET answered the error step as moved, and sent no other source or order.');
        }
        return { found: false };
    });
    const grid: GridHandle<BlazorRow> = createGrid(root, gridOptions(init.options, columns, source, heard, paint, findError));
    return Object.freeze({
        update: (text: string) => { gate.receive({ update: parsed<WireUpdate>(text) }); },
        state: (text: string) => { gate.receive({ state: parsed<WireState>(text) }); },
        placeSelection: (range: SelectionRange, focus: CellPosition, rowSequenceVersion: number) => {
            // .NET sent the source's state before it asked: the version is judged against that state.
            gate.flush();
            return grid.placeSelection(range, focus, rowSequenceVersion);
        },
        focus: () => { grid.focus(); },
        returnKeyboard: (from: Element | null) => { grid.returnKeyboard(from === null ? undefined : from); },
        handKeyboardTo: (control: HTMLElement | null, preventScroll: boolean) => {
            if (control !== null) grid.handKeyboardTo(control, { preventScroll });
        },
        dispose: () => {
            // Nothing is let go once .NET has let go of the bridge, and nothing held is taken in.
            gate.stop();
            rows.stop();
            grid.destroy();
        },
    });
}

/** JSON .NET wrote, in a shape the adapter's own C# writes, member for member. */
// oxlint-disable-next-line typescript/no-unnecessary-type-parameters -- the caller names the shape .NET wrote, and so the one assertion is here
export function parsed<T>(json: string): T {
    if (typeof json !== 'string') throw new TypeError('Internal: .NET sent the bridge something other than JSON text.');
    return JSON.parse(json) as T; // oxlint-disable-line typescript/no-unsafe-type-assertion -- the shapes ExGrid.Script writes; each reader checks what it reads
}

type Handlers = Required<Pick<GridOptions<BlazorRow>,
    | 'onSelectionChanged' | 'onEdit' | 'onPaste' | 'onFill' | 'onClear' | 'onUndo' | 'onRedo' | 'onRefused' | 'onColumnWidthChanged' | 'onColumnOrderChanged'
    | 'onColumnHidden' | 'onPinnedCountChanged' | 'onHeaderGroupToggled' | 'onEditingChanged' | 'onEditDiscarded' | 'onCellDoubleClick' | 'onLeave'
    | 'onOverwriteNotice' | 'onAction'>>;

type Asking = <T>(signal: AbortSignal, ask: (number: number) => Promise<T>) => Promise<T>;

// One function per event, made once, so the core sees the same handler on every update (ADR-0003). A write of the
// user's own goes by `write`, which holds what .NET sends until .NET has handled it. An edit, a paste and a fill are
// answered by .NET, which may refuse them: each is pending until it has (ADR-0380 sections 2 and 5).
function handlers(dotnet: DotNetHandle, write: (kind: HeardKind, payload: unknown) => Promise<unknown>, asking: Asking): Handlers {
    const send = (kind: HeardKind, payload: unknown): void => { void dotnet.invokeMethodAsync('Heard', kind, payload); };
    return Object.freeze({
        onSelectionChanged: (selection: GridSelection) => { send('selectionChanged', wireSelection(selection)); },
        onEdit: intent => asking(intent.signal, async answer => {
            const answered = await write('edit', {
                answer, row: intent.row.id, column: intent.column, value: intent.value, seenText: intent.seenText, replacedText: intent.replacedText,
            });
            intent.signal.throwIfAborted();
            const refusal = refusalOf(answered);
            if (refusal !== undefined) intent.refuse(refusal);
        }),
        onPaste: intent => asking(intent.signal, async answer => {
            const answered = await write('paste', {
                answer, targets: intent.plan.targets, source: intent.plan.source, collapsesSelection: intent.plan.collapsesSelection, values: intent.values,
                origins: intent.origins, rowSequenceVersion: intent.rowSequenceVersion, enteredAt: intent.enteredAt, fillSource: intent.fillSource,
            });
            intent.signal.throwIfAborted();
            const refusal = refusalOf(answered);
            // A paste is refused without a sentence of the grid's: telling the user why is the Consumer's (ADR-0050 item 3).
            if (refusal !== undefined) intent.refuse();
        }),
        onFill: intent => asking(intent.signal, async answer => {
            const answered = await write('fill', {
                answer, source: intent.source, target: intent.target, direction: intent.direction, rowSequenceVersion: intent.rowSequenceVersion,
            });
            intent.signal.throwIfAborted();
            // A fill, as a paste, is refused without a sentence of the grid's (ADR-0050 item 5).
            if (refusalOf(answered) !== undefined) intent.refuse();
        }),
        onClear: intent => { void write('clear', { targets: intent.targets, rowSequenceVersion: intent.rowSequenceVersion }); },
        onUndo: () => { send('undo', null); },
        onRedo: () => { send('redo', null); },
        onRefused: (refusal: GridRefusal<BlazorRow>) => { send('refused', wireRefusal(refusal)); },
        onColumnWidthChanged: change => { send('columnWidthChanged', change); },
        onColumnOrderChanged: order => { send('columnOrderChanged', order); },
        onColumnHidden: column => { send('columnHidden', column); },
        onPinnedCountChanged: count => { send('pinnedCountChanged', count); },
        onHeaderGroupToggled: toggle => { send('headerGroupToggled', toggle); },
        onEditingChanged: open => { send('editingChanged', open); },
        onEditDiscarded: reason => { send('editDiscarded', reason); },
        onCellDoubleClick: cell => { send('cellDoubleClick', cell); },
        onLeave: () => { send('leave', null); },
        onOverwriteNotice: notice => { send('overwriteNotice', notice); },
        onAction: event => { send('action', { row: event.row.id, column: event.column, action: event.action }); },
    });
}

/** What .NET answered a write of the user's own: `null` where it took it, and where it refused it, the Consumer's
 * sentence, empty for a paste or a fill, which carry none. */
function refusalOf(answer: unknown): string | undefined {
    if (answer === null) return undefined;
    if (typeof answer !== 'string') throw new TypeError('Internal: .NET answered a write with something other than a refusal.');
    return answer;
}

/** .NET's Edit Verdict, as `ExGrid.Script` writes it. */
function verdictOf(answer: unknown, column: string): EditVerdict {
    if (answer === null) throw new Error(`.NET gave no verdict on column '${column}': its Validate failed, and the component with it.`);
    if (typeof answer !== 'string') throw new TypeError(`Internal: .NET answered the verdict on column '${column}' with something other than JSON text.`);
    const verdict = parsed<{ readonly kind: unknown; readonly message?: unknown }>(answer);
    if (verdict.kind === 'accept') return { kind: 'accept' };
    if ((verdict.kind === 'flag' || verdict.kind === 'reject') && typeof verdict.message === 'string') return { kind: verdict.kind, message: verdict.message };
    throw new TypeError(`Internal: .NET answered the verdict on column '${column}' with ${JSON.stringify(verdict)}.`);
}

type FindErrorOf = (request: GridErrorStepRequest, signal: AbortSignal) => Promise<GridFindResult>;

/** .NET's answer to an error step, as `ExGrid.Script` writes a Find result, or `moved` where .NET did not ask the
 * Consumer, since what the step was asked under is gone there. A step .NET did not answer, its component gone or its
 * `OnFindError` taken away meanwhile, is given up: nothing moves, and nothing is claimed of cells nobody read. */
function findResultOf(answer: unknown): GridFindResult | 'moved' {
    if (answer === null) throw new DOMException('.NET gave the error step no answer: the component is gone.', 'AbortError');
    if (typeof answer !== 'string') throw new TypeError('Internal: .NET answered the error step with something other than JSON text.');
    const found = parsed<{ readonly found: unknown; readonly row?: unknown; readonly column?: unknown; readonly moved?: unknown; readonly withdrawn?: unknown }>(answer);
    if (found.withdrawn === true) throw new DOMException('The Consumer took OnFindError away as the step was asked.', 'AbortError');
    if (found.moved === true) return 'moved';
    if (found.found === false) return { found: false };
    if (found.found === true && typeof found.row === 'number' && typeof found.column === 'string') return { found: true, row: found.row, column: found.column };
    throw new TypeError(`Internal: .NET answered the error step with ${JSON.stringify(found)}.`);
}

type MessageOf = NonNullable<GridOptions<BlazorRow>['cellMessage']>;

interface Paint {
    readonly cellState: NonNullable<GridOptions<BlazorRow>['cellState']>;
    /** The message function for the Consumer's judgement numbered `judgement`: the same while the number is, and
     * another when it moves, which the core hears as another judgement and asks a message it shows again. */
    cellMessage(judgement: number): MessageOf;
}

// The display channels (ADR-0003): a cell's state comes with its row, so a row whose states change is another object,
// and a cell's message is asked of .NET as the popover opens, never while painting (ED-17).
function cellPaint(dotnet: DotNetHandle, columns: ColumnSet): Paint {
    let made: { readonly judgement: number; readonly ask: MessageOf } | undefined;
    const ask: MessageOf = async (row, column) => {
        const message: unknown = await dotnet.invokeMethodAsync('Message', row.id, column.name);
        if (message === null) return undefined;
        if (typeof message !== 'string') throw new TypeError(`Internal: .NET answered the message of column '${column.name}' with something other than text.`);
        return message;
    };
    return Object.freeze({
        cellState: (row: BlazorRow, column: GridColumn<BlazorRow>): CellState => {
            if (row.states === undefined) return 'normal';
            const state = row.states[columns.indexOf(column)];
            if (state === undefined) throw new Error(`Internal: row ${row.id} came with no Cell State for column '${column.name}'.`);
            return state;
        },
        cellMessage: (judgement: number): MessageOf => {
            if (made?.judgement !== judgement) made = { judgement, ask: (row, column) => ask(row, column) };
            return made.ask;
        },
    });
}

function gridOptions(wire: WireOptions, columns: ColumnSet, source: DotNetSource, heard: Handlers, paint: Paint, findError: FindErrorOf): GridOptions<BlazorRow> {
    const hears = new Set(wire.hears);
    const temporal: unknown = Reflect.get(globalThis, 'Temporal');
    const passed = Object.fromEntries(passedOptions.filter(name => wire[name] !== undefined).map(name => [name, wire[name]]));
    return {
        ...(passed as Partial<Pick<GridOptions<BlazorRow>, PassedOption>>),
        columns: columns.columns,
        source,
        locale: wire.locale,
        // A C# Consumer cannot hand the grid a JavaScript namespace, so Blazor's ExGrid hands it the browser's. Every
        // browser the family serves ships one (ADR-0017); where one does not, the core refuses a filterable Date column
        // by name, as it refuses one given none.
        ...(temporal === undefined ? {} : { temporal: temporal as TemporalLike }), // oxlint-disable-line typescript/no-unsafe-type-assertion -- the core checks it is a Temporal namespace as it takes it
        ...(wire.cellStates === true ? { cellState: paint.cellState } : {}),
        ...(wire.cellMessages === undefined ? {} : { cellMessage: paint.cellMessage(wire.cellMessages) }),
        ...(wire.findsErrors === true ? { findError } : {}),
        ...(hears.has('selectionChanged') ? { onSelectionChanged: heard.onSelectionChanged } : {}),
        ...(hears.has('edit') ? { onEdit: heard.onEdit } : {}),
        ...(hears.has('paste') ? { onPaste: heard.onPaste } : {}),
        ...(hears.has('fill') ? { onFill: heard.onFill } : {}),
        ...(hears.has('clear') ? { onClear: heard.onClear } : {}),
        ...(hears.has('undo') ? { onUndo: heard.onUndo } : {}),
        ...(hears.has('redo') ? { onRedo: heard.onRedo } : {}),
        ...(hears.has('refused') ? { onRefused: heard.onRefused } : {}),
        ...(hears.has('columnWidthChanged') ? { onColumnWidthChanged: heard.onColumnWidthChanged } : {}),
        ...(hears.has('columnOrderChanged') ? { onColumnOrderChanged: heard.onColumnOrderChanged } : {}),
        ...(hears.has('columnHidden') ? { onColumnHidden: heard.onColumnHidden } : {}),
        ...(hears.has('pinnedCountChanged') ? { onPinnedCountChanged: heard.onPinnedCountChanged } : {}),
        ...(hears.has('headerGroupToggled') ? { onHeaderGroupToggled: heard.onHeaderGroupToggled } : {}),
        ...(hears.has('editingChanged') ? { onEditingChanged: heard.onEditingChanged } : {}),
        ...(hears.has('editDiscarded') ? { onEditDiscarded: heard.onEditDiscarded } : {}),
        ...(hears.has('cellDoubleClick') ? { onCellDoubleClick: heard.onCellDoubleClick } : {}),
        ...(hears.has('leave') ? { onLeave: heard.onLeave } : {}),
        ...(hears.has('overwriteNotice') ? { onOverwriteNotice: heard.onOverwriteNotice } : {}),
        ...(hears.has('action') ? { onAction: heard.onAction } : {}),
    };
}

/** A Selection as .NET reads it back: its ranges, its Focus, and which range holds the Focus. */
export function wireSelection(selection: GridSelection): unknown {
    const { focus, focusRange } = selection;
    if (focus === undefined || focusRange === undefined) return { ranges: [], focus: null, focusRangeIndex: 0 };
    // By identity: the Focus range is one of the ranges, and a rectangle selected twice is two ranges alike.
    const focusRangeIndex = selection.ranges.indexOf(focusRange);
    if (focusRangeIndex < 0) throw new Error('Internal: a Selection\'s Focus range is not among its ranges.');
    return { ranges: selection.ranges, focus, focusRangeIndex };
}

/** A refusal as .NET reads it back: a row it names by its id, or `null` where the Window no longer holds it. */
function wireRefusal(refusal: GridRefusal<BlazorRow>): unknown {
    switch (refusal.kind) {
        case 'action': return { ...refusal, row: refusal.row === undefined ? null : refusal.row.id };
        case 'copy':
        case 'paste':
        case 'commit':
        case 'find':
        case 'mark':
        case 'placement': return refusal;
        default: throw new Error(`Internal: a refusal of the kind ${String((refusal as { readonly kind: unknown }).kind)}.`);
    }
}
