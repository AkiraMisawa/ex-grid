// What the Sheet tells its grid about the Formula being edited (ADR-0057), as the C# Sheet's SheetFormulaAids: the
// References in the editor's text, and the Linked Table columns behind the keys the grid coloured.

import { EditorCompletion, EditorHint, type GridEditorReference, type GridEditorRewrite, type ReferenceKeyColour, type SelectionRange } from '@ex-grid/core';
import {
    CellAddress, CellRange, cycleReference, formulaHintAt, formulaPointAt, LinkedTableColumn, referenceText, type ArgumentHint, type Sheet,
} from '@ex-grid/sheet-engine';
import { defined } from './must';
import type { LinkedColumnColour } from './pointing-scope';

/** Whether a Reference can go at the caret (ADR-0051): after `=`, an operator, `(` or `,` in a Formula. */
export function sheetPointAt(text: string, caret: number): boolean {
    return caret >= 0 && caret <= text.length && formulaPointAt(text, caret) !== undefined;
}

/**
 * Completion and the argument hint (ADR-0051, ADR-0058), as the C# Sheet's `SheetFormulaAids.Complete`: the declared
 * functions and `sheet`'s Linked Tables whose names begin with the name at the caret, a table's columns after `Table[`,
 * an argument's values where it takes one of a fixed list, and the hint for the function whose argument list holds the
 * caret. Nothing for text that is not a Formula.
 */
export function sheetCompletion(sheet: Sheet, text: string, caret: number): EditorCompletion | undefined {
    if (!text.startsWith('=') || caret < 0 || caret > text.length) return undefined;
    const completion = sheet.complete(text, caret);
    const hint = hintOf(formulaHintAt(text, caret));
    if (completion === undefined && hint === undefined) return undefined;
    const candidates = completion === undefined ? [] : completion.candidates.map(candidate => ({
        label: candidate.name, start: completion.start, length: completion.length, insert: candidate.insertText,
    }));
    return new EditorCompletion(hint === undefined ? { candidates } : { candidates, hint });
}

/**
 * The hint line for an argument hint: the function's signature, `SUM(number1, [number2], ...)`, with the name of the
 * argument the caret is in set off, none past the last argument the function takes.
 */
export function hintOf(hint: ArgumentHint | undefined): EditorHint | undefined {
    if (hint === undefined) return undefined;
    const { function: declared, currentArgument } = hint;
    const text = declared.signature;
    if (currentArgument === undefined) return new EditorHint({ text });
    // The signature is the name, "(", the arguments joined by ", ", and ")": the argument's place in it is counted
    // over the names before it, never searched for as text, so a name that is a prefix of another is set off where it
    // stands.
    let at = declared.name.length + 1;
    for (const name of declared.arguments.split(', ')) {
        if (name === currentArgument) return new EditorHint({ text, emphasisStart: at, emphasisLength: name.length });
        at += name.length + 2;
    }
    return new EditorHint({ text });
}

/** The Reference Point mode writes for a pointed range (ADR-0051): `A3`, or `B7:C9` from its top-left. */
export function sheetReferenceText(range: SelectionRange): string {
    return referenceText(new CellRange(new CellAddress(range.topRow, range.leftColumn),
        new CellAddress(range.topRow + range.rowCount - 1, range.leftColumn + range.columnCount - 1)));
}

/**
 * F4 (ADR-0051, 2026-09-29): the Reference at the caret, the end of a range the caret touches, or every one the
 * selection covers, overlaps or touches, cycled to its next form by the engine, with the selection over what was
 * rewritten. Nothing where nothing changes: no Reference there, or text that is not a Formula.
 */
export function sheetCycleReference(text: string, selectionStart: number, selectionEnd: number): GridEditorRewrite | undefined {
    return cycleReference(text, selectionStart, selectionEnd);
}

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
 * names, folded as the engine finds tables and columns, ignoring case. */
export function linkedColumnKey(column: LinkedTableColumn): string {
    return column.key;
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
