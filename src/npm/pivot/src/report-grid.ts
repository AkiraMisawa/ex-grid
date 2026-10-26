// The Pivot Report as ExGrid's Consumer draws it (ADR-0059, ADR-0063): its rows a Window the report source
// answers, its label columns pinned and its value columns under Header Groups. Nothing is held here but what
// one report's metadata says; the rows stay with the report source, asked for as the grid scrolls, so the
// DOM and the memory grow with the rows on screen, not with the report (ADR-0151, ADR-0027 P1).

import {
    GridSource, type FetchingGridSource, type GridColumn, type GridColumnWidth, type GridHeaderGroup, type GridPage, type GridQuery,
} from '@ex-grid/core';
import { decimalText } from '@ex-grid/data';
import {
    PivotRowKey, type PivotDisplayRow, type PivotLayout, type PivotReportMetadata, type PivotReportSettings, type PivotReportSource,
    type PivotReportState, type PivotValue,
} from '@ex-grid/pivot-engine';
import { elementAt } from './must';

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

/** A display row's identity across Windows and reports: its role, its Value Field and its Items. */
export function reportRowKey(row: PivotDisplayRow): string {
    return new PivotRowKey(row.role, row.valueField, row.items).toString();
}

function labelColumn(name: string, header: string, index: number, width: GridColumnWidth): GridColumn<PivotDisplayRow> {
    return Object.freeze({
        name,
        header,
        type: 'text',
        width,
        // A label is Text; an Item the report shows as a number is still a label, aligned as text (ADR-0059).
        align: 'left',
        queryable: false,
        value: (row: PivotDisplayRow) => elementAt(row.labels, index).text,
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
export interface ReportColumns {
    readonly columns: readonly GridColumn<PivotDisplayRow>[];
    readonly headerGroups: readonly GridHeaderGroup[];
    readonly pinnedColumnCount: number;
}

/** The label columns' widths when the user has not set one: the widest label the grid paints, between 40 and
 * 400 pixels, as Auto widths are (ADR-0016). */
const labelWidth: GridColumnWidth = Object.freeze({ auto: true });

/**
 * The grid's columns for a report: each label column, then each value column, with any width the user
 * dragged a column of that name to (ADR-0016), and the column Items as Header Groups over the value columns,
 * never over the pinned labels (ADR-0032).
 */
export function reportColumns(metadata: PivotReportMetadata, userWidths: ReadonlyMap<string, number>): ReportColumns {
    const widthOf = (name: string): GridColumnWidth | undefined => {
        const px = userWidths.get(name);
        return px === undefined ? undefined : Object.freeze({ px, minPx: Math.min(40, px), maxPx: Math.max(400, px) });
    };
    const columns: GridColumn<PivotDisplayRow>[] = [];
    metadata.labelColumns.forEach((label, index) => {
        columns.push(labelColumn(label.name, label.header, index, widthOf(label.name) ?? labelWidth));
    });
    metadata.valueColumns.forEach((value, index) => {
        columns.push(valueColumn(value.name, value.header, index, widthOf(value.name)));
    });
    const names = metadata.valueColumns.map(column => column.name);
    const headerGroups = metadata.headerSpans.map(span => Object.freeze({
        label: span.label,
        columns: Object.freeze(names.slice(span.firstColumn, span.firstColumn + span.columnCount)),
        tier: span.tier,
        tierSpan: span.tierSpan,
    }));
    return Object.freeze({ columns: Object.freeze(columns), headerGroups: Object.freeze(headerGroups), pinnedColumnCount: metadata.labelColumns.length });
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
    return GridSource.fetch(fetch, {
        rowKey: reportRowKey,
        readAheadRows: 60,
        // A Window of another report is never shown beside this one's rows: the pivot asks again, and the grid
        // stands on what it holds meanwhile. Any other failure is the grid's to say (ADR-0025).
        onFetchFailed: ({ error }) => {
            if (!(error instanceof ReportMovedError)) throw error;
            request.moved();
        },
    });
}
