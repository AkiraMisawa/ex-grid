// What the shared cases' runners of @ex-grid/data read alike: a case's JSON, and a Snapshot or a value as
// tests/cases/README.md says a case writes one. A key a case leaves out fails the case.
import { createSnapshot, decimalFromText, ticksFromText, type Snapshot, type SnapshotColumnInput, type SnapshotKind } from '../src/index';

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
