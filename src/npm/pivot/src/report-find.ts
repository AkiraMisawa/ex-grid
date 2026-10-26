// Find over the report as shown (ADR-0332 §1), the Razor component's FindInReportAsync: ExPivot answers each step
// of its report grid's Find as that grid's Consumer (ADR-0055). What is searched is every cell the report lays
// out, the row labels, values and totals, by its displayed text, the text the user reads. The column Items and the
// value captions stand in the grid's headers and Header Groups, which the grid searches itself (ADR-0333). A
// collapsed group is not searched inside, because its rows are not laid out, and nothing is expanded to show a
// match: a search never changes the report's shape. The cells outside the Window are read from the report source
// by the Report Version on screen, as Copy reads them, in blocks, so a step that finds early stops reading.

import { findMatches, type GridColumn, type GridFindPattern, type GridFindRequest, type GridFindResult } from '@ex-grid/core';
import type { PivotDisplayRow, PivotReportCopyBlock, PivotReportMetadata, PivotReportRefusal, PivotReportSource } from '@ex-grid/pivot-engine';
import { elementAt } from './must';

// How many cells one read of the report asks for: a step that finds early stops reading, and a remote report is
// asked in blocks rather than for the whole report at once.
const findBlockCells = 20_000;

/** What one step over one Report Version came to: an answer, or why it cannot be given. */
export type ReportFindRead =
    | { readonly kind: 'answered'; readonly answer: GridFindResult }
    /** The source no longer holds the Report Version the step reads. */
    | { readonly kind: 'notHeld' }
    /** The source refused to be read. */
    | { readonly kind: 'refused'; readonly refusal: PivotReportRefusal }
    /** The source answered another version or range than it was asked. */
    | { readonly kind: 'invalid' };

const notFound: ReportFindRead = Object.freeze({ kind: 'answered', answer: Object.freeze({ found: false }) });
const notHeld: ReportFindRead = Object.freeze({ kind: 'notHeld' });
const invalid: ReportFindRead = Object.freeze({ kind: 'invalid' });

/**
 * One Find step over the report `metadata` describes, in ADR-0055's order: row by row, across a row in the
 * request's column order, from the cell after the Focus, wrapping, the Focus's own cell last; within the selection
 * when more than one cell is selected. A column of the request the report does not lay out is skipped.
 */
export async function findInReport(
    source: PivotReportSource, metadata: PivotReportMetadata, request: GridFindRequest, signal: AbortSignal,
): Promise<ReportFindRead> {
    const rows = metadata.rowCount;
    const known = new Set([...metadata.labelColumns.map(column => column.name), ...metadata.valueColumns.map(column => column.name)]);
    // The request's columns the report lays out, by their position in the request.
    const read = request.columns.flatMap((name, position) => (known.has(name) ? [{ name, position }] : []));
    const columns = request.columns.length;
    if (request.text === '' || rows === 0 || read.length === 0) return notFound;

    // Where the walk starts: past the Focus, which is looked at last; without one, from the first cell (the last,
    // backward), as the reference step walks.
    const { backward, from, scope } = request;
    const [startRow, startColumn] = from !== undefined && from.row >= 0 && from.row < rows && from.column >= 0 && from.column < columns
        ? [from.row, from.column]
        : backward ? [0, 0] : [rows - 1, columns - 1];
    const step = backward ? -1 : 1;
    const names = Object.freeze(read.map(column => column.name));
    const blockRows = Math.max(1, Math.floor(findBlockCells / names.length));

    let block: PivotReportCopyBlock | undefined;
    let blockTop = 0;
    for (let visit = 0; visit <= rows; visit++) {
        signal.throwIfAborted();
        const row = (((startRow + step * visit) % rows) + rows) % rows;
        if (scope !== undefined && !scope.some(range => row >= range.topRow && row < range.topRow + range.rowCount)) continue;
        if (block === undefined || row < blockTop || row >= blockTop + block.rows.length) {
            const top = backward ? Math.max(0, row - blockRows + 1) : row;
            const bottom = backward ? row : Math.min(rows - 1, row + blockRows - 1);
            const answer = await source.copy(Object.freeze({
                version: metadata.version,
                columns: names,
                ranges: Object.freeze([Object.freeze({ top, left: 0, bottom, right: names.length - 1 })]),
                maxCells: (bottom - top + 1) * names.length,
            }), signal);
            signal.throwIfAborted();
            if (answer.refusal?.kind === 'reportVersionNotHeld') return notHeld;
            if (answer.refusal !== undefined) return Object.freeze({ kind: 'refused', refusal: answer.refusal });
            const got = answer.blocks[0];
            if (answer.version !== metadata.version || answer.blocks.length !== 1 || got === undefined || got.rows.length !== bottom - top + 1
                || got.rows.some(cells => cells.length !== names.length)) {
                return invalid;
            }
            block = got;
            blockTop = top;
        }
        const cells = elementAt(block.rows, row - blockTop);
        for (let k = 0; k < read.length; k++) {
            const index = backward ? read.length - 1 - k : k;
            const { name, position } = elementAt(read, index);
            // The Focus's row is visited twice: first past the Focus, last up to it and on it.
            if (visit === 0 && (backward ? position >= startColumn : position <= startColumn)) continue;
            if (visit === rows && (backward ? position < startColumn : position > startColumn)) continue;
            if (scope !== undefined && !scope.some(range => row >= range.topRow && row < range.topRow + range.rowCount
                && position >= range.leftColumn && position < range.leftColumn + range.columnCount)) continue;
            if (findMatches(elementAt(cells, index).text, request)) {
                return Object.freeze({ kind: 'answered', answer: Object.freeze({ found: true, row, column: name }) });
            }
        }
    }
    return notFound;
}

// Each report column's displayed text, as the report grid's cells show it and its Copy reads it.
const cellTexts = new WeakMap<GridColumn<PivotDisplayRow>, (row: PivotDisplayRow) => string>();

/** Declares `column`'s displayed text, which the Match Mark judges it by. */
export function withCellText(column: GridColumn<PivotDisplayRow>, text: (row: PivotDisplayRow) => string): GridColumn<PivotDisplayRow> {
    cellTexts.set(column, text);
    return column;
}

/**
 * The Match Mark's rule over the report (ADR-0332 §4): the reference rule over a cell's displayed text, the one
 * `findInReport` finds by, so the mark and the step agree. One function for every pivot: its identity reaches
 * the grid (ADR-0003).
 */
export function reportMatch(row: PivotDisplayRow, column: GridColumn<PivotDisplayRow>, pattern: GridFindPattern): boolean {
    const text = cellTexts.get(column);
    if (text === undefined) throw new Error(`Internal: the report column '${column.name}' declares no displayed text.`);
    return findMatches(text(row), pattern);
}
