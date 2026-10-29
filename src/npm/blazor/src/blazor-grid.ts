// Blazor's ExGrid, the browser's half (ADR-0350 section 7, ADR-0370 section 1): the Script Core mounted on the
// element the component renders, given the component's parameters as options and its source's state as data,
// and every event it raises handed to .NET in the order it was raised. It decides nothing and computes nothing:
// what the grid shows and what each input means is the core's, and the rows, their order and what an intent
// does to them are the Consumer's, in .NET.

import {
    createGrid, EditorCompletion, EditorHint, gridInputRoot, type AggregateResult, type CellAppearance, type CellPosition, type CompletionCandidate, type CellState, type EditVerdict,
    type GridChromeStylesheet, type GridColumn, type GridCommand, type GridContextMenuContext, type GridCopyAnswer, type GridCopyRequest, type GridDeclaredKeyPress, type GridDirection, type GridErrorStepRequest, type GridEditorReference,
    type GridEditorRewrite, type GridFindPattern, type GridFindResult, type GridHandle, type GridOptions, type GridPointedAt, type GridRefusal, type GridReplaceRequest, type GridReplaceResult,
    type GridSelection, type GridSummaryRequest, type GridSummaryResult, type RowKind, type RowMarkCounts, type RowMarkIntent, type RowMarks, type SelectionRange, type SelectionSummary,
    type SummaryFigure, type TemporalLike, type GridTurn, type GridViewRead, type ViewPlace, findMatches, textOf,
} from '@ex-grid/core';
import {
    awaitPointingScope, crossingCalls, describeAnswer, HeldMessages, isGridRazorSlotName, keyOf, ScriptFunctionError, scriptFunctionName, ScriptModules,
    slotHostOptionsOf, WireMetrics, type DotNetCalls, type WireHeld, type GridRazorSlotName, type LoadedScriptFunction, type WireCellMetrics, type WirePresentationDefaults, type WireScript,
} from '@ex-grid/blazor-shared';
import { InputNumbers, LostCall } from './input-numbers';
import { DotNetSource, type DotNetHandle, type SummarizeOf, type WireSourceTraits, type WireState } from './dotnet-source';
import { RazorSlots } from './razor-slots';
import { ColumnSet, RowSet, type BlazorRow, type RowCollector, type VerdictOf, type WireColumn } from './wire-rows';
import { WriteGate } from './write-gate';

/** The events .NET listens to: each one the component has a handler for, and no other, since a grid given a
 * handler can behave otherwise than one without (a grid without `onEdit` edits nothing). */
export type HeardKind =
    | 'selectionChanged' | 'edit' | 'paste' | 'fill' | 'clear' | 'undo' | 'redo' | 'refused' | 'columnWidthChanged' | 'columnOrderChanged'
    | 'columnHidden' | 'pinnedCountChanged' | 'headerGroupToggled' | 'editingChanged' | 'editDiscarded' | 'cellDoubleClick' | 'leave' | 'overwriteNotice'
    | 'action' | 'nameBoxEntered' | 'referenceKeyColoursChanged' | 'declaredKey' | 'selectionSummaryChanged' | 'summaryFiguresChanged'
    | 'replace' | 'mark' | 'parentWithoutHeight';

/** The component's parameters, as the core's options are spelt. .NET sends only what the Consumer set, and the
 * core's own default stands for the rest, so a default is written once, in the core. */
export interface WireOptions extends Partial<Pick<GridOptions<BlazorRow>, PassedOption>> {
    readonly columns: readonly WireColumn[];
    /** The columns' number, which .NET moves each time it declares other ones. */
    readonly columnsVersion: number;
    readonly locale: string;
    /** The component's Cell Metrics, which beat a Wrapper's widths (ADR-0016). */
    readonly cellMetrics?: WireCellMetrics;
    /** A Wrapper's cascaded defaults (ADR-0030): the widths of the font it sets, its Density, hover band and Row Stripes. */
    readonly presentationDefaults?: WirePresentationDefaults;
    readonly hears: readonly HeardKind[];
    /** Whether the Consumer answers Cell States, which come with each row (ADR-0006). */
    readonly cellStates?: true;
    /** Where the Consumer answers a cell's message, which .NET is asked for as the popover opens (ADR-0034): the
     * number of its judgement, which moves when it hands another, so a message shown is asked again. */
    readonly cellMessages?: number;
    /** Whether the source is `GridSource.From`'s, which finds by the reference rule over the text .NET formats, so the
     * painted matches are marked by that rule over the texts the browser holds (ADR-0332 §4). */
    readonly referenceMarks?: true;
    /** Whether the Consumer answers the error step, F8 and Shift+F8, which .NET is asked as it is taken (ADR-0381). */
    readonly findsErrors?: true;
    /** Whether the Consumer answers completion, which .NET is asked as the editor's text or caret moves (ADR-0051). */
    readonly completesText?: true;
    /** Whether the Consumer adds commands to the Context Menu, which .NET is asked for as it opens (ADR-0421). */
    readonly contextCommands?: true;
    /** The Consumer's words for the grid's own, by id, as .NET's `CommandLabel` answers each id the grid words
     * (ADR-0440 section 4). */
    readonly commandLabels?: Readonly<Record<string, string>>;
    /** The Chrome slots the Consumer's Chrome fills with Razor, which .NET renders as each is mounted (ADR-0410). */
    readonly razorSlots?: readonly GridRazorSlotName[];
    /** The stylesheets the Consumer's Chrome brings (ADR-0123), whose notices the grid's root renders: none where absent. */
    readonly stylesheets?: readonly GridChromeStylesheet[];
    /** The Pointing Scope's declaration the grid took as it was made, which it joins in the page (ADR-0430). */
    readonly pointedAt?: WirePointedAt;
    /** The Consumer's functions of Formula entry, its own script, loaded before the grid was made (ADR-0431), and the
     * other functions the browser asks as it paints or decides a key (ADR-0440). */
    readonly scripts?: WireScripts;
    /** Whether the Consumer answers each row's Row Kind, which comes with the row (ADR-0024). */
    readonly rowKinds?: true;
    /** Whether the Consumer answers cells' Font, Fill and Borders, which come with each row (ADR-0050 item 15). */
    readonly appearances?: true;
    /** Whether the Consumer answers when cells changed, which comes with each row (ADR-0068). */
    readonly changeTimes?: true;
    /** Whether the grid has Row Marks, kept in .NET by the component's `Marks` or its source's (ADR-0043): each row comes
     * with whether it is marked, and the state with the counts. */
    readonly marks?: true;
    /** Whether the Consumer answers each copy in place of the grid's assembly, asked of .NET (ADR-0050 item 9). */
    readonly copies?: true;
    /** Whether the Consumer answers Replace All, asked of .NET (ADR-0332). */
    readonly replaces?: true;
    /** Whether the component's `OnSummarize` answers the Selection Summary, asked of .NET (ADR-0130). */
    readonly summarizes?: true;
}

/** The functions of Formula entry the browser asks as it decides a key, each the Consumer's script (ADR-0431). */
export interface WireScripts {
    readonly pointAt?: WireScript;
    readonly referenceText?: WireScript;
    readonly cycleReference?: WireScript;
    readonly referencesIn?: WireScript;
    readonly rowHeadings?: WireScript;
    readonly nameBoxLabel?: WireScript;
    readonly findMatch?: WireScript;
    readonly dataEdge?: WireScript;
}

/** A Pointing Scope's declaration for this grid, as .NET's `GridPointedInBrowser` names it (ADR-0430). */
export interface WirePointedAt {
    readonly scope: string;
    readonly declaration: string;
    readonly table: string;
    readonly tableColumns: Readonly<Record<string, string>>;
}

/** The options .NET sets as they are, with no function or object of the browser's in them. */
type PassedOption =
    | 'density' | 'rowHeight' | 'headerHeight' | 'viewportHeight' | 'viewportWidth' | 'pinnedColumnCount' | 'virtualiseColumns' | 'stripeRows'
    | 'highlightFocusRow' | 'highlightHoverRow' | 'hideColumnMenu' | 'hideCopyWithHeaders' | 'headerClickSelects' | 'pasteMaySpill' | 'findsHeaders'
    | 'showSelectionSummary' | 'copyCellCap' | 'pasteByteCap' | 'headerGroups' | 'showFillHandle' | 'showFormulaBar' | 'outlinedColumns'
    | 'pageSize' | 'rangeRequestMargin' | 'rowHeadingWidth' | 'hideHeader' | 'summaryFigures' | 'declaredKeys' | 'changeHighlightDuration';

const passedOptions: readonly PassedOption[] = Object.freeze([
    'density', 'rowHeight', 'headerHeight', 'viewportHeight', 'viewportWidth', 'pinnedColumnCount', 'virtualiseColumns', 'stripeRows',
    'highlightFocusRow', 'highlightHoverRow', 'hideColumnMenu', 'hideCopyWithHeaders', 'headerClickSelects', 'pasteMaySpill', 'findsHeaders',
    'showSelectionSummary', 'copyCellCap', 'pasteByteCap', 'headerGroups', 'showFillHandle', 'showFormulaBar', 'outlinedColumns',
    'pageSize', 'rangeRequestMargin', 'rowHeadingWidth', 'hideHeader', 'summaryFigures', 'declaredKeys', 'changeHighlightDuration',
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
    /** .NET rendered the contents of the Razor slot `id`, for the data it was sent numbered `version`, in `contents`, in
     * its holder beside the root (ADR-0410). */
    slotRendered(id: number, version: number, contents: HTMLElement | null): void;
    /** The call held as `held`, too large for a message of its own, as the stream .NET reads it from. Taken once. */
    take(held: number): Promise<Uint8Array<ArrayBuffer>>;
    /** .NET found the numbered inputs `first` to `last` never arrived, sent while the Server circuit was down (ADR-0240
     * section 11): what waits on them ends, and the user is told in `words`. */
    inputsLost(first: number, last: number, words: string): void;
    /** The view as the grid holds it now (ADR-0331, ADR-0441 section 1), what .NET sent before taken in first: a
     * {@link WireView}, kept under its number for .NET to hand back, or held where too large for a message. `released`
     * are the numbers of the views .NET no longer holds. */
    readView(released: readonly number[]): WireView | WireHeld;
    /** Shows a view .NET hands back, or rows never shown, when the grid takes the version it names: a {@link WireShownView}. */
    showView(json: string): void;
    /** The open edit's text, or null, what .NET sent before taken in first (ADR-0441 section 2). */
    readEditText(): string | null | WireHeld;
    /** Places .NET's work `work` in the Input Order, inside .NET's work `within` while that runs (ADR-0441 section 3):
     * in its turn .NET is asked to run it. Settles once it has; fails where it failed. */
    inOrder(work: number, within: number | null): Promise<boolean>;
    /** As `inOrder`, and an Escape cancels it (ADR-0240 section 12): true once it landed, false where it was cancelled
     * or never ran. */
    cancellableInOrder(work: number, within: number | null): Promise<boolean>;
    /** The Selection once every input received before the call is answered (ADR-0441 section 3), or null where the
     * grid went first. */
    selectionAfterInputs(within: number | null): Promise<unknown>;
    /** The Selection now, what .NET sent before taken in first: for a turn that holds the inputs after it, an event's
     * handler or cancellable work (ADR-0441 section 3). */
    readSelection(): unknown;
    /** Asks the Selection Summary again (ADR-0130). */
    refreshSummary(): void;
    /** Writes text into the open edit as Point writes a Reference (ADR-0058). */
    writePointedText(text: string): boolean;
    /** Takes back what `writePointedText` wrote last (ADR-0058). */
    takeBackPointedText(): boolean;
    /** Stops the grid and empties its element. */
    dispose(): void;
}

/** A view the grid read, as .NET reads it (ADR-0441 section 1): the number the bridge keeps it under, and what it
 * holds. */
export interface WireView {
    readonly number: number;
    readonly rowSequenceVersion: number;
    readonly selection: unknown;
    readonly scrollTopPx: number;
    readonly scrollLeftPx: number;
}

/** A view .NET hands back: one the bridge keeps, by its number, or none for rows never shown, under the version that
 * brings the rows, with a place to select in place of its Selection. */
export interface WireShownView {
    readonly number: number | null;
    readonly rowSequenceVersion: number;
    readonly place: ViewPlace | null;
}

/** What `attach` is handed once: the parameters, what the source is, and its state then. */
export interface WireAttach {
    readonly options: WireOptions;
    readonly source: WireSourceTraits;
    readonly state: WireState;
    /** Whether .NET is across a circuit, Blazor Server's, whose hub limits the size of a message: only then is a
     * call larger than `inlineBytes` held for .NET to take as a stream (ADR-0005, as amended 2026-10-20). */
    readonly circuit: boolean;
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

// The Consumer's functions, loaded once for the page, before the grids that name them are made (ADR-0431).
const pageModules = new ScriptModules();
const pageFunctions = new Map<string, LoadedScriptFunction>();

/**
 * Loads the Consumer's functions `json` names, a list of {@link WireScript}, before .NET attaches a grid that names
 * them, so no key is decided without them (ADR-0431). Answers null once each is loaded, or the refusal, which .NET says
 * in the grid's place: a module that does not load, or an export that is not a function.
 */
export async function loadScripts(json: string): Promise<string | null> {
    const scripts = parsed<readonly WireScript[]>(json);
    try {
        const functionOf = await pageModules.load(scripts);
        for (const script of scripts) pageFunctions.set(keyOf(script), functionOf(script));
        return null;
    } catch (e: unknown) { // oxlint-disable-line ex-grid/no-catch -- the refusal of the Consumer's script is answered as its words, which .NET shows in the grid's place; anything else is thrown on
        if (e instanceof ScriptFunctionError) return e.message;
        throw e;
    }
}

/** A function `loadScripts` loaded for the page, which .NET names only once it has: never one it did not load. */
function pageFunctionOf(script: WireScript): LoadedScriptFunction {
    const loaded = pageFunctions.get(keyOf(script));
    if (loaded === undefined) throw new Error(`Internal: .NET named ${scriptFunctionName(script)} without loading it first.`);
    return loaded;
}

/** The calls that can carry more than a message holds, and that .NET's `Held` makes: `ScriptBridge.Held`. */
const heldGridCalls: ReadonlySet<string> = new Set([
    'Call', 'Heard', 'Complete', 'Release', 'MountSlot', 'UpdateSlot', 'Summarize',
]);

/** The calls that carry an input of the user's that .NET carries out or answers, numbered as they cross (ADR-0240
 * section 11). A write crosses as a numbered `Heard`, and a Validate on its own, since each tells the user itself. */
const numberedGridCalls: ReadonlySet<string> = new Set(['Find', 'FindError', 'ContextCommands', 'RunContextCommand', 'Copy', 'SetSorts', 'SetFilter']);

/** What a call lost on the way to .NET is to a step that would otherwise wait on it: given up, as the core reads an
 * `AbortError`, and said by {@link InputNumbers}' owner instead (ADR-0240 section 11). */
function givenUp(answer: unknown): void {
    if (answer instanceof LostCall) throw new DOMException('The call never reached .NET: the connection was lost.', 'AbortError');
}

/**
 * Mounts the Script Core on `root`, the element the Blazor component rendered, with `dotnet` the component's
 * `DotNetObjectReference` and `json` a {@link WireAttach}. What it answers is the component's handle on the grid.
 */
export function attach(
    root: HTMLElement, bridge: DotNetHandle, json: string, makeCollector = browserCollector, functionOf: (script: WireScript) => LoadedScriptFunction = pageFunctionOf,
): BlazorGrid {
    const init = parsed<WireAttach>(json);
    // A call too large for a message of its own, a large paste, a long edit, a filter of many values or a filter
    // panel's list, is held, and .NET takes it in its turn: the hub would otherwise close the circuit.
    const holding = new HeldMessages(init.circuit);
    const dotnet = crossingCalls(bridge, holding, heldGridCalls);
    // An input made while the Server circuit is down never reaches .NET, and Blazor never ends its call: numbered, its
    // loss is found by the next number .NET receives, and the user is told (ADR-0240 section 11).
    const numbers = new InputNumbers(dotnet);
    const numbered = numbers.calls(numberedGridCalls, dotnet);
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
        const answer = await numbers.call('Validate', [number, columns.version, row.id, column, text], true);
        // Cancelled meanwhile: .NET gave no verdict, and the grid reads none.
        signal.throwIfAborted();
        // Never asked: the commit is refused in the editor, which keeps the text, in the words of a lost input.
        if (answer instanceof LostCall) return { kind: 'reject', message: answer.words };
        return verdictOf(answer, column);
    });
    // A slot the grid mounts as it is made holds its Settled Mark once there is a grid to hold, before the task ends.
    const made: { grid?: GridHandle<BlazorRow> } = {};
    const slots = new RazorSlots(dotnet, slotHostOptionsOf(() => made.grid, 'grid'));
    const columns = new ColumnSet(verdict, (slot, cell) => slots.template(slot, cell));
    columns.take(init.options.columns, init.options.columnsVersion);
    const rows = new RowSet(makeCollector, (ids, seen) => { void dotnet.invokeMethodAsync('Release', ids, seen); });
    // A Selection Summary is asked of the source numbered `generation`, or of the component's OnSummarize (ADR-0130).
    const summarize: SummarizeOf = (generation, request, signal) => asking(signal, async number => {
        const answer = await dotnet.invokeMethodAsync('Summarize', number, generation === undefined ? null : generation, request);
        signal.throwIfAborted();
        return summaryResultOf(answer);
    });
    const sourceOf = (traits: WireSourceTraits, state: WireState): DotNetSource => {
        const taken = new DotNetSource(numbered, rows, columns, traits, (generation, request, signal) => askedFind('Find', generation, request, signal), summarize);
        taken.take(state);
        return taken;
    };
    let source = sourceOf(init.source, init.state);
    // The parameters as .NET last sent them, and the Scope's declaration once the page's Scope stands (ADR-0430).
    let wired = init.options;
    let pointedAt: GridPointedAt<BlazorRow> | undefined;
    const scripts = new ScriptHooks(functionOf, columns);
    const optionsNow = (): GridOptions<BlazorRow> => {
        const options = gridOptions(wired, columns, source, heard, paint, findError, completeEditorText, slots, menus, metrics, scripts, asked);
        return pointedAt === undefined ? options : { ...options, pointedAt };
    };

    const update = (next: WireUpdate): void => {
        // The columns first, so the rows that come with them are read by them.
        columns.take(next.options.columns, next.options.columnsVersion);
        if (next.source !== undefined) {
            if (next.state === undefined) throw new TypeError('Internal: .NET sent another source without its state.');
            source = sourceOf(next.source, next.state);
        } else if (next.state !== undefined) {
            source.take(next.state);
        }
        wired = next.options;
        grid.update(optionsNow());
        marks.changed();
    };

    const gate = new WriteGate<WireUpdate, WireState>(message => {
        if ('update' in message) {
            update(message.update);
        } else {
            source.take(message.state);
            marks.changed();
        }
    });
    const write: Write = (kind, payload, speaks = false) => gate.handling(numbers.call('Heard', [kind, payload], speaks));
    const heard = handlers(dotnet, numbers, write, asking);
    const paint = cellPaint(dotnet, columns);
    // Asked of the source the grid holds now, by its number. .NET sends what its source owes before it answers, and
    // answers a step asked of a source or an order it no longer holds as moved: once what a write of the user's holds
    // meanwhile is taken in, the core holds the newer one, and says the rows moved (ADR-0381, ADR-0142).
    // A Find step and an error step alike (ADR-0055, ADR-0381).
    const askedFind = (method: 'Find' | 'FindError', generation: number, request: { readonly rowSequenceVersion: number }, signal: AbortSignal): Promise<GridFindResult> =>
        asking(signal, async number => {
            const askedOf = source;
            const answer = await numbered.invokeMethodAsync(method, number, generation, request);
            signal.throwIfAborted();
            givenUp(answer);
            const found = findResultOf(answer);
            if (found !== 'moved') return found;
            await gate.released();
            // A "none found" the core would read as said of the rows it shows: .NET moved without sending what moved.
            if (source === askedOf && source.snapshot().rowSequenceVersion === request.rowSequenceVersion) {
                throw new Error(`Internal: .NET answered the ${method === 'Find' ? 'Find' : 'error'} step as moved, and sent no other source or order.`);
            }
            return { found: false };
        });
    const findError: FindErrorOf = (request, signal) => askedFind('FindError', source.generation, request, signal);
    // Asked of .NET for the text and caret the core hands; the core drops an answer for text it no longer holds.
    const completeEditorText = latestOnly(async (text, caret) => completionOf(await dotnet.invokeMethodAsync('Complete', text, caret)));
    const menus = contextMenus(numbered, () => ({ generation: source.generation, columnsVersion: columns.version }));
    const metrics = new WireMetrics();
    const marks = new WireMarks(() => source, write);
    const asked = askedThrough(numbered, asking, marks, summarize, write);
    // A key or press made while a numbered call is unanswered may wait on one lost, which nothing numbered follows
    // while it waits: .NET is told how far the numbers have gone, and finds any that never arrived (ADR-0240 section 11).
    // Heard on the grid's Input Root, as the core finds it, in the capture phase and before the grid's own listeners
    // there, which may hold the input and stop it; only an input made on this grid counts.
    const inputRoot = gridInputRoot(root);
    const tellSent = (event: Event): void => {
        if (!(event.target instanceof Node) || !root.contains(event.target)) return;
        numbers.tell();
    };
    const hearSent = (hear: boolean): void => {
        for (const type of ['keydown', 'mousedown']) {
            if (hear) inputRoot.addEventListener(type, tellSent, true);
            else inputRoot.removeEventListener(type, tellSent, true);
        }
    };
    hearSent(true);
    let grid: GridHandle<BlazorRow>;
    try {
        grid = createGrid(root, optionsNow());
    } catch (error) {
        hearSent(false);
        throw error;
    }
    made.grid = grid;
    // Once .NET has let go of the bridge, nothing is told to it.
    let stopped = false;
    // The views .NET read, by the number each crossed under: the core knows a view by the object it handed out
    // (ADR-0441 section 1).
    const views = new Map<number, GridViewRead>();
    let viewsRead = 0;
    // The turns of .NET's work while it runs, by its number: work .NET places from inside it runs there.
    const turns = new Map<number, GridTurn>();
    const placeOf = (within: number | null): Pick<GridHandle<BlazorRow>, 'inOrder' | 'cancellableInOrder'> => {
        if (within === null) return grid;
        const turn = turns.get(within);
        if (turn === undefined) throw new Error(`Internal: .NET placed work inside its work ${String(within)}, which holds no turn.`);
        return turn;
    };
    // .NET's work in its turn: .NET runs it and says how it ended; a failure's own exception stays in .NET, which throws
    // it from its call.
    // A cancel .NET saw after an Escape aborted `signal` is the signal's, as the core reads one; one it saw otherwise,
    // the grid going, ends the work as cancelled there.
    const runWork = async (work: number, signal?: AbortSignal): Promise<void> => {
        const ended = await dotnet.invokeMethodAsync('RunWork', work);
        if (ended === 'landed') return;
        if (ended === 'failed') throw new Error(`.NET's work ${String(work)} failed.`);
        if (ended !== 'cancelled') throw new TypeError(`Internal: .NET ended its work as ${JSON.stringify(ended)}.`);
        signal?.throwIfAborted();
        throw new Error(`.NET's work ${String(work)} was cancelled there.`);
    };
    // An Escape names cancellable work while the core holds what says it too late: once the core lets that go, no
    // Escape can, and .NET lets the work go.
    const workForgotten = new FinalizationRegistry<number>(work => { if (!stopped) void dotnet.invokeMethodAsync('ForgetWork', work); });
    const answer = <T>(value: T): T | WireHeld => holding.crossing(value) ?? value;
    // The grid joins its Scope once a Sheet of it brings it to the page, and is pointed at from then on (ADR-0430).
    const declared = init.options.pointedAt;
    const leaveScope = declared === undefined ? undefined : awaitPointingScope(declared.scope, scope => {
        pointedAt = scope.registerGrid(declared.declaration, declared.table, declared.tableColumns, tableRowOf);
        grid.update(optionsNow());
    });
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
        slotRendered: (id: number, version: number, contents: HTMLElement | null) => { slots.rendered(id, version, contents); },
        take: (number: number) => holding.take(number),
        readView: (released: readonly number[]) => {
            for (const number of released) views.delete(number);
            // .NET sent the source's state before it asked: the view is read with it.
            gate.flush();
            const view = grid.readView();
            viewsRead += 1;
            views.set(viewsRead, view);
            return answer({
                number: viewsRead, rowSequenceVersion: view.rowSequenceVersion, selection: wireSelection(view.selection),
                scrollTopPx: view.scrollTopPx, scrollLeftPx: view.scrollLeftPx,
            });
        },
        showView: (text: string) => {
            const shown = parsed<WireShownView>(text);
            const place = shown.place === null ? undefined : shown.place;
            if (shown.number === null) {
                grid.showNewView(shown.rowSequenceVersion, place);
                return;
            }
            const view = views.get(shown.number);
            if (view === undefined) throw new Error(`Internal: .NET handed back the view ${String(shown.number)}, which it had let go.`);
            grid.showViewWith(view, shown.rowSequenceVersion, place);
        },
        readEditText: () => {
            gate.flush();
            const text = grid.readEditText();
            // No edit open crosses as JSON's null, which .NET reads as none.
            return answer(text === undefined ? null : text);
        },
        inOrder: async (work: number, within: number | null) => {
            await placeOf(within).inOrder(async turn => {
                turns.set(work, turn);
                try {
                    await runWork(work);
                } finally {
                    turns.delete(work);
                }
            });
            return true;
        },
        cancellableInOrder: (work: number, within: number | null) => {
            const tooLate = (landed: boolean): void => { if (!stopped) void dotnet.invokeMethodAsync('WorkCancelledTooLate', work, landed); };
            const cancel = (): void => { if (!stopped) void dotnet.invokeMethodAsync('CancelWork', work); };
            workForgotten.register(tooLate, work);
            return placeOf(within).cancellableInOrder(async signal => {
                signal.addEventListener('abort', cancel, { once: true });
                try {
                    await runWork(work, signal);
                } finally {
                    signal.removeEventListener('abort', cancel);
                }
            }, tooLate);
        },
        selectionAfterInputs: async (within: number | null) => {
            let read: GridSelection | undefined;
            await placeOf(within).inOrder(() => { read = grid.readView().selection; });
            // The grid went before the turn came: nothing was read.
            return answer(read === undefined ? null : wireSelection(read));
        },
        readSelection: () => {
            gate.flush();
            return answer(wireSelection(grid.readView().selection));
        },
        refreshSummary: () => {
            gate.flush();
            grid.refreshSummary();
        },
        writePointedText: (text: string) => {
            gate.flush();
            return grid.writePointedText(text);
        },
        takeBackPointedText: () => {
            gate.flush();
            return grid.takeBackPointedText();
        },
        inputsLost: (first: number, last: number, words: string) => {
            if (!Number.isInteger(first) || !Number.isInteger(last) || first > last || typeof words !== 'string') {
                throw new TypeError(`Internal: .NET named the inputs ${String(first)} to ${String(last)} lost, in ${JSON.stringify(words)}.`);
            }
            const lost = Array.from({ length: last - first + 1 }, (_, index) => first + index);
            // Said once for them all, unless each lost call tells the user itself; an empty sentence says nothing.
            if (numbers.lose(lost, words) && words !== '') grid.announce(words);
        },
        dispose: () => {
            stopped = true;
            hearSent(false);
            // Nothing is let go once .NET has let go of the bridge, and nothing held is taken in.
            gate.stop();
            rows.stop();
            slots.dispose();
            holding.clear();
            leaveScope?.();
            grid.destroy();
        },
    });
}

// A row of a grid of a Pointing Scope as the table's row: .NET sends every row of such a grid with it (ADR-0430).
function tableRowOf(row: BlazorRow): readonly unknown[] {
    if (row.table === undefined) throw new Error(`Internal: .NET sent row ${String(row.id)} of a grid in a Pointing Scope without its table's row.`);
    return row.table;
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
    | 'onOverwriteNotice' | 'onAction' | 'onNameBoxEntered' | 'onReferenceKeyColoursChanged' | 'onParentWithoutHeight'>>;

type Asking = <T>(signal: AbortSignal, ask: (number: number) => Promise<T>) => Promise<T>;

/** A write of the user's own, numbered as it crosses (ADR-0240 section 11): `speaks` where its own refusal tells the user
 * that it was lost, as an edit's does in its editor. */
type Write = (kind: HeardKind, payload: unknown, speaks?: boolean) => Promise<unknown>;

// One function per event, made once, so the core sees the same handler on every update (ADR-0003). A write of the
// user's own goes by `write`, which holds what .NET sends until .NET has handled it. An edit, a paste and a fill are
// answered by .NET, which may refuse them: each is pending until it has (ADR-0380 sections 2 and 5).
// What the browser has already done, a Selection or a column's width, is only heard by .NET, and crosses as it is; what
// .NET carries out, an Action, an undo or a Name Box entry, is numbered (ADR-0240 section 11).
function handlers(dotnet: DotNetHandle, numbers: InputNumbers, write: Write, asking: Asking): Handlers {
    const send = (kind: HeardKind, payload: unknown): void => { void dotnet.invokeMethodAsync('Heard', kind, payload); };
    const carry = (kind: HeardKind, payload: unknown): void => { void numbers.call('Heard', [kind, payload]); };
    return Object.freeze({
        onSelectionChanged: (selection: GridSelection) => { send('selectionChanged', wireSelection(selection)); },
        onEdit: intent => asking(intent.signal, async answer => {
            const answered = await write('edit', {
                answer, row: intent.row.id, column: intent.column, value: intent.value, seenText: intent.seenText, replacedText: intent.replacedText,
            }, true);
            intent.signal.throwIfAborted();
            // Never reached .NET: refused in the editor, which keeps the text, never sent again.
            if (answered instanceof LostCall) {
                intent.refuse(answered.words);
                return;
            }
            const refusal = refusalOf(answered);
            if (refusal !== undefined) intent.refuse(refusal);
        }),
        onPaste: intent => asking(intent.signal, async answer => {
            const answered = await write('paste', {
                answer, targets: intent.plan.targets, source: intent.plan.source, collapsesSelection: intent.plan.collapsesSelection, values: intent.values,
                origins: intent.origins, rowSequenceVersion: intent.rowSequenceVersion, enteredAt: intent.enteredAt, fillSource: intent.fillSource,
            });
            intent.signal.throwIfAborted();
            if (answered instanceof LostCall) {
                intent.refuse();
                return;
            }
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
            if (answered instanceof LostCall || refusalOf(answered) !== undefined) intent.refuse();
        }),
        onClear: intent => { void write('clear', { targets: intent.targets, rowSequenceVersion: intent.rowSequenceVersion }); },
        onUndo: () => { carry('undo', null); },
        onRedo: () => { carry('redo', null); },
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
        onParentWithoutHeight: () => { send('parentWithoutHeight', null); },
        onOverwriteNotice: notice => { send('overwriteNotice', notice); },
        onAction: event => { carry('action', { row: event.row.id, column: event.column, action: event.action }); },
        onNameBoxEntered: typed => { carry('nameBoxEntered', typed); },
        onReferenceKeyColoursChanged: keys => { send('referenceKeyColoursChanged', keys); },
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

type CompleteEditorTextOf = (text: string, caret: number) => Promise<EditorCompletion | undefined>;

/**
 * `ask`, one question at a time: a question asked while another is on its way waits for it, and one asked after it
 * takes its place, answering it nothing. Typing at machine speed then crosses to .NET as the question on its way and
 * the latest, never one per key ahead of the edit, each awaiting the Consumer in its turn. The core asks again at each
 * key and drops an answer to any question but its last, so the list it shows is the one for the text it holds,
 * whichever question crossed.
 */
export function latestOnly(ask: CompleteEditorTextOf): CompleteEditorTextOf {
    let asking = false;
    let waiting: { readonly text: string; readonly caret: number; readonly answer: (to: Promise<EditorCompletion | undefined> | undefined) => void } | undefined;
    const crossing = async (text: string, caret: number): Promise<EditorCompletion | undefined> => {
        asking = true;
        try {
            return await ask(text, caret);
        } finally {
            asking = false;
            const next = waiting;
            waiting = undefined;
            if (next !== undefined) next.answer(crossing(next.text, next.caret));
        }
    };
    return (text, caret) => {
        if (!asking) return crossing(text, caret);
        // Replaced: the core asked again since, and drops any answer to this one.
        waiting?.answer(undefined);
        return new Promise(answer => { waiting = { text, caret, answer }; });
    };
}

/** .NET's completion, as `ScriptJson.Completion` writes it, or nothing where it answered none. */
function completionOf(answer: unknown): EditorCompletion | undefined {
    if (answer === null) return undefined;
    if (typeof answer !== 'string') throw new TypeError('Internal: .NET answered a completion with something other than JSON text.');
    const { candidates, hint } = parsed<{
        readonly candidates: readonly CompletionCandidate[];
        readonly hint?: { readonly text: string; readonly emphasisStart: number; readonly emphasisLength: number };
    }>(answer);
    return new EditorCompletion(hint === undefined ? { candidates } : { candidates, hint: new EditorHint(hint) });
}

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

/** The Consumer's Context Menu commands, and their words, as the core takes them (ADR-0421). */
interface ContextMenus {
    readonly commands: (context: GridContextMenuContext<BlazorRow>) => Promise<readonly GridCommand[]>;
    /** The words of an id: the Consumer's command's as .NET answered the menu, else the grid's own as `table` gives
     * them. The same function while the table holds the same words, so the core words nothing again for nothing. */
    labelFor(table: Readonly<Record<string, string>> | undefined): (id: string) => string | undefined;
}

// The Consumer's commands are .NET's, asked as the menu opens, which stands once they come (ADR-0421). Each menu is
// numbered, and a command runs as the number of the menu it stood in and its place there: .NET keeps the commands of
// the last menu asked, the only one standing. A command runs in .NET, and the core holds the input after it until
// .NET has run it. A menu asked under columns or a source .NET no longer holds, or of a component gone, is given up,
// and no menu stands. Made once, so the core sees the same functions on every update (ADR-0003).
function contextMenus(dotnet: DotNetCalls, held: () => { readonly generation: number; readonly columnsVersion: number }): ContextMenus {
    let asked = 0;
    let words: ReadonlyMap<string, string> = new Map();
    let labels: { readonly text: string; readonly label: (id: string) => string | undefined } | undefined;
    return Object.freeze({
        commands: async (context: GridContextMenuContext<BlazorRow>): Promise<readonly GridCommand[]> => {
            asked += 1;
            const number = asked;
            const { generation, columnsVersion } = held();
            const answer = await dotnet.invokeMethodAsync('ContextCommands', number, generation, columnsVersion, context.row.id, context.column,
                context.selection, context.rowSequenceVersion);
            givenUp(answer);
            const commands = commandsOf(answer);
            words = new Map(commands.flatMap(command => (command.label === undefined ? [] : [[command.id, command.label] as const])));
            return Object.freeze(commands.map(({ id, enabled }, index) => Object.freeze({
                id, enabled, invoke: async () => { givenUp(await dotnet.invokeMethodAsync('RunContextCommand', number, index)); },
            })));
        },
        labelFor: (table: Readonly<Record<string, string>> | undefined) => {
            const text = table === undefined ? '' : JSON.stringify(table);
            if (labels?.text !== text) {
                const own = new Map(table === undefined ? [] : Object.entries(table));
                labels = { text, label: (id: string) => (words.has(id) ? words.get(id) : own.get(id)) };
            }
            return labels.label;
        },
    });
}

/** .NET's answer to a Context Menu's opening, as `ExGrid.Script` writes it: its commands, or `moved` where what the
 * menu was opened on is no longer what .NET holds. One .NET did not answer, its component gone, is given up. */
function commandsOf(answer: unknown): readonly { readonly id: string; readonly enabled: boolean; readonly label: string | undefined }[] {
    if (answer === null) throw new DOMException('.NET gave the Context Menu no commands: the component is gone.', 'AbortError');
    if (typeof answer !== 'string') throw new TypeError('Internal: .NET answered the Context Menu\'s commands with something other than JSON text.');
    const read = parsed<unknown>(answer);
    if (typeof read === 'object' && read !== null && Reflect.get(read, 'moved') === true) {
        throw new DOMException('The Context Menu was opened on columns or rows .NET no longer holds.', 'AbortError');
    }
    if (!Array.isArray(read)) throw new TypeError('Internal: .NET answered the Context Menu\'s commands with something other than a list.');
    return (read as readonly unknown[]).map(command => {
        const { id, enabled, label } = command as { readonly id?: unknown; readonly enabled?: unknown; readonly label?: unknown }; // oxlint-disable-line typescript/no-unsafe-type-assertion -- each member is checked below
        if (typeof id !== 'string' || typeof enabled !== 'boolean' || (label !== undefined && typeof label !== 'string')) {
            throw new TypeError(`Internal: .NET answered a Context Menu's command as ${JSON.stringify(command)}.`);
        }
        return { id, enabled, label };
    });
}

type MessageOf = NonNullable<GridOptions<BlazorRow>['cellMessage']>;

interface Paint {
    readonly cellState: NonNullable<GridOptions<BlazorRow>['cellState']>;
    readonly rowKind: (row: BlazorRow) => RowKind;
    readonly cellAppearance: (row: BlazorRow, column: GridColumn<BlazorRow>) => CellAppearance;
    readonly cellChangedAt: (row: BlazorRow, column: string) => number | undefined;
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
        // Each comes with its row, as a state does: a row whose kind, appearance or change times change is another object.
        rowKind: (row: BlazorRow): RowKind => row.kind ?? 'detail', // oxlint-disable-line ex-grid/no-stand-in-default -- a Detail row crosses with no kind (RowPaint.WriteRow)
        cellAppearance: (row: BlazorRow, column: GridColumn<BlazorRow>): CellAppearance => {
            if (row.appearances === undefined) return noAppearance;
            const appearance = row.appearances[columns.indexOf(column)];
            if (appearance === undefined) throw new Error(`Internal: row ${row.id} came with no appearance for column '${column.name}'.`);
            return appearance ?? noAppearance; // null on the wire is the column's own paint (RowPaint.WriteRow)
        },
        cellChangedAt: (row: BlazorRow, column: string): number | undefined => {
            if (row.changedAt === undefined) return undefined;
            const at = row.changedAt[columns.indexNamed(column)];
            if (at === undefined) throw new Error(`Internal: row ${row.id} came with no change time for column '${column}'.`);
            return at ?? undefined; // oxlint-disable-line ex-grid/no-stand-in-default -- null on the wire is a cell never changed (RowPaint.WriteRow)
        },
    });
}

// The reference rule over the text .NET formats (ADR-0332 §4), for a source in .NET that finds by it, so a mark agrees
// with the step. One function for every grid: its identity never asks the painted rows to judge again (ADR-0003).
const referenceMark = (row: BlazorRow, column: GridColumn<BlazorRow>, pattern: GridFindPattern): boolean => findMatches(textOf(column, row), pattern);

/** No Font, Fill or Border of the Consumer's: the column's own paint. */
const noAppearance: CellAppearance = Object.freeze({});

function gridOptions(
    wire: WireOptions, columns: ColumnSet, source: DotNetSource, heard: Handlers, paint: Paint, findError: FindErrorOf,
    completeEditorText: CompleteEditorTextOf, slots: RazorSlots, menus: ContextMenus, metrics: WireMetrics, scripts: ScriptHooks, asked: Asked,
): GridOptions<BlazorRow> {
    const cellMetrics = metrics.cellMetrics(wire.cellMetrics);
    const presentationDefaults = metrics.presentationDefaults(wire.presentationDefaults);
    const hears = new Set(wire.hears);
    const razorSlots = wire.razorSlots;
    for (const name of razorSlots === undefined ? [] : razorSlots) {
        if (!isGridRazorSlotName(name)) throw new TypeError(`Internal: .NET named '${String(name)}' as a slot its Chrome fills.`);
    }
    const temporal: unknown = Reflect.get(globalThis, 'Temporal');
    const passed = Object.fromEntries(passedOptions.filter(name => wire[name] !== undefined).map(name => [name, wire[name]]));
    return {
        ...(passed as Partial<Pick<GridOptions<BlazorRow>, PassedOption>>),
        columns: columns.columns,
        source,
        locale: wire.locale,
        ...(cellMetrics === undefined ? {} : { cellMetrics }),
        ...(presentationDefaults === undefined ? {} : { presentationDefaults }),
        // A C# Consumer cannot hand the grid a JavaScript namespace, so Blazor's ExGrid hands it the browser's. Every
        // browser the family serves ships one (ADR-0017); where one does not, the core refuses a filterable Date column
        // by name, as it refuses one given none.
        ...(temporal === undefined ? {} : { temporal: temporal as TemporalLike }), // oxlint-disable-line typescript/no-unsafe-type-assertion -- the core checks it is a Temporal namespace as it takes it
        ...(wire.cellStates === true ? { cellState: paint.cellState } : {}),
        ...(wire.rowKinds === true ? { rowKind: paint.rowKind } : {}),
        ...(wire.appearances === true ? { cellAppearance: paint.cellAppearance } : {}),
        ...(wire.changeTimes === true ? { cellChangedAt: paint.cellChangedAt } : {}),
        ...(wire.marks === true ? { marks: asked.marks } : {}),
        ...(wire.copies === true ? { copyAnswerAsync: asked.copyAnswerAsync } : {}),
        ...(wire.replaces === true ? { onReplace: asked.onReplace } : {}),
        ...(wire.summarizes === true ? { onSummarize: asked.onSummarize } : {}),
        ...(hears.has('declaredKey') ? { onDeclaredKey: asked.onDeclaredKey } : {}),
        ...(hears.has('selectionSummaryChanged') ? { onSelectionSummaryChanged: asked.onSelectionSummaryChanged } : {}),
        ...(hears.has('summaryFiguresChanged') ? { onSummaryFiguresChanged: asked.onSummaryFiguresChanged } : {}),
        ...(wire.cellMessages === undefined ? {} : { cellMessage: paint.cellMessage(wire.cellMessages) }),
        ...(wire.findsErrors === true ? { findError } : {}),
        ...(wire.completesText === true ? { completeEditorText } : {}),
        ...(wire.referenceMarks === true ? { findMatch: referenceMark } : {}),
        ...scripts.options(wire.scripts),
        ...(wire.contextCommands === true ? { contextCommands: menus.commands } : {}),
        ...(wire.contextCommands === true || wire.commandLabels !== undefined ? { commandLabel: menus.labelFor(wire.commandLabels) } : {}),
        ...((razorSlots === undefined || razorSlots.length === 0) && wire.stylesheets === undefined
            ? {}
            : { chrome: slots.chrome(razorSlots === undefined ? [] : razorSlots, wire.stylesheets) }),
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
        ...(hears.has('parentWithoutHeight') ? { onParentWithoutHeight: heard.onParentWithoutHeight } : {}),
        ...(hears.has('overwriteNotice') ? { onOverwriteNotice: heard.onOverwriteNotice } : {}),
        ...(hears.has('action') ? { onAction: heard.onAction } : {}),
        ...(hears.has('nameBoxEntered') ? { onNameBoxEntered: heard.onNameBoxEntered } : {}),
        ...(hears.has('referenceKeyColoursChanged') ? { onReferenceKeyColoursChanged: heard.onReferenceKeyColoursChanged } : {}),
    };
}

type ScriptOptions = Pick<GridOptions<BlazorRow>, 'pointAt' | 'referenceText' | 'cycleReference' | 'referencesIn' | 'rowHeadings' | 'nameBoxLabel' | 'findMatch' | 'dataEdge'>;

/**
 * The Consumer's functions of Formula entry as the core takes them (ADR-0431), each made once per function, so the
 * core sees the same one on every update (ADR-0003). Each calls the Consumer's function where the core asks it, and a
 * throw, or an answer its option does not take, is thrown on as an error naming the function, never answered another
 * way. The core checks a rewrite and the References themselves, as it checks a React Consumer's.
 */
class ScriptHooks {
    readonly #made = new Map<string, unknown>();
    readonly #functionOf: (script: WireScript) => LoadedScriptFunction;
    readonly #columns: ColumnSet;

    constructor(functionOf: (script: WireScript) => LoadedScriptFunction, columns: ColumnSet) {
        this.#functionOf = functionOf;
        this.#columns = columns;
    }

    readonly #values = new WeakMap<BlazorRow, Readonly<Record<string, unknown>>>();

    // A row's values by its columns' names, as the Consumer's script reads a row: made once a row, as the core asks of
    // each painted cell.
    #valuesOf(row: BlazorRow): Readonly<Record<string, unknown>> {
        const made = this.#values.get(row);
        if (made !== undefined) return made;
        const values = Object.freeze(Object.fromEntries(this.#columns.columns.map((column, index) => [column.name, row.cells[index]])));
        this.#values.set(row, values);
        return values;
    }

    options(wire: WireScripts | undefined): Partial<ScriptOptions> {
        if (wire === undefined) return {};
        const { pointAt, referenceText, cycleReference, referencesIn, rowHeadings, nameBoxLabel, findMatch, dataEdge } = wire;
        return {
            // A position's label, asked as each row is painted, a row not yet arrived included (ADR-0050 item 1).
            ...(rowHeadings === undefined ? {} : { rowHeadings: this.#hook('rowHeadings', rowHeadings, f => (row: number) => checkedAnswer(rowHeadings,
                'rowHeadings', f(row), (a): a is string => typeof a === 'string', 'text')) }),
            ...(nameBoxLabel === undefined ? {} : { nameBoxLabel: this.#hook('nameBoxLabel', nameBoxLabel, f => (cell: CellPosition) => checkedAnswer(nameBoxLabel,
                'nameBoxLabel', f(cell), (a): a is string => typeof a === 'string', 'text')) }),
            // A row is handed to the Consumer's script as its values by column name: the browser holds no row of .NET's.
            ...(findMatch === undefined ? {} : { findMatch: this.#hook('findMatch', findMatch, f => (row: BlazorRow, column: GridColumn<BlazorRow>, pattern: GridFindPattern) =>
                checkedAnswer(findMatch, 'findMatch', f(this.#valuesOf(row), column.name, pattern), (a): a is boolean => typeof a === 'boolean', 'true or false')) }),
            ...(dataEdge === undefined ? {} : { dataEdge: this.#hook('dataEdge', dataEdge, f => (from: CellPosition, direction: GridDirection) => checkedAnswer(dataEdge,
                'dataEdge', f(from, direction), isCell, 'a cell { row, column }')) }),
            ...(pointAt === undefined ? {} : { pointAt: this.#hook('pointAt', pointAt, f => (text: string, caret: number) => checkedAnswer(pointAt, 'pointAt',
                f(text, caret), (a): a is boolean => typeof a === 'boolean', 'true or false')) }),
            ...(referenceText === undefined ? {} : { referenceText: this.#hook('referenceText', referenceText, f => (range: SelectionRange) => checkedAnswer(referenceText,
                'referenceText', f(range), (a): a is string => typeof a === 'string', 'text')) }),
            // Nothing is null, as .NET writes it; undefined is a function that answered nothing, and is refused, since
            // taken as nothing it would quietly leave the text, or outline nothing. What the core takes beyond the shape,
            // a span inside the text, is the core's to check.
            ...(cycleReference === undefined ? {} : { cycleReference: this.#hook('cycleReference', cycleReference, f => (text: string, start: number, end: number) =>
                nothingAsUndefined(checkedAnswer(cycleReference, 'cycleReference', f(text, start, end), (a): a is GridEditorRewrite | null => a === null || isRewrite(a),
                    'a rewrite { text, selectionStart, selectionEnd }, or null for none'))) }),
            ...(referencesIn === undefined ? {} : { referencesIn: this.#hook('referencesIn', referencesIn, f => (text: string) =>
                nothingAsUndefined(checkedAnswer(referencesIn, 'referencesIn', f(text), (a): a is readonly GridEditorReference[] | null => a === null || (Array.isArray(a) && a.every(isReference)),
                    'a list of References { start, length, range or key }, or null for none'))) }),
        };
    }

    #hook<T>(option: string, script: WireScript, make: (call: (...args: readonly unknown[]) => unknown) => T): T {
        const key = `${option}\u0000${keyOf(script)}`;
        const made = this.#made.get(key);
        if (made !== undefined) return made as T; // oxlint-disable-line typescript/no-unsafe-type-assertion -- made by this method for this option
        const f = this.#functionOf(script) as (...args: readonly unknown[]) => unknown; // oxlint-disable-line typescript/no-unsafe-type-assertion -- the Consumer's function, whose answer is checked as it is called
        const call = (...args: readonly unknown[]): unknown => {
            try {
                return f(...args);
            } catch (e: unknown) { // thrown on, named as the Consumer's function's own
                throw new ScriptFunctionError(`${scriptFunctionName(script)} threw as ${option}: ${e instanceof Error ? e.message : describeAnswer(e)}.`, { cause: e });
            }
        };
        const hook = make(call);
        this.#made.set(key, hook);
        return hook;
    }
}

/** What the Consumer's function answered, when it is what its option takes. */
function checkedAnswer<T>(script: WireScript, option: string, answer: unknown, is: (answer: unknown) => answer is T, takes: string): T {
    if (is(answer)) return answer;
    throw new ScriptFunctionError(`${scriptFunctionName(script)} answered ${describeAnswer(answer)} as ${option}, which takes ${takes}.`);
}

/** A script's null, which C#'s Consumer writes for nothing, as the core's undefined. */
function nothingAsUndefined<T>(answer: T | null): T | undefined {
    return answer === null ? undefined : answer;
}

const isRecord = (value: unknown): value is Readonly<Record<string, unknown>> => typeof value === 'object' && value !== null;

/** Whether an answer is shaped as a cell's position. */
function isCell(value: unknown): value is CellPosition {
    return isRecord(value) && Number.isInteger(value['row']) && Number.isInteger(value['column']);
}

/** Whether an answer is shaped as a rewrite of the editor's text. */
function isRewrite(value: unknown): value is GridEditorRewrite {
    return isRecord(value) && typeof value['text'] === 'string' && typeof value['selectionStart'] === 'number' && typeof value['selectionEnd'] === 'number';
}

/** Whether an answer is shaped as a Reference: a span, and the cells it names or a key. */
function isReference(value: unknown): value is GridEditorReference {
    if (!isRecord(value) || typeof value['start'] !== 'number' || typeof value['length'] !== 'number') return false;
    const { range, key } = value;
    const isRange = isRecord(range) && ['topRow', 'leftColumn', 'rowCount', 'columnCount'].every(part => typeof range[part] === 'number');
    return (range === undefined || isRange) && (key === undefined || typeof key === 'string');
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

/** The functions the grid asks .NET through beyond its events, made once, so the core sees the same ones on every
 * update (ADR-0003). */
interface Asked {
    readonly marks: WireMarks;
    readonly copyAnswerAsync: NonNullable<GridOptions<BlazorRow>['copyAnswerAsync']>;
    readonly onReplace: NonNullable<GridOptions<BlazorRow>['onReplace']>;
    readonly onSummarize: NonNullable<GridOptions<BlazorRow>['onSummarize']>;
    readonly onDeclaredKey: NonNullable<GridOptions<BlazorRow>['onDeclaredKey']>;
    readonly onSelectionSummaryChanged: NonNullable<GridOptions<BlazorRow>['onSelectionSummaryChanged']>;
    readonly onSummaryFiguresChanged: NonNullable<GridOptions<BlazorRow>['onSummaryFiguresChanged']>;
}

// A declared key, a Replace All and a marking gesture are writes of the user's: each goes by `write`, which holds what
// .NET sends until .NET has handled it, and the core holds the input after it until it has (ADR-0240's note of
// 2026-10-19, ADR-0142). A copy's answer and a summary are questions, numbered so a newer one calls off the older.
function askedThrough(
    dotnet: DotNetCalls, asking: Asking, marks: WireMarks, summarize: SummarizeOf, write: Write,
): Asked {
    const send = (kind: HeardKind, payload: unknown): void => { void dotnet.invokeMethodAsync('Heard', kind, payload); };
    return Object.freeze({
        marks,
        copyAnswerAsync: (request: GridCopyRequest, signal: AbortSignal) => asking(signal, async number => {
            const answer = await dotnet.invokeMethodAsync('Copy', number, request);
            signal.throwIfAborted();
            givenUp(answer);
            return copyAnswerOf(answer);
        }),
        onReplace: async (request: GridReplaceRequest) => {
            const answer = await write('replace', request);
            givenUp(answer);
            return replaceResultOf(answer);
        },
        onSummarize: (request: GridSummaryRequest, signal: AbortSignal) => summarize(undefined, request, signal),
        onDeclaredKey: async (press: GridDeclaredKeyPress) => {
            await write('declaredKey', { key: press.key, editOpen: press.editOpen, selection: wireSelection(press.selection), rowSequenceVersion: press.rowSequenceVersion });
        },
        onSelectionSummaryChanged: (summary: SelectionSummary) => { send('selectionSummaryChanged', wireSummary(summary)); },
        onSummaryFiguresChanged: (figures: readonly SummaryFigure[]) => { send('summaryFiguresChanged', figures); },
    });
}

/**
 * Row Marks kept in .NET (ADR-0043), by the component's `Marks` or by its source's: whether a row is marked comes with
 * the row, and the counts with the source's state, so the core paints what .NET last sent. A gesture crosses to .NET as
 * a write, and what it marked comes back as the state its handling sends. The Row Kind the core pushes is .NET's
 * already: the component hands the marks its own `RowKind` there, where every row's kind is read.
 */
class WireMarks implements RowMarks<BlazorRow> {
    readonly #source: () => DotNetSource;
    readonly #write: (kind: HeardKind, payload: unknown) => Promise<unknown>;
    readonly #listeners = new Set<() => void>();

    constructor(source: () => DotNetSource, write: (kind: HeardKind, payload: unknown) => Promise<unknown>) {
        this.#source = source;
        this.#write = write;
    }

    isMarked(row: BlazorRow): boolean {
        if (row.marked === undefined) throw new Error(`Internal: row ${row.id} came without its mark from a grid with Row Marks.`);
        return row.marked;
    }

    get counts(): RowMarkCounts | undefined {
        return this.#source().markCounts;
    }

    async onMarkIntent(intent: RowMarkIntent<BlazorRow>): Promise<void> {
        await this.#write('mark', intent.kind === 'oneRow' ? { kind: intent.kind, row: intent.row.id, marked: intent.marked } : intent);
    }

    setRowKind(): void {
        // .NET's marks read the component's own RowKind, which it hands them as it is set.
    }

    subscribe(listener: () => void): () => void {
        this.#listeners.add(listener);
        return () => { this.#listeners.delete(listener); };
    }

    /** .NET sent a state: the marks and their counts in it are the ones to paint. */
    changed(): void {
        for (const listener of Array.from(this.#listeners)) listener();
    }
}

/** A Selection Summary as .NET reads it back: an exact figure's digits as text, which JSON holds whole where a `bigint`
 * cannot cross at all. */
function wireSummary(summary: SelectionSummary): unknown {
    const { result } = summary;
    if (result?.kind !== 'answered') return summary;
    const figures = Object.fromEntries(Object.entries(result.figures).map(([figure, figured]) => [figure,
        figured.kind === 'number'
            ? {
                ...figured, number: wireNumber(figured.number),
                exact: figured.exact === undefined ? undefined : { mantissa: figured.exact.mantissa.toString(), scale: figured.exact.scale },
            }
            : figured]));
    return { ...summary, result: { ...result, figures } };
}

/** A figure's number as JSON holds it: one past a double's range by the name .NET reads, never JSON's null. */
function wireNumber(value: number): number | string {
    return Number.isFinite(value) ? value : String(value);
}

/** A figure's number as .NET writes it: a number, or the name of one JSON cannot hold. */
function figureNumberOf(value: unknown): number | undefined {
    if (typeof value === 'number') return value;
    if (value === 'NaN') return Number.NaN;
    if (value === 'Infinity') return Number.POSITIVE_INFINITY;
    if (value === '-Infinity') return Number.NEGATIVE_INFINITY;
    return undefined;
}

/** .NET's answer to a copy, as `ExGrid.Script` writes a `GridCopyAnswer`; one .NET did not answer, its component gone, is
 * given up, and nothing is written. */
function copyAnswerOf(answer: unknown): GridCopyAnswer {
    if (answer === null) throw new DOMException('.NET gave the copy no answer: the component is gone.', 'AbortError');
    if (typeof answer !== 'string') throw new TypeError('Internal: .NET answered a copy with something other than JSON text.');
    const read = parsed<{ readonly kind?: unknown; readonly text?: unknown; readonly html?: unknown; readonly sentence?: unknown; readonly error?: unknown }>(answer);
    // The Consumer's answer failed in .NET (CON-8): the core refuses the copy aloud and surfaces this.
    if (read.kind === 'failed' && typeof read.error === 'string') throw new Error(`[ex-grid] the grid failed to build a copy: the Consumer's answer threw ${read.error}.`);
    if (read.kind === 'write' && typeof read.text === 'string' && typeof read.html === 'string') return { kind: 'write', text: read.text, html: read.html };
    if (read.kind === 'refuse' && typeof read.sentence === 'string') return { kind: 'refuse', sentence: read.sentence };
    throw new TypeError(`Internal: .NET answered a copy with ${answer}.`);
}

/** .NET's answer to a Replace All, as `ExGrid.Script` writes a `GridReplaceResult`. */
function replaceResultOf(answer: unknown): GridReplaceResult {
    if (typeof answer !== 'string') throw new TypeError('Internal: .NET answered a Replace All with something other than JSON text.');
    const read = parsed<{ readonly kind?: unknown; readonly count?: unknown; readonly cells?: unknown; readonly rowSequenceVersion?: unknown }>(answer);
    const version = read.rowSequenceVersion;
    if (typeof version !== 'number') throw new TypeError(`Internal: .NET answered a Replace All with ${answer}.`);
    if (read.kind === 'replaced' && typeof read.count === 'number') return { kind: 'replaced', count: read.count, rowSequenceVersion: version };
    if (read.kind === 'refused' && Array.isArray(read.cells)) {
        const cells = (read.cells as readonly unknown[]).map(cell => {
            const { row, column, message } = cell as { readonly row?: unknown; readonly column?: unknown; readonly message?: unknown }; // oxlint-disable-line typescript/no-unsafe-type-assertion -- each member is checked below
            if (typeof row !== 'number' || typeof column !== 'string' || (message !== null && typeof message !== 'string')) {
                throw new TypeError(`Internal: .NET answered a refused cell as ${JSON.stringify(cell)}.`);
            }
            return { row, column, message: message === null ? undefined : message };
        });
        return { kind: 'refused', cells, rowSequenceVersion: version };
    }
    throw new TypeError(`Internal: .NET answered a Replace All with ${answer}.`);
}

/** .NET's answer to a Selection Summary, as `ExGrid.Script` writes a `GridSummaryResult`; one .NET did not answer, its
 * source replaced or its component gone, is given up, and no figure is shown for it. */
function summaryResultOf(answer: unknown): GridSummaryResult {
    if (answer === null) throw new DOMException('.NET gave the Selection Summary no answer: its source or component is gone.', 'AbortError');
    if (typeof answer !== 'string') throw new TypeError('Internal: .NET answered a Selection Summary with something other than JSON text.');
    const read = parsed<{ readonly kind?: unknown; readonly reason?: unknown; readonly figures?: unknown; readonly texts?: unknown }>(answer);
    if (read.kind === 'declined' && typeof read.reason === 'string') return { kind: 'declined', reason: read.reason };
    if (read.kind !== 'answered' || !isRecord(read.figures) || !isRecord(read.texts)) throw new TypeError(`Internal: .NET answered a Selection Summary with ${answer}.`);
    const figures: Partial<Record<SummaryFigure, AggregateResult>> = {};
    for (const [figure, result] of Object.entries(read.figures)) figures[summaryFigureOf(figure)] = aggregateOf(result);
    const texts: Partial<Record<SummaryFigure, string>> = {};
    for (const [figure, text] of Object.entries(read.texts)) {
        if (typeof text !== 'string') throw new TypeError(`Internal: .NET wrote the text of '${figure}' as ${JSON.stringify(text)}.`);
        texts[summaryFigureOf(figure)] = text;
    }
    return { kind: 'answered', figures: Object.freeze(figures), texts: Object.freeze(texts) };
}

const summaryFigures: readonly string[] = ['average', 'count', 'numericalCount', 'min', 'max', 'sum'];

function summaryFigureOf(name: string): SummaryFigure {
    if (!summaryFigures.includes(name)) throw new TypeError(`Internal: .NET answered the figure '${name}'.`);
    return name as SummaryFigure; // oxlint-disable-line typescript/no-unsafe-type-assertion -- checked against the figures above
}

// A figure as .NET's AggregateResult holds it: nothing, an error, or a number, with the decimal it was summed in.
function aggregateOf(value: unknown): AggregateResult {
    if (!isRecord(value)) throw new TypeError(`Internal: .NET wrote a figure as ${JSON.stringify(value)}.`);
    const { kind, error, exact } = value;
    const number = figureNumberOf(value['number']);
    if (kind === 'empty') return { kind: 'empty' };
    if (kind === 'error' && (error === 'divideByZero' || error === 'notANumber')) return { kind: 'error', error };
    if (kind === 'number' && number !== undefined) {
        if (exact === undefined) return { kind: 'number', number, exact: undefined };
        if (isRecord(exact) && typeof exact['mantissa'] === 'string' && Number.isInteger(exact['scale'])) {
            return { kind: 'number', number, exact: Object.freeze({ mantissa: BigInt(exact['mantissa']), scale: exact['scale'] as number }) }; // oxlint-disable-line typescript/no-unsafe-type-assertion -- checked an integer above
        }
    }
    throw new TypeError(`Internal: .NET wrote a figure as ${JSON.stringify(value)}.`);
}
