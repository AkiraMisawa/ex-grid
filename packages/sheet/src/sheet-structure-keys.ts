// Excel's insert and delete keys, as ExSheet declares them to its grid (ADR-0050 item 14's note of 2026-10-02;
// ADR-0071, Part C's case 12), the counterpart of C#'s SheetStructureKeys: Ctrl with `+`, typed with Shift
// (Ctrl+Shift+= on a US or UK layout) or without it (the numeric keypad's `+`, or a layout with a key of its
// own), and Ctrl with `-`. They are the browser's zoom keys, so declaring them is what keeps a Sheet from zooming
// the page. Like the formatting keys they are read by the character typed.

import type { GridExtent, GridSelection } from '@ex-grid/core';
import { SheetEdit } from '@ex-grid/sheet-engine';

/** What an insert or delete key does. */
export type SheetStructureKey = 'insert' | 'delete';

const byKey: ReadonlyMap<string, SheetStructureKey> = new Map([
    ['Control++', 'insert'],
    ['Control+Shift++', 'insert'],
    ['Control+-', 'delete'],
]);

/** The keys ExSheet declares to its grid for inserting and deleting, in the grid's canonical form. */
export function sheetStructureKeysDeclared(): readonly string[] {
    return Object.freeze([...byKey.keys()]);
}

/** What a declared key does; `undefined` for a key that is not one of these. */
export function sheetStructureKeyOf(key: string): SheetStructureKey | undefined {
    return byKey.get(key);
}

/**
 * What the key does to the Selection on a Sheet of the extent: the rows a Selection of whole rows spans, or the
 * columns a Selection of whole columns spans, inserted above or to the left, or deleted, as the Context Menu's
 * commands do. `undefined` for every other Selection — several ranges, every cell at once, or a range that is
 * not whole rows or whole columns — which the key refuses: Excel opens a dialog there that shifts cells, and
 * ExSheet shifts no cells (ADR-0050 item 14).
 */
export function sheetStructureEditFor(kind: SheetStructureKey, selection: GridSelection, extent: GridExtent): SheetEdit | undefined {
    if (selection.ranges.length !== 1) return undefined;
    const [range] = selection.ranges;
    if (range === undefined) return undefined;
    const wholeRows = range.leftColumn === 0 && range.columnCount === extent.columnCount;
    const wholeColumns = range.topRow === 0 && range.rowCount === extent.rowCount;
    if (wholeRows === wholeColumns) return undefined;
    if (wholeRows) return kind === 'insert' ? SheetEdit.insertRows(range.topRow, range.rowCount) : SheetEdit.deleteRows(range.topRow, range.rowCount);
    return kind === 'insert'
        ? SheetEdit.insertColumns(range.leftColumn, range.columnCount)
        : SheetEdit.deleteColumns(range.leftColumn, range.columnCount);
}
