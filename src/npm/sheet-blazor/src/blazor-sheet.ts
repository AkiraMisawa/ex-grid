// Blazor's ExSheet, the browser's half (ADR-0370 section 1): ExSheet's Script Core mounted on the element the
// component renders, given the component's parameters as options and its documents as the JSON both engines read and
// write, and every event it raises handed to .NET in the order it was raised. It decides nothing and computes
// nothing: the Workbook, its Formulas and what each input means are the Script Core's, in the browser.

import {
    builtInLabelIds, type AggregateResult, type GridSelection, type GridSummaryRequest, type GridSummaryResult, type SelectionSummary, type SummaryFigure,
    type ViewportSize,
} from '@ex-grid/core';
import {
    crossingCalls, HeldMessages, holdPointingScope, isGridRazorSlotName, RazorSlotHost, slotHostOptionsOf, WireMetrics, type GridRazorSlotName, type PagePointingScope,
    type WireHeld, type WirePresentationDefaults,
} from '@ex-grid/blazor-shared';
import { decimalText } from '@ex-grid/data';
import {
    createSheet, PointingScope, sheetWords, type FocusFormatReading, type GridPointedAt, type PointingRefusal, type SheetChrome, type SheetHandle, type SheetLabel, type SheetOptions, type SheetTurn,
    type SheetToolbarCommand, type SheetToolbarContent, type SheetWordId, type TableRow,
} from '@ex-grid/sheet';
import {
    CellAddress, columnCount, FormulaSyntaxError, readCellFormatChange, readLinkedRows, readSheetEdit, RepeatedKeyError, SheetDocumentError, SheetRefusedError,
    WorkbookDocument, writeCellFormat, writeLinkedRows, writeRepeatedKey, type SheetDay, type SheetMoment, type SheetRefusal, type Value,
    type Workbook,
} from '@ex-grid/sheet-engine';
import { isSheetRazorSlotName, sheetRazorSlotKinds, toolbarContentSlotName, type SheetRazorSlotName } from './razor-sheet-slots';

/** The events .NET listens to: each one the component has a handler for, and no other. Whether an edit is open is
 * always heard, since the component answers `IsEditing`. */
export type HeardKind =
    | 'workbookChanged' | 'documentChanged' | 'sheetsChanged' | 'showToolbarChanged' | 'summaryFiguresChanged' | 'selectionChanged'
    | 'selectionSummaryChanged' | 'formatKeyRefused' | 'pointingRefused';

/** ExSheet's command ids as the Razor component names them (`SheetCommandIds`), each with the id the Script Core
 * words it by, so a Consumer's `CommandLabel` words both components alike. */
const commandIdAliases: ReadonlyMap<string, SheetWordId> = new Map<string, SheetWordId>([
    ['exsheet.insert-rows', 'command.insertRows'],
    ['exsheet.delete-rows', 'command.deleteRows'],
    ['exsheet.insert-columns', 'command.insertColumns'],
    ['exsheet.delete-columns', 'command.deleteColumns'],
    ['exsheet.format-cells', 'command.formatCells'],
    ['exsheet.summary-refused', 'summaryRefused'],
]);

/** Every id the Consumer's `CommandLabel` words: the grid's own, and ExSheet's by the Razor component's names. .NET
 * cannot hand a function across, so it answers each of them once and sends the words (ADR-0370 section 10). */
export function labelIds(): readonly string[] {
    return Object.freeze([...new Set([...builtInLabelIds(), ...commandIdAliases.keys()])]);
}

/** The options .NET sets as they are, with no function or object of the browser's in them. */
type PassedOption =
    | 'showFormulaBar' | 'pinnedColumnCount' | 'showRowHeadings' | 'showColumnHeadings' | 'showSheetTabs' | 'showToolbar'
    | 'showSelectionSummary' | 'summaryFigures' | 'allowCopyWithHeaders' | 'rowHeight' | 'viewportWidth' | 'viewportHeight'
    | 'today' | 'timeZone';

const passedOptions: readonly PassedOption[] = Object.freeze([
    'showFormulaBar', 'pinnedColumnCount', 'showRowHeadings', 'showColumnHeadings', 'showSheetTabs', 'showToolbar',
    'showSelectionSummary', 'summaryFigures', 'allowCopyWithHeaders', 'rowHeight', 'viewportWidth', 'viewportHeight',
    'today', 'timeZone',
]);

/** The component's parameters, as the Script Core's options are spelt. .NET sends what the Razor component defaults,
 * so a Sheet looks the same under either. */
export interface WireOptions {
    readonly locale: string;
    readonly showFormulaBar?: boolean;
    readonly pinnedColumnCount?: number;
    readonly showRowHeadings?: boolean;
    readonly showColumnHeadings?: boolean;
    readonly showSheetTabs?: boolean;
    readonly showToolbar?: boolean;
    readonly showSelectionSummary?: boolean;
    readonly summaryFigures?: readonly SummaryFigure[];
    readonly allowCopyWithHeaders?: boolean;
    readonly rowHeight?: number;
    readonly viewportWidth?: ViewportSize;
    readonly viewportHeight?: ViewportSize;
    /** The Wrapper's widths and Density its paper cascades (ADR-0030), which the cells are painted and measured in. */
    readonly presentationDefaults?: WirePresentationDefaults;
    readonly today?: SheetDay;
    readonly timeZone?: string;
    /** The Sheet Seed, an unsigned 64-bit integer written in decimal: JSON's numbers cannot hold one. */
    readonly seed?: string;
    /** The Pointing Scope's name in the page, where the Sheet joins it (ADR-0430). */
    readonly pointingScope?: string;
    /** The slots the Consumer's Chrome fills with Razor (ADR-0410): the grid's, never the find panel, which is the
     * Sheet's own, with Within (ADR-0332 §5), and the Sheet's own. */
    readonly razorSlots?: readonly (GridRazorSlotName | SheetRazorSlotName)[];
    /** What the component's `ToolbarContent` declares (ADR-0100), in markup order; absent, the default row alone. */
    readonly toolbar?: readonly WireToolbarRow[];
    readonly hears: readonly HeardKind[];
}

/** A Toolbar Row as .NET declares it: `default` for `DefaultToolbarRow`, or a row whose label is `null` for the items
 * that stand in no `ToolbarRow`, which takes the default row's name, as the Razor component lays them out. */
export type WireToolbarRow = 'default' | {
    readonly label: string | null;
    readonly keyTip?: string;
    readonly items: readonly WireToolbarEntry[];
};

/** One of a row's items: ExSheet's own, a separator, or the Consumer's `ToolbarButton`, by the number .NET gave it,
 * whose `OnClick` .NET runs and whose `ChildContent`, where `content` says it has one, .NET draws. */
export type WireToolbarEntry = SheetToolbarCommand | 'separator' | {
    readonly button: number;
    readonly text: string;
    readonly keyTip?: string;
    readonly disabledReason?: string;
    readonly content: boolean;
};

/** The Consumer's word for each of {@link labelIds} it answers, or `null` to keep the English. */
export type WireWords = Readonly<Record<string, string>> | null;

/** What `attach` is handed once: the parameters, the Consumer's document, if it handed one, and its words. */
export interface WireAttach {
    readonly options: WireOptions;
    /** The document in the JSON both engines write, a Workbook Document or a Sheet Document, or `null` for a Workbook
     * of one empty Sheet in `locale`. */
    readonly document: string | null;
    /** How many of the Consumer's documents .NET has sent, this one included. */
    readonly documentsSent: number;
    readonly words: WireWords;
    /** Whether .NET is across a circuit, Blazor Server's, whose hub limits the size of a message: only then is a
     * message too large for one held (`HeldMessages`). WebAssembly's .NET takes any message as it is. */
    readonly circuit: boolean;
}

/** What `update` is handed: the parameters again, whole; a document of the Consumer's own making, if it handed one
 * other than the one this Sheet raised last, or `null` for an empty Sheet; and the words, if they moved. */
export interface WireUpdate {
    readonly options: WireOptions;
    readonly document?: string | null;
    /** How many of the Consumer's documents .NET has sent, this one included; with `document`. */
    readonly documentsSent?: number;
    readonly words?: WireWords;
}

/**
 * What a command answers .NET: what the Sheet's method answered; the refusal it threw, which .NET throws as its own
 * `SheetRefusedException`; or what else it threw that a caller's mistake or the Sheet's state explains, which .NET
 * throws as the Razor component's engine throws it.
 */
export type WireAnswer<T = boolean> = { readonly done: T } | { readonly refused: WireRefusal } | { readonly thrown: WireThrown };

/**
 * An error a command threw, by its kind (`tests/cases/README.md`): `argument` a `TypeError`, `outOfRange` a
 * `RangeError`, `invalidOperation` a plain `Error`, `formulaSyntax` a Formula that cannot be read, with where and why,
 * and `repeatedKey` a refused snapshot, as `writeRepeatedKey` writes it.
 */
export type WireThrown =
    | { readonly kind: 'argument' | 'outOfRange' | 'invalidOperation'; readonly message: string }
    | { readonly kind: 'formulaSyntax'; readonly message: string; readonly formula: string; readonly position: number; readonly reason: string }
    | { readonly kind: 'repeatedKey'; readonly message: string; readonly repeatedKey: string }
    | { readonly kind: 'sheetDocument'; readonly message: string; readonly documentVersion: number | null };

/** What .NET's read of a file answered in the opening's turn: the document, its failure's message, which .NET throws
 * itself as it was, or that it saw its token cancelled. */
export type WireRead = { readonly document: string } | { readonly failed: string } | { readonly cancelled: true };

/** What .NET's write of a file answered: done, or its failure's message, which .NET throws itself as it was. */
export type WireWritten = { readonly done: true } | { readonly failed: string };

/**
 * The Workbook as a save hands it to .NET, for .NET to make the same Workbook and compute the same Values: its
 * document; each Linked Table's rows, as `writeLinkedRows` writes them, or `null` while it waits; the seed, an unsigned
 * 64-bit integer written in decimal, with the count of changes its draws are made from (ADR-0180); the Sheet Day; and
 * the moment `NOW` answers, the one the last recalculation read (ADR-0124).
 */
export interface WireSaved {
    readonly document: string;
    readonly tables: readonly { readonly name: string; readonly rows: string | null }[];
    readonly seed: string | null;
    readonly changes: number;
    readonly today: SheetDay | null;
    readonly now: SheetMoment | null;
}

/** A Linked Table as the Sheet declares it, for .NET's `LinkedTable`. */
export interface WireLinkedTable {
    readonly name: string;
    readonly columns: readonly string[];
    readonly keyColumns: readonly string[];
    readonly isWaiting: boolean;
    readonly rowCount: number;
}

/** A refusal, as .NET's `SheetRefusal` reads it. */
export interface WireRefusal {
    readonly reason: SheetRefusal['reason'];
    readonly message: string;
}

/** The documents of the Workbook as it stands: always the Workbook's, and the one Sheet's while it holds one. */
export interface WireDocuments {
    readonly workbook: string;
    readonly sheet: string | null;
}

/** The Focus cell's Cell Format, as a {@link BlazorSheet.readFocusFormat} read it. */
export interface WireFocusFormat {
    /** The number the set that follows names it by. */
    readonly reading: number;
    /** The Cell Format, as `writeCellFormat` writes it. */
    readonly format: string;
}

/** An answer as it crosses to .NET: itself, or held (`WireHeld`) when it is too large for a message of its own. */
export type Crossing<T> = T | WireHeld;

/** What the component asks of the Sheet it attached. */
export interface BlazorSheet {
    /** Takes a {@link WireUpdate}, as the JSON text .NET wrote. */
    update(json: string): void;
    undo(): Crossing<WireAnswer>;
    redo(): Crossing<WireAnswer>;
    openFormatCells(): Crossing<WireAnswer>;
    /** Does an edit .NET's `SheetWire` wrote, on the Sheet named, or with none named while the Workbook holds one. */
    do(sheet: string | null, edit: string): Crossing<WireAnswer<null>>;
    /** Sets a change .NET's `SheetWire` wrote on the Selection as the grid holds it; false when nothing is selected. */
    setCellFormat(change: string): Crossing<WireAnswer>;
    /** A cell's Cell Format as it shows, as `writeCellFormat` writes it. */
    cellFormatAt(sheet: string | null, address: string): Crossing<WireAnswer<string>>;
    /** The Focus cell's Cell Format as it shows, as `writeCellFormat` writes it, read for a change .NET makes from it,
     * and numbered for the set that follows; null when nothing is selected (ADR-0100). */
    readFocusFormat(): Crossing<WireAnswer<WireFocusFormat | null>>;
    /** Sets a change .NET made from the reading numbered `reading`, which it takes, as `SheetHandle.setCellFormatFrom`
     * does: refused once what it was read from no longer stands. */
    setCellFormatFrom(reading: number, change: string): Crossing<WireAnswer>;
    /** Lets go of the reading numbered `reading`, whose change .NET did not make. */
    forgetFocusFormat(reading: number): void;
    /** The Linked Tables declared, in the order they were declared. */
    linkedTables(): Crossing<WireLinkedTable[]>;
    declareLinkedTable(name: string, columns: readonly string[], key: readonly string[]): Crossing<WireAnswer<null>>;
    /** Pushes a snapshot .NET's `SheetWire` wrote. */
    pushLinkedTable(name: string, rows: string): Crossing<WireAnswer<null>>;
    /**
     * Opens a file at this call's place in the Input Order (ADR-0330 §10): in its turn the Sheet asks .NET to read the
     * opening numbered `id`, and an Escape that cancels it tells .NET so. Called from the OnClick of the press numbered
     * `press`, while that press holds the order, it opens inside the press's turn, as part of it (ADR-0240 section 6);
     * a press that has ended since leaves it its own place.
     */
    open(id: number, press: number | null): Promise<Crossing<WireAnswer>>;
    /** Saves at this call's place in the Input Order (ADR-0330 §10): in its turn the Sheet hands .NET the Workbook as a
     * {@link WireSaved}, for the write of the save numbered `id`. Inside the press numbered `press`, as `open` is. */
    save(id: number, press: number | null): Promise<Crossing<WireAnswer<null>>>;
    /** The Workbook as it stands, as documents. */
    documents(): Crossing<WireDocuments>;
    /** A message held for .NET (`WireHeld`), as the stream .NET reads it from: gzip-compressed where the browser can
     * compress, and as it is where it cannot. Taken once. */
    take(held: number): Promise<Uint8Array<ArrayBuffer>>;
    /** .NET rendered the contents of the Razor slot `id` for the data numbered `version` in `contents` (ADR-0410). */
    slotRendered(id: number, version: number, contents: HTMLElement | null): void;
    /** Says a refusal .NET met running a Consumer's toolbar button in the Sheet's notice, as the Razor component's
     * toolbar says one. */
    tell(sentence: string): void;
    /** Stops the Sheet and empties its element. */
    dispose(): void;
}

/** The calls that can carry more than a message holds, and that .NET's `Held` makes: `ScriptBridge.Held`. */
const heldSheetCalls: ReadonlySet<string> = new Set(['Heard', 'Write', 'MountSlot', 'UpdateSlot']);

/** What the bridge calls in .NET. */
export interface DotNetHandle {
    invokeMethodAsync(method: string, ...args: readonly unknown[]): Promise<unknown>;
}

/**
 * Mounts ExSheet's Script Core on `root`, the element the Blazor component rendered, with `dotnet` the component's
 * `DotNetObjectReference` and `json` a {@link WireAttach}. What it answers is the component's handle on the Sheet.
 */
export function attach(root: HTMLElement, bridge: DotNetHandle, json: string): BlazorSheet {
    const init = parsed<WireAttach>(json);
    let stopped = false;
    // An event or a save's Workbook too large for a message of its own is held, and .NET takes it in its turn, as the
    // Shared Script crosses every product's: the hub would otherwise close the circuit.
    const held = new HeldMessages(init.circuit);
    const dotnet = crossingCalls(bridge, held, heldSheetCalls);
    // Once the Sheet is stopped .NET has let go of the bridge, and nothing more crosses: an event still being taken
    // then is let go with it, and only a failure while the Sheet stands is the page's to report.
    const send = (kind: HeardKind | 'editingChanged', payload: unknown): void => {
        if (stopped) return;
        void dotnet.invokeMethodAsync('Heard', kind, payload).catch((failure: unknown) => { // oxlint-disable-line ex-grid/no-catch -- rethrown while the Sheet stands; once stopped .NET has let go of what it was taking
            if (!stopped) throw failure;
        });
    };
    // A command's answer as it crosses: itself, or held when it is too large for a message of its own; .NET takes it
    // before it reads the answer.
    const answer = <T>(value: T): T | WireHeld => held.crossing(value) ?? value;
    // The Focus readings .NET is making a change from, by number, until it sets the change or lets the reading go.
    const readings = new Map<number, FocusFormatReading>();
    let readingNumber = 0;
    // The document the Sheet is handed on every update: the Consumer's, read once, or the one the Sheet raised last,
    // the same object, so handing it back opens nothing.
    let document = init.document === null ? undefined : WorkbookDocument.fromJson(init.document);
    // How many of the Consumer's documents the Sheet has been given: told with each document it raises, so .NET knows
    // whether one of the Consumer's reached the screen after it.
    let documentsSent = init.documentsSent;
    let words = wordsOf(init.words);
    // What .NET listens to, as the parameters last said.
    let hears: ReadonlySet<HeardKind> = new Set(init.options.hears);
    const heard = handlers(send, kind => hears.has(kind), raised => { document = raised; }, () => documentsSent, () => made);
    // The Pointing Scope the parameters name, held in the page while this Sheet stands in it (ADR-0430).
    let inScope: { readonly id: string; readonly scope: SheetPageScope; readonly release: () => void } | undefined;
    const scopeNamed = (id: string | undefined): PointingScope | undefined => {
        if (inScope?.id !== id) {
            const before = inScope;
            inScope = id === undefined ? undefined : { id, ...sheetPageScope(id) };
            before?.release();
        }
        return inScope?.scope.pointing;
    };
    // The slots the Chrome fills with Razor (ADR-0410), the grid's and the Sheet's. A slot mounted as the Sheet is made
    // holds its Settled Mark once there is a Sheet to hold, before the task ends.
    const making: { sheet?: SheetHandle } = {};
    const slots = new RazorSlotHost(dotnet, sheetRazorSlotKinds, slotHostOptionsOf(() => making.sheet, 'Sheet'));
    const chromeOf = (wire: WireOptions): SheetChrome | undefined => {
        const names = wire.razorSlots;
        if (names === undefined || names.length === 0) return undefined;
        for (const name of names) {
            // The find panel is the Sheet's own, which a grid's panel would replace (ADR-0332 §5): .NET never names it.
            if (!(isGridRazorSlotName(name) || isSheetRazorSlotName(name)) || name === 'findPanel') throw new TypeError(`Internal: .NET named '${name}' as a slot its Chrome fills.`);
        }
        return slots.mounts(names);
    };
    // The Consumer's buttons' contents, which .NET draws from their markup: each the same function from one update to
    // the next, as the Sheet asks, so a content standing is not drawn again.
    const contents = new Map<number, SheetToolbarContent>();
    const contentOf = (button: number): SheetToolbarContent => {
        const known = contents.get(button);
        if (known !== undefined) return known;
        const content: SheetToolbarContent = element => slots.mount(toolbarContentSlotName, element, { button });
        contents.set(button, content);
        return content;
    };
    // A press on a Consumer's button runs its OnClick in .NET in the press's place in the Sheet's Input Order: the inputs
    // made after it wait until it has run, as on the Razor component (ADR-0240 section 6), so no edit opens in between.
    // Each press is numbered, and its turn held by that number while the OnClick runs: a file the OnClick opens or saves
    // is opened or saved inside it, as part of the press, as on the Razor component.
    let presses = 0;
    const pressTurns = new Map<number, SheetTurn>();
    const pressed = (button: number): void => {
        const sheet = making.sheet;
        if (sheet === undefined) throw new Error('Internal: a toolbar button was pressed while the Sheet was being made.');
        const press = ++presses;
        void sheet.inOrder(async turn => {
            pressTurns.set(press, turn);
            try {
                await dotnet.invokeMethodAsync('ToolbarPressed', button, press).catch((failure: unknown) => { // oxlint-disable-line ex-grid/no-catch -- rethrown while the Sheet stands; once stopped .NET has let go of the button
                    if (!stopped) throw failure;
                });
            } finally {
                pressTurns.delete(press);
            }
        });
    };
    // Where a file .NET opens or saves takes its place: inside the press it was called from while that press holds the
    // order, else the Sheet's own place for it.
    const placeOf = (press: number | null): Pick<SheetHandle, 'open' | 'save'> => {
        const turn = press === null ? undefined : pressTurns.get(press);
        return turn === undefined ? made : turn;
    };
    const toolbarOf = (wire: WireOptions, wording: Words): SheetOptions['toolbar'] => {
        // A button gone takes its content's function with it: .NET numbers a button made again anew.
        const standing = new Set(wire.toolbar?.flatMap(row => (row === 'default' ? [] : row.items.flatMap(entry => (typeof entry === 'string' ? [] : [entry.button])))));
        for (const button of contents.keys()) if (!standing.has(button)) contents.delete(button);
        return toolbarRowsOf(wire, wording);
    };
    const toolbarRowsOf = (wire: WireOptions, wording: Words): SheetOptions['toolbar'] => wire.toolbar?.map(row => (row === 'default' ? row : {
        // The items that stand in no row are one row, named as the Script Core names its own default row.
        label: row.label ?? sheetWords({ label: wording.label }).toolbarHomeRow,
        keyTip: row.keyTip,
        items: row.items.map(entry => (typeof entry === 'string' ? entry : {
            text: entry.text,
            onPress: () => { pressed(entry.button); },
            keyTip: entry.keyTip,
            disabledReason: entry.disabledReason,
            content: entry.content ? contentOf(entry.button) : undefined,
        })),
    }));
    // The Sheet raises nothing while it is made, so `made` stands before a handler reads it.
    // The same defaults written again are the same object, so the Sheet measures its columns anew only when they move.
    const metrics = new WireMetrics();
    const made = createSheet(root, sheetOptions(
        init.options, document, words, heard, scopeNamed(init.options.pointingScope), chromeOf(init.options), toolbarOf(init.options, words), metrics,
    ));
    making.sheet = made;
    return Object.freeze({
        update: (text: string) => {
            const next = parsed<WireUpdate>(text);
            // .NET counts a document sent whether or not the Sheet takes it, so the count moves first.
            if (next.documentsSent !== undefined) documentsSent = next.documentsSent;
            // Taken only once the Sheet has: one it refuses, as a document of a version it does not read, changes nothing.
            const nextDocument = next.document === undefined ? document : next.document === null ? undefined : WorkbookDocument.fromJson(next.document);
            const nextWords = next.words === undefined ? words : wordsOf(next.words);
            // Heard as the parameters now say, events the update itself raises included.
            hears = new Set(next.options.hears);
            made.update(sheetOptions(
                next.options, nextDocument, nextWords, heard, scopeNamed(next.options.pointingScope), chromeOf(next.options), toolbarOf(next.options, nextWords),
                metrics,
            ));
            document = nextDocument;
            words = nextWords;
        },
        undo: () => answer(answered(() => made.undo())),
        redo: () => answer(answered(() => made.redo())),
        openFormatCells: () => answer(answered(() => made.openFormatCells())),
        do: (sheet: string | null, edit: string) => answer(answered(() => {
            const read = readSheetEdit(edit);
            if (sheet === null) made.do(read);
            else made.do(sheet, read);
            return null;
        })),
        setCellFormat: (change: string) => answer(answered(() => made.setCellFormat(readCellFormatChange(change)))),
        cellFormatAt: (sheet: string | null, address: string) => answer(answered(() => {
            const at = CellAddress.parse(address);
            return writeCellFormat(sheet === null ? made.cellFormatAt(at) : made.cellFormatAt(sheet, at));
        })),
        readFocusFormat: () => answer(answered(() => {
            const reading = made.readFocusFormat();
            if (reading === undefined) return null;
            readingNumber += 1;
            readings.set(readingNumber, reading);
            return { reading: readingNumber, format: writeCellFormat(reading.format) };
        })),
        setCellFormatFrom: (reading: number, change: string) => answer(answered(() => {
            const read = readings.get(reading);
            if (read === undefined) throw new Error(`Internal: .NET named the Focus reading ${String(reading)}, which the Sheet does not hold.`);
            readings.delete(reading);
            return made.setCellFormatFrom(read, readCellFormatChange(change));
        })),
        forgetFocusFormat: (reading: number) => { readings.delete(reading); },
        linkedTables: () => answer(made.workbook.linkedTables.map(table => ({
            name: table.name, columns: table.columns, keyColumns: table.keyColumns, isWaiting: table.isWaiting, rowCount: table.rowCount,
        }))),
        declareLinkedTable: (name: string, columns: readonly string[], key: readonly string[]) => answer(answered(() => {
            made.declareLinkedTable(name, columns, key);
            return null;
        })),
        pushLinkedTable: (name: string, rows: string) => answer(answered(() => {
            made.pushLinkedTable(name, readLinkedRows(rows));
            return null;
        })),
        open: async (id: number, press: number | null) => answer(await answeredLater(() => placeOf(press).open(signal => readInDotNet(dotnet, id, signal, () => stopped)))),
        save: async (id: number, press: number | null) => answer(await answeredLater(async () => {
            await placeOf(press).save(workbook => writeInDotNet(dotnet, id, workbook));
            return null;
        })),
        documents: () => answer({ workbook: made.workbook.toDocument().toJson(), sheet: sheetDocumentOf(made) }),
        take: (number: number) => held.take(number),
        slotRendered: (id: number, version: number, drawn: HTMLElement | null) => { slots.rendered(id, version, drawn); },
        tell: (sentence: string) => { made.tell(sentence); },
        dispose: () => {
            stopped = true;
            // Every slot's contents go back to their holders before the Sheet empties its element.
            slots.dispose();
            held.clear();
            made.destroy();
            // Left once the Sheet has left it: its last member gone, the page lets the Scope go.
            scopeNamed(undefined);
        },
    });
}

/** JSON .NET wrote, in a shape the adapter's own C# writes, member for member. */
// oxlint-disable-next-line typescript/no-unnecessary-type-parameters -- the caller names the shape .NET wrote, and so the one assertion is here
export function parsed<T>(json: string): T {
    if (typeof json !== 'string') throw new TypeError('Internal: .NET sent the bridge something other than JSON text.');
    return JSON.parse(json) as T; // oxlint-disable-line typescript/no-unsafe-type-assertion -- the shapes ExSheet.Script writes; each reader checks what it reads
}

// A command's answer, or the Sheet's refusal of it, which crosses as data: any other failure is the command's.
function answered<T>(command: () => T): WireAnswer<T> {
    try {
        return { done: command() };
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- what the command threw is its answer, which .NET throws again as its own; anything else is rethrown
        const thrown = wireThrown(error);
        if (thrown === undefined) throw error;
        return thrown;
    }
}

async function answeredLater<T>(command: () => Promise<T>): Promise<WireAnswer<T>> {
    try {
        return { done: await command() };
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- what the command threw is its answer, which .NET throws again as its own; anything else is rethrown
        const thrown = wireThrown(error);
        if (thrown === undefined) throw error;
        return thrown;
    }
}

// The read of an opening, in its turn: .NET reads the file, and an Escape that aborts the signal cancels .NET's token.
async function readInDotNet(dotnet: DotNetHandle, id: number, signal: AbortSignal, stopped: () => boolean): Promise<WorkbookDocument> {
    const cancel = (): void => {
        if (!stopped()) void dotnet.invokeMethodAsync('CancelRead', id);
    };
    signal.addEventListener('abort', cancel, { once: true });
    try {
        const read = parsed<WireRead>(textOf(await dotnet.invokeMethodAsync('Read', id)));
        if ('document' in read) return WorkbookDocument.fromJson(read.document);
        if ('failed' in read) throw new Error(read.failed);
        // .NET saw its token cancelled: by the Escape, or by the component going away, which stops the Sheet too.
        signal.throwIfAborted();
        throw new Error('The read was cancelled in .NET.');
    } finally {
        signal.removeEventListener('abort', cancel);
    }
}

// The write of a save, in its turn: the Workbook read whole before anything is awaited, as it is lent for the call
// only, and handed to .NET, which makes its own of it: held, when it is too large for a message of its own.
function writeInDotNet(dotnet: DotNetHandle, id: number, workbook: Workbook): Promise<void> {
    return dotnet.invokeMethodAsync('Write', id, savedOf(workbook)).then(answer => {
        const written = parsed<WireWritten>(textOf(answer));
        if ('failed' in written) throw new Error(written.failed);
    });
}

function savedOf(workbook: Workbook): WireSaved {
    const { seed, today, moment } = workbook;
    return {
        document: workbook.toDocument().toJson(),
        tables: workbook.linkedTables.map(table => {
            const columns = workbook.linkedTableColumns(table.name);
            return { name: table.name, rows: columns === undefined ? null : writeLinkedRows(rowsOf(columns, table.rowCount)) };
        }),
        seed: seed === undefined ? null : seed.toString(),
        changes: workbook.changes,
        today: today === undefined ? null : { year: today.year, month: today.month, day: today.day },
        now: moment === undefined ? null : moment,
    };
}

// A snapshot's columns as its rows.
function rowsOf(columns: readonly (readonly (Value | undefined)[])[], count: number): (Value | undefined)[][] {
    return Array.from({ length: count }, (_, row) => columns.map(column => column[row]));
}

function textOf(answer: unknown): string {
    if (typeof answer !== 'string') throw new TypeError('Internal: .NET answered the bridge something other than JSON text.');
    return answer;
}

// What the Sheet and its engine throw at a caller, as data .NET throws again as the Razor component's engine throws
// it: the kinds of `tests/cases/README.md`, refused alike by both engines. Anything else is a defect, and stays one.
function wireThrown(error: unknown): { readonly refused: WireRefusal } | { readonly thrown: WireThrown } | undefined {
    if (error instanceof SheetRefusedError) return { refused: wireRefusal(error.refusal) };
    if (error instanceof RepeatedKeyError) return { thrown: { kind: 'repeatedKey', message: error.message, repeatedKey: writeRepeatedKey(error) } };
    if (error instanceof FormulaSyntaxError) {
        return { thrown: { kind: 'formulaSyntax', message: error.message, formula: error.formula, position: error.position, reason: error.reason } };
    }
    if (error instanceof SheetDocumentError) {
        return { thrown: { kind: 'sheetDocument', message: error.message, documentVersion: error.documentVersion === undefined ? null : error.documentVersion } };
    }
    if (error instanceof RangeError) return { thrown: { kind: 'outOfRange', message: error.message } };
    if (error instanceof TypeError) return { thrown: { kind: 'argument', message: error.message } };
    if (error instanceof Error && Object.getPrototypeOf(error) === Error.prototype) return { thrown: { kind: 'invalidOperation', message: error.message } };
    return undefined;
}

function wireRefusal(refusal: SheetRefusal): WireRefusal {
    return { reason: refusal.reason, message: refusal.message };
}

// The one Sheet's document while the Workbook holds one, as the Razor component raises it: a Sheet Document records
// one Sheet whole, and no more.
function sheetDocumentOf(sheet: SheetHandle): string | null {
    const { workbook } = sheet;
    const [only, ...others] = workbook.sheets;
    return only !== undefined && others.length === 0 ? workbook.toSheetDocument(only.name).toJson() : null;
}

type Handlers = Required<Pick<SheetOptions,
    'onWorkbookChanged' | 'onSheetsChanged' | 'onShowToolbarChanged' | 'onSummaryFiguresChanged' | 'onSelectionChanged' | 'onEditingChanged'
    | 'onSelectionSummaryChanged' | 'onFormatKeyRefused' | 'onPointingRefused'>>;

// One function per event, made once, so the Sheet sees the same handler on every update.
function handlers(
    send: (kind: HeardKind | 'editingChanged', payload: unknown) => void, hearing: (kind: HeardKind) => boolean,
    raised: (document: WorkbookDocument) => void, documentsSent: () => number, sheet: () => SheetHandle,
): Handlers {
    return Object.freeze({
        // The document raised is kept whether or not .NET listens to both, since it is the Workbook on screen.
        onWorkbookChanged: (document: WorkbookDocument) => {
            raised(document);
            send('workbookChanged', {
                workbook: hearing('workbookChanged') ? document.toJson() : null,
                sheet: hearing('documentChanged') ? sheetDocumentOf(sheet()) : null,
                documentsSent: documentsSent(),
            });
        },
        onSheetsChanged: change => { send('sheetsChanged', change); },
        onShowToolbarChanged: shown => { send('showToolbarChanged', shown); },
        onSummaryFiguresChanged: figures => { send('summaryFiguresChanged', figures); },
        onSelectionChanged: selection => { send('selectionChanged', wireSelection(selection)); },
        onSelectionSummaryChanged: summary => { send('selectionSummaryChanged', wireSummary(summary)); },
        onEditingChanged: open => { send('editingChanged', open); },
        onFormatKeyRefused: refusal => { send('formatKeyRefused', wireRefusal(refusal)); },
        onPointingRefused: (refusal: PointingRefusal) => { send('pointingRefused', { reason: refusal.reason, message: refusal.message }); },
    });
}

/**
 * A Pointing Scope of the page as ExSheet's Blazor module makes it (ADR-0430): the Script Core's Scope, which this
 * module's Sheets are given, and the grids ExGrid's module registers in it, each declaration once. A grid's row is read
 * as the table's row from the Values .NET sent with it, once per row the grid holds.
 */
class SheetPageScope implements PagePointingScope {
    /** The Script Core's Scope. */
    readonly pointing = new PointingScope();
    readonly #declared = new Map<string, unknown>();
    readonly #read = new WeakMap<readonly unknown[], TableRow>();

    registerGrid<TRow>(
        declaration: string, table: string, tableColumns: Readonly<Record<string, string>>, tableRow: (row: TRow) => readonly unknown[],
    ): GridPointedAt<TRow> {
        const known = this.#declared.get(declaration);
        // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- registered below by ExGrid's module, whose rows are of one type
        if (known !== undefined) return known as GridPointedAt<TRow>;
        const made = this.pointing.registerGrid<TRow>(table, row => this.#tableRow(tableRow(row)), tableColumns);
        this.#declared.set(declaration, made);
        return made;
    }

    // Read once per row object: the Scope asks for the rows the grid paints while a cell is dashed.
    #tableRow(written: readonly unknown[]): TableRow {
        const known = this.#read.get(written);
        if (known !== undefined) return known;
        const [row] = readLinkedRows(JSON.stringify([written]));
        if (row === undefined) throw new Error('Internal: a table\'s row read as no row.');
        this.#read.set(written, row);
        return row;
    }
}

// The page's Scope named `id`, held by a Sheet: the one standing, or one this module makes.
function sheetPageScope(id: string): { readonly scope: SheetPageScope; readonly release: () => void } {
    const held = holdPointingScope(id, () => new SheetPageScope());
    if (!(held.scope instanceof SheetPageScope)) {
        held.release();
        throw new Error('Internal: the page holds a Pointing Scope that ExSheet\'s module did not make.');
    }
    return { scope: held.scope, release: held.release };
}

/** A Selection as .NET reads it back (`GridSelection.Reported`): its ranges, its Focus, and which range holds the
 * Focus; no range and no Focus for none. Written as Blazor's ExGrid and ExPivot write their own. */
export function wireSelection(selection: Pick<GridSelection, 'ranges' | 'focus' | 'focusRange'>): unknown {
    const { focus, focusRange } = selection;
    if (focus === undefined || focusRange === undefined) return { ranges: [], focus: null, focusRangeIndex: 0 };
    // By identity: the Focus range is one of the ranges, and a rectangle selected twice is two ranges alike.
    const focusRangeIndex = selection.ranges.indexOf(focusRange);
    if (focusRangeIndex < 0) throw new Error('Internal: a Selection\'s Focus range is not among its ranges.');
    return { ranges: selection.ranges, focus, focusRangeIndex };
}

/** A Selection Summary as .NET reads it back: its status, the question and the answer, an exact figure as its
 * decimal's digits, which no JSON number holds. Written as Blazor's ExPivot writes its own, but for the columns: a
 * Sheet's are its letters, A to XFD, which .NET makes again rather than read 16,384 names on every selection. */
export function wireSummary(summary: SelectionSummary): unknown {
    return { status: summary.status, request: summary.request === undefined ? null : wireRequest(summary.request), result: summary.result === undefined ? null : wireResult(summary.result) };
}

function wireRequest(request: GridSummaryRequest): unknown {
    lettersChecked(request.columns);
    return {
        ranges: request.ranges,
        rowSequenceVersion: request.rowSequenceVersion,
        figures: request.figures,
        focus: request.focus === undefined ? null : request.focus,
    };
}

// The column lists already seen to be the Sheet's letters, so a list the grid hands again is not walked again.
const lettered = new WeakSet<readonly string[]>();
let letters: readonly string[] | undefined;

function lettersChecked(columns: readonly string[]): void {
    if (lettered.has(columns)) return;
    letters ??= Object.freeze(Array.from({ length: columnCount }, (_, at) => CellAddress.columnName(at)));
    const all = letters;
    if (columns.length !== all.length || columns.some((name, at) => name !== all[at]))
        throw new Error('Internal: a Sheet\'s columns are not its letters, A to XFD.');
    lettered.add(columns);
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

/** The Consumer's words as the Sheet takes them: the grid's ids through `commandLabel`, ExSheet's through `label`, by
 * the Script Core's id for each of the Razor component's. The same functions until .NET sends others. */
interface Words {
    readonly label: SheetLabel | undefined;
    readonly commandLabel: ((id: string) => string | undefined) | undefined;
}

export function wordsOf(words: WireWords): Words {
    if (words === null) return { label: undefined, commandLabel: undefined };
    const held: ReadonlyMap<string, string> = new Map(Object.entries(words));
    const aliased = new Map<SheetWordId, string>();
    for (const [razorId, id] of commandIdAliases) {
        const word = held.get(razorId);
        if (word !== undefined) aliased.set(id, word);
    }
    return {
        label: id => aliased.get(id),
        commandLabel: id => (commandIdAliases.has(id) ? undefined : held.get(id)),
    };
}

function sheetOptions(
    wire: WireOptions, document: WorkbookDocument | undefined, words: Words, heard: Handlers, pointingScope: PointingScope | undefined,
    chrome: SheetChrome | undefined, toolbar: SheetOptions['toolbar'], metrics: WireMetrics,
): SheetOptions {
    const hears = new Set(wire.hears);
    const presentationDefaults = metrics.presentationDefaults(wire.presentationDefaults);
    const passed = Object.fromEntries(passedOptions.filter(name => wire[name] !== undefined).map(name => [name, wire[name]]));
    return {
        ...(passed as Partial<Pick<SheetOptions, PassedOption>>),
        locale: wire.locale,
        document,
        ...(wire.seed === undefined ? {} : { seed: BigInt(wire.seed) }),
        ...(presentationDefaults === undefined ? {} : { presentationDefaults }),
        ...(words.label === undefined ? {} : { label: words.label }),
        ...(words.commandLabel === undefined ? {} : { commandLabel: words.commandLabel }),
        // A document is raised only to a listener: the Sheet writes none otherwise.
        ...(hears.has('workbookChanged') || hears.has('documentChanged') ? { onWorkbookChanged: heard.onWorkbookChanged } : {}),
        ...(hears.has('sheetsChanged') ? { onSheetsChanged: heard.onSheetsChanged } : {}),
        // Unheard, Ctrl+F1 is not claimed and stays the browser's (ADR-0100).
        ...(hears.has('showToolbarChanged') ? { onShowToolbarChanged: heard.onShowToolbarChanged } : {}),
        ...(hears.has('summaryFiguresChanged') ? { onSummaryFiguresChanged: heard.onSummaryFiguresChanged } : {}),
        ...(hears.has('selectionChanged') ? { onSelectionChanged: heard.onSelectionChanged } : {}),
        ...(hears.has('selectionSummaryChanged') ? { onSelectionSummaryChanged: heard.onSelectionSummaryChanged } : {}),
        ...(hears.has('formatKeyRefused') ? { onFormatKeyRefused: heard.onFormatKeyRefused } : {}),
        ...(pointingScope === undefined ? {} : { pointingScope }),
        ...(chrome === undefined ? {} : { chrome }),
        ...(toolbar === undefined ? {} : { toolbar }),
        ...(hears.has('pointingRefused') ? { onPointingRefused: heard.onPointingRefused } : {}),
        onEditingChanged: heard.onEditingChanged,
    };
}
