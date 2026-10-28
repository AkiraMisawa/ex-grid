// ExPivot's Script Core (ADR-0370 section 1): Excel's PivotTable, drawn by ExGrid as that grid's Consumer
// (ADR-0059). It holds the Pivot Layout and asks its report source; ExGrid paints, selects, navigates and
// copies. It owns the DOM inside its own root, writes the Razor component's DOM contract and reads ExPivot's
// stylesheet unchanged (ADR-0350 section 4), and depends on no UI framework.
//
// Every move is a question, and only the newest question's answer is shown: a move made while a question is
// out is taken against the layout that question asks for, and when that question is refused every move taken
// against it is refused with it, and the pane goes back to the layout the report shows (ADR-0370 section 3,
// spine rule 6). The one clock it waits on is the redraw interval (ADR-0067): the source's changes of data are
// gathered, and asked for at most once per interval, which decides when the newest data reaches the screen, never
// which data it is.

import {
    CellTextMetrics, GridPresentationDefaults, gridTextGeometry, type GridColumnWidthChange, type GridDensity, type GridHandle, type GridOptions,
    type GridSelection, type GridTextGeometry, type GridTextOptions,
    type CellPosition, type GridCommand, type GridContextMenuContext,
    type FetchingGridSource, type GridRefusal, type ViewportSize, type GridColumn, type GridCopyAnswer, type GridCopyRequest,
    type SelectionSummary, type SummaryFigure,
    assembleCopy, defaultChangeHighlightMs, defaultSummaryFigures, summaryFigureOrder, systemClock,
} from '@ex-grid/core';
import {
    FetchingPivotSource, LocalPivotReportSource, chooseLayout, defaultMaxLeaves, emptyPivotLayout, formatPivotCount, isLayoutChosen, isValuesPseudoField,
    layoutChoiceChanges, pivotLayoutChoices, pivotValueCaptions, placementOf, placementsIn, isItemCollapsed, pivotWord, pivotWordIdList, setCollapsed, setHiddenItems,
    itemKeysEqual, pivotItemKeyId, validateLayout, writePivotLayoutJson, type PivotDisplayRow, type PivotEntry, type PivotFieldPlacement, type PivotItemInfo, type PivotItemCounts, type PivotLabel, type PivotLayout, type PivotLayoutChoice,
    type PivotReportCopyCell, type PivotReportRefusal, type PivotReportSettings, type PivotSourceChanged, type PivotReportState, type PivotSlicing, type PivotSource, type PivotToggle,
    pivotReportDetailsQuery, PivotItemPage, itemsOfPage, PivotChangeTimes, type PivotFieldItem,
} from '@ex-grid/pivot-engine';
import { detailsEvent, detailsProblemText, type PivotDetailItem, type PivotDetailsView, type PivotShowDetailsEvent } from './details';
import {
    checkedPivotChrome, StandingSlot, type PivotChrome, type PivotChromeSlot, type PivotReportFiltersSlotContext, type PivotReportFiltersView, type PivotStaleReportSlotContext,
    type PivotStaleReportView, type PivotToolbarSlotContext, type PivotToolbarView,
} from './chrome';
import { DetailsSheets, type MountGrid } from './details-view';
import { entryMenuCommands, entryOfField, type EntryMenuCommand, type EntryPanel } from './entry-menu';
import { FieldListView, fieldOf, sameEntry, type FieldListEdit, type FieldListHandlers, type FieldListMove } from './field-list';
import { entryKey, SlottedFieldList } from './field-list-slot';
import { itemFilterPanel, itemsProblemText } from './item-filter';
import { ItemListings, pendingFirst, searchItems, type FieldItems, type ItemsUnder } from './items';
import { PivotMenuFrame, SlottedMenuFrame, type PivotMenu, type PivotMenuCommand } from './menu';
import { clearStyle, element, writeStyle } from './dom';
import { defined, elementAt, unreachable } from './must';
import { fieldSettingsPanel, valueFieldSettingsPanel, type PivotPanel } from './panels';
import { checkedName, PartElements, swap, type PartRefs } from './parts';
import { cellItems, ReportColumns, reportMatch, reportRows, type PivotCellItems } from './report-grid';
import type { ReportFindGivenUp } from './report-find';
import { captionOfRemove, removeNamedFieldId, reportMenuCommands, valueFieldAt } from './report-menu';
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

/** The Pivot Source failed to answer the report's newest question (ADR-0067). */
export interface PivotFailedEvent {
    /** What the source threw. */
    readonly error: unknown;
    /** Whether the report on screen stays as a Stale Report, whose notice says the failure: such a failure is raised here
     * and thrown no further. Any other is said in the Pivot Toolbar, and thrown on after it is raised. */
    readonly stale: boolean;
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
    /** Raised when the Pivot Source fails to answer the report's newest question, as the Razor component keeps
     * `LastError`: a live source whose transport is down is heard of here (ADR-0067). */
    readonly onFailed?: (event: PivotFailedEvent) => void;
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
    /** The most rows a report may have (ADR-0066): Excel's 1,048,576 unless given. A layout past it is refused, and
     * newer data past it leaves a Stale Report. */
    readonly maxRows?: number;
    /** The most columns a report may have, its label columns included (ADR-0066): Excel's 16,384 unless given. A layout
     * past it is refused, and newer data past it leaves a Stale Report. */
    readonly maxColumns?: number;
    /** How long after a change of data reached the screen the next is asked for, in milliseconds: 250 unless given
     * (ADR-0067). The source's notices that come meanwhile are gathered and asked for once, so a source that changes
     * many times a second redraws the report at most once per interval; a notice after a quiet spell is asked for at
     * once. */
    readonly redrawInterval?: number;
    /** The report's row height, or the Density's. */
    readonly rowHeight?: number;
    /** The report's Density, beating a Wrapper's. */
    readonly density?: GridDensity;
    /** The widths the report's grids charge each glyph, beating a Wrapper's and the Density's (ADR-0016): whoever sets
     * the font the cells are drawn in hands its widths, so `####` and the label widths are judged by the face on
     * screen. */
    readonly cellMetrics?: CellTextMetrics;
    /** What a Wrapper hands the report's grids (ADR-0030): the widths of the font it sets, its Density, and its hover
     * band and Row Stripes. A value the pivot is given beats these. */
    readonly presentationDefaults?: GridPresentationDefaults;
    /** How long the Change Highlight marks a value that changed with the data, in milliseconds: one second unless
     * given (ADR-0068). The source's changes are marked when newer data is shown, and a Refresh marks what changed
     * too; a move of the user's marks nothing. Zero marks nothing, and a negative or endless one is refused. */
    readonly changeHighlightDuration?: number;
    /** How the report's work shares the thread: a slice of 30 ms between macrotasks unless given (PV-40). Read
     * once, when the pivot is made. */
    readonly slicing?: PivotSlicing;
    /** Hears Show Details (ADR-0059, ADR-0063): the records behind a value cell double clicked. A Consumer that
     * listens takes them, and the pivot shows nothing; otherwise they open as `detailsView` says. */
    readonly onShowDetails?: (event: PivotShowDetailsEvent) => void;
    /** Where the records open when nobody hears `onShowDetails`: a tab at the report's foot, which covers the report
     * while it is selected, or a dialog; `tab` unless given. */
    readonly detailsView?: PivotDetailsView;
    /** Whether the Selection Summary is shown under the report (ADR-0130): Excel's status-bar figures over the cells
     * selected, summed by the report source over the whole report, rows not drawn included. On unless given. */
    readonly showSelectionSummary?: boolean;
    /** The Selection Summary's figures, Excel's Average, Count and Sum unless given (ADR-0130). The report holds the
     * choice made in the figures menu from then on; figures handed in that differ from the ones last handed replace
     * it. */
    readonly summaryFigures?: readonly SummaryFigure[];
    /** Raised when the figures menu changes the figures shown (ADR-0130). */
    readonly onSummaryFiguresChanged?: (figures: readonly SummaryFigure[]) => void;
    /** Hears the Selection Summary as it now stands (ADR-0130), for an application that shows the figures elsewhere. */
    readonly onSelectionSummaryChanged?: (summary: SelectionSummary) => void;
    /** The parts a Wrapper's Chrome, or the Consumer's own, draws in place of the built-in markup (ADR-0061, ADR-0390),
     * its `grid` dressing the report's grid and Show Details' grids. A part standing keeps the slot it opened with. */
    readonly chrome?: PivotChrome;
}

export type { MountGrid } from './details-view';

/** Why a question was asked: a move of the user's, a layout the Consumer handed in, newer data a person or the Consumer
 * asked for (Refresh, Retry, a new source), or newer data the source told of, which is asked quietly, without the loading
 * indication, so a live report does not flicker (ADR-0067). */
type Cause = 'user' | 'consumer' | 'data' | 'live';

/** Whether a question was asked for newer data: its answer marks what changed, and one it cannot show leaves a Stale
 * Report (ADR-0067). */
function asksData(cause: Cause): boolean {
    return cause === 'data' || cause === 'live';
}

/** The redraw interval unless the Consumer gives one, in milliseconds: the Razor component's `DefaultRedrawInterval`
 * (ADR-0067). */
const defaultRedrawInterval = 250;

/** Excel's own limits on a worksheet's rows and columns, the report's caps unless given (ADR-0066). */
const excelRows = 1_048_576;
const excelColumns = 16_384;

interface Question {
    readonly layout: PivotLayout;
    readonly generation: number;
    readonly controller: AbortController;
    /** Whether the layout is the user's, raised once shown: a move of the user's, or one asked again before its
     * answer came, under new settings or data, as the Razor component carries its pending raise. */
    readonly raise: boolean;
    /** Whether the answer marks what changed: new data, or the user's layout asked again under new data. */
    readonly markChanges: boolean;
    /** Whether the Pivot Source is asked again rather than answered from what the report source holds: while a Refresh
     * is owed. */
    readonly refreshData: boolean;
    /** The user's moves this question carries: its own, after those of the questions it superseded while they
     * were out, which were taken against them and go with it (ADR-0370 section 3). */
    readonly moves: readonly FieldListMove[];
    /** Whether it answers changes of data the source told of: those gathered when it was asked, and those the
     * question it superseded carried (ADR-0067). */
    readonly carries: boolean;
    /** Who asked it: the user's move, the Consumer's layout or settings, Refresh or Retry, or the source's notice. */
    readonly cause: Cause;
    /** What it is asked of and under: the rows of its answer are asked of the same, with the same. */
    readonly reportSource: LocalPivotReportSource;
    /** The Pivot Source its report source asks, which Show Details asks for the records behind a cell. */
    readonly source: PivotSource;
    readonly settings: PivotReportSettings;
    /** The locale its settings carry, which its counts and its report are written in. */
    readonly locale: string;
    readonly maxLeaves: number;
    /** The caps on the report's rows and columns it is shown under (ADR-0066). */
    readonly maxRows: number;
    readonly maxColumns: number;
    /** The Item a user's expand or collapse toggled, which keeps the Focus in the report this question brings. */
    readonly toggled: ToggledItem | undefined;
}

interface Shown {
    readonly layout: PivotLayout;
    /** The answer the report was drawn from; none for a layout with nothing to show. */
    readonly answer: PivotReportState | undefined;
    /** The question it answered, whose locale the grid writes the report in. */
    readonly question: Question | undefined;
}

// Keys that change no Selection: a modifier pressed alone, and a key an IME or a dead key takes to compose text.
const inertKeys: ReadonlySet<string> = new Set(['Shift', 'Control', 'Alt', 'Meta', 'AltGraph', 'CapsLock', 'Process', 'Dead']);

/** An Item expanded or collapsed on a row: the row Items down to it, the last of them the Item, which tell its place
 * from the same Item's under another parent; and how many gestures the user had made in the report's grid then. */
interface ToggledItem {
    readonly path: readonly PivotFieldItem[];
    readonly gestures: number;
}

// The label cell of the toggled Item among the rows held: the one that paints its button on the toggled row's path.
function toggledCell(rows: readonly PivotDisplayRow[], start: number, { path }: ToggledItem): CellPosition | undefined {
    const toggled = elementAt(path, path.length - 1);
    for (const [i, row] of rows.entries()) {
        if (row.rowPath.length < path.length || path.some((step, level) => {
            const at = elementAt(row.rowPath, level);
            return at.field !== step.field || !itemKeysEqual(at.item, step.item);
        })) continue;
        const column = row.labels.findIndex(label => label.toggle !== undefined && label.toggle.field === toggled.field
            && itemKeysEqual(label.toggle.item, toggled.item));
        if (column >= 0) return Object.freeze({ row: start + i, column });
    }
    return undefined;
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
 * in the Field List, a panel that menu opened in its place, or Filter… under a report filter in the band. */
type OpenSurface =
    | { readonly kind: 'layout'; readonly popup: PivotMenu }
    | (AtEntry & { readonly kind: 'entry'; readonly popup: PivotMenu })
    | (AtEntry & { readonly kind: 'panel'; readonly popup: PivotPanel })
    | { readonly kind: 'band'; readonly field: string; readonly against: PivotLayout; readonly popup: PivotPanel };

type EntrySurface = Extract<OpenSurface, { readonly kind: 'entry' | 'panel' }>;

/** A report filter in the band: its caption, and its button, which says what it shows and opens Filter… under it. */
interface BandFilter {
    readonly filter: HTMLElement;
    readonly caption: HTMLElement;
    readonly anchor: HTMLElement;
    readonly button: HTMLButtonElement;
    readonly summary: HTMLElement;
}

// Stops what a panel asked that is still on its way, as it leaves.
function closed(open: OpenSurface): void {
    if (open.kind === 'panel' || open.kind === 'band') open.popup.close?.();
    else open.popup.close();
}

// The Layout menu's groups, each headed where it starts, in Excel's order (ADR-0061).
const layoutHeadings: ReadonlyMap<PivotLayoutChoice, string> = new Map([
    ['doNotShowSubtotals', 'subtotals'],
    ['grandTotalsOff', 'grand-totals'],
    ['compactForm', 'report-layout'],
]);

// The ids of the pivot's own words: a command of the grid's own is not one of them.
const pivotWordIds: ReadonlySet<string> = new Set(pivotWordIdList());

// The rows a first answer brings: a screen's worth and more. The grid asks for the rest as it scrolls.
const firstWindowRows = 200;

// Whether a layout shows nothing: no field in Rows, Columns or Values, so there is nothing to compute, whatever
// stands in Filters (ADR-0059).
function showsNothing(layout: PivotLayout): boolean {
    return layout.rows.length === 0 && layout.columns.length === 0 && layout.values.length === 0;
}

/** What the report's text is charged by, as its grids resolve it (ADR-0028). */
function textOf(options: PivotViewOptions): GridTextOptions {
    return {
        ...(options.density === undefined ? {} : { density: options.density }),
        ...(options.cellMetrics === undefined ? {} : { cellMetrics: options.cellMetrics }),
        ...(options.presentationDefaults === undefined ? {} : { presentationDefaults: options.presentationDefaults }),
    };
}

/** Whether two Cell Metrics, either of them none, charge every glyph alike. */
function sameCellMetrics(a: CellTextMetrics | undefined, b: CellTextMetrics | undefined): boolean {
    return a === b || (a !== undefined && b !== undefined && a.equals(b));
}

/** Whether two Wrappers' defaults, either of them none, hand the grids the same widths and words. */
function sameDefaults(a: GridPresentationDefaults | undefined, b: GridPresentationDefaults | undefined): boolean {
    if (a === b) return true;
    if (a === undefined || b === undefined) return false;
    const read = ['wideWidthPx', 'digitWidthPx', 'narrowWidthPx', 'boldWideWidthPx', 'boldDigitWidthPx', 'boldNarrowWidthPx', 'otherWidthPx',
        'boldOtherWidthPx', 'glyphWidths', 'fontSizePx', 'density', 'highlightHoverRow', 'stripeRows'] as const;
    return read.every(name => a[name] === b[name]);
}

/** Whether two geometries charge every glyph alike. */
function sameGeometry(a: GridTextGeometry, b: GridTextGeometry): boolean {
    if (a === b) return true;
    const widths = ['wideWidthPx', 'digitWidthPx', 'narrowWidthPx', 'fullWidthPx', 'otherWidthPx', 'cellHorizontalPaddingPx'] as const;
    if (widths.some(name => a[name] !== b[name])) return false;
    const glyphs = Object.keys(a.glyphWidths);
    return glyphs.length === Object.keys(b.glyphWidths).length && glyphs.every(point => a.glyphWidths[Number(point)] === b.glyphWidths[Number(point)]);
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
    const redrawInterval = given['redrawInterval'];
    if (redrawInterval !== undefined && (typeof redrawInterval !== 'number' || !Number.isFinite(redrawInterval) || redrawInterval < 0)) {
        throw new RangeError(`redrawInterval is a time in milliseconds of zero or more, not ${JSON.stringify(redrawInterval)} (ADR-0067).`);
    }
    for (const name of ['maxLeaves', 'maxRows', 'maxColumns']) {
        const cap = given[name];
        if (cap !== undefined && (typeof cap !== 'number' || !Number.isSafeInteger(cap) || cap < 1)) {
            throw new RangeError(`${name} is a whole number of at least 1, not ${JSON.stringify(cap)} (ADR-0066).`);
        }
    }
    if (given['cellMetrics'] !== undefined && !(given['cellMetrics'] instanceof CellTextMetrics)) {
        throw new TypeError('cellMetrics is a CellTextMetrics: the widths the report\'s grids charge each glyph (ADR-0016).');
    }
    if (given['presentationDefaults'] !== undefined && !(given['presentationDefaults'] instanceof GridPresentationDefaults)) {
        throw new TypeError('presentationDefaults is a GridPresentationDefaults, as a Wrapper makes one (ADR-0030).');
    }
    const highlight = given['changeHighlightDuration'];
    if (highlight !== undefined && (typeof highlight !== 'number' || !Number.isFinite(highlight) || highlight < 0)) {
        throw new RangeError(`changeHighlightDuration is a finite number of zero or more milliseconds, not ${typeof highlight === 'number' ? String(highlight) : typeof highlight} (ADR-0068).`);
    }
    const slicing = given['slicing'];
    if (slicing !== undefined && (typeof slicing !== 'object' || slicing === null)) throw new TypeError('slicing is an object.');
    const showSummary = given['showSelectionSummary'];
    if (showSummary !== undefined && typeof showSummary !== 'boolean') throw new TypeError('showSelectionSummary is true or false.');
    const figures: unknown = given['summaryFigures'];
    if (figures !== undefined && (!Array.isArray(figures) || !figures.every(figure => (summaryFigureOrder as readonly unknown[]).includes(figure)))) {
        throw new TypeError(`summaryFigures is a list of the figures ${summaryFigureOrder.join(', ')} (ADR-0130).`);
    }
    for (const name of ['onLayoutChanged', 'onRefused', 'onFailed', 'onShowFieldListChanged', 'onSelectionChanged', 'onShowDetails', 'label',
        'onSummaryFiguresChanged', 'onSelectionSummaryChanged']) {
        const handler = given[name];
        if (handler !== undefined && typeof handler !== 'function') throw new TypeError(`${name} is a function.`);
    }
    const detailsView = given['detailsView'];
    if (detailsView !== undefined && detailsView !== 'tab' && detailsView !== 'dialog') {
        throw new TypeError(`detailsView is 'tab' or 'dialog', not ${JSON.stringify(detailsView)}.`);
    }
    checkedPivotChrome(given['chrome']);
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

// Two lists of figures that name the same figures, whatever their order: the grid shows them in Excel's.
function sameFigures(a: readonly SummaryFigure[] | undefined, b: readonly SummaryFigure[] | undefined): boolean {
    if (a === b) return true;
    if (a === undefined || b === undefined) return false;
    const named = new Set(b);
    return new Set(a).size === named.size && a.every(figure => named.has(figure));
}

// A copy refused, in the sentence the grid says.
function refuse(sentence: string): GridCopyAnswer {
    return Object.freeze({ kind: 'refuse', sentence });
}

/** One ExPivot, drawn into its root. */
export class PivotView {
    readonly #root: HTMLElement;
    readonly #mountGrid: MountGrid;
    readonly #report: HTMLElement;
    readonly #notice: HTMLElement;
    // The Stale Report's live region under the Pivot Toolbar, always present, and the notice it holds while the report
    // is stale: the sentence, and Retry (ADR-0067).
    readonly #staleRegion: HTMLElement;
    readonly #staleNotice: HTMLElement;
    readonly #staleMessage: HTMLElement;
    readonly #retryButton: HTMLButtonElement;
    readonly #fieldListToggle: HTMLButtonElement;
    readonly #refreshButton: HTMLButtonElement;
    readonly #layoutAnchor: HTMLElement;
    readonly #toolbarStart: HTMLElement;
    // The built-in Pivot Toolbar, which a Chrome's toolbar stands in place of (ADR-0390).
    readonly #toolbar: HTMLElement;
    // The controls and anchors the toolbar, the band and the Stale Report's notice handed in, the built-in markup's or
    // a Chrome's: the keyboard goes to them, and the popups are placed in them, by name.
    readonly #parts: PartElements;
    readonly #toolbarSlot: StandingSlot<PivotToolbarView, PivotToolbarSlotContext>;
    readonly #bandSlot: StandingSlot<PivotReportFiltersView, PivotReportFiltersSlotContext>;
    readonly #staleSlot: StandingSlot<PivotStaleReportView, PivotStaleReportSlotContext>;
    // What a standing part's contents threw as they were mounted, thrown once the draw that mounted them is done.
    #slotErrors: Error[] = [];
    // Whether the draw is the Consumer's, making the pivot or updating it, which a Chrome that cannot be drawn is thrown
    // to.
    #consumerDraws = false;
    // The report filter band (ADR-0061): each report filter's elements, kept while it stands in Filters.
    readonly #band: HTMLElement;
    readonly #bandFilters = new Map<string, BandFilter>();
    readonly #layoutButton: HTMLButtonElement;
    readonly #layoutLabel: HTMLElement;
    readonly #backdrop: HTMLElement;
    readonly #sheet: HTMLElement;
    readonly #empty: HTMLElement;
    readonly #gridRoot: HTMLElement;
    readonly #dropRemove: HTMLElement;
    readonly #fieldListBox: HTMLElement;
    readonly #fieldListScroll: HTMLElement;
    readonly #builtInFieldList: FieldListView;
    // The Field List a Chrome draws (ADR-0390), in the built-in pane's place while it stands.
    readonly #slottedFieldList: SlottedFieldList;
    #fieldListSlotted = false;
    // Show Details' sheets, when the Consumer does not take the records: tabs at the report's foot, or a dialog.
    readonly #details: DetailsSheets;
    // The Show Details events this pivot handed a Consumer: the only ones it opens when they are handed back.
    readonly #detailsRaised = new WeakSet<PivotShowDetailsEvent>();
    readonly #userWidths = new Map<string, number>();
    // Read once, when the pivot is made: every report source it makes shares the thread the same way.
    readonly #slicing: PivotSlicing | undefined;
    #options: PivotViewOptions;
    #words: PivotScreenWords;
    // The Selection Summary's figures shown, and the ones last handed in (ADR-0130), as the Razor component's
    // `HeldSummaryFigures`: the figures menu's choice stands until the Consumer hands in different ones.
    #summaryFigures: readonly SummaryFigure[];
    #summaryFiguresHanded: readonly SummaryFigure[] | undefined;
    // The words of the grid's commands, made again when the Consumer's words change.
    #commandLabel: (id: string) => string | undefined;
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
    // While Defer Layout Update is ticked (ADR-0061): the layout the pane shows and builds, which Update applies, and
    // what the pane moved into it that the report on screen does not hold yet, which a refusal of that layout names.
    #deferred: Deferred | undefined;
    #generation = 0;
    #requests = 0;
    #refusal: string | undefined;
    // A Refresh pressed and not yet answered from the Pivot Source asked again.
    #refreshOwed = false;
    // A change of data no question asked since answers, and the Source Version the latest notice named, if it named
    // one (ADR-0067). When a change of data last reached the screen, from which the next waits the redraw interval;
    // the timer that waits; and how to stop hearing the report source's notices.
    #changed = false;
    #changedTo: string | undefined;
    // The Stale Report (ADR-0067): what happened while the newest data cannot be shown, and when the data the report
    // shows arrived, which the notice and a refused Copy say.
    #stale: string | undefined;
    // The Change Highlight (ADR-0068): when this pivot first showed each change the report source marks, one for the
    // pivot's life across sources; the grid's question about a cell, one function for the pivot's life, as the Razor
    // component holds its delegate, so that its identity says nothing changed (ADR-0003): a newly marked value is a
    // new row; and each value column's index by its name, which a row's marks are listed by.
    readonly #changeTimes = new PivotChangeTimes();
    readonly #cellChangedAt: NonNullable<GridOptions<PivotDisplayRow>['cellChangedAt']> = (row, column) => {
        const index = this.#valueColumnIndexes.get(column);
        return index === undefined ? undefined : this.#changeTimes.changedAt(row, index);
    };
    #valueColumnIndexes = new Map<string, number>();
    // Whether what left the report stale was a question that refreshed the Pivot Source, which Retry then asks again
    // rather than the report alone: asking a source that did not refresh would show its old data as the newest.
    #staleRetryRefreshes = false;
    #shownAt = Date.now();
    // The time of the data shown as last said, and what it was said for: the time, the locale and the day.
    #asOfSaid: { readonly key: string; readonly text: string } | undefined;
    #lastDataShown: number | undefined;
    #redrawTimer: ReturnType<typeof setTimeout> | undefined;
    #stopHearing: (() => void) | undefined;
    #fieldListShown: boolean;
    #counts: PivotItemCounts | undefined;
    #countsAsked: AbortController | undefined;
    #grid: GridHandle<PivotDisplayRow> | undefined;
    #rows: FetchingGridSource<PivotDisplayRow> | undefined;
    // The grid's columns as last drawn, whose names a copy asks the report source for.
    #gridColumns: readonly GridColumn<PivotDisplayRow>[] = [];
    #dropRemoveShown = false;
    #open: OpenSurface | undefined;
    // The Items Filter… lists, asked of the source under the report on screen (ADR-0066).
    readonly #items = new ItemListings(() => { this.#draw(); });
    // A field menu the pane left while it held the keyboard: the keyboard goes to its field's entry once drawn.
    #keyboardAfterDraw: OpenSurface | undefined;
    // Stops waiting for the first rows of the report a toggled Item keeps the Focus in.
    #placing: (() => void) | undefined;
    // The presses, wheels and keys the user has made in the report's grid, counted: a toggled Item keeps the Focus only
    // while none has come since the toggle (spine rule 6).
    #gestures = 0;
    #destroyed = false;

    constructor(root: HTMLElement, options: PivotViewOptions, mountGrid: MountGrid) {
        this.#options = checkedOptions(options);
        this.#root = root;
        this.#mountGrid = mountGrid;
        this.#slicing = options.slicing;
        this.#words = new PivotScreenWords(options.label);
        this.#summaryFiguresHanded = options.summaryFigures;
        this.#summaryFigures = options.summaryFigures ?? defaultSummaryFigures;
        this.#commandLabel = this.#commandLabelOf(options.label);
        this.#geometry = gridTextGeometry(textOf(options));
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
        this.#parts = new PartElements(root);
        const toolbar = div('ex-pivot-toolbar');
        this.#toolbar = toolbar;
        const start = div('ex-pivot-toolbar-start');
        this.#toolbarStart = start;
        this.#band = div('ex-pivot-filters');
        this.#band.setAttribute('role', 'group');
        const end = div('ex-pivot-toolbar-end');
        // Refresh asks the Pivot Source again, offered only by a source that can be asked again (ADR-0066).
        this.#refreshButton = document.createElement('button');
        this.#refreshButton.type = 'button';
        this.#refreshButton.className = 'ex-pivot-toolbar-button ex-pivot-refresh-button';
        this.#refreshButton.setAttribute('data-ex-claims', 'click');
        this.#refreshButton.addEventListener('click', () => { this.#refresh(); });
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
        // A polite live region, always present, so a notice that appears in it is announced without interrupting the
        // reader as the Pivot Toolbar's refusal of their own change does. Empty, it takes no room.
        this.#staleRegion = div('ex-pivot-stale');
        this.#staleRegion.setAttribute('role', 'status');
        this.#staleNotice = div('ex-pivot-stale-notice');
        this.#staleMessage = document.createElement('span');
        this.#staleMessage.className = 'ex-pivot-stale-message';
        this.#retryButton = document.createElement('button');
        this.#retryButton.type = 'button';
        this.#retryButton.className = 'ex-pivot-retry';
        this.#retryButton.setAttribute('data-ex-claims', 'click');
        this.#retryButton.addEventListener('click', () => { this.#retry(); });
        this.#staleNotice.append(this.#staleMessage, this.#retryButton);
        this.#toolbarSlot = new StandingSlot('toolbar', document, Object.freeze({
            layoutMenu: Object.freeze({ label: '', open: false }), fieldList: Object.freeze({ label: '', shown: false }), reportFilters: false,
        }));
        this.#bandSlot = new StandingSlot('reportFilters', document, Object.freeze({ name: '', filters: Object.freeze([]) }));
        this.#staleSlot = new StandingSlot('staleReport', document, Object.freeze({
            message: '', asOfText: '', reason: '', retry: Object.freeze({ label: '', enabled: false }),
        }));
        this.#bandSlot.element.setAttribute('role', 'group');
        this.#sheet = div('ex-pivot-sheet');
        // Records covering the report take no drop meant for it.
        this.#sheet.addEventListener('dragover', event => {
            if (!this.#fieldList.removable || this.#details.covering) return;
            event.preventDefault();
        });
        this.#sheet.addEventListener('drop', event => {
            event.preventDefault();
            if (!this.#details.covering) this.#fieldList.dropOnReport();
        });
        this.#empty = div('ex-pivot-empty');
        this.#empty.setAttribute('role', 'status');
        this.#gridRoot = document.createElement('div');
        this.#dropRemove = div('ex-pivot-report-drop-remove');
        this.#dropRemove.setAttribute('aria-hidden', 'true');
        this.#dropRemove.append(document.createElement('span'));
        this.#report.append(toolbar, this.#notice, this.#staleRegion, this.#sheet);
        this.#fieldListBox = div('ex-pivot-field-list');
        this.#fieldListBox.setAttribute('role', 'region');
        const scroll = div('ex-pivot-field-list-scroll');
        this.#fieldListBox.append(scroll);
        this.#fieldListScroll = scroll;
        const fieldListHandlers: FieldListHandlers = {
            edit: edit => { this.#edited(edit); },
            close: () => { this.#setFieldListShown(false); },
            dragging: removable => { this.#paintDropRemove(removable); },
            menu: entry => { this.#toggleEntryMenu(entry); },
            defer: deferred => { this.#setDeferred(deferred); },
            update: () => { this.#applyPending(); },
        };
        this.#builtInFieldList = new FieldListView(scroll, fieldListHandlers);
        this.#slottedFieldList = new SlottedFieldList(document, fieldListHandlers, this.#parts);
        root.append(this.#report, this.#fieldListBox);
        this.#details = new DetailsSheets({
            document,
            mountGrid,
            words: () => this.#words,
            look: () => ({
                rowHeight: this.#options.rowHeight, density: this.#options.density, cellMetrics: this.#options.cellMetrics,
                presentationDefaults: this.#options.presentationDefaults, commandLabel: this.#options.label, chrome: this.#chrome?.grid,
            }),
            problemText: problem => detailsProblemText(problem, this.#words),
            changed: () => { this.#draw(); },
            // The report's grid takes the keyboard back; a report with no grid has the Field List's toggle take it, so
            // the keyboard stays in the pivot.
            returnKeyboard: () => {
                if (this.#grid === undefined) this.#parts.focus('field-list');
                else this.#gridRoot.focus();
            },
            parts: this.#parts,
            chrome: () => this.#chrome,
        });
        // A drag that moves on from an Area, anywhere in the pivot, leaves it unmarked; and one that leaves the
        // pivot for an element the browser names. WebKit names none on leaving one element for another, so a
        // leave that names nothing is not taken as one.
        const listening = { signal: this.#listening.signal };
        const gestured = (): void => { this.#gestures++; };
        this.#gridRoot.addEventListener('pointerdown', gestured, { capture: true, ...listening });
        this.#gridRoot.addEventListener('wheel', gestured, { capture: true, passive: true, ...listening });
        this.#gridRoot.addEventListener('keydown', event => {
            if (!event.isComposing && !inertKeys.has(event.key)) gestured();
        }, { capture: true, ...listening });
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
            if (event.target instanceof Node && this.#within(open, event.target)) return;
            this.#close('stay');
        };
        root.addEventListener('mousedown', outside, listening);
        root.addEventListener('focusin', outside, listening);

        this.#askCounts();
        // Heard once the pivot is whole: a construction that throws leaves no listener on the Consumer's source.
        this.#hear(this.#reportSource);
        if (options.layout !== undefined) this.#ask(options.layout, 'consumer', []);
        try {
            this.#consumerDraws = true;
            this.#draw();
        } catch (error) {
            // A pivot refused as it is made leaves nothing hearing the source.
            this.destroy();
            throw error;
        } finally {
            this.#consumerDraws = false;
        }
    }

    #reportSourceOf(source: PivotSource): LocalPivotReportSource {
        const slicing = this.#slicing;
        return new LocalPivotReportSource(source, slicing === undefined ? undefined : { slicing });
    }

    // A report source is disposed once nothing asks it and no row on screen reads it.
    #release(reportSource: LocalPivotReportSource | undefined): void {
        if (reportSource !== undefined && reportSource !== this.#reportSource && reportSource !== this.#shownReportSource) reportSource.dispose();
    }

    /** Takes the options again, whole. Options refused leave the pivot as it was. A Chrome slot whose contents cannot
     * be drawn is thrown once the options are taken, with the built-in markup standing in its place (ADR-0390). */
    update(options: PivotViewOptions): void {
        if (this.#destroyed) throw new Error('This ExPivot was destroyed: make a new one.');
        checkedOptions(options);
        const before = this.#options;
        const sourceMoved = options.source !== before.source;
        // A layout handed in is a question, unless the pane holds it already or this pivot raised it.
        const layoutMoved = options.layout !== this.#layoutGiven;
        const consumerLayout = layoutMoved && options.layout !== undefined && options.layout !== this.#layoutAhead && !this.#raised.has(options.layout)
            ? options.layout
            : undefined;
        const asked = consumerLayout ?? (sourceMoved ? this.#layoutAhead : undefined);
        // Checked against the source it will be asked of before anything changes; a new source is always asked.
        if (asked !== undefined) validateLayout(asked, this.#declared(options.source));
        this.#options = options;
        if (layoutMoved) this.#layoutGiven = options.layout;
        // The Consumer's layout replaces the user's, a pending one too. A pending layout placing a field the new source
        // does not have goes back to the one asked of it, which is checked above: the rest of what a layout is checked
        // for does not depend on the source, and the pending one was made by the pane's own edits.
        const pending = this.#deferred?.layout;
        if (asked !== undefined && pending !== undefined && (consumerLayout !== undefined || !placesOnly(pending, this.#declared(options.source)))) {
            this.#deferred = Object.freeze({ layout: asked, moves: Object.freeze([]) });
        }
        // What the report's text is charged by: a label's width is estimated under it, so the report is asked again
        // when it charges otherwise, and not for equal widths handed again in a new object, as a framework's render may.
        const geometry = gridTextGeometry(textOf(options));
        const textMoved = options.density !== before.density || !sameGeometry(geometry, this.#geometry);
        // The grids are handed what they resolve their own metrics from when it charges or reads otherwise, not when
        // equal values come again in new objects.
        const metricsGiven = !sameCellMetrics(options.cellMetrics, before.cellMetrics) || !sameDefaults(options.presentationDefaults, before.presentationDefaults);
        const settingsMoved = options.locale !== before.locale || options.label !== before.label || textMoved;
        if (options.label !== before.label) {
            this.#words = new PivotScreenWords(options.label);
            this.#commandLabel = this.#commandLabelOf(options.label);
        }
        if (textMoved) this.#geometry = geometry;
        if (options.label !== before.label || textMoved) this.#columns = this.#columnsOf(options);
        // A highlight's length is the next question's to carry, which keeps what it compares that long; the report on
        // screen is not asked again for it, and the grid paints by it at once.
        const highlightMoved = highlightMsOf(options) !== highlightMsOf(before);
        if (settingsMoved || highlightMoved) this.#settings = this.#settingsOf(options);
        if (options.showFieldList !== before.showFieldList) this.#fieldListShown = options.showFieldList !== false;
        if (sourceMoved) {
            // A new source is asked from nothing: no Refresh is owed to it.
            this.#refreshOwed = false;
            // The rows on screen keep reading the report source that answered them until a new answer replaces them.
            const replaced = this.#reportSource;
            this.#reportSource = this.#reportSourceOf(options.source);
            this.#hear(this.#reportSource);
            this.#release(replaced);
            this.#counts = undefined;
            this.#askCounts();
        }
        if (options.redrawInterval !== before.redrawInterval) {
            // A redraw waiting on the interval before is timed again on the new one.
            this.#disarmRedraw();
            this.#scheduleRedraw();
        }
        if (asked !== undefined) {
            this.#ask(asked, consumerLayout !== undefined ? 'consumer' : 'data', []);
        } else if (settingsMoved || capsOf(options) !== capsOf(before)) {
            this.#ask(this.#layoutAhead, 'consumer', []);
        }
        const gridChromeMoved = options.chrome?.grid !== before.chrome?.grid;
        const summaryMoved = this.#adoptSummaryFigures(options.summaryFigures) || options.showSelectionSummary !== before.showSelectionSummary;
        if (summaryMoved || highlightMoved || options.rowHeight !== before.rowHeight || textMoved || metricsGiven || options.viewportHeight !== before.viewportHeight
            || options.viewportWidth !== before.viewportWidth || gridChromeMoved) {
            this.#drawGrid();
        }
        const detailsChromeMoved = options.chrome?.detailsTabs !== before.chrome?.detailsTabs || options.chrome?.detailsDialog !== before.chrome?.detailsDialog;
        this.#consumerDraws = true;
        try {
            if (settingsMoved || metricsGiven || options.rowHeight !== before.rowHeight || gridChromeMoved || detailsChromeMoved) {
                this.#details.draw();
                this.#slotErrors.push(...this.#details.takeRefused());
            }
            this.#draw();
        } finally {
            this.#consumerDraws = false;
        }
    }

    /** Opens the records of a Show Details event this pivot raised, as `detailsView` says, as when nobody hears
     * `onShowDetails`: a Consumer that heard it and does not take it hands it back. An event another pivot raised is
     * refused, rather than shown under this pivot's report; a stopped pivot opens nothing. */
    openDetails(event: PivotShowDetailsEvent): void {
        if (!this.#detailsRaised.has(event)) throw new Error('These records are another pivot\'s: a pivot opens only the Show Details it raised.');
        if (this.#destroyed) return;
        this.#consumerDraws = true;
        try {
            this.#details.open(event, this.#options.detailsView ?? 'tab', this.#locale); // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0059: a tab at the report's foot unless the Consumer asks for the dialog, as the Razor component's DetailsView
            this.#slotErrors.push(...this.#details.takeRefused());
            this.#throwRefused();
        } finally {
            this.#consumerDraws = false;
        }
    }

    /** Stops the pivot: it asks and hears nothing more, and empties its element. The source stays the
     * Consumer's. */
    destroy(): void {
        if (this.#destroyed) return;
        this.#destroyed = true;
        this.#listening.abort();
        this.#stopHearing?.();
        this.#stopHearing = undefined;
        this.#disarmRedraw();
        this.#takeDown();
        this.#toolbarSlot.hide();
        this.#bandSlot.hide();
        this.#staleSlot.hide();
        this.#slottedFieldList.hide();
        this.#parts.dispose();
        this.#items.dispose();
        this.#details.destroy();
        this.#generation++;
        this.#asked?.controller.abort();
        this.#asked = undefined;
        this.#countsAsked?.abort();
        this.#placing?.();
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
        clearStyle(this.#root);
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

    // The layout the report is on, or on its way to: the one the newest question asks for, or the one it shows.
    get #layoutAhead(): PivotLayout {
        return this.#asked?.layout ?? this.#shown.layout;
    }

    // The pane's layout: the pending one while Defer Layout Update is ticked, otherwise the report's.
    get #paneLayout(): PivotLayout {
        return this.#deferred?.layout ?? this.#layoutAhead;
    }

    get #redrawInterval(): number {
        return this.#options.redrawInterval ?? defaultRedrawInterval;
    }

    get #maxLeaves(): number {
        return this.#options.maxLeaves ?? defaultMaxLeaves;
    }

    get #changeHighlightMs(): number {
        return highlightMsOf(this.#options);
    }

    get #maxRows(): number {
        return this.#options.maxRows ?? excelRows;
    }

    get #maxColumns(): number {
        return this.#options.maxColumns ?? excelColumns;
    }

    get #locale(): string {
        return this.#localeOf(this.#options);
    }

    // The Chrome's slots, read as each part opens (ADR-0390).
    get #chrome(): PivotChrome | undefined {
        return this.#options.chrome;
    }

    // The Field List standing now: a Chrome's, or the built-in pane.
    get #fieldList(): FieldListView | SlottedFieldList {
        return this.#fieldListSlotted ? this.#slottedFieldList : this.#builtInFieldList;
    }

    // The report source estimates the label widths under the grid's text geometry, as the Razor component tells it
    // the grid's Cell Metrics (ADR-0151).
    #settingsOf(options: PivotViewOptions): PivotReportSettings {
        const words = wordsRecord(options.label);
        // The report source keeps what a highlight compares for as long as it is marked (ADR-0153).
        return Object.freeze({
            locale: this.#localeOf(options), labelMetrics: this.#geometry, changeHighlightMs: highlightMsOf(options),
            ...(words === undefined ? {} : { words }),
        });
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
            toggled: (toggle: PivotToggle, row: PivotDisplayRow) => { this.#toggled(toggle, row); },
        }), textOf(options));
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
        // While Defer Layout Update is ticked the pane builds the pending layout, and the report and the source are
        // left alone (ADR-0061).
        const deferred = this.#deferred;
        if (deferred !== undefined) {
            const { moved } = edit;
            // A field moved into an Area again replaces its earlier move into that Area, so the moves stay bounded.
            const moves = moved === undefined ? deferred.moves
                : [...deferred.moves.filter(move => move.field !== moved.field || move.area !== moved.area), moved];
            this.#deferred = Object.freeze({ layout: edit.layout, moves: Object.freeze(moves) });
            this.#refusal = undefined;
            this.#draw();
            return;
        }
        this.#move(edit.layout, edit.moved === undefined ? [] : [edit.moved]);
    }

    // A gesture on the report or the Pivot Toolbar, made to the layout the report is on. While Defer Layout Update
    // holds a pending layout, it reaches that one too, so Update does not take it back; where the pending layout does
    // not hold what the gesture was for, it stays as it is. A gesture that would change nothing now asks nothing.
    #reportEdit(edit: (layout: PivotLayout) => PivotLayout | undefined, toggled?: ToggledItem): void {
        const deferred = this.#deferred;
        if (deferred !== undefined) this.#deferred = Object.freeze({ ...deferred, layout: edit(deferred.layout) ?? deferred.layout });
        const now = this.#layoutAhead;
        const next = edit(now);
        if (next === undefined || next === now) this.#draw();
        else this.#move(next, [], toggled);
    }

    // Defer Layout Update ticked holds the pane's changes from here on; unticked, it applies them, as Excel does.
    #setDeferred(deferred: boolean): void {
        const held = this.#deferred;
        if (deferred === (held !== undefined)) return;
        if (deferred) {
            this.#deferred = Object.freeze({ layout: this.#layoutAhead, moves: Object.freeze([]) });
            this.#draw();
            return;
        }
        const unticked = defined(held, 'Defer Layout Update ticked');
        this.#deferred = undefined;
        if (this.#pendingApplies(unticked.layout)) this.#move(unticked.layout, unticked.moves);
        else this.#draw();
    }

    // Update applies the pending layout in one change, and Defer Layout Update stays ticked. The pending layout stays
    // the pane's, as the Razor component keeps it: refused, it is still the user's work, with the moves it names.
    #applyPending(): void {
        const held = this.#deferred;
        if (held !== undefined && this.#pendingApplies(held.layout)) this.#move(held.layout, held.moves);
    }

    // Whether a pending layout would change the report's: layouts are compared by what they say, since a gesture on
    // the report makes the pending layout and the report's anew, each its own.
    #pendingApplies(pending: PivotLayout): boolean {
        const now = this.#layoutAhead;
        return pending !== now && writePivotLayoutJson(pending) !== writePivotLayoutJson(now);
    }

    #move(layout: PivotLayout, moved: readonly FieldListMove[], toggled?: ToggledItem): void {
        // A move after a toggle asks for another report: the Item toggled is no longer the one to follow.
        this.#placing?.();
        this.#refusal = undefined;
        this.#ask(layout, 'user', moved, toggled);
        this.#draw();
    }

    #ask(layout: PivotLayout, asked: Cause, moved: readonly FieldListMove[], toggled?: ToggledItem): void {
        const superseded = this.#asked;
        superseded?.controller.abort();
        // A layout asked again before its answer came keeps what that question was owed: the user's raises once
        // shown and names its moves if refused, and new data's changes are marked.
        const again = superseded?.layout === layout;
        const raise = asked === 'user' || (again && superseded.raise);
        // A question asked while a Refresh is owed asks the Pivot Source again, and marks what changed, whatever asked it.
        const refreshData = this.#refreshOwed;
        const markChanges = asksData(asked) || refreshData || (again && superseded.markChanges);
        // An Item toggled into the layout keeps the Focus in its report, asked again or not.
        const focusOn = toggled ?? (again ? superseded.toggled : undefined);
        // A user's move made while a user's question was out was taken against it, and carries its moves on.
        const moves = [...(raise && superseded?.raise === true ? superseded.moves : []), ...moved];
        const generation = ++this.#generation;
        // Asked of the source as it stands now, a question answers every change of data told before it, and those the
        // question it superseded carried.
        const carries = this.#changed || superseded?.carries === true;
        this.#changed = false;
        this.#disarmRedraw();
        if (showsNothing(layout) && layout.filters.length === 0) {
            // Nothing to compute: the report is the empty box that says how to start. A layout with fields in Filters
            // alone is asked for still, as the Razor component asks every layout: Filter… lists their Items under its
            // Report Version (ADR-0066). Nothing on screen reads the data, so no change of data is owed to it: the next
            // layout's question is asked of the source as it stands then.
            this.#asked = undefined;
            this.#showLayout(layout, undefined, raise);
            return;
        }
        const question: Question = {
            layout, generation, controller: new AbortController(), raise, markChanges, refreshData, moves: Object.freeze(moves), carries, cause: asked,
            reportSource: this.#reportSource, source: this.#options.source, settings: this.#settings, locale: this.#locale, maxLeaves: this.#maxLeaves,
            maxRows: this.#maxRows, maxColumns: this.#maxColumns, toggled: focusOn,
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
                refreshData: question.refreshData,
            }), question.controller.signal);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a superseded question's failure is nobody's; the newest one's is said where the user sees it and rethrown
            if (!current()) return;
            this.#asked = undefined;
            const stale = this.#staleFor(question);
            if (stale) this.#markStale(this.#words.staleFailed(error), question.refreshData);
            else this.#refusal = this.#words.failed(error);
            this.#draw();
            // A failure is said where the user sees it, and asked again only by the next notice, Refresh, Retry or move:
            // asking again on the clock would ask a failing source once per interval.
            this.#landed(question, undefined, false);
            this.#options.onFailed?.(Object.freeze({ error, stale }));
            // A failure the Stale Report says is raised and thrown no further, as the Razor component keeps it in
            // LastError: a live source whose transport is down would otherwise throw once for each notice. Any other is
            // rethrown.
            if (!stale) throw error;
            return;
        }
        // Late and superseded: a newer question was asked, and this answers a layout no longer wanted.
        if (!current()) return;
        this.#asked = undefined;
        if (update.refusal !== undefined) {
            if (this.#staleFor(question)) {
                this.#markStale(this.#staleRefusalOf(update.refusal, question), question.refreshData);
                this.#draw();
            } else {
                this.#refuse(update.refusal, question);
            }
            // The report on screen stays on its layout, and the changes the refused question carried are gathered again
            // for it: a refusal of the user's move is no reason to leave the totals before them on screen. A refusal of
            // the layout on screen itself is said, and asked again only by the next notice, Refresh or move.
            this.#landed(question, undefined, question.layout !== this.#shown.layout);
            return;
        }
        const broken = capBrokenBy(update.metadata, question);
        if (broken !== undefined) {
            // An answer past the report's caps is not shown (ADR-0066): newer data under the layout on screen leaves a
            // Stale Report, and a layout is refused, naming the moves it held. Either way the version is the one the
            // source told of, so it is not asked for again until the data moves on.
            const cap = formatPivotCount(broken.cap, question.locale);
            // The source answered, so a Refresh it carried is paid, and Retry asks the report again.
            if (question.refreshData) this.#refreshOwed = false;
            if (this.#staleFor(question)) {
                this.#markStale(this.#words.fill(broken.stale, cap), false);
                this.#draw();
            } else {
                this.#sayRefused(this.#words.fill(broken.refusal, cap), heldFields(heldMoves(question)));
            }
            this.#landed(question, update.metadata.sourceVersion, question.layout !== this.#shown.layout);
            return;
        }
        // The data on screen is the Pivot Source's as it is now: the Refresh is paid.
        if (question.refreshData) this.#refreshOwed = false;
        // Newer data, or data asked for again, ends a Stale Report; the time of the data shown moves with its version, and
        // with its source, whose versions are its own.
        const shown = this.#shown;
        const newer = shown.answer?.metadata.sourceVersion !== update.metadata.sourceVersion || shown.question?.reportSource !== question.reportSource;
        if (newer) this.#shownAt = Date.now();
        if (newer || asksData(question.cause)) {
            this.#stale = undefined;
            this.#staleRetryRefreshes = false;
        }
        // The changes this Window carries are shown from now, on the clock the grid paints by (createGrid's is the
        // system clock, ADR-0068). A cell keeps its mark after the source stops listing its change, until the
        // highlight stamped here ends.
        this.#changeTimes.adopt(update, systemClock.now(), this.#changeHighlightMs);
        this.#showLayout(question.layout, { answer: update, question }, question.raise);
        this.#landed(question, update.metadata.sourceVersion, false);
    }

    // ---- Changes of data (ADR-0067) --------------------------------------------------------------------

    // Whether a question that could not be shown leaves a Stale Report rather than a refusal: one the data asked, for the
    // layout of the report on screen. A move of the user's or the Consumer's is refused, and the pane goes back.
    #staleFor(question: Question): boolean {
        return asksData(question.cause) && this.#shown.answer !== undefined && question.layout === this.#shown.layout;
    }

    #markStale(sentence: string, retryRefreshes: boolean): void {
        this.#stale = sentence;
        this.#staleRetryRefreshes = retryRefreshes;
    }

    // What a refusal of newer data says, after the time of the data shown. A refusal the report source made itself, and
    // not the Pivot Source, is its failure to answer, as the Razor component says it.
    #staleRefusalOf(refusal: PivotReportRefusal, question: Question): string {
        const source = refusal.sourceRefusal;
        if (source === undefined) return this.#words.fill('stale-source-failed', refusal.message);
        if (source.kind === 'tooManyLeaves') return this.#words.fill('stale-too-many-cells', formatPivotCount(source.limit ?? question.maxLeaves, question.locale));
        return this.#words.fill('stale-source-refused', source.message);
    }

    // The Stale Report's Retry asks again what failed: Refresh, when that is what failed, otherwise the report's question
    // of the Pivot Source (ADR-0067). Either asks the Pivot Source itself, as the Razor component's Retry does: a report
    // source that kept its last answer would give back the data the report already shows, and the notice would go.
    #retry(): void {
        if (this.#staleRetryRefreshes) {
            this.#refresh();
            return;
        }
        this.#refreshOwed = true;
        this.#refusal = undefined;
        this.#ask(this.#layoutAhead, 'data', []);
        this.#draw();
    }

    // When the data the report shows arrived, in the report's locale: the time, and the date too when it is not today's,
    // since a time alone would say the report is fresher than it is.
    #asOf(): string {
        const at = new Date(this.#shownAt);
        const today = new Date().toDateString() === at.toDateString();
        const key = `${this.#shownAt} ${this.#locale} ${today ? 'today' : 'before'}`;
        if (this.#asOfSaid?.key !== key) {
            const text = new Intl.DateTimeFormat(this.#locale, today ? { timeStyle: 'medium' } : { dateStyle: 'short', timeStyle: 'medium' }).format(at);
            this.#asOfSaid = { key, text };
        }
        return this.#asOfSaid.text;
    }

    // Hears the report source's notices, and no longer the one before: a source handed over is asked from nothing, and
    // that question answers whatever was gathered.
    #hear(reportSource: LocalPivotReportSource): void {
        this.#stopHearing?.();
        this.#changed = false;
        this.#changedTo = undefined;
        this.#disarmRedraw();
        this.#stopHearing = reportSource.subscribe(changed => { this.#sourceChanged(reportSource, changed); });
    }

    // The source's data moved on: gathered, and asked for at once when the last change reached the screen more than
    // the redraw interval ago and nothing is out, otherwise when that time comes, or when the question out lands. A
    // notice of the very version on screen, with nothing out and nothing gathered before it, has nothing to bring.
    #sourceChanged(reportSource: LocalPivotReportSource, changed: PivotSourceChanged): void {
        if (this.#destroyed || reportSource !== this.#reportSource) return;
        const { answer, question } = this.#shown;
        if (!this.#changed && this.#asked === undefined && this.#stale === undefined && changed.sourceVersion !== undefined && question?.reportSource === reportSource
            && answer?.metadata.sourceVersion === changed.sourceVersion) {
            return;
        }
        this.#changed = true;
        this.#changedTo = changed.sourceVersion;
        this.#scheduleRedraw();
    }

    // Asks for the changes gathered no sooner than the redraw interval after the last change reached the screen: at
    // once when that time has passed, else when it comes. Nothing is asked while a question is out: its landing asks.
    #scheduleRedraw(): void {
        if (!this.#changed || this.#asked !== undefined || this.#destroyed) return;
        const ahead = this.#layoutAhead;
        if (showsNothing(ahead) && ahead.filters.length === 0) {
            // Nothing on screen reads the data: the next layout's question is asked of the source as it stands then.
            this.#changed = false;
            this.#disarmRedraw();
            return;
        }
        const now = performance.now();
        const due = this.#lastDataShown === undefined ? now : this.#lastDataShown + this.#redrawInterval;
        if (due <= now) {
            this.#ask(ahead, 'live', []);
            this.#draw();
            return;
        }
        if (this.#redrawTimer !== undefined) return;
        this.#redrawTimer = setTimeout(() => {
            this.#redrawTimer = undefined;
            this.#scheduleRedraw();
        }, due - now);
    }

    #disarmRedraw(): void {
        if (this.#redrawTimer === undefined) return;
        clearTimeout(this.#redrawTimer);
        this.#redrawTimer = undefined;
    }

    // A question landed — answered, refused or failed. One that carried changes of data brought them to the screen, or
    // found them unshowable, and the next waits the redraw interval from now; the changes gathered meanwhile are asked
    // for then, unless the answer is already of the version the latest notice named. `regather` gathers again the changes
    // a question carried and did not bring.
    #landed(question: Question, version: string | undefined, regather: boolean): void {
        if (question.carries) this.#lastDataShown = performance.now();
        if (regather && question.carries) this.#changed = true;
        if (this.#changed && this.#changedTo !== undefined && version === this.#changedTo) this.#changed = false;
        this.#scheduleRedraw();
    }

    #nextRequestId(): string {
        this.#requests++;
        return `ex-pivot-${this.#requests}`;
    }

    #showLayout(layout: PivotLayout, answered: { readonly answer: PivotReportState; readonly question: Question } | undefined, raise: boolean): void {
        const answer = answered?.answer;
        this.#shown = { layout, answer, question: answered?.question };
        // A pending move the report on screen now holds is news no more: a refusal of the pending layout names it no more.
        const deferred = this.#deferred;
        if (deferred !== undefined && deferred.moves.length > 0) {
            this.#deferred = Object.freeze({ layout: deferred.layout, moves: Object.freeze(deferred.moves.filter(move => !holdsMove(layout, move))) });
        }
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
    // move taken against the refused layout went with it, and is named (ADR-0370 section 3). A pending layout, while
    // Defer Layout Update is ticked, is the user's work in the pane, and stays, as the Razor component keeps it.
    #refuse(refusal: PivotReportRefusal, question: Question): void {
        const { message, fields } = this.#sentenceFor(refusal, question);
        this.#sayRefused(message, fields);
    }

    #sayRefused(message: string, fields: readonly string[]): void {
        this.#refusal = message;
        this.#draw();
        this.#options.onRefused?.(Object.freeze({ message, fields: Object.freeze(fields) }));
    }

    #sentenceFor(refusal: PivotReportRefusal, question: Question): { readonly message: string; readonly fields: readonly string[] } {
        const held = heldMoves(question);
        const named = heldFields(held);
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
        this.#placing?.();
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
            findGivenUp: why => { this.#findGivenUp(answer, why); },
            words: () => this.#words,
        });
        this.#drawGrid();
        if (question.toggled !== undefined) this.#placeToggled(question.toggled);
    }

    // The toggled Item keeps the Focus, as it does in Excel (ADR-0050 item 4): its label cell is selected among the
    // first rows the grid holds of the report it was toggled into, on the toggled row's path, as the Razor component
    // finds it among the rows drawn, and under the order those rows came in. A press, a key or a wheel the user turns
    // in the grid after the toggle, while its question is out or its rows are on their way, is newer than the toggle,
    // and the toggle places nothing over it (spine rule 6); a modifier pressed alone, or a key composing text, is no
    // such gesture. Nor does the toggle place anything after a later move, or once another report is shown. A placement the grid refuses leaves the Selection it dropped with the
    // old report, and the toggled Item is found in no other rows than those first ones: no place, rather than a
    // late one over what the user did since.
    #placeToggled(toggled: ToggledItem): void {
        const rows = this.#rows;
        const grid = this.#grid;
        if (rows === undefined || grid === undefined) return;
        const place = (): void => {
            if (this.#gestures !== toggled.gestures) {
                stop();
                return;
            }
            const state = rows.snapshot();
            if (state.window.length === 0) return;
            stop();
            const at = toggledCell(state.window, state.windowStart, toggled);
            if (at === undefined) return;
            grid.placeSelection(Object.freeze({ topRow: at.row, leftColumn: at.column, rowCount: 1, columnCount: 1 }), at, state.rowSequenceVersion);
        };
        const unsubscribe = rows.subscribe(place);
        const stop = (): void => {
            unsubscribe();
            if (this.#placing === stop) this.#placing = undefined;
        };
        this.#placing = stop;
        place();
    }

    // The source answered a Window from another report than the one shown: the data moved on under it, and the
    // report on screen is asked again under its layout, unless something newer is asked already.
    #reportMoved(answer: PivotReportState): void {
        // A Stale Report is not asked again for it: the newest data is what it could not show, and asking again would
        // break the same cap once per interval while the reader scrolls. Retry and the source's next notice ask.
        if (this.#destroyed || this.#shown.answer !== answer || this.#asked !== undefined || this.#stale !== undefined) return;
        // A change of data like any other: asked for no sooner than the redraw interval after the last one shown.
        this.#changed = true;
        this.#scheduleRedraw();
    }

    // Figures handed in replace the ones shown only when they differ from the ones handed before: an update that hands
    // the same again keeps the figures menu's choice. True when the figures shown moved.
    #adoptSummaryFigures(handed: readonly SummaryFigure[] | undefined): boolean {
        if (sameFigures(handed, this.#summaryFiguresHanded)) return false;
        this.#summaryFiguresHanded = handed;
        const shown = handed ?? defaultSummaryFigures;
        if (sameFigures(shown, this.#summaryFigures)) return false;
        this.#summaryFigures = shown;
        return true;
    }

    // The figures menu's choice, which the report holds and raises (ADR-0130). Held in a field: its identity reaches the
    // grid.
    readonly #summaryFiguresChosen = (figures: readonly SummaryFigure[]): void => {
        if (this.#destroyed) return;
        this.#summaryFigures = figures;
        this.#drawGrid();
        this.#options.onSummaryFiguresChanged?.(figures);
    };

    // The Selection Summary as it stands, raised to the Consumer's handler as the options now hold it. Held in a field,
    // so a handler that is a new function at each update does not draw the grid again.
    readonly #selectionSummaryChanged = (summary: SelectionSummary): void => {
        this.#options.onSelectionSummaryChanged?.(summary);
    };

    #drawGrid(): void {
        const answer = this.#shown.answer;
        const rows = this.#rows;
        const question = this.#shown.question;
        if (answer === undefined || rows === undefined || question === undefined) return;
        const { locale } = question;
        const { columns, headerGroups, pinnedColumnCount } = this.#columns.of(answer.metadata, this.#userWidths);
        this.#gridColumns = columns;
        this.#valueColumnIndexes = new Map(answer.metadata.valueColumns.map((column, index) => [column.name, index]));
        const { rowHeight, density, cellMetrics, presentationDefaults } = this.#options;
        const gridChrome = this.#chrome?.grid;
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
            contextCommands: this.#contextCommands,
            // Find over the report as shown, and its painted matches marked by the rule it finds by (ADR-0332).
            findMatch: reportMatch,
            onRefused: this.#gridRefused,
            copyAnswerAsync: this.#copyReport,
            commandLabel: this.#commandLabel,
            cellChangedAt: this.#cellChangedAt,
            changeHighlightDuration: this.#changeHighlightMs,
            showSelectionSummary: this.#options.showSelectionSummary !== false,
            summaryFigures: this.#summaryFigures,
            onSummaryFiguresChanged: this.#summaryFiguresChosen,
            onSelectionSummaryChanged: this.#selectionSummaryChanged,
            ...(gridChrome === undefined ? {} : { chrome: gridChrome }),
            ...(rowHeight === undefined ? {} : { rowHeight }),
            ...(density === undefined ? {} : { density }),
            ...(cellMetrics === undefined ? {} : { cellMetrics }),
            ...(presentationDefaults === undefined ? {} : { presentationDefaults }),
        };
        if (this.#grid === undefined) {
            // Made standing in the sheet, inside the pivot's box: the grid reads its Input Root once, as it is made, and
            // on an element standing nowhere it would hear only itself, not the Field List, the toolbar or the popups
            // (ADR-0240 section 6). The draw that follows keeps it there, or puts the report's notice in its place.
            if (this.#gridRoot.parentNode !== this.#sheet) this.#sheet.prepend(this.#gridRoot);
            this.#grid = this.#mountGrid(this.#gridRoot, options);
        } else {
            this.#grid.update(options);
        }
    }

    // A Find step over the report that cannot go on is said on the Pivot Toolbar, while that report is the one shown:
    // a step over a report since replaced is the grid's to refuse, as its source is no longer the grid's (ADR-0332 §1).
    #findGivenUp(answer: PivotReportState, why: ReportFindGivenUp): void {
        if (this.#destroyed || this.#shown.answer !== answer) return;
        switch (why.kind) {
            case 'notHeld': this.#refusal = this.#words.word('find-report-not-held'); break;
            case 'refused': this.#refusal = this.#words.fill('source-refused', why.refusal.message); break;
            case 'invalid': this.#refusal = this.#words.word('find-invalid-answer'); break;
            default: unreachable(why);
        }
        this.#draw();
    }

    // A copy is the report source's, of the report on screen, as the Razor component's is (ADR-0152): each cell's text
    // and its locale-free form, and a number column Item's header in full in the HTML flavour (ADR-0005's note of
    // 2026-10-13). Every refusal is said in ExPivot's words, a source's own inside one (ADR-0060). Held in a field: its
    // identity reaches the grid.
    readonly #copyReport = async (request: GridCopyRequest, signal: AbortSignal): Promise<GridCopyAnswer> => {
        // The whole question is captured before awaiting: a new Window never rebinds this gesture.
        const metadata = this.#shown.answer?.metadata;
        const source = this.#shownReportSource;
        const rows = this.#rows;
        if (metadata === undefined || source === undefined || rows === undefined || request.rowSequenceVersion !== rows.snapshot().rowSequenceVersion) {
            return refuse(this.#words.word('data-changed'));
        }
        // A Stale Report shows the data as of its time, and says so beside it; pasted elsewhere, the numbers would lose
        // that and look current. So Copy is refused, saying as of when, until Retry brings the newest (ADR-0067).
        if (this.#stale !== undefined) return refuse(this.#words.fill('copy-stale-report', this.#asOf()));
        const { plan } = request;
        const version = metadata.version;
        const answer = await source.copy(Object.freeze({
            version,
            columns: Object.freeze(this.#gridColumns.map(column => column.name)),
            ranges: Object.freeze(plan.segments.map(range => Object.freeze({
                top: range.topRow, left: range.leftColumn, bottom: range.topRow + range.rowCount - 1, right: range.leftColumn + range.columnCount - 1,
            }))),
        }), signal);
        if (answer.version !== version) return refuse(this.#words.word('copy-another-version'));
        if (answer.refusal !== undefined) return refuse(this.#words.fill('source-refused', answer.refusal.message));
        if (answer.blocks.length !== plan.segments.length) return refuse(this.#words.word('copy-missing-range'));
        const whole = plan.segments.every((range, i) => {
            const block = elementAt(answer.blocks, i);
            return block.rows.length === range.rowCount && block.headers.length === range.columnCount
                && (block.headerRaws === undefined || block.headerRaws.length === range.columnCount)
                && block.rows.every(cells => cells.length === range.columnCount);
        });
        if (!whole) return refuse(this.#words.word('copy-incomplete-range'));
        // The block a position lies in: the one the last position lay in, which assembly mostly asks again, or the one
        // found by halves along the plan's order, as the Razor component finds it.
        const vertical = plan.orientation === 'vertical';
        let hint = 0;
        const blockAt = (row: number, column: number): number => {
            const at = vertical ? row : column;
            const within = (index: number): number => {
                const range = elementAt(plan.segments, index);
                const start = vertical ? range.topRow : range.leftColumn;
                const end = start + (vertical ? range.rowCount : range.columnCount) - 1;
                return at < start ? -1 : at > end ? 1 : 0;
            };
            if (within(hint) === 0) return hint;
            let low = 0;
            let high = plan.segments.length - 1;
            while (low <= high) {
                const middle = low + Math.floor((high - low) / 2);
                const side = within(middle);
                if (side === 0) {
                    hint = middle;
                    return middle;
                }
                if (side < 0) high = middle - 1;
                else low = middle + 1;
            }
            throw new Error('The copied position is outside the approved plan.');
        };
        const cellAt = (row: number, column: number): PivotReportCopyCell => {
            const index = blockAt(row, column);
            const range = elementAt(plan.segments, index);
            return elementAt(elementAt(elementAt(answer.blocks, index).rows, row - range.topRow), column - range.leftColumn);
        };
        const headerBlock = (column: number): number => {
            const index = plan.segments.findIndex(range => column >= range.leftColumn && column < range.leftColumn + range.columnCount);
            if (index < 0) throw new Error('The copy header is outside the approved plan.');
            return index;
        };
        const flavours = assembleCopy(plan, {
            displayAt: (row, column) => cellAt(row, column).text,
            // A blank carries no raw form, and is written empty, as the Razor component writes it.
            // oxlint-disable-next-line ex-grid/no-stand-in-default -- ADR-0005: a blank cell's HTML flavour is empty
            rawAt: (row, column) => cellAt(row, column).raw ?? '',
            ...(request.withHeaders ? {
                headerAt: (column: number) => {
                    const index = headerBlock(column);
                    return elementAt(elementAt(answer.blocks, index).headers, column - elementAt(plan.segments, index).leftColumn);
                },
                headerRawAt: (column: number) => {
                    const index = headerBlock(column);
                    return elementAt(answer.blocks, index).headerRaws?.[column - elementAt(plan.segments, index).leftColumn];
                },
            } : {}),
        });
        return Object.freeze({ kind: 'write', text: flavours.text, html: flavours.html });
    };

    // The report grid's refusals: Ctrl+H on a report, which is not written, is said on the Pivot Toolbar in Excel's
    // words (ADR-0332 §2). The refusals the find panel shows itself are left to it. Held in a field: its identity
    // reaches the grid.
    readonly #gridRefused = (refusal: GridRefusal<PivotDisplayRow>): void => {
        if (this.#destroyed || refusal.kind !== 'find' || refusal.reason !== 'replaceUnavailable') return;
        this.#refusal = this.#words.word('replace-unavailable');
        this.#draw();
    };

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
        const { answer, question } = this.#shown;
        const rows = this.#rows;
        if (answer === undefined || question === undefined || rows === undefined) return;
        const state = rows.snapshot();
        const row = state.window[cell.row - state.windowStart];
        if (row === undefined) return;
        const labels = answer.metadata.labelColumns.length;
        if (cell.column >= labels) {
            this.#showDetails(answer, question, row, cell.column - labels);
            return;
        }
        // A double click on an outer Item's label expands or collapses it, as in Excel.
        const toggle = elementAt(row.labels, cell.column).toggle;
        if (toggle !== undefined) this.#toggled(toggle, row);
    }

    // Show Details (ADR-0059, ADR-0063): the records behind a value cell, asked of the Pivot Source the report came
    // from under its Source Version, so they add up to the cell. An empty cell has none. Resolved now, from the cell
    // the user acted on: the question names its Items and the Source Version, so a collapse or a sort that follows
    // leaves it the same question.
    #showDetails(answer: PivotReportState, question: Question, row: PivotDisplayRow, valueColumn: number): void {
        if (row.values[valueColumn] === undefined) return;
        const { metadata } = answer;
        const words = this.#words;
        const fields = question.source.fields;
        const options = { locale: question.locale, ...(words.label === undefined ? {} : { label: words.label }) };
        // Each Item with its field's caption and its label, by the engine's rule for its field (ADR-0060).
        const items = (path: readonly PivotFieldItem[]): readonly PivotDetailItem[] => Object.freeze(path.map(step => {
            const declared = fields.find(field => field.name === step.field);
            if (declared === undefined) throw new Error(`Internal: the report's field '${step.field}' is not its source's.`);
            const [info] = itemsOfPage(PivotItemPage.of(metadata.sourceVersion, [step.item], 1), metadata.layout, declared, options);
            if (info === undefined) throw new Error('Internal: an Item listed is labelled.');
            return Object.freeze({ field: declared.caption, item: info.label });
        }));
        const rowItems = items(row.rowPath);
        const column = elementAt(metadata.valueColumns, valueColumn);
        const columnItems = items(column.columnPath);
        const valueField = valueFieldAt(metadata, row, valueColumn);
        const path = [...rowItems, ...columnItems].map(item => item.item);
        const details = detailsEvent(question.source, pivotReportDetailsQuery(metadata, row, valueColumn), {
            title: words.fill('details-title', path.length === 0 ? words.word('grand-total') : path.join(' / ')),
            rowItems,
            columnItems,
            valueField: valueField < 0 ? undefined : elementAt(metadata.valueCaptions, valueField),
        });
        const heard = this.#options.onShowDetails;
        if (heard !== undefined) {
            this.#detailsRaised.add(details);
            heard(details);
            return;
        }
        this.#details.open(details, this.#options.detailsView ?? 'tab', question.locale); // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0059: a tab at the report's foot unless the Consumer asks for the dialog, as the Razor component's DetailsView
        this.#slotErrors.push(...this.#details.takeRefused());
        this.#throwRefused();
    }

    // The report's commands in the grid's Context Menu (ADR-0036, ADR-0059), for a row of the report on screen.
    readonly #contextCommands = (context: GridContextMenuContext<PivotDisplayRow>): readonly GridCommand[] => {
        const { answer, question } = this.#shown;
        const rows = this.#rows;
        if (this.#destroyed || answer === undefined || question === undefined || rows === undefined || !rows.snapshot().window.includes(context.row)) {
            return [];
        }
        return reportMenuCommands({
            metadata: answer.metadata,
            row: context.row,
            column: context.column,
            report: this.#layoutAhead,
            pane: this.#paneLayout,
            fieldListShown: this.#fieldListShown,
            captionOf: field => this.#captionOf(field),
        }, {
            edit: (edit, toggled) => { this.#reportEdit(edit, toggled === undefined ? undefined : this.#toggledItemOf(context.row, toggled)); },
            showDetails: valueColumn => { this.#showDetails(answer, question, context.row, valueColumn); },
            valueFieldSettings: (valueField, holds) => { this.#openFromReport(valueField, holds); },
            setFieldListShown: shown => { this.#setFieldListShown(shown); },
        });
    };

    // The words of the grid's commands (ADR-0036): the Consumer's, then the pivot's own for its commands; the grid's
    // own commands keep the grid's English where the Consumer gives none. Made again with the Consumer's words, so the
    // grid, which holds words by the function they came from, reads them again.
    #commandLabelOf(label: PivotLabel | undefined): (id: string) => string | undefined {
        return id => {
            const own = label?.(id);
            if (own !== undefined) return own;
            const caption = captionOfRemove(id);
            if (caption !== undefined) return this.#words.fill(removeNamedFieldId, caption);
            return pivotWordIds.has(id) ? pivotWord(id, undefined) : undefined;
        };
    }

    // Value Field Settings… from the report opens in the pane, under the Value Field's entry, showing the Field List.
    // Opened only while the pane holds the Value Field the cell showed there.
    #openFromReport(valueField: number, holds: (pane: PivotLayout) => boolean): void {
        const pane = this.#paneLayout;
        const value = pane.values[valueField];
        if (value === undefined || !holds(pane)) return;
        this.#setFieldListShown(true);
        this.#openPanelAt({ entry: { area: 'values', index: valueField }, field: value.field, against: pane }, 'value-field-settings');
    }

    // An outer Item's button pressed, or its label double clicked: the Item is collapsed or expanded (ADR-0063).
    // The press does what the button it was taken on said, against the report's layout, the one the newest question
    // asks for; where that layout no longer holds the Item as the button painted it, a press already moved it, or
    // its field left the layout, and the press is refused rather than turned into its opposite (spine rule 6). Two
    // presses of one painted button collapse once, as the Razor component's do. A pending layout follows it.
    // An Item toggled on a row, as it keeps the Focus in the report the toggle brings.
    #toggledItemOf(row: PivotDisplayRow, toggled: PivotFieldItem): ToggledItem {
        const level = row.rowPath.findIndex(step => step.field === toggled.field && itemKeysEqual(step.item, toggled.item));
        if (level < 0) throw new Error(`Internal: the row toggled holds no '${toggled.field}' Item there.`);
        return Object.freeze({ path: Object.freeze(row.rowPath.slice(0, level + 1)), gestures: this.#gestures });
    }

    // The Item keeps the Focus in the report the press brings.
    #toggled(toggle: PivotToggle, row: PivotDisplayRow): void {
        if (this.#destroyed) return;
        const now = this.#layoutAhead;
        const placed = now.rows.find(placement => placement.field === toggle.field);
        if (placed === undefined || isItemCollapsed(placed, toggle.item) !== toggle.isCollapsed) return;
        this.#reportEdit(layout => (layout.rows.some(placement => placement.field === toggle.field)
            ? setCollapsed(layout, toggle.field, toggle.item, !toggle.isCollapsed)
            : undefined), this.#toggledItemOf(row, toggle));
    }

    // The report asked again of the Pivot Source, under the pane's layout. A move of the user's still out is asked again
    // with it, and stays the user's.
    #refresh(): void {
        // Owed until an answer from the Pivot Source asked again lands: a move made meanwhile, or a layout with nothing
        // to compute, does not answer it from the data before it.
        this.#refreshOwed = true;
        this.#refusal = undefined;
        this.#askCounts();
        this.#ask(this.#layoutAhead, 'data', []);
        this.#draw();
    }

    // ---- Menus and panels (ADR-0061) -------------------------------------------------------------------

    #toggleLayoutMenu(): void {
        if (this.#open?.kind === 'layout') {
            this.#close('opener');
            return;
        }
        this.#anchorFor('layout', 'chrome.toolbar hands in no anchor \'layout\': hand context.anchor(\'layout\') the element around Layout ▾');
        this.#openSurface({ kind: 'layout', popup: this.#menuFrame('toolbar-end') });
    }

    // A popup of the Pivot Toolbar's opens only in an anchor that stands: one that would stand nowhere, behind a
    // backdrop that takes every press, is refused, naming what the Chrome did not hand in (ADR-0390).
    #anchorFor(name: string, missing: string): void {
        if (!this.#parts.anchored(name)) throw new Error(`An ExPivot's ${missing} (ADR-0390).`);
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
        if (this.#fieldListSlotted) {
            this.#anchorFor(entryKey(entry), `chrome.fieldList hands in no anchor for the entry '${entryKey(entry)}': hand context.anchor(entry.key) the element around its button`);
        }
        this.#openSurface({ kind: 'entry', entry, field, against: pane, popup: this.#menuFrame('field-list') });
    }

    #menuFrame(place: 'field-list' | 'toolbar-end'): PivotMenu {
        const escape = (): void => { this.#close('opener'); };
        const mount = this.#chrome?.menu;
        const frame = mount === undefined
            ? new PivotMenuFrame(this.#root.ownerDocument, place, escape)
            : new SlottedMenuFrame(this.#root.ownerDocument, place, mount, this.#words, escape);
        this.#closeOnLeaving(frame.element);
        return frame;
    }

    // The keyboard leaving what is open for another control — Tab past its last command — closes it, so the backdrop of
    // a surface opened from the Pivot Toolbar takes no press meant for that control, and a field menu does not stand
    // open over the pane once the keyboard left the pivot. A field menu's entry, which toggles it, is inside its bounds.
    #closeOnLeaving(frame: HTMLElement): void {
        frame.addEventListener('focusout', event => {
            const to = event.relatedTarget;
            const open = this.#open;
            // A popup of a Chrome's contents may stand outside the frame, and take the keyboard while it is open.
            if (open?.popup.element !== frame || !(to instanceof Node) || open.popup.innerPopupOpen?.() === true) return;
            if (!this.#within(open, to)) this.#close('stay');
        });
    }

    // Whether `node` is inside what is open. A field menu's bounds are its entry: the anchor around the frame, and the
    // entry's button, which a Chrome may draw beside the anchor rather than in it, as a design system draws a menu's
    // opener.
    #within(open: OpenSurface, node: Node): boolean {
        const frame = open.popup.element;
        if (open.kind !== 'entry') return frame.contains(node);
        const anchor = frame.parentNode;
        return (anchor !== null && anchor.contains(node)) || this.#parts.controlContains(entryKey(open.entry), node);
    }

    // A report filter's button opens Filter… under it, over the report, and closes the one standing under it.
    #toggleBand(field: string): void {
        const open = this.#open;
        if (open?.kind === 'band' && open.field === field) {
            this.#close('opener');
            return;
        }
        this.#anchorFor(bandName(field), `chrome.reportFilters hands in no anchor for '${field}': hand context.anchor('${field}') the element around its button`);
        const report = this.#layoutAhead;
        const handlers = {
            // OK's Hidden Items are the report's next, taken against the layout Filter… was opened on (spine rule 6),
            // and reach a pending layout that holds the field too.
            apply: (layout: PivotLayout) => {
                this.#finish(surface, () => {
                    if (report === this.#layoutAhead) {
                        const hidden = defined(layout.filters.find(placement => placement.field === field), `the report filter '${field}'`).hiddenItems;
                        this.#reportEdit(now => (placementOf(now, field) === undefined ? undefined : setHiddenItems(now, field, hidden, []).layout));
                    }
                    return undefined;
                });
            },
            cancel: () => { if (this.#open === surface) this.#close('opener'); },
            items: () => this.#itemsOf(field),
            search: (search: string, signal: AbortSignal) =>
                Promise.resolve().then(() => searchItems(field, search, defined(this.#itemsUnder(field), 'a report to search the Items of'), signal)),
        };
        this.#items.retry(field);
        const popup = itemFilterPanel(this.#root.ownerDocument,
            { layout: report, field, caption: this.#captionOf(field), place: 'toolbar-start', locale: this.#locale, words: this.#words }, handlers,
            this.#chrome?.itemFilter);
        this.#closeOnLeaving(popup.element);
        const surface: OpenSurface = { kind: 'band', field, against: report, popup };
        this.#openSurface(surface);
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
        if (keyboard === 'opener') this.#returnKeyboard(open, this.#entryNow(open));
    }

    // Takes what is open off the page, without drawing.
    #takeDown(): OpenSurface | undefined {
        const open = this.#open;
        this.#open = undefined;
        if (open === undefined) return undefined;
        closed(open);
        this.#parts.unplace(open.popup.element);
        open.popup.element.remove();
        return open;
    }

    // The keyboard goes back to the button that opened the menu: Layout ▾, a report filter's button, or the entry `entry`
    // names, where the field the menu was for stands now; a field taken out of the Areas hands it to its tick box in
    // the list.
    #returnKeyboard(open: OpenSurface, entry: PivotEntry | undefined): void {
        if (open.kind === 'layout') this.#parts.focus('layout');
        else if (open.kind === 'band') this.#focusBand(open.field);
        else if (entry !== undefined) this.#fieldList.focusEntry(entry);
        else this.#fieldList.focusField(open.field);
    }

    // A report filter's button; once the field has left Filters, its tick box in the Field List, or the toggle that
    // shows the list.
    #focusBand(field: string): void {
        if (this.#layoutAhead.filters.some(placement => placement.field === field)) this.#parts.focus(bandName(field));
        else if (this.#fieldListShown) this.#fieldList.focusField(field);
        else this.#parts.focus('field-list');
    }

    // Where the field a menu or panel in the Field List was for stands in the pane's layout now; none for what opened
    // from the Pivot Toolbar.
    #entryNow(open: OpenSurface): PivotEntry | undefined {
        return open.kind === 'entry' || open.kind === 'panel' ? entryOfField(this.#paneLayout, open.field, open.entry) : undefined;
    }


    // Closes what is open, does what its command or its OK says, and hands the keyboard back once the pivot is drawn
    // after it. The popup leaves the page once the keyboard has gone back, so the Layout menu's never passes it
    // through the page's body (ADR-0061). Pressed once what it stood in has closed, it does nothing.
    #finish(open: OpenSurface, run: () => PivotEntry | undefined): void {
        if (this.#open !== open) return;
        this.#open = undefined;
        closed(open);
        this.#returnKeyboard(open, run());
        this.#parts.unplace(open.popup.element);
        open.popup.element.remove();
    }

    #command(open: OpenSurface, command: Omit<PivotMenuCommand, 'invoke'>, run: () => PivotEntry | undefined): PivotMenuCommand {
        return Object.freeze({ ...command, invoke: () => { this.#finish(open, run); } });
    }

    // The Layout menu (ADR-0061): Excel's Design tab choices, the current one of each group marked, and one that
    // would change nothing disabled, read from the report's layout. A choice is the report's, and reaches a pending
    // layout too.
    #layoutCommands(open: OpenSurface): readonly PivotMenuCommand[] {
        const report = this.#layoutAhead;
        const words = this.#words;
        return pivotLayoutChoices.map(choice => {
            const id = layoutChoiceId(choice);
            const heading = layoutHeadings.get(choice);
            return this.#command(open, {
                id,
                label: words.word(id),
                enabled: layoutChoiceChanges(report, choice),
                checked: isLayoutChosen(report, choice),
                ...(heading === undefined ? {} : { heading: words.word(heading) }),
            }, () => {
                this.#reportEdit(now => chooseLayout(now, choice));
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
        if (this.#open === open) this.#openPanelAt(open, panel);
    }

    // A panel opened under a field's entry in the pane: from its menu, or from the report's Context Menu.
    #openPanelAt(open: AtEntry, panel: EntryPanel): void {
        const document = this.#root.ownerDocument;
        const { entry, against } = open;
        // The panel's OK and Cancel are called once it stands, and act only while it does.
        const handlers = {
            // OK's layout is the pane's next, taken against the layout the panel was opened on (spine rule 6).
            apply: (layout: PivotLayout) => {
                this.#finish(surface, () => {
                    this.#edited({ layout, against, moved: undefined });
                    return this.#entryNow(surface);
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
                popup = itemFilterPanel(document, { layout: against, field, caption: this.#captionOf(field), place: 'field-list', locale: this.#locale, words }, {
                    ...handlers,
                    items: () => this.#itemsOf(field),
                    // Asked from the panel's draw, it says a problem in a later turn, never in the middle of that draw.
                    search: (search, signal) => Promise.resolve().then(() => searchItems(field, search, defined(this.#itemsUnder(field), 'a report to search the Items of'), signal)),
                }, this.#chrome?.itemFilter);
                break;
            }
            case 'field-settings':
                popup = fieldSettingsPanel(document, {
                    layout: against,
                    placement: elementAt(placementsIn(against, entry.area), entry.index),
                    caption: this.#entryCaption(entry, captions),
                    valueCaptions: captions,
                    words,
                }, handlers, this.#chrome?.fieldSettings);
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
                }, handlers, this.#chrome?.valueFieldSettings);
                break;
            default:
                unreachable(panel);
        }
        const surface: EntrySurface = { kind: 'panel', entry, field: open.field, against, popup };
        this.#openSurface(surface);
    }

    // Each draw draws the open menu again from the pane's layout. A field's menu or panel, or a report filter's Filter…,
    // goes when the pane leaves the layout it was opened on, since its entry may now be another field's and its draft is
    // taken against that layout; one in the Field List goes too when the list is hidden. The keyboard it held goes to
    // the entry or the button its field stands at now.
    #drawMenu(valueCaptions: readonly string[] | undefined): void {
        const open = this.#open;
        const inList = open?.kind === 'entry' || open?.kind === 'panel';
        const holding = open?.kind === 'band' ? this.#layoutAhead : this.#paneLayout;
        if (open !== undefined && open.kind !== 'layout' && (open.against !== holding || (inList && !this.#fieldListShown))) {
            const held = open.popup.element.contains(this.#root.ownerDocument.activeElement);
            this.#takeDown();
            // Hidden with the Field List, the keyboard goes to the toggle that shows it again.
            if (held && (this.#fieldListShown || !inList)) this.#keyboardAfterDraw = open;
            else if (held) this.#parts.focus('field-list');
        }
        const shown = this.#open;
        this.#drawToolbar();
        if (shown?.kind === 'layout') {
            shown.popup.draw(this.#words.word('layout-menu'), this.#layoutCommands(shown));
            this.#parts.place('layout', shown.popup.element);
        }
        this.#drawBand();
        if (shown?.kind === 'band') this.#parts.place(bandName(shown.field), shown.popup.element);
        // Under what is open from the Pivot Toolbar, a press anywhere else closes it.
        if (shown?.kind === 'layout' || shown?.kind === 'band') {
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
        if (shown?.kind === 'panel' || shown?.kind === 'band') shown.popup.redraw?.();
    }

    // Shows a standing part's contents by `mount`; answers whether they stand. Contents that cannot be drawn leave the
    // built-in markup in their place, and are refused by name once the pivot has drawn.
    #showStanding<View, Context>(slot: StandingSlot<View, Context>, mount: PivotChromeSlot<Context>,
        context: (...made: [...Parameters<Parameters<StandingSlot<View, Context>['show']>[1]>, refs: PartRefs]) => Context): boolean {
        // Each mount hands its controls and anchors in by refs of its own.
        return this.#answered(slot.show(mount, (shared, input) => context(shared, input, this.#parts.refs()), id => this.#words.word(id)));
    }

    // Whether a standing part's contents stand, as `answer` says; an error it answers is the draw's to throw.
    #answered(answer: { readonly shown: boolean; readonly error?: Error }): boolean {
        if (answer.error !== undefined) this.#slotErrors.push(answer.error);
        return answer.shown;
    }

    // The Pivot Toolbar (ADR-0061): the built-in markup's, or a Chrome's in its place, drawn from the same view; the
    // refusal of the user's last change is the toolbar's to say. The built-in markup's controls are handed in again
    // each time it stands, as a Chrome's are when it draws them.
    #drawToolbar(): void {
        const words = this.#words;
        const view = this.#toolbarSlot.viewOf(Object.freeze({
            layoutMenu: Object.freeze({ label: words.word('layout-menu'), open: this.#open?.kind === 'layout' }),
            ...(this.#options.source.features.canRefresh ? { refresh: Object.freeze({ label: words.word('refresh') }) } : {}),
            fieldList: Object.freeze({ label: words.word('field-list-toggle'), shown: this.#fieldListShown }),
            reportFilters: this.#layoutAhead.filters.length > 0,
            ...(this.#refusal === undefined ? {} : { refusal: this.#refusal }),
        }));
        const mount = this.#chrome?.toolbar;
        // A control taken away while it holds the keyboard hands it to the Field List's toggle, so it stays in the pivot.
        const refreshHeld = view.refresh === undefined && this.#parts.holds('refresh');
        // The slot stands in the page as the contents mount, so plain script hands in controls already in it; contents
        // refused once leave the built-in toolbar standing, and the keyboard on it.
        if (mount !== undefined && !this.#toolbarSlot.refuses(mount)) swap(this.#toolbar, this.#toolbarSlot.element);
        const shown = mount !== undefined && this.#showStanding(this.#toolbarSlot, mount, (shared, input, refs) => ({
            ...shared,
            toggleLayoutMenu: input(() => { this.#toggleLayoutMenu(); }),
            refresh: input(() => { if (this.#options.source.features.canRefresh) this.#refresh(); }),
            toggleFieldList: input(() => { this.#setFieldListShown(!this.#fieldListShown); }),
            control: name => refs.control(checkedName(name, ['layout', 'refresh', 'field-list'], 'toolbar')),
            anchor: name => refs.anchor(checkedName(name, ['layout', 'report-filters'], 'toolbar')),
        }));
        if (shown) {
            this.#notice.remove();
        } else {
            this.#toolbarSlot.hide();
            swap(this.#toolbarSlot.element, this.#toolbar);
            for (const [name, control] of [['layout', this.#layoutButton], ['refresh', this.#refreshButton], ['field-list', this.#fieldListToggle]] as const) {
                this.#parts.handInControl(name, control);
            }
            this.#parts.handInAnchor('layout', this.#layoutAnchor);
            this.#parts.handInAnchor('report-filters', this.#toolbarStart);
            this.#layoutLabel.textContent = view.layoutMenu.label;
            this.#layoutButton.setAttribute('aria-expanded', view.layoutMenu.open ? 'true' : 'false');
            this.#fieldListToggle.textContent = view.fieldList.label;
            this.#fieldListToggle.setAttribute('aria-pressed', view.fieldList.shown ? 'true' : 'false');
            this.#refreshButton.textContent = words.word('refresh');
            if (view.refresh === undefined) this.#refreshButton.remove();
            else if (this.#refreshButton.parentNode === null) this.#fieldListToggle.before(this.#refreshButton);
            const refusal = view.refusal ?? ''; // oxlint-disable-line ex-grid/no-stand-in-default -- no refusal is an empty notice, which takes no room
            if (this.#notice.textContent !== refusal) this.#notice.textContent = refusal;
            this.#notice.hidden = view.refusal === undefined;
            if (this.#notice.parentNode !== this.#report) this.#toolbar.after(this.#notice);
        }
        if (refreshHeld) this.#parts.focus('field-list');
    }

    // The report filter band, on the Pivot Toolbar's left (ADR-0061): each field in Filters, its caption, and a button
    // that says what it shows and opens Filter… under it, over the report. It stands while Filters holds a field, in
    // the toolbar's anchor for it; the built-in markup's or a Chrome's, drawn from the same view.
    #drawBand(): void {
        const words = this.#words;
        const open = this.#open;
        const view = this.#bandSlot.viewOf(Object.freeze({
            name: words.word('report-filters'),
            filters: Object.freeze(this.#layoutAhead.filters.map(placement => {
                const caption = this.#captionOf(placement.field);
                return Object.freeze({
                    field: placement.field,
                    caption,
                    summary: this.#bandSummary(placement),
                    filtered: placement.hiddenItems.length > 0,
                    open: open?.kind === 'band' && open.field === placement.field,
                    buttonLabel: words.fill('filter-of', caption),
                });
            })),
        }));
        const mount = this.#chrome?.reportFilters;
        const shown = mount !== undefined && this.#showStanding(this.#bandSlot, mount, (shared, input, refs) => ({
            ...shared,
            toggle: input((field: string) => { if (this.#layoutAhead.filters.some(placement => placement.field === field)) this.#toggleBand(field); }),
            control: field => refs.control(bandName(checkedField(field))),
            anchor: field => refs.anchor(bandName(checkedField(field))),
        }));
        const band = shown ? this.#bandSlot.element : this.#band;
        if (shown) {
            this.#band.remove();
            for (const field of this.#bandFilters.keys()) this.#forgetBandFilter(field);
            this.#bandSlot.element.setAttribute('aria-label', view.name);
        } else {
            this.#bandSlot.hide();
            this.#bandSlot.element.remove();
            this.#drawBuiltInBand(view);
        }
        if (view.filters.length === 0) {
            band.remove();
            this.#parts.place('report-filters', undefined);
        } else {
            this.#parts.place('report-filters', band);
        }
    }

    #drawBuiltInBand(view: PivotReportFiltersView): void {
        const document = this.#root.ownerDocument;
        const drawn: HTMLElement[] = [];
        const kept = new Set<string>();
        for (const shown of view.filters) {
            const { field } = shown;
            let band = this.#bandFilters.get(field);
            if (band === undefined) {
                band = this.#bandFilter(document, field);
                this.#bandFilters.set(field, band);
            }
            this.#parts.handInControl(bandName(field), band.button);
            this.#parts.handInAnchor(bandName(field), band.anchor);
            kept.add(field);
            if (band.caption.textContent !== shown.caption) band.caption.textContent = shown.caption;
            if (band.summary.textContent !== shown.summary) band.summary.textContent = shown.summary;
            band.button.classList.toggle('ex-pivot-filter-active', shown.filtered);
            band.button.setAttribute('aria-expanded', shown.open ? 'true' : 'false');
            band.button.setAttribute('aria-label', shown.buttonLabel);
            drawn.push(band.filter);
        }
        for (const field of this.#bandFilters.keys()) {
            if (!kept.has(field)) this.#forgetBandFilter(field);
        }
        this.#band.setAttribute('aria-label', view.name);
        const children = this.#band.children;
        if (drawn.length !== children.length || drawn.some((filter, i) => children[i] !== filter)) this.#band.replaceChildren(...drawn);
    }

    // A built-in report filter that no longer stands: its elements are dropped, and taken back from the parts.
    #forgetBandFilter(field: string): void {
        const band = this.#bandFilters.get(field);
        if (band === undefined) return;
        this.#bandFilters.delete(field);
        this.#parts.forget(bandName(field), band.button);
        this.#parts.forget(bandName(field), band.anchor);
    }

    #bandFilter(document: Document, field: string): BandFilter {
        const filter = element(document, 'div', 'ex-pivot-filter');
        const caption = element(document, 'span', 'ex-pivot-filter-caption');
        const anchor = element(document, 'span', 'ex-pivot-filter-anchor');
        const button = element(document, 'button', 'ex-pivot-filter-button');
        button.type = 'button';
        button.setAttribute('data-ex-claims', 'click');
        button.setAttribute('aria-haspopup', 'dialog');
        const summary = element(document, 'span', 'ex-pivot-filter-summary');
        const arrow = element(document, 'span', 'ex-pivot-entry-arrow');
        arrow.setAttribute('aria-hidden', 'true');
        arrow.textContent = '▾';
        button.append(summary, arrow);
        button.addEventListener('click', () => { this.#toggleBand(field); });
        anchor.append(button);
        filter.append(caption, anchor);
        return { filter, caption, anchor, button, summary };
    }

    // What a report filter shows, as Excel shows a page field: (All) while it hides nothing, the one Item shown, or
    // (Multiple Items), read from the field's Items, asked of the source; Loading… until a first listing lands, rather
    // than a guess, and why when they cannot be listed. While a new Source Version's Items are on their way, it is read
    // from the Items in view, the earlier version's (ADR-0066 refined).
    #bandSummary(placement: PivotFieldPlacement): string {
        const words = this.#words;
        if (placement.hiddenItems.length === 0) return words.word('all');
        const items = this.#itemsOf(placement.field);
        if (items.problem !== undefined) return itemsProblemText(items.problem, words);
        const page = items.listed;
        if (page === undefined) return words.word('loading');
        if (page.total > page.items.length) {
            // More Items than are held: provably several shown, or not known.
            return page.total - placement.hiddenItems.length > 1 ? words.word('multiple-items') : words.word('loading');
        }
        // One Item shown is named; past one, how many does not matter.
        const hidden = new Set(placement.hiddenItems.map(key => pivotItemKeyId(key)));
        let shown: PivotItemInfo | undefined;
        for (const item of page.items) {
            if (hidden.has(pivotItemKeyId(item.key))) continue;
            if (shown !== undefined) return words.word('multiple-items');
            shown = item;
        }
        // None shown (data that no longer holds the one Item shown) is said as the Razor component says it.
        return shown === undefined ? words.word('multiple-items') : shown.label;
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

    // Whether a question is out under the loading indication: any but the quiet one a source's notice asked.
    #loading(): boolean {
        return this.#asked !== undefined && this.#asked.cause !== 'live';
    }

    // The Field List: a Chrome's in the pane's place while one is given and can be drawn, else the built-in pane.
    #placeFieldList(): void {
        const mount = this.#chrome?.fieldList;
        const slotted = mount !== undefined && this.#answered(this.#slottedFieldList.show(mount, id => this.#words.word(id)));
        if (!slotted) this.#slottedFieldList.hide();
        this.#fieldListSlotted = slotted;
        const pane = slotted ? this.#slottedFieldList.element : this.#builtInFieldList.element;
        if (pane.parentNode !== this.#fieldListScroll) this.#fieldListScroll.replaceChildren(pane);
        // A drag whose pane went with its contents ended with them: the report no longer offers to remove its field.
        if (!this.#fieldList.removable) this.#dropRemoveShown = false;
    }

    // The Stale Report's notice, and the class by which ExPivot's stylesheet mutes the report's numbers (ADR-0067). Retry
    // is not offered twice at once: while a question a person or the Consumer asked is out, it is the one Retry would
    // ask. A quiet one for the source's notice leaves Retry offered, or a ticking source would hardly ever let it be
    // pressed.
    #drawStale(words: PivotScreenWords): void {
        const stale = this.#stale !== undefined && this.#shown.answer !== undefined ? this.#stale : undefined;
        this.#report.classList.toggle('ex-pivot-report-stale', stale !== undefined);
        // Retry taken away or disabled while it holds the keyboard hands it to the Field List toggle, so the keyboard stays
        // in the pivot, on a button whose press asks nothing of the source.
        const held = this.#parts.holds('retry');
        const handOn = (): void => { if (held) this.#parts.focus('field-list'); };
        const mount = this.#chrome?.staleReport;
        if (stale === undefined) {
            this.#staleNotice.remove();
            this.#staleSlot.hide();
            this.#staleSlot.element.remove();
            handOn();
            return;
        }
        const asOf = this.#asOf();
        const view = this.#staleSlot.viewOf(Object.freeze({
            message: words.fill('stale-report', asOf, stale),
            asOfText: asOf,
            reason: stale,
            retry: Object.freeze({ label: words.word('retry'), enabled: !this.#loading() }),
        }));
        if (mount !== undefined && this.#staleSlot.element.parentNode !== this.#staleRegion) this.#staleRegion.append(this.#staleSlot.element);
        const shown = mount !== undefined && this.#showStanding(this.#staleSlot, mount, (shared, input, refs) => ({
            ...shared,
            // Asked only while Retry is offered: one pressed while what it would ask is out asks nothing twice.
            retry: input(() => { if (shared.view().retry.enabled) this.#retry(); }),
            control: name => refs.control(checkedName(name, ['retry'], 'staleReport')),
        }));
        if (shown) {
            this.#staleNotice.remove();
        } else {
            this.#staleSlot.hide();
            this.#staleSlot.element.remove();
            this.#parts.handInControl('retry', this.#retryButton);
            if (this.#staleMessage.textContent !== view.message) this.#staleMessage.textContent = view.message;
            if (this.#retryButton.textContent !== view.retry.label) this.#retryButton.textContent = view.retry.label;
            this.#retryButton.disabled = !view.retry.enabled;
            if (this.#staleNotice.parentNode !== this.#staleRegion) this.#staleRegion.append(this.#staleNotice);
        }
        if (!view.retry.enabled) handOn();
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
        this.#placeFieldList();
        this.#drawMenu(valueCaptions);
        const stretch = this.#options.viewportHeight === 'stretch';
        const width = this.#options.fieldListWidth ?? 300; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0061: the Field List's declared default width, as the Razor component's
        // The Geometry Tokens, inline on the root (ADR-0027): the Field List's width and a level's indent.
        writeStyle(this.#root, `--ex-pivot-field-list-width: ${width}px; --ex-pivot-indent: ${this.#indentPx}px${stretch ? '; height: 100%; flex: 1 1 auto; min-height: 0' : ''}`);
        // A question for the newer data the source told of is asked quietly, so a live report is not busy every interval.
        if (this.#loading()) this.#root.setAttribute('aria-busy', 'true');
        else this.#root.removeAttribute('aria-busy');
        this.#drawStale(words);
        writeStyle(this.#sheet, stretch ? 'display: flex; flex-direction: column; flex: 1 1 auto; min-height: 0' : '');
        const answered = this.#reportDrawn;
        // The report or the box that says why there is none, and over the report, while a placed field is
        // dragged, the mark that a drop there removes it (ADR-0061). Elements come and go rather than hide, since
        // the stylesheet gives the box a display of its own.
        // While a details tab is selected its records cover the report, which stays laid out, with its state, and is
        // not painted: its own header would otherwise paint over the records', and a report nobody sees takes no
        // keyboard and is not read out (ADR-0059). Records covering it take no drop meant for it.
        const details = this.#details;
        const panel = details.panel;
        this.#sheet.className = panel === undefined ? 'ex-pivot-sheet' : 'ex-pivot-sheet ex-pivot-sheet-covered';
        const dropRemove = answered && this.#dropRemoveShown && panel === undefined && !details.dialogShown;
        elementAt([...this.#dropRemove.children], 0).textContent = words.word('remove-field');
        const shownInSheet = [answered ? this.#gridRoot : this.#empty, ...(dropRemove ? [this.#dropRemove] : []), ...(panel === undefined ? [] : [panel])];
        if (shownInSheet.length !== this.#sheet.children.length || shownInSheet.some((child, i) => this.#sheet.children[i] !== child)) {
            this.#sheet.replaceChildren(...shownInSheet);
        }
        // The tabs stand at the report's foot, where Excel's sheet tabs are, while a details tab stands.
        if (!details.tabsShown) details.tabList.remove();
        else if (details.tabList.parentNode !== this.#report) this.#report.append(details.tabList);
        // While the dialog stands, what it covers takes neither the keyboard nor the pointer: it is modal (ADR-0059).
        this.#report.inert = details.dialogShown;
        this.#fieldListBox.inert = details.dialogShown;
        for (const shown of details.dialogElements) {
            if (shown.parentNode !== this.#root) this.#root.append(shown);
        }
        if (!answered) {
            this.#empty.textContent = words.word(this.#asked !== undefined && !showsNothing(this.#layoutAhead) ? 'loading' : 'empty-report');
            const height = this.#options.viewportHeight ?? 400; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0028: the report's declared default height, as the Razor component's
            writeStyle(this.#empty, height === 'stretch' ? 'flex: 1 1 auto; min-height: 0' : `height: ${height}px`);
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
                deferred: this.#deferred !== undefined,
                canUpdate: this.#deferred !== undefined && this.#pendingApplies(this.#deferred.layout),
                popup: this.#open?.kind === 'entry' || this.#open?.kind === 'panel' ? { entry: this.#open.entry, frame: this.#open.popup.element } : undefined,
            });
        }
        const left = this.#keyboardAfterDraw;
        this.#keyboardAfterDraw = undefined;
        if (left !== undefined) this.#returnKeyboard(left, this.#entryNow(left));
        this.#throwRefused();
    }

    // Throws the first of the slots' contents that could not be drawn: to the Consumer, where it makes or updates the
    // pivot or opens records; otherwise on its own, as a listener's error is, so the source, the user's input or the
    // timer that drew goes on.
    #throwRefused(): void {
        const [refused] = this.#slotErrors;
        this.#slotErrors = [];
        if (refused === undefined) return;
        if (this.#consumerDraws) throw refused;
        queueMicrotask(() => { throw refused; });
    }
}

/** Defer Layout Update ticked: the layout the pane shows and builds, and the moves into it the report does not hold. */
interface Deferred {
    readonly layout: PivotLayout;
    readonly moves: readonly FieldListMove[];
}

// The moves a refused question's layout still holds: a field moved and then taken out again is not in it. A field may
// stand in Values as well as in an axis, so a move is known by its field and its Area.
function heldMoves(question: Question): readonly FieldListMove[] {
    return question.moves.filter((move, i, all) => all.findLastIndex(later => later.field === move.field && later.area === move.area) === i
        && holdsMove(question.layout, move));
}

// The fields a refusal names: those of the moves its layout still holds, each once.
function heldFields(held: readonly FieldListMove[]): readonly string[] {
    return [...new Set(held.map(move => move.field))];
}

// How long a Change Highlight lasts under a pivot's options (ADR-0068).
function highlightMsOf(options: PivotViewOptions): number {
    return options.changeHighlightDuration ?? defaultChangeHighlightMs;
}

// The caps a pivot's options set, each as it applies, so a cap given as its default asks nothing again.
function capsOf(options: PivotViewOptions): string {
    return `${options.maxLeaves ?? defaultMaxLeaves} ${options.maxRows ?? excelRows} ${options.maxColumns ?? excelColumns}`;
}

// The cap a report breaks, or none (ADR-0066): its rows, or its columns with the label columns, past the question's —
// with the word that refuses a layout for it and the word that says newer data broke it.
function capBrokenBy(metadata: PivotReportState['metadata'], question: Question): { readonly refusal: string; readonly stale: string; readonly cap: number } | undefined {
    if (metadata.rowCount > question.maxRows) return { refusal: 'refused-too-many-rows', stale: 'stale-too-many-rows', cap: question.maxRows };
    if (metadata.labelColumns.length + metadata.valueColumns.length > question.maxColumns) {
        return { refusal: 'refused-too-many-columns', stale: 'stale-too-many-columns', cap: question.maxColumns };
    }
    return undefined;
}

// Whether `layout` holds the field a move put in its Area.
function holdsMove(layout: PivotLayout, move: FieldListMove): boolean {
    return move.area === 'values' ? layout.values.some(value => value.field === move.field) : placementOf(layout, move.field)?.area === move.area;
}

// Whether every field `layout` places is one of `declared`.
function placesOnly(layout: PivotLayout, declared: ReadonlyMap<string, unknown>): boolean {
    return [...layout.filters, ...layout.rows, ...layout.columns, ...layout.values].every(placed => declared.has(placed.field));
}

// .NET's Math.Round, which sends a tie to the even number (CONVENTIONS.md).
function roundHalfToEven(value: number): number {
    const floor = Math.floor(value);
    const diff = value - floor;
    if (diff < 0.5) return floor;
    if (diff > 0.5) return floor + 1;
    return floor % 2 === 0 ? floor : floor + 1;
}

// The name a report filter's button and the anchor around it are handed in by.
function bandName(field: string): string {
    return `filter:${field}`;
}

// A report filter's field, as a script may hand it to the band's `control` or `anchor`: the field's name.
function checkedField(field: string): string {
    const given: unknown = field;
    if (typeof given !== 'string') throw new TypeError('An ExPivot\'s report filter band hands in a filter\'s button and anchor by the field\'s name (ADR-0390).');
    return given;
}
