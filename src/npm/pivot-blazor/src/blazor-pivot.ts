// Blazor's ExPivot, the browser's half (ADR-0370 section 1): ExPivot's Script Core mounted on the element the
// component renders, given the component's parameters as options and its C# Pivot Source as a source it asks
// through .NET, and every event it raises handed to .NET in the order it was raised. It decides nothing and
// computes nothing: the report, the Field List and what each input means are the Script Core's, and the data
// is the Consumer's, in .NET.

import { createPivot, type PivotHandle, type PivotViewOptions } from '@ex-grid/pivot';
import { readPivotLayoutJson, writePivotLayoutJson, type PivotLayout } from '@ex-grid/pivot-engine';
import { DotNetPivotSource, type DotNetHandle, type WireSource } from './dotnet-pivot-source';

/** The events .NET listens to: each one the component has a handler for, and no other. The layout is always heard,
 * since the one the pivot raised is the one it is handed back. */
export type HeardKind = 'layoutChanged' | 'showFieldListChanged';

/** The options .NET sets as they are, with no function or object of the browser's in them. */
type PassedOption =
    | 'showFieldList' | 'fieldListWidth' | 'viewportHeight' | 'viewportWidth' | 'rowHeight' | 'density' | 'maxLeaves' | 'maxRows' | 'maxColumns'
    | 'redrawInterval' | 'changeHighlightDuration' | 'detailsView';

const passedOptions: readonly PassedOption[] = Object.freeze([
    'showFieldList', 'fieldListWidth', 'viewportHeight', 'viewportWidth', 'rowHeight', 'density', 'maxLeaves', 'maxRows', 'maxColumns',
    'redrawInterval', 'changeHighlightDuration', 'detailsView',
]);

/** The component's parameters, as the Script Core's options are spelt. .NET sends only what the Consumer set, and
 * the Script Core's own default stands for the rest, so a default is written once, in the Script Core. */
export interface WireOptions extends Partial<Pick<PivotViewOptions, PassedOption>> {
    readonly locale: string;
    readonly hears: readonly HeardKind[];
}

/** What `attach` is handed once: the parameters, the source, and the Consumer's layout, if it handed one. */
export interface WireAttach {
    readonly options: WireOptions;
    readonly source: WireSource;
    /** The layout in PivotLayoutJson, or `null` to start from nothing. */
    readonly layout: string | null;
    /** How many of the Consumer's layouts .NET has sent, this one included. */
    readonly layoutsSent: number;
}

/** What `update` is handed: the parameters again, whole; another source, if the Consumer handed one; and a layout of
 * the Consumer's own making, if it handed one other than the one this pivot raised last. */
export interface WireUpdate {
    readonly options: WireOptions;
    readonly source?: WireSource;
    readonly layout?: string;
    /** How many of the Consumer's layouts .NET has sent, this one included; with `layout`. */
    readonly layoutsSent?: number;
}

/** What the component asks of the pivot it attached. Each call carries its data as the JSON text .NET wrote. */
export interface BlazorPivot {
    /** Takes a {@link WireUpdate}. */
    update(json: string): void;
    /** Takes what the source of `generation` said, a `PivotSourceChanged` in PivotJson. A source the Consumer has
     * since replaced is heard of no more. */
    changed(generation: number, json: string): void;
    /** Stops the pivot and empties its element. */
    dispose(): void;
}

/**
 * Mounts ExPivot's Script Core on `root`, the element the Blazor component rendered, with `dotnet` the component's
 * `DotNetObjectReference` and `json` a {@link WireAttach}. What it answers is the component's handle on the pivot.
 */
export function attach(root: HTMLElement, dotnet: DotNetHandle, json: string, makeCollector = browserCollector): BlazorPivot {
    const init = parsed<WireAttach>(json);
    let stopped = false;
    // Once the pivot is stopped .NET has let go of the bridge, and nothing more crosses: a question still on its way is
    // the destroyed pivot's, which reads no answer, so it is left unanswered rather than failed.
    const bridge: DotNetHandle = {
        invokeMethodAsync: (method, ...args) => (stopped ? new Promise<never>(() => undefined) : dotnet.invokeMethodAsync(method, ...args)),
    };
    // A source the browser no longer holds, an open Details tab's included, is let go in .NET.
    const collector = makeCollector(generation => { void bridge.invokeMethodAsync('Release', generation); });
    const sourceOf = (wire: WireSource): DotNetPivotSource => {
        const made = new DotNetPivotSource(bridge, wire);
        collector.register(made, wire.generation);
        return made;
    };
    let source = sourceOf(init.source);
    // The layout on screen as the pivot knows it: the Consumer's, read once, or the one the pivot raised last, the same
    // object, so handing it back asks nothing.
    let layout: PivotLayout | undefined = init.layout === null ? undefined : readPivotLayoutJson(init.layout);
    // How many of the Consumer's layouts the pivot has been given: told with each layout it raises, so .NET knows
    // whether one of the Consumer's reached the screen after it.
    let layoutsSent = init.layoutsSent;
    const send = (kind: HeardKind, payload: unknown): void => { void bridge.invokeMethodAsync('Heard', kind, payload); };
    const heard = handlers(send, raised => { layout = raised; }, () => layoutsSent);
    const pivot: PivotHandle = createPivot(root, pivotOptions(init.options, source, layout, heard));
    return Object.freeze({
        update: (text: string) => {
            const next = parsed<WireUpdate>(text);
            // Taken only once the pivot has: one it refuses, as a layout naming a field the source lacks, changes nothing.
            const nextSource = next.source === undefined ? source : sourceOf(next.source);
            const nextLayout = next.layout === undefined ? layout : readPivotLayoutJson(next.layout);
            pivot.update(pivotOptions(next.options, nextSource, nextLayout, heard));
            source = nextSource;
            layout = nextLayout;
            if (next.layoutsSent !== undefined) layoutsSent = next.layoutsSent;
        },
        changed: (generation: number, text: string) => {
            if (generation === source.generation) source.hear(text);
        },
        dispose: () => {
            stopped = true;
            pivot.destroy();
        },
    });
}

/** What tells .NET that the browser let a source go. */
export interface SourceCollector {
    register(source: DotNetPivotSource, generation: number): void;
}

/** The browser's collector, unless a test hands its own. */
export function browserCollector(collected: (generation: number) => void): SourceCollector {
    const registry = new FinalizationRegistry<number>(collected);
    return { register: (source, generation) => { registry.register(source, generation); } };
}

/** JSON .NET wrote, in a shape the adapter's own C# writes, member for member. */
// oxlint-disable-next-line typescript/no-unnecessary-type-parameters -- the caller names the shape .NET wrote, and so the one assertion is here
export function parsed<T>(json: string): T {
    if (typeof json !== 'string') throw new TypeError('Internal: .NET sent the bridge something other than JSON text.');
    return JSON.parse(json) as T; // oxlint-disable-line typescript/no-unsafe-type-assertion -- the shapes ExPivot.Script writes; each reader checks what it reads
}

type Handlers = Required<Pick<PivotViewOptions, 'onLayoutChanged' | 'onShowFieldListChanged'>>;

// One function per event, made once, so the pivot sees the same handler on every update. The layout raised is kept
// whether or not .NET listens, since it is the layout on screen.
function handlers(send: (kind: HeardKind, payload: unknown) => void, raised: (layout: PivotLayout) => void, layoutsSent: () => number): Handlers {
    return Object.freeze({
        onLayoutChanged: event => {
            raised(event.layout);
            send('layoutChanged', { layout: writePivotLayoutJson(event.layout), layoutsSent: layoutsSent() });
        },
        onShowFieldListChanged: event => { send('showFieldListChanged', event.shown); },
    });
}

function pivotOptions(wire: WireOptions, source: DotNetPivotSource, layout: PivotLayout | undefined, heard: Handlers): PivotViewOptions {
    const hears = new Set(wire.hears);
    const passed = Object.fromEntries(passedOptions.filter(name => wire[name] !== undefined).map(name => [name, wire[name]]));
    return {
        ...(passed as Partial<Pick<PivotViewOptions, PassedOption>>),
        source,
        locale: wire.locale,
        ...(layout === undefined ? {} : { layout }),
        // Always heard: the layout the pivot raised is the one handed back on the next update.
        onLayoutChanged: heard.onLayoutChanged,
        ...(hears.has('showFieldListChanged') ? { onShowFieldListChanged: heard.onShowFieldListChanged } : {}),
    };
}
