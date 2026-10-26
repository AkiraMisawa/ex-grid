// Where Ctrl+arrow stops on a Sheet (ADR-0050, item 2), answered as Excel answers it from the Sheet's blanks,
// the counterpart of C#'s SheetEdges. A cell is filled when it holds an Entry — a Formula whose Value is empty
// text is filled, as in Excel — and blank otherwise; formatting alone does not fill a cell.
//
// - From a filled cell whose neighbour in that direction is filled: to the last filled cell of that block.
// - Otherwise — from a blank cell, or from the last cell of a block: to the next filled cell along the line, or
//   to the Sheet's edge when there is none.
// - At the Sheet's edge: nowhere.
//
// The filled cells are indexed per column and per row, each line's positions kept sorted, and kept in step with
// every change the Sheet reports, so an answer never walks the blanks between two blocks.

import type { GridDirection } from '@ex-grid/core';
import { CellAddress, columnCount, rowCount, type Sheet, type SheetChange } from '@ex-grid/sheet-engine';
import { elementAt, unreachable } from './must';

/** The first index of a sorted list whose value is at least `value`; the list's length when none is. */
function lowerBound(sorted: readonly number[], value: number): number {
    let low = 0;
    let high = sorted.length;
    while (low < high) {
        const middle = (low + high) >>> 1;
        if (elementAt(sorted, middle) < value) low = middle + 1;
        else high = middle;
    }
    return low;
}

function holds(sorted: readonly number[], value: number): boolean {
    const at = lowerBound(sorted, value);
    return at < sorted.length && sorted[at] === value;
}

/** The index of a Sheet's filled cells, which answers where Ctrl+arrow stops (ADR-0050, item 2). */
export class SheetEdges {
    readonly #rowsByColumn = new Map<number, number[]>();
    readonly #columnsByRow = new Map<number, number[]>();

    /** The index of every filled cell of the Sheet. */
    static of(sheet: Sheet): SheetEdges {
        const edges = new SheetEdges();
        for (const address of sheet.entryAddresses) edges.#set(address, true);
        return edges;
    }

    /**
     * Brings the index up to date after a change the Sheet reported. Every cell that became filled or blank is
     * among the change's Value changes: a blank cell has no Value and a filled one has one.
     */
    update(sheet: Sheet, change: SheetChange): void {
        for (const address of change.valueChanges) this.#set(address, sheet.getEntry(address) !== undefined);
    }

    /** The cell Ctrl+arrow reaches from `from` in the direction. */
    find(from: CellAddress, direction: GridDirection): CellAddress {
        switch (direction) {
            case 'up': return new CellAddress(along(this.#rowsByColumn.get(from.column), from.row, -1, rowCount - 1), from.column);
            case 'down': return new CellAddress(along(this.#rowsByColumn.get(from.column), from.row, 1, rowCount - 1), from.column);
            case 'left': return new CellAddress(from.row, along(this.#columnsByRow.get(from.row), from.column, -1, columnCount - 1));
            case 'right': return new CellAddress(from.row, along(this.#columnsByRow.get(from.row), from.column, 1, columnCount - 1));
            default: return unreachable(direction);
        }
    }

    #set(address: CellAddress, filled: boolean): void {
        mark(this.#rowsByColumn, address.column, address.row, filled);
        mark(this.#columnsByRow, address.row, address.column, filled);
    }
}

function along(filled: readonly number[] | undefined, at: number, step: -1 | 1, last: number): number {
    let next = at + step;
    if (next < 0 || next > last) return at;
    if (filled === undefined || filled.length === 0) return step > 0 ? last : 0;
    if (holds(filled, at) && holds(filled, next)) {
        while (next + step >= 0 && next + step <= last && holds(filled, next + step)) next += step;
        return next;
    }
    // The first filled cell past this one.
    if (step > 0) {
        const beyond = lowerBound(filled, next);
        return beyond < filled.length ? elementAt(filled, beyond) : last;
    }
    const before = lowerBound(filled, next + 1) - 1;
    return before >= 0 ? elementAt(filled, before) : 0;
}

function mark(index: Map<number, number[]>, line: number, position: number, filled: boolean): void {
    const positions = index.get(line);
    if (filled) {
        if (positions === undefined) {
            index.set(line, [position]);
            return;
        }
        const at = lowerBound(positions, position);
        if (positions[at] !== position) positions.splice(at, 0, position);
        return;
    }
    if (positions === undefined) return;
    const at = lowerBound(positions, position);
    if (positions[at] !== position) return;
    positions.splice(at, 1);
    if (positions.length === 0) index.delete(line);
}
