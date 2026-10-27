// Show Details (ADR-0059, ADR-0063): the records behind a value cell, asked of the report source the report came
// from under its Source Version, so they add up to the cell. The Consumer takes them when it listens; otherwise they
// open as a sheet, in a tab at the report's foot or in a dialog: an ExGrid of the source's fields, fetched in pages,
// in the markup and under the classes the Razor component's PivotDetailsSheet and PivotDetailsGrid draw (ADR-0350
// section 4).

import { FetchingGridSource, type ColumnType, type GridColumn, type GridPage, type GridQuery } from '@ex-grid/core';
import { decimalText } from '@ex-grid/data';
import {
    formatPivotDetailValue, pivotDetailsQuery, type PivotDetailPage, type PivotDetailRecord, type PivotDetailValue, type PivotDetailsQuery, type PivotField,
    type PivotSource, type PivotSourceRefusal,
} from '@ex-grid/pivot-engine';
import { unreachable } from './must';
import type { PivotScreenWords } from './words';

/** One Item of a cell's path: its field's caption and its label, as the report shows them. */
export interface PivotDetailItem {
    /** The caption of the Item's field. */
    readonly field: string;
    /** The Item's label. */
    readonly item: string;
}

/** The records behind a value cell, which Show Details opens (ADR-0059): handed to `onShowDetails` when the Consumer
 * listens, and otherwise shown by the pivot. */
export interface PivotShowDetailsEvent {
    /** What the cell stands for, as its sheet is titled: `Details: EU / Rates`. */
    readonly title: string;
    /** The Items of the cell's row, outermost first. */
    readonly rowItems: readonly PivotDetailItem[];
    /** The Items of the cell's column, outermost first. */
    readonly columnItems: readonly PivotDetailItem[];
    /** The caption of the cell's Value Field, or `undefined` where the cell is not one Value Field's. */
    readonly valueField: string | undefined;
    /** The question for the records, plain data a Consumer may send elsewhere, as the Razor component's
     * `PivotDetails.Query` (ADR-0151): the cell's Items, the Hidden Items and the Source Version it is answered under. */
    readonly query: PivotDetailsQuery;
    /** The Pivot Source the report was computed from, which the records are asked of. */
    readonly source: PivotSource;
    /** The source's fields, which each record holds a value of, in order. */
    readonly fields: readonly PivotField[];
    /** A page of the records, from `start`, at most `count` of them, under the Source Version the report was computed
     * from: a source that no longer holds it refuses, rather than answer records that do not add up. */
    readonly records: (start: number, count: number, signal?: AbortSignal) => Promise<PivotDetailPage>;
}

/** Where the pivot shows the records when the Consumer does not take them: a tab at the report's foot, or a dialog. */
export type PivotDetailsView = 'tab' | 'dialog';

/** The records behind `query`'s cell, asked of the Pivot Source the report was computed from, which stays the
 * Consumer's however long the records are shown. */
export function detailsEvent(
    source: PivotSource, query: PivotDetailsQuery, described: Omit<PivotShowDetailsEvent, 'source' | 'query' | 'fields' | 'records'>,
): PivotShowDetailsEvent {
    return Object.freeze({
        ...described,
        source,
        query,
        fields: source.fields,
        // Checked as the question is: a page from a negative or broken start is refused by name, never asked.
        records: (start: number, count: number, signal?: AbortSignal) => source.details(pivotDetailsQuery({ ...query, start, count }), signal),
    });
}

/** A record's value as the details grid holds it: the text it shows, and its own text, which the grid copies. */
export class DetailCellValue {
    readonly #value: PivotDetailValue;
    /** The text the cell shows. */
    readonly text: string;

    constructor(value: PivotDetailValue, text: string) {
        this.#value = value;
        this.text = text;
    }

    /** What the cell is painted and aligned as: a value not of its field's declared type keeps its own (ADR-0060). */
    get type(): ColumnType {
        const value = this.#value;
        if (typeof value === 'string') return 'text';
        if (typeof value === 'boolean') return 'boolean';
        if (typeof value === 'bigint') return 'date';
        return 'number';
    }

    /** The raw text the clipboard carries: an exact number's digits, a double's shortest text, and otherwise what
     * the cell shows (ADR-0005). */
    toString(): string {
        const value = this.#value;
        if (typeof value === 'number' && Number.isFinite(value)) return String(value);
        if (typeof value === 'object' && value !== null) return decimalText(value);
        return this.text;
    }
}

/** Why the records cannot be shown: the source refused (the data changed under the report's Source Version, for
 * one), or it failed. */
export type DetailsProblem = { readonly refusal: PivotSourceRefusal } | { readonly error: unknown };

/** The sentence that says why the records cannot be shown. */
export function detailsProblemText(problem: DetailsProblem, words: PivotScreenWords): string {
    return 'error' in problem ? words.failed(problem.error) : words.sourceRefused(problem.refusal);
}

class RefusedError extends Error {
    readonly refusal: PivotSourceRefusal;

    constructor(refusal: PivotSourceRefusal) {
        super(refusal.message);
        this.refusal = refusal;
    }
}

function columnTypeOf(field: PivotField): ColumnType {
    switch (field.type) {
        case 'number': return 'number';
        case 'date': return 'date';
        case 'boolean': return 'boolean';
        case 'text': return 'text';
        default: return unreachable(field.type);
    }
}

function cellOf(value: unknown): DetailCellValue | undefined {
    if (value === undefined) return undefined;
    if (!(value instanceof DetailCellValue)) throw new TypeError('Internal: a details cell holds a record\'s value.');
    return value;
}

/** What a details cell is painted and aligned as: its value's own type, or its column's for a Blank. */
export function detailCellType(row: PivotDetailRecord, column: GridColumn<PivotDetailRecord>): ColumnType {
    // A Blank is painted as its column's type, as the Razor component's CellTypeOf does.
    return cellOf(column.value(row))?.type ?? column.type;
}

/**
 * One Show Details the pivot opened: the cell's records as a Grid Source of pages fetched from the report source,
 * under the Source Version the report was computed from, and one column per field of the source, headed by its
 * caption. It keeps that version for as long as it stands; a source that can no longer answer under it refuses, and
 * the sheet says the data has changed rather than show records that do not add up. It owns its source and releases
 * it when it closes.
 */
export class DetailsSheet {
    /** Unique in its pivot: its tab's id, and its panel's with `-panel`. */
    readonly id: string;
    readonly details: PivotShowDetailsEvent;
    /** The locale its records are written in: the pivot's when it opened, as its title's words are. */
    readonly locale: string;
    readonly columns: readonly GridColumn<PivotDetailRecord>[];
    readonly source: FetchingGridSource<PivotDetailRecord>;
    /** Why the records cannot be shown, or `undefined` while they can. */
    problem: DetailsProblem | undefined;
    readonly #closed = new AbortController();

    /** `changed` is called when the records can no longer be shown. */
    constructor(id: string, details: PivotShowDetailsEvent, locale: string, changed: () => void) {
        this.id = id;
        this.details = details;
        this.locale = locale;
        this.columns = Object.freeze(details.fields.map((field, index) => {
            const values = new WeakMap<PivotDetailRecord, DetailCellValue | undefined>();
            return Object.freeze({
                name: field.name,
                header: field.caption,
                type: columnTypeOf(field),
                queryable: false,
                value: (record: PivotDetailRecord) => {
                    if (values.has(record)) return values.get(record);
                    const value = record.values[index];
                    const cell = value === undefined || value === null ? undefined : new DetailCellValue(value, formatPivotDetailValue(value, field.format, locale));
                    values.set(record, cell);
                    return cell;
                },
                format: (value: unknown) => cellOf(value)?.text ?? '', // oxlint-disable-line ex-grid/no-stand-in-default -- a Blank shows nothing
            });
        }));
        this.source = FetchingGridSource.fetch((query, signal) => this.#fetch(query, signal), {
            onFetchFailed: ({ error }) => {
                if (this.#closed.signal.aborted) return;
                this.problem = error instanceof RefusedError ? Object.freeze({ refusal: error.refusal }) : Object.freeze({ error });
                changed();
            },
        });
    }

    async #fetch(query: GridQuery, signal: AbortSignal): Promise<GridPage<PivotDetailRecord>> {
        // The records behind a cell are in the data's order, and none is left out: the details grid offers no sort
        // and no filter, and a question carrying one is not answered quietly in another order (ADR-0059).
        if (query.sorts.length > 0 || query.filter !== undefined) throw new Error('The records behind a cell are shown in the data\'s order, unfiltered (ADR-0059).');
        const page = await this.details.records(query.range.start, query.range.count, signal);
        if (page.refusal !== undefined) throw new RefusedError(page.refusal);
        return Object.freeze({ rows: page.records, start: page.start, totalCount: page.total });
    }

    /** Releases its source: the sheet has closed. */
    dispose(): void {
        this.#closed.abort();
        this.source.dispose();
    }
}
