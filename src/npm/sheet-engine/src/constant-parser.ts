// Reads a typed constant under a Sheet's culture, so that it can be recorded parsed (ADR-0048): a number with
// the culture's separators, a percentage, a date in the culture's day order, a time, a boolean, one of Excel's
// Error Values, or else text.
import { dayOrder, type Culture } from './culture';
import { serialFromDate } from './date-serial';
import { equalsIgnoreCase, isAsciiDigit, isWhiteSpace, trimEndChar, upperInvariant } from './dotnet';
import { abbreviatedMonthNamesOf, NumberFormat } from './number-format';
import { elementAt } from './must';
import { parseTypedError, Value } from './value';

/** A typed constant, and the number format Excel applies to a General cell when it is typed (a date's, a percentage's). */
export interface ParsedConstant {
    readonly value: Value;
    readonly implied: NumberFormat | undefined;
}

/** How a typed number was written, which decides the format Excel gives a General cell for it. */
interface NumberShape {
    percent: boolean;
    decimals: boolean;
    grouped: boolean;
    exponent: boolean;
}

/** A number read, and how it was written. */
interface ReadNumber {
    readonly number: number;
    readonly shape: NumberShape;
}

/**
 * The year a typed date takes when it is written without one: the current year, as Excel takes it. The C#
 * engine reads `DateTime.Today`; here it is the local calendar's year, read when a constant is parsed.
 */
export function currentYear(): number {
    return new Date().getFullYear();
}

/** A typed constant, read as Excel reads it under the culture. */
export function parseConstant(typed: string, culture: Culture): Value {
    return parseConstantWithFormat(typed, culture).value;
}

/** A typed constant, and the number format Excel applies to a General cell when it is typed. */
export function parseConstantWithFormat(typed: string, culture: Culture): ParsedConstant {
    // A leading apostrophe makes the rest text, whatever it looks like, as in Excel.
    if (typed.length > 0 && typed.charAt(0) === "'") return { value: Value.fromText(typed.slice(1)), implied: undefined };
    if (equalsIgnoreCase(typed, 'TRUE')) return { value: Value.fromBoolean(true), implied: undefined };
    if (equalsIgnoreCase(typed, 'FALSE')) return { value: Value.fromBoolean(false), implied: undefined };
    const error = parseTypedError(typed);
    if (error !== undefined) return { value: Value.fromError(error), implied: undefined };
    const trimmed = trimSpaces(typed);
    const number = readNumber(trimmed, culture);
    if (number !== undefined) return { value: Value.fromNumber(number.number), implied: implied(number.shape) };
    const dollars = readDollars(trimmed, culture);
    if (dollars !== undefined) {
        return {
            value: Value.fromNumber(dollars.number),
            implied: NumberFormat.parse(dollars.shape.decimals ? '$#,##0.00_);[Red]($#,##0.00)' : '$#,##0_);[Red]($#,##0)'),
        };
    }
    const dateTime = parseDateTime(trimmed, culture, currentYear());
    if (dateTime !== undefined) return { value: Value.fromNumber(dateTime.serial), implied: dateTime.format };
    return { value: Value.fromText(typed), implied: undefined };
}

/**
 * The format a typed number gives a General cell, as Excel was observed to give it: a percentage `0%` or
 * `0.00%`; an exponent `0.00E+00`; thousands separators `#,##0`, or `#,##0.00` with decimals. Otherwise none.
 */
function implied(shape: NumberShape): NumberFormat | undefined {
    if (shape.percent) return NumberFormat.parse(shape.decimals ? '0.00%' : '0%');
    if (shape.exponent) return NumberFormat.parse('0.00E+00');
    if (shape.grouped) return NumberFormat.parse(shape.decimals ? '#,##0.00' : '#,##0');
    return undefined;
}

/** `string.Trim(' ')`: spaces alone, at both ends. */
function trimSpaces(text: string): string {
    return text.replace(/^ +| +$/g, '');
}

/**
 * A dollar amount under a culture whose currency is written `$5` (en-US): `$` with an optional `-` before or
 * after it, then an unsigned number with no percentage or exponent (TYPED-021, TYPED-047, TYPED-052). Any
 * other currency, or another sign around the symbol, stays text until Excel's reading of it is observed.
 */
function readDollars(text: string, culture: Culture): ReadNumber | undefined {
    if (culture.data.currencySymbol !== '$' || culture.data.currencyPositivePattern !== 0) return undefined;
    const negative = text.startsWith('-$') || text.startsWith('$-');
    const amount = negative ? text.slice(2) : text.startsWith('$') ? text.slice(1) : undefined;
    if (amount === undefined || amount.length === 0 || !isAsciiDigit(amount.charCodeAt(0))) return undefined;
    const read = readNumber(amount, culture);
    if (read === undefined || read.shape.percent || read.shape.exponent) return undefined;
    return negative ? { number: -read.number, shape: read.shape } : read;
}

/** Whether the typed text is a number written without `%`, a currency symbol or a date's shape: what Excel's automatic percent entry divides by 100. */
export function isPlainNumber(typed: string, culture: Culture): boolean {
    const read = readNumber(trimSpaces(typed), culture);
    return read !== undefined && !read.shape.percent;
}

/** Text read as a number the way a typed number is: what Excel's arithmetic does with numeric text; `undefined` where it is none. */
export function parseNumber(text: string, culture: Culture): number | undefined {
    const trimmed = trimSpaces(text);
    const number = readNumber(trimmed, culture);
    if (number !== undefined) return number.number;
    // A dollar amount reads as it does typed (TYPED-021): "$1,000" is 1000 to VALUE and to the operators.
    const dollars = readDollars(trimmed, culture);
    if (dollars !== undefined) return dollars.number;
    return parseDateTime(trimmed, culture, currentYear())?.serial;
}

/**
 * A number under the culture: an optional sign or parentheses for a negative, digits with the culture's group
 * separator in groups of three, the culture's decimal separator, an optional exponent, an optional trailing
 * `%`. Digits past the fifteenth significant one become zeros, as Excel keeps fifteen.
 */
function readNumber(text: string, culture: Culture): ReadNumber | undefined {
    if (text.length === 0) return undefined;
    const shape: NumberShape = { percent: false, decimals: false, grouped: false, exponent: false };
    const decimalSeparator = culture.data.numberDecimalSeparator;
    const groupSeparator = culture.data.numberGroupSeparator;
    let i = 0;
    let negative = false;
    let parenthesised = false;
    if (text.charAt(i) === '+' || text.charAt(i) === '-') {
        negative = text.charAt(i) === '-';
        i++;
    } else if (text.charAt(i) === '(') {
        parenthesised = true;
        negative = true;
        i++;
    }

    let integer = '';
    let groupRun = -1; // digits since the last group separator; -1 before any separator
    while (i < text.length) {
        if (isAsciiDigit(text.charCodeAt(i))) {
            integer += text.charAt(i);
            if (groupRun >= 0) groupRun++;
            i++;
            continue;
        }
        const separatorLength = groupSeparatorAt(text, i, groupSeparator);
        if (separatorLength > 0 && integer.length > 0 && (groupRun === -1 ? integer.length <= 3 : groupRun === 3)) {
            groupRun = 0;
            i += separatorLength;
            continue;
        }
        break;
    }
    if (groupRun >= 0 && groupRun !== 3) return undefined;
    if (groupRun >= 0) shape.grouped = true;

    let fraction = '';
    if (text.startsWith(decimalSeparator, i)) {
        i += decimalSeparator.length;
        shape.decimals = true;
        while (i < text.length && isAsciiDigit(text.charCodeAt(i))) fraction += text.charAt(i++);
    }
    if (integer.length === 0 && fraction.length === 0) return undefined;

    let exponent = 0;
    if (i < text.length && (text.charAt(i) === 'E' || text.charAt(i) === 'e')) {
        let j = i + 1;
        let exponentNegative = false;
        if (j < text.length && (text.charAt(j) === '+' || text.charAt(j) === '-')) {
            exponentNegative = text.charAt(j) === '-';
            j++;
        }
        const start = j;
        while (j < text.length && isAsciiDigit(text.charCodeAt(j))) j++;
        if (j === start || j - start > 4) return undefined;
        exponent = Number(text.slice(start, j));
        if (exponentNegative) exponent = -exponent;
        shape.exponent = true;
        i = j;
    }
    if (i < text.length && text.charAt(i) === '%') {
        shape.percent = true;
        i++;
    }
    if (parenthesised) {
        if (i >= text.length || text.charAt(i) !== ')') return undefined;
        i++;
    }
    if (i !== text.length) return undefined;

    // Excel keeps fifteen significant digits of what is typed; the rest become zeros.
    const kept = fifteenSignificant(integer + fraction);
    const spelled = `${kept.slice(0, integer.length)}${fraction.length > 0 ? `.${kept.slice(integer.length)}` : ''}E${String(exponent)}`;
    let number = Number(spelled);
    if (!Number.isFinite(number)) return undefined;
    if (shape.percent) number /= 100;
    if (negative) number = -number;
    return Number.isFinite(number) ? { number, shape } : undefined;
}

/** The digits with every one past the fifteenth significant one made a zero. */
function fifteenSignificant(digits: string): string {
    let significant = 0;
    let kept = '';
    for (const digit of digits) {
        if (significant === 0 && digit === '0') {
            kept += digit;
            continue;
        }
        kept += ++significant > 15 ? '0' : digit;
    }
    return kept;
}

/**
 * A number written in a Formula (invariant digits, an optional point and exponent), read as Excel reads it:
 * digits past the fifteenth significant one become zeros, so `=123456789012345678` holds 123456789012345000,
 * as Excel was observed to.
 */
export function parseFormulaNumber(invariant: string): number {
    let significant = 0;
    let kept = '';
    let k = 0;
    for (; k < invariant.length; k++) {
        const c = invariant.charAt(k);
        if (c === 'e' || c === 'E') break;
        if (!isAsciiDigit(invariant.charCodeAt(k)) || (significant === 0 && c === '0')) {
            kept += c;
            continue;
        }
        kept += ++significant > 15 ? '0' : c;
    }
    const number = Number(kept + invariant.slice(k));
    if (Number.isNaN(number)) throw new Error(`Internal: '${invariant}' was matched as a number.`);
    return number;
}

/** The culture's group separator at `at`; a space-like separator also accepts a plain space. */
function groupSeparatorAt(text: string, at: number, separator: string): number {
    if (text.startsWith(separator, at)) return separator.length;
    const spaceLike = separator.length === 1 && isWhiteSpace(separator.charCodeAt(0));
    const c = text.charAt(at);
    return spaceLike && (c === ' ' || c === ' ' || c === ' ') ? 1 : 0;
}

/**
 * The first two-digit year read as 19xx: 49 is 2049 and 50 is 1950, as Windows' default reads a typed date and
 * Excel was observed to take one typed with real keys (ADR-0047, second run).
 */
const firstTwentiethCenturyYear = 50;

// \d is [0-9] here, where .NET's reads any decimal digit and then refuses to parse one outside ASCII.
const datePattern = /^(?<a>[0-9]{1,4})(?<s1>[/\-.])(?<b>[0-9]{1,2})(?:\k<s1>(?<c>[0-9]{1,4}))?$/;
// .NET's \s: its white space, which JavaScript's \s does not match exactly.
const timePattern = /^(?<h>[0-9]{1,2}):(?<m>[0-9]{1,2})(?::(?<s>[0-9]{1,2}))?(?:[\f\n\r\t\v\x85\p{Z}]*(?<ampm>[AaPp][Mm]?))?$/u;
const monthNamePattern = /^(?:(?<d1>[0-9]{1,2})[-/ ](?<m1>\p{L}+\.?)(?:[-/ ](?<y1>[0-9]{2}|[0-9]{4}))?|(?<m2>\p{L}+\.?)[-/ ](?<n2>[0-9]{1,2}|[0-9]{4}))$/u;

/** A date or time read: its serial, and the format Excel gives a General cell for it. */
export interface ParsedDateTime {
    readonly serial: number;
    readonly format: NumberFormat;
}

/**
 * A date in the culture's day order (month-day-year under `en-US`, year-month-day under `ja-JP`,
 * day-month-year under `de-DE`) with `/`, `-` or the culture's own separator; a four-digit year written first
 * is always year-month-day. Without a year, the date is in `year`, as Excel takes it. A two-digit year is
 * 2000–2049 for 00–49 and 1950–1999 for 50–99. A time `h:mm[:ss] [AM|PM]`, alone or after the date.
 */
export function parseDateTime(text: string, culture: Culture, year: number): ParsedDateTime | undefined {
    let datePart = text;
    let timePart: string | undefined;
    const space = text.indexOf(' ');
    if (space > 0 && timePattern.test(trimSpaces(text.slice(space + 1)))) {
        datePart = text.slice(0, space);
        timePart = trimSpaces(text.slice(space + 1));
    }

    if (timePart === undefined) {
        const timeOnly = parseTime(text);
        if (timeOnly !== undefined) return { serial: timeOnly.fraction, format: NumberFormat.parse(timeOnly.format) };
        const named = parseMonthName(datePart, culture, year);
        if (named !== undefined) return named;
    }

    const match = datePattern.exec(datePart);
    if (match === null) return undefined;
    const groups = defined(match.groups);
    const separator = group(groups, 's1');
    if (separator === '.' && culture.data.dateSeparator !== '.') return undefined;

    const a = group(groups, 'a');
    const b = group(groups, 'b');
    const c = groups['c'];
    let y: number;
    let month: number;
    let day: number;
    const order = dayOrder(culture);
    if (a.length >= 3 || order === 'ymd') {
        if (c === undefined) return undefined;
        y = yearOf(a);
        month = Number(b);
        day = Number(c);
    } else if (order === 'dmy') {
        day = Number(a);
        month = Number(b);
        y = c === undefined ? year : yearOf(c);
    } else {
        month = Number(a);
        day = Number(b);
        y = c === undefined ? year : yearOf(c);
    }
    const whole = serialFromDate(y, month, day);
    if (whole === undefined) return undefined;

    let fraction = 0;
    if (timePart !== undefined) {
        const time = parseTime(timePart);
        if (time === undefined) return undefined;
        fraction = time.fraction;
    }
    // A date with its year records Excel's built-in short date, whatever the culture, as Excel was observed
    // to; one typed without a year shows as day and month name.
    const format = timePart !== undefined ? NumberFormat.shortDateTime : c === undefined ? NumberFormat.parse('d-mmm') : NumberFormat.shortDate;
    return { serial: whole + fraction, format };
}

function yearOf(digits: string): number {
    const value = Number(digits);
    if (digits.length > 2) return value;
    return value < firstTwentiethCenturyYear ? 2000 + value : 1900 + value;
}

/**
 * A date written with the culture's name of a month, as Excel reads it: `26-Sep` or `Sep 26` is that day in
 * `year`, shown `d-mmm` (TYPED-022); `26-Sep-2026` is shown `d-mmm-yy`, and `Sep 2026` is the first of the
 * month, shown `mmm-yy`.
 */
function parseMonthName(text: string, culture: Culture, currentYearGiven: number): ParsedDateTime | undefined {
    const match = monthNamePattern.exec(text);
    if (match === null) return undefined;
    const groups = defined(match.groups);
    let day: number;
    let year: number;
    let month: number | undefined;
    let code: string;
    const d1 = groups['d1'];
    if (d1 !== undefined) {
        month = monthNamed(group(groups, 'm1'), culture);
        day = Number(d1);
        const y = groups['y1'];
        year = y !== undefined ? yearOf(y) : currentYearGiven;
        code = y !== undefined ? 'd-mmm-yy' : 'd-mmm';
    } else {
        month = monthNamed(group(groups, 'm2'), culture);
        const number = group(groups, 'n2');
        const value = Number(number);
        if (number.length <= 2 && value >= 1 && value <= 31) {
            day = value;
            year = currentYearGiven;
            code = 'd-mmm';
        } else {
            day = 1;
            year = yearOf(number);
            code = 'mmm-yy';
        }
    }
    if (month === undefined) return undefined;
    const whole = serialFromDate(year, month, day);
    if (whole === undefined) return undefined;
    return { serial: whole, format: NumberFormat.parse(code) };
}

/** The month (1–12) the culture names with `name`, abbreviated or in full, in any case, a trailing full stop ignored. */
function monthNamed(name: string, culture: Culture): number | undefined {
    const wanted = trimEndChar(name, '.');
    const abbreviated = abbreviatedMonthNamesOf(culture);
    for (let m = 0; m < 12; m++) {
        if (equalsIgnoreCase(wanted, trimEndChar(elementAt(abbreviated, m), '.')) || equalsIgnoreCase(wanted, elementAt(culture.data.monthNames, m))) {
            return m + 1;
        }
    }
    return undefined;
}

function parseTime(text: string): { fraction: number; format: string } | undefined {
    const match = timePattern.exec(text);
    if (match === null) return undefined;
    const groups = defined(match.groups);
    let hour = Number(group(groups, 'h'));
    const minute = Number(group(groups, 'm'));
    const s = groups['s'];
    const second = s === undefined ? 0 : Number(s);
    if (minute > 59 || second > 59) return undefined;
    const ampmText = groups['ampm'];
    const ampm = ampmText === undefined ? '' : String.fromCharCode(upperInvariant(ampmText.charCodeAt(0)));
    if (ampm !== '') {
        if (hour < 1 || hour > 12) return undefined;
        hour %= 12;
        if (ampm === 'P') hour += 12;
    } else if (hour > 23) {
        return undefined;
    }
    return {
        fraction: (hour * 3600 + minute * 60 + second) / 86_400,
        format: (s !== undefined ? 'h:mm:ss' : 'h:mm') + (ampm !== '' ? ' AM/PM' : ''),
    };
}

function defined(groups: Record<string, string | undefined> | undefined): Record<string, string | undefined> {
    if (groups === undefined) throw new Error('Internal: a pattern with named groups matched without them.');
    return groups;
}

function group(groups: Record<string, string | undefined>, name: string): string {
    const value = groups[name];
    if (value === undefined) throw new Error(`Internal: the group '${name}' matched without a value.`);
    return value;
}
