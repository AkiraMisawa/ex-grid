// The reference Replace (ADR-0332 §2 and §3), the counterpart of C#'s GridReplace: what one cell's
// replacement is, and what a Replace All over the rows writes or refuses. `GridSource.from` answers with
// it and a remote source is held to it, as to Find.
//
// What is matched is the cell's entry, the text the Cell Editor would open with, by Find's rule. A match
// is replaced as Excel's Replace does: every occurrence of the text in the entry, compared the same way;
// with Whole cell, the entry whole. Replace All is all or nothing, and uncapped: every match in the scope
// is planned first; if any cannot be written, nothing is written and every such cell is named.

import { itemAt } from '../must';
import { foldCase } from '../query/text-casing';
import type { SelectionRange } from '../selection/range';
import { inScope, matcherOf, patternOf, type GridFindPattern } from './find';

/**
 * A Replace All (ADR-0332 §3): the find request's fields and the replacement, raised once over every
 * match, under the Row Sequence Version it was read in. The grid holds no rows (ADR-0001), so only the
 * Consumer can reach every match: it performs the whole write, all of it or none of it.
 */
export interface GridReplaceRequest extends GridFindPattern {
    /** What each match is replaced with. May be empty: the matched text is removed. */
    readonly replacement: string;
    /** The selected ranges, when more than one cell is selected, as Find's. Absent: every row. */
    readonly scope?: readonly SelectionRange[] | undefined;
    /** The visible columns' names in the grid's current order, as in a Find request. */
    readonly columns: readonly string[];
    /** The names among `columns` the grid declares Editable (ADR-0035): a write may land only there. A
     * match in any other column refuses the whole Replace All, naming the cell. */
    readonly editableColumns: readonly string[];
    /** The order the scope's positions were read in (ADR-0011). */
    readonly rowSequenceVersion: number;
}

/** One match a Replace could not write, by its place: a row in the order the request was read in and a
 * column name, and why in the Consumer's words, or `undefined` where the column is not Editable. */
export interface GridReplaceRefusedCell {
    /** The row's position in the order the request was read in. */
    readonly row: number;
    /** The column's name. */
    readonly column: string;
    /** Why, in the Consumer's words, or `undefined` where the column is not Editable. */
    readonly message: string | undefined;
}

/**
 * The Consumer's answer to a Replace All (ADR-0332 §3): how many cells it replaced, or a refusal naming
 * every match that could not be written, in which case it replaced nothing. Either is answered under the
 * Row Sequence Version the Consumer read its rows in; under another than the request's, the grid refuses
 * the answer as the order having moved.
 */
export type GridReplaceResult =
    | { readonly kind: 'replaced'; readonly count: number; readonly rowSequenceVersion: number }
    | { readonly kind: 'refused'; readonly cells: readonly GridReplaceRefusedCell[]; readonly rowSequenceVersion: number };

/** Every match was replaced, as one write: `count` cells, 0 where nothing matched. */
export function replacedCells(count: number, rowSequenceVersion: number): GridReplaceResult {
    if (!Number.isInteger(count) || count < 0) throw new RangeError(`A Replace All replaces 0 cells or more, not ${count}.`);
    return Object.freeze({ kind: 'replaced', count, rowSequenceVersion });
}

/** Nothing was replaced, because the cells named could not be written. A refusal that names nothing is
 * the silence ADR-0034 rules out, and is refused. */
export function refusedCells(cells: readonly GridReplaceRefusedCell[], rowSequenceVersion: number): GridReplaceResult {
    if (cells.length === 0) throw new TypeError('A refused Replace All names the cells that could not be written (ADR-0332 §2).');
    return Object.freeze({ kind: 'refused', cells: Object.freeze(cells.map(cell => Object.freeze({ ...cell }))), rowSequenceVersion });
}

/** The Consumer's write of one replaced cell (ADR-0332 §3): a new row holding the new value, since
 * identity is the change signal (ADR-0003), or a refusal in the Consumer's words. */
export type GridReplaceWrite<Row> =
    | { readonly row: Row }
    | { readonly refusal: string };

/** One row a Replace All rewrote: the object it read, and the new one holding every replaced cell. */
export interface GridReplacedRow<Row> {
    /** The row as it stood. */
    readonly before: Row;
    /** The row that replaces it. */
    readonly after: Row;
}

/** The reference Replace All's answer: the result the grid is handed, and the rows it rewrote, empty
 * when refused, which the Consumer keeps as one step of its undo stack. */
export interface GridReplaceAnswer<Row> {
    /** What the grid is told: how many cells were replaced, or which could not be written. */
    readonly result: GridReplaceResult;
    /** The rows the replacement made, each with the row it replaces. */
    readonly changes: readonly GridReplacedRow<Row>[];
}

/**
 * The reference rule for one cell (ADR-0332 §2): `entry` with `pattern` replaced by `replacement`, or
 * `undefined` where the entry does not match. Without Whole cell, every occurrence is replaced, compared
 * as the pattern compares; with it, an entry equal to the text is replaced whole. An empty pattern matches
 * nothing.
 */
export function replacedEntry(entry: string, pattern: GridFindPattern, replacement: string): string | undefined {
    return replacerOf(pattern, replacement)(entry);
}

// The rule for one cell prepared once for a Replace All, so the pattern is folded once rather than once for
// every cell planned.
function replacerOf(pattern: GridFindPattern, replacement: string): (entry: string) => string | undefined {
    if (typeof replacement !== 'string') throw new TypeError('A replacement is text, which may be empty.');
    const matches = matcherOf(pattern);
    const text = pattern.matchCase ? pattern.text : foldCase(pattern.text);
    return entry => {
        if (typeof entry !== 'string') throw new TypeError('A cell is matched by its entry, which is text.');
        if (!matches(entry)) return undefined;
        if (pattern.wholeCell) return replacement;
        if (pattern.matchCase) return entry.split(text).join(replacement);
        // Upper-casing keeps every character's length (each code point to one of the same plane), so a
        // place in the folded entry is the same place in the entry.
        const folded = foldCase(entry);
        let written = '';
        let from = 0;
        for (let at = folded.indexOf(text); at >= 0; at = folded.indexOf(text, from)) {
            written += entry.slice(from, at) + replacement;
            from = at + text.length;
        }
        return written + entry.slice(from);
    };
}

/**
 * One Replace All over `rows`, the result in the order the request was read in, all or nothing. Read under
 * another `rowSequenceVersion` than the request's, it plans nothing, and the answer carries that version,
 * which the grid refuses as the order having moved. Otherwise each row is written through `write`, one
 * matching cell after the next, left to right in the request's column order, each write on the object the
 * previous one returned. `entryOf` answers a column's entry by name, or `undefined` for a column with no
 * text to search or one not known. `write` must only plan: it is asked of every match in an Editable
 * column even once one has refused, so the refusal names them all, and nothing it returns is applied
 * then. A write that returns the row it was handed is refused by name: a change made in place does not
 * reach the screen (ADR-0003).
 */
export function planReplace<Row>(
    rows: readonly Row[],
    rowSequenceVersion: number,
    request: GridReplaceRequest,
    entryOf: (column: string) => ((row: Row) => string) | undefined,
    write: (row: Row, column: string, text: string) => GridReplaceWrite<Row>,
): GridReplaceAnswer<Row> {
    const nothing = (): GridReplaceAnswer<Row> => Object.freeze({ result: replacedCells(0, rowSequenceVersion), changes: Object.freeze([]) });
    if (request.rowSequenceVersion !== rowSequenceVersion || request.text === '' || rows.length === 0 || request.columns.length === 0) {
        return nothing();
    }
    const replaced = replacerOf(patternOf(request), request.replacement);
    const entries = request.columns.map(entryOf);
    const editable = request.columns.map(name => request.editableColumns.includes(name));
    const refused: GridReplaceRefusedCell[] = [];
    const changes: GridReplacedRow<Row>[] = [];
    let count = 0;
    rows.forEach((before, row) => {
        let current = before;
        entries.forEach((entry, column) => {
            if (entry === undefined) return;
            if (request.scope !== undefined && !inScope(request.scope, row, column)) return;
            // Judged on the row as read, whatever an earlier cell's write made of it.
            const text = replaced(entry(before));
            if (text === undefined) return;
            const name = itemAt(request.columns, column);
            if (!itemAt(editable, column)) {
                refused.push({ row, column: name, message: undefined });
                return;
            }
            const written = write(current, name, text);
            if ('refusal' in written) {
                if (typeof written.refusal !== 'string' || written.refusal.trim() === '') {
                    throw new TypeError(`The write of row ${row}, column '${name}' refused without saying why; a refusal is named in the Consumer's words (ADR-0034).`);
                }
                refused.push({ row, column: name, message: written.refusal });
                return;
            }
            if (written.row === current) {
                throw new Error(`The write of row ${row}, column '${name}' returned the row it was handed. A change made in place `
                    + 'does not reach the screen: return a new object; identity is the change signal (ADR-0003).');
            }
            current = written.row;
            count++;
        });
        if (refused.length === 0 && current !== before) changes.push(Object.freeze({ before, after: current }));
    });
    return refused.length > 0
        ? Object.freeze({ result: refusedCells(refused, rowSequenceVersion), changes: Object.freeze([]) })
        : Object.freeze({ result: replacedCells(count, rowSequenceVersion), changes: Object.freeze(changes) });
}
