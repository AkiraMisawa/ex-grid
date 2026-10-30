// An Item as a Pivot Layout writes it, and as the engine tells Items apart (ADR-0060). A key names an
// Item by its kind and its invariant text: a number in its shortest round-trip form, a date as ISO, a
// Boolean as TRUE or FALSE, text as it is, so a saved layout names the same Items under any locale. Two
// keys of text are one Item when they differ only in case, as .NET's OrdinalIgnoreCase folds them.

import { ticksOf } from '@ex-grid/data';
import { compareOrdinalIgnoringCase, foldedText } from '../text/casing';
import { formatDate } from '../text/date-text';
import { invariantDates, invariantNumbers } from '../text/invariant';
import { formatDouble } from '../text/number-text';
import { itemKindOrder, itemKindTitle, type PivotItemKind } from '../layout/pivot-enums';
import { unreachable } from '../must';

/** An Item as a Pivot Layout writes it: its kind and its invariant text, which a Blank has none of. */
export interface PivotItemKey {
    /** The Item's kind. */
    readonly kind: PivotItemKind;
    /** Its invariant text; absent only for a Blank. */
    readonly value?: string;
}

/** The text of `#NUM!`, the Item of a number no one can hold. */
export const pivotErrorText = '#NUM!';

/** The format a date Item's key is written in, as .NET writes it. */
const keyDateFormat = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF";

/** The Item `(blank)`. */
export const blankItemKey: PivotItemKey = Object.freeze({ kind: 'blank' });

/** The Item `#NUM!`. */
export const errorItemKey: PivotItemKey = Object.freeze({ kind: 'error', value: pivotErrorText });

/**
 * Names one Item, refusing a value that is not the invariant text of its kind: a key that could not name
 * any Item is a mapping bug, not an Item nobody has. A number's text is read as .NET reads a float (spaces,
 * a sign, a point and an exponent), and a date's as `yyyy-MM-ddTHH:mm:ss` with up to seven digits of a
 * second; the text is kept as it was given.
 */
export function pivotItemKey(kind: PivotItemKind, value?: string): PivotItemKey {
    switch (kind) {
        case 'blank':
            if (value !== undefined) throw new TypeError('A Blank Item has no value.');
            return blankItemKey;
        case 'number':
            if (value === undefined || numberOfText(value) === undefined) {
                throw new TypeError(`'${String(value)}' is not the invariant text of a finite number.`);
            }
            break;
        case 'date':
            if (value === undefined || ticksOfKeyText(value) === undefined) {
                throw new TypeError(`'${String(value)}' is not an ISO date (${keyDateFormat}).`);
            }
            break;
        case 'boolean':
            if (value !== 'TRUE' && value !== 'FALSE') throw new TypeError(`'${String(value)}' is not TRUE or FALSE.`);
            break;
        case 'text':
            if (typeof value !== 'string') throw new TypeError('A text Item has a value.');
            break;
        case 'error':
            if (value !== pivotErrorText) throw new TypeError(`The error Item is ${pivotErrorText}.`);
            return errorItemKey;
        default:
            throw new RangeError(`Unknown PivotItemKind '${String(kind satisfies never)}'.`);
    }
    return Object.freeze({ kind, value });
}

/** The key of a text Item. */
export function textItemKey(text: string): PivotItemKey {
    return Object.freeze({ kind: 'text', value: text });
}

/** The key of a number Item; a number that is not finite is `#NUM!`, and -0 is 0. */
export function numberItemKey(number: number): PivotItemKey {
    if (!Number.isFinite(number)) return errorItemKey;
    return Object.freeze({ kind: 'number', value: formatDouble(number === 0 ? 0 : number, 'R', invariantNumbers) });
}

/** The key of a date Item, by its clock value in DateTime ticks. */
export function dateItemKey(ticks: bigint): PivotItemKey {
    return Object.freeze({ kind: 'date', value: formatDate(ticks, keyDateFormat, invariantDates) });
}

/** The key of a Boolean Item. */
export function booleanItemKey(value: boolean): PivotItemKey {
    return Object.freeze({ kind: 'boolean', value: value ? 'TRUE' : 'FALSE' });
}

/** Whether two keys name one Item: the same kind and text, text compared ignoring case. */
export function itemKeysEqual(a: PivotItemKey, b: PivotItemKey): boolean {
    if (a.kind !== b.kind) return false;
    if (a.kind === 'text') return compareOrdinalIgnoringCase(a.value ?? '', b.value ?? '') === 0; // oxlint-disable-line ex-grid/no-stand-in-default -- a text key always has a value
    return a.value === b.value;
}

/** `Text:East`, `Number:1234.5`, `Blank`: a key as the C# engine's messages write it. */
export function itemKeyText(key: PivotItemKey): string {
    return key.value === undefined ? itemKindTitle(key.kind) : `${itemKindTitle(key.kind)}:${key.value}`;
}

/** What a public key compares as, as a Map key: its kind, and its text folded where text is compared
 * ignoring case. Two keys equal by {@link itemKeysEqual} have one. */
export function publicKeyId(key: PivotItemKey): string {
    return key.kind === 'text' ? `text:${foldedText(key.value ?? '')}` : `${key.kind}:${key.value ?? ''}`; // oxlint-disable-line ex-grid/no-stand-in-default -- a Blank's key has no text, and every other kind's has one
}

// ---- reading a key's text --------------------------------------------------------------------------

/** A number's text read as .NET's `double.TryParse(text, NumberStyles.Float, InvariantCulture)` reads it,
 * or `undefined` where it does not read or is not finite. */
export function numberOfText(text: string): number | undefined {
    const match = /^[\t-\r ]*([+-]?)(\d*)(?:\.(\d*))?(?:[eE]([+-]?\d+))?[\t-\r ]*$/.exec(text);
    if (match === null) return undefined;
    const [, sign = '', whole = '', fraction = '', exponent = '0'] = match;
    if (whole.length === 0 && fraction.length === 0) return undefined;
    const number = Number(`${sign}${whole.length === 0 ? '0' : whole}.${fraction}e${exponent}`);
    return Number.isFinite(number) ? number : undefined;
}

/** A date key's text read as .NET's `DateTime.TryParseExact(text, "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF")` reads
 * it, as ticks, or `undefined` where it does not read. */
export function ticksOfKeyText(text: string): bigint | undefined {
    const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.(\d{0,7}))?$/.exec(text);
    if (match === null) return undefined;
    const [, y = '', mo = '', d = '', h = '', mi = '', s = '', f = ''] = match;
    const year = Number(y);
    const month = Number(mo);
    const day = Number(d);
    const hour = Number(h);
    const minute = Number(mi);
    const second = Number(s);
    if (year < 1 || month < 1 || month > 12 || day < 1 || day > daysIn(year, month) || hour > 23 || minute > 59 || second > 59) {
        return undefined;
    }
    return ticksOf(year, month, day, hour, minute, second, Number(f.padEnd(7, '0')));
}

function daysIn(year: number, month: number): number {
    if (month === 2) return year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0) ? 29 : 28;
    return month === 4 || month === 6 || month === 9 || month === 11 ? 30 : 31;
}

// ---- the engine's own key ----------------------------------------------------------------------------

/** An Item's identity while the engine sorts values into Items: its kind, and its number, clock value or
 * text. Equal as Items are told apart; {@link keyIdOf} says it as a Map key. */
export interface ItemKey {
    readonly kind: PivotItemKind;
    /** The number of a number Item, never -0; 0 otherwise. */
    readonly number: number;
    /** The clock ticks of a date; 0 or 1 for a Boolean; 0 otherwise. */
    readonly ticks: bigint;
    /** The text of a text Item; empty otherwise. */
    readonly text: string;
}

const blankKey: ItemKey = Object.freeze({ kind: 'blank', number: 0, ticks: 0n, text: '' });
const errorKey: ItemKey = Object.freeze({ kind: 'error', number: 0, ticks: 0n, text: '' });

export const ItemKeys = Object.freeze({
    blank: blankKey,
    error: errorKey,
    text: (text: string): ItemKey => Object.freeze({ kind: 'text', number: 0, ticks: 0n, text }),
    number: (number: number): ItemKey => Number.isFinite(number)
        ? Object.freeze({ kind: 'number', number: number === 0 ? 0 : number, ticks: 0n, text: '' })
        : errorKey,
    date: (ticks: bigint): ItemKey => Object.freeze({ kind: 'date', number: 0, ticks, text: '' }),
    boolean: (value: boolean): ItemKey => Object.freeze({ kind: 'boolean', number: 0, ticks: value ? 1n : 0n, text: '' }),
});

/** What an Item compares as, as a Map key. */
export function keyIdOf(key: ItemKey): string {
    switch (key.kind) {
        case 'number': return `n${String(key.number)}`;
        case 'date': return `d${key.ticks}`;
        case 'boolean': return `b${key.ticks}`;
        case 'text': return `t${foldedText(key.text)}`;
        case 'error': return 'e';
        case 'blank': return 'k';
        default: return unreachable(key.kind);
    }
}

/** Whether two keys are one Item. */
export function sameItem(a: ItemKey, b: ItemKey): boolean {
    if (a.kind !== b.kind) return false;
    switch (a.kind) {
        case 'number': return a.number === b.number;
        case 'date':
        case 'boolean': return a.ticks === b.ticks;
        case 'text': return compareOrdinalIgnoringCase(a.text, b.text) === 0;
        case 'error':
        case 'blank': return true;
        default: return unreachable(a.kind);
    }
}

/** The engine's key of a public one. */
export function fromPublicKey(key: PivotItemKey): ItemKey {
    switch (key.kind) {
        case 'blank': return blankKey;
        case 'error': return errorKey;
        case 'text': return ItemKeys.text(must(key.value));
        case 'number': return ItemKeys.number(must(numberOfText(must(key.value))));
        case 'date': return ItemKeys.date(must(ticksOfKeyText(must(key.value))));
        case 'boolean': return ItemKeys.boolean(key.value === 'TRUE');
        default: return unreachable(key.kind);
    }
}

/** The value an Item hands an Order Key and a format (ADR-0060): a text Item's text, a number's number, a date's
 * clock ticks, a Boolean's Boolean; `undefined` for a Blank and for #NUM!, which no key orders. */
export function itemValueOf(key: ItemKey): string | number | bigint | boolean | undefined {
    switch (key.kind) {
        case 'text': return key.text;
        case 'number': return key.number;
        case 'date': return key.ticks;
        case 'boolean': return key.ticks === 1n;
        case 'error':
        case 'blank': return undefined;
        default: return unreachable(key.kind);
    }
}

/** The value the Item `key` names hands an Order Key (ADR-0060): a text Item's text, a number's number, a date's
 * clock ticks, a Boolean's Boolean; `undefined` for a Blank and for #NUM!. A source that answers an Order Key's
 * keys for the Items it names files them under this. */
export function pivotItemValue(key: PivotItemKey): string | number | bigint | boolean | undefined {
    return itemValueOf(fromPublicKey(key));
}

/** The public key of the engine's. */
export function toPublicKey(key: ItemKey): PivotItemKey {
    switch (key.kind) {
        case 'blank': return blankItemKey;
        case 'error': return errorItemKey;
        case 'text': return textItemKey(key.text);
        case 'number': return numberItemKey(key.number);
        case 'date': return dateItemKey(key.ticks);
        case 'boolean': return booleanItemKey(key.ticks === 1n);
        default: return unreachable(key.kind);
    }
}

/** The order a source lists Items in when it knows no locale (ADR-0066): by kind, then numbers and dates
 * by value, FALSE before TRUE, and text ordinally ignoring case. */
export function compareInvariant(x: ItemKey, y: ItemKey): number {
    if (x.kind !== y.kind) return Math.sign(itemKindOrder(x.kind) - itemKindOrder(y.kind));
    switch (x.kind) {
        case 'number': return x.number < y.number ? -1 : x.number > y.number ? 1 : 0;
        case 'date':
        case 'boolean': return x.ticks < y.ticks ? -1 : x.ticks > y.ticks ? 1 : 0;
        case 'text': return compareOrdinalIgnoringCase(x.text, y.text);
        case 'error':
        case 'blank': return 0;
        default: return unreachable(x.kind);
    }
}

function must<T>(value: T | undefined): T {
    if (value === undefined) throw new Error('Internal: a public key that was checked does not read.');
    return value;
}
