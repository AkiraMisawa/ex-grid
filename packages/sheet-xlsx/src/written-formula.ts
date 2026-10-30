import { CellAddress, DeclaredFunction, Sheet, type Entry } from '@ex-grid/sheet-engine';
import { elementAt } from './must';
import { equalsIgnoreCase, foldCase, startsWithIgnoreCase } from './text-casing';

// A Formula as ExSheet holds it, in the engine's invariant syntax (ADR-0047), written as an .xlsx stores it (ADR-0330 §4), as
// ExSheet.Xlsx's WrittenFormula writes it, the reverse of stored-formula.ts: `_xlfn.` put back before each function newer
// than Excel 2007, `_xlfn._xlws.` before `FILTER` and `SORT`, `_xlpm.` before each name a `LET` binds or a `LAMBDA` takes, and
// the Spill Range reference `A1#` written `_xlfn.ANCHORARRAY(A1)`. Without its prefix, Excel reads a newer function as a name
// it does not know, and shows `#NAME?`. Text in quotes, a Sheet's name, a Reference and a number are never touched. A
// structured reference to a Linked Table, which is written as an Excel Table of its name (ADR-0330 §6), is written as the
// engine stores it, which is Excel's own form: single brackets whatever the column's name holds, with `'` before each of
// `[ ] # '` (`Positions[Market Value]`, `Positions[Rate'#]`; observed, verification/2026-09-27-windows-excel, TEXT-021 and
// TEXT-022).
//
// The text is split as the engine's lexer splits it. Which names a `LET` or a `LAMBDA` binds is read from the calls'
// arguments, and checked against what the engine itself found unbound (`Entry.unboundNames`): where the two disagree, the
// Formula is refused rather than written with a prefix in the wrong place.

/** Why a Formula cannot be written as Excel stores it. */
export type WrittenFormulaProblem =
    | 'none'
    /** A structured reference (`Positions[PV]`) to a table, or to a column of one, the Workbook does not declare: the engine
     * shows `#NAME?` or `#REF!`, and the file would hold no Excel Table for Excel to read. */
    | 'structuredReference'
    /** A structured reference to a Linked Table's column whose name begins or ends with a space, which a structured reference
     * in Excel does not keep; holds `@`, which Excel reads at the start as the row the Formula stands in; or holds a character
     * whose form in a structured reference has not been seen in Excel (`unseenInColumns`). */
    | 'columnName'
    /** A Linked Table's name read as a name of its own (`=SUM(Positions)`): ExSheet shows `#NAME?` there, and Excel, once the
     * file holds the Excel Table, reads the table's rows. */
    | 'tableName'
    /** The text does not split into the tokens the engine reads, or its names do not bind as the engine binds them. */
    | 'unreadable'
    /** A Reference names a Sheet the Workbook does not have: Excel would read it as another workbook's. */
    | 'missingSheet'
    /** A name a `LET` binds or a `LAMBDA` takes is called, and the engine declares no function of that name: the engine reads
     * the call as one of an unknown function (`#NAME?`), and Excel calls what the name is bound to. */
    | 'boundCall'
    /** A text constant in the Formula is longer than the 255 characters Excel takes in one. */
    | 'longText'
    /** Calls are nested deeper than the 64 levels Excel takes. */
    | 'tooDeep'
    /** A call has more than the 255 arguments Excel takes. */
    | 'tooManyArguments';

/**
 * Excel's functions newer than Excel 2007, which a file stores with `_xlfn.` before them (ECMA-376's future functions, and the
 * ones Excel has added since), every one of Excel's, declared by the engine or not: a Formula naming one the engine does not
 * declare is carried as written, and Excel computes it.
 */
export const newerFunctions: readonly string[] = Object.freeze([
    'ACOT', 'ACOTH', 'AGGREGATE', 'ARABIC', 'ARRAYTOTEXT', 'BASE', 'BETA.DIST', 'BETA.INV', 'BINOM.DIST',
    'BINOM.DIST.RANGE', 'BINOM.INV', 'BITAND', 'BITLSHIFT', 'BITOR', 'BITRSHIFT', 'BITXOR', 'BYCOL', 'BYROW',
    'CEILING.MATH', 'CEILING.PRECISE', 'CHISQ.DIST', 'CHISQ.DIST.RT', 'CHISQ.INV', 'CHISQ.INV.RT', 'CHISQ.TEST',
    'CHOOSECOLS', 'CHOOSEROWS', 'COMBINA', 'CONCAT', 'CONFIDENCE.NORM', 'CONFIDENCE.T', 'COPILOT', 'COT', 'COTH',
    'COVARIANCE.P', 'COVARIANCE.S', 'CSC', 'CSCH', 'DAYS', 'DECIMAL', 'DETECTLANGUAGE', 'DROP', 'ECMA.CEILING',
    'ENCODEURL', 'ERF.PRECISE', 'ERFC.PRECISE', 'EXPAND', 'EXPON.DIST', 'F.DIST', 'F.DIST.RT', 'F.INV', 'F.INV.RT',
    'F.TEST', 'FIELDVALUE', 'FILTER', 'FILTERXML', 'FLOOR.MATH', 'FLOOR.PRECISE', 'FORECAST.ETS',
    'FORECAST.ETS.CONFINT', 'FORECAST.ETS.SEASONALITY', 'FORECAST.ETS.STAT', 'FORECAST.LINEAR', 'FORMULATEXT',
    'GAMMA', 'GAMMA.DIST', 'GAMMA.INV', 'GAMMALN.PRECISE', 'GAUSS', 'GROUPBY', 'HSTACK', 'HYPGEOM.DIST', 'IFNA', 'IFS',
    'IMAGE', 'IMCOSH', 'IMCOT', 'IMCSC', 'IMCSCH', 'IMSEC', 'IMSECH', 'IMSINH', 'IMTAN', 'ISFORMULA', 'ISOMITTED',
    'ISOWEEKNUM', 'LAMBDA', 'LET', 'LOGNORM.DIST', 'LOGNORM.INV', 'MAKEARRAY', 'MAP', 'MAXIFS', 'MINIFS', 'MODE.MULT',
    'MODE.SNGL', 'MUNIT', 'NEGBINOM.DIST', 'NETWORKDAYS.INTL', 'NORM.DIST', 'NORM.INV', 'NORM.S.DIST', 'NORM.S.INV',
    'NUMBERVALUE', 'PDURATION', 'PERCENTILE.EXC', 'PERCENTILE.INC', 'PERCENTOF', 'PERCENTRANK.EXC', 'PERCENTRANK.INC',
    'PERMUTATIONA', 'PHI', 'PIVOTBY', 'POISSON.DIST', 'QUARTILE.EXC', 'QUARTILE.INC', 'RANDARRAY', 'RANK.AVG',
    'RANK.EQ', 'REDUCE', 'REGEXEXTRACT', 'REGEXREPLACE', 'REGEXTEST', 'RRI', 'SCAN', 'SEC', 'SECH', 'SEQUENCE',
    'SHEET', 'SHEETS', 'SKEW.P', 'SORT', 'SORTBY', 'STDEV.P', 'STDEV.S', 'STOCKHISTORY', 'SWITCH', 'T.DIST',
    'T.DIST.2T', 'T.DIST.RT', 'T.INV', 'T.INV.2T', 'T.TEST', 'TAKE', 'TEXTAFTER', 'TEXTBEFORE', 'TEXTJOIN',
    'TEXTSPLIT', 'TOCOL', 'TOROW', 'TRANSLATE', 'TRIMRANGE', 'UNICHAR', 'UNICODE', 'UNIQUE', 'VALUETOTEXT', 'VAR.P',
    'VAR.S', 'VSTACK', 'WEBSERVICE', 'WEIBULL.DIST', 'WORKDAY.INTL', 'WRAPCOLS', 'WRAPROWS', 'XLOOKUP', 'XMATCH',
    'XOR', 'Z.TEST',
]);

const newer: ReadonlySet<string> = new Set(newerFunctions.map(foldCase));

/** Whether `name` is one of Excel's functions newer than Excel 2007, without regard to case. */
export function isNewerFunction(name: string): boolean {
    return newer.has(foldCase(name));
}

/** The functions Excel stores as worksheet-only as well as newer: `_xlfn._xlws.`. */
const worksheetOnly: ReadonlySet<string> = new Set(['FILTER', 'SORT']);

const errors = ['#NULL!', '#DIV/0!', '#VALUE!', '#REF!', '#NAME?', '#NUM!', '#N/A'];

/** The longest text constant Excel takes in a Formula, in characters. */
export const maxText = 255;

/** The deepest Excel nests calls in a Formula. */
export const maxDepth = 64;

/** The most arguments Excel takes in one call. */
export const maxArguments = 255;

/**
 * The characters of a column's name whose form in a structured reference Excel has not been seen to write: `@`, and those
 * Microsoft's documentation of structured references lists as needing the column in double brackets, which single brackets
 * (TEXT-021, TEXT-022) may not carry as Excel stores them.
 */
const unseenInColumns = /[@,:."{}$^&*+=\-<>/\t\n\r]/;

type Kind = 'space' | 'text' | 'error' | 'reference' | 'number' | 'name' | 'open' | 'close' | 'comma' | 'operator' | 'structuredReference';

class Token {
    readonly kind: Kind;
    readonly text: string;
    /** A Reference written with `#` after it: the Spill Range of the Formula in that cell. */
    readonly spilled: boolean;
    /** The Sheet a Reference names, unquoted, or `undefined` for one of its own Sheet; for a 3D Reference, the Sheet its span
     * begins on. */
    readonly sheet: string | undefined;
    /** For a 3D Reference (`Sheet1:Sheet3!B2`), the Sheet its span ends on, unquoted. */
    readonly lastSheet: string | undefined;
    /** A name that a `(` follows: a function's. */
    readonly called: boolean;
    /** A name a `LET` binds or a `LAMBDA` takes, or a use of one where it is bound. */
    bound = false;

    constructor(kind: Kind, text: string, more: { spilled?: boolean; sheet?: string | undefined; lastSheet?: string; called?: boolean } = {}) {
        this.kind = kind;
        this.text = text;
        this.spilled = more.spilled === true;
        this.sheet = more.sheet;
        this.lastSheet = more.lastSheet;
        this.called = more.called === true;
    }
}

/**
 * The Formula's text as the file stores it, without its `=`, and the problem that refuses it, `none` when it can be written.
 * @param entry - A Formula Entry.
 * @param hasSheet - Whether the Workbook written has a Sheet of a name a Reference qualifies, without regard to case;
 * `undefined` to take every name.
 * @param columnsOf - The columns of the Linked Table of a name, without regard to case, as declared, or `undefined` where none
 * is; `undefined` itself to declare none, so that every structured reference is refused.
 */
export function writeFormula(
    entry: Entry,
    hasSheet: ((name: string) => boolean) | undefined,
    columnsOf?: (name: string) => readonly string[] | undefined,
): { readonly text: string; readonly problem: WrittenFormulaProblem } {
    const formula = entry.formula;
    if (formula === undefined) throw new Error('Internal: only a Formula Entry is written as a Formula.');
    const refused = (problem: WrittenFormulaProblem): { readonly text: string; readonly problem: WrittenFormulaProblem } => ({ text: formula.slice(1), problem });
    const split = splitFormula(formula, columnsOf);
    if (split.problem !== 'none') return refused(split.problem);
    const tokens = split.tokens;
    if (hasSheet !== undefined && tokens.some(t => (t.sheet !== undefined && !hasSheet(t.sheet)) || (t.lastSheet !== undefined && !hasSheet(t.lastSheet)))) {
        return refused('missingSheet');
    }
    const bound = bind(tokens);
    if (bound !== 'none') return refused(bound);
    if (columnsOf !== undefined && tokens.some(t => t.kind === 'name' && !t.called && !t.bound && columnsOf(t.text) !== undefined)) return refused('tableName');
    if (tokens.some(t => t.kind === 'text' && t.text.length - 2 - count(t.text, '""') > maxText)) return refused('longText');
    if (!sameUnbound(tokens, entry.unboundNames)) return refused('unreadable');
    let text = '';
    for (const token of tokens) {
        if (token.kind === 'reference' && token.spilled) text += `_xlfn.ANCHORARRAY(${token.text})`;
        else if (token.kind === 'name' && token.bound) text += `_xlpm.${token.text}`;
        else if (token.kind === 'name' && token.called && isNewerFunction(token.text)) text += (worksheetOnly.has(foldCase(token.text)) ? '_xlfn._xlws.' : '_xlfn.') + token.text;
        else text += token.text;
    }
    return { text, problem: 'none' };
}

function count(text: string, of: string): number {
    let n = 0;
    for (let at = text.indexOf(of); at >= 0; at = text.indexOf(of, at + of.length)) n++;
    return n;
}

// The engine's own grammar (ExSheet.Engine's Lexer), which the invariant text it holds follows. Each is sticky: it matches at
// `lastIndex` alone, as .NET's \G does.

const referencePattern = new RegExp(
    String.raw`(?:(?<sheet>'(?:[^']|'')+'|[A-Za-z_][A-Za-z0-9_.]*)!)?`
    + String.raw`(?:(?<c1>\$?[A-Za-z]{1,3})(?<r1>\$?[0-9]{1,7})(?::(?<c2>\$?[A-Za-z]{1,3})(?<r2>\$?[0-9]{1,7}))?`
    + String.raw`|(?<cc1>\$?[A-Za-z]{1,3}):(?<cc2>\$?[A-Za-z]{1,3})`
    + String.raw`|(?<rr1>\$?[0-9]{1,7}):(?<rr2>\$?[0-9]{1,7}))`
    + String.raw`(?![A-Za-z0-9_.(\[!:$])`,
    'y');

const qualifiedRefError = /(?<sheet>'(?:[^']|'')+'|[A-Za-z_][A-Za-z0-9_.]*)!#REF!/iy;

/** A 3D Reference's span before its `!`, as the engine's lexer reads one: `Sheet1:Sheet3!`, `'Q1 Sales:Sheet3'!`. */
const sheetSpanPattern = /(?:'(?<quotedFirst>(?:[^':]|'')+):(?<quotedLast>(?:[^':]|'')+)'|(?<first>[A-Za-z_][A-Za-z0-9_.]*):(?<last>[A-Za-z_][A-Za-z0-9_.]*))!/y;

const numberPattern = /(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?/y;

// \p{L} of one UTF-16 code unit, as .NET matches it: a letter outside the Basic Multilingual Plane is two code units, neither a
// letter, so it is kept out here too.
const namePattern = /(?=[\0-￿])[\p{L}_\\](?:(?=[\0-￿])[\p{L}0-9_.])*/uy;

function matchAt(pattern: RegExp, text: string, at: number): RegExpExecArray | null {
    pattern.lastIndex = at;
    return pattern.exec(text);
}

const operators = ['<>', '<=', '>=', '=', '<', '>', '+', '-', '*', '/', '^', '&', '%'];

/** The Formula's tokens after its `=`, split as the engine's lexer splits them. */
function splitFormula(formula: string, columnsOf: ((name: string) => readonly string[] | undefined) | undefined): { readonly tokens: readonly Token[]; readonly problem: WrittenFormulaProblem } {
    const tokens: Token[] = [];
    const unreadable = { tokens: [], problem: 'unreadable' } as const;
    let i = 1;
    while (i < formula.length) {
        const c = formula.charAt(i);
        if (c === ' ' || c === '\n' || c === '\r') {
            const start = i;
            while (i < formula.length && (formula.charAt(i) === ' ' || formula.charAt(i) === '\n' || formula.charAt(i) === '\r')) i++;
            tokens.push(new Token('space', formula.slice(start, i)));
            continue;
        }
        if (c === '"') {
            let j = i + 1;
            while (j < formula.length && !(formula.charAt(j) === '"' && (j + 1 >= formula.length || formula.charAt(j + 1) !== '"'))) j += formula.charAt(j) === '"' ? 2 : 1;
            if (j >= formula.length) return unreadable;
            tokens.push(new Token('text', formula.slice(i, j + 1)));
            i = j + 1;
            continue;
        }
        if (c === '#') {
            const error = errors.find(e => startsWithIgnoreCase(formula, i, e));
            if (error === undefined) return unreadable;
            tokens.push(new Token('error', formula.slice(i, i + error.length)));
            i += error.length;
            continue;
        }
        if (c === '(' || c === ')' || c === ',') {
            tokens.push(new Token(c === '(' ? 'open' : c === ')' ? 'close' : 'comma', c));
            i++;
            continue;
        }
        const qualifiedError = matchAt(qualifiedRefError, formula, i);
        if (qualifiedError !== null) {
            tokens.push(new Token('error', qualifiedError[0], { sheet: sheetOf(qualifiedError) }));
            i += qualifiedError[0].length;
            continue;
        }
        const span = matchAt(sheetSpanPattern, formula, i);
        if (span !== null) {
            const place = matchAt(referencePattern, formula, i + span[0].length);
            if (place?.groups !== undefined && place.groups['sheet'] === undefined && isReference(place)) {
                // Sheet1:Sheet3!B2#, which the engine reads as #REF!, is stored as Excel stored it: SUM(_xlfn.ANCHORARRAY(Sheet2:Sheet4!B2))
                // (verification/2026-10-24-windows-workbook-12/records/spill-xml.txt).
                const end = i + span[0].length + place[0].length;
                const spannedSpill = place.groups['c1'] !== undefined && place.groups['c2'] === undefined && end < formula.length && formula.charAt(end) === '#';
                const g = span.groups;
                const first = g?.['quotedFirst'] ?? g?.['first'];
                const last = g?.['quotedLast'] ?? g?.['last'];
                if (first === undefined || last === undefined) throw new Error('Internal: a span names no Sheets.');
                tokens.push(new Token('reference', span[0] + place[0], { spilled: spannedSpill, sheet: first.replaceAll("''", "'"), lastSheet: last.replaceAll("''", "'") }));
                i = end + (spannedSpill ? 1 : 0);
                continue;
            }
        }
        const reference = matchAt(referencePattern, formula, i);
        if (reference?.groups !== undefined && isReference(reference)) {
            const end = i + reference[0].length;
            const spilled = reference.groups['c1'] !== undefined && reference.groups['c2'] === undefined && end < formula.length && formula.charAt(end) === '#';
            tokens.push(new Token('reference', reference[0], { spilled, sheet: sheetOf(reference) }));
            i = end + (spilled ? 1 : 0);
            continue;
        }
        if ((c >= '0' && c <= '9') || c === '.') {
            const number = matchAt(numberPattern, formula, i);
            if (number === null) return unreadable;
            tokens.push(new Token('number', number[0]));
            i += number[0].length;
            continue;
        }
        const name = matchAt(namePattern, formula, i);
        if (name !== null) {
            const end = i + name[0].length;
            if (end < formula.length && formula.charAt(end) === '[') {
                const column = structuredColumn(formula, end);
                if (column.name === undefined) {
                    if (!column.at) return unreadable;
                    return { tokens, problem: 'columnName' };
                }
                const declared = columnsOf?.(name[0]);
                const columnName = column.name;
                if (declared === undefined || !declared.some(d => equalsIgnoreCase(d, columnName))) return { tokens, problem: 'structuredReference' };
                if (columnName.startsWith(' ') || columnName.endsWith(' ') || unseenInColumns.test(columnName)) {
                    return { tokens, problem: 'columnName' };
                }
                tokens.push(new Token('structuredReference', name[0] + storedColumn(columnName)));
                i = column.after;
                continue;
            }
            tokens.push(new Token('name', name[0], { called: end < formula.length && formula.charAt(end) === '(' }));
            i = end;
            continue;
        }
        const op = operators.find(o => formula.startsWith(o, i));
        if (op !== undefined) {
            tokens.push(new Token('operator', op));
            i += op.length;
            continue;
        }
        return unreadable;
    }
    return { tokens, problem: 'none' };
}

/**
 * The column a structured reference names, read from its `[` as the engine's lexer reads it — `[Column]` or `[[Column]]`, with
 * `'` taking the next character as it is — or no name where the engine reads none; then `at` says whether it met an
 * unescaped `@`, which the engine reads in no column's name.
 */
function structuredColumn(formula: string, open: number): { readonly name: string | undefined; readonly after: number; readonly at: boolean } {
    const none = (at: boolean): { readonly name: undefined; readonly after: number; readonly at: boolean } => ({ name: undefined, after: open, at });
    const doubled = open + 1 < formula.length && formula.charAt(open + 1) === '[';
    let i = open + (doubled ? 2 : 1);
    let column = '';
    for (;;) {
        if (i >= formula.length) return none(false);
        const c = formula.charAt(i);
        if (c === "'" && i + 1 < formula.length) {
            column += formula.charAt(i + 1);
            i += 2;
            continue;
        }
        if (c === '[' || c === '#' || c === '@') return none(c === '@');
        if (c === ']') break;
        column += c;
        i++;
    }
    if (doubled) {
        if (i + 1 >= formula.length || formula.charAt(i + 1) !== ']') return none(false);
        i++;
    }
    if (column.length === 0) return none(false);
    return { name: column, after: i + 1, at: false };
}

/** A column of a structured reference as the engine stores it and Excel writes it back (TEXT-021, TEXT-022): in single
 * brackets, with `'` before each of `[ ] # '`. */
function storedColumn(column: string): string {
    let text = '[';
    for (const c of column) {
        if (c === '[' || c === ']' || c === '#' || c === "'") text += "'";
        text += c;
    }
    return `${text}]`;
}

/** The Sheet a match's qualifier names, unquoted, or `undefined` where it has none. */
function sheetOf(match: RegExpExecArray): string | undefined {
    const raw = match.groups?.['sheet'];
    if (raw === undefined) return undefined;
    return raw.startsWith("'") ? raw.slice(1, -1).replaceAll("''", "'") : raw;
}

/** One call, or one pair of parentheses, as its arguments stand: the indexes of each argument's tokens. */
interface Frame {
    readonly function: Token | undefined;
    readonly arguments: number[][];
}

interface Binding { readonly name: string; readonly first: number; readonly last: number }

/**
 * Marks every name a `LET` binds or a `LAMBDA` takes, and each use of one where it is bound; or the problem that refuses the
 * Formula: calls nested or argued beyond Excel's limits, or a bound name called.
 */
function bind(tokens: readonly Token[]): WrittenFormulaProblem {
    const bindings: Binding[] = [];
    const frames: Frame[] = [{ function: undefined, arguments: [[]] }];
    const top = (): Frame => elementAt(frames, frames.length - 1);
    const lastArgument = (frame: Frame): number[] => elementAt(frame.arguments, frame.arguments.length - 1);
    let calls = 0;
    for (let i = 0; i < tokens.length; i++) {
        const token = elementAt(tokens, i);
        switch (token.kind) {
            case 'open': {
                lastArgument(top()).push(i);
                const previous = i > 0 ? elementAt(tokens, i - 1) : undefined;
                const called = previous !== undefined && previous.kind === 'name' && previous.called ? previous : undefined;
                if (called !== undefined && ++calls > maxDepth) return 'tooDeep';
                frames.push({ function: called, arguments: [[]] });
                break;
            }
            case 'comma':
                if (frames.length > 1) {
                    top().arguments.push([]);
                    if (top().function !== undefined && top().arguments.length > maxArguments) return 'tooManyArguments';
                } else {
                    lastArgument(top()).push(i);
                }
                break;
            case 'close':
                if (frames.length > 1) {
                    const frame = elementAt(frames, frames.length - 1);
                    frames.pop();
                    if (frame.function !== undefined) calls--;
                    lastArgument(top()).push(i);
                    binds(frame, tokens, i, bindings);
                } else {
                    lastArgument(top()).push(i);
                }
                break;
            case 'space':
                break;
            case 'text':
            case 'error':
            case 'reference':
            case 'number':
            case 'name':
            case 'operator':
            case 'structuredReference':
                lastArgument(top()).push(i);
                break;
            default:
                return unreachableKind(token.kind);
        }
    }
    for (let i = 0; i < tokens.length; i++) {
        const name = elementAt(tokens, i);
        if (name.kind !== 'name' || !bindings.some(b => b.first <= i && i <= b.last && equalsIgnoreCase(b.name, name.text))) continue;
        // A call is of a function wherever the engine declares one of its name, bound or not, as the engine's parser takes it;
        // a bound name called otherwise is a call the engine and Excel read differently.
        if (!name.called) name.bound = true;
        else if (DeclaredFunction.find(name.text) === undefined) return 'boundCall';
    }
    return 'none';
}

function unreachableKind(kind: never): never {
    throw new Error(`Internal: ${String(kind)} is not a token's kind.`);
}

/** What a closed `LET` or `LAMBDA` call binds: a `LET`'s names over what follows each in the call, and a `LAMBDA`'s parameters
 * over its last argument. */
function binds(frame: Frame, tokens: readonly Token[], close: number, bindings: Binding[]): void {
    const fn = frame.function;
    if (fn === undefined) return;
    const args = frame.arguments;
    const loneName = (argument: readonly number[]): readonly [number, Token] | undefined => {
        if (argument.length !== 1) return undefined;
        const at = elementAt(argument, 0);
        const token = elementAt(tokens, at);
        return token.kind === 'name' && !token.called ? [at, token] : undefined;
    };
    if (equalsIgnoreCase(fn.text, 'LET')) {
        for (let k = 0; k + 1 < args.length; k += 2) {
            const named = loneName(elementAt(args, k));
            if (named !== undefined) bindings.push({ name: named[1].text, first: named[0], last: close });
        }
    } else if (equalsIgnoreCase(fn.text, 'LAMBDA') && args.length > 0 && elementAt(args, args.length - 1).length > 0) {
        const body = elementAt(args, args.length - 1);
        for (let k = 0; k + 1 < args.length; k++) {
            const named = loneName(elementAt(args, k));
            if (named !== undefined) {
                bindings.push({ name: named[1].text, first: named[0], last: named[0] });
                bindings.push({ name: named[1].text, first: elementAt(body, 0), last: elementAt(body, body.length - 1) });
            }
        }
    }
}

/**
 * Whether the names left unbound here are the ones the engine found unbound: what it parsed is what is written. A name of
 * another script is one the engine takes only where a `LET` binds it, and lists in neither.
 */
function sameUnbound(tokens: readonly Token[], engine: readonly string[]): boolean {
    const unbound = new Set(tokens
        .filter(t => t.kind === 'name' && !t.called && !t.bound && !isBoolean(t.text) && isAscii(t.text))
        .map(t => foldCase(t.text)));
    const found = new Set(engine.map(foldCase));
    return unbound.size === found.size && [...found].every(name => unbound.has(name));
}

/** Whether every UTF-16 code unit of `text` is ASCII. */
function isAscii(text: string): boolean {
    for (let i = 0; i < text.length; i++) {
        if (text.charCodeAt(i) >= 0x80) return false;
    }
    return true;
}

function isBoolean(name: string): boolean {
    return equalsIgnoreCase(name, 'TRUE') || equalsIgnoreCase(name, 'FALSE');
}

/** Whether a Reference's column, as matched, is one the engine reads; one not matched is. */
function readsColumn(value: string | undefined): boolean {
    return value === undefined || CellAddress.tryParseColumn(trimDollars(value)) !== undefined;
}

/** Whether a Reference's row, as matched, is one the engine reads; one not matched is. */
function readsRow(value: string | undefined): boolean {
    if (value === undefined) return true;
    const digits = trimDollars(value);
    return !digits.startsWith('0') && /^[0-9]+$/.test(digits) && Number(digits) <= Sheet.rowCount;
}

/** Whether a match of `referencePattern` is a Reference the engine reads: no column past `XFD`, no row past the Sheet's last. */
function isReference(match: RegExpExecArray): boolean {
    const g = match.groups;
    if (g === undefined) return false;
    return readsColumn(g['c1']) && readsColumn(g['c2']) && readsColumn(g['cc1']) && readsColumn(g['cc2'])
        && readsRow(g['r1']) && readsRow(g['r2']) && readsRow(g['rr1']) && readsRow(g['rr2']);
}

function trimDollars(text: string): string {
    let i = 0;
    while (i < text.length && text.charAt(i) === '$') i++;
    return text.slice(i);
}


