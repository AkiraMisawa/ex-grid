// What the shared cases' runners of @ex-grid/data read alike: a case's JSON, and a Snapshot or a value as
// tests/cases/README.md says a case writes one. A key a case leaves out fails the case.
import {
    createSnapshot, decimalFromText, decimalText, ticksFromText, ticksText, type Snapshot, type SnapshotColumn, type SnapshotColumnInput,
    type SnapshotKind,
} from '../src/index';

/** A value of a case, as JSON holds it. */
export type Json = null | boolean | number | string | readonly Json[] | { readonly [key: string]: Json };
export type JsonObject = { readonly [key: string]: Json };

export function isObject(value: Json | undefined): value is JsonObject {
    return typeof value === 'object' && value !== null && !isList(value);
}

/** The key's value, which the case must write: a missing key fails the case rather than taking a default. */
export function key(object: Json | undefined, name: string): Json {
    if (!isObject(object)) throw new Error(`Expected an object holding '${name}'.`);
    const value = object[name];
    if (value === undefined) throw new Error(`The case leaves out '${name}'.`);
    return value;
}

export function text(value: Json): string {
    if (typeof value !== 'string') throw new Error(`Expected a string, not ${JSON.stringify(value)}.`);
    return value;
}

export function isList(value: Json): value is readonly Json[] {
    return Array.isArray(value);
}

export function list(value: Json): readonly Json[] {
    if (!isList(value)) throw new Error(`Expected a list, not ${JSON.stringify(value)}.`);
    return value;
}

/** Each case of a file, which is an object. */
export function cases(file: Json): readonly JsonObject[] {
    return list(key(file, 'cases')).map(entry => {
        if (!isObject(entry)) throw new Error(`A case is an object, not ${JSON.stringify(entry)}.`);
        return entry;
    });
}

/** `words` as a pattern that matches them at the start of a message. */
export function startingWith(words: string): RegExp {
    return new RegExp(`^${words.replaceAll(/[\\^$.*+?()[\]{}|/-]/g, '\\$&')}`);
}

export function kindOf(value: Json): SnapshotKind {
    const kind = text(value);
    if (kind === 'text' || kind === 'decimal' || kind === 'double' || kind === 'integer' || kind === 'date' || kind === 'boolean') return kind;
    throw new Error(`'${kind}' is not a kind.`);
}

/** Text as a case writes it: a string, or `{"utf16": …}` with `\uXXXX` escapes for text JSON readers refuse. */
export function textValue(value: Json): string {
    if (typeof value === 'string') return value;
    return text(key(value, 'utf16')).replace(/\\u([0-9a-fA-F]{4})/g, (_, hex: string) => String.fromCharCode(Number.parseInt(hex, 16)));
}

/** A double's IEEE 754 bits, as a case writes the double. */
export function doubleBits(value: Json): bigint {
    const view = new DataView(new ArrayBuffer(8));
    if (isObject(value)) return BigInt(`0x${text(key(value, 'bits'))}`);
    if (value === 'Infinity') view.setFloat64(0, Number.POSITIVE_INFINITY);
    else if (value === '-Infinity') view.setFloat64(0, Number.NEGATIVE_INFINITY);
    else if (typeof value === 'number') view.setFloat64(0, value);
    else throw new Error(`${JSON.stringify(value)} is not a double.`);
    return view.getBigUint64(0);
}

export function doubleOf(value: Json): number {
    const view = new DataView(new ArrayBuffer(8));
    view.setBigUint64(0, doubleBits(value));
    return view.getFloat64(0);
}

export function whole(value: Json): bigint {
    if (typeof value === 'number' && Number.isSafeInteger(value)) return BigInt(value);
    return BigInt(text(value));
}

/** A column as a case writes it: its `name`, `kind` and `values`, under `caption`. */
export function columnInput(column: Json, caption: string | undefined): SnapshotColumnInput {
    const name = text(key(column, 'name'));
    const values = list(key(column, 'values'));
    const kind = kindOf(key(column, 'kind'));
    const each = <T>(read: (value: Json) => T): (T | null)[] => values.map(value => (value === null ? null : read(value)));
    const named = caption === undefined ? { name } : { name, caption };
    switch (kind) {
        case 'text': return { ...named, kind, values: each(textValue) };
        case 'decimal': return { ...named, kind, values: each(value => decimalFromText(text(value))) };
        case 'double': return { ...named, kind, values: each(doubleOf) };
        case 'integer': return { ...named, kind, values: each(whole) };
        case 'date': return { ...named, kind, values: each(value => ticksFromText(text(value))) };
        case 'boolean': return { ...named, kind, values: each(value => { if (typeof value !== 'boolean') throw new Error('Expected a boolean.'); return value; }) };
        default: throw new Error(`'${String(kind satisfies never)}' is not a kind.`);
    }
}

/** The Snapshot a case gives, every key written: its version, its Record Key, and each column's name,
 * caption, kind and values. */
export function build(snapshot: Json): Snapshot {
    const columns = list(key(snapshot, 'columns')).map(column => columnInput(column, text(key(column, 'caption'))));
    const recordKey = key(snapshot, 'recordKey');
    return createSnapshot({ columns, version: whole(key(snapshot, 'version')), ...(recordKey === null ? {} : { recordKey: text(recordKey) }) });
}

/** An integer as the cases write one: a number where a double holds it exactly, its text past that. */
export function integer(value: bigint): Json {
    return value >= -(2n ** 53n) && value <= 2n ** 53n ? Number(value) : value.toString();
}

/** A row's value as tests/cases/README.md writes it for its kind, a Blank `null`. */
export function valueOf(column: SnapshotColumn, row: number): Json {
    if (column.isBlank(row)) return null;
    switch (column.kind) {
        case 'text': return column.valueAt(row);
        case 'decimal': {
            const value = column.valueAt(row);
            if (value === null) throw new Error('Internal: a held decimal is Blank.');
            return decimalText(value);
        }
        case 'double': {
            const value = column.valueAt(row);
            if (value === null) throw new Error('Internal: a held double is Blank.');
            if (value === Number.POSITIVE_INFINITY) return 'Infinity';
            if (value === Number.NEGATIVE_INFINITY) return '-Infinity';
            if (Number.isNaN(value) || Object.is(value, -0)) return { bits: column.bitsAt(row).toString(16).padStart(16, '0') };
            return value;
        }
        case 'integer': {
            const value = column.valueAt(row);
            if (value === null) throw new Error('Internal: a held integer is Blank.');
            return integer(value);
        }
        case 'date': {
            const value = column.valueAt(row);
            if (value === null) throw new Error('Internal: a held date is Blank.');
            const written = ticksText(value);
            return written.endsWith('T00:00:00') ? written.slice(0, -'T00:00:00'.length) : written;
        }
        case 'boolean': return column.valueAt(row);
        default: throw new Error(`'${String(column satisfies never)}' is not a column.`);
    }
}

/** The file, as the case writes it: UTF-8 text, `{"hex": …}` bytes, or `{"repeat": text, "times": n}`. */
export function fileOf(value: Json): Uint8Array {
    const encoder = new TextEncoder();
    const pieces = list(value).map(piece => {
        if (typeof piece === 'string') return encoder.encode(piece);
        if (isObject(piece) && piece['hex'] !== undefined) {
            const hex = text(piece['hex']).replaceAll(' ', '');
            return Uint8Array.from({ length: hex.length / 2 }, (_, i) => Number.parseInt(hex.slice(i * 2, i * 2 + 2), 16));
        }
        const once = encoder.encode(text(key(piece, 'repeat')));
        const times = key(piece, 'times');
        if (typeof times !== 'number' || !Number.isInteger(times) || times < 0) throw new Error(`Expected a number of times, not ${JSON.stringify(times)}.`);
        const repeated = new Uint8Array(once.length * times);
        for (let i = 0; i < times; i++) repeated.set(once, i * once.length);
        return repeated;
    });
    const bytes = new Uint8Array(pieces.reduce((sum, piece) => sum + piece.length, 0));
    let at = 0;
    for (const piece of pieces) {
        bytes.set(piece, at);
        at += piece.length;
    }
    return bytes;
}
