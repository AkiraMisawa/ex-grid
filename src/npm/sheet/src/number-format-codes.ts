// The format codes the Number tab's categories write, and how a code is recognised as one of them when Format
// Cells opens (ADR-0071), the counterpart of C#'s NumberFormatCodes. Every code here is one the engine reads; a
// code that matches none of them opens under Custom, as it is.

import { NumberFormat, type Culture } from '@ex-grid/sheet-engine';
import { currencyDecimalDigitsOf, currencyPositivePatternOf, currencySymbolOf } from './culture-facts';
import type { NumberFormatCategory } from './format-cells-offer';

/** The most decimal places Excel's spinner offers. */
export const mostDecimalPlaces = 30;

/** How many ways a negative number shows under Number: `-1234.10`, in red, in brackets, in red brackets. */
export const numberNegativeStyles = 4;

/** How many ways a negative amount shows under Currency: with a minus, in red, in red with a minus, in brackets,
 * in red brackets. */
export const currencyNegativeStyles = 5;

/** The Date category's formats: Excel's built-in short date first, shown in the culture's own pattern. */
export const dateTypes: readonly string[] = Object.freeze(
    ['m/d/yyyy', 'd-mmm-yy', 'd-mmm', 'mmm-yy', 'mmmm d, yyyy', 'd mmmm yyyy', 'yyyy-mm-dd', 'dd/mm/yyyy']);

/** The Time category's formats. */
export const timeTypes: readonly string[] = Object.freeze(['h:mm', 'h:mm AM/PM', 'h:mm:ss', 'h:mm:ss AM/PM', 'mm:ss', 'm/d/yyyy h:mm']);

/** The codes Custom lists to start from. */
export const customTypes: readonly string[] = Object.freeze(
    ['General', '0', '0.00', '#,##0', '#,##0.00', '0%', '0.00%', '0.00E+00', '@', ...dateTypes, ...timeTypes]);

function checkPlaces(places: number): void {
    if (!Number.isInteger(places) || places < 0 || places > mostDecimalPlaces) {
        throw new RangeError(`Decimal places are 0 to ${mostDecimalPlaces}, not ${places}.`);
    }
}

/** The digits of a number: with or without the thousands separator, and the decimal places. */
function digits(places: number, separator: boolean): string {
    checkPlaces(places);
    return (separator ? '#,##0' : '0') + (places > 0 ? `.${'0'.repeat(places)}` : '');
}

/** The Number category's code. */
export function numberCode(places: number, separator: boolean, negative: number): string {
    const body = digits(places, separator);
    switch (negative) {
        case 0: return body;
        case 1: return `${body};[Red]${body}`;
        case 2: return `${body}_);(${body})`;
        case 3: return `${body}_);[Red](${body})`;
        default: throw new RangeError(`${negative} is not one of Number's negative styles.`);
    }
}

/** The Currency category's code, with the culture's symbol on the side and at the distance the culture puts it. */
export function currencyCode(places: number, negative: number, culture: Culture): string {
    const body = symbolised(digits(places, true), culture);
    switch (negative) {
        case 0: return body;
        case 1: return `${body};[Red]${body}`;
        case 2: return `${body};[Red]-${body}`;
        case 3: return `${body}_);(${body})`;
        case 4: return `${body}_);[Red](${body})`;
        default: throw new RangeError(`${negative} is not one of Currency's negative styles.`);
    }
}

/** The Percentage category's code. */
export function percentageCode(places: number): string {
    return `${digits(places, false)}%`;
}

/** The Scientific category's code. */
export function scientificCode(places: number): string {
    return `${digits(places, false)}E+00`;
}

/**
 * The culture's currency symbol beside the digits, where the culture puts it. A symbol of currency signs alone
 * (`$`, `£`, `¥`, `€`) is written as it is, as Excel writes it; any other is quoted, so its letters are not read
 * as codes.
 */
function symbolised(body: string, culture: Culture): string {
    const currencySymbol = currencySymbolOf(culture);
    const symbol = /^\p{Sc}*$/u.test(currencySymbol) ? currencySymbol : `"${currencySymbol.replaceAll('"', '')}"`;
    switch (currencyPositivePatternOf(culture)) {
        case 1: return body + symbol;
        case 2: return `${symbol} ${body}`;
        case 3: return `${body} ${symbol}`;
        default: return symbol + body;
    }
}

/** The culture's default decimal places for a category, as Excel offers them. */
export function defaultPlaces(category: NumberFormatCategory, culture: Culture): number {
    return category === 'currency' ? currencyDecimalDigitsOf(culture) : 2;
}

/** What a category shows a code as when Format Cells opens on it. */
export interface RecognisedNumberFormat {
    /** The category that writes the code. */
    readonly category: NumberFormatCategory;
    /** Its decimal places, 2 where the category takes none. */
    readonly places: number;
    /** Whether Number groups thousands. */
    readonly separator: boolean;
    /** Which negative style. */
    readonly negative: number;
    /** The code as the category shows it: a Date's or a Time's as listed, a Custom code as the culture spells
     * it, any other as written. */
    readonly type: string;
}

function recognised(category: NumberFormatCategory, places: number, separator: boolean, negative: number, type: string): RecognisedNumberFormat {
    return Object.freeze({ category, places, separator, negative, type });
}

function sameIgnoringCase(a: string, b: string): boolean {
    return a.toUpperCase() === b.toUpperCase();
}

/**
 * The category, and its options, that writes the format's code exactly; Custom when none does, with the code as
 * the culture spells it (`NumberFormat.localCode`), which the Custom box reads back as the same format (the
 * fourteenth Windows run, case 11).
 */
export function recogniseNumberFormat(format: NumberFormat, culture: Culture): RecognisedNumberFormat {
    const code = format.code;
    if (format.isGeneral) return recognised('general', 2, false, 0, code);
    if (code === '@') return recognised('text', 2, false, 0, code);
    for (let places = 0; places <= mostDecimalPlaces; places++) {
        for (let negative = 0; negative < numberNegativeStyles; negative++) {
            if (code === numberCode(places, false, negative)) return recognised('number', places, false, negative, code);
            if (code === numberCode(places, true, negative)) return recognised('number', places, true, negative, code);
        }
        for (let negative = 0; negative < currencyNegativeStyles; negative++) {
            if (code === currencyCode(places, negative, culture)) return recognised('currency', places, true, negative, code);
        }
        if (code === percentageCode(places)) return recognised('percentage', places, false, 0, code);
        if (code === scientificCode(places)) return recognised('scientific', places, false, 0, code);
    }
    const date = dateTypes.find(type => sameIgnoringCase(type, code));
    if (date !== undefined) return recognised('date', 2, false, 0, date);
    const time = timeTypes.find(type => sameIgnoringCase(type, code));
    if (time !== undefined) return recognised('time', 2, false, 0, time);
    return recognised('custom', 2, false, 0, format.localCode(culture));
}

/**
 * The codes Custom lists to start from, as the culture spells them (`NumberFormat.localCode`), each once: under
 * en-GB the built-in short date is listed as `dd/mm/yyyy`, so that the one picked from the list is the built-in,
 * as one typed is.
 */
export function customTypesIn(culture: Culture): readonly string[] {
    const listed: string[] = [];
    for (const code of customTypes) {
        const local = NumberFormat.parse(code).localCode(culture);
        if (!listed.some(other => sameIgnoringCase(other, local))) listed.push(local);
    }
    return Object.freeze(listed);
}
