// A Pivot Field as a source offers it (ADR-0061/0066): its name, caption and declared type, the format its
// Items are labelled with, the order its Items are declared in (Excel's custom lists) and its Order Key —
// and, over records, how it is read from one. The C# engine's PivotField, PivotField<TRecord> and
// PivotNumberFormat, with their checks and their messages.

import { ticksOf } from '@ex-grid/data';
import { PivotFormatError } from '../errors';
import { itemKeyOfValue, readRecordValue } from '../items/record-value';
import { keyIdOf } from '../items/item-key';
import type { PivotDatePart, PivotFieldType } from '../layout/pivot-enums';
import { formatDate } from '../text/date-text';
import { invariantDates, invariantNumbers } from '../text/invariant';
import { formatDecimal, formatDouble } from '../text/number-text';

/** What an Order Key gives an Item: a value Items are ordered by, ascending; one field's keys are all of
 * one type. `null` or `undefined` gives the Item no key. */
export type PivotOrderKeyValue = string | number | bigint | boolean;

/**
 * A field's Order Key (ADR-0060): from an Item's value — the text of a text Item (its first spelling), the
 * number of a number Item, the clock ticks of a date, the Boolean of a Boolean — to what the Items are
 * ordered by. Called once per Item where the report is laid out; one that throws is refused, naming the
 * field and the Item.
 */
export type PivotOrderKey = (value: string | number | bigint | boolean) => PivotOrderKeyValue | null | undefined;

/** A Pivot Field as a source offers it: everything but how it is read from a record. */
export interface PivotField {
    /** What a Pivot Layout addresses it by; unique among the fields. */
    readonly name: string;
    /** What the Field List and the report call it. */
    readonly caption: string;
    /** The declared type, which decides the defaults (ADR-0060). */
    readonly type: PivotFieldType;
    /** A .NET format string an Item's number or date is labelled with, under the report's locale; absent for
     * the value's own text. */
    readonly format?: string;
    /** Values whose Items come first, in this order, when the field is sorted by label. */
    readonly itemOrder: readonly unknown[];
    /** The Snapshot column the field reads, when it is not the column of its own name. */
    readonly column?: string;
    /** The part of a Date column the field is: its year, quarter or month. */
    readonly datePart?: PivotDatePart;
    /** The field's Order Key. */
    readonly orderKey?: PivotOrderKey;
}

/** A Pivot Field over records in memory: a field and how it is read from one record. */
export interface PivotRecordField<TRecord> extends PivotField {
    /** Reads the field from a record; `null` or `undefined` is a Blank. */
    readonly value: (record: TRecord) => unknown;
}

/** A field's declaration, each setting left out taking its default: the name for the caption, no format,
 * no declared order, the column of its own name, no Order Key. */
export interface PivotFieldInput {
    /** What a Pivot Layout addresses it by. */
    readonly name: string;
    /** The declared type. */
    readonly type: PivotFieldType;
    /** What the Field List and the report call it; its name when left out. */
    readonly caption?: string;
    /** A .NET format string its Items are labelled with. */
    readonly format?: string;
    /** Values whose Items come first, in this order. */
    readonly itemOrder?: readonly unknown[];
    /** The Snapshot column it reads; the column of its own name when left out. */
    readonly column?: string;
    /** The part of a Date column it is. */
    readonly datePart?: PivotDatePart;
    /** Its Order Key. */
    readonly orderKey?: PivotOrderKey;
}

/** A record field's declaration. */
export interface PivotRecordFieldInput<TRecord> extends PivotFieldInput {
    /** Reads the field from a record; `null` or `undefined` is a Blank. */
    readonly value: (record: TRecord) => unknown;
}

const fieldTypes: readonly PivotFieldType[] = ['text', 'number', 'date', 'boolean'];
const dateParts: readonly PivotDatePart[] = ['year', 'quarter', 'month'];

/** The C# engine's name of a field type: `Text`, `Number`. */
export function fieldTypeTitle(type: PivotFieldType): string {
    return type.charAt(0).toUpperCase() + type.slice(1);
}

/** The C# engine's name of a date part: `Year`, `Month`. */
export function datePartTitle(part: PivotDatePart): string {
    return part.charAt(0).toUpperCase() + part.slice(1);
}

/** A field, its declaration checked and frozen. Refuses, with a TypeError, an empty name, an empty caption,
 * an empty column, a format that cannot format the field's values and a date part of a field not declared
 * Date; with a RangeError an unknown type or date part. */
export function pivotField(input: PivotFieldInput): PivotField {
    return Object.freeze(declared(input));
}

/** A record field, its declaration checked as {@link pivotField} checks it, and its accessor a function. */
export function pivotRecordField<TRecord>(input: PivotRecordFieldInput<TRecord>): PivotRecordField<TRecord> {
    const field = declared(input);
    if (typeof input.value !== 'function') throw new TypeError(`Pivot Field '${field.name}' reads its value with a function.`);
    return Object.freeze({ ...field, value: input.value });
}

/** A field that is a part of a Date column (ADR-0060): its year, quarter or month, labelled `2026`, `Qtr3`,
 * `Sep` in the report's words and ordered by the calendar. */
export function datePartField(name: string, column: string, part: PivotDatePart, caption?: string): PivotField {
    if (typeof column !== 'string' || column.length === 0) throw new TypeError('A date part names the Date column it is a part of.');
    return pivotField({ name, type: 'date', column, datePart: part, ...(caption === undefined ? {} : { caption }) });
}

type Mutable<T> = { -readonly [K in keyof T]: T[K] };

function declared(input: PivotFieldInput): PivotField {
    const { name, type } = input;
    if (typeof name !== 'string' || name.length === 0) throw new TypeError('A Pivot Field is named, and its name is not empty.');
    if (!fieldTypes.includes(type)) throw new RangeError(`Unknown PivotFieldType for Pivot Field '${name}'.`);
    if (input.caption !== undefined && input.caption.length === 0) {
        throw new TypeError(`Pivot Field '${name}' has an empty caption; leave it out for the name.`);
    }
    if (input.format !== undefined) {
        const problem = formatProblem(type, input.format);
        if (problem !== undefined) throw new TypeError(`Pivot Field '${name}' declares the format '${input.format}', which ${problem}.`);
    }
    if (input.column !== undefined && input.column.length === 0) {
        throw new TypeError(`Pivot Field '${name}' names an empty column; leave it out for the column '${name}'.`);
    }
    if (input.datePart !== undefined) {
        if (!dateParts.includes(input.datePart)) throw new RangeError(`Unknown PivotDatePart for Pivot Field '${name}'.`);
        if (type !== 'date') {
            throw new TypeError(`Pivot Field '${name}' is a date part, and is declared ${fieldTypeTitle(type)}; a date part is declared Date.`);
        }
    }
    if (input.orderKey !== undefined && typeof input.orderKey !== 'function') {
        throw new TypeError(`Pivot Field '${name}' has an Order Key that is not a function.`);
    }
    const field: Mutable<PivotField> = {
        name,
        caption: input.caption ?? name,
        type,
        itemOrder: Object.freeze([...(input.itemOrder ?? [])]), // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0060: no declared order is an empty one
    };
    if (input.format !== undefined) field.format = input.format;
    if (input.column !== undefined) field.column = input.column;
    if (input.datePart !== undefined) field.datePart = input.datePart;
    if (input.orderKey !== undefined) field.orderKey = input.orderKey;
    return field;
}

// ---- Formats --------------------------------------------------------------------------------------

/** The longest format string a field or a Value Field may declare. */
export const maxFormatLength = 64;

/** The most digits a standard number format may ask for. */
export const maxFormatPrecision = 30;

/**
 * What is wrong with a number format, or `undefined` when nothing is (PivotNumberFormat.Check): empty,
 * longer than 64 characters, a standard format asking for more than 30 digits, or one .NET cannot format
 * a number with.
 */
export function numberFormatProblem(format: string): string | undefined {
    if (format.length === 0) return 'is empty';
    if (format.length > maxFormatLength) return `is longer than ${maxFormatLength} characters`;
    if (/^[A-Za-z][0-9]+$/.test(format)) {
        // int.TryParse: a precision too large for an int is not read, and the format is tried as it is.
        const precision = Number(format.slice(1));
        if (precision <= 2147483647 && precision > maxFormatPrecision) return `asks for ${precision} digits, more than ${maxFormatPrecision}`;
    }
    try {
        formatDecimal({ mantissa: 12345n, scale: 1 }, format, invariantNumbers);
        formatDouble(-1234.5, format, invariantNumbers);
        formatDouble(0, format, invariantNumbers);
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a format .NET refuses is the problem this check names

        if (error instanceof PivotFormatError) return 'cannot format a number';
        throw error;
    }
    return undefined;
}

const sampleDate = ticksOf(2026, 9, 30, 13, 45, 0);

// A number's format is held to what a Value Field's is; a date's needs only to format a date.
function formatProblem(type: PivotFieldType, format: string): string | undefined {
    if (type !== 'date') return numberFormatProblem(format);
    if (format.length === 0 || format.length > maxFormatLength) return `is empty or longer than ${maxFormatLength} characters`;
    try {
        formatDate(sampleDate, format, invariantDates);
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a format .NET refuses is the problem this check names

        if (error instanceof PivotFormatError) return 'cannot format a date';
        throw error;
    }
    return undefined;
}

// ---- What the report needs of a field -----------------------------------------------------------------

/** What laying out a report reads of a field: its declaration, and its declared order by Item. */
export interface FieldMeta {
    readonly field: PivotField;
    /** The declared order, by the Item's {@link keyIdOf}: the first place a value takes. */
    readonly declaredOrder: ReadonlyMap<string, number>;
}

/** A field's meta. */
export function fieldMeta(field: PivotField): FieldMeta {
    const order = new Map<string, number>();
    field.itemOrder.forEach((value, index) => {
        const id = keyIdOf(itemKeyOfValue(readRecordValue(value)));
        if (!order.has(id)) order.set(id, index);
    });
    return Object.freeze({ field, declaredOrder: order });
}

/** The fields by name, refusing a name declared twice: a layout could not say which it means. */
export function declaredFields<T extends PivotField>(fields: readonly T[]): ReadonlyMap<string, T> {
    const byName = new Map<string, T>();
    for (const field of fields) {
        if (byName.has(field.name)) throw new TypeError(`Two Pivot Fields are named '${field.name}'; a layout could not say which it means.`);
        byName.set(field.name, field);
    }
    return byName;
}

/** Each field's meta, by name. */
export function fieldMetas(fields: readonly PivotField[]): ReadonlyMap<string, FieldMeta> {
    const metas = new Map<string, FieldMeta>();
    for (const [name, field] of declaredFields(fields)) metas.set(name, fieldMeta(field));
    return metas;
}
