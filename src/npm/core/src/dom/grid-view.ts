// The grid in the DOM (ADR-0350 sections 2 and 4): the Script Core builds and updates the elements inside
// the instance root itself, to the DOM contract the C# core writes, so ex-grid.css and layer 3 read both
// alike. A row is plain markup and the only unit that is skipped or painted again (ADR-0003); the cells
// are rebuilt with their row.
//
// One pass resolves and paints, as the C# component's render does (PrepareRender, then the markup): what
// the browser told and what the Consumer handed over become the Viewport's slice and the columns on
// screen, and the DOM is brought to them. Anything that changes them asks for a pass, which runs in a
// microtask, so a burst of changes in one task paints once.

import { AutoWidth } from '../columns/auto-width';
import { ColumnGeometry, type ColumnRange } from '../columns/column-geometry';
import { decideOverflow } from '../columns/overflow-rules';
import { textFor } from '../cells/display-text';
import { GridMetrics, type GridDensity } from '../grid-metrics';
import { defined, itemAt } from '../must';
import { canonicalKey, resolveKey, type GridKeyAction } from '../keys/grid-keys';
import { cellClasses } from '../paint/cell-classes';
import { columnMark } from '../paint/heading-mark-styles';
import { rowClasses } from '../paint/row-classes';
import { cell as cellPosition, sameCell, type CellPosition, type GridExtent } from '../selection/cell';
import { markedColumns, wholeColumnRuns } from '../selection/heading-marks';
import { HeldSelection } from '../selection/held';
import { rangeCellCount, sameRange as sameSelectionRange, spansEveryColumn, spansEveryRow, type SelectionRange } from '../selection/range';
import { Selection, type GridSelection } from '../selection/selection';
import type { GridSource, GridSourceState } from '../source/grid-source';
import { InMemoryGridSource } from '../source/in-memory-source';
import { rowRange, type RowRange } from '../viewport/row-range';
import { ViewportBox } from '../viewport/viewport-box';
import { ViewportGeometry } from '../viewport/viewport-geometry';
import type { BrowserReports, ViewportReport } from './browser-reports';
import { ColumnStyles } from './column-styles';
import { resolveColumns, type GridColumn, type ResolvedColumn } from './grid-column';
import { moveByKey, movingKinds, type KeyReveal } from './key-moves';
import { selectionLayers, type LayerPiece } from './selection-layers';
import { stylesheetName, stylesheetStamp } from './shipped-stylesheet';

/** A Viewport axis: a number of pixels the Consumer declares, or `'stretch'`, the parent's (ADR-0028). */
export type ViewportSize = number | 'stretch';

/** What a grid is drawn from. Rows reach it only through a Grid Source; `rows` is `GridSource.from(rows)`. */
export interface GridOptions<Row> {
    /** The columns, in display order. */
    readonly columns: readonly GridColumn<Row>[];
    /** Where the rows come from. Exactly one of `source` and `rows` is given. */
    readonly source?: GridSource<Row>;
    /** The rows, held by a source the grid makes over them. Exactly one of `source` and `rows` is given. */
    readonly rows?: readonly Row[];
    /** The preset every geometry not given comes from; `compact` unless given (ADR-0028). */
    readonly density?: GridDensity;
    /** The height of every row in pixels, beating the preset's (ADR-0013). */
    readonly rowHeight?: number;
    /** The header's height in pixels; the row height's when only that is given (ADR-0028). */
    readonly headerHeight?: number;
    /** The Viewport's width: 800 pixels unless given, or `'stretch'` for the parent's (ADR-0028). */
    readonly viewportWidth?: ViewportSize;
    /** The Viewport's height, header and scrollbar included: 600 pixels unless given, or `'stretch'`. */
    readonly viewportHeight?: ViewportSize;
    /** How many columns from the left stay in place while the rest scroll (ADR-0004); none unless given. */
    readonly pinnedColumnCount?: number;
    /** Whether the columns off screen are left out of the DOM (ADR-0004); true unless given. */
    readonly virtualiseColumns?: boolean;
    /** Hears the Selection each time it changes (ADR-0011): after the grid has painted it, once however
     * many gestures changed it in between. A Selection is dropped, and heard empty, when the Row Sequence
     * Version or the columns change. */
    readonly onSelectionChanged?: (selection: GridSelection) => void;
}

const defaultViewportWidthPx = 800;
const defaultViewportHeightPx = 600;

/** How long scrolling must have stood still before a fling's Placeholders are painted over (ADR-0004), and
 * a Selection before it is announced (ADR-0033). */
const settleDelayMs = 150;

/** The keys whose own keydown is no key of the grid's: they neither take nor end a release of Tab. */
const modifierKeys: ReadonlySet<string> = new Set(['Control', 'Shift', 'Alt', 'Meta', 'AltGraph', 'CapsLock']);

/** The built-in Chrome's words for a stylesheet of another version (ADR-0123, ADR-0203). */
const stylesheetNoticeWords = 'This page holds another version\'s stylesheet. Reload the page.';

/** The rows the header stands for in the accessibility tree, ahead of the data rows (ADR-0033). */
const headerRowCount = 1;

/** What each instance's ids begin with, so two grids on a page never share one (ADR-0018). */
let instances = 0;

/** A row as it was last painted: what decides whether it is painted again (ADR-0003). */
interface PaintedRow<Row> {
    readonly element: HTMLElement;
    readonly row: Row;
    readonly index: number;
    readonly styles: ColumnStyles;
    readonly columns: readonly ResolvedColumn<Row>[];
    readonly placeholder: boolean;
    readonly scrollable: ColumnRange | undefined;
}

/** The options as `update` takes them: checked and resolved, and not yet the grid's. */
interface Taken<Row> {
    readonly source: GridSource<Row>;
    /** The source when the grid made it over `rows`. */
    readonly own: InMemoryGridSource<Row> | undefined;
    readonly rows: readonly Row[] | undefined;
    readonly metrics: GridMetrics;
    readonly declared: readonly GridColumn<Row>[];
    readonly columns: readonly ResolvedColumn<Row>[];
    readonly autoWidths: readonly AutoWidth[];
    readonly columnsChanged: boolean;
    readonly width: ViewportSize;
    readonly height: ViewportSize;
    readonly pinnedCount: number;
    readonly virtualiseColumns: boolean;
    readonly onSelectionChanged: ((selection: GridSelection) => void) | undefined;
}

/** A reveal's repaint under way (ADR-0012, 2026-10-08): where the reveal left the scroller, and what stops
 * the frame it waits for. */
interface Repaint {
    readonly top: number;
    readonly left: number;
    cancel: () => void;
}

/** A page follow (ADR-0204): armed by a move for the Focus it moved to and the cell its reveal brought into
 * the Viewport, and asked once that cell is painted. */
interface Follow {
    readonly focus: CellPosition;
    readonly target: CellPosition;
}

/** Whether two lists hold the same ranges, in order. */
function sameRanges(a: readonly SelectionRange[], b: readonly SelectionRange[]): boolean {
    return a.length === b.length && a.every((range, i) => sameSelectionRange(range, itemAt(b, i)));
}

/** Whether two arrays hold the same items, in the same order. */
function sameItems<T>(a: readonly T[], b: readonly T[]): boolean {
    return a === b || (a.length === b.length && a.every((item, i) => item === b[i]));
}

/** A header cell as painted, with what its classes are composed from. */
interface HeaderCell {
    readonly element: HTMLElement;
    readonly column: number;
    readonly align: GridColumn<unknown>['align'];
    readonly pinned: boolean;
}

/** The Heading Mark's runs, made once per Selection and extent (ADR-0008, 2026-10-13). */
interface HeadingMarkRuns {
    readonly selection: Selection;
    readonly extent: GridExtent;
    readonly columns: readonly ColumnRange[];
    readonly whole: readonly ColumnRange[];
}

/** The Heading Mark as painted over the header: the runs and the geometry it was painted from, its
 * rectangles' styles and their elements, so a pass that changes none of them touches nothing (PF-3). */
interface PaintedHeadingMarks {
    readonly marks: HeadingMarkRuns;
    readonly styles: ColumnStyles;
    readonly topPx: number;
    readonly heightPx: number;
    readonly pinnedStyles: readonly string[];
    readonly scrollableStyles: readonly string[];
    readonly elements: { readonly pinned: HTMLElement | undefined; readonly scrollable: readonly HTMLElement[] };
}

/** Whether two runs of columns are the same run. */
function sameRange(a: ColumnRange | undefined, b: ColumnRange | undefined): boolean {
    return a === b || (a !== undefined && b !== undefined && a.start === b.start && a.count === b.count);
}

function sizeOf(size: ViewportSize | undefined, fallback: number, name: string): ViewportSize {
    if (size === undefined) return fallback;
    if (size === 'stretch') return size;
    if (typeof size !== 'number' || !Number.isFinite(size) || size <= 0) {
        throw new RangeError(`${name} is a finite, positive number of pixels or 'stretch', not ${String(size)} (ADR-0028).`);
    }
    return size;
}

/** The stretch half of the root's style (ADR-0028, "Which element takes the box"). */
function stretchRootStyle(width: ViewportSize, height: ViewportSize): string {
    if (height !== 'stretch') return width === 'stretch' ? '; min-width: 0' : '';
    return width === 'stretch'
        ? '; display: flex; flex-direction: column; height: 100%; flex: 1 1 auto; min-height: 0; min-width: 0'
        : '; display: flex; flex-direction: column; height: 100%; flex: 1 1 auto; min-height: 0';
}

/**
 * One grid in one element. Made by `createGrid`, which hands back its handle; nothing outside the core
 * holds it.
 */
export class GridView<Row> {
    readonly #root: HTMLElement;
    readonly #reports: BrowserReports;
    readonly #ids: string;
    readonly #scroller: HTMLElement;
    readonly #spacer: HTMLElement;
    readonly #header: HTMLElement;
    readonly #viewport: HTMLElement;
    readonly #stamp: HTMLElement;
    readonly #announce: HTMLElement;
    readonly #shadeLayer: HTMLElement;
    readonly #pinnedLayer: HTMLElement;
    readonly #selectionLayer: HTMLElement;
    readonly #metaIsPrimary: boolean;
    readonly #stops: (() => void)[] = [];

    // What the Consumer handed over.
    #declared: readonly GridColumn<Row>[] = [];
    #columns: readonly ResolvedColumn<Row>[] = [];
    #metrics = GridMetrics.resolve({ density: 'compact' });
    #source: GridSource<Row>;
    #ownSource: InMemoryGridSource<Row> | undefined;
    #ownRows: readonly Row[] | undefined;
    // Set once the first source is subscribed to, which is the grid holding a source at all.
    #unsubscribe: (() => void) | undefined;
    #state: GridSourceState<Row>;
    #sequenceOffset = 0;
    #width: ViewportSize = defaultViewportWidthPx;
    #height: ViewportSize = defaultViewportHeightPx;
    #pinnedCount = 0;
    #virtualiseColumns = true;
    #onSelectionChanged: ((selection: GridSelection) => void) | undefined;

    // The Selection (ADR-0011, ADR-0012), held under the Row Sequence Version it was made under, and the
    // columns it was made over, by name: either changing drops it.
    #held = HeldSelection.empty(0);
    #columnNames: readonly string[] = [];
    // The Selection the Consumer last heard.
    #raised: Selection = Selection.empty;
    #dragging = false;
    // A press the grid answered, whose release is answered too.
    #pressed = false;
    // Escape with nothing to dismiss releases Tab: the next Tab is the browser's (ADR-0012).
    #tabReleased = false;
    // The inputs answered, written on the stamp by the pass that paints the answer (ADR-0220).
    #answered = 0;
    #revealOwed: KeyReveal | undefined;
    #revealToken = 0;
    #repaint: Repaint | undefined;
    #followArmed: Follow | undefined;
    #followToken = 0;
    #cancelFollow: (() => void) | undefined;
    // The live region (ADR-0033): what it says, and the ranges it last described.
    #announcedRanges: readonly SelectionRange[] = [];
    #describesSelection = false;
    #cancelAnnounce: (() => void) | undefined;

    // What the browser told (ADR-0013, ADR-0028, ADR-0053, ADR-0090).
    #viewportReport: ViewportReport | undefined;
    #ceilingPx: number | undefined;
    #devicePixelRatio: number | undefined;
    #scrollTopPx = 0;
    #scrollLeftPx = 0;

    // What a pass resolves.
    #geometry: ViewportGeometry | undefined;
    #visible: RowRange | undefined;
    #styles: ColumnStyles;
    #scrollable: ColumnRange | undefined;
    #autoWidths: readonly AutoWidth[] = [];
    #measuredWindow: readonly Row[] | undefined;
    #measuredWindowStart = -1;
    #measuredVisible: RowRange | undefined;
    #placeholderMode = false;
    #cancelSettle: (() => void) | undefined;
    #lastRaised: { readonly range: RowRange; readonly windowStart: number; readonly windowCount: number } | undefined;

    // What is painted.
    #painted = new Map<unknown, PaintedRow<Row>>();
    #paintedHeader: {
        readonly styles: ColumnStyles; readonly columns: readonly ResolvedColumn<Row>[]; readonly scrollable: ColumnRange | undefined; readonly sorts: string;
    } | undefined;
    // The header cells as painted, each with its class before the Heading Mark's, and the mark's runs and
    // rectangles (ADR-0008, 2026-10-13): made once per Selection and extent, and the rectangles once per
    // Selection, column geometry and band, since a pass runs on every scroll frame (PF-3).
    #headerCells: readonly HeaderCell[] = [];
    #marks: HeadingMarkRuns | undefined;
    #paintedMarks: PaintedHeadingMarks | undefined;
    // The placeholder rows on screen, by position: a position still waiting keeps its element (ADR-0003).
    #placeholders = new Map<number, HTMLElement>();
    readonly #written = new Map<string, string>();
    #layoutId = 0;
    #paintId = 0;
    #layoutStyles: ColumnStyles | undefined;
    #paintedLayers: string | undefined;
    #layerInputs: Readonly<Record<string, unknown>> | undefined;
    #shadeHolds = false;

    // The Settled Mark (ADR-0220).
    #passOwed = false;
    #settledShown: boolean | undefined;
    #destroyed = false;

    constructor(root: HTMLElement, options: GridOptions<Row>, reports: BrowserReports) {
        this.#root = root;
        this.#reports = reports;
        instances += 1;
        this.#ids = `ex${instances}-`;
        const doc = root.ownerDocument;
        const element = (tag: string, attributes: Readonly<Record<string, string>>): HTMLElement => {
            const made = doc.createElement(tag);
            for (const [name, value] of Object.entries(attributes)) made.setAttribute(name, value);
            return made;
        };

        // Everything handed over is checked and resolved, and the source told its columns, before anything
        // of the grid's exists: options a grid cannot be drawn from leave nothing behind.
        const taken = this.#take(options);
        this.#prepare(taken, undefined);
        this.#source = taken.source;
        this.#state = taken.source.snapshot();

        // The root's own attributes (ADR-0033): `dir` so a grid on an RTL page is an LTR island, and the
        // root the tab stop the keys are heard on (ADR-0018).
        root.setAttribute('tabindex', '0');
        root.setAttribute('dir', 'ltr');
        root.setAttribute('id', `${this.#ids}root`);
        root.setAttribute('role', 'grid');
        root.setAttribute('aria-multiselectable', 'true');
        root.setAttribute('data-ex-reveal', '0');
        root.setAttribute('data-ex-follow', '0');

        // The core's half of the Settled Mark, as the C# core writes it on the root's first child: every
        // input answered as it is taken, and no layer an Escape would peel (ADR-0220, ADR-0230).
        const stamp = element('span', { hidden: '', 'data-ex-answered': '0', 'data-ex-escape-leaves': '' });
        this.#stamp = stamp;
        // The stylesheet's notice (ADR-0123): shown by a stylesheet of another version, hidden by its own.
        const notice = element('div', {
            'data-ex-notice': 'stylesheet', 'data-ex-stylesheet': stylesheetName, 'data-ex-stamp': stylesheetStamp, style: 'display: none',
        });
        notice.textContent = stylesheetNoticeWords;
        // One scroll container for both axes, the header and the Pinned Columns held by `position: sticky`
        // inside it; tabindex -1 keeps it out of the tab sequence (A11Y-4).
        this.#scroller = element('div', { class: 'ex-scroller', tabindex: '-1', role: 'presentation' });
        this.#spacer = element('div', { class: 'ex-spacer', role: 'presentation' });
        this.#header = element('div', {
            class: 'ex-header', role: 'row', 'aria-rowindex': '1', 'data-ex-claims': 'mousedown click',
        });
        this.#viewport = element('div', { class: 'ex-viewport', role: 'presentation', 'data-ex-claims': 'contextmenu' });
        // The selection layers (ADR-0008, ADR-0206): paint, not content, so hidden from the accessibility
        // tree, and the Viewport's first children, so they ride its translation and pass under the pinned
        // cells. The pinned shade layer stands only while it holds a tint.
        this.#shadeLayer = element('div', { class: 'ex-shade-pinned', 'aria-hidden': 'true' });
        this.#pinnedLayer = element('div', { class: 'ex-selection-pinned', 'aria-hidden': 'true' });
        this.#selectionLayer = element('div', { class: 'ex-selection', 'aria-hidden': 'true' });
        this.#spacer.append(this.#header, this.#viewport);
        this.#scroller.append(this.#spacer);
        const announce = element('div', { class: 'ex-announce', role: 'status' });
        this.#announce = announce;
        // The Layout Ceiling's probe (ADR-0053): an element declared 2^25 px tall, laid out at the tallest
        // the browser can.
        const probe = element('div', { class: 'ex-ceiling-probe', 'aria-hidden': 'true' });
        const tallest = element('div', { style: 'width: 1px; height: 33554432px' });
        probe.append(tallest);
        root.replaceChildren(stamp, notice, this.#scroller, announce, probe);
        this.#styles = new ColumnStyles(new ColumnGeometry({ widthsPx: [], pinnedCount: 0, viewportWidthPx: 1 }));
        this.#metaIsPrimary = reports.metaIsPrimary();

        // A first pass that throws leaves nothing listening either: there is no handle to stop it with.
        let drawn = false;
        try {
            this.#commit(taken);
            const onScroll = (): void => {
                this.#onScroll();
            };
            this.#scroller.addEventListener('scroll', onScroll, { passive: true });
            // The keys, heard on the root in the capture phase, ahead of anything inside it (ADR-0018).
            root.addEventListener('keydown', this.#onKeyDown, true);
            this.#viewport.addEventListener('mousedown', this.#onPress);
            this.#viewport.addEventListener('mouseup', this.#onRelease);
            root.addEventListener('focusout', this.#onFocusLeft);
            this.#scroller.addEventListener('focus', this.#onScrollerFocus);
            this.#stops.push(
                () => { this.#scroller.removeEventListener('scroll', onScroll); },
                () => { root.removeEventListener('keydown', this.#onKeyDown, true); },
                () => { this.#viewport.removeEventListener('mousedown', this.#onPress); },
                () => { this.#viewport.removeEventListener('mouseup', this.#onRelease); },
                () => { root.removeEventListener('focusout', this.#onFocusLeft); },
                () => { root.removeEventListener('mouseup', this.#onDragEnd); },
                () => { this.#viewport.removeEventListener('mousemove', this.#onDrag); },
                () => { this.#scroller.removeEventListener('focus', this.#onScrollerFocus); },
                reports.observeViewport(this.#scroller, report => {
                    this.#viewportReport = report;
                    this.#invalidate();
                }),
                reports.observeCeiling(tallest, ceilingPx => {
                    this.#ceilingPx = ceilingPx;
                    this.#invalidate();
                }),
                reports.observeDevicePixel(ratio => {
                    this.#devicePixelRatio = ratio;
                    this.#invalidate();
                }),
            );
            this.#pass();
            drawn = true;
        } finally {
            if (!drawn) this.destroy();
        }
    }

    /** Takes the Consumer's options again; what did not change costs nothing (ADR-0003). Options a grid
     * cannot be drawn from are refused whole: the grid stays as it was. */
    update(options: GridOptions<Row>): void {
        if (this.#destroyed) throw new Error('This grid was destroyed: a grid destroyed is not drawn again.');
        const taken = this.#take(options);
        this.#prepare(taken, this.#ownSource);
        this.#commit(taken);
        this.#invalidate();
    }

    /** Stops hearing everything and empties the root. */
    destroy(): void {
        if (this.#destroyed) return;
        this.#destroyed = true;
        this.#cancelSettle?.();
        this.#cancelAnnounce?.();
        this.#cancelFollow?.();
        this.#repaint?.cancel();
        for (const stop of this.#stops) stop();
        this.#unsubscribe?.();
        this.#root.replaceChildren();
        for (const name of ['tabindex', 'dir', 'id', 'role', 'aria-multiselectable', 'data-ex-reveal', 'data-ex-follow',
            'data-ex-follow-focus', 'aria-activedescendant', 'data-ex-settled', 'class', 'style', 'aria-rowcount', 'aria-colcount']) {
            this.#root.removeAttribute(name);
        }
    }

    // The options checked and resolved, with nothing of the grid's changed. `rows` handed again as the same
    // array is the same source; a new array is a new source, made here and told nothing yet.
    #take(options: GridOptions<Row>): Taken<Row> {
        if ((options.source === undefined) === (options.rows === undefined)) {
            throw new TypeError('A grid takes its rows from exactly one of `source` and `rows` (ADR-0350, section 16).');
        }
        let source: GridSource<Row>;
        let own: InMemoryGridSource<Row> | undefined;
        if (options.source === undefined) {
            const rows = defined(options.rows, 'the rows');
            own = this.#ownSource !== undefined && this.#ownRows === rows ? this.#ownSource : InMemoryGridSource.from(rows);
            source = own;
        } else {
            source = options.source;
        }

        const { density = 'compact', pinnedColumnCount = 0, virtualiseColumns = true } = options;
        const resolved = GridMetrics.resolve({
            density,
            ...(options.rowHeight === undefined ? {} : { rowHeightPx: options.rowHeight }),
            ...(options.headerHeight === undefined ? {} : { headerHeightPx: options.headerHeight }),
        });
        const metricsChanged = !resolved.equals(this.#metrics);
        const metrics = metricsChanged ? resolved : this.#metrics;
        let columns = this.#columns;
        let autoWidths = this.#autoWidths;
        // A new array holding the same column objects is the same columns: a Framework Adapter hands a new
        // array on every render, and nothing about them changed (ADR-0003).
        const columnsChanged = metricsChanged || !sameItems(options.columns, this.#declared);
        if (columnsChanged) {
            const previous = this.#columns;
            const previousWidths = this.#autoWidths;
            columns = resolveColumns(options.columns);
            // An Auto width is carried by the column's name for the same column object under the same
            // metrics, as the C# core carries it, and measured afresh otherwise: two equal declarations may
            // paint differently (ADR-0016; ADR-0003's note of 2026-10-11).
            const at = new Map(previous.map(({ column }, i) => [column.name, i]));
            autoWidths = columns.map(({ column, spec, header }) => {
                const was = at.get(column.name);
                if (!metricsChanged && was !== undefined && itemAt(previous, was).column === column) {
                    return itemAt(previousWidths, was);
                }
                // The header is content the column paints too: unobserved, a short Auto column would
                // stand with its label chopped (ADR-0016).
                return spec.width.isAuto
                    ? AutoWidth.of(spec).observe(metrics.headerRequiredPx({ label: header, menuButton: false, sortable: column.queryable !== false }))
                    : AutoWidth.of(spec);
            });
        }
        const width = sizeOf(options.viewportWidth, defaultViewportWidthPx, 'viewportWidth');
        const height = sizeOf(options.viewportHeight, defaultViewportHeightPx, 'viewportHeight');
        if (!Number.isSafeInteger(pinnedColumnCount) || pinnedColumnCount < 0) {
            throw new RangeError(`pinnedColumnCount is a whole number of at least 0, not ${pinnedColumnCount}.`);
        }
        const { onSelectionChanged }: { readonly onSelectionChanged?: unknown } = options;
        if (onSelectionChanged !== undefined && typeof onSelectionChanged !== 'function') {
            throw new TypeError('onSelectionChanged is a function of the Selection.');
        }
        return {
            source, own, rows: options.rows, metrics, declared: options.columns, columns, autoWidths, columnsChanged,
            width, height, pinnedCount: pinnedColumnCount, virtualiseColumns, onSelectionChanged: options.onSelectionChanged,
        };
    }

    // What the taken options ask of the sources, the last step that may refuse them: the source told its
    // columns, and a source the grid made over new rows given the Sort and the Filter in force over the
    // last ones. A source the grid made and then refused is dropped, never held.
    #prepare(taken: Taken<Row>, previousOwn: InMemoryGridSource<Row> | undefined): void {
        const { source, own } = taken;
        const newSource = source !== this.#sourceIfAny();
        if (newSource || taken.columnsChanged) source.setColumns(taken.columns.map(({ column }) => column));
        if (own !== undefined && own !== previousOwn && previousOwn !== undefined) {
            const { sorts, filter } = previousOwn.snapshot();
            own.setSorts(sorts);
            own.setFilter(filter);
        }
    }

    // The source in force, or none while the constructor has not taken one.
    #sourceIfAny(): GridSource<Row> | undefined {
        return this.#unsubscribe === undefined ? undefined : this.#source;
    }

    // What was taken becomes the grid's. Nothing here refuses.
    #commit(taken: Taken<Row>): void {
        const { source } = taken;
        if (source !== this.#sourceIfAny()) {
            // The Row Sequence Version the grid shows moves on whenever its source is another one: every
            // position may now name another row (ADR-0011). Each source counts from where it stood.
            const shown = this.#unsubscribe === undefined ? undefined : this.#sequence();
            this.#unsubscribe?.();
            this.#source = source;
            this.#unsubscribe = source.subscribe(this.#onSource);
            this.#state = source.snapshot();
            this.#sequenceOffset = shown === undefined ? 0 : shown + 1 - this.#state.rowSequenceVersion;
            this.#lastRaised = undefined;
        }
        this.#ownSource = taken.own;
        this.#ownRows = taken.own === undefined ? undefined : taken.rows;
        if (taken.columnsChanged) {
            this.#columns = taken.columns;
            this.#autoWidths = taken.autoWidths;
            this.#measuredWindow = undefined;
        }
        this.#declared = taken.declared;
        this.#metrics = taken.metrics;
        this.#width = taken.width;
        this.#height = taken.height;
        this.#pinnedCount = taken.pinnedCount;
        this.#virtualiseColumns = taken.virtualiseColumns;
        this.#onSelectionChanged = taken.onSelectionChanged;
    }

    // The Row Sequence Version the grid writes: its source's, carried past every change of source.
    #sequence(): number {
        return this.#state.rowSequenceVersion + this.#sequenceOffset;
    }

    readonly #onSource = (): void => {
        this.#state = this.#source.snapshot();
        this.#invalidate();
    };

    #onScroll(): void {
        // A reveal's repaint is the browser's business: meanwhile the grid stands where the reveal left the
        // scroller, and a scroll the user makes is read when the repaint yields to it (ADR-0012).
        if (this.#repaint !== undefined) return;
        // The offsets the browser holds, read on the scroll it reports: the one read this grid makes.
        const top = this.#scroller.scrollTop;
        const left = this.#scroller.scrollLeft;
        if (top === this.#scrollTopPx && left === this.#scrollLeftPx) return;
        // The user scrolled away from a move whose cell is not painted yet: the page no longer follows it
        // (ADR-0204).
        this.#followArmed = undefined;
        const painted = this.#visible;
        const previousLeftPx = this.#scrollLeftPx;
        this.#scrollTopPx = top;
        this.#scrollLeftPx = left;
        const landed = this.#geometry?.sliceAt(top);
        // Either axis on its own makes a fling, and the horizontal one only while the columns are
        // virtualised: off, panning changes no cell (ADR-0004).
        const flung = (painted !== undefined && landed !== undefined && landed.start !== painted.start
            && defined(this.#geometry, 'the geometry a slice came from').isFling(painted.start, landed.start))
            || (this.#virtualiseColumns && this.#styles.geometry.isFling(previousLeftPx, left));
        if (flung) this.#placeholderMode = true;
        if (this.#placeholderMode) {
            // Any scrolling keeps it unsettled: the delay measures stillness.
            this.#cancelSettle?.();
            this.#cancelSettle = this.#reports.clock.setTimer(this.#onSettle, settleDelayMs);
        }
        this.#invalidate();
    }

    readonly #onSettle = (): void => {
        this.#cancelSettle = undefined;
        this.#placeholderMode = false;
        this.#invalidate();
    };

    readonly #onScrollerFocus = (): void => {
        // The scroller takes DOM focus from a press on the rows; the keys and aria-activedescendant are the
        // root's (ADR-0033), so it is handed straight on.
        this.#root.focus({ preventScroll: true });
    };

    #invalidate(): void {
        if (this.#destroyed) return;
        this.#showSettled(false);
        if (this.#passOwed) return;
        this.#passOwed = true;
        queueMicrotask(() => {
            this.#passOwed = false;
            if (!this.#destroyed) this.#pass();
        });
    }

    #pass(): void {
        this.#reconcileSelection();
        this.#resolve();
        const revealed = this.#stageReveal();
        this.#stageFollow();
        this.#paint();
        if (revealed) {
            // Written in the task that painted the slice, so the offset and the rows reach the screen
            // together (ADR-0012, 2026-09-29); its scroll event then finds nothing moved.
            this.#scroller.scrollTop = this.#scrollTopPx;
            this.#scroller.scrollLeft = this.#scrollLeftPx;
            this.#repaintAfterReveal(this.#scrollTopPx, this.#scrollLeftPx);
        }
        this.#raiseRangeIfUncovered();
        this.#raiseSelection();
        this.#lookSettled();
    }

    // Settled while nothing is owed: no pass, no fling's settle, no announcement, no reveal's repaint and
    // no page follow, and the browser has told the box and the ceiling (ADR-0220).
    #lookSettled(): void {
        this.#showSettled(!this.#passOwed && this.#cancelSettle === undefined && this.#cancelAnnounce === undefined
            && this.#repaint === undefined && this.#followArmed === undefined && this.#cancelFollow === undefined
            && this.#viewportReport !== undefined && this.#ceilingPx !== undefined);
    }

    #showSettled(settled: boolean): void {
        if (settled === this.#settledShown) return;
        this.#settledShown = settled;
        this.#root.toggleAttribute('data-ex-settled', settled);
    }

    // ---- Resolving (the C# core's PrepareRender) ------------------------------------------------------

    get #bandHeightPx(): number {
        return this.#metrics.headerHeightPx;
    }

    #resolve(): void {
        const report = this.#viewportReport;
        const visibleWidthPx = this.#width === 'stretch'
            ? Math.max(0, report?.contentWidthPx ?? 0) // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0028: a stretched axis is 0 until the browser reports it, and paints nothing
            : ViewportBox.visibleWidthOf(this.#width, report?.gutterWidthPx ?? 0); // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0013: the gutter is 0 until the browser reports otherwise
        const visibleHeightPx = this.#height === 'stretch'
            ? Math.max(0, report?.contentHeightPx ?? 0) // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0028: as the width
            : ViewportBox.visibleHeightOf(this.#height, report?.gutterHeightPx ?? 0); // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0013: as the width
        const band = this.#bandHeightPx;
        const total = this.#totalRows;
        if (visibleHeightPx <= band || visibleWidthPx <= 0) {
            // A declared height this small is the Consumer's defect, said by name; a reported zero is a
            // hidden tab, and paints nothing until the next report (ADR-0013, ADR-0028).
            if (visibleHeightPx <= band && this.#height !== 'stretch') {
                throw new Error(`A horizontal scrollbar takes ${report?.gutterHeightPx ?? 0}px out of the ${this.#height}px Viewport, ` // oxlint-disable-line ex-grid/no-stand-in-default -- the message names the gutter, 0 until reported
                    + `leaving ${visibleHeightPx}px — not enough for the header band's own ${band}px, let alone a row. `
                    + 'Declare a Viewport tall enough to hold its own scrollbar (ADR-0013).');
            }
            this.#visible = undefined;
            this.#scrollable = undefined;
            this.#geometry = undefined;
            this.#resolveColumnGeometry(visibleWidthPx);
            return;
        }
        // The rows share the scroll content with the header band, so they get what it leaves, under the
        // ceiling less the band (ADR-0013, ADR-0053).
        this.#geometry = new ViewportGeometry({
            rowHeightPx: this.#metrics.rowHeightPx,
            viewportHeightPx: visibleHeightPx - band,
            totalRowCount: total,
            scrollRoomPx: Math.max(1, (this.#ceilingPx ?? ViewportGeometry.maxScrollHeightPx) - band),
        });
        this.#visible = this.#geometry.sliceAt(this.#scrollTopPx);
        this.#observeAutoWidths();
        this.#resolveColumnGeometry(visibleWidthPx);
        this.#scrollable = this.#styles.geometry.scrollableSliceAt(this.#scrollLeftPx, this.#virtualiseColumns);
    }

    get #totalRows(): number {
        return this.#state.totalCount ?? this.#state.windowStart + this.#state.window.length;
    }

    // Auto widths grow from what has been painted, every column observed so the horizontal axis stays
    // still, and only the rows newly on screen read (ADR-0016). Nothing is painted mid-fling, so nothing is
    // measured then.
    #observeAutoWidths(): void {
        const { window, windowStart } = this.#state;
        const sameWindow = this.#measuredWindow === window && this.#measuredWindowStart === windowStart;
        if (this.#placeholderMode && this.#measuredWindow !== undefined) return;
        const visible = this.#visible;
        const seen = sameWindow ? this.#measuredVisible : undefined;
        if (sameWindow && seen?.start === visible?.start && seen?.count === visible?.count) return;
        this.#measuredWindow = window;
        this.#measuredWindowStart = windowStart;
        this.#measuredVisible = visible;
        if (visible === undefined) return;
        const widths = [...this.#autoWidths];
        for (let index = visible.start; index < visible.start + visible.count; index++) {
            if (seen !== undefined && index >= seen.start && index < seen.start + seen.count) continue;
            const slice = index - windowStart;
            if (slice < 0 || slice >= window.length) continue;
            const row = itemAt(window, slice);
            this.#columns.forEach(({ column, spec }, c) => {
                if (!spec.width.isAuto) return;
                const text = textFor(column, column.value(row));
                widths[c] = itemAt(widths, c).observe(this.#metrics.cellMetrics.for(column.type).estimatePx(text));
            });
        }
        this.#autoWidths = widths;
    }

    // A new geometry only when something it is made of moved, so an unchanged layout keeps its instance and
    // the rows keep skipping (ADR-0003).
    #resolveColumnGeometry(viewportWidthPx: number): void {
        const widths = this.#columns.map(({ spec }, c) => spec.resolveWidthPx(itemAt(this.#autoWidths, c)));
        const geometry = this.#styles.geometry;
        const width = Math.max(1, viewportWidthPx);
        if (geometry.count === widths.length && geometry.requestedPinnedCount === this.#pinnedCount
            && geometry.viewportWidthPx === width && geometry.devicePixelRatio === this.#devicePixelRatio
            && widths.every((px, c) => geometry.declaredWidthPxOf(c) === px)) {
            return;
        }
        this.#styles = new ColumnStyles(new ColumnGeometry({
            widthsPx: widths,
            pinnedCount: this.#pinnedCount,
            viewportWidthPx: width,
            ...(this.#devicePixelRatio === undefined ? {} : { devicePixelRatio: this.#devicePixelRatio }),
        }));
    }

    #raiseRangeIfUncovered(): void {
        // Mid-fling the range is passing through; settling raises the one landed on (ADR-0004).
        const range = this.#visible;
        if (this.#placeholderMode) return;
        const { window, windowStart } = this.#state;
        if (range === undefined || (range.start >= windowStart && range.start + range.count <= windowStart + window.length)) {
            this.#lastRaised = undefined;
            return;
        }
        // Asked once, and not again until the Window moved: where it sits counts, never which instance it
        // is, or a source answering with rows that still miss would be asked forever (ADR-0001).
        const last = this.#lastRaised;
        if (last !== undefined && last.range.start === range.start && last.range.count === range.count
            && last.windowStart === windowStart && last.windowCount === window.length) {
            return;
        }
        this.#lastRaised = { range, windowStart, windowCount: window.length };
        // A source whose fetch fails says so through its own rejection, which reaches the page unheard as
        // any unhandled error does: nothing here makes it go away (ADR-0025).
        void this.#source.needRange(rowRange(range.start, range.count));
    }

    // ---- The Selection, the keys and the press (ADR-0011, ADR-0012, ADR-0052) ----------------------------

    get #extent(): GridExtent {
        return { rowCount: this.#totalRows, columnCount: this.#columns.length };
    }

    // Drops the Selection when the positions it is written in stop meaning what they meant (ADR-0011): the
    // Row Sequence Version for the rows, which moves on with another source too, and the columns by name,
    // in order, never the array's identity, which a Framework Adapter hands afresh on every render.
    #reconcileSelection(): void {
        const version = this.#sequence();
        const names = this.#columns.map(({ column }) => column.name);
        this.#held = sameItems(names, this.#columnNames) ? this.#held.under(version) : HeldSelection.empty(version);
        this.#columnNames = names;
    }

    // Takes a gesture's Selection; whether it changed anything.
    #apply(next: Selection): boolean {
        if (next.equals(this.#held.selection)) return false;
        this.#held = this.#held.with(next);
        return true;
    }

    // The Consumer hears the Selection the pass painted, once however many gestures made it, and the live
    // region is armed for it (ADR-0033).
    #raiseSelection(): void {
        const selection = this.#held.selection;
        if (selection === this.#raised) return;
        this.#raised = selection;
        this.#scheduleAnnouncement(selection);
        this.#onSelectionChanged?.(selection);
    }

    // Only when the ranges moved: a Focus move announces nothing, aria-activedescendant moved with it. A
    // 1×1 or empty Selection is not announced, and a sentence describing the one it replaced is emptied,
    // silently (A11Y-10). The rest is said once the Selection has stood still for the settle delay.
    #scheduleAnnouncement(selection: Selection): void {
        const { ranges } = selection;
        if (sameRanges(ranges, this.#announcedRanges)) return;
        if (ranges.length === 0 || (ranges.length === 1 && rangeCellCount(itemAt(ranges, 0)) === 1)) {
            this.#announcedRanges = ranges;
            this.#cancelAnnounce?.();
            this.#cancelAnnounce = undefined;
            if (this.#describesSelection) this.#say('');
            return;
        }
        this.#cancelAnnounce?.();
        this.#cancelAnnounce = this.#reports.clock.setTimer(this.#onAnnounceSettled, settleDelayMs);
    }

    readonly #onAnnounceSettled = (): void => {
        this.#cancelAnnounce = undefined;
        if (this.#destroyed) return;
        const selection = this.#held.selection;
        if (!sameRanges(selection.ranges, this.#announcedRanges)) {
            this.#announcedRanges = selection.ranges;
            this.#say(this.#describe(selection));
            this.#describesSelection = true;
        }
        this.#lookSettled();
    };

    #say(sentence: string): void {
        this.#describesSelection = false;
        this.#announce.textContent = sentence;
    }

    // The extent in one sentence, naming the corners by index (ADR-0033), as the C# core words it.
    #describe(selection: Selection): string {
        const { ranges } = selection;
        if (ranges.length === 0) return '';
        if (ranges.length > 1) return `${selection.cellCount} cells in ${ranges.length} ranges selected`;
        const range = itemAt(ranges, 0);
        const left = itemAt(this.#columns, range.leftColumn).header;
        const right = itemAt(this.#columns, range.leftColumn + range.columnCount - 1).header;
        return `${range.rowCount} rows by ${range.columnCount} columns selected, `
            + `${left} ${range.topRow + 1} to ${right} ${range.topRow + range.rowCount}`;
    }

    readonly #onKeyDown = (event: KeyboardEvent): void => {
        // Mid-composition an IME owns Enter, Escape and the arrows; and a key typed in a control inside the
        // root is that control's.
        if (event.isComposing || event.keyCode === 229 || event.target !== this.#root || modifierKeys.has(event.key)) return;
        const press = {
            key: event.key, ctrl: event.ctrlKey, shift: event.shiftKey, alt: event.altKey, meta: event.metaKey,
            metaIsPrimary: this.#metaIsPrimary,
        };
        // A released Tab is the browser's, and any other key ends the release (ADR-0012).
        if (this.#tabReleased) {
            this.#tabReleased = false;
            const canonical = canonicalKey(press);
            if (canonical === 'Tab' || canonical === 'Shift+Tab') return;
        }
        const action = resolveKey(press);
        if (action.kind !== 'leave' && !movingKinds.has(action.kind)) return;
        event.preventDefault();
        event.stopPropagation();
        this.#answered += 1;
        if (action.kind === 'leave') {
            // Escape with nothing left to dismiss is the way out of Enter and Tab cycling: the root keeps DOM
            // focus and the next Tab leaves the grid.
            this.#tabReleased = true;
        } else {
            this.#moveByKey(action);
        }
        this.#invalidate();
    };

    #moveByKey(action: GridKeyAction): void {
        this.#reconcileSelection();
        const geometry = this.#geometry;
        const visible = this.#visible;
        // Nothing painted is nothing the keys can be aimed at.
        if (geometry === undefined || visible === undefined) return;
        // A key pressed with nothing selected starts at the first cell on screen (ADR-0012): the first
        // Pinned Column, or the first scrollable column painted; with neither, there is no column at all.
        const scrollable = this.#scrollable;
        const start = cellPosition(visible.start, this.#styles.geometry.pinnedCount > 0 || scrollable === undefined ? 0 : scrollable.start);
        // PageUp and PageDown move by the rows fully visible, at least one (ADR-0012).
        const viewportRows = Math.max(1, Math.floor(geometry.viewportHeightPx / this.#metrics.rowHeightPx));
        const move = moveByKey(action, this.#held.selection, this.#extent, start, viewportRows);
        if (move === undefined) return;
        this.#apply(move.selection);
        this.#revealOwed = move.reveal;
        const focus = move.selection.focus;
        // And the page follows what the reveal brings into view (ADR-0204).
        if (focus !== undefined) this.#followArmed = { focus, target: move.reveal.target };
    }

    readonly #onPress = (event: MouseEvent): void => {
        // Only a press on the rows' own box, whose cells take no pointer events: its offsets are the
        // Viewport's, which the arithmetic reads (ADR-0012).
        if (event.button !== 0 || event.target !== this.#viewport) return;
        this.#reconcileSelection();
        const at = this.#cellUnder(event);
        if (at === undefined) return;
        this.#answered += 1;
        this.#pressed = true;
        // A press on the grid ends a release of Tab: the user has come back to it (ADR-0012).
        this.#tabReleased = false;
        const extent = this.#extent;
        const current = this.#held.selection;
        const toggling = !event.shiftKey && (event.ctrlKey || (event.metaKey && this.#metaIsPrimary));
        const next = event.shiftKey ? current.extendTo(at, extent)
            : toggling ? current.toggleRange(at, extent)
                : current.click(at, extent);
        this.#apply(next);
        // A Ctrl+press that took a cell out of the Selection starts no drag.
        this.#setDragging(!(toggling && current.contains(at)));
        this.#invalidate();
    };

    readonly #onDrag = (event: MouseEvent): void => {
        if ((event.buttons & 1) === 0) {
            this.#setDragging(false);
            return;
        }
        if (event.target !== this.#viewport) return;
        const at = this.#cellUnder(event);
        if (at !== undefined && this.#apply(this.#held.selection.extendTo(at, this.#extent))) this.#invalidate();
    };

    // The release of a press the grid answered is an input answered too, as the C# core counts it
    // (ADR-0220).
    readonly #onRelease = (): void => {
        if (!this.#pressed) return;
        this.#pressed = false;
        this.#answered += 1;
        this.#invalidate();
    };

    // A drag ends at its release anywhere on the grid, over the header too; released outside it, the next
    // move without the button ends it (#onDrag). Nothing is heard on the document: every grid on the page
    // would hear every drag (ADR-0018).
    readonly #onDragEnd = (): void => {
        this.#setDragging(false);
    };

    // DOM focus leaving the grid ends a release of Tab, as the C# core's key gate does (ADR-0080).
    readonly #onFocusLeft = (event: FocusEvent): void => {
        const next = event.relatedTarget;
        if (!(next instanceof Node) || !this.#root.contains(next)) this.#tabReleased = false;
    };

    // The move listener stands only while a drag runs, so a pointer passing over costs nothing.
    #setDragging(dragging: boolean): void {
        if (dragging === this.#dragging) return;
        this.#dragging = dragging;
        if (dragging) {
            this.#viewport.addEventListener('mousemove', this.#onDrag);
            this.#root.addEventListener('mouseup', this.#onDragEnd);
        } else {
            this.#viewport.removeEventListener('mousemove', this.#onDrag);
            this.#root.removeEventListener('mouseup', this.#onDragEnd);
        }
    }

    // The cell under a pointer on the Viewport, by arithmetic on its offsets (ADR-0012): rows counted from
    // the row painted first, columns from the content's left edge.
    #cellUnder(event: MouseEvent): CellPosition | undefined {
        const geometry = this.#geometry;
        const visible = this.#visible;
        if (geometry === undefined || visible === undefined) return undefined;
        const row = geometry.rowAt(event.offsetY + geometry.offsetPxOf(visible.start));
        const column = this.#styles.geometry.columnAt(event.offsetX, this.#scrollLeftPx);
        return row === undefined || column === undefined ? undefined : cellPosition(row, column);
    }

    #cellId(position: CellPosition): string {
        return `${this.#ids}r${position.row}c${position.column}`;
    }

    // Whether a cell is in the DOM now: its row painted with its data, and its column pinned or in the
    // horizontal slice, never mid-fling, when the scrollable cells are skipped (ADR-0004, ADR-0033).
    #cellIsPainted(position: CellPosition): boolean {
        const visible = this.#visible;
        if (visible === undefined || position.row < visible.start || position.row >= visible.start + visible.count) return false;
        const slice = position.row - this.#state.windowStart;
        if (slice < 0 || slice >= this.#state.window.length) return false;
        if (position.column < this.#styles.geometry.pinnedCount) return true;
        const columns = this.#scrollable;
        return !this.#placeholderMode && columns !== undefined
            && position.column >= columns.start && position.column < columns.start + columns.count;
    }

    // Moves the model to where a key's reveal is going, and resolves the slice there, so the pass paints
    // the rows at the offset it then writes (ADR-0012, 2026-09-29). Whether there is an offset to write.
    #stageReveal(): boolean {
        const reveal = this.#revealOwed;
        this.#revealOwed = undefined;
        const geometry = this.#geometry;
        if (reveal === undefined || geometry === undefined) return false;
        const { target } = reveal;
        const columns = this.#styles.geometry;
        if (target.row >= this.#totalRows || target.column >= columns.count) return false;
        // PageUp and PageDown scroll by the rows the Focus moved, through the mapping, so a compressed grid
        // moves by rows (ADR-0053); the reveal still runs over the stepped offset.
        const stepped = reveal.stepRows === 0
            ? this.#scrollTopPx
            : geometry.scrollTopStepped(this.#scrollTopPx, reveal.stepRows * this.#metrics.rowHeightPx);
        let top = geometry.scrollTopToReveal(target.row, stepped);
        // "Go to the beginning" is not "make this cell visible": with Pinned Columns the two come apart.
        let left = reveal.rowStart ? 0 : columns.scrollLeftToReveal(target.column, this.#scrollLeftPx);
        // While extending, an axis the range spans end to end is not scrolled for (ADR-0052, 2026-09-29).
        const range = reveal.extending;
        if (range !== undefined) {
            const extent = this.#extent;
            if (spansEveryRow(range, extent)) top = this.#scrollTopPx;
            if (spansEveryColumn(range, extent) && !reveal.rowStart) left = this.#scrollLeftPx;
        }
        if (top === this.#scrollTopPx && left === this.#scrollLeftPx) return false;
        this.#scrollTopPx = top;
        this.#scrollLeftPx = left;
        this.#revealToken += 1;
        this.#repaint?.cancel();
        this.#repaint = undefined;
        this.#resolve();
        return true;
    }

    // A reveal's repaint (ADR-0012, 2026-10-08): under Citrix with hardware acceleration off, a far reveal
    // left the Viewport white until the offset moved once more. So the offset moves a pixel in the frame
    // after the reveal's and back in the frame after that. The grid hears nothing of it (#onScroll), and a
    // scroll the user makes in between stands: the move back is not made, and the offset is read again. The
    // move back writes the value the reveal wrote, on the moved axis only.
    #repaintAfterReveal(writtenTop: number, writtenLeft: number): void {
        const scroller = this.#scroller;
        const current: Repaint = { top: scroller.scrollTop, left: scroller.scrollLeft, cancel: () => { /* no frame asked yet */ } };
        this.#repaint = current;
        current.cancel = this.#reports.nextFrame(this.#root, () => {
            if (this.#repaint !== current) return;
            if (scroller.scrollTop !== current.top || scroller.scrollLeft !== current.left) {
                this.#yieldRepaint();
                return;
            }
            // Away from the edge the offset stands at, so the move is never clamped to nothing; across, where
            // the rows cannot scroll at all.
            scroller.scrollTop = current.top + (current.top > 0 ? -1 : 1);
            const across = scroller.scrollTop === current.top;
            if (across) scroller.scrollLeft = current.left + (current.left > 0 ? -1 : 1);
            const moved = { top: scroller.scrollTop, left: scroller.scrollLeft };
            if (moved.top === current.top && moved.left === current.left) {
                this.#endRepaint();
                return;
            }
            current.cancel = this.#reports.nextFrame(this.#root, () => {
                if (this.#repaint !== current) return;
                if (scroller.scrollTop !== moved.top || scroller.scrollLeft !== moved.left) {
                    this.#yieldRepaint();
                    return;
                }
                if (across) scroller.scrollLeft = writtenLeft;
                else scroller.scrollTop = writtenTop;
                this.#endRepaint();
            });
        });
    }

    // The offset is read once more: a write the browser clamped stands where it landed, not where it was
    // asked to.
    #endRepaint(): void {
        this.#repaint = undefined;
        this.#onScroll();
        this.#lookSettled();
    }

    // The user scrolled during the repaint: their offset stands, and is read.
    #yieldRepaint(): void {
        this.#repaint = undefined;
        this.#onScroll();
        this.#lookSettled();
    }

    // Numbers a page follow once the cell it brings in is painted (ADR-0204), and asks the browser after
    // the frame that paints it, and after a reveal's repaint. One whose Focus has moved on is dropped.
    #stageFollow(): void {
        const follow = this.#followArmed;
        if (follow === undefined) return;
        const focus = this.#held.selection.focus;
        if (focus === undefined || !sameCell(focus, follow.focus)) {
            this.#followArmed = undefined;
            return;
        }
        if (!this.#cellIsPainted(follow.target)) return;
        this.#followArmed = undefined;
        this.#followToken += 1;
        this.#cancelFollow?.();
        const afterPaint = (): void => {
            let cancelTimer: (() => void) | undefined;
            const cancelFrame = this.#reports.nextFrame(this.#root, () => {
                cancelTimer = this.#reports.clock.setTimer(() => {
                    if (this.#repaint !== undefined) {
                        afterPaint();
                        return;
                    }
                    this.#cancelFollow = undefined;
                    this.#follow(follow);
                    this.#lookSettled();
                }, 0);
            });
            this.#cancelFollow = () => {
                cancelFrame();
                cancelTimer?.();
            };
        };
        afterPaint();
    }

    // The browser places, the grid does not measure (ADR-0204): the page moves the cell into the window at
    // the nearest edge, only while the keyboard is still this grid's and the Focus is the one the follow
    // was taken for. The grid's own scroller is the grid's, and is put back if the browser moved it.
    #follow(follow: Follow): void {
        const focus = this.#held.selection.focus;
        if (this.#destroyed || focus === undefined || !sameCell(focus, follow.focus) || !this.#keyboardHere()) return;
        const element = this.#root.querySelector(`#${CSS.escape(this.#cellId(follow.target))}`);
        if (element === null) return;
        const scroller = this.#scroller;
        const top = scroller.scrollTop;
        const left = scroller.scrollLeft;
        element.scrollIntoView({ block: 'nearest', inline: 'nearest' });
        if (scroller.scrollTop !== top) scroller.scrollTop = top;
        if (scroller.scrollLeft !== left) scroller.scrollLeft = left;
    }

    // DOM focus inside this root and not in a grid nested in it, or on nothing.
    #keyboardHere(): boolean {
        const active = this.#root.ownerDocument.activeElement;
        const doc = this.#root.ownerDocument;
        return active === null || active === doc.body || active === doc.documentElement
            || (this.#root.contains(active) && active.closest('.ex-grid') === this.#root);
    }

    // The selection layers' rectangles over the painted rows, written only when they changed. The layers
    // the Viewport holds first, in order.
    #paintLayers(): readonly HTMLElement[] {
        const visible = this.#visible;
        const geometry = this.#geometry;
        // What lies above the layer at its edges (ADR-0008, 2026-10-01): the first row whose top edge is below
        // the readable area's top, and the offset that takes a column under the pinned block.
        const firstOpenRow = visible === undefined || geometry === undefined ? -1
            : geometry.viewportTopPxOf(visible.start, this.#scrollTopPx) > 0.01 ? visible.start : visible.start + 1;
        const inputs = {
            selection: this.#held.selection, start: visible?.start, count: visible?.count, styles: this.#styles,
            rowHeightPx: this.#metrics.rowHeightPx, scrollLeftPx: this.#scrollLeftPx, firstOpenRow,
        };
        const was = this.#layerInputs;
        // A pass that moved none of them, every scroll frame along an axis the layers ignore, writes nothing.
        if (was === undefined || Object.entries(inputs).some(([name, value]) => was[name] !== value)) {
            this.#layerInputs = inputs;
            const pieces = visible === undefined || geometry === undefined
                ? { shade: undefined, pinned: [], scrollable: [] }
                : selectionLayers(inputs.selection, this.#styles.geometry, inputs.rowHeightPx, visible,
                    { firstOpenRow, scrollLeftPx: this.#scrollLeftPx, hasHeader: true });
            const signature = JSON.stringify(pieces);
            if (signature !== this.#paintedLayers) {
                this.#paintedLayers = signature;
                const fill = (layer: HTMLElement, held: readonly LayerPiece[]): void => {
                    layer.replaceChildren(...held.map(({ className, style }) => {
                        const made = this.#root.ownerDocument.createElement('div');
                        made.className = className;
                        made.setAttribute('style', style);
                        return made;
                    }));
                };
                if (pieces.shade !== undefined) fill(this.#shadeLayer, pieces.shade);
                fill(this.#pinnedLayer, pieces.pinned);
                fill(this.#selectionLayer, pieces.scrollable);
                this.#shadeHolds = pieces.shade !== undefined;
            }
        }
        return this.#shadeHolds ? [this.#shadeLayer, this.#pinnedLayer, this.#selectionLayer] : [this.#pinnedLayer, this.#selectionLayer];
    }

    // ---- Painting -------------------------------------------------------------------------------------

    /** Writes an attribute only when it changed, so an unchanged pass touches nothing. */
    #write(element: HTMLElement, key: string, name: string, value: string | undefined): void {
        const slot = `${key}:${name}`;
        if (this.#written.get(slot) === value) return;
        if (value === undefined) {
            this.#written.delete(slot);
            element.removeAttribute(name);
        } else {
            this.#written.set(slot, value);
            element.setAttribute(name, value);
        }
    }

    #paint(): void {
        const metrics = this.#metrics;
        const geometry = this.#styles.geometry;
        const root = this.#root;
        this.#write(root, 'root', 'class', this.#state.loading ? 'ex-grid ex-loading' : 'ex-grid');
        // The Geometry Tokens, inline on the root from the same resolved metrics the arithmetic reads
        // (ADR-0027, ADR-0028), and the Device Pixel once told (ADR-0090).
        this.#write(root, 'root', 'style', `--ex-row-height: ${metrics.rowHeightPx}px; --ex-header-height: ${metrics.headerHeightPx}px; `
            + `--ex-font-size: ${metrics.fontSizePx}px; --ex-cell-padding-x: ${metrics.cellPaddingXPx}px; `
            + `--ex-action-padding-x: ${metrics.actionPaddingXPx}px; --ex-action-border-width: ${metrics.actionBorderPx}px; `
            + `--ex-action-gap: ${metrics.actionGapPx}px; --ex-menu-button-width: ${metrics.menuButtonWidthPx}px; `
            + `--ex-menu-button-inset: ${metrics.menuButtonInsetPx}px; --ex-sort-mark-width: ${metrics.sortMarkWidthPx}px; `
            + `--ex-resize-grip-width: ${metrics.resizeGripWidthPx}px; --ex-column-guide-width: ${metrics.columnGuideWidthPx}px`
            + stretchRootStyle(this.#width, this.#height)
            + (this.#devicePixelRatio === undefined ? '' : `; --ex-dp: ${1 / this.#devicePixelRatio}px`));
        this.#write(root, 'root', 'aria-rowcount', String(this.#totalRows + headerRowCount));
        this.#write(root, 'root', 'aria-colcount', String(this.#columns.length));
        // The numbers a reveal and a page follow are known by (ADR-0012, ADR-0204), the Focus a follow
        // was taken for, painted or not, and the Focus cell for assistive technology only while it is in
        // the DOM, so the attribute never names a missing id (ADR-0033).
        this.#write(root, 'root', 'data-ex-reveal', String(this.#revealToken));
        this.#write(root, 'root', 'data-ex-follow', String(this.#followToken));
        const focus = this.#held.selection.focus;
        this.#write(root, 'root', 'data-ex-follow-focus', focus === undefined ? undefined : this.#cellId(focus));
        this.#write(root, 'root', 'aria-activedescendant',
            focus !== undefined && this.#cellIsPainted(focus) ? this.#cellId(focus) : undefined);
        this.#write(this.#stamp, 'stamp', 'data-ex-answered', String(this.#answered));
        this.#write(this.#scroller, 'scroller', 'style', this.#scrollerStyle());
        const scrollHeightPx = this.#geometry?.scrollHeightPx ?? 0; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0013: a Viewport with no room for a row scrolls nothing
        this.#write(this.#spacer, 'spacer', 'style', `width: ${geometry.totalWidthPx}px; height: ${this.#bandHeightPx + scrollHeightPx}px`);
        this.#paintHeader();
        this.#paintViewport();
    }

    #scrollerStyle(): string {
        const width = this.#width;
        const height = this.#height;
        if (width === 'stretch' && height === 'stretch') return 'flex: 1 1 auto; min-height: 0';
        if (width === 'stretch') return `height: ${height}px`;
        if (height === 'stretch') return `width: ${width}px; flex: 1 1 auto; min-height: 0`;
        return `width: ${width}px; height: ${height}px`;
    }

    #paintHeader(): void {
        const painted = this.#paintedHeader;
        // The Sort in force is painted too, as each header's aria-sort (ADR-0033).
        const sorts = this.#state.sorts.map(({ column, direction }) => `${JSON.stringify(column)}:${direction}`).join(',');
        if (painted !== undefined && painted.styles === this.#styles && painted.columns === this.#columns
            && sameRange(painted.scrollable, this.#scrollable) && painted.sorts === sorts) {
            this.#paintHeadingMarks(false);
            return;
        }
        this.#paintedHeader = { styles: this.#styles, columns: this.#columns, scrollable: this.#scrollable, sorts };
        const doc = this.#root.ownerDocument;
        const cells: HTMLElement[] = [];
        const headerCells: HeaderCell[] = [];
        const headerCell = (c: number, pinned: boolean): void => {
            const { column, header } = itemAt(this.#columns, c);
            const cell = doc.createElement('div');
            cell.className = headerClass(column.align, pinned, 'none');
            cell.setAttribute('style', pinned ? this.#styles.pinnedCell(c) : this.#styles.cell(c));
            cell.setAttribute('role', 'columnheader');
            cell.setAttribute('aria-colindex', String(c + 1));
            // Sorted or not, a column that can be sorted says so (ADR-0033).
            if (column.queryable !== false) cell.setAttribute('aria-sort', this.#ariaSort(column.name));
            cell.textContent = header;
            cells.push(cell);
            headerCells.push({ element: cell, column: c, align: column.align, pinned });
        };
        for (let c = 0; c < this.#styles.geometry.pinnedCount; c++) headerCell(c, true);
        const scrollable = this.#scrollable;
        if (scrollable !== undefined) {
            if (this.#styles.hasGap(scrollable.start)) cells.push(this.#gap(scrollable.start));
            for (let c = scrollable.start; c < scrollable.start + scrollable.count; c++) headerCell(c, false);
        }
        this.#header.replaceChildren(...cells);
        this.#headerCells = headerCells;
        this.#paintHeadingMarks(true);
    }

    // The Heading Mark over the header (ADR-0008, 2026-10-13): every column holding part of the Selection
    // is marked, and one covered whole is marked so (D1), on the header cell's class; and a rectangle per
    // run of marked columns, over the band's last tier, on whose bottom edge the stylesheet draws the line.
    // Its Pinned Columns' part rides a layer first in the band, emitted only while it holds a mark; the rest
    // follows the header cells and passes beneath the pinned ones, as the columns do (ADR-0004). It is
    // painted over the header, so no row learns the Selection (ADR-0003).
    #paintHeadingMarks(rebuilt: boolean): void {
        const selection = this.#held.selection;
        const extent = this.#extent;
        let marks = this.#marks;
        if (marks === undefined || marks.selection !== selection
            || marks.extent.rowCount !== extent.rowCount || marks.extent.columnCount !== extent.columnCount) {
            marks = { selection, extent, columns: markedColumns(selection), whole: wholeColumnRuns(selection, extent) };
            this.#marks = marks;
        }
        const heightPx = this.#metrics.headerHeightPx;
        const topPx = this.#bandHeightPx - heightPx;
        const painted = this.#paintedMarks;
        if (!rebuilt && painted !== undefined && painted.marks === marks && painted.styles === this.#styles
            && painted.topPx === topPx && painted.heightPx === heightPx) {
            return;
        }

        for (const { element, column, align, pinned } of this.#headerCells) {
            const mark = runsHold(marks.whole, column) ? 'whole' : runsHold(marks.columns, column) ? 'marked' : 'none';
            const className = headerClass(align, pinned, mark);
            if (element.className !== className) element.className = className;
        }

        const geometry = this.#styles.geometry;
        const stylesIn = (pinnedLayer: boolean): readonly string[] =>
            marks.columns.flatMap(run => present(columnMark(run, geometry, pinnedLayer, topPx, heightPx)));
        const pinnedStyles = stylesIn(true);
        const scrollableStyles = stylesIn(false);
        let elements: { readonly pinned: HTMLElement | undefined; readonly scrollable: readonly HTMLElement[] };
        if (painted !== undefined && sameItems(painted.pinnedStyles, pinnedStyles) && sameItems(painted.scrollableStyles, scrollableStyles)) {
            // The same rectangles: a header rebuilt by a scroll or a Sort takes the marks it had back,
            // rather than making them again (PF-3).
            if (!rebuilt) {
                this.#paintedMarks = { ...painted, marks, styles: this.#styles, topPx, heightPx };
                return;
            }
            elements = painted.elements;
        } else {
            if (painted !== undefined) {
                painted.elements.pinned?.remove();
                for (const element of painted.elements.scrollable) element.remove();
            }
            elements = this.#headingMarkElements(pinnedStyles, scrollableStyles);
        }
        if (elements.pinned !== undefined) this.#header.prepend(elements.pinned);
        this.#header.append(...elements.scrollable);
        this.#paintedMarks = { marks, styles: this.#styles, topPx, heightPx, pinnedStyles, scrollableStyles, elements };
    }

    #headingMarkElements(pinnedStyles: readonly string[], scrollableStyles: readonly string[]): PaintedHeadingMarks['elements'] {
        const doc = this.#root.ownerDocument;
        const mark = (style: string): HTMLElement => {
            const element = doc.createElement('div');
            element.className = 'ex-heading-mark ex-heading-mark-column';
            element.setAttribute('style', style);
            return element;
        };
        let pinned: HTMLElement | undefined;
        if (pinnedStyles.length > 0) {
            pinned = doc.createElement('div');
            pinned.className = 'ex-heading-marks-pinned';
            pinned.setAttribute('aria-hidden', 'true');
            pinned.append(...pinnedStyles.map(mark));
        }
        const scrollable = scrollableStyles.map(style => {
            const element = mark(style);
            element.setAttribute('aria-hidden', 'true');
            return element;
        });
        return { pinned, scrollable };
    }

    #ariaSort(name: string): string {
        const sort = this.#state.sorts.find(s => s.column === name);
        if (sort === undefined) return 'none';
        return sort.direction === 'ascending' ? 'ascending' : 'descending';
    }

    #gap(firstScrollable: number): HTMLElement {
        const gap = this.#root.ownerDocument.createElement('div');
        gap.className = 'ex-gap';
        gap.setAttribute('style', this.#styles.gap(firstScrollable));
        gap.setAttribute('aria-hidden', 'true');
        return gap;
    }

    #paintViewport(): void {
        const viewport = this.#viewport;
        const visible = this.#visible;
        const geometry = this.#geometry;
        if (this.#layoutStyles !== this.#styles) {
            this.#layoutStyles = this.#styles;
            this.#layoutId += 1;
        }
        // Placed from the offset the slice is painted at, on the nearest Device Pixel (ADR-0053, DC-59).
        const topPx = geometry === undefined || visible === undefined ? 0 : geometry.paintedTopPxOf(visible.start, this.#scrollTopPx);
        this.#write(viewport, 'viewport', 'style', `transform: translateY(round(nearest, ${topPx}px, var(--ex-dp, 1px)))`);
        this.#write(viewport, 'viewport', 'data-ex-first-row', visible === undefined ? undefined : String(visible.start));
        this.#write(viewport, 'viewport', 'data-ex-sequence', String(this.#sequence()));
        this.#write(viewport, 'viewport', 'data-ex-layout', String(this.#layoutId));

        const layers = this.#paintLayers();
        const rows: HTMLElement[] = [...layers];
        const next = new Map<unknown, PaintedRow<Row>>();
        const nextPlaceholders = new Map<number, HTMLElement>();
        let repainted = false;
        if (visible !== undefined) {
            const { window, windowStart } = this.#state;
            const rowKey = this.#source.rowKey;
            for (let index = visible.start; index < visible.start + visible.count; index++) {
                const slice = index - windowStart;
                if (slice < 0 || slice >= window.length) {
                    let placeholder = this.#placeholders.get(index);
                    if (placeholder === undefined) {
                        placeholder = this.#placeholderRow(index);
                        repainted = true;
                    }
                    nextPlaceholders.set(index, placeholder);
                    rows.push(placeholder);
                    continue;
                }
                const row = itemAt(window, slice);
                const key: unknown = rowKey === undefined ? row : rowKey(row);
                const was = this.#painted.get(key);
                const scrollable = this.#placeholderMode ? undefined : this.#scrollable;
                if (was !== undefined && !next.has(key) && was.row === row && was.index === index && was.styles === this.#styles
                    && was.columns === this.#columns && was.placeholder === this.#placeholderMode && sameRange(was.scrollable, scrollable)) {
                    next.set(key, was);
                    rows.push(was.element);
                    continue;
                }
                const element = this.#rowElement(row, index, scrollable);
                repainted = true;
                // A key met twice in one Window is painted, never paired: which of the two a later row
                // continues cannot be told (ADR-0140).
                if (!next.has(key)) {
                    next.set(key, { element, row, index, styles: this.#styles, columns: this.#columns, placeholder: this.#placeholderMode, scrollable });
                }
                rows.push(element);
            }
        }
        this.#painted = next;
        this.#placeholders = nextPlaceholders;
        // In order, moving only what is out of place, so a row that stays is not touched (ADR-0003). A
        // selection layer coming or going paints no cell.
        const isLayer = (node: ChildNode): boolean =>
            node === this.#shadeLayer || node === this.#pinnedLayer || node === this.#selectionLayer;
        // The shade layer gone is taken out first, or every row after it would be moved past it.
        if (!layers.includes(this.#shadeLayer)) this.#shadeLayer.remove();
        let at: ChildNode | null = viewport.firstChild;
        for (const row of rows) {
            if (at === row) {
                at = at.nextSibling;
            } else {
                viewport.insertBefore(row, at);
                if (!isLayer(row)) repainted = true;
            }
        }
        while (at !== null) {
            const after: ChildNode | null = at.nextSibling;
            at.remove();
            if (!isLayer(at)) repainted = true;
            at = after;
        }
        if (repainted) this.#paintId += 1;
        this.#write(viewport, 'viewport', 'data-ex-paint', String(this.#paintId));
    }

    // A position with no row behind it at all: not even a Pinned Column has anything to paint, and
    // aria-busy, because a value that has not arrived must not read as one (ADR-0004, ADR-0033).
    #placeholderRow(index: number): HTMLElement {
        const element = this.#root.ownerDocument.createElement('div');
        element.className = rowClasses({ kind: 'detail', placeholder: true });
        element.setAttribute('role', 'row');
        element.setAttribute('aria-rowindex', String(headerRowCount + index + 1));
        element.setAttribute('aria-busy', 'true');
        return element;
    }

    // A row: the Pinned Columns, then one gap for what is virtualised away to the left, then the scrollable
    // columns on screen (ADR-0004). Mid-fling only the pinned cells are painted, and the row says it is busy.
    #rowElement(row: Row, index: number, scrollable: ColumnRange | undefined): HTMLElement {
        const element = this.#root.ownerDocument.createElement('div');
        element.className = rowClasses({ kind: 'detail', placeholder: this.#placeholderMode });
        element.setAttribute('role', 'row');
        element.setAttribute('aria-rowindex', String(headerRowCount + index + 1));
        if (this.#placeholderMode) element.setAttribute('aria-busy', 'true');
        for (let c = 0; c < this.#styles.geometry.pinnedCount; c++) element.append(this.#cell(row, index, c, true));
        if (scrollable !== undefined) {
            if (this.#styles.hasGap(scrollable.start)) element.append(this.#gap(scrollable.start));
            for (let c = scrollable.start; c < scrollable.start + scrollable.count; c++) element.append(this.#cell(row, index, c, false));
        }
        return element;
    }

    #cell(row: Row, index: number, c: number, pinned: boolean): HTMLElement {
        const { column, numeric } = itemAt(this.#columns, c);
        const text = textFor(column, column.value(row));
        // The declared width, not the one put on Device Pixels: an Auto column is exactly as wide as its
        // widest estimate, and rounding it must not hash the value it grew for (ADR-0090).
        const decision = decideOverflow(column.type, text, this.#styles.geometry.declaredWidthPxOf(c), this.#metrics.cellMetrics);
        const cell = this.#root.ownerDocument.createElement('div');
        cell.className = cellClasses({ numeric, pinned, state: 'normal', align: column.align ?? 'auto' }); // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0006: a column that declares no alignment aligns by its type
        cell.setAttribute('style', pinned ? this.#styles.pinnedCell(c) : this.#styles.cell(c));
        cell.setAttribute('role', 'gridcell');
        cell.setAttribute('id', `${this.#ids}r${index}c${c}`);
        cell.setAttribute('aria-colindex', String(c + 1));
        // The accessible name is the value wherever what is painted is not (ADR-0016, ADR-0033).
        if (decision.isHashed || decision.displayText !== text) cell.setAttribute('aria-label', text);
        if (decision.isHashed) {
            const hashes = this.#root.ownerDocument.createElement('span');
            hashes.className = 'ex-hashes';
            hashes.textContent = decision.displayText;
            cell.append(hashes);
        } else {
            cell.textContent = decision.displayText;
        }
        return cell;
    }
}

/** Whether one of `runs`, disjoint and in order, holds `index`: a binary search, so a header cell's mark
 * costs nothing per heading in the runs (ADR-0008's economy). */
function runsHold(runs: readonly ColumnRange[], index: number): boolean {
    let low = 0;
    let high = runs.length - 1;
    while (low <= high) {
        const middle = low + Math.floor((high - low) / 2);
        const run = itemAt(runs, middle);
        if (index < run.start) high = middle - 1;
        else if (index >= run.start + run.count) low = middle + 1;
        else return true;
    }
    return false;
}

const present = (style: string | undefined): readonly string[] => (style === undefined ? [] : [style]);

/** How the Heading Mark marks a column's header (ADR-0008): not, as holding part of the Selection, or as
 * covered whole by it (D1). */
type HeadingMark = 'none' | 'marked' | 'whole';

const headingMarkClasses: Readonly<Record<HeadingMark, string>> = {
    none: '', marked: ' ex-heading-marked', whole: ' ex-heading-marked ex-heading-whole',
};

/** A header cell's classes: pinned or not, its alignment and its Heading Mark, as the C# core composes them. */
function headerClass(align: GridColumn<unknown>['align'], pinned: boolean, mark: HeadingMark): string {
    const base = pinned ? 'ex-header-cell ex-pinned' : 'ex-header-cell';
    return (align === undefined || align === 'auto' ? base : `${base} ex-align-${align}`) + headingMarkClasses[mark];
}
