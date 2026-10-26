// XLOOKUP and XMATCH and their search (C#'s Functions.cs); INDEX, CHOOSE, ROW, COLUMN, ROWS, COLUMNS, OFFSET and
// INDIRECT (Functions.Lookup.cs); VLOOKUP, HLOOKUP and MATCH (Functions.LegacyLookup.cs); Excel's wildcards.
import { CellAddress, columnCount, rowCount } from '../../cell-address';
import { isAscii, isAsciiLetterOrDigit, upperInvariant } from '../../dotnet';
import { elementAt } from '../../must';
import { Value } from '../../value';
import { compareValues, type Evaluator } from '../evaluator';
import type { FunctionCall } from '../function-definition';
import { matchReference, readReference, readStructuredReference } from '../lexer';
import { Operand } from '../operand';
import { firstMatch } from '../portable-regex';
import { Area, Reference } from '../reference';
import { ValueArray } from '../value-array';
import { arrayOrValue, isArray, numberArgument, scalarArgument, truthOf, unlifted } from './shared';

/** What a search answered: the position found, or none; or the Operand an argument failed as. */
interface SearchOutcome {
    readonly found?: number | undefined;
    readonly failure?: Operand | undefined;
}

/** A Value not blank, with its position along a vector. */
interface Candidate {
    readonly index: number;
    readonly value: Value;
}

/** A range of one row or one column, as XLOOKUP reads it: a rectangle of cells, an array or a Linked Table's column. */
export class Vector {
    readonly source: Operand;
    readonly vertical: boolean;
    readonly length: number;

    constructor(source: Operand, vertical: boolean, length: number) {
        this.source = source;
        this.vertical = vertical;
        this.length = length;
    }

    static of(range: Operand): Vector | undefined {
        if (range.kind === 'column') return new Vector(range, true, range.columnOf().length);
        if (range.kind === 'array') {
            const array = range.arrayOf();
            if (array.columns === 1) return new Vector(range, true, array.rows);
            if (array.rows === 1) return new Vector(range, false, array.columns);
            return undefined;
        }
        const area = range.area;
        if (area.columns === 1) return new Vector(range, true, area.rows);
        if (area.rows === 1) return new Vector(range, false, area.columns);
        return undefined;
    }

    /** The Values that are not blank, with their positions along the vector, in order. */
    candidates(): Candidate[] {
        const found: Candidate[] = [];
        if (this.source.kind === 'column') {
            const column = this.source.columnOf();
            column.forEach((value, index) => {
                if (value !== undefined) found.push({ index, value });
            });
            return found;
        }
        if (this.source.kind === 'array') {
            const array = this.source.arrayOf();
            for (let i = 0; i < this.length; i++) {
                const value = this.vertical ? array.at(i, 0) : array.at(0, i);
                if (value !== undefined) found.push({ index: i, value });
            }
            return found;
        }
        const area = this.source.area;
        const cells = this.source.cellsOf();
        for (const address of cells.nonBlankIn(area)) {
            const value = cells.read(address);
            if (value === undefined) throw new Error(`Internal: ${address.toString()} was listed as not blank.`);
            found.push({ index: this.vertical ? address.row - area.row1 : address.column - area.column1, value });
        }
        return found;
    }

    /** The first blank position in search order, from the end when `fromEnd`; `undefined` when none is blank. */
    firstBlank(fromEnd: boolean): number | undefined {
        const filled = new Set(this.candidates().map(c => c.index));
        if (filled.size === this.length) return undefined;
        for (let k = 0; k < this.length; k++) {
            const index = fromEnd ? this.length - 1 - k : k;
            if (!filled.has(index)) return index;
        }
        return undefined;
    }

    /** The item at a position: a cell, read as a Reference, or a Linked Table's or an array's Value. */
    itemAt(index: number): Operand {
        if (this.source.kind === 'column') return Operand.ofScalar(this.source.columnOf()[index]);
        if (this.source.kind === 'array') {
            const array = this.source.arrayOf();
            return Operand.ofScalar(this.vertical ? array.at(index, 0) : array.at(0, index));
        }
        const area = this.source.area;
        const row = this.vertical ? area.row1 + index : area.row1;
        const column = this.vertical ? area.column1 : area.column1 + index;
        return this.source.sameSheet(new Area(row, column, row, column));
    }
}

/** Rows `r1`..`r2` and columns `c1`..`c2` of an array, from 0. */
export function slice(array: ValueArray, r1: number, r2: number, c1: number, c2: number): ValueArray {
    const result = new ValueArray(r2 - r1 + 1, c2 - c1 + 1);
    for (let r = r1; r <= r2; r++) {
        for (let c = c1; c <= c2; c++) result.set(r - r1, c - c1, array.at(r, c));
    }
    return result;
}

export function xLookup(call: FunctionCall): Operand {
    // The lookup value's own refusal and Error Value come first, before the arrays are looked at.
    const lookupOperand = call.operand(0);
    if (isArray(lookupOperand)) return unlifted(call);
    const lookup = call.evaluator.scalarOf(lookupOperand);
    if (lookup?.isError === true) return Operand.of(lookup);

    const lookupArray = arrayOrValue(call.operand(1));
    // A return array that holds a kept blank (ADR-0125) is answered where the blank is not what is returned.
    const returnArray = arrayOrValue(call.keepingBlanks(2));
    if (returnArray.keptBlank && returnArray.kind !== 'array') return call.evaluator.refuse();
    if (!lookupArray.isRange) return lookupArray.isError ? lookupArray : Operand.ofError('#VALUE!');
    if (!returnArray.isRange) return returnArray.isError ? returnArray : Operand.ofError('#VALUE!');
    // A range of one row or one column; a Linked Table's column runs down.
    const vector = Vector.of(lookupArray);
    if (vector === undefined) return Operand.ofError('#VALUE!');
    // The return array lies along the lookup array. Several cells across it return the whole row (or column) found, which spills (ADR-0125).
    let along: number;
    let across: number;
    if (returnArray.kind === 'area') [along, across] = vector.vertical ? [returnArray.area.rows, returnArray.area.columns] : [returnArray.area.columns, returnArray.area.rows];
    else if (returnArray.kind === 'array') {
        const array = returnArray.arrayOf();
        [along, across] = vector.vertical ? [array.rows, array.columns] : [array.columns, array.rows];
    } else {
        const length = returnArray.columnOf().length;
        [along, across] = vector.vertical ? [length, 1] : [1, length];
    }
    if (along !== vector.length) return Operand.ofError('#VALUE!');
    const outcome = search(call, vector, 4);
    if (outcome.failure !== undefined) return outcome.failure;
    const index = outcome.found;
    if (index === undefined) return call.has(3) ? call.operand(3) : Operand.ofError('#N/A');
    if (across === 1) {
        const returnVector = Vector.of(returnArray);
        if (returnVector === undefined) throw new Error('Internal: a return array one across is a vector.');
        const item = returnVector.itemAt(index);
        return returnArray.keptBlank && item.kind === 'scalar' && item.scalar === undefined ? call.evaluator.refuse() : item;
    }
    if (returnArray.kind === 'area') {
        const area = returnArray.area;
        return returnArray.sameSheet(vector.vertical
            ? new Area(area.row1 + index, area.column1, area.row1 + index, area.column2)
            : new Area(area.row1, area.column1 + index, area.row2, area.column1 + index));
    }
    const array = returnArray.arrayOf();
    const sliced = vector.vertical ? slice(array, index, index, 0, array.columns - 1) : slice(array, 0, array.rows - 1, index, index);
    return returnArray.keptBlank && sliced.all().includes(undefined) ? call.evaluator.refuse() : Operand.ofArray(sliced);
}

/**
 * XLOOKUP's and XMATCH's search of `vector` for argument 0, with `match_mode` and `search_mode` at `modesAt` and
 * the one after. A mode outside Excel's list is Excel's own `#VALUE!` (XLOOKUP-058). So is the regular
 * expression mode under a binary search (XLOOKUP-104). Refused: a binary search over data that is not sorted as
 * the mode says (`binarySearch`), and a regular expression `portable-regex` does not take, or a text it would
 * read otherwise than PCRE2.
 */
function search(call: FunctionCall, vector: Vector, modesAt: number): SearchOutcome {
    const evaluator = call.evaluator;
    const lookupOperand = call.operand(0);
    if (isArray(lookupOperand)) return { failure: unlifted(call) };
    const lookup = evaluator.scalarOf(lookupOperand);
    if (lookup?.isError === true) return { failure: Operand.of(lookup) };

    let matchMode = 0;
    if (call.has(modesAt)) {
        const mode = numberArgument(call, modesAt);
        if (mode instanceof Operand) return { failure: mode };
        matchMode = Math.trunc(mode);
        if (matchMode !== 0 && matchMode !== -1 && matchMode !== 1 && matchMode !== 2 && matchMode !== 3) return { failure: Operand.ofError('#VALUE!') };
    }
    let searchMode = 1;
    if (call.has(modesAt + 1)) {
        const mode = numberArgument(call, modesAt + 1);
        if (mode instanceof Operand) return { failure: mode };
        searchMode = Math.trunc(mode);
        if (searchMode !== 1 && searchMode !== -1 && searchMode !== 2 && searchMode !== -2) return { failure: Operand.ofError('#VALUE!') };
    }

    if (searchMode === 2 || searchMode === -2) {
        if (matchMode === 3) return { failure: Operand.ofError('#VALUE!') };
        const binary = binarySearch(vector, lookup, matchMode, searchMode === 2);
        return binary === undefined ? { failure: evaluator.refuse() } : { found: binary.found };
    }
    if (lookup !== undefined) {
        let candidates = vector.candidates();
        if (searchMode === -1) candidates = candidates.toReversed();
        if (matchMode === 3) {
            // Numbers and booleans are matched by their text; Error Values match nothing.
            const texts = candidates.filter(c => !c.value.isError).map(c => ({ index: c.index, text: evaluator.toText(c.value) }));
            const matched = firstMatch(evaluator.toText(lookup), texts);
            return matched === undefined ? { failure: evaluator.refuse() } : { found: matched.found };
        }
        switch (matchMode) {
            case 0: return { found: firstExact(candidates, lookup) };
            case 2: return { found: firstWildcard(candidates, lookup) };
            default: return { found: nearest(candidates, lookup, matchMode) };
        }
    }
    // A blank lookup value matches a blank cell, as Excel was observed to (XLOOKUP-067).
    if (matchMode === 0) return { found: vector.firstBlank(searchMode === -1) };
    return {};
}

/**
 * XLOOKUP's binary search (`search_mode` 2 and −2), admitted only over a lookup array that is sorted as the mode
 * says (ADR-0047). `undefined` is a refusal: the array is not sorted as the mode says; it holds blanks, Error
 * Values, or values of more than one kind; the lookup value is blank or of another kind; text holds anything but
 * ASCII letters, digits and spaces; or the match mode is the wildcard one. Of a run of equal keys, the one Excel
 * was observed to return (Part A item 9): an equal key is the first ascending and the last descending; a nearest
 * one is the end of its run nearer the lookup value.
 */
function binarySearch(vector: Vector, lookup: Value | undefined, matchMode: number, ascending: boolean): { found: number | undefined } | undefined {
    if (matchMode === 2 || matchMode === 3 || lookup === undefined || lookup.isError || !orderable(lookup)) return undefined;
    const candidates = vector.candidates();
    if (candidates.length !== vector.length) return undefined;
    for (const { value } of candidates) {
        if (value.kind !== lookup.kind || !orderable(value)) return undefined;
    }
    for (let i = 1; i < candidates.length; i++) {
        const order = compareValues(elementAt(candidates, i - 1).value, elementAt(candidates, i).value);
        if (ascending ? order > 0 : order < 0) return undefined;
    }

    // The value that matches: the wanted one, or failing that the nearest smaller (−1) or larger (1).
    let match: Value | undefined;
    for (const { value } of candidates) {
        const order = compareValues(value, lookup);
        if (order === 0) {
            match = value;
            break;
        }
        if (matchMode === -1 && order < 0 && (match === undefined || compareValues(value, match) > 0)) match = value;
        if (matchMode === 1 && order > 0 && (match === undefined || compareValues(value, match) < 0)) match = value;
    }
    if (match === undefined || (matchMode === 0 && compareValues(match, lookup) !== 0)) return { found: undefined };
    const first = compareValues(match, lookup) === 0 ? ascending : matchMode === 1 ? ascending : !ascending;
    let found: number | undefined;
    for (const { index, value } of candidates) {
        if (compareValues(value, match) !== 0) continue;
        found = index;
        if (first) break;
    }
    return { found };
}

function orderable(value: Value): boolean {
    if (value.kind === 'number' || value.kind === 'boolean') return true;
    return value.kind === 'text' && onlyAsciiLettersDigitsSpaces(value.text);
}

function onlyAsciiLettersDigitsSpaces(text: string): boolean {
    for (let i = 0; i < text.length; i++) {
        const c = text.charCodeAt(i);
        if (!isAsciiLetterOrDigit(c) && c !== 0x20) return false;
    }
    return true;
}

function sameKindEqual(candidate: Value, wanted: Value): boolean {
    return candidate.kind === wanted.kind && candidate.kind !== 'error' && compareValues(candidate, wanted) === 0;
}

function firstExact(candidates: readonly Candidate[], wanted: Value): number | undefined {
    for (const { index, value } of candidates) {
        if (sameKindEqual(value, wanted)) return index;
    }
    return undefined;
}

function firstWildcard(candidates: readonly Candidate[], wanted: Value): number | undefined {
    if (wanted.kind !== 'text') return firstExact(candidates, wanted);
    for (const { index, value } of candidates) {
        if (value.kind === 'text' && wildcardMatches(wanted.text, value.text)) return index;
    }
    return undefined;
}

/**
 * An exact match first; otherwise the nearest smaller (mode −1) or larger (mode 1) value, the first such in
 * search order. Values of another kind take part in Excel's order across kinds — numbers before text, text
 * before booleans (XLOOKUP-071). Error Values take no part.
 */
function nearest(candidates: readonly Candidate[], wanted: Value, mode: number): number | undefined {
    let best: number | undefined;
    let bestValue: Value | undefined;
    for (const { index, value } of candidates) {
        if (value.kind === 'error') continue;
        const order = compareValues(value, wanted);
        if (order === 0) return index;
        if (mode === -1 ? order > 0 : order < 0) continue;
        if (bestValue === undefined || (mode === -1 ? compareValues(value, bestValue) > 0 : compareValues(value, bestValue) < 0)) {
            best = index;
            bestValue = value;
        }
    }
    return best;
}

/** Excel's wildcards: `*` any run, `?` one character, `~` escapes the next; without regard to case. */
export function wildcardMatches(pattern: string, text: string): boolean {
    return wildcardAt(pattern, 0, text, 0);
}

function wildcardAt(pattern: string, from: number, text: string, at: number): boolean {
    let p = from;
    let t = at;
    while (p < pattern.length) {
        let c = pattern.charCodeAt(p);
        if (c === 0x2A) { // *
            while (p < pattern.length && pattern.charCodeAt(p) === 0x2A) p++;
            if (p === pattern.length) return true;
            for (let start = t; start <= text.length; start++) {
                if (wildcardAt(pattern, p, text, start)) return true;
            }
            return false;
        }
        if (t >= text.length) return false;
        if (c === 0x3F) { // ?
            p++;
            t++;
            continue;
        }
        const next = pattern.charCodeAt(p + 1);
        if (c === 0x7E && p + 1 < pattern.length && (next === 0x2A || next === 0x3F || next === 0x7E)) {
            c = next;
            p++;
        }
        if (upperInvariant(c) !== upperInvariant(text.charCodeAt(t))) return false;
        p++;
        t++;
    }
    return t === text.length;
}

// ---- INDEX, XMATCH, CHOOSE, ROW, COLUMN, ROWS, COLUMNS, OFFSET, INDIRECT -----------------------------------

/**
 * INDEX: the cell at `row_num` and `column_num` of the range, both counted from 1 and truncated. Over a range of
 * one row given `row_num` alone, that number runs along the row. Below 0 is `#VALUE!`, and past the range's edge
 * is `#REF!`. A row or a column of 0 names the whole column or row, which spills (ADR-0125). Over a Reference of
 * several rows and several columns, `column_num` cannot be left out (INDEX-012). A kept blank stays marked in
 * what `INDEX` picks of it (SORT-022, UNIQUE-018).
 */
export function indexFunction(call: FunctionCall): Operand {
    const source = call.keepingBlanks(0);
    const picked = pick(call, source);
    return source.keptBlank ? picked.withKeptBlank() : picked;
}

function pick(call: FunctionCall, source: Operand): Operand {
    if (source.isError) return source;
    let rows = 1;
    let columns = 1;
    if (source.kind === 'area') [rows, columns] = [source.area.rows, source.area.columns];
    else if (source.kind === 'column') rows = source.columnOf().length;
    else if (source.kind === 'array') [rows, columns] = [source.arrayOf().rows, source.arrayOf().columns];

    let row = 0;
    if (call.has(1)) {
        const given = numberArgument(call, 1);
        if (given instanceof Operand) return given;
        row = given;
    }
    let column = 0;
    if (call.count < 3) {
        if (rows === 1 && columns > 1) [row, column] = [1, row];
    } else if (call.has(2)) {
        const given = numberArgument(call, 2);
        if (given instanceof Operand) return given;
        column = given;
    }
    row = Math.trunc(row);
    column = Math.trunc(column);
    if (row < 0 || column < 0) return Operand.ofError('#VALUE!');
    if (call.count < 3 && source.isReference && rows > 1 && columns > 1) return Operand.ofError('#REF!');
    if (row > rows || column > columns) return Operand.ofError('#REF!');
    if ((row === 0 && rows > 1) || (column === 0 && columns > 1)) {
        // The whole row, column or range: rows r1..r2 and columns c1..c2, from 0.
        const [r1, r2] = row === 0 ? [0, rows - 1] : [row - 1, row - 1];
        const [c1, c2] = column === 0 ? [0, columns - 1] : [column - 1, column - 1];
        switch (source.kind) {
            case 'area': {
                const area = source.area;
                return source.sameSheet(new Area(area.row1 + r1, area.column1 + c1, area.row1 + r2, area.column1 + c2));
            }
            case 'column': return Operand.ofArray(slice(call.evaluator.toArray(source), r1, r2, c1, c2));
            case 'array': return Operand.ofArray(slice(source.arrayOf(), r1, r2, c1, c2));
            case 'scalar':
            case 'missing':
            case 'threeD':
                break;
            default: throw new Error(`Internal: ${String(source.kind)} is no kind of operand.`);
        }
    }
    const r = Math.max(row, 1) - 1;
    const c = Math.max(column, 1) - 1;
    switch (source.kind) {
        case 'area': return source.sameSheet(new Area(source.area.row1 + r, source.area.column1 + c, source.area.row1 + r, source.area.column1 + c));
        case 'column': return Operand.ofScalar(source.columnOf()[r]);
        case 'array': return Operand.ofScalar(source.arrayOf().at(r, c));
        case 'scalar':
        case 'missing':
        case 'threeD':
            return source;
        default: throw new Error(`Internal: ${String(source.kind)} is no kind of operand.`);
    }
}

/** XMATCH: the position, from 1, at which XLOOKUP's search finds the lookup value; `#N/A` when nothing matches. */
export function xMatch(call: FunctionCall): Operand {
    const lookupOperand = call.operand(0);
    if (isArray(lookupOperand)) return unlifted(call);
    const lookup = call.evaluator.scalarOf(lookupOperand);
    if (lookup?.isError === true) return Operand.of(lookup);
    const lookupArray = arrayOrValue(call.operand(1));
    if (!lookupArray.isRange) return lookupArray.isError ? lookupArray : Operand.ofError('#VALUE!');
    const vector = Vector.of(lookupArray);
    if (vector === undefined) return Operand.ofError('#VALUE!');
    const outcome = search(call, vector, 2);
    if (outcome.failure !== undefined) return outcome.failure;
    return outcome.found === undefined ? Operand.ofError('#N/A') : Operand.of(Value.fromNumber(outcome.found + 1));
}

/** CHOOSE: the value at `index_num`, from 1, truncated; below 1 or past the values, `#VALUE!`. Only the chosen value is evaluated, and a Reference stays a Reference. */
export function choose(call: FunctionCall): Operand {
    const given = numberArgument(call, 0);
    if (given instanceof Operand) return given;
    const at = Math.trunc(given);
    if (at < 1 || at > call.count - 1) return Operand.ofError('#VALUE!');
    return call.has(at) ? call.operand(at) : Operand.blank;
}

export function rowFunction(call: FunctionCall): Operand {
    return place(call, true);
}

export function columnFunction(call: FunctionCall): Operand {
    return place(call, false);
}

/**
 * ROW and COLUMN: the number, from 1, of the Reference's row or column, or of the Formula's own cell when it is
 * left out. A Reference of several rows to ROW gives each row's number, down a column, and of several columns to
 * COLUMN each column's, along a row; it spills (ADR-0125). A Linked Table's column has no place: `#VALUE!`.
 */
function place(call: FunctionCall, ofRow: boolean): Operand {
    if (!call.has(0)) {
        const self = call.evaluator.self;
        if (self === undefined) return Operand.ofError('#VALUE!');
        return Operand.of(Value.fromNumber((ofRow ? self.row : self.column) + 1));
    }
    const reference = call.operand(0);
    if (reference.kind !== 'area') return reference.isError ? reference : Operand.ofError('#VALUE!');
    const area = reference.area;
    const count = ofRow ? area.rows : area.columns;
    const places = ofRow ? new ValueArray(count, 1) : new ValueArray(1, count);
    for (let k = 0; k < count; k++) {
        const number = Value.fromNumber((ofRow ? area.row1 : area.column1) + k + 1);
        if (ofRow) places.set(k, 0, number);
        else places.set(0, k, number);
    }
    return Operand.ofArray(places);
}

export function rowsFunction(call: FunctionCall): Operand {
    return extent(call, true);
}

export function columnsFunction(call: FunctionCall): Operand {
    return extent(call, false);
}

/** ROWS and COLUMNS: how many rows or columns the range spans, read for its extent alone (UNIQUE-011). */
function extent(call: FunctionCall, ofRows: boolean): Operand {
    const operand = call.measured(0);
    if (operand.isError) return operand;
    let count = 1;
    if (operand.kind === 'area') count = ofRows ? operand.area.rows : operand.area.columns;
    else if (operand.kind === 'column') count = ofRows ? operand.columnOf().length : 1;
    else if (operand.kind === 'array') count = ofRows ? operand.arrayOf().rows : operand.arrayOf().columns;
    return Operand.of(Value.fromNumber(count));
}

/**
 * OFFSET (ADR-0124, a volatile function): the Reference `rows` below and `cols` right of `reference`, `height`
 * rows by `width` columns, each truncated and the size the reference's own when left out. A size below 1, or a
 * Reference past the Sheet's edge, is `#REF!`; a Linked Table's column has no place to move from: `#VALUE!`.
 */
export function offset(call: FunctionCall): Operand {
    const reference = call.operand(0);
    if (reference.kind !== 'area') return reference.isError ? reference : Operand.ofError('#VALUE!');
    const area = reference.area;
    const rowsGiven = numberArgument(call, 1);
    if (rowsGiven instanceof Operand) return rowsGiven;
    const columnsGiven = numberArgument(call, 2);
    if (columnsGiven instanceof Operand) return columnsGiven;
    let height = area.rows;
    let width = area.columns;
    if (call.has(3)) {
        const given = numberArgument(call, 3);
        if (given instanceof Operand) return given;
        height = given;
    }
    if (call.has(4)) {
        const given = numberArgument(call, 4);
        if (given instanceof Operand) return given;
        width = given;
    }
    height = Math.trunc(height);
    width = Math.trunc(width);
    if (height < 1 || width < 1) return Operand.ofError('#REF!');
    const top = area.row1 + Math.trunc(rowsGiven);
    const left = area.column1 + Math.trunc(columnsGiven);
    const bottom = top + height - 1;
    const right = left + width - 1;
    if (top < 0 || left < 0 || bottom >= rowCount || right >= columnCount) return Operand.ofError('#REF!');
    return reference.sameSheet(new Area(top, left, bottom, right));
}

/**
 * INDIRECT (ADR-0124's note of 2026-10-10, a volatile function): the Reference its text names, in A1 style or
 * R1C1 style (`a1` FALSE, 0, a blank or left empty), with a Linked Table's text and a Spill Range's read as the
 * typed ones are. Text that names no place is `#REF!`; the structured forms a Formula does not take and a
 * declared Linked Table's bare name are refused (INDIRECT-003 to INDIRECT-105, as the C# engine's remarks list
 * them).
 */
export function indirect(call: FunctionCall): Operand {
    const value = scalarArgument(call, 0);
    if (value instanceof Operand) return value;
    // a1: TRUE, or left out, is A1 style; FALSE, 0, a blank or an empty argument is R1C1's.
    let isA1 = true;
    if (call.count > 1) {
        if (!call.has(1)) isA1 = false;
        else {
            const style = scalarArgument(call, 1);
            if (style instanceof Operand) return style;
            if (style === undefined) isA1 = false;
            else {
                const truth = truthOf(style);
                if (truth === undefined) return Operand.ofError('#VALUE!');
                isA1 = truth;
            }
        }
    }
    const text = call.evaluator.toText(value);
    return isA1 ? indirectA1(call, text) : indirectRowColumn(call, text);
}

/** INDIRECT's A1-style text: a Reference as a Formula writes one, `A1#`, or a Linked Table's text; a `#` after a range, a row or a column, or inside a join, `#REF!`. */
function indirectA1(call: FunctionCall, text: string): Operand {
    const cells = call.evaluator.cells;
    const table = indirectTableText(call, text);
    if (table !== undefined) return table;
    // A # inside a join, after its first part (A1#:A2, A1#:A2#, 1#:2), names no place (INDIRECT-098).
    if (hashInsideJoin(text) !== undefined) return Operand.ofError('#REF!');
    // A column past XFD, or a row past 1048576, names no place (INDIRECT-017).
    const read = readReference(text, 0);
    if (read === undefined) return Operand.ofError('#REF!');
    const { reference, length } = read;
    // A Sheet's name the Workbook does not have names no place (INDIRECT-008); a name it has reads that Sheet (ADR-0331).
    const target = cells.resolve(reference.sheetName);
    if (target === undefined) return Operand.ofError('#REF!');
    if (length === text.length - 1 && text.charAt(length) === '#') {
        // A1#: the Spill Range of the Formula in A1, read as the typed one is (INDIRECT-026, 042; ADR-0125).
        if (reference.shape === 'cell') return target.spill(new CellAddress(reference.row1, reference.column1));
        // A # after a range, a whole row or a whole column names no place (INDIRECT-092, 096).
        return Operand.ofError('#REF!');
    }
    // Text that goes on past the Reference is no Reference (INDIRECT-021, generalised).
    if (length !== text.length) return Operand.ofError('#REF!');
    return Operand.ofArea(reference.area, target);
}

/**
 * The join A1 text with a `#` inside it would be with that `#` taken out — a range, whole rows or whole columns,
 * followed by nothing or by one more `#` — where the `#` stands right after the join's first part; otherwise
 * `undefined`.
 */
function hashInsideJoin(text: string): Reference | undefined {
    for (let hash = text.indexOf('#:'); hash >= 0; hash = text.indexOf('#:', hash + 1)) {
        const join = text.slice(0, hash) + text.slice(hash + 1);
        const match = matchReference(join, 0);
        if (match === undefined) continue;
        // The : the # stood before is the join's, not one inside a quoted Sheet name.
        const qualifier = match.groups['sheet'];
        if (qualifier !== undefined && hash < qualifier.index + qualifier.value.length) continue;
        if (match.length !== join.length && (match.length !== join.length - 1 || join.charAt(join.length - 1) !== '#')) continue;
        const read = readReference(join, 0);
        if (read !== undefined && read.reference.shape !== 'cell') return read.reference;
    }
    return undefined;
}

const structuredText = /^[A-Za-z_\\][A-Za-z0-9_.]*\[/;

/**
 * A Linked Table's text, read alike whatever `a1` says (INDIRECT-025, INDIRECT-061), or `undefined` for text that
 * is none: `Table[Column]` read as the typed one is (ADR-0049); any other text a structured reference begins as,
 * refused; and a declared table's bare name, refused by decision (ADR-0049's rule 2; 2026-10-12).
 */
function indirectTableText(call: FunctionCall, text: string): Operand | undefined {
    const cells = call.evaluator.cells;
    const structured = readStructuredReference(text, 0);
    if (structured !== undefined && structured.length === text.length) {
        const tableColumn = cells.tableColumn(structured.table, structured.column);
        return tableColumn.isError && tableColumn.scalar?.error === '#NAME?' ? Operand.ofError('#REF!') : tableColumn;
    }
    if (structuredText.test(text)) return call.evaluator.refuse();
    if (cells.isLinkedTable(text)) return call.evaluator.refuse();
    return undefined;
}

const axis = String.raw`(?:[0-9]+|\[[+-]?[0-9]+\])`;
const partPattern = String.raw`[Rr]${axis}?(?:[Cc]${axis}?)?|[Cc]${axis}?`;

/**
 * R1C1 notation, one part, two or three separated by `:`, after an optional Sheet qualifier. A `#` before the
 * first `:` is matched, as `inside`, so that it can be refused.
 */
const rowColumnText = new RegExp(
    String.raw`^(?:(?<sheet>'(?:[^']|'')+'|[A-Za-z_][A-Za-z0-9_.]*)!)?(?<first>${partPattern})(?:(?<inside>#)?:(?<second>${partPattern})(?::(?<third>${partPattern}))?)?$`);

/** One R1C1 part: an R with a row or none, a C with a column or none, or both. */
const rowColumnPartText = new RegExp(String.raw`^(?:(?<r>[Rr])(?<row>${axis})?)?(?:(?<c>[Cc])(?<column>${axis})?)?$`);

/**
 * INDIRECT's R1C1-style text (ADR-0124's notes of 2026-10-12, 2026-10-14 and 2026-10-18): R and C each with a
 * number counted from 1, an offset in brackets from the Formula's own cell, which wraps round past the Sheet's
 * edge, or no number; parts joined by `:` left to right (`RowColumnSpan.join`); one cell followed by `#`, its
 * Spill Range; anything else that is not R1C1 notation `#REF!`.
 */
function indirectRowColumn(call: FunctionCall, text: string): Operand {
    const spilled = text.endsWith('#');
    const match = rowColumnText.exec(spilled ? text.slice(0, -1) : text);
    if (match === null) return indirectTableText(call, text) ?? Operand.ofError('#REF!');
    const groups = match.groups;
    if (groups === undefined) throw new Error('Internal: a pattern with named groups matched without them.');
    const raw = groups['sheet'];
    const sheet = raw === undefined ? undefined : raw.startsWith("'") ? raw.slice(1, -1).replaceAll("''", "'") : raw;
    // A Sheet's name the Workbook does not have names no place (INDIRECT-093; ADR-0331).
    const target = call.evaluator.cells.resolve(sheet);
    if (target === undefined) return Operand.ofError('#REF!');
    const inside = groups['inside'] !== undefined;
    const parts = [readPart(defined(groups['first']))];
    if (groups['second'] !== undefined) parts.push(readPart(groups['second']));
    if (groups['third'] !== undefined) parts.push(readPart(groups['third']));
    // A # after one cell is its Spill Range (INDIRECT-060); after a range, a row or a column, or inside a join, #REF!.
    if (inside || (spilled && (parts.length > 1 || !isCell(elementAt(parts, 0))))) return Operand.ofError('#REF!');
    // An offset counts from the Formula's own cell; a Formula evaluated in no cell has none, as ROW() has none.
    let own = new CellAddress(0, 0);
    if (parts.some(isRelative)) {
        const self = call.evaluator.self;
        if (self === undefined) return Operand.ofError('#VALUE!');
        own = self;
    }
    const spans: RowColumnSpan[] = [];
    for (const each of parts) {
        const span = placeSpan(each, own);
        if (span === undefined) return Operand.ofError('#REF!');
        spans.push(span);
    }
    let joined = elementAt(spans, 0);
    for (let i = 1; i < spans.length; i++) {
        const next = joinSpans(joined, elementAt(spans, i));
        if (next === undefined) return Operand.ofError('#REF!');
        joined = next;
    }
    if (spilled) return target.spill(new CellAddress(joined.rows?.low ?? 0, joined.columns?.low ?? 0)); // oxlint-disable-line ex-grid/no-stand-in-default -- a whole row or column starts at 0
    const [row1, row2] = joined.rows === undefined ? [0, rowCount - 1] : [joined.rows.low, joined.rows.high];
    const [column1, column2] = joined.columns === undefined ? [0, columnCount - 1] : [joined.columns.low, joined.columns.high];
    const reference = Reference.rectangle(sheet, row1, column1, row2, column2, true, true, true, true);
    return Operand.ofArea(reference.area, target);
}

function defined(text: string | undefined): string {
    if (text === undefined) throw new Error('Internal: a group the pattern requires did not match.');
    return text;
}

interface Span {
    readonly low: number;
    readonly high: number;
}

/** A part of R1C1 text placed on the Sheet, or several joined: the rows and the columns it spans, each `undefined` where it spans the whole Sheet. */
interface RowColumnSpan {
    readonly rows: Span | undefined;
    readonly columns: Span | undefined;
}

/** One axis of an R1C1 part: a row or a column counted from 1, or an offset from the Formula's own. */
interface RowColumnAxis {
    readonly relative: boolean;
    readonly number: number;
}

/** One part of R1C1 text: its row and its column, each `undefined` where the part has no R or no C. */
interface RowColumnPart {
    readonly row: RowColumnAxis | undefined;
    readonly column: RowColumnAxis | undefined;
}

function isRelative(part: RowColumnPart): boolean {
    return part.row?.relative === true || part.column?.relative === true;
}

function isCell(part: RowColumnPart): boolean {
    return part.row !== undefined && part.column !== undefined;
}

function readPart(text: string): RowColumnPart {
    const match = rowColumnPartText.exec(text);
    const groups = match?.groups;
    if (groups === undefined) throw new Error('Internal: an R1C1 part the notation matched does not read.');
    return {
        row: groups['r'] === undefined ? undefined : readAxis(groups['row'] ?? ''), // oxlint-disable-line ex-grid/no-stand-in-default -- an R with nothing after it is the own row
        column: groups['c'] === undefined ? undefined : readAxis(groups['column'] ?? ''), // oxlint-disable-line ex-grid/no-stand-in-default -- a C with nothing after it is the own column
    };
}

/**
 * `3` and `03` are absolute (INDIRECT-059); `[-1]`, `[+1]`, `[0]` and `[-0]` are offsets (INDIRECT-063 to 065);
 * nothing written is an offset of 0 (INDIRECT-044, 066). A number too long for any Sheet is past its edge.
 */
function readAxis(written: string): RowColumnAxis {
    if (written.length === 0) return { relative: true, number: 0 };
    const relative = written.startsWith('[');
    let digits = relative ? written.slice(1, -1) : written;
    const negative = digits.startsWith('-');
    if (negative || digits.startsWith('+')) digits = digits.slice(1);
    digits = digits.replace(/^0+/, '');
    const number = digits.length === 0 ? 0 : digits.length > 9 ? 1_000_000_000 : Number(digits);
    return { relative, number: negative ? -number : number };
}

/** The row or column from 0, or `undefined` where a number names no place; an offset past the edge wraps round (INDIRECT-052, 082 to 084). */
function placeAxis(axisRead: RowColumnAxis, own: number, count: number): number | undefined {
    if (axisRead.relative) return Math.abs(axisRead.number) < count ? (own + axisRead.number + count) % count : undefined;
    return axisRead.number >= 1 && axisRead.number <= count ? axisRead.number - 1 : undefined;
}

function placeSpan(part: RowColumnPart, own: CellAddress): RowColumnSpan | undefined {
    let rowsSpan: Span | undefined;
    let columnsSpan: Span | undefined;
    if (part.row !== undefined) {
        const r = placeAxis(part.row, own.row, rowCount);
        if (r === undefined) return undefined;
        rowsSpan = { low: r, high: r };
    }
    if (part.column !== undefined) {
        const c = placeAxis(part.column, own.column, columnCount);
        if (c === undefined) return undefined;
        columnsSpan = { low: c, high: c };
    }
    return { rows: rowsSpan, columns: columnsSpan };
}

/**
 * Two spans joined by `:`: two of the same kind, the rectangle they span; cells then a whole column, the cells'
 * rows across the columns they span (INDIRECT-075, 101, 103); a whole row then cells, the rows they span down
 * the cells' columns (INDIRECT-076, 102). Any other two name no place (INDIRECT-058, 062, 077, 104).
 */
function joinSpans(firstGiven: RowColumnSpan, secondGiven: RowColumnSpan): RowColumnSpan | undefined {
    let first = firstGiven;
    let second = secondGiven;
    const firstCells = first.rows !== undefined && first.columns !== undefined;
    const secondCells = second.rows !== undefined && second.columns !== undefined;
    if (firstCells && second.rows === undefined && second.columns !== undefined) second = { rows: first.rows, columns: second.columns };
    else if (secondCells && first.rows !== undefined && first.columns === undefined) first = { rows: first.rows, columns: second.columns };
    if ((first.rows === undefined) !== (second.rows === undefined) || (first.columns === undefined) !== (second.columns === undefined)) return undefined;
    return { rows: hull(first.rows, second.rows), columns: hull(first.columns, second.columns) };
}

function hull(a: Span | undefined, b: Span | undefined): Span | undefined {
    return a !== undefined && b !== undefined ? { low: Math.min(a.low, b.low), high: Math.max(a.high, b.high) } : undefined;
}

// ---- VLOOKUP, HLOOKUP, MATCH ---------------------------------------------------------------------------------

export function vLookup(call: FunctionCall): Operand {
    return legacyLookup(call, true);
}

export function hLookup(call: FunctionCall): Operand {
    return legacyLookup(call, false);
}

/**
 * VLOOKUP and HLOOKUP: first exact match, or the last smaller/equal key in an ascending table. Unlike INDEX, their
 * result is a Value, and a blank target becomes numeric zero. A table that holds a kept blank (ADR-0125) is
 * answered where the blank is never read.
 */
function legacyLookup(call: FunctionCall, vertical: boolean): Operand {
    const lookup = scalarArgument(call, 0);
    if (lookup instanceof Operand) return lookup;
    const table = arrayOrValue(call.keepingBlanks(1));
    if (table.keptBlank && table.kind !== 'array') return call.evaluator.refuse();
    if (!table.isRange) return table.isError ? table : Operand.ofError('#VALUE!');
    const given = numberArgument(call, 2);
    if (given instanceof Operand) return given;
    const at = Math.trunc(given);
    if (at < 1) return Operand.ofError('#VALUE!');
    let rowsOf: number;
    let columnsOf: number;
    if (table.kind === 'area') [rowsOf, columnsOf] = [table.area.rows, table.area.columns];
    else if (table.kind === 'array') [rowsOf, columnsOf] = [table.arrayOf().rows, table.arrayOf().columns];
    else [rowsOf, columnsOf] = [table.columnOf().length, 1];
    if (at > (vertical ? columnsOf : rowsOf)) return Operand.ofError('#REF!');
    let mode = 1;
    if (call.count > 3) {
        const modeGiven = numberArgument(call, 3);
        if (modeGiven instanceof Operand) return modeGiven;
        mode = modeGiven;
    }
    const keys = legacyTableLine(table, vertical, 0);
    if (table.keptBlank && keys.firstBlank(false) !== undefined) return call.evaluator.refuse();
    const outcome = legacySearch(call.evaluator, keys, lookup, mode === 0 ? 0 : 1);
    if (outcome.failure !== undefined) return outcome.failure;
    if (outcome.found === undefined) return Operand.ofError('#N/A');
    const result = legacyTableLine(table, vertical, at - 1).itemAt(outcome.found);
    if (table.keptBlank && result.kind === 'scalar' && result.scalar === undefined) return call.evaluator.refuse();
    return Operand.of(call.evaluator.scalarOf(result) ?? Value.fromNumber(0));
}

/** The indicated column or row of a table, retaining References and sparse ranges. */
function legacyTableLine(table: Operand, vertical: boolean, at: number): Vector {
    if (table.kind === 'area') {
        const area = table.area;
        const line = vertical
            ? new Area(area.row1, area.column1 + at, area.row2, area.column1 + at)
            : new Area(area.row1 + at, area.column1, area.row1 + at, area.column2);
        return new Vector(table.sameSheet(line), vertical, vertical ? area.rows : area.columns);
    }
    if (table.kind === 'column') {
        return vertical ? new Vector(table, true, table.columnOf().length) : new Vector(Operand.asArray(table.columnOf()[at]), false, 1);
    }
    const array = table.arrayOf();
    const sliced = vertical ? slice(array, 0, array.rows - 1, at, at) : slice(array, at, at, 0, array.columns - 1);
    return new Vector(sliced.isSingle ? Operand.asArray(sliced.at(0, 0)) : Operand.ofArray(sliced), vertical, vertical ? array.rows : array.columns);
}

/** MATCH's position, from 1; descending approximate matches take the first equal key. */
export function matchFunction(call: FunctionCall): Operand {
    const lookup = scalarArgument(call, 0);
    if (lookup instanceof Operand) return lookup;
    const array = arrayOrValue(call.operand(1));
    if (!array.isRange) return array.isError ? array : Operand.ofError('#VALUE!');
    const vector = Vector.of(array);
    if (vector === undefined) return Operand.ofError('#VALUE!');
    let mode = 1;
    if (call.count > 2) {
        const given = numberArgument(call, 2);
        if (given instanceof Operand) return given;
        mode = given;
    }
    const outcome = legacySearch(call.evaluator, vector, lookup, Math.sign(mode));
    if (outcome.failure !== undefined) return outcome.failure;
    return outcome.found === undefined ? Operand.ofError('#N/A') : Operand.of(Value.fromNumber(outcome.found + 1));
}

/**
 * The searches shared by VLOOKUP, HLOOKUP and MATCH. Approximate searches retain ADR-0047's refusal of data that
 * cannot be established as sorted. They never use XLOOKUP's duplicate-key rules.
 */
function legacySearch(evaluator: Evaluator, vector: Vector, lookup: Value | undefined, mode: number): SearchOutcome {
    const wanted = lookup ?? Value.fromNumber(0);
    if (wanted.kind === 'text' && wanted.text.length > 255) return { failure: evaluator.refuse() };
    const candidates = vector.candidates();
    if (mode === 0) return legacyExact(evaluator, candidates, wanted);
    if (candidates.length !== vector.length || candidates.some(c => c.value.isError)) return { failure: evaluator.refuse() };
    const kind = candidates.length === 0 ? wanted.kind : elementAt(candidates, 0).value.kind;
    if (candidates.some(c => c.value.kind !== kind || !orderable(c.value))) return { failure: evaluator.refuse() };
    for (let i = 1; i < candidates.length; i++) {
        const order = compareValues(elementAt(candidates, i - 1).value, elementAt(candidates, i).value);
        if (mode > 0 ? order > 0 : order < 0) return { failure: evaluator.refuse() };
    }
    if (wanted.kind !== kind) return {};
    if (!orderable(wanted)) return { failure: evaluator.refuse() };
    let found: number | undefined;
    for (const { index: at, value } of candidates) {
        const order = compareValues(value, wanted);
        if (mode > 0 ? order > 0 : order < 0) break;
        found = at;
        if (mode < 0 && order === 0) break;
    }
    // MATCH-018 pins the first equal key descending. No observation yet pins which duplicate wins when a
    // descending search finds the next larger key instead.
    if (mode < 0 && found !== undefined && found > 0
        && compareValues(elementAt(candidates, found).value, wanted) !== 0
        && compareValues(elementAt(candidates, found - 1).value, elementAt(candidates, found).value) === 0) {
        return { failure: evaluator.refuse() };
    }
    return { found };
}

function allAscii(text: string): boolean {
    for (let i = 0; i < text.length; i++) {
        if (!isAscii(text.charCodeAt(i))) return false;
    }
    return true;
}

/**
 * Exact legacy matching, including wildcards, is observed for ASCII text only. An absent key with #N/A in the
 * vector is #N/A; whether a later matching key wins over an earlier Error Value is not pinned, and is refused.
 */
function legacyExact(evaluator: Evaluator, candidates: readonly Candidate[], wanted: Value): SearchOutcome {
    if (wanted.kind === 'text' && !allAscii(wanted.text)) return { failure: evaluator.refuse() };
    let errorSeen = false;
    let unobservedError = false;
    for (const { index: at, value } of candidates) {
        if (value.isError) {
            errorSeen = true;
            unobservedError ||= value.error !== '#N/A';
            continue;
        }
        if (value.kind !== wanted.kind) continue;
        if (value.kind === 'text' && !allAscii(value.text)) return { failure: evaluator.refuse() };
        const matches = wanted.kind === 'text' ? wildcardMatches(wanted.text, value.text) : sameKindEqual(value, wanted);
        if (matches) return errorSeen ? { failure: evaluator.refuse() } : { found: at };
    }
    return unobservedError ? { failure: evaluator.refuse() } : {};
}
