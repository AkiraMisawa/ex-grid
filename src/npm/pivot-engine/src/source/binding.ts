// How a Pivot Field reads its values from a Snapshot (ADR-0060/0064/0066): one column — the field's own, or
// the Date column a date part is a part of — or, for a field read through an accessor, one column per kind
// of value the accessor returned, a row's value standing in exactly one of them, or in none for a Blank.
// The second is how a field "whose values are not all of the declared type is still pivoted as its values
// are". The C# engine's FieldBinding, RecordColumns and RowValues.

import { createSnapshot, SnapshotError, type Snapshot, type SnapshotColumn, type SnapshotColumnInput, type SnapshotDecimal, type SnapshotKind } from '@ex-grid/data';
import { declaredFields, datePartTitle, type PivotField, type PivotRecordField } from '../fields/pivot-field';
import { ItemKeys, type ItemKey } from '../items/item-key';
import { isDecimal, readRecordValue, type RecordValue } from '../items/record-value';
import type { PivotDatePart, PivotFieldType } from '../layout/pivot-enums';
import { defined, elementAt, unreachable } from '../must';
import { datePartOf } from '../words/pivot-words';
import { dateParts as datePartsOfTicks } from '../text/date-text';
import { decimalOfWhole, decimalToNumber } from '../aggregates/exact-decimal';

/** One field's columns, and the part of a Date column it is. */
export interface FieldBinding {
    readonly field: PivotField;
    readonly columns: readonly SnapshotColumn[];
    /** The one column, when the field reads one. */
    readonly single: SnapshotColumn | undefined;
    readonly part: PivotDatePart | undefined;
}

function bindingOf(field: PivotField, columns: readonly SnapshotColumn[]): FieldBinding {
    return Object.freeze({ field, columns, single: columns.length === 1 ? columns[0] : undefined, part: field.datePart });
}

/** The field type a Snapshot column's kind declares by default. */
export function fieldTypeOf(kind: SnapshotKind): PivotFieldType {
    switch (kind) {
        case 'text': return 'text';
        case 'decimal':
        case 'integer':
        case 'double': return 'number';
        case 'date': return 'date';
        case 'boolean': return 'boolean';
        default: return unreachable(kind);
    }
}

/** The C# engine's name of a Snapshot kind: `Text`, `Decimal`. */
export function kindTitle(kind: SnapshotKind): string {
    return kind.charAt(0).toUpperCase() + kind.slice(1);
}

/**
 * The fields of a Snapshot (ADR-0066): `fields` each bound to the column it names — its `column`, or its
 * own name — or, with none declared, one field per column, captioned as the column is and typed by its kind.
 * An unknown column is refused by name, and so is a date part of a column that is not a Date column.
 */
export function bindSnapshot(snapshot: Snapshot, fields: readonly PivotField[] | undefined): {
    readonly fields: readonly PivotField[];
    readonly bindings: ReadonlyMap<string, FieldBinding>;
} {
    const declared = fields === undefined ? snapshot.columns.map(defaultField) : [...declaredFields(fields).values()];
    const bindings = new Map<string, FieldBinding>();
    for (const field of declared) {
        const name = field.column ?? field.name;
        const column = snapshot.columns.find(candidate => candidate.name === name);
        if (column === undefined) {
            throw new TypeError(field.column === undefined
                ? `The Snapshot has no column named '${name}' for the Pivot Field '${field.name}'.`
                : `The Pivot Field '${field.name}' reads the column '${name}', and the Snapshot has no column of that name.`);
        }
        if (field.datePart !== undefined && column.kind !== 'date') {
            throw new TypeError(`The Pivot Field '${field.name}' is the ${datePartTitle(field.datePart)} of '${name}', which is a ${kindTitle(column.kind)} column, not a Date column.`);
        }
        bindings.set(field.name, bindingOf(field, [column]));
    }
    return { fields: Object.freeze(declared), bindings };
}

function defaultField(column: SnapshotColumn): PivotField {
    const field: { name: string; caption: string; type: PivotFieldType; itemOrder: readonly unknown[] } = {
        name: column.name,
        caption: column.caption.length === 0 ? column.name : column.caption,
        type: fieldTypeOf(column.kind),
        itemOrder: Object.freeze([]),
    };
    return Object.freeze(field);
}

// ---- Records into a Snapshot ----------------------------------------------------------------------

type ValueKind = 'text' | 'decimal' | 'double' | 'date' | 'boolean';

/** One field's columns, made as its kinds are met, each holding every row read so far. */
class FieldColumns {
    readonly order: ValueKind[] = [];
    text: (string | null)[] | undefined;
    decimal: (SnapshotDecimal | null)[] | undefined;
    double: (number | null)[] | undefined;
    date: (bigint | null)[] | undefined;
    boolean: (boolean | null)[] | undefined;

    append(row: number, value: RecordValue): void {
        if (value.kind !== 'blank' && !this.order.includes(value.kind)) {
            this.order.push(value.kind);
            this.make(value.kind, row);
        }
        this.text?.push(value.kind === 'text' ? value.text : null);
        this.decimal?.push(value.kind === 'decimal' ? value.decimal : null);
        this.double?.push(value.kind === 'double' ? value.number : null);
        this.date?.push(value.kind === 'date' ? value.ticks : null);
        this.boolean?.push(value.kind === 'boolean' ? value.value : null);
    }

    // A field no value was read for — every value Blank, or no records — reads a Text column of Blanks.
    seal(rows: number): void {
        if (this.order.length > 0) return;
        this.order.push('text');
        this.make('text', rows);
    }

    inputs(f: number): SnapshotColumnInput[] {
        return this.order.map(kind => {
            const name = columnName(f, kind);
            switch (kind) {
                case 'text': return { name, kind, values: defined(this.text, name) };
                case 'decimal': return { name, kind, values: defined(this.decimal, name) };
                case 'double': return { name, kind, values: defined(this.double, name) };
                case 'date': return { name, kind, values: defined(this.date, name) };
                case 'boolean': return { name, kind, values: defined(this.boolean, name) };
                default: return unreachable(kind);
            }
        });
    }

    private make(kind: ValueKind, rows: number): void {
        const blanks = (): null[] => Array.from({ length: rows }, () => null);
        switch (kind) {
            case 'text': this.text = blanks(); return;
            case 'decimal': this.decimal = blanks(); return;
            case 'double': this.double = blanks(); return;
            case 'date': this.date = blanks(); return;
            case 'boolean': this.boolean = blanks(); return;
            default: unreachable(kind);
        }
    }
}

function columnName(field: number, kind: ValueKind): string {
    return `${field}:${kindTitle(kind)}`;
}

/**
 * The Snapshot behind a source over records: the records read once through the fields' accessors, each
 * value keeping its own kind, so a field is held as one column per kind its accessor returned — text, an
 * exact number (a bigint or a SnapshotDecimal), a double, a date, a Boolean — with each row's value in the
 * column of its kind and a Blank in the others. A field whose every value was Blank reads a Text column of
 * Blanks. An accessor that throws, or a date part read as anything but a date, fails the build whole,
 * naming the row and the field (ADR-0064).
 */
export function recordSnapshot<TRecord>(records: readonly TRecord[], fields: readonly PivotRecordField<TRecord>[]): {
    readonly snapshot: Snapshot;
    readonly bindings: ReadonlyMap<string, FieldBinding>;
} {
    return snapshotOfValues(records, fields, (record, row, f) => {
        const field = elementAt(fields, f);
        let raw: unknown;
        try {
            raw = field.value(record);
        } catch (error) {
            const reason = error instanceof Error ? `${error.name}: ${error.message}` : String(error);
            throw new SnapshotError(`reading the value threw ${reason}`, { row: row + 1, column: field.name });
        }
        return readRecordValue(raw);
    });
}

/**
 * The Snapshot of records as a record behind a cell carries them (ADR-0370 section 3): each record's values in
 * `fields`' order — text, an exact number as a SnapshotDecimal, a double, a date by its ticks, a Boolean, or
 * `null` for a Blank — and a date part by its number, as {@link detailAt} gives it, so a date part is bound as
 * the number it is. A value of another kind, or a record of another length, fails the build whole, naming the
 * record and the field.
 */
export function detailSnapshot(records: readonly (readonly DetailValue[])[], fields: readonly PivotField[]): {
    readonly snapshot: Snapshot;
    readonly bindings: ReadonlyMap<string, FieldBinding>;
} {
    records.forEach((record, row) => {
        if (!Array.isArray(record) || record.length !== fields.length) {
            throw new SnapshotError(`the record carries ${Array.isArray(record) ? String(record.length) : 'no array of'} values, and there are ${fields.length} fields`, { row: row + 1 });
        }
    });
    const asNumbers = fields.map(withoutDatePart);
    return snapshotOfValues(records, asNumbers, (record, _row, f) => {
        const value = elementAt(record, f);
        const part = elementAt(fields, f).datePart;
        if (part !== undefined && value !== null && typeof value !== 'number') {
            throw new TypeError(`The ${datePartTitle(part)} of a date travels as its number, and the record carries a ${isDecimal(value) ? 'decimal' : typeof value}.`);
        }
        // A date travels by its ticks; every other value is read as a Consumer's is.
        return typeof value === 'bigint' ? { kind: 'date', ticks: value } : readRecordValue(value);
    });
}

// A field read as the values it carries: a date part's are its numbers.
function withoutDatePart(field: PivotField): PivotField {
    const { datePart, ...rest } = field;
    return datePart === undefined ? field : Object.freeze(rest);
}

/** A value of a record behind a cell: text, an exact number, a double, a date by its ticks, a Boolean, or a
 * Blank. */
export type DetailValue = string | number | bigint | boolean | SnapshotDecimal | null;

// The Snapshot of `records`, each field's value in a record read by `read`, held as recordSnapshot holds it.
function snapshotOfValues<TRecord>(records: readonly TRecord[], fields: readonly PivotField[], read: (record: TRecord, row: number, field: number) => RecordValue): {
    readonly snapshot: Snapshot;
    readonly bindings: ReadonlyMap<string, FieldBinding>;
} {
    declaredFields(fields);
    const made = fields.map(() => new FieldColumns());
    records.forEach((record, row) => {
        fields.forEach((field, f) => {
            let value: RecordValue;
            try {
                value = read(record, row, f);
            } catch (error) {
                if (error instanceof TypeError || error instanceof RangeError) {
                    throw new SnapshotError(error.message, { row: row + 1, column: field.name });
                }
                throw error;
            }
            if (field.datePart !== undefined && value.kind !== 'blank' && value.kind !== 'date') {
                throw new SnapshotError(`the value ${describe(value)} is not a date, and the field is the ${datePartTitle(field.datePart)} of a date.`,
                    { row: row + 1, column: field.name });
            }
            elementAt(made, f).append(row, value);
        });
    });
    made.forEach(columns => columns.seal(records.length));
    const snapshot = createSnapshot({ columns: made.flatMap((columns, f) => columns.inputs(f)) });
    const bindings = new Map<string, FieldBinding>();
    fields.forEach((field, f) => {
        bindings.set(field.name, bindingOf(field, elementAt(made, f).order.map(kind => snapshot.column(columnName(f, kind)))));
    });
    return { snapshot, bindings };
}

function describe(value: RecordValue): string {
    switch (value.kind) {
        case 'text': return `'${value.text}' (text)`;
        case 'decimal': return `${value.decimal.mantissa} (decimal)`;
        case 'double': return `${value.number} (number)`;
        case 'boolean': return `${value.value} (boolean)`;
        case 'blank':
        case 'date': throw new Error('Internal: a Blank and a date are a date part\'s values.');
        default: return unreachable(value);
    }
}

// ---- One row's value ----------------------------------------------------------------------------

/** The part of a date, by its ticks. */
export function datePartOfTicks(part: PivotDatePart, ticks: bigint): number {
    const parts = datePartsOfTicks(ticks);
    return datePartOf(part, parts.year, parts.month);
}

/** The Item a row's value belongs to (ADR-0060), as the Item space maps it. */
export function itemKeyAt(binding: FieldBinding, row: number): ItemKey {
    for (const column of binding.columns) {
        if (column.isBlank(row)) continue;
        switch (column.kind) {
            case 'text': return ItemKeys.text(must(column.valueAt(row)));
            case 'decimal': return ItemKeys.number(decimalToNumber(must(column.valueAt(row))));
            case 'integer': return ItemKeys.number(Number(must(column.valueAt(row))));
            case 'double': return ItemKeys.number(must(column.valueAt(row)));
            case 'date': {
                const ticks = must(column.valueAt(row));
                return binding.part === undefined ? ItemKeys.date(ticks) : ItemKeys.number(datePartOfTicks(binding.part, ticks));
            }
            case 'boolean': return ItemKeys.boolean(must(column.valueAt(row)));
            default: return unreachable(column);
        }
    }
    return ItemKeys.blank;
}

/** A row's value as a record behind a cell carries it: text, an exact number as a SnapshotDecimal, a double,
 * a date by its ticks, a Boolean, or `null` for a Blank. A date part is its number. */
export function detailAt(binding: FieldBinding, row: number): DetailValue {
    for (const column of binding.columns) {
        if (column.isBlank(row)) continue;
        switch (column.kind) {
            case 'text': return must(column.valueAt(row));
            case 'decimal': return must(column.valueAt(row));
            case 'integer': return decimalOfWhole(must(column.valueAt(row)));
            case 'double': return must(column.valueAt(row));
            case 'date': {
                const ticks = must(column.valueAt(row));
                return binding.part === undefined ? ticks : datePartOfTicks(binding.part, ticks);
            }
            case 'boolean': return must(column.valueAt(row));
            default: return unreachable(column);
        }
    }
    return null;
}

function must<T>(value: T | null): T {
    if (value === null) throw new Error('Internal: a value that is not Blank reads as null.');
    return value;
}
