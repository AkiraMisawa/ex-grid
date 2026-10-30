// LEFT, RIGHT, MID, LEN, TRIM, CONCAT, TEXT, TEXTJOIN, CONCATENATE, SUBSTITUTE, REPLACE, FIND, REPT, EXACT,
// NUMBERVALUE and VALUE (C#'s Functions.Text.cs); UPPER, LOWER, PROPER and SEARCH as the Windows runs observed
// them (Functions.ObservedText.cs). Characters are counted as Excel counts them, in UTF-16 code units.
import { CellAddress } from '../../cell-address';
import { parseNumber } from '../../constant-parser';
import { isAscii, isAsciiDigit, isAsciiLetter, isWhiteSpace } from '../../dotnet';
import { NumberFormat } from '../../number-format';
import { generalFitted, generalLimit } from '../../number-text';
import { Value } from '../../value';
import type { FunctionCall } from '../function-definition';
import { Operand } from '../operand';
import { powerOfTen } from '../rounding';
import { isArray, numberArgument, numberResult, scalarArgument, textArgument, truthOf, unlifted } from './shared';

/** The most characters a cell holds; CONCAT is `#VALUE!` past it. */
export const textLimit = 32_767;

function textResult(text: string): Operand {
    return Operand.of(Value.fromText(text));
}

export function left(call: FunctionCall): Operand {
    return edge(call, true);
}

export function right(call: FunctionCall): Operand {
    return edge(call, false);
}

/** LEFT and RIGHT: `num_chars` is 1 when left out, 0 when given empty, truncated to a whole number, and `#VALUE!` below 0. */
function edge(call: FunctionCall, fromStart: boolean): Operand {
    const text = textArgument(call, 0);
    if (text instanceof Operand) return text;
    let count = 1;
    if (call.count > 1) {
        count = 0;
        if (call.has(1)) {
            const given = numberArgument(call, 1);
            if (given instanceof Operand) return given;
            count = given;
        }
    }
    count = Math.trunc(count);
    if (count < 0) return Operand.ofError('#VALUE!');
    const length = Math.min(count, text.length);
    return textResult(fromStart ? text.slice(0, length) : text.slice(text.length - length));
}

/** MID: `start_num` below 1 or `num_chars` below 0 is `#VALUE!`; a start past the end gives empty text. */
export function mid(call: FunctionCall): Operand {
    const text = textArgument(call, 0);
    if (text instanceof Operand) return text;
    const startGiven = numberArgument(call, 1);
    if (startGiven instanceof Operand) return startGiven;
    const countGiven = numberArgument(call, 2);
    if (countGiven instanceof Operand) return countGiven;
    const start = Math.trunc(startGiven);
    const count = Math.trunc(countGiven);
    if (start < 1 || count < 0) return Operand.ofError('#VALUE!');
    if (start > text.length) return textResult('');
    const from = start - 1;
    const length = Math.min(count, text.length - from);
    return textResult(text.slice(from, from + length));
}

export function len(call: FunctionCall): Operand {
    const text = textArgument(call, 0);
    return text instanceof Operand ? text : Operand.of(Value.fromNumber(text.length));
}

/** TRIM removes U+0020 from both ends and leaves one between words; other white space, the non-breaking space among it, is kept. */
export function trim(call: FunctionCall): Operand {
    const text = textArgument(call, 0);
    return text instanceof Operand ? text : textResult(text.split(' ').filter(word => word.length > 0).join(' '));
}

/**
 * CONCAT joins its arguments in order, and a range's cells row by row; blanks add nothing, a kept blank among
 * them (ADR-0125). An Error Value anywhere is the result, the first one found. A result longer than a cell holds
 * is `#VALUE!`.
 */
export function concat(call: FunctionCall): Operand {
    const evaluator = call.evaluator;
    let joined = '';
    for (let i = 0; i < call.count; i++) {
        const operand = call.keepingBlanks(i);
        switch (operand.kind) {
            case 'area':
            case 'column':
            case 'array':
            case 'threeD':
                for (const value of evaluator.rangeValues(operand)) {
                    if (value.isError) return Operand.of(value);
                    joined += evaluator.toText(value);
                }
                break;
            case 'missing':
                break;
            case 'scalar':
                if (operand.scalar?.isError === true) return operand;
                joined += evaluator.toText(operand.scalar);
                break;
            default: throw new Error(`Internal: ${String(operand.kind)} is no kind of operand.`);
        }
        if (joined.length > textLimit) return Operand.ofError('#VALUE!');
    }
    return textResult(joined);
}

/**
 * TEXT (ADR-0120): the Value as a cell in the format shows it under the Sheet's culture, the code read in the
 * invariant spelling and as written. A blank is 0, and text that reads as a number is that number. A number in
 * General, or under a code of a text section alone such as `@`, is General at its own limit, 11 characters
 * beside the minus sign (`textGeneral`). An empty code shows a number as empty text (TEXTFN-018). A number the
 * format cannot show is `#VALUE!` (TEXTFN-015). A code the cell formats refuse is refused. The code's colour is
 * dropped.
 */
export function formatAsText(call: FunctionCall): Operand {
    const value = scalarArgument(call, 0);
    if (value instanceof Operand) return value;
    const code = textArgument(call, 1);
    if (code instanceof Operand) return code;
    let format = NumberFormat.general;
    if (code.length > 0) {
        const read = NumberFormat.tryParseAsWritten(code).format;
        if (read === undefined) return call.evaluator.refuse();
        format = read;
    }
    const culture = call.evaluator.culture;
    let shown: Value;
    if (value === undefined) shown = Value.fromNumber(0);
    else if (value.kind === 'text') {
        const number = parseNumber(value.text, culture);
        shown = number === undefined ? value : Value.fromNumber(number);
    } else shown = value;
    if (shown.kind === 'number' && code.length === 0) return textResult('');
    if (shown.kind === 'number' && format.showsNumbersAsGeneral) {
        const general = generalFitted(shown.number, generalLimit + (shown.number < 0 ? 1 : 0), culture);
        return general === undefined ? Operand.ofError('#VALUE!') : textResult(general);
    }
    const formatted = format.format(shown, culture);
    return formatted.cannotShow ? Operand.ofError('#VALUE!') : textResult(formatted.text);
}

/**
 * TEXTJOIN: the texts in order, a range's cells row by row, with the delimiter between them; with
 * `ignore_empty` TRUE, blanks and empty text are left out, and otherwise each is an empty item. A delimiter of
 * more than one cell is refused; a 3D Reference's cells are delimiters taken in turn, Sheet by Sheet (ADR-0331
 * §7). An Error Value is the result, and a result longer than a cell holds is `#VALUE!`.
 */
export function textJoin(call: FunctionCall): Operand {
    const evaluator = call.evaluator;
    const delimiters: string[] = [];
    const given = call.oneValue(0);
    if (given.kind === 'threeD') {
        for (const item of itemsOf(given)) {
            if (item?.isError === true) return Operand.of(item);
            delimiters.push(evaluator.toText(item));
        }
    } else {
        if (isArray(given)) return unlifted(call);
        const value = evaluator.scalarOf(given);
        if (value?.isError === true) return Operand.of(value);
        delimiters.push(evaluator.toText(value));
    }
    const ignoreValue = scalarArgument(call, 1);
    if (ignoreValue instanceof Operand) return ignoreValue;
    let ignoreEmpty = false;
    if (ignoreValue !== undefined) {
        const truth = truthOf(ignoreValue);
        if (truth === undefined) return Operand.ofError('#VALUE!');
        ignoreEmpty = truth;
    }
    let joined = '';
    let joins = 0;
    for (let i = 2; i < call.count; i++) {
        for (const item of itemsOf(call.operand(i))) {
            if (item?.isError === true) return Operand.of(item);
            const text = evaluator.toText(item);
            if (ignoreEmpty && text.length === 0) continue;
            if (joins > 0) joined += delimiters[(joins - 1) % delimiters.length] as string; // oxlint-disable-line typescript/no-unsafe-type-assertion -- an index modulo the count
            joined += text;
            joins++;
            if (joined.length > textLimit) return Operand.ofError('#VALUE!');
        }
    }
    return textResult(joined);
}

/** Every cell of a range, blanks included as `undefined`, row by row; a scalar as itself. */
function* itemsOf(operand: Operand): Generator<Value | undefined> {
    switch (operand.kind) {
        case 'area': {
            const area = operand.area;
            const cells = operand.cellsOf();
            for (let row = area.row1; row <= area.row2; row++) {
                for (let column = area.column1; column <= area.column2; column++) yield cells.read(new CellAddress(row, column));
            }
            return;
        }
        case 'column':
            yield* operand.columnOf();
            return;
        case 'threeD': {
            // Sheet by Sheet in tab order, each row by row (ADR-0331 §7).
            const area = operand.area;
            for (const sheet of operand.sheetsOf()) {
                for (let row = area.row1; row <= area.row2; row++) {
                    for (let column = area.column1; column <= area.column2; column++) yield sheet.read(new CellAddress(row, column));
                }
            }
            return;
        }
        case 'array':
            yield* operand.arrayOf().all();
            return;
        case 'missing':
            yield undefined;
            return;
        case 'scalar':
            yield operand.scalar;
            return;
        default: throw new Error(`Internal: ${String(operand.kind)} is no kind of operand.`);
    }
}

/** CONCATENATE: its arguments joined, each one Value. A range of more than one cell is refused; CONCAT is the one that takes ranges. */
export function concatenate(call: FunctionCall): Operand {
    let joined = '';
    for (let i = 0; i < call.count; i++) {
        if (!call.has(i)) continue;
        const text = textArgument(call, i);
        if (text instanceof Operand) return text;
        joined += text;
        if (joined.length > textLimit) return Operand.ofError('#VALUE!');
    }
    return textResult(joined);
}

/**
 * SUBSTITUTE: `new_text` for every `old_text`, case-sensitive and left to right without overlapping, or for the
 * `instance_num`-th alone, the occurrences then counted at every character, overlapping ones included
 * (SUBSTITUTE-005). An `instance_num` below 1 is `#VALUE!`, and an empty `old_text` changes nothing.
 */
export function substitute(call: FunctionCall): Operand {
    const text = textArgument(call, 0);
    if (text instanceof Operand) return text;
    const old = textArgument(call, 1);
    if (old instanceof Operand) return old;
    const replacement = textArgument(call, 2);
    if (replacement instanceof Operand) return replacement;
    if (call.has(3)) {
        const given = numberArgument(call, 3);
        if (given instanceof Operand) return given;
        const instance = Math.trunc(given);
        if (instance < 1) return Operand.ofError('#VALUE!');
        if (old.length === 0) return textResult(text);
        let at = -1;
        for (let n = 0; n < instance; n++) {
            at = text.indexOf(old, at + 1);
            if (at < 0) return textResult(text);
        }
        return textResult(text.slice(0, at) + replacement + text.slice(at + old.length));
    }
    return textResult(old.length === 0 ? text : text.split(old).join(replacement));
}

/** REPLACE: `num_chars` characters from `start_num` replaced by `new_text`; a start below 1 or a count below 0 is `#VALUE!`, and a start past the end appends. */
export function replace(call: FunctionCall): Operand {
    const text = textArgument(call, 0);
    if (text instanceof Operand) return text;
    const startGiven = numberArgument(call, 1);
    if (startGiven instanceof Operand) return startGiven;
    const countGiven = numberArgument(call, 2);
    if (countGiven instanceof Operand) return countGiven;
    const replacement = textArgument(call, 3);
    if (replacement instanceof Operand) return replacement;
    const start = Math.trunc(startGiven);
    const count = Math.trunc(countGiven);
    if (start < 1 || count < 0) return Operand.ofError('#VALUE!');
    const from = Math.min(start - 1, text.length);
    const length = Math.min(count, text.length - from);
    const result = text.slice(0, from) + replacement + text.slice(from + length);
    return result.length > textLimit ? Operand.ofError('#VALUE!') : textResult(result);
}

/**
 * FIND: the position, from 1, of `find_text` in `within_text` from `start_num`, case-sensitive and without
 * wildcards; not found is `#VALUE!`, and so is a start below 1 or past the text's length. An empty `find_text`
 * is found at the start.
 */
export function find(call: FunctionCall): Operand {
    const wanted = textArgument(call, 0);
    if (wanted instanceof Operand) return wanted;
    const within = textArgument(call, 1);
    if (within instanceof Operand) return within;
    let start = 1;
    if (call.has(2)) {
        const given = numberArgument(call, 2);
        if (given instanceof Operand) return given;
        start = given;
    }
    start = Math.trunc(start);
    if (start < 1 || start > within.length) return Operand.ofError('#VALUE!');
    if (wanted.length === 0) return Operand.of(Value.fromNumber(start));
    const at = within.indexOf(wanted, start - 1);
    return at < 0 ? Operand.ofError('#VALUE!') : Operand.of(Value.fromNumber(at + 1));
}

/** REPT: the text repeated, the count truncated; below 0, or a result longer than a cell holds, `#VALUE!`. */
export function rept(call: FunctionCall): Operand {
    const text = textArgument(call, 0);
    if (text instanceof Operand) return text;
    const given = numberArgument(call, 1);
    if (given instanceof Operand) return given;
    const times = Math.trunc(given);
    if (times < 0 || text.length * times > textLimit) return Operand.ofError('#VALUE!');
    return textResult(text.repeat(text.length === 0 ? 0 : times));
}

/** EXACT: whether the two texts are the same, case and all. */
export function exact(call: FunctionCall): Operand {
    const first = textArgument(call, 0);
    if (first instanceof Operand) return first;
    const second = textArgument(call, 1);
    if (second instanceof Operand) return second;
    return Operand.of(Value.fromBoolean(first === second));
}

/**
 * NUMBERVALUE: the text read as a number with the separators given, the first character of each; left out, the
 * Sheet's culture's. Spaces are ignored, and so are group separators before the decimal one; each `%` at the
 * end divides by 100; empty text is 0. A second decimal separator, a group separator after it, or anything else
 * that is no digit is `#VALUE!`.
 */
export function numberValue(call: FunctionCall): Operand {
    const text = textArgument(call, 0);
    if (text instanceof Operand) return text;
    const data = call.evaluator.culture.data;
    let decimalSeparator = data.numberDecimalSeparator.charAt(0);
    let groupSeparator = data.numberGroupSeparator.length > 0 ? data.numberGroupSeparator.charAt(0) : '\0';
    if (call.has(1)) {
        const given = textArgument(call, 1);
        if (given instanceof Operand) return given;
        if (given.length === 0) return Operand.ofError('#VALUE!');
        decimalSeparator = given.charAt(0);
    }
    if (call.has(2)) {
        const given = textArgument(call, 2);
        if (given instanceof Operand) return given;
        if (given.length === 0) return Operand.ofError('#VALUE!');
        groupSeparator = given.charAt(0);
    }
    if (decimalSeparator === groupSeparator) return Operand.ofError('#VALUE!');

    let digits = '';
    let percents = 0;
    let seenDecimal = false;
    for (let i = 0; i < text.length; i++) {
        const c = text.charAt(i);
        const code = text.charCodeAt(i);
        if (isWhiteSpace(code)) continue;
        if (percents > 0 && c !== '%') return Operand.ofError('#VALUE!');
        if (c === '%') percents++;
        else if (c === decimalSeparator) {
            if (seenDecimal) return Operand.ofError('#VALUE!');
            seenDecimal = true;
            digits += '.';
        } else if (c === groupSeparator) {
            if (seenDecimal) return Operand.ofError('#VALUE!');
        } else if (isAsciiDigit(code) || ((c === '-' || c === '+') && digits.length === 0)) digits += c;
        else return Operand.ofError('#VALUE!');
    }
    if (digits.length === 0) return percents === 0 ? Operand.of(Value.fromNumber(0)) : Operand.ofError('#VALUE!');
    // What double.TryParse takes with a leading sign and a decimal point: at least one digit.
    if (!/[0-9]/.test(digits)) return Operand.ofError('#VALUE!');
    return numberResult(Number(digits) / powerOfTen(2 * percents));
}

/**
 * VALUE (ADR-0124): text read as a number under the Sheet's culture, as the arithmetic operators read text — a
 * date or a time typed as one is its serial. A number is itself, a blank 0; a boolean, and text that reads as no
 * number, are `#VALUE!`.
 */
export function valueOf(call: FunctionCall): Operand {
    const value = scalarArgument(call, 0);
    if (value instanceof Operand) return value;
    if (value === undefined) return Operand.of(Value.fromNumber(0));
    if (value.kind === 'number') return Operand.of(value);
    if (value.kind === 'text') {
        const parsed = parseNumber(value.text, call.evaluator.culture);
        if (parsed !== undefined) return Operand.of(Value.fromNumber(parsed));
    }
    return Operand.ofError('#VALUE!');
}

// ---- Observed case and search (2026-10-03 Windows observations, ADR-0047) ----------------------------------
// A character outside the domains observed, and a sigma context not observed, refuse the Formula.

export function upper(call: FunctionCall): Operand {
    const text = textArgument(call, 0);
    if (text instanceof Operand) return text;
    let result = '';
    for (let i = 0; i < text.length; i++) {
        const c = text.charAt(i);
        if (!observedCaseCharacter(c)) return call.evaluator.refuse();
        result += observedUpper(c);
    }
    return textResult(result);
}

export function lower(call: FunctionCall): Operand {
    const text = textArgument(call, 0);
    if (text instanceof Operand) return text;
    for (let i = 0; i < text.length; i++) {
        const c = text.charAt(i);
        if (!observedCaseCharacter(c) && c !== 'Α' && c !== 'Ο') return call.evaluator.refuse();
    }
    if (!observedSigmaContexts(text)) return call.evaluator.refuse();
    let result = '';
    for (let i = 0; i < text.length; i++) {
        const c = text.charAt(i);
        if (c === 'Σ') {
            const next = text.charAt(i + 1);
            result += next === 'Α' || next === 'Ο' || next === 'Σ' ? 'σ' : 'ς';
        } else result += observedLower(c);
    }
    return textResult(result);
}

/** LOWER's contextual sigma is observed only in uppercase Greek words separated by spaces. */
function observedSigmaContexts(text: string): boolean {
    for (const word of text.split(' ')) {
        if (word.includes('Σ') && /[^ΑΟΣ]/.test(word)) return false;
    }
    return true;
}

function observedLower(c: string): string {
    const code = c.charCodeAt(0);
    if (code >= 0x41 && code <= 0x5A) return String.fromCharCode(code + 32);
    switch (c) {
        case 'É': return 'é';
        case 'İ': return 'i';
        case 'Ǳ':
        case 'ǲ':
            return 'ǳ';
        case 'Α': return 'α';
        case 'Ο': return 'ο';
        default: return c;
    }
}

export function proper(call: FunctionCall): Operand {
    const text = textArgument(call, 0);
    if (text instanceof Operand) return text;
    for (let i = 0; i < text.length; i++) {
        if (!observedCaseCharacter(text.charAt(i))) return call.evaluator.refuse();
    }
    // Only isolated sigma letters were observed for PROPER; its word context is separate from LOWER's.
    for (const word of text.split(' ')) {
        if (word.length > 1 && /[σςΣ]/.test(word)) return call.evaluator.refuse();
    }
    let result = '';
    let initial = true;
    for (let i = 0; i < text.length; i++) {
        const c = text.charAt(i);
        const lowered = observedLower(c);
        result += initial ? observedUpper(lowered) : lowered;
        initial = !observedCaseLetter(c);
    }
    return textResult(result);
}

function observedCaseLetter(c: string): boolean {
    return isAsciiLetter(c.charCodeAt(0)) || 'éÉßẞıİǱǲǳﬃσςΣ'.includes(c);
}

/**
 * SEARCH has its own observed case folding: dotless i does not equal i, although UPPER maps both to I. Positions
 * and question marks are answered only for the admitted BMP text.
 */
export function searchText(call: FunctionCall): Operand {
    const wanted = textArgument(call, 0);
    if (wanted instanceof Operand) return wanted;
    const within = textArgument(call, 1);
    if (within instanceof Operand) return within;
    let start = 1;
    if (call.has(2)) {
        const given = numberArgument(call, 2);
        if (given instanceof Operand) return given;
        start = given;
    }
    start = Math.trunc(start);
    if (!allSearchCharacters(wanted) || !allSearchCharacters(within)) return call.evaluator.refuse();
    if (start < 1 || start > within.length) return Operand.ofError('#VALUE!');
    if (wanted.length === 0) return Operand.of(Value.fromNumber(start));

    const tokens: { character: string; wild: boolean }[] = [];
    for (let i = 0; i < wanted.length; i++) {
        const c = wanted.charAt(i);
        const next = wanted.charAt(i + 1);
        if (c === '~' && (next === '*' || next === '?' || next === '~')) {
            tokens.push({ character: next, wild: false });
            i++;
        } else tokens.push({ character: c, wild: c === '*' || c === '?' });
    }

    // A suffix of the pattern is matched at every text position. Once the pattern ends, remaining text is
    // allowed: SEARCH finds a substring, unlike a full-cell criterion.
    let next: boolean[] = Array.from({ length: within.length + 1 }, () => true);
    let current: boolean[] = Array.from({ length: within.length + 1 }, () => false);
    for (let p = tokens.length - 1; p >= 0; p--) {
        const { character, wild } = tokens[p] as { character: string; wild: boolean }; // oxlint-disable-line typescript/no-unsafe-type-assertion -- p is within the tokens
        const star = wild && character === '*';
        current[within.length] = star && next[within.length] === true;
        for (let t = within.length - 1; t >= start - 1; t--) {
            current[t] = star
                ? next[t] === true || current[t + 1] === true
                : next[t + 1] === true && (wild || observedSearchFold(character) === observedSearchFold(within.charAt(t)));
        }
        [current, next] = [next, current];
    }
    for (let i = start - 1; i < within.length; i++) {
        if (next[i] === true) return Operand.of(Value.fromNumber(i + 1));
    }
    return Operand.ofError('#VALUE!');
}

function allSearchCharacters(text: string): boolean {
    for (let i = 0; i < text.length; i++) {
        if (!isAscii(text.charCodeAt(i)) && !'éÉßıİσςΣ'.includes(text.charAt(i))) return false;
    }
    return true;
}

function observedSearchFold(c: string): string {
    const code = c.charCodeAt(0);
    if (code >= 0x61 && code <= 0x7A) return String.fromCharCode(code - 32);
    switch (c) {
        case 'é': return 'É';
        case 'σ':
        case 'ς':
            return 'Σ';
        default: return c;
    }
}

/** An explicit alphabet keeps the result independent of an operating system's Unicode tables. */
function observedCaseCharacter(c: string): boolean {
    return isAscii(c.charCodeAt(0)) || 'éÉßẞıİǱǲǳﬃ σςΣ'.includes(c);
}

function observedUpper(c: string): string {
    const code = c.charCodeAt(0);
    if (code >= 0x61 && code <= 0x7A) return String.fromCharCode(code - 32);
    switch (c) {
        case 'é': return 'É';
        case 'ı': return 'I';
        case 'ǲ':
        case 'ǳ':
            return 'Ǳ';
        case 'σ':
        case 'ς':
            return 'Σ';
        default: return c;
    }
}
