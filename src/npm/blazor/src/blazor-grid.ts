// Blazor's ExGrid, the browser's half (ADR-0350 section 7, ADR-0370 section 1): the Script Core mounted on the
// element the component renders, given the component's parameters as options and its source's state as data,
// and every event it raises handed to .NET in the order it was raised. It decides nothing and computes nothing:
// what the grid shows and what each input means is the core's, and the rows, their order and what an intent
// does to them are the Consumer's, in .NET.

import {
    createGrid, type CellPosition, type GridHandle,
    type GridOptions, type GridRefusal, type GridSelection, type SelectionRange, type TemporalLike,
} from '@ex-grid/core';
import { DotNetSource, type DotNetHandle, type WireSourceTraits, type WireState } from './dotnet-source';
import { ColumnSet, RowSet, type BlazorRow, type RowCollector, type WireColumn } from './wire-rows';
import { WriteGate } from './write-gate';

/** The events .NET listens to: each one the component has a handler for, and no other, since a grid given a
 * handler can behave otherwise than one without (a grid without `onEdit` edits nothing). */
export type HeardKind =
    | 'selectionChanged' | 'edit' | 'paste' | 'clear' | 'undo' | 'redo' | 'refused' | 'columnWidthChanged' | 'columnOrderChanged'
    | 'columnHidden' | 'pinnedCountChanged' | 'editingChanged' | 'editDiscarded' | 'cellDoubleClick' | 'leave' | 'overwriteNotice' | 'action';

/** The component's parameters, as the core's options are spelt. .NET sends only what the Consumer set, and the
 * core's own default stands for the rest, so a default is written once, in the core. */
export interface WireOptions extends Partial<Pick<GridOptions<BlazorRow>, PassedOption>> {
    readonly columns: readonly WireColumn[];
    /** The columns' number, which .NET moves each time it declares other ones. */
    readonly columnsVersion: number;
    readonly locale: string;
    readonly hears: readonly HeardKind[];
}

/** The options .NET sets as they are, with no function or object of the browser's in them. */
type PassedOption =
    | 'density' | 'rowHeight' | 'headerHeight' | 'viewportHeight' | 'viewportWidth' | 'pinnedColumnCount' | 'virtualiseColumns' | 'stripeRows'
    | 'highlightFocusRow' | 'highlightHoverRow' | 'hideColumnMenu' | 'hideCopyWithHeaders' | 'headerClickSelects' | 'pasteMaySpill' | 'findsHeaders'
    | 'showSelectionSummary' | 'copyCellCap' | 'pasteByteCap' | 'headerGroups';

const passedOptions: readonly PassedOption[] = Object.freeze([
    'density', 'rowHeight', 'headerHeight', 'viewportHeight', 'viewportWidth', 'pinnedColumnCount', 'virtualiseColumns', 'stripeRows',
    'highlightFocusRow', 'highlightHoverRow', 'hideColumnMenu', 'hideCopyWithHeaders', 'headerClickSelects', 'pasteMaySpill', 'findsHeaders',
    'showSelectionSummary', 'copyCellCap', 'pasteByteCap', 'headerGroups',
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
    const columns = new ColumnSet();
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
        grid.update(gridOptions(next.options, columns, source, heard));
    };

    const gate = new WriteGate<WireUpdate, WireState>(message => {
        if ('update' in message) update(message.update);
        else source.take(message.state);
    }, held => { rows.discard(held.window, held.seq); });
    const write = (kind: HeardKind, payload: unknown): void => { void gate.handling(dotnet.invokeMethodAsync('Heard', kind, payload)); };
    const heard = handlers(dotnet, write);
    const grid: GridHandle<BlazorRow> = createGrid(root, gridOptions(init.options, columns, source, heard));
    return Object.freeze({
        update: (text: string) => { gate.receive({ update: parsed<WireUpdate>(text) }); },
        state: (text: string) => { gate.receive({ state: parsed<WireState>(text) }); },
        placeSelection: (range: SelectionRange, focus: CellPosition, rowSequenceVersion: number) => {
            // .NET sent the source's state before it asked: the version is judged against that state.
            gate.flush();
            return grid.placeSelection(range, focus, rowSequenceVersion);
        },
        focus: () => { grid.focus(); },
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
    | 'onSelectionChanged' | 'onEdit' | 'onPaste' | 'onClear' | 'onUndo' | 'onRedo' | 'onRefused' | 'onColumnWidthChanged' | 'onColumnOrderChanged'
    | 'onColumnHidden' | 'onPinnedCountChanged' | 'onEditingChanged' | 'onEditDiscarded' | 'onCellDoubleClick' | 'onLeave' | 'onOverwriteNotice'
    | 'onAction'>>;

// One function per event, made once, so the core sees the same handler on every update (ADR-0003). A write of the
// user's own goes by `write`, which holds what .NET sends until .NET has handled it.
function handlers(dotnet: DotNetHandle, write: (kind: HeardKind, payload: unknown) => void): Handlers {
    const send = (kind: HeardKind, payload: unknown): void => { void dotnet.invokeMethodAsync('Heard', kind, payload); };
    return Object.freeze({
        onSelectionChanged: (selection: GridSelection) => { send('selectionChanged', wireSelection(selection)); },
        onEdit: intent => {
            write('edit', { row: intent.row.id, column: intent.column, value: intent.value, seenText: intent.seenText, replacedText: intent.replacedText });
        },
        onPaste: intent => {
            write('paste', {
                targets: intent.plan.targets, source: intent.plan.source, collapsesSelection: intent.plan.collapsesSelection, values: intent.values,
                origins: intent.origins, rowSequenceVersion: intent.rowSequenceVersion, enteredAt: intent.enteredAt, fillSource: intent.fillSource,
            });
        },
        onClear: intent => { write('clear', { targets: intent.targets, rowSequenceVersion: intent.rowSequenceVersion }); },
        onUndo: () => { send('undo', null); },
        onRedo: () => { send('redo', null); },
        onRefused: (refusal: GridRefusal<BlazorRow>) => { send('refused', wireRefusal(refusal)); },
        onColumnWidthChanged: change => { send('columnWidthChanged', change); },
        onColumnOrderChanged: order => { send('columnOrderChanged', order); },
        onColumnHidden: column => { send('columnHidden', column); },
        onPinnedCountChanged: count => { send('pinnedCountChanged', count); },
        onEditingChanged: open => { send('editingChanged', open); },
        onEditDiscarded: reason => { send('editDiscarded', reason); },
        onCellDoubleClick: cell => { send('cellDoubleClick', cell); },
        onLeave: () => { send('leave', null); },
        onOverwriteNotice: notice => { send('overwriteNotice', notice); },
        onAction: event => { send('action', { row: event.row.id, column: event.column, action: event.action }); },
    });
}

function gridOptions(wire: WireOptions, columns: ColumnSet, source: DotNetSource, heard: Handlers): GridOptions<BlazorRow> {
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
        ...(hears.has('selectionChanged') ? { onSelectionChanged: heard.onSelectionChanged } : {}),
        ...(hears.has('edit') ? { onEdit: heard.onEdit } : {}),
        ...(hears.has('paste') ? { onPaste: heard.onPaste } : {}),
        ...(hears.has('clear') ? { onClear: heard.onClear } : {}),
        ...(hears.has('undo') ? { onUndo: heard.onUndo } : {}),
        ...(hears.has('redo') ? { onRedo: heard.onRedo } : {}),
        ...(hears.has('refused') ? { onRefused: heard.onRefused } : {}),
        ...(hears.has('columnWidthChanged') ? { onColumnWidthChanged: heard.onColumnWidthChanged } : {}),
        ...(hears.has('columnOrderChanged') ? { onColumnOrderChanged: heard.onColumnOrderChanged } : {}),
        ...(hears.has('columnHidden') ? { onColumnHidden: heard.onColumnHidden } : {}),
        ...(hears.has('pinnedCountChanged') ? { onPinnedCountChanged: heard.onPinnedCountChanged } : {}),
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
