// The typing aids over the text of a Formula being typed (C#'s FormulaEntry.cs; ADR-0051, ADR-0057, ADR-0058).
import { CellAddress } from './cell-address';
import { CellRange } from './cell-range';
import { parseFormulaNumber } from './constant-parser';
import { Culture } from './culture';
import { DeclaredFunction } from './declared-function';
import { compareIgnoreCase, equalsIgnoreCase, ignoreCaseKey, isAsciiLetterOrDigit, isWhiteSpace, startsWithAt, startsWithAtIgnoreCase, toUpperInvariant } from './dotnet';
import { approximatelyEqual } from './formulas/arithmetic';
import { compareValues } from './formulas/evaluator';
import { structuredReferenceText as writeStructuredReference } from './formulas/formula-text';
import type { ArgumentValue } from './formulas/function-definition';
import { findFunction } from './formulas/function-library';
import { matchReference, readReference, readStructuredReference, type ReferenceMatch, type ReferenceMatchPart } from './formulas/lexer';
import { quoteSheetName } from './formulas/reference';
import { elementAt } from './must';
import { formulaConstantLongest, writtenText } from './number-text';
import type { Value } from './value';

/**
 * What a completion candidate names: a function of the declared set (`DeclaredFunction`), a Linked Table
 * declared on the Workbook (ADR-0049), a column of the Linked Table named before `[` (ADR-0058), or one of the
 * values an argument takes from a fixed list (ADR-0058).
 */
export type CompletionKind = 'function' | 'linkedTable' | 'linkedTableColumn' | 'argumentValue';

/** One candidate completion offers (ADR-0051, ADR-0058). */
export interface CompletionCandidate {
    /** What the list shows: the name as declared, or for an argument's value, Excel's text for it (`0 - Exact match`). */
    readonly name: string;
    /** What it names. */
    readonly kind: CompletionKind;
    /**
     * What accepting it writes in place of what was typed: a function's name and its `(`, a table's name, a
     * column's name with any character the grammar reads specially escaped by `'`, an argument's value (`0`).
     */
    readonly insertText: string;
    /** One sentence for a function; `undefined` for the others. */
    readonly description: string | undefined;
}

/**
 * The candidates for what is being typed at the caret (ADR-0051): accepting one replaces the `length` characters
 * from `start` with its `insertText`.
 */
export interface FormulaCompletion {
    /** Where what is being typed starts in the text: a name, a column after `[`, an argument's value. */
    readonly start: number;
    /** How long it is, including any part of it after the caret. */
    readonly length: number;
    /**
     * The candidates, in the order to list them; never empty. Names are in alphabetical order, a table's columns
     * in the table's order, and an argument's values in Excel's.
     */
    readonly candidates: readonly CompletionCandidate[];
}

/** The argument hint (ADR-0051): the function whose argument list holds the caret, and which argument it is in. */
export interface ArgumentHint {
    /** The declared function. */
    readonly function: DeclaredFunction;
    /** Which argument the caret is in, from 0. */
    readonly argumentIndex: number;
    /**
     * That argument's name as `DeclaredFunction.arguments` lists it — for an open-ended function past its named
     * arguments, the one that repeats — or `undefined` when the caret is past the last argument the function takes.
     */
    readonly currentArgument: string | undefined;
}

/** Where Point mode writes a Reference: the `length` characters from `start` are replaced (none, for an insertion at the caret). */
export interface PointSite {
    /** Where the Reference goes in the text. */
    readonly start: number;
    /** How many characters it replaces. */
    readonly length: number;
}

/**
 * What F4 makes of the text being edited (ADR-0051): the new text, and the span in it of what was rewritten,
 * which a grid selects or collapses to a caret at its end as the edit stands.
 */
export interface ReferenceCycle {
    /** The text with the Reference or References cycled. */
    readonly text: string;
    /** Where what was rewritten starts in `text`: at its first column or row part, after any Sheet qualifier before it. */
    readonly selectionStart: number;
    /** Where it ends. */
    readonly selectionEnd: number;
}

/**
 * A Linked Table's column, as a structured reference names it (`Positions[PV]`): the key a Reference Outline is
 * told by (ADR-0057). Two are equal when they name the same table and the same column without regard to case, as
 * the Sheet finds a table and its column (ADR-0049).
 */
export class LinkedTableColumn {
    /** The table's name, as written. */
    readonly table: string;

    /** The column's name, as written, with its `'` escapes read. */
    readonly column: string;

    /** The column of `table` named `column`. */
    constructor(table: string, column: string) {
        this.table = table;
        this.column = column;
    }

    /** Whether `other` names the same table and column, without regard to case. */
    equals(other: LinkedTableColumn): boolean {
        return equalsIgnoreCase(this.table, other.table) && equalsIgnoreCase(this.column, other.column);
    }

    /** A string two equal columns share, for keying a `Map` as `equals` compares. */
    get key(): string {
        return JSON.stringify([ignoreCaseKey(this.table), ignoreCaseKey(this.column)]);
    }
}

/**
 * A Reference in the text of a Formula being edited (ADR-0057): where it is written, and what it names — cells on
 * this Sheet, or a Linked Table's column. Exactly one of `cells` and `linkedColumn` is set.
 */
export interface FormulaReference {
    /** Where it starts in the text, whose `=` is at 0. */
    readonly start: number;
    /** How many characters it spans, its Sheet qualifier included. */
    readonly length: number;
    /**
     * The cells it names, held from their top-left corner however it was written (`B2:A1` names A1:B2), whole
     * columns for `A:A` and whole rows for `1:1`; `undefined` for a structured reference.
     */
    readonly cells: CellRange | undefined;
    /** The Linked Table column a structured reference reads; `undefined` for cells. */
    readonly linkedColumn: LinkedTableColumn | undefined;
}

type TokenKind = 'operand' | 'text' | 'operator' | 'open' | 'close' | 'comma';

interface Token {
    readonly kind: TokenKind;
    readonly start: number;
    readonly end: number;
    readonly text: string;
    readonly unterminated: boolean;
    readonly hasBrackets: boolean;
}

/** A parenthesis open at the caret: the function it calls (`undefined` for a grouping one), how many of its commas stand before the caret, and where the argument the caret is in starts. */
interface Frame {
    readonly function: string | undefined;
    readonly commas: number;
    readonly argumentStart: number;
}

/**
 * What F4 cycles in a Reference the grammar matched: the span it stands over, and its column and row parts in the
 * order written, each with whether it is a column.
 */
interface CycleTarget {
    readonly start: number;
    readonly end: number;
    readonly whole: boolean;
    readonly parts: readonly { readonly part: ReferenceMatchPart; readonly column: boolean }[];
}

const operators = ['<>', '<=', '>=', '=', '<', '>', '+', '-', '*', '/', '^', '&', '%'];
const columnSpecials = ['[', ']', '#', '@', "'"];
const namePattern = /^[A-Za-z_\\][A-Za-z0-9_.]*$/;
const pointedPattern = /^\$?[A-Za-z]{1,3}\$?[1-9][0-9]{0,6}(?::\$?[A-Za-z]{1,3}\$?[1-9][0-9]{0,6})?$/;
/** What may follow the colon of a range still being typed: nothing yet, or the start of a cell's address (`B`, `$B$`, `B1`). */
const secondCornerBegun = /^\$?[A-Za-z]{0,3}\$?[0-9]{0,7}$/;
/** A number constant with one sign before it; white space after it is read apart, as .NET's `\s` reads it. */
const signedNumberPattern = /^(?<sign>[+-]?)(?<number>(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?)(?<rest>.*)$/s;
const errorKey = 'An Error Value is not a key a lookup finds (ADR-0058).';

// Pure answers over the text of a Formula being typed and its caret, for the aids ADR-0051 asks of the Consumer:
// completion candidates, the argument hint, whether a Reference can go at the caret (Point mode), the Reference
// text for a pointed range, and what F4 makes of the Reference at the caret; and, for ADR-0057's Reference
// Outlines, every Reference in the text. Each works on unfinished text: nothing here requires the Formula to
// parse. Text not beginning with `=` is not a Formula, and gets no aid.

/**
 * What completion offers at the caret, following Excel's triggers (ADR-0051, ADR-0058):
 *
 * - at the end of a name standing where an operand can start, the declared functions and `linkedTables`
 *   beginning with it, without regard to case;
 *
 * - inside the brackets of `Table[`, the columns of that table beginning with the column typed so far, in the
 *   table's order, and nothing else: not `@` or `#All`, which the grammar refuses (ADR-0047). The brackets are
 *   a context of their own, so at an argument whose values are a fixed list they list the columns too;
 *
 * - at an argument that takes one of a fixed list of values (`DeclaredFunction.valuesOf`), those values, in
 *   Excel's order, and nothing else. They are listed before anything is typed, and only while nothing of the
 *   argument stands after the caret, white space included. A number lists the value it is alone (`0` lists
 *   `0 - Exact match`) and nothing when it is no value (`4`); any other text lists every value.
 *
 * `undefined` anywhere else — after `=`, an operator, `(` or `,` at any other argument or inside a grouping
 * parenthesis, inside text in quotes, a Reference, a number — and when nothing matches.
 *
 * @param text - The text being edited.
 * @param caret - Where the caret stands in it. Throws a RangeError outside 0 to the text's length.
 * @param linkedTables - The Linked Tables' names.
 * @param columnsOf - The columns of the Linked Table a name names, found without regard to case, in the
 * table's order; `undefined` for a name no table has. Left out, no table's columns are known, and nothing is
 * listed after `Table[`.
 */
export function completeFormula(text: string, caret: number, linkedTables: Iterable<string>,
    columnsOf?: (name: string) => readonly string[] | undefined): FormulaCompletion | undefined {
    if (!isFormula(text, caret)) return undefined;
    const tokens = scan(text);
    const index = tokens.findIndex(t => t.start < caret && caret <= t.end);
    const at = index >= 0 ? elementAt(tokens, index) : undefined;
    const bracketed = at !== undefined && at.kind === 'operand' && at.hasBrackets ? at : undefined;
    // Inside Table['s brackets a structured reference is being typed, a context of its own as a grouping
    // parenthesis is: its columns are listed at any argument (decided with the user, 2026-10-01).
    if (bracketed !== undefined && insideBrackets(text, caret, bracketed)) {
        return operandMayStart(tokens, index) && columnsOf !== undefined ? completeColumn(text, caret, bracketed, columnsOf) : undefined;
    }
    // An argument whose values are a fixed list is completed with them alone: letters there list no function
    // and no table, as Excel's do not (ADR-0058, the thirteenth run, Q54).
    const argument = valueArgumentAt(text, tokens, caret);
    if (argument !== undefined) return completeValue(text, caret, tokens, argument.start, argument.values);
    // Before a structured reference's brackets or past them, no name is being typed.
    if (bracketed !== undefined) return undefined;
    return index >= 0 ? completeName(text, caret, tokens, index, linkedTables) : undefined;
}

/**
 * The hint for the innermost function call whose argument list holds the caret, counting arguments by the
 * commas at its own depth; grouping parentheses inside it belong to it, and text in quotes is one argument
 * whatever it holds. `undefined` outside any call, or when that function is not declared. Throws a RangeError
 * when the caret lies outside 0 to the text's length.
 */
export function formulaHintAt(text: string, caret: number): ArgumentHint | undefined {
    if (!isFormula(text, caret)) return undefined;
    for (const frame of framesAt(text, scan(text), caret).toReversed()) {
        if (frame.function === undefined) continue;
        const declared = DeclaredFunction.find(frame.function);
        if (declared === undefined) return undefined;
        return { function: declared, argumentIndex: frame.commas, currentArgument: argumentName(declared, frame.commas) };
    }
    return undefined;
}

/**
 * Whether a Reference can be written at the caret (ADR-0051's Point predicate): the Formula stands after its
 * `=`, an operator (not `%`), `(` or `,`, with nothing after the caret that the Reference would run into.
 * `undefined` when it cannot. Throws a RangeError when the caret lies outside 0 to the text's length.
 */
export function formulaPointAt(text: string, caret: number): PointSite | undefined {
    if (!isFormula(text, caret)) return undefined;
    const tokens = scan(text);
    if (tokens.some(t => t.start < caret && caret < t.end) || tokens.some(t => t.unterminated && t.start < caret)) return undefined;
    const before = tokens.findLastIndex(t => t.end <= caret);
    if (!operandMayStart(tokens, before + 1) || !mayFollowOperand(tokens, caret)) return undefined;
    return { start: caret, length: 0 };
}

/**
 * The Reference that ends at the caret and stands where Point mode could have written it — the one a further
 * arrow key replaces while the user is pointing (ADR-0051). `undefined` when no Reference ends at the caret
 * in such a place. Only the component knows whether it is pointing; typed by hand, the same text is not
 * pointed. Kept off the package's surface, as the C# engine keeps it internal, until ExSheet's component is ported.
 */
export function pointedReferenceAt(text: string, caret: number): PointSite | undefined {
    if (!isFormula(text, caret)) return undefined;
    const tokens = scan(text);
    const index = tokens.findIndex(t => t.end === caret);
    if (index < 0) return undefined;
    const token = elementAt(tokens, index);
    if (token.kind !== 'operand' || !pointedPattern.test(text.slice(token.start, token.end))) return undefined;
    if (!operandMayStart(tokens, index) || !mayFollowOperand(tokens, caret)) return undefined;
    return { start: token.start, length: token.end - token.start };
}

/**
 * The Reference Point mode writes for a pointed range: `B7` for one cell, `B7:C9` from its top-left
 * otherwise. With `sheet`, the name of the Sheet the range was pointed at when that is not the one the
 * Formula is typed on (ADR-0331 §5), the range qualified with it, quoted where a Reference must quote it —
 * `Sheet2!B7`, `'Q1 Sales'!B7:C9`. Throws a RangeError when `sheet` is empty.
 */
export function referenceText(range: CellRange, sheet?: string): string {
    if (sheet === undefined) return range.toString();
    if (sheet.length === 0) throw new RangeError('The Sheet name is empty.');
    return `${quoteSheetName(sheet)}!${range.toString()}`;
}

/**
 * What Point writes for a pressed column of a Linked Table, through a Pointing Scope (ADR-0058): Excel's
 * structured reference, `Positions[PV]`, written as the engine writes one back — single brackets, with `'`
 * before each of `[ ] # ' @` in the column's name. Throws a RangeError when either name is empty.
 */
export function structuredReferenceText(table: string, column: string): string {
    if (table.length === 0) throw new RangeError('The table name is empty.');
    if (column.length === 0) throw new RangeError('The column name is empty.');
    return writeStructuredReference(table, column);
}

/**
 * A Value written as a Formula writes a constant of its kind (ADR-0058): text in double quotes, with any `"`
 * in it doubled (`"R-4471"`); a number in the invariant form the engine writes a number constant in (`1250`,
 * `-0.5`); a boolean as `TRUE` or `FALSE`. `undefined` for an Error Value: a lookup by one finds nothing, so
 * nothing is written for it.
 */
export function constantText(value: Value): string | undefined {
    switch (value.kind) {
        case 'text':
            return `"${value.text.replaceAll('"', '""')}"`;
        case 'number':
            return writtenText(value.number, Culture.invariant, formulaConstantLongest);
        case 'boolean':
            return value.boolean ? 'TRUE' : 'FALSE';
        case 'error':
            return undefined;
        default:
            return unreachableKind(value.kind);
    }
}

/**
 * What Point writes for a pressed cell of a Linked Table, through a Pointing Scope (ADR-0058): the lookup that
 * reads the cell by its row's key, `XLOOKUP("R-4471", Positions[Id], Positions[PV])` — the key as a constant
 * of its kind (`constantText`), then the key column and the pressed column as structured references
 * (`structuredReferenceText`). It names no position, so the Formula reads the same row after the grid that
 * showed it is sorted (ADR-0049, rule 2).
 *
 * For a key of several columns (ADR-0058, amended 2026-10-03), `keyColumns` and `keys` are lists, one Value
 * per key column, and the lookup reads the cell by every part of its row's key:
 * `XLOOKUP(1, (Cds[Entity]="ACME")*(Cds[Tenor]="5Y"), Cds[Spread])`. Each part is compared as the `=`
 * operator compares, and the row whose parts all match is the one read.
 *
 * Throws a RangeError when a name is empty, the key has not one Value per key column, or a part of it is an
 * Error Value, which no lookup finds.
 */
export function lookupText(table: string, keyColumn: string, key: Value, column: string): string;
/**
 * What Point writes for a pressed cell of a Linked Table, through a Pointing Scope (ADR-0058): the lookup that
 * reads the cell by its row's key, `XLOOKUP("R-4471", Positions[Id], Positions[PV])` — the key as a constant
 * of its kind (`constantText`), then the key column and the pressed column as structured references
 * (`structuredReferenceText`). It names no position, so the Formula reads the same row after the grid that
 * showed it is sorted (ADR-0049, rule 2).
 *
 * For a key of several columns (ADR-0058, amended 2026-10-03), `keyColumns` and `keys` are lists, one Value
 * per key column, and the lookup reads the cell by every part of its row's key:
 * `XLOOKUP(1, (Cds[Entity]="ACME")*(Cds[Tenor]="5Y"), Cds[Spread])`. Each part is compared as the `=`
 * operator compares, and the row whose parts all match is the one read.
 *
 * Throws a RangeError when a name is empty, the key has not one Value per key column, or a part of it is an
 * Error Value, which no lookup finds.
 */
export function lookupText(table: string, keyColumns: readonly string[], keys: readonly Value[], column: string): string;
export function lookupText(table: string, keyColumns: string | readonly string[], keys: Value | readonly Value[], column: string): string {
    if (typeof keyColumns === 'string') {
        if (isList(keys)) throw new TypeError('One key column takes one Value.');
        const constant = constantText(keys);
        if (constant === undefined) throw new RangeError(errorKey);
        return `XLOOKUP(${constant}, ${structuredReferenceText(table, keyColumns)}, ${structuredReferenceText(table, column)})`;
    }
    if (!isList(keys)) throw new TypeError('Several key columns take one Value each.');
    if (keyColumns.length === 0 || keyColumns.length !== keys.length) throw new RangeError('The key has one Value per key column.');
    if (keyColumns.length === 1) return lookupText(table, elementAt(keyColumns, 0), elementAt(keys, 0), column);
    const parts = keyColumns.map((keyColumn, i) => {
        const constant = constantText(elementAt(keys, i));
        if (constant === undefined) throw new RangeError(errorKey);
        return `(${structuredReferenceText(table, keyColumn)}=${constant})`;
    });
    return `XLOOKUP(1, ${parts.join('*')}, ${structuredReferenceText(table, column)})`;
}

/**
 * Whether the lookup `lookupText` writes for `key` finds a row whose key is `candidate`: `XLOOKUP`'s exact
 * match, which tells a number from text and text apart without regard to case, and never matches a blank
 * (`undefined`) or an Error Value. A Pointing Scope finds the row a press was written for by it, wherever the
 * grid that shows the table has sorted that row to (ADR-0058, "What is drawn").
 *
 * For a key of several columns, `key` and `candidate` are lists, one Value per key column: every part equal as
 * the `=` operator finds it — of one kind, text without regard to case, numbers at Excel's fifteen digits —
 * and none blank or an Error Value.
 */
export function lookupFinds(key: Value, candidate: Value | undefined): boolean;
/**
 * Whether the lookup `lookupText` writes for `key` finds a row whose key is `candidate`: `XLOOKUP`'s exact
 * match, which tells a number from text and text apart without regard to case, and never matches a blank
 * (`undefined`) or an Error Value. A Pointing Scope finds the row a press was written for by it, wherever the
 * grid that shows the table has sorted that row to (ADR-0058, "What is drawn").
 *
 * For a key of several columns, `key` and `candidate` are lists, one Value per key column: every part equal as
 * the `=` operator finds it — of one kind, text without regard to case, numbers at Excel's fifteen digits —
 * and none blank or an Error Value.
 */
export function lookupFinds(keys: readonly Value[], candidates: readonly (Value | undefined)[]): boolean;
export function lookupFinds(keys: Value | readonly Value[], candidates: Value | undefined | readonly (Value | undefined)[]): boolean {
    if (!isList(keys)) {
        if (isList(candidates)) throw new TypeError('One key is compared with one Value.');
        return candidates !== undefined && candidates.kind === keys.kind && !keys.isError && compareValues(candidates, keys) === 0;
    }
    if (!isList(candidates)) throw new TypeError('A key of several Values is compared with as many.');
    if (keys.length !== candidates.length) return false;
    if (keys.length === 1) return lookupFinds(elementAt(keys, 0), candidates[0]);
    for (let i = 0; i < keys.length; i++) {
        const value = candidates[i];
        const key = elementAt(keys, i);
        if (value === undefined || value.kind !== key.kind || key.isError) return false;
        if (compareValues(value, key, approximatelyEqual) !== 0) return false;
    }
    return true;
}

/**
 * F4 (ADR-0051, 2026-09-29; as Excel does it, the seventeenth Windows run): the Reference at the caret cycled
 * to its next form — `A1` → `$A$1` → `A$1` → `$A1` → `A1` — answered with the span of what was rewritten. The
 * Reference at the caret is the one the caret is inside or touching, on either side; the two ends of a cell
 * range are References of their own, so only the end at the caret cycles, from its own form. With a
 * selection, every end it covers, overlaps or touches cycles, each to the next form of the first one. Whole
 * columns and whole rows are one Reference of two forms (`A:A` ↔ `$A:$A`); a Sheet qualifier is kept, before
 * the first end, and only the cell part cycles. The rewritten parts' column letters are written in upper
 * case; every other character stays as it was typed. The span answered runs from the first Reference
 * rewritten, after its Sheet qualifier, to the end of the last. `undefined` — nothing changes — for text that
 * is not a Formula, and where no Reference is at the caret or in the selection.
 *
 * @param text - The text being edited.
 * @param selectionStart - Where the selection starts, or the caret.
 * @param selectionEnd - Where the selection ends; `selectionStart` for a caret. Throws a RangeError when the
 * selection does not lie inside the text.
 */
export function cycleReference(text: string, selectionStart: number, selectionEnd: number): ReferenceCycle | undefined {
    if (!Number.isInteger(selectionStart) || selectionStart < 0 || selectionStart > text.length) {
        throw new RangeError("The selection starts 0 to the text's length.");
    }
    if (!Number.isInteger(selectionEnd) || selectionEnd < selectionStart || selectionEnd > text.length) {
        throw new RangeError("The selection ends from its start to the text's length.");
    }
    if (text.length === 0 || text.charAt(0) !== '=') return undefined;

    // Inside or touching, for a caret and a selection alike (observed in Excel: + selected in =A1+B1 gives
    // =$A$1+$B$1). A caret takes the first Reference it touches.
    const caret = selectionStart === selectionEnd;
    const targets: CycleTarget[] = [];
    for (const token of scan(text)) {
        const touched = token.start <= selectionEnd && selectionStart <= token.end;
        if (!touched || token.kind !== 'operand' || token.unterminated || token.hasBrackets) continue;
        // The operand is a Reference only where the grammar reads one over the whole of it.
        const reference = matchReference(text, token.start);
        if (reference === undefined || reference.index + reference.length !== token.end) continue;
        for (const target of targetsOf(reference)) {
            if (target.start > selectionEnd || selectionStart > target.end) continue;
            targets.push(target);
            if (caret) break;
        }
        if (caret && targets.length > 0) break;
    }
    const first = targets[0];
    if (first === undefined) return undefined;

    const form = nextForm(first);
    let rewritten = '';
    let at = 0;
    // The span starts at the first part rewritten: a Sheet qualifier before it is kept as it was typed, and
    // lies outside what F4 rewrote (observed, run 18a).
    const start = elementAt(first.parts, 0).part.index;
    for (const target of targets) {
        for (const { part, column } of target.parts) {
            rewritten += text.slice(at, part.index);
            if (column ? form.column : form.row) rewritten += '$';
            const written = part.value.replace(/^\$+/, '');
            rewritten += column ? toUpperInvariant(written) : written;
            at = part.index + part.value.length;
        }
    }
    const end = rewritten.length;
    rewritten += text.slice(at);
    return { text: rewritten, selectionStart: start, selectionEnd: end };
}

/**
 * Every Reference in the text of a Formula being edited, in the order written (ADR-0057): its span, and the
 * cells it names or, for a structured reference, the Linked Table column it reads. A Reference is what the
 * formula grammar reads as one over the whole of an operand, as for F4. The text need not parse: an unfinished
 * Formula (`=SUM(A1,`) is answered as far as it goes, and an unclosed string, quoted Sheet name or bracket
 * hides only what follows it. Nothing inside a string is a Reference, nor is a function name (`LOG10(`), nor
 * one qualified with another Sheet's name, which names no cells. A range typed as far as its colon
 * (`=SUM(A1:`) answers its first corner, the colon left out. Text beginning with `+` or `-` is answered as a
 * Formula is, as Excel colours it and ExSheet enters it (`=+A1`); other text is answered with nothing (the
 * eighth Windows run, cases 24–26).
 *
 * @param text - The text being edited.
 * @param sheetName - This Sheet's name: a Reference qualified with it, without regard to case, names this Sheet's cells.
 */
export function formulaReferences(text: string, sheetName: string): readonly FormulaReference[] {
    const lead = text.charAt(0);
    if (lead !== '=' && lead !== '+' && lead !== '-') return [];

    const references: FormulaReference[] = [];
    for (const token of scan(text)) {
        if (token.kind !== 'operand' || token.unterminated) continue;
        const length = token.end - token.start;
        if (token.hasBrackets) {
            const structured = readStructuredReference(text, token.start);
            if (structured !== undefined && structured.length === length) {
                references.push({ start: token.start, length, cells: undefined, linkedColumn: new LinkedTableColumn(structured.table, structured.column) });
            }
            continue;
        }
        const read = readOperandReference(text, token.start, length);
        if (read === undefined) continue;
        const qualifier = read.reference.sheetName;
        if (qualifier !== undefined && !equalsIgnoreCase(qualifier, sheetName)) continue;
        const area = read.reference.area;
        const cells = new CellRange(new CellAddress(area.row1, area.column1), new CellAddress(area.row2, area.column2));
        references.push({ start: token.start, length: read.length, cells, linkedColumn: undefined });
    }
    return references;
}

function unreachableKind(kind: never): never {
    throw new Error(`Internal: ${String(kind)} is no kind of Value.`);
}

function isList<T>(value: T | readonly T[]): value is readonly T[] {
    return Array.isArray(value);
}

function isFormula(text: string, caret: number): boolean {
    if (!Number.isInteger(caret) || caret < 0 || caret > text.length) throw new RangeError("The caret is 0 to the text's length.");
    return text.length > 0 && text.charAt(0) === '=' && caret >= 1;
}

/**
 * Whether the caret stands inside the brackets of a structured reference: after its first `[`, and not past the
 * `]` that closes it, `'` escapes read as the scan reads them.
 */
function insideBrackets(text: string, caret: number, token: Token): boolean {
    const open = indexWithin(text, '[', token.start, token.end);
    if (open < 0 || caret <= open) return false;
    let depth = 0;
    for (let i = open; i < caret; i++) {
        const c = text.charAt(i);
        if (c === "'") {
            i++;
            continue;
        }
        if (c === '[') depth++;
        else if (c === ']' && --depth === 0) return false;
    }
    return true;
}

/** Where `c` first stands in `text` from `from` up to `end`, or -1. */
function indexWithin(text: string, c: string, from: number, end: number): number {
    const index = text.indexOf(c, from);
    return index >= 0 && index < end ? index : -1;
}

/** The functions and tables beginning with the name that ends at the caret. */
function completeName(text: string, caret: number, tokens: readonly Token[], index: number, linkedTables: Iterable<string>): FormulaCompletion | undefined {
    const token = elementAt(tokens, index);
    if (token.kind !== 'operand' || token.unterminated) return undefined;
    const prefix = text.slice(token.start, caret);
    if (!namePattern.test(prefix) || !operandMayStart(tokens, index)) return undefined;
    // Inside a Reference no name is being typed: =F|3 is the cell F3 with the caret in it. At its end the text
    // may still be a name's beginning (=LOG1 lists LOG10), as Excel's list shows.
    if (caret < token.end) {
        const reference = readReference(text, token.start);
        if (reference !== undefined && token.start + reference.length === token.end) return undefined;
    }

    const candidates: CompletionCandidate[] = [];
    for (const f of DeclaredFunction.all) {
        if (startsWithAtIgnoreCase(f.name, 0, prefix)) candidates.push({ name: f.name, kind: 'function', insertText: `${f.name}(`, description: f.description });
    }
    for (const t of linkedTables) {
        if (startsWithAtIgnoreCase(t, 0, prefix)) candidates.push({ name: t, kind: 'linkedTable', insertText: t, description: undefined });
    }
    return candidates.length === 0
        ? undefined
        : { start: token.start, length: token.end - token.start, candidates: candidates.toSorted((a, b) => compareIgnoreCase(a.name, b.name)) };
}

/**
 * The columns after `Table[` (ADR-0058): the caret stands inside the one pair of brackets the grammar reads, after
 * a declared table's name, and what is typed before it is the beginning of a column, `'` escapes read. Accepting a
 * column writes its name, escaped, over the column typed — up to the closing bracket when there is one — and
 * leaves the closing bracket to the user, as for a table's name.
 */
function completeColumn(text: string, caret: number, token: Token, columnsOf: (name: string) => readonly string[] | undefined): FormulaCompletion | undefined {
    const open = indexWithin(text, '[', token.start, token.end);
    if (open < 0 || caret <= open) return undefined;
    const table = text.slice(token.start, open);
    if (!namePattern.test(table)) return undefined;
    const columns = columnsOf(table);
    if (columns === undefined || columns.length === 0) return undefined;

    let typed = '';
    let end = -1;
    for (let i = open + 1; i < token.end; i++) {
        const c = text.charAt(i);
        if (c === "'") {
            // An escape the caret splits leaves no column typed that can be read.
            if (i < caret && i + 1 >= caret) return undefined;
            if (i < caret) typed += text.charAt(i + 1);
            i++;
            continue;
        }
        if (c === ']') {
            end = i;
            break;
        }
        // A second bracket, #All and @ are forms the grammar refuses (ADR-0047). Past the caret, only a second
        // bracket matters: the first ] would not close this one.
        if (c === '[' || (i < caret && (c === '#' || c === '@'))) return undefined;
        if (i < caret) typed += c;
    }
    if (end >= 0 && end < caret) return undefined;
    const candidates = columns
        .filter(column => startsWithAtIgnoreCase(column, 0, typed))
        .map((column): CompletionCandidate => ({ name: column, kind: 'linkedTableColumn', insertText: escapeColumn(column), description: undefined }));
    const start = open + 1;
    return candidates.length === 0 ? undefined : { start, length: (end >= 0 ? end : caret) - start, candidates };
}

/** A column's name as a structured reference writes it: `'` before each character the grammar reads specially. */
function escapeColumn(column: string): string {
    let escaped = '';
    for (const c of column) {
        if (columnSpecials.includes(c)) escaped += "'";
        escaped += c;
    }
    return escaped;
}

/**
 * Where the argument the caret stands at starts, and its values, when the innermost parenthesis open at the caret
 * is a declared function's and that argument takes one of a fixed list (ADR-0058); `undefined` anywhere else.
 * Inside a grouping parenthesis the caret stands in an expression of its own, as inside a call of its own
 * (decided with the user, 2026-10-01).
 */
function valueArgumentAt(text: string, tokens: readonly Token[], caret: number): { start: number; values: readonly ArgumentValue[] } | undefined {
    const call = framesAt(text, tokens, caret).at(-1);
    if (call?.function === undefined) return undefined;
    const declared = DeclaredFunction.find(call.function);
    if (declared === undefined) return undefined;
    const values = declared.valuesOf(call.commas);
    return values.length === 0 ? undefined : { start: call.argumentStart, values };
}

/**
 * The values of an argument that takes one of a fixed list (ADR-0058), while nothing of it stands after the
 * caret: the caret is at the argument's end, before the `,` or `)` that ends it. Before a value, inside one or
 * before white space Excel lists nothing (Part B of the ninth Windows run, Q49; the thirteenth, Q55). A number or
 * boolean lists the value it is alone, a number no value takes lists nothing; any other text lists every value
 * (the tenth and thirteenth Windows runs, Q54). Accepting one writes it over everything typed in the argument.
 */
function completeValue(text: string, caret: number, tokens: readonly Token[], argumentStart: number, values: readonly ArgumentValue[]): FormulaCompletion | undefined {
    if (tokens.some(t => t.start < caret && caret < t.end)) return undefined;
    if (caret < text.length && text.charAt(caret) !== ',' && text.charAt(caret) !== ')') return undefined;

    let start = argumentStart;
    while (start < caret && isWhiteSpace(text.charCodeAt(start))) start++;
    const typed = text.slice(start, caret);
    const number = numberOf(typed);
    let listed: readonly ArgumentValue[];
    if (number !== undefined) {
        listed = values.filter(v => numberOf(v.value) === number);
    } else {
        const literal = values.find(v => equalsIgnoreCase(v.value, trimWhiteSpaceBoth(typed)));
        listed = literal === undefined ? values : [literal];
    }
    if (listed.length === 0) return undefined;
    const candidates = listed.map((v): CompletionCandidate => ({ name: v.text, kind: 'argumentValue', insertText: v.value, description: undefined }));
    return { start, length: caret - start, candidates };
}

/** `string.Trim()`: white space as `char.IsWhiteSpace` reads it, off both ends. */
function trimWhiteSpaceBoth(text: string): string {
    let start = 0;
    let end = text.length;
    while (start < end && isWhiteSpace(text.charCodeAt(start))) start++;
    while (end > start && isWhiteSpace(text.charCodeAt(end - 1))) end--;
    return text.slice(start, end);
}

/**
 * The number `typed` is, read as the grammar reads a number constant with one sign before it and white space
 * after it (`-1`, `+1`, `1.0`, `1 `), or `undefined` for text that is not a number (`-`, `1+`, `A1`; decided with
 * the user, 2026-10-01).
 */
function numberOf(typed: string): number | undefined {
    const match = signedNumberPattern.exec(typed);
    const groups = match?.groups;
    if (groups === undefined) return undefined;
    const { sign, number, rest } = groups;
    if (sign === undefined || number === undefined || rest === undefined) throw new Error('Internal: the number pattern matched without its groups.');
    for (let i = 0; i < rest.length; i++) {
        if (!isWhiteSpace(rest.charCodeAt(i))) return undefined;
    }
    const value = parseFormulaNumber(number);
    return sign === '-' ? -value : value;
}

/** The parentheses open at the caret, the innermost last. */
function framesAt(text: string, tokens: readonly Token[], caret: number): Frame[] {
    const frames: Frame[] = [];
    for (let i = 0; i < tokens.length; i++) {
        const token = elementAt(tokens, i);
        if (token.end > caret) break;
        switch (token.kind) {
            case 'open': {
                const previous = i > 0 ? elementAt(tokens, i - 1) : undefined;
                const name = previous === undefined ? '' : text.slice(previous.start, previous.end);
                const callee = previous !== undefined && previous.kind === 'operand' && previous.end === token.start && namePattern.test(name) ? name : undefined;
                frames.push({ function: callee, commas: 0, argumentStart: token.end });
                break;
            }
            case 'close':
                frames.pop();
                break;
            case 'comma': {
                const frame = frames.pop();
                if (frame !== undefined) frames.push({ function: frame.function, commas: frame.commas + 1, argumentStart: token.end });
                break;
            }
            case 'operand':
            case 'text':
            case 'operator':
                break;
            default:
                return unreachableToken(token.kind);
        }
    }
    return frames;
}

function unreachableToken(kind: never): never {
    throw new Error(`Internal: ${String(kind)} is no kind of token.`);
}

function argumentName(declared: DeclaredFunction, index: number): string | undefined {
    const names = declared.arguments.split(', ');
    const last = elementAt(names, names.length - 1);
    if (index < names.length && elementAt(names, index) !== '...') return elementAt(names, index);
    if (last === '...') {
        const definition = findFunction(declared.name);
        if (definition?.inPairs === true) return elementAt(names, definition.pairOffset + (index - definition.pairOffset) % 2);
        return elementAt(names, names.length - 2);
    }
    return undefined;
}

/** What F4 cycles in a Reference the grammar matched: a cell range's two ends are two, a whole column or row one. */
function targetsOf(reference: ReferenceMatch): CycleTarget[] {
    const end = reference.index + reference.length;
    const group = (name: string): ReferenceMatchPart | undefined => reference.groups[name];
    const required = (name: string): ReferenceMatchPart => {
        const part = group(name);
        if (part === undefined) throw new Error(`Internal: the Reference matched without its ${name}.`);
        return part;
    };
    if (group('cc1') !== undefined) {
        return [{ start: reference.index, end, whole: true, parts: [{ part: required('cc1'), column: true }, { part: required('cc2'), column: true }] }];
    }
    if (group('rr1') !== undefined) {
        return [{ start: reference.index, end, whole: true, parts: [{ part: required('rr1'), column: false }, { part: required('rr2'), column: false }] }];
    }
    const c1 = required('c1');
    const r1 = required('r1');
    const targets: CycleTarget[] = [{ start: reference.index, end: r1.index + r1.value.length, whole: false, parts: [{ part: c1, column: true }, { part: r1, column: false }] }];
    const c2 = group('c2');
    if (c2 !== undefined) {
        const r2 = required('r2');
        targets.push({ start: c2.index, end: r2.index + r2.value.length, whole: false, parts: [{ part: c2, column: true }, { part: r2, column: false }] });
    }
    return targets;
}

/**
 * The form F4 gives next, from `target`'s own parts: whether columns and rows are written with `$`. A cell's four
 * forms run relative, both, the row alone, the column alone; whole columns and whole rows have two, relative and
 * absolute, taken from their first part, and give both axes the one they take.
 */
function nextForm(target: CycleTarget): { column: boolean; row: boolean } {
    const firstAbsolute = elementAt(target.parts, 0).part.value.startsWith('$');
    if (target.whole) return firstAbsolute ? { column: false, row: false } : { column: true, row: true };
    const secondAbsolute = elementAt(target.parts, 1).part.value.startsWith('$');
    if (!firstAbsolute && !secondAbsolute) return { column: true, row: true };
    if (firstAbsolute && secondAbsolute) return { column: false, row: true };
    if (!firstAbsolute) return { column: true, row: false };
    return { column: false, row: false };
}

/**
 * The operand at `start` read as a Reference over the whole of it, as F4 reads one; or, while a range is typed as
 * far as its colon or into its second corner (`A1:`, `A1:B`), its first corner, which Excel colours (the eighth
 * Windows run, case 26). The colon looked for is the one after a Sheet qualifier's `!`, not one inside a quoted
 * Sheet name, and what follows it must be the start of a second corner and no more.
 */
function readOperandReference(text: string, start: number, length: number): ReturnType<typeof readReference> {
    const whole = readReference(text, start);
    if (whole !== undefined && whole.length === length) return whole;
    const bang = text.lastIndexOf('!', start + length - 1);
    const from = bang >= start ? bang + 1 : start;
    const colon = indexWithin(text, ':', from, start + length);
    if (colon <= start || !secondCornerBegun.test(text.slice(colon + 1, start + length))) return undefined;
    const first = readReference(text.slice(0, colon), start);
    return first !== undefined && first.length === colon - start ? first : undefined;
}

/** Whether an operand can start at token `index`: right after the `=`, an operator that takes a right side, `(` or `,`. */
function operandMayStart(tokens: readonly Token[], index: number): boolean {
    if (index === 0) return true;
    const previous = elementAt(tokens, index - 1);
    return previous.kind === 'open' || previous.kind === 'comma' || (previous.kind === 'operator' && previous.text !== '%');
}

/** Whether what follows the caret leaves room for an operand: the end, `)`, `,` or an operator. */
function mayFollowOperand(tokens: readonly Token[], caret: number): boolean {
    const next = tokens.find(t => t.start >= caret);
    return next === undefined || next.kind === 'close' || next.kind === 'comma' || next.kind === 'operator';
}

function makeToken(kind: TokenKind, start: number, end: number, text: string, unterminated = false, hasBrackets = false): Token {
    return { kind, start, end, text, unterminated, hasBrackets };
}

/**
 * A tolerant scan of a Formula's text after its `=`: text in quotes, parentheses, commas, operators, and operands
 * — a run of name, number, Reference or Error Value characters, with a quoted Sheet name and a structured
 * reference's brackets inside it. It never fails: an unclosed quote or bracket runs to the end and is marked so.
 */
function scan(text: string): Token[] {
    const tokens: Token[] = [];
    let i = 1;
    while (i < text.length) {
        const c = text.charAt(i);
        if (isWhiteSpace(text.charCodeAt(i))) {
            i++;
            continue;
        }
        const start = i;
        if (c === '"') {
            i++;
            let closed = false;
            while (i < text.length) {
                if (text.charAt(i) === '"') {
                    if (i + 1 < text.length && text.charAt(i + 1) === '"') {
                        i += 2;
                        continue;
                    }
                    i++;
                    closed = true;
                    break;
                }
                i++;
            }
            tokens.push(makeToken('text', start, i, text.slice(start, i), !closed));
            continue;
        }
        if (c === '(' || c === ')' || c === ',') {
            tokens.push(makeToken(c === '(' ? 'open' : c === ')' ? 'close' : 'comma', i, i + 1, c));
            i++;
            continue;
        }
        const op = operators.find(o => startsWithAt(text, i, o));
        if (op !== undefined) {
            tokens.push(makeToken('operator', i, i + op.length, op));
            i += op.length;
            continue;
        }

        // An Error Value, whose spelling holds characters that are operators elsewhere (#DIV/0!).
        if (c === '#') {
            i++;
            while (i < text.length && (isAsciiLetterOrDigit(text.charCodeAt(i)) || '/!?_'.includes(text.charAt(i)))) i++;
            tokens.push(makeToken('operand', start, i, text.slice(start, i)));
            continue;
        }

        // An operand: everything up to the next space, quote, parenthesis, comma or operator, with a quoted Sheet
        // name and bracketed columns read whole.
        let unterminated = false;
        let brackets = false;
        while (i < text.length && !unterminated) {
            const d = text.charAt(i);
            if (d === "'") {
                i++;
                unterminated = true;
                while (i < text.length) {
                    if (text.charAt(i) === "'" && !(i + 1 < text.length && text.charAt(i + 1) === "'")) {
                        i++;
                        unterminated = false;
                        break;
                    }
                    i += text.charAt(i) === "'" ? 2 : 1;
                }
                continue;
            }
            if (d === '[') {
                brackets = true;
                let depth = 0;
                unterminated = true;
                while (i < text.length) {
                    if (text.charAt(i) === "'" && i + 1 < text.length) {
                        i += 2;
                        continue;
                    }
                    if (text.charAt(i) === '[') depth++;
                    else if (text.charAt(i) === ']' && --depth === 0) {
                        i++;
                        unterminated = false;
                        break;
                    }
                    i++;
                }
                continue;
            }
            if (isWhiteSpace(text.charCodeAt(i)) || d === '"' || d === '(' || d === ')' || d === ',' || operators.some(o => o.charAt(0) === d)) break;
            i++;
        }
        if (i === start) i++;
        tokens.push(makeToken('operand', start, i, text.slice(start, i), unterminated, brackets));
    }
    return tokens;
}
