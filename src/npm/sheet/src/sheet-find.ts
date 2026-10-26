// ExSheet's answer to a Find step (ADR-0055) and its Replace plan (ADR-0332 §2), the counterpart of C#'s SheetFind.
// A step searches the Sheet's cells, every row of them, by the text each is shown with, a Formula's Value as shown,
// never its Entry. It follows the core's reference step clause for clause: by rows, across a row in the order of the
// request's columns, from the cell after the Focus (before it, backward), wrapping, inside the scope when there is
// one. It walks only the cells that hold an Entry, since a blank shows no text a search could match, so a Sheet of a
// million rows is searched in the time its occupied cells take. `sheetCellMatches` is the one rule a step and the
// Match Mark judge a cell by (ADR-0332 §4), so the mark never shows a match a step does not reach.

import { findMatches, replacedEntry, type GridFindPattern, type GridFindRequest, type GridFindResult, type SelectionRange } from '@ex-grid/core';
import { CellAddress, Sheet } from '@ex-grid/sheet-engine';
import { shownTextOf } from './sheet-row';

/** One step over `sheet`, or not found. */
export function sheetFindStep(sheet: Sheet, request: GridFindRequest): GridFindResult {
    if (request.text === '' || request.columns.length === 0) return { found: false };
    const positions = positionsOf(request.columns);
    const columnCount = request.columns.length;
    const total = Sheet.rowCount * columnCount;
    const from = request.from;
    // The ordinal of the cell `from` names, in row-major order of the request's columns: the walk starts one step
    // past it and ends on it, as the reference's does.
    const start = from !== undefined && from.row >= 0 && from.row < Sheet.rowCount && from.column >= 0 && from.column < columnCount
        ? (from.row * columnCount) + from.column
        : request.backward ? 0 : total - 1;
    let best = Number.POSITIVE_INFINITY;
    let found: GridFindResult = { found: false };
    for (const address of sheet.entryAddresses) {
        const position = positions.get(address.column);
        if (position === undefined) continue;
        if (request.scope !== undefined && !inScope(request.scope, address.row, position)) continue;
        const ordinal = (address.row * columnCount) + position;
        // How many steps the walk takes to reach this cell: 1 for the cell after `from`, and the most, the whole
        // Sheet, for `from`'s own cell, which is considered last.
        const walked = (((request.backward ? start - ordinal : ordinal - start) % total) + total) % total;
        const distance = walked === 0 ? total : walked;
        if (distance >= best || !sheetCellMatches(sheet, address, request)) continue;
        best = distance;
        found = { found: true, row: address.row, column: columnName(request.columns, position) };
    }
    return found;
}

/** Whether the cell at `address` matches `pattern` as a step finds it: a cell that holds an Entry, whose shown text
 * matches by the core's rule. The Match Mark judges the painted cells by it (ADR-0332 §4), handing the text the row
 * already holds as `shown`. */
export function sheetCellMatches(sheet: Sheet, address: CellAddress, pattern: GridFindPattern, shown?: string): boolean {
    return sheet.getEntry(address) !== undefined && findMatches(shown ?? shownTextOf(sheet, address), pattern);
}

/** One cell a Replace All writes: its address and the text it is entered with. */
export interface SheetReplaceWrite {
    readonly address: CellAddress;
    readonly text: string;
}

/** One cell a Replace All cannot write, and why; `position` is its column's place among the request's columns. */
export interface SheetReplaceRefusal {
    readonly address: CellAddress;
    readonly position: number;
    readonly reason: string;
}

/** What a Replace All writes, or the cells that refuse it all. */
export interface SheetReplacePlan {
    readonly writes: readonly SheetReplaceWrite[];
    readonly refused: readonly SheetReplaceRefusal[];
}

/**
 * What a Replace All writes over `sheet` (ADR-0332 §2): every cell whose Entry, the text the Cell Editor opens with,
 * matches `pattern`, with the match replaced, by rows; inside `scope` and the columns `positions` names. A replaced
 * text that is a Formula the engine cannot read is refused with the reason `judge` gives, as a typed one is: then
 * nothing may be written, and every such cell is named.
 */
export function planSheetReplace(
    sheet: Sheet, pattern: GridFindPattern, replacement: string, positions: ReadonlyMap<number, number>,
    scope: readonly SelectionRange[] | undefined, judge: (address: CellAddress, text: string) => string | undefined,
): SheetReplacePlan {
    const writes: SheetReplaceWrite[] = [];
    const refused: SheetReplaceRefusal[] = [];
    if (pattern.text === '') return { writes, refused };
    // In row-major order, as the engine gives them.
    for (const address of sheet.entryAddresses) {
        const position = positions.get(address.column);
        if (position === undefined) continue;
        if (scope !== undefined && !inScope(scope, address.row, position)) continue;
        const text = replacedEntry(sheet.getEntryText(address), pattern, replacement);
        if (text === undefined) continue;
        const reason = judge(address, text);
        if (reason === undefined) writes.push({ address, text });
        else refused.push({ address, position, reason });
    }
    return { writes, refused };
}

/** Where each Sheet column stands among `columns`; a column they do not name is not searched. */
export function positionsOf(columns: readonly string[]): ReadonlyMap<number, number> {
    const positions = new Map<number, number>();
    columns.forEach((name, i) => {
        const column = CellAddress.tryParseColumn(name);
        if (column !== undefined) positions.set(column, i);
    });
    return positions;
}

function columnName(columns: readonly string[], position: number): string {
    const name = columns[position];
    if (name === undefined) throw new Error(`Internal: no column stands at ${String(position)} of the request.`);
    return name;
}

function inScope(scope: readonly SelectionRange[], row: number, column: number): boolean {
    return scope.some(range => row >= range.topRow && row < range.topRow + range.rowCount
        && column >= range.leftColumn && column < range.leftColumn + range.columnCount);
}
