// ExPivot's Script Core (ADR-0370 section 1): Excel's PivotTable, drawn by ExGrid as that grid's Consumer
// (ADR-0059). It holds the Pivot Layout and asks its report source; ExGrid paints, selects, navigates and
// copies. It owns the DOM inside its own root, writes the Razor component's DOM contract and reads ExPivot's
// stylesheet unchanged (ADR-0350 section 4), and depends on no UI framework.
//
// Every move is a question, and only the newest question's answer is shown: a move made while a question is
// out is taken against the layout that question asks for, and when that question is refused every move taken
// against it is refused with it, and the pane goes back to the layout the report shows (ADR-0370 section 3,
// spine rule 6). Nothing waits on a clock.

import {
    gridTextGeometry, type GridColumnWidthChange, type GridDensity, type GridHandle, type GridOptions, type GridSelection, type GridTextGeometry,
    type CellPosition,
    type FetchingGridSource, type ViewportSize,
} from '@ex-grid/core';
import {
    FetchingPivotSource, LocalPivotReportSource, chooseLayout, defaultMaxLeaves, emptyPivotLayout, formatPivotCount, isLayoutChosen, isValuesPseudoField,
    layoutChoiceChanges, pivotLayoutChoices, pivotValueCaptions, placementOf, placementsIn, isItemCollapsed, pivotWordIdList, setCollapsed,
    validateLayout, type PivotDisplayRow, type PivotEntry, type PivotItemCounts, type PivotLabel, type PivotLayout, type PivotLayoutChoice,
    type PivotReportRefusal, type PivotReportSettings, type PivotReportState, type PivotSlicing, type PivotSource, type PivotToggle,
} from '@ex-grid/pivot-engine';
import { entryMenuCommands, entryOfField, type EntryMenuCommand, type EntryPanel } from './entry-menu';
import { FieldListView, fieldOf, sameEntry, type FieldListEdit, type FieldListMove } from './field-list';
import { ItemFilterPanel } from './item-filter';
import { ItemListings, pendingFirst, searchItems, type FieldItems, type ItemsUnder } from './items';
import { PivotMenuFrame, type PivotMenuCommand } from './menu';
import { defined, elementAt, unreachable } from './must';
import { fieldSettingsPanel, valueFieldSettingsPanel, type PivotPanel } from './panels';
import { cellItems, ReportColumns, reportRows, type PivotCellItems } from './report-grid';
import { layoutChoiceId, PivotScreenWords } from './words';

/** A Pivot Layout the user made and the report now shows. */
export interface PivotLayoutChangedEvent {
    /** The layout the report shows. */
    readonly layout: PivotLayout;
}

/** The Field List shown or hidden by the user. */
export interface PivotFieldListShownEvent {
    /** Whether it now stands beside the report. */
    readonly shown: boolean;
}

/** A move the report could not take, said where the user sees it (ADR-0066, ADR-0370 section 3). */
export interface PivotRefusedEvent {
    /** The sentence the pivot shows. */
    readonly message: string;
    /** The fields the refusal names, in the order they were moved: every move taken against the refused
     * question, which went with it. Empty where it names none. */
    readonly fields: readonly string[];
}

/** The report's selection, as the grid raised it, and what its cells stand for. */
export interface PivotSelectionChangedEvent {
    /** The selection of the report grid. */
    readonly selection: GridSelection;
    /** Each selected cell's row and column Items, each cell once, in the order selected (ADR-0340); `undefined`
     * when the selection reaches a row the grid does not hold drawn now, which is read from no Items rather than
     * from some of them (spine rule 1), and when no report is shown. A selection dropped over a report is said with
     * an empty list. */
    readonly items: readonly PivotCellItems[] | undefined;
}

/** An ExPivot's options (ADR-0370 section 1): what the Consumer hands, whole, on every update. */
export interface PivotViewOptions {
    /** The Pivot Source the report is computed from: its fields are what the Field List offers. A new
     * instance is a refresh. */
    readonly source: PivotSource;
    /** The Pivot Layout, View State the Consumer holds (ADR-0059). A layout handed in replaces the one on
     * screen; one this pivot raised through `onLayoutChanged` and handed back, however late, is its own and asks
     * nothing, so an earlier layout is asked for again by handing in one of the Consumer's own making. Left out,
     * the pivot starts from nothing and keeps the user's changes itself. A layout naming a field the source
     * does not offer is refused by name. */
    readonly layout?: PivotLayout;
    /** Raised with the layout the report shows after each change the user made, once it is answered: never
     * for one a newer move superseded, nor for one refused. */
    readonly onLayoutChanged?: (event: PivotLayoutChangedEvent) => void;
    /** Raised when a move is refused, with the sentence the pivot shows. */
    readonly onRefused?: (event: PivotRefusedEvent) => void;
    /** The locale numbers, dates and counts are written in and text Items ordered by: a BCP 47 tag, or
     * `invariant`, which the pivot reads unless given (ADR-0370 section 10). A React adapter passes the
     * browser's language. It changes no word. */
    readonly locale?: string;
    /** The Consumer's word for an id, or `undefined` to keep the English (ADR-0370 section 10).
     * `japanesePivotWords` gives Excel's Japanese edition's words in one line. */
    readonly label?: PivotLabel;
    /** Whether the Field List stands beside the report; true unless given. */
    readonly showFieldList?: boolean;
    /** Raised when the report's selection changes, with what its cells stand for: what a Board reads its links
     * by (ADR-0340). */
    readonly onSelectionChanged?: (event: PivotSelectionChangedEvent) => void;
    /** Raised when the user shows or hides the Field List. */
    readonly onShowFieldListChanged?: (event: PivotFieldListShownEvent) => void;
    /** The Field List's width in pixels; 300 unless given. */
    readonly fieldListWidth?: number;
    /** The report grid's height, header included: pixels, or `stretch` for the parent's; 400 unless given. */
    readonly viewportHeight?: ViewportSize;
    /** The report grid's width; `stretch` unless given, what the Field List leaves. */
    readonly viewportWidth?: ViewportSize;
    /** The most leaves a question may ask the source for (ADR-0066): 200,000 unless given. A layout past it is
     * refused by name, and the report stays on the layout before. */
    readonly maxLeaves?: number;
    /** The report's row height, or the Density's. */
    readonly rowHeight?: number;
    /** The report's Density. */
    readonly density?: GridDensity;
    /** How the report's work shares the thread: a slice of 30 ms between macrotasks unless given (PV-40). Read
     * once, when the pivot is made. */
    readonly slicing?: PivotSlicing;
}

/** Draws a grid into an element: ExGrid's `createGrid`, or a layer-2 test's own. */
export type MountGrid = (element: HTMLElement, options: GridOptions<PivotDisplayRow>) => GridHandle<PivotDisplayRow>;

/** Why a question was asked: a move of the user's, a layout the Consumer handed in, or new data. */
type Cause = 'user' | 'consumer' | 'data';

interface Question {
    readonly layout: PivotLayout;
    readonly generation: number;
    readonly controller: AbortController;
    /** Whether the layout is the user's, raised once shown: a move of the user's, or one asked again before its
     * answer came, under new settings or data, as the Razor component carries its pending raise. */
    readonly raise: boolean;
    /** Whether the answer marks what changed: new data, or the user's layout asked again under new data. */
    readonly markChanges: boolean;
    /** The user's moves this question carries: its own, after those of the questions it superseded while they
     * were out, which were taken against them and go with it (ADR-0370 section 3). */
    readonly moves: readonly FieldListMove[];
    /** What it is asked of and under: the rows of its answer are asked of the same, with the same. */
    readonly reportSource: LocalPivotReportSource;
    readonly settings: PivotReportSettings;
    /** The locale its settings carry, which its counts and its report are written in. */
    readonly locale: string;
    readonly maxLeaves: number;
}

interface Shown {
    readonly layout: PivotLayout;
    /** The answer the report was drawn from; none for a layout with nothing to show. */
    readonly answer: PivotReportState | undefined;
    /** The question it answered, whose locale the grid writes the report in. */
    readonly question: Question | undefined;
}

/** What a menu or a panel opened under a field's entry was opened on. */
interface AtEntry {
    readonly entry: PivotEntry;
    /** The field the entry stands for; none for Σ Values. */
    readonly field: string | undefined;
    /** The pane's layout it was opened on, which it closes on leaving. */
    readonly against: PivotLayout;
}

/** What is open in the pivot (ADR-0061), one at a time: the Layout menu from the Pivot Toolbar, a placed field's menu
 * in the Field List, or a panel that menu opened in its place. */
type OpenSurface =
    | { readonly kind: 'layout'; readonly popup: PivotMenuFrame }
    | (AtEntry & { readonly kind: 'entry'; readonly popup: PivotMenuFrame })
    | (AtEntry & { readonly kind: 'panel'; readonly popup: PivotPanel });

type EntrySurface = Exclude<OpenSurface, { readonly kind: 'layout' }>;

// The Layout menu's groups, each headed where it starts, in Excel's order (ADR-0061).
const layoutHeadings: ReadonlyMap<PivotLayoutChoice, string> = new Map([
    ['doNotShowSubtotals', 'subtotals'],
    ['grandTotalsOff', 'grand-totals'],
    ['compactForm', 'report-layout'],
]);

// The rows a first answer brings: a screen's worth and more. The grid asks for the rest as it scrolls.
const firstWindowRows = 200;

// Whether a layout shows nothing: no field in Rows, Columns or Values, so there is nothing to compute, whatever
// stands in Filters (ADR-0059).
function showsNothing(layout: PivotLayout): boolean {
    return layout.rows.length === 0 && layout.columns.length === 0 && layout.values.length === 0;
}

function checkedOptions(options: PivotViewOptions): PivotViewOptions {
    // Checked as an untyped caller may hand them, where they arrive (CONVENTIONS.md).
    const given: Readonly<Record<string, unknown>> = { ...options };
    const source = given['source'];
    if (typeof source !== 'object' || source === null || !('fields' in source) || !('aggregate' in source)) {
        throw new TypeError('An ExPivot needs a Pivot Source (source) to compute its report from.');
    }
    const width = given['fieldListWidth'];
    if (width !== undefined && (typeof width !== 'number' || !Number.isFinite(width) || width <= 0 || width >= 10_000)) {
        throw new RangeError(`fieldListWidth is a width in pixels above zero, not ${JSON.stringify(width)}.`);
    }
    const maxLeaves = given['maxLeaves'];
    if (maxLeaves !== undefined && (typeof maxLeaves !== 'number' || !Number.isSafeInteger(maxLeaves) || maxLeaves < 1)) {
        throw new RangeError(`maxLeaves is a whole number of at least 1, not ${JSON.stringify(maxLeaves)} (ADR-0066).`);
    }
    const slicing = given['slicing'];
    if (slicing !== undefined && (typeof slicing !== 'object' || slicing === null)) throw new TypeError('slicing is an object.');
    for (const name of ['onLayoutChanged', 'onRefused', 'onShowFieldListChanged', 'label']) {
        const handler = given[name];
        if (handler !== undefined && typeof handler !== 'function') throw new TypeError(`${name} is a function.`);
    }
    const locale = given['locale'];
    if (locale !== undefined && (typeof locale !== 'string' || locale === '')) {
        throw new TypeError('locale is a BCP 47 tag or "invariant", never an empty tag (ADR-0370 section 10).');
    }
    return options;
}

// The Consumer's words as the report source takes them, plain data that crosses to a Worker as it is.
function wordsRecord(label: PivotLabel | undefined): Readonly<Record<string, string>> | undefined {
    if (label === undefined) return undefined;
    const words: Record<string, string> = {};
    for (const id of pivotWordIdList()) {
        const word = label(id);
        if (word !== undefined) words[id] = word;
    }
    return Object.freeze(words);
}

/** One ExPivot, drawn into its root. */
export class PivotView {
    readonly #root: HTMLElement;
    readonly #mountGrid: MountGrid;
    readonly #report: HTMLElement;
    readonly #notice: HTMLElement;
    readonly #fieldListToggle: HTMLButtonElement;
    readonly #layoutAnchor: HTMLElement;
    readonly #layoutButton: HTMLButtonElement;
    readonly #layoutLabel: HTMLElement;
    readonly #backdrop: HTMLElement;
    readonly #sheet: HTMLElement;
    readonly #empty: HTMLElement;
    readonly #gridRoot: HTMLElement;
    readonly #dropRemove: HTMLElement;
    readonly #fieldListBox: HTMLElement;
    readonly #fieldList: FieldListView;
    readonly #userWidths = new Map<string, number>();
    // Read once, when the pivot is made: every report source it makes shares the thread the same way.
    readonly #slicing: PivotSlicing | undefined;
    #options: PivotViewOptions;
    #words: PivotScreenWords;
    // The text geometry of the report grid's density: what its label widths are estimated under, and a level's indent.
    #geometry: GridTextGeometry;
    #columns: ReportColumns;
    #settings: PivotReportSettings;
    #reportSource: LocalPivotReportSource;
    // The report source the rows on screen read their Windows from: the one before a new source's answer lands.
    #shownReportSource: LocalPivotReportSource | undefined;
    // Every layout raised through onLayoutChanged, as the Razor component holds them: one handed back is the
    // Consumer binding the pivot's own layout back, never a layout it asks for, however late it comes. An earlier
    // layout is asked for again by handing in a layout of the Consumer's own making. Held weakly, so the user's
    // moves cost nothing once nobody holds them.
    readonly #raised = new WeakSet<PivotLayout>();
    // Unhooks the listeners on the root, which is the Consumer's element and outlives the pivot.
    readonly #listening = new AbortController();
    #layoutGiven: PivotLayout | undefined;
    #shown: Shown;
    #asked: Question | undefined;
    #generation = 0;
    #requests = 0;
    #refusal: string | undefined;
    #fieldListShown: boolean;
    #counts: PivotItemCounts | undefined;
    #countsAsked: AbortController | undefined;
    #grid: GridHandle<PivotDisplayRow> | undefined;
    #rows: FetchingGridSource<PivotDisplayRow> | undefined;
    #dropRemoveShown = false;
    #open: OpenSurface | undefined;
    // The Items Filter… lists, asked of the source under the report on screen (ADR-0066).
    readonly #items = new ItemListings(() => { this.#draw(); });
    // A field menu the pane left while it held the keyboard: the keyboard goes to its field's entry once drawn.
    #keyboardAfterDraw: EntrySurface | undefined;
    #destroyed = false;

    constructor(root: HTMLElement, options: PivotViewOptions, mountGrid: MountGrid) {
        this.#options = checkedOptions(options);
        this.#root = root;
        this.#mountGrid = mountGrid;
        this.#slicing = options.slicing;
        this.#words = new PivotScreenWords(options.label);
        this.#geometry = gridTextGeometry(options.density === undefined ? {} : { density: options.density });
        this.#columns = this.#columnsOf(options);
        this.#settings = this.#settingsOf(options);
        this.#fieldListShown = options.showFieldList !== false;
        this.#layoutGiven = options.layout;
        if (options.layout !== undefined) validateLayout(options.layout, this.#declared(options.source));
        this.#shown = { layout: emptyPivotLayout, answer: undefined, question: undefined };
        this.#reportSource = this.#reportSourceOf(options.source);

        const document = root.ownerDocument;
        const div = (className: string): HTMLDivElement => {
            const made = document.createElement('div');
            made.className = className;
            return made;
        };
        root.replaceChildren();
        root.classList.add('ex-pivot');
        // The pivot's box is its Input Root (ADR-0240 section 6): the report grid hears the whole pivot from here.
        root.setAttribute('data-ex-input-root', '');
        this.#report = div('ex-pivot-report');
        const toolbar = div('ex-pivot-toolbar');
        const start = div('ex-pivot-toolbar-start');
        const end = div('ex-pivot-toolbar-end');
        this.#fieldListToggle = document.createElement('button');
        this.#fieldListToggle.type = 'button';
        this.#fieldListToggle.className = 'ex-pivot-toolbar-button ex-pivot-field-list-toggle';
        this.#fieldListToggle.setAttribute('data-ex-claims', 'click');
        this.#fieldListToggle.addEventListener('click', () => { this.#setFieldListShown(!this.#fieldListShown); });
        // Layout ▾ opens the Layout menu under it, over the report, at its right edge; its wrapper is positioned
        // so the menu needs no position the pivot would have to measure (ADR-0061).
        this.#layoutAnchor = document.createElement('span');
        this.#layoutAnchor.className = 'ex-pivot-toolbar-anchor';
        this.#layoutButton = document.createElement('button');
        this.#layoutButton.type = 'button';
        this.#layoutButton.className = 'ex-pivot-toolbar-button ex-pivot-layout-button';
        this.#layoutButton.setAttribute('data-ex-claims', 'click');
        this.#layoutButton.setAttribute('aria-haspopup', 'menu');
        this.#layoutLabel = document.createElement('span');
        const arrow = document.createElement('span');
        arrow.className = 'ex-pivot-entry-arrow';
        arrow.setAttribute('aria-hidden', 'true');
        arrow.textContent = '▾';
        this.#layoutButton.append(this.#layoutLabel, arrow);
        this.#layoutButton.addEventListener('click', () => { this.#toggleLayoutMenu(); });
        this.#layoutAnchor.append(this.#layoutButton);
        end.append(this.#layoutAnchor, this.#fieldListToggle);
        // Under a menu opened from the Pivot Toolbar, a press anywhere else closes it.
        this.#backdrop = div('ex-pivot-backdrop');
        this.#backdrop.setAttribute('data-ex-claims', 'click');
        this.#backdrop.setAttribute('aria-hidden', 'true');
        this.#backdrop.addEventListener('click', () => { this.#close('opener'); });
        toolbar.append(start, end);
        this.#notice = div('ex-pivot-refusal-notice');
        this.#notice.setAttribute('role', 'alert');
        this.#sheet = div('ex-pivot-sheet');
        this.#sheet.addEventListener('dragover', event => {
            if (!this.#fieldList.removable) return;
            event.preventDefault();
        });
        this.#sheet.addEventListener('drop', event => {
            event.preventDefault();
            this.#fieldList.dropOnReport();
        });
        this.#empty = div('ex-pivot-empty');
        this.#empty.setAttribute('role', 'status');
        this.#gridRoot = document.createElement('div');
        this.#dropRemove = div('ex-pivot-report-drop-remove');
        this.#dropRemove.setAttribute('aria-hidden', 'true');
        this.#dropRemove.append(document.createElement('span'));
        this.#report.append(toolbar, this.#notice, this.#sheet);
        this.#fieldListBox = div('ex-pivot-field-list');
        this.#fieldListBox.setAttribute('role', 'region');
        const scroll = div('ex-pivot-field-list-scroll');
        this.#fieldListBox.append(scroll);
        this.#fieldList = new FieldListView(scroll, {
            edit: edit => { this.#edited(edit); },
            close: () => { this.#setFieldListShown(false); },
            dragging: removable => { this.#paintDropRemove(removable); },
            menu: entry => { this.#toggleEntryMenu(entry); },
        });
        root.append(this.#report, this.#fieldListBox);
        // A drag that moves on from an Area, anywhere in the pivot, leaves it unmarked; and one that leaves the
        // pivot for an element the browser names. WebKit names none on leaving one element for another, so a
        // leave that names nothing is not taken as one.
        const listening = { signal: this.#listening.signal };
        root.addEventListener('dragenter', event => { this.#fieldList.dragEntered(event.target); }, listening);
        root.addEventListener('dragleave', event => {
            const to = event.relatedTarget;
            if (to instanceof Node && !root.contains(to)) this.#fieldList.leaveTargets();
        }, listening);
        // A press, or focus coming in, anywhere in the pivot outside an open field menu and its entry dismisses the
        // menu, and leaves the keyboard where it went (ADR-0061). A panel keeps its draft until OK or Cancel.
        const outside = (event: Event): void => {
            const open = this.#open;
            if (open?.kind !== 'entry') return;
            const item = open.popup.element.parentNode;
            if (event.target instanceof Node && item !== null && item.contains(event.target)) return;
            this.#close('stay');
        };
        root.addEventListener('mousedown', outside, listening);
        root.addEventListener('focusin', outside, listening);

        this.#askCounts();
        if (options.layout !== undefined) this.#ask(options.layout, 'consumer', undefined);
        this.#draw();
    }

    #reportSourceOf(source: PivotSource): LocalPivotReportSource {
        const slicing = this.#slicing;
        return new LocalPivotReportSource(source, slicing === undefined ? undefined : { slicing });
    }

    // A report source is disposed once nothing asks it and no row on screen reads it.
    #release(reportSource: LocalPivotReportSource | undefined): void {
        if (reportSource !== undefined && reportSource !== this.#reportSource && reportSource !== this.#shownReportSource) reportSource.dispose();
    }

    /** Takes the options again, whole. Options refused leave the pivot as it was. */
    update(options: PivotViewOptions): void {
        if (this.#destroyed) throw new Error('This ExPivot was destroyed: make a new one.');
        checkedOptions(options);
        const before = this.#options;
        const sourceMoved = options.source !== before.source;
        // A layout handed in is a question, unless the pane holds it already or this pivot raised it.
        const layoutMoved = options.layout !== this.#layoutGiven;
        const consumerLayout = layoutMoved && options.layout !== undefined && options.layout !== this.#paneLayout && !this.#raised.has(options.layout)
            ? options.layout
            : undefined;
        const asked = consumerLayout ?? (sourceMoved ? this.#paneLayout : undefined);
        // Checked against the source it will be asked of before anything changes.
        if (asked !== undefined) validateLayout(asked, this.#declared(options.source));
        this.#options = options;
        if (layoutMoved) this.#layoutGiven = options.layout;
        const densityMoved = options.density !== before.density;
        const settingsMoved = options.locale !== before.locale || options.label !== before.label || densityMoved;
        if (options.label !== before.label) this.#words = new PivotScreenWords(options.label);
        if (densityMoved) this.#geometry = gridTextGeometry(options.density === undefined ? {} : { density: options.density });
        if (options.label !== before.label || densityMoved) this.#columns = this.#columnsOf(options);
        if (settingsMoved) this.#settings = this.#settingsOf(options);
        if (options.showFieldList !== before.showFieldList) this.#fieldListShown = options.showFieldList !== false;
        if (sourceMoved) {
            // The rows on screen keep reading the report source that answered them until a new answer replaces them.
            const replaced = this.#reportSource;
            this.#reportSource = this.#reportSourceOf(options.source);
            this.#release(replaced);
            this.#counts = undefined;
            this.#askCounts();
        }
        if (asked !== undefined) {
            this.#ask(asked, consumerLayout !== undefined ? 'consumer' : 'data', undefined);
        } else if (settingsMoved || options.maxLeaves !== before.maxLeaves) {
            this.#ask(this.#paneLayout, 'consumer', undefined);
        }
        if (options.rowHeight !== before.rowHeight || options.density !== before.density || options.viewportHeight !== before.viewportHeight
            || options.viewportWidth !== before.viewportWidth) {
            this.#drawGrid();
        }
        this.#draw();
    }

    /** Stops the pivot: it asks and hears nothing more, and empties its element. The source stays the
     * Consumer's. */
    destroy(): void {
        if (this.#destroyed) return;
        this.#destroyed = true;
        this.#listening.abort();
        this.#takeDown();
        this.#items.dispose();
        this.#generation++;
        this.#asked?.controller.abort();
        this.#asked = undefined;
        this.#countsAsked?.abort();
        this.#grid?.destroy();
        this.#grid = undefined;
        this.#rows?.dispose();
        this.#rows = undefined;
        const asked = this.#reportSource;
        const read = this.#shownReportSource;
        this.#shownReportSource = undefined;
        asked.dispose();
        if (read !== asked) read?.dispose();
        this.#root.replaceChildren();
        this.#root.classList.remove('ex-pivot');
        this.#root.removeAttribute('data-ex-input-root');
        this.#root.removeAttribute('style');
        this.#root.removeAttribute('aria-busy');
    }

    /** The layout the report on screen was laid out under. */
    get shownLayout(): PivotLayout {
        return this.#shown.layout;
    }

    // Whether the report on screen is drawn as a grid: one was answered, and its layout shows something. A layout with
    // fields in Filters alone is answered, for their Items, and the box says how to start.
    get #reportDrawn(): boolean {
        return this.#shown.answer !== undefined && !showsNothing(this.#shown.layout);
    }

    // The pane's layout: the one the newest question asks for, or the one the report shows.
    get #paneLayout(): PivotLayout {
        return this.#asked?.layout ?? this.#shown.layout;
    }

    get #maxLeaves(): number {
        return this.#options.maxLeaves ?? defaultMaxLeaves;
    }

    get #locale(): string {
        return this.#localeOf(this.#options);
    }

    // The report source estimates the label widths under the grid's text geometry, as the Razor component tells it
    // the grid's Cell Metrics (ADR-0151).
    #settingsOf(options: PivotViewOptions): PivotReportSettings {
        const words = wordsRecord(options.label);
        return Object.freeze({ locale: this.#localeOf(options), labelMetrics: this.#geometry, ...(words === undefined ? {} : { words }) });
    }

    // A level's indent and a button's side: the em of the grid's font, rounded as .NET rounds it, which is what the
    // report source charges a level (ADR-0151).
    get #indentPx(): number {
        return roundHalfToEven(this.#geometry.fullWidthPx);
    }

    #columnsOf(options: PivotViewOptions): ReportColumns {
        const words = this.#words;
        return new ReportColumns(Object.freeze({
            indentPx: this.#indentPx,
            toggleName: (toggle: PivotToggle) => words.fill(toggle.isCollapsed ? 'expand-item' : 'collapse-item', toggle.itemLabel),
            toggled: (toggle: PivotToggle) => { this.#toggled(toggle); },
        }), options.density);
    }

    #localeOf(options: PivotViewOptions): string {
        return options.locale ?? 'invariant'; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0370 section 10: a Script Core given no locale reads as .NET's InvariantCulture does
    }

    #declared(source: PivotSource): ReadonlyMap<string, unknown> {
        return new Map(source.fields.map(field => [field.name, field]));
    }

    // ---- Asking ----------------------------------------------------------------------------------------

    #edited(edit: FieldListEdit): void {
        // A gesture taken against a layout that is no longer the pane's means nothing now: its indexes named the
        // entries of a layout that was replaced (ADR-0061, spine rule 6).
        if (edit.against !== this.#paneLayout) {
            this.#draw();
            return;
        }
        this.#move(edit.layout, edit.moved);
    }

    #move(layout: PivotLayout, moved: FieldListMove | undefined): void {
        this.#refusal = undefined;
        this.#ask(layout, 'user', moved);
        this.#draw();
    }

    #ask(layout: PivotLayout, asked: Cause, moved: FieldListMove | undefined): void {
        const superseded = this.#asked;
        superseded?.controller.abort();
        // A layout asked again before its answer came keeps what that question was owed: the user's raises once
        // shown and names its moves if refused, and new data's changes are marked.
        const again = superseded?.layout === layout;
        const raise = asked === 'user' || (again && superseded.raise);
        const markChanges = asked === 'data' || (again && superseded.markChanges);
        // A user's move made while a user's question was out was taken against it, and carries its moves on.
        const moves = [...(raise && superseded?.raise === true ? superseded.moves : []), ...(moved === undefined ? [] : [moved])];
        const generation = ++this.#generation;
        if (showsNothing(layout) && layout.filters.length === 0) {
            // Nothing to compute: the report is the empty box that says how to start. A layout with fields in Filters
            // alone is asked for still, as the Razor component asks every layout: Filter… lists their Items under its
            // Report Version (ADR-0066).
            this.#asked = undefined;
            this.#showLayout(layout, undefined, raise);
            return;
        }
        const question: Question = {
            layout, generation, controller: new AbortController(), raise, markChanges, moves: Object.freeze(moves),
            reportSource: this.#reportSource, settings: this.#settings, locale: this.#locale, maxLeaves: this.#maxLeaves,
        };
        this.#asked = question;
        void this.#run(question);
    }

    async #run(question: Question): Promise<void> {
        const current = (): boolean => question.generation === this.#generation && !this.#destroyed;
        let update;
        try {
            update = await question.reportSource.window(Object.freeze({
                requestId: this.#nextRequestId(),
                layout: question.layout,
                settings: question.settings,
                window: Object.freeze({ start: 0, count: firstWindowRows }),
                maxLeaves: question.maxLeaves,
                markChanges: question.markChanges,
            }), question.controller.signal);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a superseded question's failure is nobody's; the newest one's is said where the user sees it and rethrown
            if (!current()) return;
            this.#asked = undefined;
            this.#refusal = this.#words.fill('source-failed', error instanceof Error ? error.message : String(error));
            this.#draw();
            throw error;
        }
        // Late and superseded: a newer question was asked, and this answers a layout no longer wanted.
        if (!current()) return;
        this.#asked = undefined;
        if (update.refusal !== undefined) {
            this.#refuse(update.refusal, question);
            return;
        }
        this.#showLayout(question.layout, { answer: update, question }, question.raise);
    }

    #nextRequestId(): string {
        this.#requests++;
        return `ex-pivot-${this.#requests}`;
    }

    #showLayout(layout: PivotLayout, answered: { readonly answer: PivotReportState; readonly question: Question } | undefined, raise: boolean): void {
        const answer = answered?.answer;
        this.#shown = { layout, answer, question: answered?.question };
        this.#showReport(answered);
        if (answer !== undefined && this.#counts !== undefined && this.#counts.sourceVersion !== answer.metadata.sourceVersion) {
            // Counted under another Source Version: asked again, so the marks follow the data (ADR-0370 section 3).
            this.#askCounts();
        }
        this.#draw();
        if (raise) {
            this.#raised.add(layout);
            this.#options.onLayoutChanged?.(Object.freeze({ layout }));
        }
    }

    // A refused question: the pane goes back to the layout the report shows, and the sentence says why. Every
    // move taken against the refused layout went with it, and is named (ADR-0370 section 3).
    #refuse(refusal: PivotReportRefusal, question: Question): void {
        const { message, fields } = this.#sentenceFor(refusal, question);
        this.#refusal = message;
        this.#draw();
        this.#options.onRefused?.(Object.freeze({ message, fields: Object.freeze(fields) }));
    }

    #sentenceFor(refusal: PivotReportRefusal, question: Question): { readonly message: string; readonly fields: readonly string[] } {
        // The moves the refused layout still holds: a field moved and then taken out again is not in it.
        // A field may stand in Values as well as in an axis, so a move is known by its field and its Area.
        const held = question.moves.filter((move, i, all) => all.findLastIndex(later => later.field === move.field && later.area === move.area) === i
            && (move.area === 'values' ? question.layout.values.some(value => value.field === move.field) : placementOf(question.layout, move.field)?.area === move.area));
        const named = [...new Set(held.map(move => move.field))];
        const source = refusal.sourceRefusal;
        if (source?.kind !== 'tooManyLeaves') return { message: refusal.message, fields: refusal.field === undefined ? named : [refusal.field] };
        // A source that names no limit stopped at the cap it was asked with (ADR-0066).
        const { locale } = question;
        const cap = formatPivotCount(source.limit ?? question.maxLeaves, locale);
        // Server data is refused naming each field moved into Rows or Columns, which make the combinations
        // (ADR-0370 section 3); data the pivot computes itself is refused by the cells the layout needs, as the
        // Razor component says it.
        const combining = held.filter(move => move.area === 'rows' || move.area === 'columns');
        if (!(this.#options.source instanceof FetchingPivotSource) || combining.length === 0) {
            return { message: this.#words.fill('refused-too-many-cells', cap), fields: named };
        }
        const sentences = combining.map(move => {
            const caption = this.#captionOf(move.field);
            const area = this.#words.area(move.area);
            const count = this.#counts?.countOf(move.field);
            const sentence = count === undefined
                ? this.#words.fill('refused-too-many-combinations', caption, area, cap)
                : this.#words.fill('refused-too-many-combinations-counted', caption, formatPivotCount(count, locale), area, cap);
            return `${sentence} ${this.#words.fill('refused-too-many-combinations-remedy', caption)}`;
        });
        return { message: sentences.join(' '), fields: named };
    }

    #captionOf(field: string): string {
        const found = this.#options.source.fields.find(f => f.name === field);
        if (found === undefined) throw new Error(`Internal: the field '${field}' is not the source's.`);
        return found.caption;
    }

    // Each field's count of Items, asked once when the source is opened and again under a new Source Version,
    // of a source that declares it counts them (ADR-0370 section 3). A source that does not gets no count.
    #askCounts(): void {
        this.#countsAsked?.abort();
        const source = this.#options.source;
        if (!source.features.countsItems || source.itemCounts === undefined) {
            this.#countsAsked = undefined;
            return;
        }
        const controller = new AbortController();
        this.#countsAsked = controller;
        void this.#countItems(source, controller);
    }

    async #countItems(source: PivotSource, controller: AbortController): Promise<void> {
        let counts;
        try {
            if (source.itemCounts === undefined) throw new Error('Internal: a source counted without counting Items.');
            counts = await source.itemCounts(controller.signal);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a count superseded is nobody's; one that failed leaves the fields uncounted, said by rethrowing
            if (controller.signal.aborted || this.#destroyed) return;
            this.#countsAsked = undefined;
            throw error;
        }
        if (controller.signal.aborted || this.#destroyed || source !== this.#options.source) return;
        this.#countsAsked = undefined;
        this.#counts = counts;
        this.#draw();
    }

    // ---- The report ------------------------------------------------------------------------------------

    #showReport(answered: { readonly answer: PivotReportState; readonly question: Question } | undefined): void {
        this.#rows?.dispose();
        this.#rows = undefined;
        // The rows of the report before read a source that may since have been replaced.
        const before = this.#shownReportSource;
        this.#shownReportSource = answered?.question.reportSource;
        this.#release(before);
        if (answered === undefined || !this.#reportDrawn) {
            this.#grid?.destroy();
            this.#grid = undefined;
            return;
        }
        const { answer, question } = answered;
        this.#rows = reportRows({
            source: question.reportSource,
            layout: question.layout,
            settings: question.settings,
            maxLeaves: question.maxLeaves,
            answered: answer,
            nextRequestId: () => this.#nextRequestId(),
            moved: () => { this.#reportMoved(answer); },
        });
        this.#drawGrid();
    }

    // The source answered a Window from another report than the one shown: the data moved on under it, and the
    // report on screen is asked again under its layout, unless something newer is asked already.
    #reportMoved(answer: PivotReportState): void {
        if (this.#destroyed || this.#shown.answer !== answer || this.#asked !== undefined) return;
        this.#ask(this.#shown.layout, 'data', undefined);
        this.#draw();
    }

    #drawGrid(): void {
        const answer = this.#shown.answer;
        const rows = this.#rows;
        const question = this.#shown.question;
        if (answer === undefined || rows === undefined || question === undefined) return;
        const { locale } = question;
        const { columns, headerGroups, pinnedColumnCount } = this.#columns.of(answer.metadata, this.#userWidths);
        const { rowHeight, density, label } = this.#options;
        const options: GridOptions<PivotDisplayRow> = {
            columns,
            headerGroups,
            pinnedColumnCount,
            source: rows,
            locale,
            viewportHeight: this.#options.viewportHeight ?? 400, // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0028: the report's declared default height, as the Razor component's
            viewportWidth: this.#options.viewportWidth ?? 'stretch', // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0028: the report takes what the Field List leaves unless told
            // Item order and Hidden Items are the Pivot Layout's, and a report is computed: no column menu, no
            // sort, no filter, and every write refused by the grid's own gate, since nobody hears one (ADR-0059).
            hideColumnMenu: true,
            onColumnWidthChanged: change => { this.#widthChanged(change); },
            onCellDoubleClick: cell => { this.#doubleClicked(cell); },
            onSelectionChanged: selection => { this.#selectionChanged(selection); },
            ...(label === undefined ? {} : { commandLabel: label }),
            ...(rowHeight === undefined ? {} : { rowHeight }),
            ...(density === undefined ? {} : { density }),
        };
        if (this.#grid === undefined) this.#grid = this.#mountGrid(this.#gridRoot, options);
        else this.#grid.update(options);
    }

    // A width the user dragged a column to is the Consumer's to hand back (ADR-0016): the pivot keeps it, by the
    // column's name, for every report that has the column.
    #widthChanged(change: GridColumnWidthChange): void {
        this.#userWidths.set(change.column, change.widthPx);
        this.#drawGrid();
    }

    // The grid's selection, said with what its cells stand for, read from the rows the grid holds when it is
    // raised: each report is a new source, so a selection the grid keeps is over the same report's rows, and one
    // over another report is dropped and raised again (ADR-0011). The Items are read when first asked for, from
    // what was held then, so a selection dragged over thousands of cells costs nothing until a Consumer reads it.
    #selectionChanged(selection: GridSelection): void {
        const raise = this.#options.onSelectionChanged;
        if (raise === undefined || this.#destroyed) return;
        const answer = this.#shown.answer;
        const state = this.#rows?.snapshot();
        const metadata = answer?.metadata;
        let read = false;
        let items: readonly PivotCellItems[] | undefined;
        raise(Object.freeze({
            selection,
            get items(): readonly PivotCellItems[] | undefined {
                if (!read) {
                    read = true;
                    if (metadata !== undefined && state !== undefined) items = cellItems(selection, metadata, state.window, state.windowStart);
                }
                return items;
            },
        }));
    }

    // A double click on an outer Item's label expands or collapses it, as Excel's does (ADR-0063).
    #doubleClicked(cell: CellPosition): void {
        const answer = this.#shown.answer;
        const rows = this.#rows;
        if (answer === undefined || rows === undefined || cell.column >= answer.metadata.labelColumns.length) return;
        const state = rows.snapshot();
        const row = state.window[cell.row - state.windowStart];
        if (row === undefined) return;
        const toggle = elementAt(row.labels, cell.column).toggle;
        if (toggle !== undefined) this.#toggled(toggle);
    }

    // An outer Item's button pressed, or its label double clicked: the Item is collapsed or expanded (ADR-0063).
    // The press does what the button it was taken on said, against the pane's layout, the one the newest question
    // asks for; where that layout no longer holds the Item as the button painted it, a press already moved it, or
    // its field left the pane, and the press is refused rather than turned into its opposite (spine rule 6). Two
    // presses of one painted button collapse once, as the Razor component's do.
    #toggled(toggle: PivotToggle): void {
        if (this.#destroyed) return;
        const pane = this.#paneLayout;
        const placed = pane.rows.find(placement => placement.field === toggle.field);
        if (placed === undefined || isItemCollapsed(placed, toggle.item) !== toggle.isCollapsed) return;
        this.#move(setCollapsed(pane, toggle.field, toggle.item, !toggle.isCollapsed), undefined);
    }

    // ---- Menus and panels (ADR-0061) -------------------------------------------------------------------

    #toggleLayoutMenu(): void {
        if (this.#open?.kind === 'layout') {
            this.#close('opener');
            return;
        }
        this.#openSurface({ kind: 'layout', popup: this.#menuFrame('toolbar-end') });
    }

    // An entry's button opens its menu, and closes the menu standing under it.
    #toggleEntryMenu(entry: PivotEntry): void {
        // A panel open under the entry is dropped for the menu, as the Razor component's ToggleMenu does.
        const open = this.#open;
        if (open?.kind === 'entry' && sameEntry(open.entry, entry)) {
            this.#close('opener');
            return;
        }
        const pane = this.#paneLayout;
        const field = isValuesPseudoField(entry) ? undefined : fieldOf(pane, entry);
        this.#openSurface({ kind: 'entry', entry, field, against: pane, popup: this.#menuFrame('field-list') });
    }

    #menuFrame(place: 'field-list' | 'toolbar-end'): PivotMenuFrame {
        const frame = new PivotMenuFrame(this.#root.ownerDocument, place, () => { this.#close('opener'); });
        // The keyboard leaving a menu for another control — Tab past its last command — closes it, so the Layout
        // menu's backdrop takes no press meant for that control, and a field menu does not stand open over the pane
        // once the keyboard left the pivot. A field menu's entry, which toggles it, is inside its bounds.
        frame.element.addEventListener('focusout', event => {
            const to = event.relatedTarget;
            const open = this.#open;
            if (open?.popup !== frame || !(to instanceof Node)) return;
            const bounds = open.kind === 'entry' ? frame.element.parentNode : frame.element;
            if (bounds !== null && !bounds.contains(to)) this.#close('stay');
        });
        return frame;
    }

    // Opened, its first enabled command, or its panel's first control, takes the keyboard.
    #openSurface(open: OpenSurface): void {
        this.#takeDown();
        this.#open = open;
        this.#draw();
        open.popup.takeKeyboard();
    }

    // Closed by Escape, by its button, by the backdrop or by Cancel, the keyboard goes back to what opened it.
    // Dismissed by a press or focus elsewhere, it stays where that put it.
    #close(keyboard: 'opener' | 'stay'): void {
        const open = this.#takeDown();
        if (open === undefined) return;
        this.#draw();
        if (keyboard === 'opener') this.#returnKeyboard(open, open.kind === 'layout' ? undefined : this.#entryNow(open));
    }

    // Takes what is open off the page, without drawing.
    #takeDown(): OpenSurface | undefined {
        const open = this.#open;
        this.#open = undefined;
        if (open?.kind === 'panel') open.popup.close?.();
        open?.popup.element.remove();
        return open;
    }

    // The keyboard goes back to the button that opened the menu: Layout ▾, or the entry `entry` names, where the field
    // the menu was for stands now; a field taken out of the Areas hands it to its tick box in the list.
    #returnKeyboard(open: OpenSurface, entry: PivotEntry | undefined): void {
        if (open.kind === 'layout') this.#layoutButton.focus();
        else if (entry !== undefined) this.#fieldList.focusEntry(entry);
        else this.#fieldList.focusField(open.field);
    }

    // Where the field a menu was for stands in the pane's layout now.
    #entryNow(open: EntrySurface): PivotEntry | undefined {
        return entryOfField(this.#paneLayout, open.field, open.entry);
    }

    // Closes what is open, does what its command or its OK says, and hands the keyboard back once the pivot is drawn
    // after it. The popup leaves the page once the keyboard has gone back, so the Layout menu's never passes it
    // through the page's body (ADR-0061). Pressed once what it stood in has closed, it does nothing.
    #finish(open: OpenSurface, run: () => PivotEntry | undefined): void {
        if (this.#open !== open) return;
        this.#open = undefined;
        if (open.kind === 'panel') open.popup.close?.();
        this.#returnKeyboard(open, run());
        open.popup.element.remove();
    }

    #command(open: OpenSurface, command: Omit<PivotMenuCommand, 'invoke'>, run: () => PivotEntry | undefined): PivotMenuCommand {
        return Object.freeze({ ...command, invoke: () => { this.#finish(open, run); } });
    }

    // The Layout menu (ADR-0061): Excel's Design tab choices, the current one of each group marked, and one that
    // would change nothing disabled, read from the pane's layout, which a choice is taken against.
    #layoutCommands(open: OpenSurface): readonly PivotMenuCommand[] {
        const pane = this.#paneLayout;
        const words = this.#words;
        return pivotLayoutChoices.map(choice => {
            const id = layoutChoiceId(choice);
            const heading = layoutHeadings.get(choice);
            return this.#command(open, {
                id,
                label: words.word(id),
                enabled: layoutChoiceChanges(pane, choice),
                checked: isLayoutChosen(pane, choice),
                ...(heading === undefined ? {} : { heading: words.word(heading) }),
            }, () => {
                this.#move(chooseLayout(pane, choice), undefined);
                return undefined;
            });
        });
    }

    #entryCaption(entry: PivotEntry, valueCaptions: readonly string[] | undefined): string {
        if (isValuesPseudoField(entry)) return this.#words.word('values-pseudo-field');
        if (entry.area === 'values') return elementAt(defined(valueCaptions, 'the Value Fields\' captions'), entry.index);
        return this.#captionOf(fieldOf(this.#paneLayout, entry));
    }

    // A field menu's command: a change of the layout, made as the menu closes, or a panel opened in its place.
    #entryCommand(open: EntrySurface, command: EntryMenuCommand): PivotMenuCommand {
        const { does, ...shown } = command;
        if ('edit' in does) {
            return this.#command(open, shown, () => {
                const made = does.edit();
                this.#edited(made);
                return made.entry;
            });
        }
        return Object.freeze({ ...shown, invoke: () => { this.#openPanel(open, does.panel); } });
    }

    #openPanel(open: EntrySurface, panel: EntryPanel): void {
        if (this.#open !== open) return;
        const document = this.#root.ownerDocument;
        const { entry, against } = open;
        // The panel's OK and Cancel are called once it stands, and act only while it does.
        const handlers = {
            // OK's layout is the pane's next, taken against the layout the panel was opened on (spine rule 6).
            apply: (layout: PivotLayout) => {
                this.#finish(surface, () => {
                    this.#edited({ layout, against, moved: undefined });
                    return this.#entryNow(open);
                });
            },
            cancel: () => { if (this.#open === surface) this.#close('opener'); },
        };
        const words = this.#words;
        const fields = this.#options.source.fields;
        const captions = pivotValueCaptions(against, fields, { locale: this.#locale, ...(words.label === undefined ? {} : { label: words.label }) });
        let popup: PivotPanel;
        switch (panel) {
            case 'filter-items': {
                const field = fieldOf(against, entry);
                // Opened again on Items that could not be listed, it asks for them again.
                this.#items.retry(field);
                popup = new ItemFilterPanel(document, { layout: against, field, caption: this.#captionOf(field), locale: this.#locale, words }, {
                    ...handlers,
                    items: () => this.#itemsOf(field),
                    // Asked from the panel's draw, it says a problem in a later turn, never in the middle of that draw.
                    search: (search, signal) => Promise.resolve().then(() => searchItems(field, search, defined(this.#itemsUnder(field), 'a report to search the Items of'), signal)),
                });
                break;
            }
            case 'field-settings':
                popup = fieldSettingsPanel(document, {
                    layout: against,
                    placement: elementAt(placementsIn(against, entry.area), entry.index),
                    caption: this.#entryCaption(entry, captions),
                    valueCaptions: captions,
                    words,
                }, handlers);
                break;
            case 'value-field-settings':
                popup = valueFieldSettingsPanel(document, {
                    layout: against,
                    index: entry.index,
                    fieldCaption: this.#captionOf(fieldOf(against, entry)),
                    caption: elementAt(captions, entry.index),
                    fields,
                    features: this.#options.source.features,
                    locale: this.#locale,
                    words,
                }, handlers);
                break;
            default:
                unreachable(panel);
        }
        const surface: EntrySurface = { kind: 'panel', entry, field: open.field, against, popup };
        this.#openSurface(surface);
    }

    // Each draw draws the open menu again from the pane's layout. A field's menu or panel goes when the pane leaves
    // the layout it was opened on, since its entry may now be another field's, and when the Field List is hidden; the
    // keyboard it held goes to the entry its field stands at now.
    #drawMenu(valueCaptions: readonly string[] | undefined): void {
        const open = this.#open;
        if (open !== undefined && open.kind !== 'layout' && (open.against !== this.#paneLayout || !this.#fieldListShown)) {
            const held = open.popup.element.contains(this.#root.ownerDocument.activeElement);
            this.#takeDown();
            // Hidden with the Field List, the keyboard goes to the toggle that shows it again.
            if (held && this.#fieldListShown) this.#keyboardAfterDraw = open;
            else if (held) this.#fieldListToggle.focus();
        }
        const shown = this.#open;
        this.#layoutLabel.textContent = this.#words.word('layout-menu');
        this.#layoutButton.setAttribute('aria-expanded', shown?.kind === 'layout' ? 'true' : 'false');
        if (shown?.kind === 'layout') {
            shown.popup.draw(this.#words.word('layout-menu'), this.#layoutCommands(shown));
            if (shown.popup.element.parentNode !== this.#layoutAnchor) this.#layoutAnchor.append(shown.popup.element);
            if (this.#backdrop.parentNode !== this.#root) this.#root.append(this.#backdrop);
        } else {
            this.#backdrop.remove();
        }
        if (shown?.kind === 'entry') {
            const { entry } = shown;
            const commands = entryMenuCommands({ layout: this.#paneLayout, fields: this.#options.source.fields, words: this.#words }, entry);
            shown.popup.draw(this.#words.fill('field-menu', this.#entryCaption(entry, valueCaptions)),
                commands.map(command => this.#entryCommand(shown, command)));
        }
        if (shown?.kind === 'panel') shown.popup.redraw?.();
    }

    // What a field's Items are listed under: the report on screen, and the field's order in it. None while no report
    // is shown.
    #itemsUnder(field: string): ItemsUnder | undefined {
        const { answer, question, layout } = this.#shown;
        if (answer === undefined || question === undefined) return undefined;
        const at = placementOf(layout, field);
        return Object.freeze({
            reportSource: question.reportSource,
            version: answer.metadata.version,
            sourceVersion: answer.metadata.sourceVersion,
            settings: question.settings,
            sort: at === undefined ? undefined : elementAt(placementsIn(layout, at.area), at.index).sort,
        });
    }

    // A field's Items as they stand for the report on screen; on their way while the first report is.
    #itemsOf(field: string): FieldItems {
        const under = this.#itemsUnder(field);
        return under === undefined ? pendingFirst : this.#items.of(field, under);
    }

    // ---- Drawing ---------------------------------------------------------------------------------------

    #setFieldListShown(shown: boolean): void {
        if (shown === this.#fieldListShown) return;
        this.#fieldListShown = shown;
        this.#draw();
        this.#options.onShowFieldListChanged?.(Object.freeze({ shown }));
    }

    #paintDropRemove(removable: boolean): void {
        // A drag takes the pane: a field menu open in it goes, as the Razor component's does, drawn below.
        this.#takeDown();
        this.#dropRemoveShown = removable;
        this.#draw();
    }

    #draw(): void {
        if (this.#destroyed) return;
        const words = this.#words;
        const layout = this.#paneLayout;
        const fields = this.#options.source.fields;
        // The Value Fields' captions, which the Field List and a Values entry's menu say.
        const valueCaptions = this.#fieldListShown
            ? pivotValueCaptions(layout, fields, { locale: this.#locale, ...(words.label === undefined ? {} : { label: words.label }) })
            : undefined;
        this.#drawMenu(valueCaptions);
        const stretch = this.#options.viewportHeight === 'stretch';
        const width = this.#options.fieldListWidth ?? 300; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0061: the Field List's declared default width, as the Razor component's
        // The Geometry Tokens, inline on the root (ADR-0027): the Field List's width and a level's indent.
        this.#root.setAttribute('style', `--ex-pivot-field-list-width: ${width}px; --ex-pivot-indent: ${this.#indentPx}px${stretch ? '; height: 100%; flex: 1 1 auto; min-height: 0' : ''}`);
        if (this.#asked === undefined) this.#root.removeAttribute('aria-busy');
        else this.#root.setAttribute('aria-busy', 'true');
        this.#fieldListToggle.textContent = words.word('field-list-toggle');
        this.#fieldListToggle.setAttribute('aria-pressed', this.#fieldListShown ? 'true' : 'false');
        if (this.#notice.textContent !== (this.#refusal ?? '')) this.#notice.textContent = this.#refusal ?? ''; // oxlint-disable-line ex-grid/no-stand-in-default -- no refusal is an empty notice, which takes no room
        this.#notice.hidden = this.#refusal === undefined;
        this.#sheet.setAttribute('style', stretch ? 'display: flex; flex-direction: column; flex: 1 1 auto; min-height: 0' : '');
        const answered = this.#reportDrawn;
        // The report or the box that says why there is none, and over the report, while a placed field is
        // dragged, the mark that a drop there removes it (ADR-0061). Elements come and go rather than hide, since
        // the stylesheet gives the box a display of its own.
        const dropRemove = answered && this.#dropRemoveShown;
        elementAt([...this.#dropRemove.children], 0).textContent = words.word('remove-field');
        const shownInSheet = [answered ? this.#gridRoot : this.#empty, ...(dropRemove ? [this.#dropRemove] : [])];
        if (shownInSheet.length !== this.#sheet.children.length || shownInSheet.some((child, i) => this.#sheet.children[i] !== child)) {
            this.#sheet.replaceChildren(...shownInSheet);
        }
        if (!answered) {
            const pane = this.#paneLayout;
            this.#empty.textContent = words.word(this.#asked !== undefined && !showsNothing(pane) ? 'loading' : 'empty-report');
            const height = this.#options.viewportHeight ?? 400; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0028: the report's declared default height, as the Razor component's
            this.#empty.setAttribute('style', height === 'stretch' ? 'flex: 1 1 auto; min-height: 0' : `height: ${height}px`);
        }
        this.#fieldListBox.hidden = !this.#fieldListShown;
        this.#fieldListBox.setAttribute('aria-label', words.word('field-list'));
        if (valueCaptions !== undefined) {
            this.#fieldList.draw({
                layout,
                fields,
                valueCaptions,
                counts: this.#counts,
                maxLeaves: this.#maxLeaves,
                locale: this.#locale,
                words,
                popup: this.#open !== undefined && this.#open.kind !== 'layout' ? { entry: this.#open.entry, frame: this.#open.popup.element } : undefined,
            });
        }
        const left = this.#keyboardAfterDraw;
        this.#keyboardAfterDraw = undefined;
        if (left !== undefined) this.#returnKeyboard(left, this.#entryNow(left));
    }
}

// .NET's Math.Round, which sends a tie to the even number (CONVENTIONS.md).
function roundHalfToEven(value: number): number {
    const floor = Math.floor(value);
    const diff = value - floor;
    if (diff < 0.5) return floor;
    if (diff > 0.5) return floor + 1;
    return floor % 2 === 0 ? floor : floor + 1;
}
