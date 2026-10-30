// A value a Consumer hands the engine, read by its own kind (ADR-0060: a field "whose values are not all
// of the declared type is still pivoted as its values are"), as the C# engine reads a boxed value: text, an
// exact number, a double, a date, a Boolean, or a Blank. JavaScript's kinds map onto C#'s: a string is text,
// a number is a double, a bigint and a SnapshotDecimal are exact, a Temporal plain date or date-time is a
// date by its clock value, null and undefined are Blanks. C# reads any other type as its invariant text;
// JavaScript has no such text (an object's is "[object Object]"), so another type is refused, naming it,
// rather than pivoted as text nobody chose.

import { ticksOfTemporal, type SnapshotDecimal } from '@ex-grid/data';
import { decimalToNumber } from '../aggregates/exact-decimal';
import { ItemKeys, type ItemKey } from './item-key';

/** A value as the engine reads it. */
export type RecordValue =
    | { readonly kind: 'blank' }
    | { readonly kind: 'text'; readonly text: string }
    | { readonly kind: 'decimal'; readonly decimal: SnapshotDecimal }
    | { readonly kind: 'double'; readonly number: number }
    | { readonly kind: 'date'; readonly ticks: bigint }
    | { readonly kind: 'boolean'; readonly value: boolean };

const blank: RecordValue = Object.freeze({ kind: 'blank' });

/** Whether a value is a SnapshotDecimal: a bigint mantissa and a whole scale from 0 to 28. */
export function isDecimal(value: unknown): value is SnapshotDecimal {
    if (typeof value !== 'object' || value === null) return false;
    const mantissa: unknown = Reflect.get(value, 'mantissa');
    const scale: unknown = Reflect.get(value, 'scale');
    return typeof mantissa === 'bigint' && typeof scale === 'number' && Number.isInteger(scale) && scale >= 0 && scale <= 28;
}

const decimalLimit = 1n << 96n;

/** A value read by its kind; another type is refused with a TypeError that names it. */
export function readRecordValue(value: unknown): RecordValue {
    if (value === null || value === undefined) return blank;
    switch (typeof value) {
        case 'string': return { kind: 'text', text: value };
        case 'number': return { kind: 'double', number: value };
        case 'boolean': return { kind: 'boolean', value };
        case 'bigint':
            if (value >= decimalLimit || value <= -decimalLimit) {
                throw new RangeError(`${value} is wider than the 96 bits a decimal holds.`);
            }
            return { kind: 'decimal', decimal: { mantissa: value, scale: 0 } };
        case 'object': {
            if (isDecimal(value)) {
                if (value.mantissa >= decimalLimit || value.mantissa <= -decimalLimit) {
                    throw new RangeError(`A decimal's ${value.mantissa} is wider than the 96 bits it holds.`);
                }
                return { kind: 'decimal', decimal: value };
            }
            const ticks = ticksOfTemporal(value);
            if (ticks !== undefined) return { kind: 'date', ticks };
            throw new TypeError(`A value of type ${Object.prototype.toString.call(value).slice(8, -1)} is none the engine reads: `
                + 'text, a number, a bigint, a SnapshotDecimal, a Temporal plain date or date-time, a Boolean, or null.');
        }
        case 'function':
        case 'symbol':
        case 'undefined':
            throw new TypeError(`A value of type ${typeof value} is none the engine reads.`);
        default: throw new Error(`Internal: typeof gave a type the engine does not know (${typeof value}).`);
    }
}

/** The Item a value belongs to (ADR-0060): a number by its double, a decimal by the double C# converts it
 * to, a date by its clock value. */
export function itemKeyOfValue(value: RecordValue): ItemKey {
    switch (value.kind) {
        case 'blank': return ItemKeys.blank;
        case 'text': return ItemKeys.text(value.text);
        case 'decimal': return ItemKeys.number(decimalToNumber(value.decimal));
        case 'double': return ItemKeys.number(value.number);
        case 'date': return ItemKeys.date(value.ticks);
        case 'boolean': return ItemKeys.boolean(value.value);
        default: throw new Error(`Internal: ${String(value satisfies never)} is not a value's kind.`);
    }
}
