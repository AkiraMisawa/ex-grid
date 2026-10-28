// Blazor's ExPivot, the browser's half (ADR-0370 section 1): ExPivot's Script Core mounted on the element the
// component renders, given the component's parameters as options and its C# Pivot Source as a source it asks
// through .NET, and every event it raises handed to .NET in the order it was raised. It decides nothing and
// computes nothing: the report, the Field List and what each input means are the Script Core's, and the data
// is the Consumer's, in .NET.

import { builtInLabelIds, type AggregateResult, type GridSelection, type GridSummaryRequest, type GridSummaryResult, type SelectionSummary } from '@ex-grid/core';
import { decimalText } from '@ex-grid/data';
import { crossingCalls, HeldMessages, WireMetrics, type WireCellMetrics, type WirePresentationDefaults } from '@ex-grid/blazor-shared';
import { createPivot, type PivotChrome, type PivotChromeStylesheet, type PivotHandle, type PivotShowDetailsEvent, type PivotViewOptions } from '@ex-grid/pivot';
import { PivotJson, pivotWordIdList, readPivotLayoutJson, writePivotLayoutJson, type PivotLabel, type PivotLayout } from '@ex-grid/pivot-engine';
import { DotNetPivotSource, type DotNetHandle, type WireSource } from './dotnet-pivot-source';
import { RazorPivotSlots, type PivotGrids, type WireRazorSlots } from './razor-pivot-slots';

/** The events .NET listens to: each one the component has a handler for, and no other. The layout is always heard,
 * since the one the pivot raised is the one it is handed back. */
export type HeardKind = 'layoutChanged' | 'showFieldListChanged' | 'showDetails' | 'selectionChanged' | 'summaryFiguresChanged' | 'selectionSummaryChanged';

/** Every id the pivot and the grid it draws through have words for: its own, a date part's, and the grid's. .NET
 * cannot hand a function across, so it answers each of them once and sends the words (ADR-0370 section 10). */
export function labelIds(): readonly string[] {
    return Object.freeze([...new Set([...pivotWordIdList(), ...builtInLabelIds()])]);
}

/** The options .NET sets as they are, with no function or object of the browser's in them. */
type PassedOption =
    | 'showFieldList' | 'fieldListWidth' | 'viewportHeight' | 'viewportWidth' | 'rowHeight' | 'density' | 'maxLeaves' | 'maxRows' | 'maxColumns'
    | 'redrawInterval' | 'changeHighlightDuration' | 'detailsView' | 'showSelectionSummary' | 'summaryFigures' | 'slicing';

const passedOptions: readonly PassedOption[] = Object.freeze([
    'showFieldList', 'fieldListWidth', 'viewportHeight', 'viewportWidth', 'rowHeight', 'density', 'maxLeaves', 'maxRows', 'maxColumns',
    'redrawInterval', 'changeHighlightDuration', 'detailsView', 'showSelectionSummary', 'summaryFigures', 'slicing',
]);

/** The component's parameters, as the Script Core's options are spelt. .NET sends only what the Consumer set, and
 * the Script Core's own default stands for the rest, so a default is written once, in the Script Core. */
export interface WireOptions extends Partial<Pick<PivotViewOptions, PassedOption>> {
    readonly locale: string;
    /** The component's Cell Metrics, which beat a Wrapper's widths (ADR-0016). */
    readonly cellMetrics?: WireCellMetrics;
    /** A Wrapper's cascaded defaults (ADR-0030): the widths of the font it sets, its Density, hover band and Row Stripes. */
    readonly presentationDefaults?: WirePresentationDefaults;
    readonly hears: readonly HeardKind[];
    /** The stylesheets the Consumer's Chrome brings (ADR-0123), whose notices the pivot's root renders: none where absent. */
    readonly stylesheets?: readonly PivotChromeStylesheet[];
}

/** The Consumer's word for each of {@link labelIds} it answers, or `null` to keep the English. */
export type WireWords = Readonly<Record<string, string>> | null;

/** What `attach` is handed once: the parameters, the source, and the Consumer's layout, if it handed one. */
export interface WireAttach {
    readonly options: WireOptions;
    readonly source: WireSource;
    /** The layout in PivotLayoutJson, or `null` to start from nothing. */
    readonly layout: string | null;
    /** How many of the Consumer's layouts .NET has sent, this one included. */
    readonly layoutsSent: number;
    readonly words: WireWords;
    /** Whether .NET is across a circuit, Blazor Server's, whose hub limits the size of a message: only then is a
     * call larger than `inlineBytes` held for .NET to take as a stream (ADR-0005, as amended 2026-10-20). */
    readonly circuit: boolean;
    /** The slots the Consumer's Chrome fills with Razor (ADR-0410), the pivot's and the report grid's, for the pivot's
     * life; `null` for none. */
    readonly razorSlots: WireRazorSlots | null;
}

/** What `update` is handed: the parameters again, whole; another source, if the Consumer handed one; and a layout of
 * the Consumer's own making, if it handed one other than the one this pivot raised last. */
export interface WireUpdate {
    readonly options: WireOptions;
    readonly source?: WireSource;
    readonly layout?: string;
    /** How many of the Consumer's layouts .NET has sent, this one included; with `layout`. */
    readonly layoutsSent?: number;
    /** The Consumer's words, if it set another label. */
    readonly words?: WireWords;
}

/** What the component asks of the pivot it attached. Each call carries its data as the JSON text .NET wrote. */
export interface BlazorPivot {
    /** Takes a {@link WireUpdate}. */
    update(json: string): void;
    /** Takes what the source of `generation` said, a `PivotSourceChanged` in PivotJson. A source the Consumer has
     * since replaced is heard of no more. */
    changed(generation: number, json: string): void;
    /** The call held as `held`, too large for a message of its own, as the stream .NET reads it from. Taken once. */
    take(held: number): Promise<Uint8Array<ArrayBuffer>>;
    /** .NET rendered the contents of the Razor slot `id` for the data numbered `version` in `contents` (ADR-0410). */
    slotRendered(id: number, version: number, contents: HTMLElement | null): void;
    /** Stops the pivot and empties its element. */
    dispose(): void;
}

/** The calls that can carry more than a message holds, and that .NET's `Held` makes: `ScriptBridge.Held`. */
const heldPivotCalls: ReadonlySet<string> = new Set(['Heard', 'Aggregate', 'Items', 'Details', 'ItemCounts', 'MountSlot', 'UpdateSlot']);

/**
 * Mounts ExPivot's Script Core on `root`, the element the Blazor component rendered, with `dotnet` the component's
 * `DotNetObjectReference` and `json` a {@link WireAttach}. What it answers is the component's handle on the pivot.
 */
export function attach(root: HTMLElement, dotnet: DotNetHandle, json: string, makeCollector = browserCollector): BlazorPivot {
    const init = parsed<WireAttach>(json);
    let stopped = false;
    // A call too large for a message of its own, a layout's query over many items or a large selection, is held, and
    // .NET takes it in its turn: the hub would otherwise close the circuit.
    const held = new HeldMessages(init.circuit);
    const crossing = crossingCalls(dotnet, held, heldPivotCalls);
    // Once the pivot is stopped .NET has let go of the bridge, and nothing more crosses: a question still on its way is
    // the destroyed pivot's, which reads no answer, so it is left unanswered rather than failed.
    const bridge: DotNetHandle = {
        invokeMethodAsync: (method, ...args) => (stopped ? new Promise<never>(() => undefined) : crossing.invokeMethodAsync(method, ...args)),
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
    // The Consumer's words, made into one label, the same function until .NET sends others: the pivot holds words by the
    // function they came from.
    let label = labelOf(init.words);
    const send = (kind: HeardKind, payload: unknown): Promise<unknown> => bridge.invokeMethodAsync('Heard', kind, payload);
    // Records .NET did not take, as its handler was let go after the browser last heard of it, open as nobody had heard
    // them: the double click is answered as it was taken.
    const handBack = (event: PivotShowDetailsEvent): void => { pivot.openDetails(event); };
    const heard = handlers(send, raised => { layout = raised; }, () => layoutsSent, handBack);
    // The slots the Consumer's Chrome fills with Razor stand for the grid they are in: the pivot answers which, once it
    // stands. A standing part the pivot mounts as it is made, its toolbar's, holds as soon as the pivot is handed back.
    const made: { pivot?: PivotHandle } = {};
    const early: EarlyHold[] = [];
    const grids: PivotGrids = {
        gridAround: element => made.pivot?.gridAround(element),
        holdSettled: element => {
            if (made.pivot !== undefined) return made.pivot.holdSettled(element);
            const hold: EarlyHold = { element, release: undefined, gone: false };
            early.push(hold);
            return () => { hold.gone = true; hold.release?.(); };
        },
    };
    const slots = init.razorSlots === null ? undefined : new RazorPivotSlots(bridge, init.razorSlots, grids);
    const chrome = dressedChrome(slots?.chrome());
    const metrics = new WireMetrics();
    const pivot: PivotHandle = createPivot(root, pivotOptions(init.options, source, layout, label, heard, chrome(init.options), metrics));
    made.pivot = pivot;
    for (const hold of early.splice(0)) if (!hold.gone) hold.release = pivot.holdSettled(hold.element);
    return Object.freeze({
        update: (text: string) => {
            const next = parsed<WireUpdate>(text);
            // Taken only once the pivot has: one it refuses, as a layout naming a field the source lacks, changes nothing.
            const nextSource = next.source === undefined ? source : sourceOf(next.source);
            const nextLayout = next.layout === undefined ? layout : readPivotLayoutJson(next.layout);
            const nextLabel = next.words === undefined ? label : labelOf(next.words);
            pivot.update(pivotOptions(next.options, nextSource, nextLayout, nextLabel, heard, chrome(next.options), metrics));
            source = nextSource;
            layout = nextLayout;
            label = nextLabel;
            if (next.layoutsSent !== undefined) layoutsSent = next.layoutsSent;
        },
        changed: (generation: number, text: string) => {
            if (generation === source.generation) source.hear(text);
        },
        take: (number: number) => held.take(number),
        slotRendered: (id: number, version: number, contents: HTMLElement | null) => { slots?.rendered(id, version, contents); },
        dispose: () => {
            stopped = true;
            held.clear();
            slots?.dispose();
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

type Handlers = Required<Pick<PivotViewOptions,
    'onLayoutChanged' | 'onShowFieldListChanged' | 'onShowDetails' | 'onSelectionChanged' | 'onSummaryFiguresChanged' | 'onSelectionSummaryChanged'>>;

// One function per event, made once, so the pivot sees the same handler on every update. The layout raised is kept
// whether or not .NET listens, since it is the layout on screen.
function handlers(
    send: (kind: HeardKind, payload: unknown) => Promise<unknown>, raised: (layout: PivotLayout) => void, layoutsSent: () => number,
    handBack: (event: PivotShowDetailsEvent) => void,
): Handlers {
    return Object.freeze({
        onLayoutChanged: event => {
            raised(event.layout);
            void send('layoutChanged', { layout: writePivotLayoutJson(event.layout), layoutsSent: layoutsSent() });
        },
        onShowFieldListChanged: event => { void send('showFieldListChanged', event.shown); },
        // The figures menu's choice, which the pivot holds; .NET raises it as SummaryFigures.
        onSummaryFiguresChanged: figures => { void send('summaryFiguresChanged', figures); },
        // The Selection Summary as it stands, which .NET reads back as a SelectionSummary.
        onSelectionSummaryChanged: summary => { void send('selectionSummaryChanged', wireSummary(summary)); },
        // The report grid's Selection, which .NET reads back as a GridSelection; what its cells stand for is the
        // report's, in the browser, and is not carried yet.
        onSelectionChanged: event => { void send('selectionChanged', wireSelection(event.selection)); },
        // The cell and the question for its records: .NET asks them of the source the report was computed from, and
        // answers whether its handler took them.
        onShowDetails: (event: PivotShowDetailsEvent) => {
            const { source } = event;
            if (!(source instanceof DotNetPivotSource)) throw new TypeError('Internal: Show Details named a source the bridge never made.');
            void send('showDetails', {
                generation: source.generation,
                title: event.title,
                rowItems: event.rowItems,
                columnItems: event.columnItems,
                valueField: event.valueField === undefined ? null : event.valueField,
                query: PivotJson.writeDetailsQuery(event.query),
            }).then(taken => {
                if (taken !== true) handBack(event);
            });
        },
    });
}

/** A Selection as .NET reads it back (`GridSelection.Reported`): its ranges, its Focus, and which range holds the
 * Focus; no range and no Focus for none. Written as Blazor's ExGrid writes its own. */
export function wireSelection(selection: Pick<GridSelection, 'ranges' | 'focus' | 'focusRange'>): unknown {
    const { focus, focusRange } = selection;
    if (focus === undefined || focusRange === undefined) return { ranges: [], focus: null, focusRangeIndex: 0 };
    // By identity: the Focus range is one of the ranges, and a rectangle selected twice is two ranges alike.
    const focusRangeIndex = selection.ranges.indexOf(focusRange);
    if (focusRangeIndex < 0) throw new Error('Internal: a Selection\'s Focus range is not among its ranges.');
    return { ranges: selection.ranges, focus, focusRangeIndex };
}

/** A Selection Summary as .NET reads it back: its status, the question and the answer, an exact figure as its
 * decimal's digits, which no JSON number holds. */
export function wireSummary(summary: SelectionSummary): unknown {
    return { status: summary.status, request: summary.request === undefined ? null : wireRequest(summary.request), result: summary.result === undefined ? null : wireResult(summary.result) };
}

function wireRequest(request: GridSummaryRequest): unknown {
    return {
        ranges: request.ranges,
        columns: request.columns,
        rowSequenceVersion: request.rowSequenceVersion,
        figures: request.figures,
        focus: request.focus === undefined ? null : request.focus,
    };
}

function wireResult(result: GridSummaryResult): unknown {
    if (result.kind === 'declined') return { reason: result.reason };
    return {
        figures: Object.fromEntries(Object.entries(result.figures).map(([figure, answer]) => [figure, wireFigure(answer)])),
        texts: result.texts,
    };
}

function wireFigure(answer: AggregateResult): unknown {
    if (answer.kind !== 'number') return answer;
    return answer.exact === undefined ? { kind: 'number', number: answer.number } : { kind: 'number', exact: decimalText(answer.exact) };
}

// The label the Consumer's words make, or none to keep the English.
function labelOf(words: WireWords): PivotLabel | undefined {
    if (words === null) return undefined;
    const held: ReadonlyMap<string, string> = new Map(Object.entries(words));
    return id => held.get(id);
}

/** The Chrome the pivot is handed for the options .NET sent: the slots .NET fills, and the stylesheets the Consumer's
 * Chrome brings, the same object while neither changes. */
export function dressedChrome(slots: PivotChrome | undefined): (wire: WireOptions) => PivotChrome | undefined {
    let dressed: { readonly key: string; readonly chrome: PivotChrome | undefined } | undefined;
    return wire => {
        const { stylesheets } = wire;
        const key = stylesheets === undefined ? '' : JSON.stringify(stylesheets);
        if (dressed?.key === key) return dressed.chrome;
        const chrome = stylesheets === undefined ? slots : { ...slots, stylesheets };
        dressed = { key, chrome };
        return chrome;
    };
}

function pivotOptions(
    wire: WireOptions, source: DotNetPivotSource, layout: PivotLayout | undefined, label: PivotLabel | undefined, heard: Handlers,
    chrome: PivotChrome | undefined, metrics: WireMetrics,
): PivotViewOptions {
    const hears = new Set(wire.hears);
    const cellMetrics = metrics.cellMetrics(wire.cellMetrics);
    const presentationDefaults = metrics.presentationDefaults(wire.presentationDefaults);
    const passed = Object.fromEntries(passedOptions.filter(name => wire[name] !== undefined).map(name => [name, wire[name]]));
    return {
        ...(passed as Partial<Pick<PivotViewOptions, PassedOption>>),
        source,
        locale: wire.locale,
        ...(cellMetrics === undefined ? {} : { cellMetrics }),
        ...(presentationDefaults === undefined ? {} : { presentationDefaults }),
        ...(layout === undefined ? {} : { layout }),
        ...(label === undefined ? {} : { label }),
        ...(chrome === undefined ? {} : { chrome }),
        // Always heard: the layout the pivot raised is the one handed back on the next update.
        onLayoutChanged: heard.onLayoutChanged,
        ...(hears.has('showFieldListChanged') ? { onShowFieldListChanged: heard.onShowFieldListChanged } : {}),
        ...(hears.has('showDetails') ? { onShowDetails: heard.onShowDetails } : {}),
        ...(hears.has('selectionChanged') ? { onSelectionChanged: heard.onSelectionChanged } : {}),
        ...(hears.has('summaryFiguresChanged') ? { onSummaryFiguresChanged: heard.onSummaryFiguresChanged } : {}),
        ...(hears.has('selectionSummaryChanged') ? { onSelectionSummaryChanged: heard.onSelectionSummaryChanged } : {}),
    };
}

/** A Settled Mark held by a part the pivot mounted as it was made: taken once the pivot is handed back, unless let go first. */
interface EarlyHold {
    readonly element: Element;
    release: (() => void) | undefined;
    gone: boolean;
}
