// A Board's grid view read (ADR-0342): C#'s GridViewSpec, with the View State's reader from @ex-grid/core reading
// the widths, the pinning, the sort and the filter, so that a Board's grid and a saved view never mean two things.

import {
    FormatCode, GridViewStateJsonError, GridViewStateJson, readGridViewStateJson, type ColumnType, type GridViewState,
} from '@ex-grid/core';
import {
    BoardRefusedError, isJsonArray, isJsonObject, jsonMember, jsonRawText, shapeColumn, type DataShape, type JsonValue, type SnapshotKind,
} from '@ex-grid/data';
import { members } from './json-members';

/** A grid view's spec read (ADR-0342): the input it shows, the columns in display order, and the View State it
 * opens under. */
export interface GridViewSpecReading {
    /** The Board Input the view shows. */
    readonly data: string;
    /** The columns shown, in display order. */
    readonly columns: readonly ShapedColumn[];
    /** The widths, the pinning, the sort and the filter, in the View State's form (ADR-0207). */
    readonly viewState: GridViewState;
}

/** One column of a grid built from a source's shape (ADR-0342). */
export interface ShapedColumn {
    /** The source's column it shows. */
    readonly column: string;
    /** The column's kind, as the input declares it. */
    readonly kind: SnapshotKind;
    /** The header; the column's name when the spec gives none. */
    readonly header: string;
    /** The Excel format code the spec writes, in the invariant spelling (ADR-0120); undefined where the kind's
     * default applies: an integer `#,##0`, a date `yyyy-mm-dd`, text and a boolean as they are. A decimal or a
     * double column always carries one. */
    readonly format: string | undefined;
}

const specKeys = ['data', 'columns', 'sorts', 'filter'];
const columnKeys = ['column', 'header', 'format', 'width', 'pinned'];

/** The least width a column is drawn at, C#'s ColumnWidthSpec.DefaultMinWidthPx. */
const leastWidth = 40;

/**
 * Reads `spec`, standing at `path` in a Board (`views[0].grid`), taking each input's shape from `inputs`, which
 * answers undefined for an input the Board does not declare. Refused, with a {@link @ex-grid/data#BoardRefusedError} naming the
 * path: a member the spec does not have; an input the Board does not declare; a column the input does not
 * declare, or one shown twice; a decimal or double column with no format, its places being the composer's to
 * choose; a format outside the subset a grid shows, or a date format on a number and the reverse; a format on
 * text or a boolean; and whatever the View State's reader refuses.
 */
export function readGridViewSpec(spec: JsonValue, inputs: (name: string) => DataShape | undefined, path: string): GridViewSpecReading {
    const at = (member: string): string => path === '' ? member : `${path}.${member}`;
    if (!isJsonObject(spec)) throw new BoardRefusedError(path, "A grid's spec is an object.");
    members(spec, specKeys, path, "A grid's spec");
    const data = jsonMember(spec, 'data');
    if (typeof data !== 'string' || data === '') throw new BoardRefusedError(at('data'), "A grid names the input it shows in 'data'.");
    const shape = inputs(data);
    if (shape === undefined) throw new BoardRefusedError(at('data'), `The grid shows input '${data}', which the Board does not declare.`);
    const list = jsonMember(spec, 'columns');
    if (!isJsonArray(list) || list.length === 0) {
        throw new BoardRefusedError(at('columns'), "A grid lists the columns it shows, in order, in 'columns'.");
    }

    const columns: ShapedColumn[] = [];
    const viewColumns: string[] = [];
    list.forEach((column, i) => {
        const where = `${at('columns')}[${i}]`;
        if (!isJsonObject(column)) throw new BoardRefusedError(where, "A grid's column is an object.");
        members(column, columnKeys, where, "A grid's column");
        const name = jsonMember(column, 'column');
        if (typeof name !== 'string' || name === '') {
            throw new BoardRefusedError(`${where}.column`, "A grid's column names the input's column it shows in 'column'.");
        }
        const declared = shapeColumn(shape, name);
        if (declared === undefined) throw new BoardRefusedError(`${where}.column`, `The grid shows column '${name}', which input '${data}' does not declare.`);
        let header = name;
        const headerValue = jsonMember(column, 'header');
        if (headerValue !== undefined) {
            if (typeof headerValue !== 'string' || headerValue === '') throw new BoardRefusedError(`${where}.header`, `Column '${name}': a header is text.`);
            header = headerValue;
        }
        let format: string | undefined;
        const formatValue = jsonMember(column, 'format');
        if (formatValue !== undefined) {
            if (typeof formatValue !== 'string') {
                throw new BoardRefusedError(`${where}.format`, `Column '${name}': a format is an Excel format code, as text.`);
            }
            format = formatValue;
        }
        checkFormat(name, declared.kind, format, `${where}.format`);
        columns.push(Object.freeze({ column: name, kind: declared.kind, header, format }));
        const width = jsonMember(column, 'width');
        const pinned = jsonMember(column, 'pinned');
        viewColumns.push(`{"column":${JSON.stringify(name)},"width":${width === undefined ? '"Auto"' : jsonRawText(width)},`
            + `"pinned":${pinned === undefined ? 'false' : jsonRawText(pinned)}}`);
    });

    // The rest is the View State's, read by its own reader.
    const sorts = jsonMember(spec, 'sorts');
    const filter = jsonMember(spec, 'filter');
    const view = `{"version":${GridViewStateJson.version},"columns":[${viewColumns.join(',')}],"sorts":${sorts === undefined ? '[]' : jsonRawText(sorts)}`
        + `${filter === undefined ? '' : `,"filter":${jsonRawText(filter)}`}}`;
    let viewState: GridViewState;
    try {
        viewState = readGridViewStateJson(view, shape.columns.map(column => column.name));
    } catch (error) {
        if (!(error instanceof GridViewStateJsonError)) throw error;
        throw refusedAt(error, path);
    }
    for (const shown of viewState.columns) {
        if ('px' in shown.width && shown.width.px < leastWidth) {
            throw new BoardRefusedError(at('columns'),
                `Column '${shown.column}': a width of ${shown.width.px} is below the least a column is drawn at (${leastWidth}).`);
        }
    }
    return Object.freeze({ data, columns: Object.freeze(columns), viewState });
}

/** The View State's refusal as a Board's, its JSON path (`$.filter…`) made a path under the grid's, `path`. */
export function refusedAt(error: GridViewStateJsonError, path: string): BoardRefusedError {
    const jsonPath = error.path;
    const where = jsonPath !== undefined && jsonPath.length > 1 && jsonPath.startsWith('$.') ? jsonPath.slice(2) : '';
    return new BoardRefusedError(where === '' ? path : path === '' ? where : `${path}.${where}`, error.message);
}

/** The type the grid's engine compares a column of `kind` as. */
export function columnTypeOf(kind: SnapshotKind): ColumnType {
    switch (kind) {
        case 'text': return 'text';
        case 'boolean': return 'boolean';
        case 'date': return 'date';
        case 'decimal':
        case 'double':
        case 'integer': return 'number';
        default: throw new Error(`Internal: ${String(kind satisfies never)} is not a kind.`);
    }
}

function checkFormat(name: string, kind: SnapshotKind, format: string | undefined, path: string): void {
    if (format === undefined) {
        if (kind === 'decimal' || kind === 'double') {
            throw new BoardRefusedError(path, `Column '${name}' is a ${kind} and needs a format, such as "#,##0.00", `
                + '"0.00%" or "0.00000": its places are chosen for what it holds, never guessed from the type (ADR-0342).');
        }
        return;
    }
    if (kind === 'text' || kind === 'boolean') throw new BoardRefusedError(path, `Column '${name}' is ${kind} and shows as it is; it takes no format.`);
    const reading = FormatCode.tryParse(format);
    if ('reason' in reading) throw new BoardRefusedError(path, `Column '${name}': the format code "${format}" ${reading.reason}.`);
    if (reading.format.isDate !== (kind === 'date')) {
        throw new BoardRefusedError(path, reading.format.isDate
            ? `Column '${name}' is a number, and "${format}" formats dates.`
            : `Column '${name}' is a date, and "${format}" formats numbers.`);
    }
}
