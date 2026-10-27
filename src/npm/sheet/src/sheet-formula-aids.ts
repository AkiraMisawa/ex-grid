// What the Sheet tells its grid about the Formula being edited (ADR-0057), as the C# Sheet's SheetFormulaAids: the
// References in the editor's text, and the Linked Table columns behind the keys the grid coloured.

import type { GridEditorReference, ReferenceKeyColour } from '@ex-grid/core';
import { LinkedTableColumn, type Sheet } from '@ex-grid/sheet-engine';
import { defined } from './must';
import type { LinkedColumnColour } from './pointing-scope';

/**
 * The References function (ADR-0057): each Reference in the text being edited, with its span and the cells it names
 * on `sheet`, for the grid to colour and outline. A structured reference names no cells here, so it carries the key
 * of its Linked Table's column instead. Nothing for text that is not a Formula.
 */
export function sheetReferences(sheet: Sheet, text: string): readonly GridEditorReference[] {
    return sheet.references(text).map(({ start, length, cells, linkedColumn }) => (cells === undefined
        ? { start, length, key: linkedColumnKey(defined(linkedColumn, 'the Linked Table column a structured reference reads')) }
        : { start, length, range: { topRow: cells.first.row, leftColumn: cells.first.column, rowCount: cells.rowCount, columnCount: cells.columnCount } }));
}

/** The key a Linked Table's column is coloured under (ADR-0057): one per column, however the Formula cased its
 * names, since the Sheet finds tables and columns ignoring case. */
export function linkedColumnKey(column: LinkedTableColumn): string {
    return `${column.table}[${column.column}]`.toUpperCase();
}

/**
 * The Linked Table columns behind the keys the grid told colours for (ADR-0057), in the grid's order, each under the
 * names the Consumer declared: the key is the Formula's casing, upper-cased, and the Consumer knows its table by the
 * names it gave. A key no declared column carries names nothing, and is left out.
 */
export function linkedColumnsOf(sheet: Sheet, keys: readonly ReferenceKeyColour[]): readonly LinkedColumnColour[] {
    if (keys.length === 0) return [];
    const declared = new Map<string, LinkedTableColumn>();
    for (const table of sheet.workbook.linkedTables) {
        for (const column of table.columns) {
            const named = new LinkedTableColumn(table.name, column);
            declared.set(linkedColumnKey(named), named);
        }
    }
    return keys.flatMap(({ key, colour }) => {
        const column = declared.get(key);
        return column === undefined ? [] : [Object.freeze({ column, colour })];
    });
}

/** Whether two tellings hold the same columns in the same colours at the same places, the names compared as
 * declared. */
export function sameLinkedColumns(a: readonly LinkedColumnColour[], b: readonly LinkedColumnColour[]): boolean {
    return a.length === b.length && a.every((told, i) => {
        const other = b[i];
        return other !== undefined && other.colour === told.colour && other.column.table === told.column.table && other.column.column === told.column.column;
    });
}
