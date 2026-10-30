// Splits a Formula's invariant text into tokens: Excel's spelling, with `,` between arguments and `.` as the
// decimal separator in every culture (ADR-0047).
import { columnCount, rowCount, CellAddress } from '../cell-address';
import { parseFormulaNumber } from '../constant-parser';
import { isAscii, isAsciiDigit, isLetter, isWhiteSpace, startsWithAtIgnoreCase, equalsIgnoreCase } from '../dotnet';
import { FormulaSyntaxError } from '../formula-syntax-error';
import { errorValues, type ErrorValue } from '../value';
import { Reference } from './reference';

/** What a token is. */
export type TokenKind =
    | 'number' | 'text' | 'error' | 'reference' | 'name' | 'structuredReference' | 'operator'
    | 'leftParenthesis' | 'rightParenthesis' | 'comma' | 'end';

/** One token of a Formula. */
export interface Token {
    readonly kind: TokenKind;
    readonly position: number;
    readonly text: string;
    readonly afterSpace: boolean;
    /** How many characters of the Formula the token spans, from `position`. */
    readonly length: number;
    readonly number: number;
    readonly reference: Reference | undefined;
    readonly error: ErrorValue | undefined;
    /** For a structured reference: the table's name; `text` is the column's. */
    readonly tableName: string | undefined;
    /** For `#REF!` written after a Sheet qualifier (`Sheet1!#REF!`): the Sheet's name, unquoted. */
    readonly sheetName: string | undefined;
}

function token(kind: TokenKind, position: number, text: string, afterSpace: boolean, parts: Partial<Token> = {}): Token {
    return {
        kind, position, text, afterSpace, length: 0, number: 0, reference: undefined, error: undefined, tableName: undefined, sheetName: undefined,
        ...parts,
    };
}

const sheetPart = String.raw`'(?:[^']|'')+'|[A-Za-z_][A-Za-z0-9_.]*`;
const placePart = String.raw`(?:(?<c1>\$?[A-Za-z]{1,3})(?<r1>\$?[0-9]{1,7})(?::(?<c2>\$?[A-Za-z]{1,3})(?<r2>\$?[0-9]{1,7}))?`
    + String.raw`|(?<cc1>\$?[A-Za-z]{1,3}):(?<cc2>\$?[A-Za-z]{1,3})|(?<rr1>\$?[0-9]{1,7}):(?<rr2>\$?[0-9]{1,7}))(?![A-Za-z0-9_.(\[!:$])`;

/** A Reference: an optional Sheet qualifier, then a cell, a rectangle, whole columns or whole rows. */
const referencePattern = new RegExp(`(?:(?<sheet>${sheetPart})!)?${placePart}`, 'yd');

/**
 * A 3D Reference's span before its `!` (ADR-0331 §7): `Sheet1:Sheet3!`, or quoted whole, `'Q1 Sales:Sheet3'!`,
 * as Excel writes one. A Sheet's name holds no `:`, so the one inside the quotes is the span's.
 */
const sheetSpanPattern = /(?:'(?<quotedFirst>(?:[^':]|'')+):(?<quotedLast>(?:[^':]|'')+)'|(?<first>[A-Za-z_][A-Za-z0-9_.]*):(?<last>[A-Za-z_][A-Za-z0-9_.]*))!/y;

/** `Sheet1!#REF!`: a qualified Reference whose cells were deleted, as Excel writes it. */
const qualifiedRefErrorPattern = new RegExp(`(?<sheet>${sheetPart})!#REF!`, 'iy');

const numberPattern = /(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?/y;

const namePattern = /[A-Za-z_\\][A-Za-z0-9_.]*/y;

const operators = ['<>', '<=', '>=', '=', '<', '>', '+', '-', '*', '/', '^', '&', '%'];

/** The largest number constant a Formula may hold: 9.99999999999999E+307, as Microsoft documents Excel's. */
const largestConstant = 9.99999999999999e307;

function stickyMatch(pattern: RegExp, text: string, at: number): RegExpExecArray | null {
    pattern.lastIndex = at;
    return pattern.exec(text);
}

function groupsOf(match: RegExpExecArray): Record<string, string | undefined> {
    if (match.groups === undefined) throw new Error('Internal: a pattern with named groups matched without them.');
    return match.groups;
}

/**
 * A name that may hold a letter of any script, as a `LET` name may (`名前`, `é`; ADR-0181), read as .NET's
 * `[\p{L}_\\][\p{L}0-9_.]*` reads it: a letter judged per UTF-16 unit. Its length, or 0 where none starts.
 */
function wideNameLength(formula: string, at: number): number {
    const first = formula.charCodeAt(at);
    if (!(isLetter(first) || first === 0x5f || first === 0x5c)) return 0;
    let i = at + 1;
    while (i < formula.length) {
        const c = formula.charCodeAt(i);
        if (!(isLetter(c) || isAsciiDigit(c) || c === 0x5f || c === 0x2e)) break;
        i++;
    }
    return i - at;
}

/** Where the first character outside ASCII stands in `text`, or −1. */
export function firstWide(text: string): number {
    for (let i = 0; i < text.length; i++) {
        if (!isAscii(text.charCodeAt(i))) return i;
    }
    return -1;
}

/**
 * The whitespace a Formula may hold between its tokens: a space and a line break. Excel refuses a tab; a
 * carriage return is taken with its line feed; the other space characters are refused.
 */
export function isFormulaWhitespace(c: string): boolean {
    return c === ' ' || c === '\n' || c === '\r';
}

/** The Formula's tokens from `start`, ending with an `end` token; a Formula that cannot be read is a `FormulaSyntaxError`. */
export function tokenize(formula: string, start: number): Token[] {
    const tokens: Token[] = [];
    try {
        read(formula, start, tokens);
        return tokens;
    } catch (error) {
        const wide = wideNameOutsideLet(tokens);
        if (!(error instanceof FormulaSyntaxError) || wide === undefined) throw error;
        // Before LET was admitted (ADR-0181) the lexer refused a name of another script where it stood, before
        // anything after it; outside a LET it still does.
        const at = wide.position + firstWide(wide.text);
        throw new FormulaSyntaxError(formula, at, `'${formula.charAt(at)}' is not part of the formula syntax ExSheet reads.`);
    }
}

/**
 * The first name of another script than ASCII among `tokens`, when none of them is `LET`: such a name is a
 * `LET` name or nothing (ADR-0181). `undefined` otherwise.
 */
export function wideNameOutsideLet(tokens: readonly Token[]): Token | undefined {
    let wide: Token | undefined;
    for (const t of tokens) {
        if (t.kind !== 'name') continue;
        if (equalsIgnoreCase(t.text, 'LET')) return undefined;
        if (wide === undefined && firstWide(t.text) >= 0) wide = t;
    }
    return wide;
}

function read(formula: string, start: number, tokens: Token[]): void {
    let i = start;
    for (;;) {
        let afterSpace = false;
        while (i < formula.length && isWhiteSpace(formula.charCodeAt(i))) {
            if (!isFormulaWhitespace(formula.charAt(i))) {
                throw new FormulaSyntaxError(formula, i, "a Formula's whitespace is spaces and line breaks; Excel refuses a tab or any other space character.");
            }
            i++;
            afterSpace = true;
        }
        if (i >= formula.length) {
            tokens.push(token('end', i, '', afterSpace));
            return;
        }

        const c = formula.charAt(i);
        if (c === '"') {
            let text = '';
            let j = i + 1;
            for (;;) {
                if (j >= formula.length) throw new FormulaSyntaxError(formula, i, 'the text has no closing quotation mark.');
                if (formula.charAt(j) === '"') {
                    if (j + 1 < formula.length && formula.charAt(j + 1) === '"') {
                        text += '"';
                        j += 2;
                        continue;
                    }
                    break;
                }
                text += formula.charAt(j);
                j++;
            }
            tokens.push(token('text', i, text, afterSpace, { length: j + 1 - i }));
            i = j + 1;
            continue;
        }

        if (c === '#') {
            // Excel's own seven, #NULL! to #N/A, in the order the C# enum holds them.
            const spelled = errorValues.slice(0, 7).find(e => startsWithAtIgnoreCase(formula, i, e));
            if (spelled === undefined) throw new FormulaSyntaxError(formula, i, "this is not one of Excel's Error Values.");
            tokens.push(token('error', i, spelled, afterSpace, { error: spelled, length: spelled.length }));
            i += spelled.length;
            continue;
        }

        if (c === '(' || c === ')' || c === ',') {
            tokens.push(token(c === '(' ? 'leftParenthesis' : c === ')' ? 'rightParenthesis' : 'comma', i, c, afterSpace, { length: 1 }));
            i++;
            continue;
        }

        const qualifiedError = stickyMatch(qualifiedRefErrorPattern, formula, i);
        if (qualifiedError !== null) {
            const raw = groupText(groupsOf(qualifiedError), 'sheet');
            tokens.push(token('error', i, qualifiedError[0], afterSpace, {
                error: '#REF!', sheetName: unquoteSheet(raw), length: qualifiedError[0].length,
            }));
            i += qualifiedError[0].length;
            continue;
        }

        const threeD = readThreeD(formula, i);
        if (threeD !== undefined) {
            // Sheet1:Sheet3!B2# is read as A1# is, and is #REF!, as Excel's (ADR-0331 §7).
            let { reference: spanned, length: spanLength } = threeD;
            if (spanned.shape === 'cell' && i + spanLength < formula.length && formula.charAt(i + spanLength) === '#') {
                spanned = spanned.with({ spilled: true });
                spanLength++;
            }
            tokens.push(token('reference', i, formula.slice(i, i + spanLength), afterSpace, { reference: spanned, length: spanLength }));
            i += spanLength;
            continue;
        }

        const referenceMatch = stickyMatch(referencePattern, formula, i);
        const parsed = referenceMatch === null ? undefined : referenceOf(referenceMatch);
        if (referenceMatch !== null && parsed !== undefined) {
            // A1#: the Spill Range of the Formula in A1, written after one cell only (ADR-0125).
            let length = referenceMatch[0].length;
            let reference = parsed;
            if (reference.shape === 'cell' && i + length < formula.length && formula.charAt(i + length) === '#') {
                reference = reference.with({ spilled: true });
                length++;
            }
            tokens.push(token('reference', i, formula.slice(i, i + length), afterSpace, { reference, length }));
            i += length;
            continue;
        }

        if (isAsciiDigit(formula.charCodeAt(i)) || c === '.') {
            const number = stickyMatch(numberPattern, formula, i);
            if (number === null) throw new FormulaSyntaxError(formula, i, 'this is not a number.');
            const value = parseFormulaNumber(number[0]);
            // Excel takes no number constant above 9.99999999999999E+307, its documented largest (ADR-0047, second run).
            if (!Number.isFinite(value) || value > largestConstant) throw new FormulaSyntaxError(formula, i, 'the number is too large.');
            tokens.push(token('number', i, number[0], afterSpace, { number: value, length: number[0].length }));
            i += number[0].length;
            continue;
        }

        const nameLength = wideNameLength(formula, i);
        if (nameLength > 0) {
            const name = formula.slice(i, i + nameLength);
            const end = i + nameLength;
            if (end < formula.length && formula.charAt(end) === '[') {
                // A Linked Table's name is ASCII (ADR-0049): a wider one is no structured reference.
                const wide = firstWide(name);
                if (wide >= 0) throw new FormulaSyntaxError(formula, i + wide, `'${formula.charAt(i + wide)}' is not part of the formula syntax ExSheet reads.`);
                const column = structuredColumn(formula, end);
                if (column.refusal !== undefined) throw new FormulaSyntaxError(formula, column.refusal.position, column.refusal.reason);
                tokens.push(token('structuredReference', i, column.column, afterSpace, { tableName: name, length: column.after - i }));
                i = column.after;
                continue;
            }
            tokens.push(token('name', i, name, afterSpace, { length: nameLength }));
            i = end;
            continue;
        }

        const op = operators.find(o => formula.startsWith(o, i));
        if (op !== undefined) {
            tokens.push(token('operator', i, op, afterSpace, { length: op.length }));
            i += op.length;
            continue;
        }

        throw new FormulaSyntaxError(formula, i, `'${c}' is not part of the formula syntax ExSheet reads.`);
    }
}

/** A structured reference's column read, or where and why it cannot be. */
type StructuredColumn =
    | { readonly column: string; readonly after: number; readonly refusal?: undefined }
    | { readonly column?: undefined; readonly after?: undefined; readonly refusal: { readonly position: number; readonly reason: string } };

/**
 * Reads `[Column]` or `[[Column]]`, with `'` escaping the next character, as Excel's structured references do.
 * Other structured forms (`#All`, `@`, column ranges) are not read.
 */
function structuredColumn(formula: string, open: number): StructuredColumn {
    const doubled = open + 1 < formula.length && formula.charAt(open + 1) === '[';
    let i = open + (doubled ? 2 : 1);
    let column = '';
    for (;;) {
        if (i >= formula.length) return { refusal: { position: open, reason: 'the structured reference has no closing bracket.' } };
        const c = formula.charAt(i);
        if (c === "'" && i + 1 < formula.length) {
            column += formula.charAt(i + 1);
            i += 2;
            continue;
        }
        if (c === '[' || c === '#' || c === '@') return { refusal: { position: i, reason: 'only a Table[Column] structured reference is read.' } };
        if (c === ']') break;
        column += c;
        i++;
    }
    if (doubled) {
        if (i + 1 >= formula.length || formula.charAt(i + 1) !== ']') return { refusal: { position: i, reason: 'only a Table[Column] structured reference is read.' } };
        i++;
    }
    if (column.length === 0) return { refusal: { position: open, reason: 'the structured reference names no column.' } };
    return { column, after: i + 1 };
}

/** One part of a matched Reference: its text, and where it stands in the text it was found in. */
export interface ReferenceMatchPart {
    readonly value: string;
    readonly index: number;
}

/** A Reference's text read apart: where its Sheet qualifier and each column and row part stand, `$` included. */
export interface ReferenceMatch {
    /** Where the match starts in the text it was found in. */
    readonly index: number;
    /** How many characters it spans. */
    readonly length: number;
    /** The parts that matched, by group: `sheet`, `c1`, `r1`, `c2`, `r2`, `cc1`, `cc2`, `rr1`, `rr2`. */
    readonly groups: Readonly<Record<string, ReferenceMatchPart | undefined>>;
}

/**
 * The Reference written at `at`, as this grammar reads one there, or `undefined`: the match's groups say where
 * its Sheet qualifier and each column and row part stand — `c1`, `r1`, `c2`, `r2` for a cell or an area, `cc1`,
 * `cc2` for whole columns and `rr1`, `rr2` for whole rows. Nothing that `tokenize` would not read as a Reference.
 */
export function matchReference(formula: string, at: number): ReferenceMatch | undefined {
    const match = stickyMatch(referencePattern, formula, at);
    if (match === null || referenceOf(match) === undefined) return undefined;
    const indices = match.indices?.groups;
    if (indices === undefined) throw new Error('Internal: the Reference pattern is matched with its indices.');
    const groups: Record<string, ReferenceMatchPart | undefined> = {};
    for (const [name, value] of Object.entries(groupsOf(match))) {
        const span = indices[name];
        groups[name] = value === undefined || span === undefined ? undefined : { value, index: span[0] };
    }
    return { index: match.index, length: match[0].length, groups };
}

/** The Reference `matchReference` finds at `at`, read, and how many characters it spans; `undefined` where it finds none. */
export function readReference(formula: string, at: number): { reference: Reference; length: number } | undefined {
    const match = stickyMatch(referencePattern, formula, at);
    const reference = match === null ? undefined : referenceOf(match);
    return match === null || reference === undefined ? undefined : { reference, length: match[0].length };
}

/**
 * The structured reference written at `at` — `Table[Column]` or `Table[[Column]]` — as `tokenize` reads one
 * there: the table's name as written, the column's with its `'` escapes read, and how many characters it
 * spans. `undefined` where it reads none, or reads a form it refuses (`#All`, `@`).
 */
export function readStructuredReference(formula: string, at: number): { table: string; column: string; length: number } | undefined {
    const name = stickyMatch(namePattern, formula, at);
    if (name === null) return undefined;
    const open = at + name[0].length;
    if (open >= formula.length || formula.charAt(open) !== '[') return undefined;
    const column = structuredColumn(formula, open);
    return column.refusal === undefined ? { table: name[0], column: column.column, length: column.after - at } : undefined;
}

/**
 * The 3D Reference written at `at` (ADR-0331 §7) — a span of Sheets, then a cell, a rectangle, whole columns or
 * whole rows, as after one Sheet's name — and how many characters it spans; `undefined` where none is.
 */
function readThreeD(formula: string, at: number): { reference: Reference; length: number } | undefined {
    const span = stickyMatch(sheetSpanPattern, formula, at);
    if (span === null) return undefined;
    const place = stickyMatch(referencePattern, formula, at + span[0].length);
    if (place === null || groupsOf(place)['sheet'] !== undefined) return undefined;
    const reference = referenceOf(place);
    if (reference === undefined) return undefined;
    const groups = groupsOf(span);
    const first = groups['quotedFirst'] ?? groupText(groups, 'first');
    const last = groups['quotedLast'] ?? groupText(groups, 'last');
    return {
        reference: reference.with({ sheetName: first.replaceAll("''", "'"), lastSheetName: last.replaceAll("''", "'") }),
        length: span[0].length + place[0].length,
    };
}

function groupText(groups: Record<string, string | undefined>, name: string): string {
    const value = groups[name];
    if (value === undefined) throw new Error(`Internal: the group '${name}' matched without a value.`);
    return value;
}

function unquoteSheet(raw: string): string {
    return raw.charAt(0) === "'" ? raw.slice(1, -1).replaceAll("''", "'") : raw;
}

function referenceOf(match: RegExpExecArray): Reference | undefined {
    const groups = groupsOf(match);
    const rawSheet = groups['sheet'];
    const sheet = rawSheet === undefined ? undefined : unquoteSheet(rawSheet);

    const c1Text = groups['c1'];
    if (c1Text !== undefined) {
        const c1 = columnOf(c1Text);
        const r1 = rowOf(groupText(groups, 'r1'));
        if (c1 === undefined || r1 === undefined) return undefined;
        const c2Text = groups['c2'];
        if (c2Text === undefined) return Reference.cell(sheet, r1.index, c1.index, r1.absolute, c1.absolute);
        const c2 = columnOf(c2Text);
        const r2 = rowOf(groupText(groups, 'r2'));
        if (c2 === undefined || r2 === undefined) return undefined;
        // Excel writes a rectangle from its top-left corner, whichever corner was typed first.
        const [left, right] = c2.index < c1.index ? [c2, c1] : [c1, c2];
        const [top, bottom] = r2.index < r1.index ? [r2, r1] : [r1, r2];
        return Reference.rectangle(sheet, top.index, left.index, bottom.index, right.index, top.absolute, left.absolute, bottom.absolute, right.absolute);
    }
    const cc1Text = groups['cc1'];
    if (cc1Text !== undefined) {
        const c1 = columnOf(cc1Text);
        const c2 = columnOf(groupText(groups, 'cc2'));
        if (c1 === undefined || c2 === undefined) return undefined;
        const [left, right] = c2.index < c1.index ? [c2, c1] : [c1, c2];
        // Excel holds a whole column's rows as absolute: A:XFD is written $1:$1048576.
        return Reference.rectangle(sheet, 0, left.index, rowCount - 1, right.index, true, left.absolute, true, right.absolute);
    }
    const r1 = rowOf(groupText(groups, 'rr1'));
    const r2 = rowOf(groupText(groups, 'rr2'));
    if (r1 === undefined || r2 === undefined) return undefined;
    const [top, bottom] = r2.index < r1.index ? [r2, r1] : [r1, r2];
    return Reference.rectangle(sheet, top.index, 0, bottom.index, columnCount - 1, top.absolute, true, bottom.absolute, true);
}

function columnOf(text: string): { index: number; absolute: boolean } | undefined {
    const absolute = text.charAt(0) === '$';
    const index = CellAddress.tryParseColumn(absolute ? text.slice(1) : text);
    return index === undefined ? undefined : { index, absolute };
}

function rowOf(text: string): { index: number; absolute: boolean } | undefined {
    const absolute = text.charAt(0) === '$';
    const digits = absolute ? text.slice(1) : text;
    if (digits.charAt(0) === '0') return undefined;
    const n = Number(digits);
    if (n > rowCount) return undefined;
    return { index: n - 1, absolute };
}
