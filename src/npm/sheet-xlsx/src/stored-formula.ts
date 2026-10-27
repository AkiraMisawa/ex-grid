import { startsWithIgnoreCase, trimWhiteSpace } from './text-casing';

// A Formula's text as an .xlsx stores it, read into the text ExSheet's engine reads (ADR-0330 §4), as ExSheet.Xlsx's
// StoredFormula reads it: Excel's stored prefixes taken off — `_xlfn.` before a function newer than Excel 2007, `_xlws.`
// before a worksheet-only one, and `_xlpm.` before a name a `LET` binds — and `_xlfn.ANCHORARRAY(A1)` written as the Spill
// Range reference `A1#` it stands for (ADR-0125). What the engine cannot reach is refused here, by name, before it would read
// as a plausible `#REF!` or `#NAME?`: another workbook, an Excel Table, and the implicit intersection operator. Text in
// quotes, and a Sheet's name in quotes, are never touched.

/** Why a Formula as the file stores it is refused before the engine reads it. */
export type StoredFormulaProblem =
    | 'none'
    /** A Reference into another workbook: `[1]Sheet1!A1`, `'[1]Sheet 1'!A1`, `[1]!Rate`. */
    | 'otherWorkbook'
    /** A structured reference into an Excel Table: `Table1[Amount]`, `Table1[[#This Row],[Amount]]`. */
    | 'excelTable'
    /** Excel's implicit intersection operator, `@`, stored as `_xlfn.SINGLE`. */
    | 'implicitIntersection'
    /** A Spill Range reference (`_xlfn.ANCHORARRAY`) whose argument is not one cell. */
    | 'spillRange';

const prefixes = ['_xlfn.', '_xlws.', '_xlpm.'];

/** The Formula ExSheet reads, with its `=`, and the problem that refuses it, `none` when nothing does. */
export function readStoredFormula(stored: string): { readonly read: string; readonly problem: StoredFormulaProblem } {
    const refusal = refusalOf(stored);
    if (refusal !== 'none') return { read: `=${stored}`, problem: refusal };
    const anchored = anchorArrays(stripPrefixes(stored));
    return { read: `=${anchored.text}`, problem: anchored.read ? 'none' : 'spillRange' };
}

/** The text with every stored prefix taken off, outside quotes. */
export function stripPrefixes(stored: string): string {
    let text = '';
    let i = 0;
    while (i < stored.length) {
        const c = stored.charAt(i);
        if (c === '"' || c === "'") {
            const end = skipQuoted(stored, i);
            text += stored.slice(i, end);
            i = end;
            continue;
        }
        if (c === '_' && (text.length === 0 || !isNameChar(text.charAt(text.length - 1)))) {
            const prefix = prefixes.find(p => startsWithIgnoreCase(stored, i, p));
            if (prefix !== undefined) {
                i += prefix.length;
                continue;
            }
        }
        text += c;
        i++;
    }
    return text;
}

/** Why the stored text is refused before it is read, outside quotes; `none` when it is not. */
function refusalOf(stored: string): StoredFormulaProblem {
    for (let i = 0; i < stored.length; i++) {
        const c = stored.charAt(i);
        if (c === '"') {
            i = skipQuoted(stored, i) - 1;
            continue;
        }
        if (c === "'") {
            // A Sheet's name cannot hold '[' (Excel's rule, and ExSheet's): one that starts with it names a workbook.
            if (i + 1 < stored.length && stored.charAt(i + 1) === '[') return 'otherWorkbook';
            i = skipQuoted(stored, i) - 1;
            continue;
        }
        if (c === '[') {
            const before = i > 0 ? stored.charAt(i - 1) : '\0';
            if (isNameChar(before)) return 'excelTable';
            return /^\[[0-9]+\]/.test(stored.slice(i)) ? 'otherWorkbook' : 'excelTable';
        }
        if (c === '@') return 'implicitIntersection';
        if (c === '_' && (i === 0 || !isNameChar(stored.charAt(i - 1))) && startsWithIgnoreCase(stored, i, '_xlfn.SINGLE(')) return 'implicitIntersection';
    }
    return 'none';
}

/** One cell, as a Spill Range reference names its anchor: `A1`, `$B$2`, `Sheet2!A1`, `'My Sheet'!A1`. */
const cellPattern = /^(?:'(?:[^']|'')+'!|[A-Za-z0-9_.\\]+!)?\$?[A-Za-z]{1,3}\$?[0-9]+$/;

/**
 * Each `ANCHORARRAY(ref)` written as `ref#`; `read` is false when one names anything but one cell, which the engine's
 * grammar does not spell.
 */
function anchorArrays(text: string): { readonly text: string; readonly read: boolean } {
    const name = 'ANCHORARRAY(';
    let written = '';
    let anchored = false;
    let at = 0;
    for (let i = 0; i < text.length; i++) {
        const c = text.charAt(i);
        if (c === '"' || c === "'") {
            i = skipQuoted(text, i) - 1;
            continue;
        }
        if ((c !== 'A' && c !== 'a') || (i > 0 && isNameChar(text.charAt(i - 1))) || !startsWithIgnoreCase(text, i, name)) continue;
        const open = i + name.length;
        let close = open;
        while (close < text.length && text.charAt(close) !== ')') {
            close = text.charAt(close) === '"' || text.charAt(close) === "'" ? skipQuoted(text, close) : close + 1;
        }
        const inner = close < text.length ? trimWhiteSpace(text.slice(open, close)) : '';
        if (!cellPattern.test(inner)) return { text, read: false };
        written += text.slice(at, i) + inner + '#';
        anchored = true;
        at = close + 1;
        i = close;
    }
    return anchored ? { text: written + text.slice(at), read: true } : { text, read: true };
}

/** Where the quoted run that starts at `start` ends: past its closing quote, a doubled quote read as one. */
function skipQuoted(text: string, start: number): number {
    const quote = text.charAt(start);
    let i = start + 1;
    while (i < text.length) {
        if (text.charAt(i) === quote) {
            if (i + 1 < text.length && text.charAt(i + 1) === quote) {
                i += 2;
                continue;
            }
            return i + 1;
        }
        i++;
    }
    return text.length;
}

/** `char.IsLetterOrDigit(c) || c is '_' or '.' or '\\' or '?'`, of one code unit. */
export function isNameChar(c: string): boolean {
    return /^[\p{L}\p{Nd}_.\\?]$/u.test(c);
}
