// ExSheet's answer to a copy (ADR-0050 item 9, ADR-0048, ADR-0049), the counterpart of C#'s SheetClipboard.Answer: the
// engine's copy of the range, written exactly as the engine gives it, or the engine's refusal in the Sheet's words;
// and the copy it last wrote, so that a paste of exactly that copy back into the Sheet carries its Entries.

import {
    assembleCopy, invariantMarker, markAttribute, parseClipboard, type CopyPlan, type GridCopyAnswer, type GridCopyRequest,
    type PasteFieldOrigin, type SelectionRange,
} from '@ex-grid/core';
import { CellAddress, CellRange, type Sheet, type SheetBlock, type SheetRefusal } from '@ex-grid/sheet-engine';
import { defined } from './must';
import { refusedMarkFor } from './sheet-clipboard';

/**
 * The copy ExSheet last put on the clipboard: the Entries it copied, and what was written. A later paste read from a
 * table marked as the Sheet marks its own, every field invariant, whose fields equal the ones the grid's paste parse
 * reads back from what was written, field for field, is taken to be this copy, and its Entries are pasted. A browser
 * clipboard names no owner, so equal content in the Sheet's own markup is the strongest evidence a page has; text
 * from another program reads as shown text, and is never taken for it.
 */
export class SheetOwnCopy {
    readonly block: SheetBlock;
    readonly #text: string;
    readonly #html: string;
    // Read back on the first paste that could be this copy, not at the copy, which may never be pasted.
    #fields: readonly (readonly string[])[] | undefined;

    constructor(block: SheetBlock, text: string, html: string) {
        this.block = block;
        this.#text = text;
        this.#html = html;
    }

    /** Whether a paste of `pasted`, read as `origins` say, is this copy, field for field. */
    isPastedAs(pasted: readonly (readonly string[])[], origins: readonly (readonly PasteFieldOrigin[])[]): boolean {
        if (!origins.every(row => row.every(origin => origin === 'invariant'))) return false;
        this.#fields ??= parseClipboard(this.#html, this.#text);
        const fields = this.#fields;
        if (fields === undefined) throw new Error("Internal: the engine's copy of a range read back as nothing.");
        return pasted.length === fields.length
            && pasted.every((row, r) => row.length === fields[r]?.length && row.every((field, c) => field === fields[r]?.[c]));
    }
}

/**
 * The answer to `request`, and the copy to recognise on a later paste, or `undefined` where this copy carries no
 * Entries.
 * - A copy reaching a `#GETTING_DATA` cell is refused with the engine's sentence (ADR-0049).
 * - One range is the engine's copy: the Values as shown for `text/plain`, the unformatted Values for `text/html` in
 *   Excel's markup, its table marked as the grid marks its own so that a paste reads its fields as invariant (ADR-0050
 *   item 10). Its Entries are kept for a paste back into this Sheet.
 * - Several ranges combined into one block, or a copy with the headers, carry the same Values cell by cell, and no
 *   Entries: a block of several ranges was never one place its References were relative to.
 * - Either way, a refused cell is written as it shows, and its cell of the table carries ExSheet's mark, which another
 *   program ignores: a paste into a Sheet that reads it is refused by name, never typed as an Error Value a Formula
 *   could catch (ADR-0047, 2026-10-12).
 */
export function sheetCopyAnswer(sheet: Sheet, request: GridCopyRequest, refused: (refusal: SheetRefusal) => string): {
    readonly answer: GridCopyAnswer; readonly own: SheetOwnCopy | undefined;
} {
    const { plan } = request;
    const copies = [];
    for (const segment of plan.segments) {
        const copy = sheet.copy(rangeOf(segment));
        if (copy.refusal !== undefined) return { answer: { kind: 'refuse', sentence: refused(copy.refusal) }, own: undefined };
        copies.push(copy);
    }
    const [only] = copies;
    const [segment] = plan.segments;
    if (copies.length === 1 && only !== undefined && segment !== undefined && !request.withHeaders) {
        const range = rangeOf(segment);
        const width = range.columnCount;
        const text = defined(only.text, 'the copy\'s text');
        // The engine writes its table's cells row by row over the range.
        const html = markRefused(sheet, markedInvariant(defined(only.html, 'the copy\'s table')), range.rowCount * width,
            i => new CellAddress(range.first.row + Math.floor(i / width), range.first.column + (i % width)));
        return { answer: { kind: 'write', text, html }, own: new SheetOwnCopy(defined(only.block, 'the copy\'s Entries'), text, html) };
    }
    return { answer: assembled(sheet, plan, request.withHeaders), own: undefined };
}

// The Values of several ranges, cell by cell, in the grid's own assembly; the cells in the order the table writes
// them, header cells apart, so that the refused ones are marked.
function assembled(sheet: Sheet, plan: CopyPlan, withHeaders: boolean): GridCopyAnswer {
    const written: CellAddress[] = [];
    const { text, html } = assembleCopy(plan, {
        displayAt: (row, column) => copiedText(sheet, new CellAddress(row, column)),
        rawAt: (row, column) => {
            const address = new CellAddress(row, column);
            written.push(address);
            const value = sheet.getValue(address);
            return value === undefined ? '' : value.toString();
        },
        ...(withHeaders ? { headerAt: (column: number) => CellAddress.columnName(column) } : {}),
    });
    return { kind: 'write', text, html: markRefused(sheet, html, written.length, i => defined(written[i], 'a written cell')) };
}

// A cell's text as the engine's copy writes it: as the cell shows it at no particular width, and a Value its format
// cannot show as the Value itself, never `####` (ADR-0016).
function copiedText(sheet: Sheet, address: CellAddress): string {
    const display = sheet.getDisplay(address);
    if (!display.cannotShow) return display.text;
    const value = sheet.getValue(address);
    return value === undefined ? '' : value.toString();
}

// `html` with ExSheet's mark on the `<td>` of every refused cell: `cellAt` names the table's `count` cells in the
// order it writes them. Every cell of the table is a `<td` and every other `<` in it is escaped, so the n-th `<td` is
// the n-th cell; a table that holds another count is a defect said by name, never a mark on the wrong cell.
function markRefused(sheet: Sheet, html: string, count: number, cellAt: (index: number) => CellAddress): string {
    const parts: string[] = [];
    // `from` is where the text not yet taken starts; the search goes on past each cell's `<td`.
    let from = 0;
    let cell = 0;
    for (let at = html.indexOf('<td'); at >= 0; at = html.indexOf('<td', at + '<td'.length)) {
        if (cell >= count) throw new Error('The Sheet\'s copy holds more cells than it was written from, so ExSheet cannot mark its refused cells.');
        const open = at + '<td'.length;
        const address = cellAt(cell);
        if (sheet.getValue(address)?.isRefused === true) {
            parts.push(html.slice(from, open), ` ${markAttribute}="${refusedMarkFor(address)}"`);
            from = open;
        }
        cell++;
    }
    if (cell !== count) throw new Error('The Sheet\'s copy holds fewer cells than it was written from, so ExSheet cannot mark its refused cells.');
    parts.push(html.slice(from));
    return parts.join('');
}

const engineTableOpen = '<table ';

// The engine's table, marked as the grid marks its own unformatted HTML, so the grid's paste parse reads every field
// of it as invariant.
function markedInvariant(html: string): string {
    if (!html.startsWith(engineTableOpen)) {
        throw new Error("The engine's copy no longer opens with <table and its attributes, so ExSheet cannot mark it as invariant; a paste would read its numbers as shown text.");
    }
    return `${engineTableOpen}${invariantMarker} ${html.slice(engineTableOpen.length)}`;
}

/** The engine's range for a range of the grid's. */
export function rangeOf(range: SelectionRange): CellRange {
    return new CellRange(
        new CellAddress(range.topRow, range.leftColumn),
        new CellAddress(range.topRow + range.rowCount - 1, range.leftColumn + range.columnCount - 1));
}
