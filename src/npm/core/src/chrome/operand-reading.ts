// What a filter condition's typed operand reads as (ADR-0023; tickets 94, 96, 97 and 98), as the C# core's
// FilterPanelChoices.ReadOperand reads it: the value, typed as the column's values are, or why the text was
// refused. Both the built-in panel and a substituted one read their text through here, so the same text
// means the same thing, or is refused for the same reason, in either (ADR-0010). Read in the grid's culture
// (ADR-0370 section 10), never in another culture's separators, and never guessed where it reads two ways
// (principle 1).
//
// A date is read in ticket 94's ISO forms, and in the culture's numeric order with an optional 24-hour time.
// C#'s DateTime.TryParse also reads a month's name and a 12-hour clock; the Script Core refuses those as not
// readable rather than read them its own way (ADR-0350 section 16).

import type { ColumnType, DateType } from '../query/columns';
import { displayText } from '../cells/display-text';
import { invariantLocale, gridCulture, type GridCulture } from './grid-culture';
import { defined } from '../must';

/** Why a typed operand was refused (ADR-0023; ADR-0006, note of 2026-10-01). */
export type OperandRefusal =
    /** Not refused: the text read as the column's type, or nothing was typed. */
    | 'none'
    /** Not the column's type as the culture writes it. */
    | 'notReadable'
    /** Reads as two different values, and neither is guessed (ticket 97). */
    | 'readsTwoWays'
    /** A date, but not of the column's declared date type (ADR-0023, section of 2026-10-02). */
    | 'notTheColumnsDateForm';

/** A date as it was read from text, before it is held to the column's date type. */
export interface ReadDate {
    readonly year: number;
    readonly month: number;
    readonly day: number;
    readonly hour: number;
    readonly minute: number;
    readonly second: number;
    readonly nanosecond: number;
    /** The offset written, `+09:00` or `Z`; absent where none was. */
    readonly offset?: string;
}

/** What a typed operand reads as. */
export interface OperandReading {
    /** The operand, or `undefined` where nothing was typed or the text was refused. */
    readonly value: unknown;
    /** Why the text was refused; `none` where it was not. */
    readonly refusal: OperandRefusal;
    /** For `readsTwoWays`, the two readings: a number's in the culture and as the invariant culture reads it,
     * a date's month first and day first. */
    readonly readings?: readonly [unknown, unknown];
    /** For `notTheColumnsDateForm`, what the text read as, and the column's declared date type. */
    readonly read?: ReadDate;
    readonly declared?: DateType;
}

/** The members of the Temporal namespace a date operand is made with: the browser's own, or a polyfill's.
 * The core never bundles one (ADR-0350 section 16). */
export interface TemporalLike {
    /** Makes a day, a dateOnly column's value. */
    readonly PlainDate: { from(fields: { year: number; month: number; day: number }): unknown };
    /** Makes a date and time, a dateTime column's value. */
    readonly PlainDateTime: {
        from(fields: {
            year: number; month: number; day: number; hour: number; minute: number; second: number;
            millisecond: number; microsecond: number; nanosecond: number;
        }): unknown;
    };
    /** Makes an instant in the offset it was typed with, a dateTimeOffset column's value. */
    readonly ZonedDateTime: { from(text: string): unknown };
}

const nothing: OperandReading = Object.freeze({ value: undefined, refusal: 'none' });
const notReadable: OperandReading = Object.freeze({ value: undefined, refusal: 'notReadable' });

/**
 * What a condition's typed operand reads as, for a column of `type` (ADR-0023). Empty text is no operand, not
 * a refusal. A number is read in `culture` alone, its grouping as the culture writes it; a date in ISO's forms
 * exactly, or in the culture's numeric order, and must then be of the column's `dateType`; a boolean is `true`
 * or `false`, in either case; text is itself. A date is made with `temporal`, which a date column's values
 * mean the browser has.
 */
export function readOperand(type: ColumnType, text: string, culture: GridCulture, dateType: DateType = 'dateTime',
    temporal?: TemporalLike): OperandReading {
    if (text.length === 0) return nothing;
    switch (type) {
        case 'text': return Object.freeze({ value: text, refusal: 'none' });
        case 'number': return readNumber(text, culture);
        case 'date': return readDate(text, culture, dateType, temporal);
        case 'boolean': {
            const flag = text.trim().toLowerCase();
            return flag === 'true' || flag === 'false' ? Object.freeze({ value: flag === 'true', refusal: 'none' }) : notReadable;
        }
        default: throw new RangeError(`A column's type is text, number, date or boolean, not ${JSON.stringify(type)}.`);
    }
}

/**
 * The text a condition form shows an operand in, which {@link readOperand} reads back as the same value in
 * `culture` (ADR-0006, note of 2026-10-01; ticket 96): a number in the culture's decimal separator and sign,
 * ungrouped; a date in its ISO form (ticket 94); anything else its own text. Empty for no operand.
 */
export function operandText(operand: unknown, culture: GridCulture): string {
    if (operand === null || operand === undefined) return '';
    if (typeof operand === 'number' || typeof operand === 'bigint') {
        const plain = plainNumber(operand);
        const negative = plain.startsWith('-');
        const digits = (negative ? plain.slice(1) : plain).replace('.', culture.decimal);
        return negative ? `${culture.negativeSign}${digits}` : digits;
    }
    const fractional = typeof operand === 'object' ? fractionalDateText(operand) : undefined;
    return fractional === undefined ? displayText(operand) : fractional;
}

/**
 * A date that holds a fraction of a second, in ISO's T form with the fraction written: the spaced form a cell
 * shows stops at the second, and a condition reopened in it would be applied again a fraction off, a filter
 * nobody chose. The form reads back as the same value, with its offset where it has one. `undefined` for
 * anything else.
 */
function fractionalDateText(operand: object): string | undefined {
    const nanosecond = subSecond(operand);
    if (nanosecond === undefined || nanosecond === 0) return undefined;
    const [day, time, offset] = displayText(operand).split(' ');
    const fraction = String(nanosecond).padStart(9, '0').replace(/0+$/u, '');
    return `${defined(day, 'a date\'s day')}T${defined(time, 'a date\'s time')}.${fraction}${offset === undefined ? '' : offset}`;
}

/** The nanoseconds past the second a date holds; `undefined` for a value that is no date with a time. */
function subSecond(operand: object): number | undefined {
    switch (Object.prototype.toString.call(operand)) {
        case '[object Temporal.PlainDateTime]':
        case '[object Temporal.ZonedDateTime]': {
            const milli: unknown = Reflect.get(operand, 'millisecond');
            const micro: unknown = Reflect.get(operand, 'microsecond');
            const nano: unknown = Reflect.get(operand, 'nanosecond');
            if (typeof milli !== 'number' || typeof micro !== 'number' || typeof nano !== 'number') return undefined;
            return milli * 1_000_000 + micro * 1_000 + nano;
        }
        case '[object Temporal.Instant]': {
            const epoch: unknown = Reflect.get(operand, 'epochNanoseconds');
            return typeof epoch === 'bigint' ? Number(((epoch % 1_000_000_000n) + 1_000_000_000n) % 1_000_000_000n) : undefined;
        }
        case '[object Date]': {
            const read: unknown = Reflect.get(Date.prototype, 'getUTCMilliseconds');
            if (typeof read !== 'function') return undefined;
            const milliseconds: unknown = Reflect.apply(read, operand, []);
            return typeof milliseconds === 'number' && Number.isFinite(milliseconds) ? milliseconds * 1_000_000 : undefined;
        }
        default: return undefined;
    }
}

/**
 * Why a typed operand was refused, in the built-in Chrome's words: what the text reads as, and how to type it
 * so it reads one way. `undefined` where it was not refused. A Chrome with words of its own words the refusal
 * itself.
 */
export function refusalText(reading: OperandReading, text: string, culture: GridCulture): string | undefined {
    switch (reading.refusal) {
        case 'none': return undefined;
        case 'readsTwoWays': {
            const [first, second] = reading.readings ?? refused('readsTwoWays carries its two readings');
            if (isReadDate(first) && isReadDate(second)) {
                const [one, other] = [isoDateText(first), isoDateText(second)];
                return `“${text}” reads two ways: as ${one}, and as ${other}. Type the one you mean as ${one} or ${other}.`;
            }
            const asCulture = operandText(first, culture);
            return `“${text}” reads two ways: as ${asCulture}, and as ${operandText(second, gridCulture(invariantLocale))}. `
                + `Type ${asCulture} without separators, or ${operandText(second, culture)} for the other.`;
        }
        case 'notTheColumnsDateForm': {
            const read = reading.read ?? refused('notTheColumnsDateForm carries what was read');
            const declared = reading.declared ?? refused('notTheColumnsDateForm carries the declared type');
            return dateFormRefusal(text, read, declared);
        }
        case 'notReadable': return `“${text}” is not a value of this column as ${culture.displayName} writes it.`;
        default: return refused(`an operand refusal is one of four kinds, not ${JSON.stringify(reading.refusal)}`);
    }
}

// ---- Numbers ----------------------------------------------------------------------------------------------

const spaceLike = /[   ]/gu;

/**
 * A number read in the culture, with its grouping checked (ADR-0006, note of 2026-10-01): a text that carries
 * the group separator must group as the culture writes it. A space-like group separator (fr-FR's narrow
 * no-break space) is also typed as a space or a no-break space, so those stand for it.
 */
function readNumber(text: string, culture: GridCulture): OperandReading {
    const { group, decimal } = culture;
    const trimmed = text.trim();
    const normalised = /^[   ]$/u.test(group) ? trimmed.replace(spaceLike, group) : trimmed;
    const parsed = parseNumber(normalised, culture);
    if (parsed === undefined || !groupedAsWritten(parsed, culture)) return notReadable;
    const value = valueOf(parsed);
    // A fraction a number cannot hold exactly is refused, never filtered on as the nearest double nobody typed.
    if (value === undefined) return notReadable;
    // A dot that groups here is the decimal point in the invariant culture: where the text has no decimal
    // separator of the culture's own, it may have meant either.
    if (group === '.' && normalised.includes('.') && !normalised.includes(decimal)) {
        const asPoint = parseNumber(normalised, gridCulture(invariantLocale));
        const other = asPoint === undefined ? undefined : valueOf(asPoint);
        if (asPoint !== undefined && other !== undefined && !sameNumber(parsed, asPoint)) {
            return Object.freeze({ value: undefined, refusal: 'readsTwoWays', readings: Object.freeze([value, other] as const) });
        }
    }
    return Object.freeze({ value, refusal: 'none' });
}

/** A number's text taken apart: its sign, the integer's text as typed (separators and all), and its digits. */
interface ParsedNumber {
    readonly negative: boolean;
    readonly integerText: string;
    readonly integer: string;
    readonly fraction: string;
}

/**
 * A number as C#'s `decimal.TryParse` reads it under `NumberStyles.Number`: white space either side, a sign
 * before or after, group separators anywhere in the integer part, and one decimal separator; no exponent and
 * no currency.
 */
function parseNumber(text: string, culture: GridCulture): ParsedNumber | undefined {
    let body = text.trim();
    let negative = false;
    let signed = false;
    // A culture whose minus is U+2212 (sv-SE, fi-FI) takes the keyboard's hyphen for it too, as C#'s parser
    // does (AllowHyphenDuringParsing).
    const signs: readonly (readonly [string, boolean])[] = [
        [culture.negativeSign, true], ...(culture.negativeSign === '\u2212' ? [['-', true] as const] : []), [culture.positiveSign, false],
    ];
    for (const [sign, isNegative] of signs) {
        if (signed || sign.length === 0) continue;
        if (body.startsWith(sign)) {
            body = body.slice(sign.length).trimStart();
            [negative, signed] = [isNegative, true];
        } else if (body.endsWith(sign)) {
            body = body.slice(0, -sign.length).trimEnd();
            [negative, signed] = [isNegative, true];
        }
    }
    const point = body.indexOf(culture.decimal);
    const integerText = point < 0 ? body : body.slice(0, point);
    const fraction = point < 0 ? '' : body.slice(point + culture.decimal.length);
    const integer = culture.group.length === 0 ? integerText : integerText.split(culture.group).join('');
    if (!/^\d*$/u.test(integer) || !/^\d*$/u.test(fraction) || integer.length + fraction.length === 0) return undefined;
    // The integer part starts with a digit: a group separator cannot lead it.
    if (integerText.length > 0 && !/^\d/u.test(integerText)) return undefined;
    return { negative, integerText, integer, fraction };
}

/** Whether a number's integer digits group as the culture writes them, where they are grouped at all. */
function groupedAsWritten(parsed: ParsedNumber, culture: GridCulture): boolean {
    if (culture.group.length === 0 || !parsed.integerText.includes(culture.group)) return true;
    return parsed.integerText === culture.grouped(BigInt(parsed.integer.length === 0 ? '0' : parsed.integer));
}

/** The number a parsed text holds: a `bigint` for a whole number past what a `number` holds exactly, a
 * `number` otherwise, as a cell of a Number column holds it (ADR-0350 section 16); none for a fraction no
 * `number` holds exactly as typed. */
function valueOf(parsed: ParsedNumber): number | bigint | undefined {
    const integer = parsed.integer.replace(/^0+(?=\d)/u, '');
    if (/^0*$/u.test(parsed.fraction)) {
        const whole = BigInt(integer.length === 0 ? '0' : integer);
        const signed = parsed.negative ? -whole : whole;
        if (signed >= BigInt(Number.MIN_SAFE_INTEGER) && signed <= BigInt(Number.MAX_SAFE_INTEGER)) return Number(signed);
        return signed;
    }
    const number = Number(`${parsed.negative ? '-' : ''}${integer.length === 0 ? '0' : integer}.${parsed.fraction}`);
    const [whole = '', fraction = ''] = plainNumber(Math.abs(number)).split('.');
    const typed = { negative: false, integerText: integer, integer, fraction: parsed.fraction };
    return sameNumber(typed, { negative: false, integerText: whole, integer: whole, fraction }) ? number : undefined;
}

/** Whether two texts hold the same number, compared as decimal digits, never as rounded doubles. */
function sameNumber(x: ParsedNumber, y: ParsedNumber): boolean {
    const canonical = ({ negative, integer, fraction }: ParsedNumber): string => {
        const digits = `${integer.replace(/^0+/u, '')}.${fraction.replace(/0+$/u, '')}`;
        return digits === '.' ? '0' : `${negative ? '-' : ''}${digits}`;
    };
    return canonical(x) === canonical(y);
}

/** A number's digits without an exponent: JavaScript's shortest text for it, written out where it would use
 * one, as C#'s decimal writes every digit. */
function plainNumber(value: number | bigint): string {
    if (typeof value === 'bigint') return value.toString();
    if (!Number.isFinite(value)) throw new RangeError(`${String(value)} is not a number an operand holds.`);
    const shortest = String(value);
    const exponent = /^(-?)(\d)(?:\.(\d+))?e([+-]\d+)$/u.exec(shortest);
    if (exponent === null) return shortest;
    const [, sign = '', lead = '', rest = '', power = '0'] = exponent;
    const digits = `${lead}${rest}`;
    const at = 1 + Number(power);
    if (at <= 0) return `${sign}0.${'0'.repeat(-at)}${digits}`;
    return `${sign}${digits.padEnd(at, '0')}`;
}

// ---- Dates ------------------------------------------------------------------------------------------------

// Ticket 94's forms and ISO 8601's extended form with an offset or Z, read exactly in every culture.
const isoOffset = /^(\d{4})-(\d{2})-(\d{2})(?: (\d{2}):(\d{2}):(\d{2}) ([+-]\d{2}:\d{2})|T(\d{2}):(\d{2})(?::(\d{2})(?:\.(\d{1,7}))?)?(Z|[+-]\d{2}:\d{2}))$/u;
// The T form without an offset, its fraction optional, is the one a date and time holding a fraction of a
// second reopens in (operandText).
const isoDate = /^(\d{4})-(\d{2})-(\d{2})(?: (\d{2}):(\d{2}):(\d{2})|T(\d{2}):(\d{2}):(\d{2})(?:\.(\d{1,7}))?)?$/u;
// Three runs of digits, day, month and year in some order, with a 24-hour time or nothing after it.
const numericDate = /^(\d{1,4})[./-](\d{1,2})[./-](\d{1,4})(?:[\sT]+(\d{1,2}):(\d{2})(?::(\d{2}))?)?$/u;

/** A time's optional field: zero where the form leaves it out, as a date with no time is at midnight. */
function field(group: string | undefined): number {
    return group === undefined ? 0 : Number(group);
}

/** A fraction of a second, as nanoseconds: none where the form leaves it out. */
function nanoseconds(fraction: string | undefined): number {
    return fraction === undefined ? 0 : Number(fraction.padEnd(9, '0'));
}

function readDate(text: string, culture: GridCulture, dateType: DateType, temporal: TemporalLike | undefined): OperandReading {
    const trimmed = text.trim();
    const offset = isoOffset.exec(trimmed);
    if (offset !== null) {
        const [, y, mo, d, h1, mi1, s1, z1, h2, mi2, s2, fraction, z2] = offset;
        // The two alternatives: Ticket 94's `yyyy-MM-dd HH:mm:ss zzz`, or ISO's T form, whose seconds and
        // fraction are optional, and zero where the text leaves them out.
        const spaced = z1 !== undefined;
        const read = spaced
            ? dateOf(Number(y), Number(mo), Number(d), Number(h1), Number(mi1), Number(s1), 0, z1)
            : dateOf(Number(y), Number(mo), Number(d), Number(h2), Number(mi2), field(s2), nanoseconds(fraction), defined(z2, 'the T form\'s offset'));
        return read === undefined ? notReadable : asDeclared(read, true, dateType, temporal);
    }
    const iso = isoDate.exec(trimmed);
    if (iso !== null) {
        const [, y, mo, d, h1, mi1, s1, h2, mi2, s2, fraction] = iso;
        const spaced = h1 !== undefined;
        const read = spaced
            ? dateOf(Number(y), Number(mo), Number(d), Number(h1), Number(mi1), Number(s1), 0)
            : dateOf(Number(y), Number(mo), Number(d), field(h2), field(mi2), field(s2), nanoseconds(fraction));
        return read === undefined ? notReadable : asDeclared(read, spaced || h2 !== undefined, dateType, temporal);
    }
    const numeric = numericDate.exec(trimmed);
    if (numeric === null) return notReadable;
    const [, a = '', b = '', c = '', h, mi, s] = numeric;
    const time = [field(h), field(mi), field(s)] as const;
    const hasTime = h !== undefined;
    const on = (year: number, month: number, day: number): ReadDate | undefined => dateOf(year, month, day, ...time, 0);
    const [first, second, third] = [Number(a), Number(b), Number(c)];
    // A year is written whole: a two-digit year is the culture's guess at its century, which is not made here.
    if (a.length === 4) {
        const read = on(first, second, third);
        return read === undefined ? notReadable : asDeclared(read, hasTime, dateType, temporal);
    }
    if (c.length !== 4) return notReadable;
    if (!culture.yearFirst) {
        const read = culture.dayFirst ? on(third, second, first) : on(third, first, second);
        return read === undefined ? notReadable : asDeclared(read, hasTime, dateType, temporal);
    }
    // The culture writes its year first, so a year written last has no order of day and month from it.
    const monthFirst = on(third, first, second);
    const dayFirst = on(third, second, first);
    if (monthFirst !== undefined && dayFirst !== undefined && isoDateText(monthFirst) !== isoDateText(dayFirst)) {
        return Object.freeze({ value: undefined, refusal: 'readsTwoWays', readings: Object.freeze([monthFirst, dayFirst] as const) });
    }
    const read = monthFirst ?? dayFirst;
    return read === undefined ? notReadable : asDeclared(read, hasTime, dateType, temporal);
}

/** A date read, held to the column's declared type (ADR-0023, section of 2026-10-02): a day takes no time
 * written, not even midnight; a date and time takes no offset; an instant needs one. */
function asDeclared(read: ReadDate, hasTime: boolean, dateType: DateType, temporal: TemporalLike | undefined): OperandReading {
    const declared = (): OperandReading => Object.freeze({ value: undefined, refusal: 'notTheColumnsDateForm', read, declared: dateType });
    const { year, month, day, hour, minute, second, nanosecond, offset } = read;
    switch (dateType) {
        case 'dateTime':
            if (offset !== undefined) return declared();
            // Temporal holds the fraction in three fields of three digits each.
            return made(madeWith(temporal).PlainDateTime.from({
                year, month, day, hour, minute, second,
                millisecond: Math.trunc(nanosecond / 1_000_000), microsecond: Math.trunc(nanosecond / 1_000) % 1_000, nanosecond: nanosecond % 1_000,
            }));
        case 'dateOnly':
            if (offset !== undefined || hasTime) return declared();
            return made(madeWith(temporal).PlainDate.from({ year, month, day }));
        case 'dateTimeOffset': {
            if (offset === undefined) return declared();
            const zone = offset === 'Z' ? '+00:00' : offset;
            const fraction = nanosecond === 0 ? '' : `.${String(nanosecond).padStart(9, '0')}`;
            return made(madeWith(temporal).ZonedDateTime.from(`${isoDay(read)}T${two(hour)}:${two(minute)}:${two(second)}${fraction}${zone}[${zone}]`));
        }
        default: throw new RangeError(`A Date column's date type is dateTime, dateOnly or dateTimeOffset, not ${JSON.stringify(dateType)}.`);
    }
}

function made(value: unknown): OperandReading {
    return Object.freeze({ value, refusal: 'none' });
}

function madeWith(temporal: TemporalLike | undefined): TemporalLike {
    if (temporal === undefined) {
        throw new TypeError('Reading a date operand needs Temporal, and none was found: a browser without it loads a polyfill into '
            + 'globalThis.Temporal (ADR-0350 section 16).');
    }
    return temporal;
}

/** The date of the fields, or `undefined` where there is no such date or time. */
function dateOf(year: number, month: number, day: number, hour: number, minute: number, second: number, nanosecond: number,
    offset?: string): ReadDate | undefined {
    if (year < 1 || year > 9999 || month < 1 || month > 12 || day < 1 || day > daysIn(year, month)) return undefined;
    if (hour > 23 || minute > 59 || second > 59) return undefined;
    if (offset !== undefined && offset !== 'Z' && (Number(offset.slice(1, 3)) > 14 || Number(offset.slice(4)) > 59)) return undefined;
    return Object.freeze({ year, month, day, hour, minute, second, nanosecond, ...(offset === undefined ? {} : { offset }) });
}

function daysIn(year: number, month: number): number {
    const leap = year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
    return month === 2 ? (leap ? 29 : 28) : [4, 6, 9, 11].includes(month) ? 30 : 31;
}

function isReadDate(value: unknown): value is ReadDate {
    return typeof value === 'object' && value !== null && 'year' in value && 'nanosecond' in value;
}

function two(value: number): string {
    return String(value).padStart(2, '0');
}

function isoDay({ year, month, day }: ReadDate): string {
    return `${String(year).padStart(4, '0')}-${two(month)}-${two(day)}`;
}

/** A date as a refusal names it, in a form that reads back exactly: the day alone at midnight, the day and
 * time otherwise. */
function isoDateText(read: ReadDate): string {
    const { hour, minute, second, nanosecond } = read;
    return hour === 0 && minute === 0 && second === 0 && nanosecond === 0
        ? isoDay(read)
        : `${isoDay(read)} ${two(hour)}:${two(minute)}:${two(second)}`;
}

/** Why a date is not the column's declared type, naming the form to type (ADR-0023, section of 2026-10-02). */
function dateFormRefusal(text: string, read: ReadDate, declared: DateType): string {
    const quoted = `“${text}”`;
    const time = `${two(read.hour)}:${two(read.minute)}:${two(read.second)}`;
    if (declared === 'dateOnly') {
        return read.offset === undefined
            ? `${quoted} has a time, and this column holds days. Type the day alone, as ${isoDay(read)}.`
            : `${quoted} has an offset, and this column holds days. Type the day alone, as ${isoDay(read)}.`;
    }
    if (declared === 'dateTime') {
        return `${quoted} has an offset, and this column's dates have none. Type it without one, as ${isoDateText(read)}.`;
    }
    // An offset is read in ISO's forms only, so an instant is refused here for having none.
    return `${quoted} has no offset, and this column holds moments. Type it with one, as ${isoDay(read)}T${time}+hh:mm, or with Z for UTC.`;
}

function refused(what: string): never {
    throw new Error(`Internal: ${what}.`);
}
