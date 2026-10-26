// ExSheet's answer to a Selection Summary (ADR-0130), the counterpart of C#'s SheetSummary: the Values of the
// selected cells — a Formula's result, never its Entry, and every cell an array spills into. A number is in every
// figure; text and a Boolean are counted and are never a number; an Error Value is counted, is never a number,
// and leaves only Count and Numerical Count, as Excel's status bar does; a blank cell is in none. A date is a
// number here, as it is in the Sheet and in Excel: a Sheet date is a serial number shown with a date format
// (ADR-0047). It walks only the cells that hold a Value, so a whole column of a million rows is summarised in the
// time its occupied cells take.
//
// A refused cell among them (`Value.isRefused`, a Refused Formula's `#VALUE!` or `#NAME?`) declines the whole
// summary, its reason in ExSheet's words in place of the figures (2026-10-13, decided with the user: no numeric
// figure stands over a refused cell, and the line says why). A partial summary is never shown (ADR-0130), so
// Count goes with the rest.
//
// The figures are folded by the family's one definition of them, C#'s GridSummaryCells. Its TypeScript
// counterpart, and the accumulator under it, are not part of @ex-grid/core's public surface yet, so this module
// reads the cells and declines; folding `SheetSummaryCells` into figures waits on that surface.

import type { GridSummaryRequest, GridSummaryResult } from '@ex-grid/core';
import { CellAddress, type Sheet } from '@ex-grid/sheet-engine';
import type { SheetWords } from './sheet-words';

/**
 * The selected cells of a Sheet as a Selection Summary reads them: the first refused cell in reading order and
 * what it shows, or the cells sorted by what they are, with the Focus cell whose Number Format the figures are
 * shown in (Excel's status bar shows them in the active cell's, read in the Windows run of 2026-10-11).
 */
export type SheetSummaryCells =
    | { readonly kind: 'refused'; readonly at: CellAddress; readonly shown: string }
    | {
        readonly kind: 'cells';
        /** Each number, a date's serial number among them, in Excel's double. */
        readonly numbers: readonly number[];
        /** How many cells hold an Error Value. */
        readonly errors: number;
        /** How many cells hold text or a Boolean. */
        readonly others: number;
        /** The Focus cell on the Sheet, or `undefined` where the request has none. */
        readonly focus: CellAddress | undefined;
    };

/** The cells a request names over a Sheet, as a Selection Summary reads them (ADR-0130). */
export function sheetSummaryCells(sheet: Sheet, request: GridSummaryRequest): SheetSummaryCells {
    // Where each sheet column stands in the request's order; a column it does not name is not summarised.
    const positions = new Map<number, number>();
    request.columns.forEach((name, position) => {
        const column = CellAddress.tryParseColumn(name);
        if (column !== undefined) positions.set(column, position);
    });
    const numbers: number[] = [];
    let errors = 0;
    let others = 0;
    let firstRefused: CellAddress | undefined;
    for (const address of sheet.valueAddresses) {
        const position = positions.get(address.column);
        if (position === undefined || !inRanges(request, address.row, position)) continue;
        const value = sheet.getValue(address);
        if (value === undefined) continue;
        if (value.isRefused) {
            if (firstRefused === undefined || address.compareTo(firstRefused) < 0) firstRefused = address;
            continue;
        }
        switch (value.kind) {
            case 'number': numbers.push(value.number); break;
            case 'error': errors++; break;
            case 'text':
            case 'boolean':
                others++;
                break;
            default: throw new Error(`Internal: a Value of kind ${String(value.kind)}.`);
        }
    }
    if (firstRefused !== undefined) {
        const shown = sheet.getValue(firstRefused);
        if (shown === undefined) throw new Error('Internal: a refused cell holds no Value.');
        return Object.freeze({ kind: 'refused', at: firstRefused, shown: shown.toString() });
    }
    const focus = request.focus;
    const focusName = focus === undefined ? undefined : request.columns[focus.column];
    const focusColumn = focusName === undefined ? undefined : CellAddress.tryParseColumn(focusName);
    return Object.freeze({
        kind: 'cells',
        numbers: Object.freeze(numbers),
        errors,
        others,
        focus: focus === undefined || focusColumn === undefined ? undefined : new CellAddress(focus.row, focusColumn),
    });
}

/** The Selection Summary declined over a refused cell, with the reason in the Sheet's words (2026-10-13,
 * decided with the user). */
export function sheetSummaryDeclined(refused: { readonly at: CellAddress; readonly shown: string }, words: SheetWords): GridSummaryResult {
    return Object.freeze({ kind: 'declined', reason: words.summaryRefused(refused.at, refused.shown) });
}

// A cell covered by two ranges is one address, visited once.
function inRanges(request: GridSummaryRequest, row: number, column: number): boolean {
    return request.ranges.some(range => row >= range.topRow && row < range.topRow + range.rowCount
        && column >= range.leftColumn && column < range.leftColumn + range.columnCount);
}
