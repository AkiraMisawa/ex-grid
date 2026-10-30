// A Formula's stored text (C#'s FormulaText.cs; ADR-0047). It keeps the whitespace it was typed with where Excel
// keeps it, which is before a token: whitespace at the end of the Formula, and before a `,`, is dropped, as Excel
// drops it on entry (observed, verification/2026-09-27-windows-excel). Each token is written in the engine's
// invariant spelling — declared function names, References, booleans and Error Values in upper case, numbers as
// Excel writes them (at most 15 significant digits, written out in full where Excel writes them so: `=1E15` is
// stored as `=1000000000000000` and `=.5` as `=0.5`), a structured reference in single brackets, a Reference
// spanning every row or every column in its whole-column or whole-row form. Rewriting References, on an
// insertion, a deletion, a copy or a fill, replaces only the Reference tokens and leaves every other character
// where it was — except that a Reference rewritten to `#REF!` takes the whitespace before it with it, as Excel's
// does.
import { Culture } from '../culture';
import { equalsIgnoreCase } from '../dotnet';
import { formulaConstantLongest, writtenText } from '../number-text';
import { findFunction } from './function-library';
import { isFormulaWhitespace, tokenize, type Token } from './lexer';
import { quoteSheetName, type Reference } from './reference';

/** The stored text of a Formula already read into `tokens`. */
export function normalizeFormula(formula: string, tokens: readonly Token[]): string {
    let text = '=';
    let at = 1;
    for (let i = 0; i < tokens.length; i++) {
        const token = tokens[i] as Token; // oxlint-disable-line typescript/no-unsafe-type-assertion -- i is within the tokens
        // Excel keeps whitespace only where a token follows it; the end is not one, nor a comma.
        // A carriage return with its line feed is kept as the line feed alone, as Excel keeps it.
        if (token.kind !== 'end' && token.kind !== 'comma') text += formula.slice(at, token.position).replaceAll('\r\n', '\n');
        text += tokenText(formula, token, tokens[i + 1]);
        at = token.position + token.length;
    }
    return text;
}

/**
 * The stored text with every Reference passed through `map`: the new Reference, or `undefined` where the cells it
 * named no longer exist, written `#REF!` as Excel writes it, after the Reference's Sheet qualifier when it had
 * one (`Sheet1!#REF!`). `requalify`, when given, maps the qualifier of a `#REF!` already written so (a rename),
 * `undefined` for a Sheet that is gone. The same string when nothing changed.
 */
export function rewriteReferences(stored: string, map: (reference: Reference) => Reference | undefined,
    requalify?: (sheet: string) => string | undefined): string {
    const tokens = tokenize(stored, 1);
    let text = '';
    let changed = false;
    let at = 0;
    for (const token of tokens) {
        if (token.kind === 'error' && token.sheetName !== undefined && requalify !== undefined) {
            const qualifier = token.sheetName;
            const renamed = requalify(qualifier);
            if (renamed === qualifier) continue;
            changed = true;
            text += stored.slice(at, token.position);
            // A qualifier requalified to none names a Sheet that is gone: what is left is #REF! (ADR-0331).
            if (renamed !== undefined) text += `${quoteSheetName(renamed)}!`;
            text += '#REF!';
            at = token.position + token.length;
            continue;
        }
        if (token.kind !== 'reference' || token.reference === undefined) continue;
        const reference = token.reference;
        const mapped = map(reference);
        if (mapped?.equals(reference) === true) continue;
        changed = true;
        if (mapped === undefined) {
            // The whitespace before a Reference that becomes #REF! goes with it (observed:
            // "=  A5  *  C1" with row 5 deleted is "=#REF!  *  C1"); its qualifier stays
            // (observed: "=Sheet1!A5*2" is "=Sheet1!#REF!*2").
            let kept = token.position;
            while (kept > at && isFormulaWhitespace(stored.charAt(kept - 1))) kept--;
            text += stored.slice(at, kept);
            // A qualifier requalified to none names a Sheet that is gone, and goes with it (ADR-0331); a 3D
            // Reference's span goes whole, as Excel's #REF!B2 in a copy shows (ADR-0331 §7).
            const sheet = reference.sheetName;
            if (sheet !== undefined && !reference.isThreeD && (requalify === undefined || requalify(sheet) !== undefined)) text += `${quoteSheetName(sheet)}!`;
            text += '#REF!';
        } else {
            text += stored.slice(at, token.position) + mapped.write();
        }
        at = token.position + token.length;
    }
    return changed ? text + stored.slice(at) : stored;
}

/**
 * The stored text with every structured reference written as `spell` spells its table and column — the Linked
 * Table's declared case (ADR-0047, ADR-0049) — or left as it is where it gives `undefined`. The same string when
 * nothing changed.
 */
export function rewriteStructuredReferences(stored: string,
    spell: (table: string, column: string) => { readonly table: string; readonly column: string } | undefined): string {
    const tokens = tokenize(stored, 1);
    let text = '';
    let changed = false;
    let at = 0;
    for (const token of tokens) {
        if (token.kind !== 'structuredReference' || token.tableName === undefined) continue;
        const spelled = spell(token.tableName, token.text);
        if (spelled === undefined) continue;
        if (spelled.table === token.tableName && spelled.column === token.text) continue;
        changed = true;
        text += stored.slice(at, token.position) + structuredReferenceText(spelled.table, spelled.column);
        at = token.position + token.length;
    }
    return changed ? text + stored.slice(at) : stored;
}

function tokenText(formula: string, token: Token, next: Token | undefined): string {
    switch (token.kind) {
        case 'number':
            // As Excel writes a number constant back (ADR-0047): the form it gives the same number
            // written into text, which the observed cases agree on (ARITH-073..075, TEXT-057/058).
            return writtenText(token.number, Culture.invariant, formulaConstantLongest);
        case 'text':
            return `"${token.text.replaceAll('"', '""')}"`;
        case 'error': {
            const error = token.error;
            if (error === undefined) throw new Error('Internal: an error token holds its Error Value.');
            return token.sheetName === undefined ? error : `${quoteSheetName(token.sheetName)}!${error}`;
        }
        case 'reference':
            if (token.reference === undefined) throw new Error('Internal: a reference token holds its Reference.');
            return token.reference.write();
        case 'structuredReference':
            if (token.tableName === undefined) throw new Error('Internal: a structured reference token holds its table.');
            return structuredReferenceText(token.tableName, token.text);
        case 'name': {
            const call = next !== undefined && next.kind === 'leftParenthesis' && !next.afterSpace;
            if (call) return findFunction(token.text)?.name ?? token.text;
            if (equalsIgnoreCase(token.text, 'TRUE')) return 'TRUE';
            if (equalsIgnoreCase(token.text, 'FALSE')) return 'FALSE';
            return token.text;
        }
        case 'end':
            return '';
        case 'operator':
        case 'leftParenthesis':
        case 'rightParenthesis':
        case 'comma':
            return formula.slice(token.position, token.position + token.length);
        default:
            throw new Error(`Internal: ${String(token.kind)} is no kind of token.`);
    }
}

/**
 * A structured reference into a Linked Table, `Table[Column]` (ADR-0049), in single brackets whatever the
 * column's name holds, with `'` before each of `[ ] # ' @`: Excel writes `Positions[[Market Value]]` back as
 * `Positions[Market Value]` and `Positions[[Rate'#]]` as `Positions[Rate'#]` (observed,
 * verification/2026-09-27-windows-excel). Double brackets are still read. An `@` is escaped too, as Excel's own
 * form escapes it: unescaped, the lexer refuses it, and Excel reads `[@Column]` as the row the Formula stands in.
 */
export function structuredReferenceText(table: string, column: string): string {
    let text = `${table}[`;
    for (let i = 0; i < column.length; i++) {
        const c = column.charAt(i);
        if (c === '[' || c === ']' || c === '#' || c === "'" || c === '@') text += "'";
        text += c;
    }
    return `${text}]`;
}
