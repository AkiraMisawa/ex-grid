// The Pivot Report as ExGrid's Consumer draws it (ADR-0059, ADR-0063): its rows a Window the report source
// answers, its label columns pinned and its value columns under Header Groups. Nothing is held here but what
// one report's metadata says; the rows stay with the report source, asked for as the grid scrolls, so the
// DOM and the memory grow with the rows on screen, not with the report (ADR-0151, ADR-0027 P1).

import {
    GridSource, findMatches, gridHeaderRequiredPx, type FetchingGridSource, type GridColumn, type GridColumnWidth, type GridDensity, type GridHeaderGroup,
    type GridFindPattern, type GridFindRequest, type GridFindResult, type GridPage, type GridQuery, type GridSelection, type GridTemplate,
} from '@ex-grid/core';
import { decimalText } from '@ex-grid/data';
import {
    PivotRowKey, type PivotDisplayRow, type PivotFieldItem, type PivotLabelColumn, type PivotLayout, type PivotReportMetadata, type PivotReportSettings, type PivotReportSource,
    type PivotReportState, type PivotToggle, type PivotValue,
} from '@ex-grid/pivot-engine';
import { elementAt } from './must';
import { findInReport, type ReportFindGivenUp } from './report-find';
import { summarizeReport } from './report-summary';
import type { PivotScreenWords } from './words';

/**
 * A value cell's value as the grid holds it: the report's own text is what the cell paints, and the value's
 * own text, which the grid copies as the raw flavour and a spreadsheet reads back as the same number, is the
 * exact decimal's digits where there is one, the double's shortest text otherwise, and an error's text for an
 * error (ADR-0005, ADR-0060). The grid reads an object's text through `toString`, as C#'s reads `ToString`.
 */
export class ReportCellValue {
    readonly #value: PivotValue;

    constructor(value: PivotValue) {
        this.#value = value;
    }

    /** The text the report shows. */
    get text(): string {
        return this.#value.text;
    }

    /** The raw text the clipboard carries. */
    toString(): string {
        const { error, exact, number } = this.#value;
        if (error !== undefined) return error;
        return exact === undefined ? String(number) : decimalText(exact);
    }
}

/**
 * The Match Mark's rule over the report (ADR-0332 §4): the reference rule over the text a cell shows, which is the
 * text its Copy reads and `findInReport` finds by, so the mark and the step agree. One function for every pivot:
 * its identity reaches the grid (ADR-0003).
 */
export function reportMatch(row: PivotDisplayRow, column: GridColumn<PivotDisplayRow>, pattern: GridFindPattern): boolean {
    const value = column.value(row);
    // A label with no text, or a cell with no value, shows nothing.
    if (value === undefined) return findMatches('', pattern);
    if (value instanceof ReportCellValue) return findMatches(value.text, pattern);
    if (typeof value === 'string') return findMatches(value, pattern);
    throw new TypeError(`Internal: the report column '${column.name}' holds neither a label nor a value.`);
}

/** A display row's identity across Windows and reports: its role, its Value Field and its Items. */
export function reportRowKey(row: PivotDisplayRow): string {
    return new PivotRowKey(row.role, row.valueField, row.items).toString();
}

/** What a label cell paints beside its text: a level's indent, and the expand / collapse button's name and press. */
export interface LabelPainter {
    /** A level's indent, in pixels: the em of the grid's font, as the report source charges it (ADR-0151). */
    readonly indentPx: number;
    /** The button's accessible name: "Expand East", "Collapse East". */
    readonly toggleName: (toggle: PivotToggle) => string;
    /** The button pressed on `row`: the Item is to be collapsed or expanded. */
    readonly toggled: (toggle: PivotToggle, row: PivotDisplayRow) => void;
}

/**
 * One label column's cells (ADR-0059), the Razor component's markup: the indent, the expand / collapse button
 * of an outer Item, and the label. The button is pressed, never reached by Tab, and takes the keyboard when
 * Space asks the cell to (ADR-0037); a press on it keeps the keyboard where it was, as the grid's own buttons
 * do, and a double click on it stops there, since its offsets are the button's (ADR-0063).
 */
function labelTemplate(index: number, painter: LabelPainter): GridTemplate<PivotDisplayRow> {
    // A button pressed by Enter or Space asks the report again, and is painted anew: the grid takes back the
    // keyboard it held (ADR-0037).
    return (slot, cell) => {
        const label = elementAt(cell.row.labels, index);
        const document = slot.ownerDocument;
        const box = document.createElement('span');
        box.className = 'ex-pivot-label';
        if (label.indent > 0) box.style.paddingLeft = `${label.indent * painter.indentPx}px`;
        const listening = new AbortController();
        const { toggle } = label;
        let button: HTMLButtonElement | undefined;
        if (toggle !== undefined) {
            const made = document.createElement('button');
            made.type = 'button';
            made.className = toggle.isCollapsed ? 'ex-pivot-toggle ex-interactive ex-pivot-toggle-collapsed' : 'ex-pivot-toggle ex-interactive';
            made.tabIndex = -1;
            made.setAttribute('data-ex-claims', 'click');
            made.setAttribute('aria-expanded', toggle.isCollapsed ? 'false' : 'true');
            made.setAttribute('aria-label', painter.toggleName(toggle));
            made.textContent = toggle.isCollapsed ? '+' : '\u2212';
            const { signal } = listening;
            made.addEventListener('mousedown', event => { event.preventDefault(); }, { signal });
            made.addEventListener('click', () => { painter.toggled(toggle, cell.row); }, { signal });
            made.addEventListener('dblclick', event => { event.stopPropagation(); }, { signal });
            box.append(made);
            button = made;
        }
        if (label.text !== undefined) {
            const text = document.createElement('span');
            text.className = 'ex-pivot-label-text';
            text.textContent = label.text;
            box.append(text);
        }
        slot.append(box);
        if (button !== undefined && cell.focusRequest !== 0) cell.takeKeyboard(button);
        return () => {
            listening.abort();
            box.remove();
        };
    };
}

function labelColumn(name: string, header: string, index: number, width: GridColumnWidth, painter: LabelPainter): GridColumn<PivotDisplayRow> {
    return Object.freeze({
        name,
        header,
        type: 'text',
        width,
        // A label is Text; an Item the report shows as a number is still a label, aligned as text (ADR-0059).
        align: 'left',
        queryable: false,
        value: (row: PivotDisplayRow) => elementAt(row.labels, index).text,
        template: labelTemplate(index, painter),
    });
}

function cellValueOf(value: unknown): string {
    if (!(value instanceof ReportCellValue)) throw new TypeError('Internal: a value cell holds a report value.');
    return value.text;
}

function valueColumn(name: string, header: string, index: number, width: GridColumnWidth | undefined): GridColumn<PivotDisplayRow> {
    const values = new WeakMap<PivotDisplayRow, ReportCellValue | undefined>();
    return Object.freeze({
        name,
        header,
        type: 'number',
        ...(width === undefined ? {} : { width }),
        queryable: false,
        value: (row: PivotDisplayRow) => {
            if (values.has(row)) return values.get(row);
            const shown = row.values[index];
            const cell = shown === undefined ? undefined : new ReportCellValue(shown);
            values.set(row, cell);
            return cell;
        },
        format: cellValueOf,
    });
}

/** What the grid is handed for one report: its columns, its Header Groups and how many columns are pinned. */
export interface ReportColumnSet {
    readonly columns: readonly GridColumn<PivotDisplayRow>[];
    readonly headerGroups: readonly GridHeaderGroup[];
    readonly pinnedColumnCount: number;
}

/** The narrowest and the widest a label column is laid out at when the user has not set its width (ADR-0059). */
const minLabelWidthPx = 60;
const maxLabelWidthPx = 480;

function fixedWidth(px: number): GridColumnWidth {
    return Object.freeze({ px, minPx: Math.min(40, px), maxPx: Math.max(400, px) });
}

/**
 * The grid's columns for each report, made once for each name, header, place and width and held, so a report
 * that keeps a column hands the grid the same column, and its cells are not painted again (ADR-0003). Made
 * anew when what a label cell paints with changes.
 */
export class ReportColumns {
    readonly #painter: LabelPainter;
    readonly #density: GridDensity | undefined;
    readonly #made = new Map<string, GridColumn<PivotDisplayRow>>();

    constructor(painter: LabelPainter, density: GridDensity | undefined) {
        this.#painter = painter;
        this.#density = density;
    }

    /**
     * The grid's columns for a report: each label column, then each value column, with any width the user
     * dragged a column of that name to (ADR-0016), and the column Items as Header Groups over the value columns,
     * never over the pinned labels (ADR-0032). A label column the user has not sized is as wide as its header
     * or its widest label, which the report source estimated under the grid's text geometry, between 60 and 480
     * pixels, since its cells paint a template that no Auto width observes.
     */
    of(metadata: PivotReportMetadata, userWidths: ReadonlyMap<string, number>): ReportColumnSet {
        const columns: GridColumn<PivotDisplayRow>[] = [];
        metadata.labelColumns.forEach((label, index) => {
            const user = userWidths.get(label.name);
            const px = user ?? this.#labelWidth(label.header, metadata.labelWidths[index]);
            columns.push(this.#held(`l\u001F${label.name}\u001F${label.header}\u001F${index}\u001F${px}`,
                () => labelColumn(label.name, label.header, index, fixedWidth(px), this.#painter)));
        });
        metadata.valueColumns.forEach((value, index) => {
            const px = userWidths.get(value.name);
            columns.push(this.#held(`v\u001F${value.name}\u001F${value.header}\u001F${index}\u001F${px}`,
                () => valueColumn(value.name, value.header, index, px === undefined ? undefined : fixedWidth(px))));
        });
        // Columns no report has kept are let go once they outnumber the ones in use, as the Razor component's are.
        if (this.#made.size > (4 * columns.length) + 64) {
            const used = new Set(columns);
            for (const [key, column] of this.#made) if (!used.has(column)) this.#made.delete(key);
        }
        const names = metadata.valueColumns.map(column => column.name);
        const headerGroups = metadata.headerSpans.map(span => Object.freeze({
            label: span.label,
            columns: Object.freeze(names.slice(span.firstColumn, span.firstColumn + span.columnCount)),
            tier: span.tier,
            tierSpan: span.tierSpan,
        }));
        return Object.freeze({ columns: Object.freeze(columns), headerGroups: Object.freeze(headerGroups), pinnedColumnCount: metadata.labelColumns.length });
    }

    #labelWidth(header: string, label: number | undefined): number {
        const headerPx = gridHeaderRequiredPx(header, { ...(this.#density === undefined ? {} : { density: this.#density }), menuButton: false, sortable: false });
        // A report answered without label geometry has no estimate, and its header alone sizes it.
        const widest = label === undefined ? headerPx : Math.max(headerPx, label);
        return Math.min(maxLabelWidthPx, Math.max(minLabelWidthPx, Math.ceil(widest)));
    }

    #held(key: string, make: () => GridColumn<PivotDisplayRow>): GridColumn<PivotDisplayRow> {
        let column = this.#made.get(key);
        if (column === undefined) {
            column = make();
            this.#made.set(key, column);
        }
        return column;
    }
}

/** Thrown when a Window is answered from another report than the one the grid shows: the data moved on, and
 * the report is asked again rather than shown with rows of two reports (spine rule 6). */
export class ReportMovedError extends Error {
    constructor() {
        super('The report source answered a Window of another report than the one shown: the report is asked again.');
        this.name = 'ReportMovedError';
    }
}

function pageOf(state: PivotReportState): GridPage<PivotDisplayRow> {
    return Object.freeze({
        rows: state.rows,
        start: state.window.start,
        totalCount: state.metadata.rowCount,
        orderToken: state.metadata.rowSequenceVersion,
    });
}

/** What the rows of one report are asked under. */
export interface ReportRowsRequest {
    readonly source: PivotReportSource;
    readonly layout: PivotLayout;
    readonly settings: PivotReportSettings;
    readonly maxLeaves: number;
    /** The answer the report was shown from, whose rows are served without asking again. */
    readonly answered: PivotReportState;
    /** A request identity for each Window asked. */
    readonly nextRequestId: () => string;
    /** Told when a Window came from another report: the report is to be asked again. */
    readonly moved: () => void;
    /** Told when a Find step over the report cannot go on, before the step is given up (ADR-0332 §1). */
    readonly findGivenUp: (why: ReportFindGivenUp) => void;
    /** The pivot's words as they stand when a Selection Summary is declined (ADR-0060). */
    readonly words: () => PivotScreenWords;
}

/**
 * The rows of one report as a Grid Source (ADR-0350 section 16): each range the grid asks for is a Window of
 * the same report, under the layout and settings it was answered under, named by its Report Version, and a
 * Window of any other report is refused rather than shown beside it.
 */
export function reportRows(request: ReportRowsRequest): FetchingGridSource<PivotDisplayRow> {
    const { answered } = request;
    const version = answered.metadata.version;
    const fetch = async (query: GridQuery, signal: AbortSignal): Promise<GridPage<PivotDisplayRow>> => {
        const { start, count } = query.range;
        const held = answered.window;
        // The rows the report has end at its row count: a range past it is held when what of it there is, is.
        const end = Math.min(start + count, answered.metadata.rowCount);
        if (start >= held.start && end <= held.start + answered.rows.length) return pageOf(answered);
        const update = await request.source.window(Object.freeze({
            requestId: request.nextRequestId(),
            layout: request.layout,
            settings: request.settings,
            window: Object.freeze({ start, count }),
            baseline: version,
            markChanges: false,
            maxLeaves: request.maxLeaves,
        }), signal);
        if (update.refusal !== undefined) throw new Error(update.refusal.message);
        if (update.metadata.version !== version) throw new ReportMovedError();
        return pageOf(update);
    };
    // A step that cannot go on is given up: nothing moves, and nothing claims "no match" for cells it could not read
    // (ADR-0025). The grid takes an AbortError as a step its Consumer gave up; the pivot says why while the report is
    // the one shown, and over one since replaced the grid says its source changed.
    const find = async (step: GridFindRequest, _query: unknown, signal: AbortSignal): Promise<GridFindResult> => {
        const read = await findInReport(request.source, answered.metadata, step, signal);
        if (read.kind === 'answered') return read.answer;
        request.findGivenUp(read);
        throw new DOMException('The Find step over the report was given up.', 'AbortError');
    };
    return GridSource.fetch(fetch, {
        rowKey: reportRowKey,
        readAheadRows: 60,
        find,
        // The Selection Summary is the report source's, over the report the selection was made on (ADR-0130).
        summarize: (summary, _query, signal) => summarizeReport(request.source, version, summary, request.words(), signal),
        // A Window of another report is never shown beside this one's rows: the pivot asks again, and the grid
        // stands on what it holds meanwhile. Any other failure is the grid's to say (ADR-0025).
        onFetchFailed: ({ error }) => {
            if (!(error instanceof ReportMovedError)) throw error;
            request.moved();
        },
    });
}

/** What one cell of the report stands for (ADR-0340): its row's Items and, for a value cell, its column's. */
export interface PivotCellItems {
    /** The Items its row's path names, outermost first. */
    readonly row: readonly PivotFieldItem[];
    /** The Items its column's path names, outermost first; none for a label cell. */
    readonly column: readonly PivotFieldItem[];
}

// A label column of its own field (outline and tabular forms) shows that field's Item, so its cell stands for the
// path down to it; the compact form's one column shows the row's own.
function labelPath(path: readonly PivotFieldItem[], column: PivotLabelColumn): readonly PivotFieldItem[] {
    const at = column.field === undefined ? -1 : path.findIndex(item => item.field === column.field);
    return at < 0 ? path : Object.freeze(path.slice(0, at + 1));
}

const noItems: readonly PivotFieldItem[] = Object.freeze([]);

/**
 * What each cell of a selection of the report grid stands for (ADR-0340, Q5), as the Razor component's `ItemsOf`
 * reads it: each cell once, in the order selected. `undefined` when the selection reaches a row the grid does not
 * hold drawn now, or a column past the report's, which is read from no Items rather than from some of them
 * (spine rule 1).
 */
export function cellItems(
    selection: GridSelection, metadata: PivotReportMetadata, rows: readonly PivotDisplayRow[], windowStart: number,
): readonly PivotCellItems[] | undefined {
    const labels = metadata.labelColumns.length;
    // A cell two ranges both hold is said once; one range holds each of its cells once.
    const seen = selection.ranges.length > 1 ? new Set<string>() : undefined;
    const cells: PivotCellItems[] = [];
    for (const range of selection.ranges) {
        const bottom = range.topRow + range.rowCount - 1;
        const right = range.leftColumn + range.columnCount - 1;
        if (range.topRow < windowStart || bottom >= windowStart + rows.length || right >= labels + metadata.valueColumns.length) return undefined;
        for (let r = range.topRow; r <= bottom; r++) {
            const row = elementAt(rows, r - windowStart);
            for (let c = range.leftColumn; c <= right; c++) {
                if (seen !== undefined) {
                    const key = `${r},${c}`;
                    if (seen.has(key)) continue;
                    seen.add(key);
                }
                cells.push(Object.freeze(c < labels
                    ? { row: labelPath(row.rowPath, elementAt(metadata.labelColumns, c)), column: noItems }
                    : { row: row.rowPath, column: elementAt(metadata.valueColumns, c - labels).columnPath }));
            }
        }
    }
    return Object.freeze(cells);
}
