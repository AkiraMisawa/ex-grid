// Reads the engine's shared cases (tests/cases/pivot/; ADR-0370 sections 2 and 5), which xUnit runs against
// the C# engine as well: a data set of tests/cases/pivot/data/, a layout as the layout's JSON writes one, the
// report's locale and words, and what the engine answers. tests/cases/README.md says what a file holds; the
// C# runner, dotnet/tests/ExPivot.Engine.Tests/Cases/PivotCases.cs, is the one that writes the answers.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createSnapshot, decimalFromText, SnapshotError, type Snapshot, type SnapshotColumnInput } from '@ex-grid/data';
import { Temporal } from 'temporal-polyfill';
import {
    japanesePivotWords, PivotFormatError, PivotOrderKeyError, pivotRecordField, PivotVersionError, readPivotLayoutJson,
    unitedKingdomPivotWords, type PivotDatePart, type PivotFieldType, type PivotItemKey, type PivotLabel, type PivotLayout, type PivotOptions,
    type PivotOrderKey, type PivotRecordField,
} from '../src/index';
import { defined } from '../src/must';
import { isDecimal } from '../src/items/record-value';
import { invariantNumbers } from '../src/text/invariant';
import { formatDouble } from '../src/text/number-text';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../tests/cases/pivot');

/** A JSON value as a case file holds it. */
export type Json = null | boolean | number | string | readonly Json[] | { readonly [key: string]: Json };

/** A JSON object of a case file. */
export type JsonObject = { readonly [key: string]: Json };

/** One file of cases. */
export interface CaseFile {
    readonly file: string;
    readonly document: JsonObject;
    readonly cases: readonly JsonObject[];
}

function isObject(value: Json | undefined): value is JsonObject {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function readJson(file: string): JsonObject {
    return object(parseJson(fs.readFileSync(file, 'utf8')));
}

/** A JSON document read as the value it holds. */
export function parseJson(written: string): Json {
    const read: unknown = JSON.parse(written);
    if (!isJson(read)) throw new Error(`${written} is not JSON.`);
    return read;
}

function isJson(value: unknown): value is Json {
    if (value === null || typeof value === 'boolean' || typeof value === 'number' || typeof value === 'string') return true;
    if (Array.isArray(value)) return value.every(isJson);
    return typeof value === 'object' && Object.values(value).every(isJson);
}

/** Every file of the kind, by name. */
export function caseFiles(kind: string): CaseFile[] {
    return fs.readdirSync(root)
        .filter(name => name.endsWith('.json'))
        .toSorted()
        .map(name => {
            const document = readJson(path.join(root, name));
            return { file: name, document, cases: array(must(document, 'cases')).map(object) };
        })
        .filter(file => file.document['kind'] === kind);
}

/** A key the object must hold. */
export function must(node: JsonObject, key: string): Json {
    const value = node[key];
    if (value === undefined) throw new Error(`The case names no '${key}'.`);
    return value;
}

/** The case's own value of a key, or else its file's. */
export function inherited(file: JsonObject, entry: JsonObject, key: string): Json {
    const value = entry[key] ?? file[key];
    if (value === undefined) throw new Error(`Neither the case nor its file names '${key}'.`);
    return value;
}

/** A JSON value read as a string. */
export function text(value: Json): string {
    if (typeof value !== 'string') throw new TypeError(`${JSON.stringify(value)} is not a string.`);
    return value;
}

/** A text the engine may not have, as a case writes it: `null` where it has none, as the C# runner writes a
 * missing string. */
export function orNull(written: string | undefined): Json {
    return written === undefined ? null : written;
}

/** A JSON value read as an array. */
export function array(value: Json): readonly Json[] {
    if (!isArray(value)) throw new TypeError(`${JSON.stringify(value)} is not an array.`);
    return value;
}

function isArray(value: Json): value is readonly Json[] {
    return Array.isArray(value);
}

/** A JSON value read as an object. */
export function object(value: Json): JsonObject {
    if (!isObject(value)) throw new TypeError(`${JSON.stringify(value)} is not an object.`);
    return value;
}

// ---- The data -----------------------------------------------------------------------------------

/** A data set: its fields, each reading a record's column, its records, and the values its Order Keys were
 * called with, in the order of the calls. */
export interface DataSet {
    readonly fields: readonly PivotRecordField<readonly unknown[]>[];
    readonly records: readonly (readonly unknown[])[];
    readonly keyCalls: readonly string[];
}

// The data set a case names: one of tests/cases/pivot/data/ by name, or the set itself, as a differential run
// makes one.
function setOf(file: JsonObject, entry: JsonObject): JsonObject {
    const named = inherited(file, entry, 'data');
    return typeof named === 'string' ? readJson(path.join(root, 'data', `${named}.json`)) : object(named);
}

/** The data set a case pivots: `data` names one of tests/cases/pivot/data/, or is the set itself, and the case's
 * own `fields` and `records`, when it has them, stand in for the set's. */
export function dataSet(file: JsonObject, entry: JsonObject): DataSet {
    const set = setOf(file, entry);
    const columns = array(must(set, 'columns')).map(text);
    const keyCalls: string[] = [];
    const fields = array(entry['fields'] ?? must(set, 'fields')).map(declared => field(object(declared), columns, keyCalls));
    const records = array(entry['records'] ?? must(set, 'records')).map(record => {
        const values = array(record);
        if (values.length !== columns.length) throw new Error(`A record holds ${values.length} values, and the data set has ${columns.length} columns.`);
        return values.map(caseValue);
    });
    return { fields, records, keyCalls };
}

/** The Snapshot a data set's columns make, when the set is a Snapshot's: `snapshot` names each column's name,
 * kind and caption, and the column that is its Record Key, where it has one; and `rows` holds a row's values in column order, written as {@link value} reads them; the
 * case's own `rows`, when it has them, stand in for the set's. `undefined` for a set of records. */
export function snapshotOf(file: JsonObject, entry: JsonObject): Snapshot | undefined {
    const shape = snapshotShapeOf(file, entry);
    if (shape === undefined) return undefined;
    const key = shape['recordKey'];
    return rowsSnapshot(shape, array(entry['rows'] ?? must(setOf(file, entry), 'rows')), key === undefined ? undefined : text(key));
}

/** The shape of a data set's Snapshot, when the set is a Snapshot's: what a Change Batch's records are written in. */
export function snapshotShapeOf(file: JsonObject, entry: JsonObject): JsonObject | undefined {
    const shape = setOf(file, entry)['snapshot'];
    return shape === undefined ? undefined : object(shape);
}

/** Rows written in a Snapshot's shape, as a Snapshot of their own: keyed by `recordKey` where it names a
 * column, and with no key otherwise, as a Change Batch's records are. */
export function rowsSnapshot(shape: JsonObject, written: readonly Json[], recordKey: string | undefined): Snapshot {
    const columns = array(must(shape, 'columns')).map(object);
    const rows = written.map(row => {
        const values = array(row);
        if (values.length !== columns.length) throw new Error(`A row holds ${values.length} values, and the Snapshot has ${columns.length} columns.`);
        return values.map(caseValue);
    });
    return createSnapshot({
        ...(recordKey === undefined ? {} : { recordKey }),
        columns: columns.map((column, c) => {
            const caption = column['caption'];
            return columnInput(text(must(column, 'name')), text(must(column, 'kind')), rows.map(row => row[c]), caption === undefined ? undefined : text(caption));
        }),
    });
}

// A column of a Snapshot data set, each value held to the column's kind as the C# builder's Append is.
function columnInput(name: string, kind: string, values: readonly unknown[], caption: string | undefined): SnapshotColumnInput {
    const of = <T>(is: (value: unknown) => value is T): (T | null)[] => values.map((value, row) => {
        if (value === null || is(value)) return value;
        throw new TypeError(`Row ${row + 1} of the ${kind} column '${name}' holds a ${typeof value}.`);
    });
    const named = { name, ...(caption === undefined ? {} : { caption }) };
    switch (kind) {
        case 'text': return { ...named, kind, values: of((v): v is string => typeof v === 'string') };
        case 'decimal': return { ...named, kind, values: of(isDecimal) };
        case 'double': return { ...named, kind, values: of((v): v is number => typeof v === 'number') };
        case 'integer': return { ...named, kind, values: of((v): v is bigint | number => typeof v === 'bigint' || typeof v === 'number') };
        case 'date': return { ...named, kind, values: of((v): v is Temporal.PlainDate | Temporal.PlainDateTime => v instanceof Temporal.PlainDate || v instanceof Temporal.PlainDateTime) };
        case 'boolean': return { ...named, kind, values: of((v): v is boolean => typeof v === 'boolean') };
        default: throw new Error(`'${kind}' is not a kind of column the cases name.`);
    }
}

function field(declared: JsonObject, columns: readonly string[], keyCalls: string[]): PivotRecordField<readonly unknown[]> {
    const name = text(must(declared, 'name'));
    const reads = declared['of'] === undefined ? name : text(declared['of']);
    const at = columns.indexOf(reads);
    if (at < 0) throw new Error(`The field '${name}' reads '${reads}', which is no column of the data set.`);
    const caption = declared['caption'];
    const format = declared['format'];
    const itemOrder = declared['itemOrder'];
    const datePart = declared['datePart'];
    const orderKey = declared['orderKey'];
    return pivotRecordField<readonly unknown[]>({
        name,
        type: fieldType(text(must(declared, 'type'))),
        value: record => record[at],
        ...(caption === undefined ? {} : { caption: text(caption) }),
        ...(format === undefined ? {} : { format: text(format) }),
        ...(itemOrder === undefined ? {} : { itemOrder: array(itemOrder).map(caseValue) }),
        ...(datePart === undefined ? {} : { datePart: part(text(datePart)) }),
        ...(orderKey === undefined ? {} : { orderKey: orderKeyOf(text(orderKey), keyCalls) }),
    });
}

function fieldType(name: string): PivotFieldType {
    if (name === 'text' || name === 'number' || name === 'date' || name === 'boolean') return name;
    throw new Error(`'${name}' is not a field type.`);
}

function part(name: string): PivotDatePart {
    if (name === 'year' || name === 'quarter' || name === 'month') return name;
    throw new Error(`'${name}' is not a date part.`);
}

/**
 * A value as a case writes it: a JSON string is text, a number a double, `true` and `false` Booleans, `null`
 * a Blank; `{"decimal": "100.50"}` an exact decimal, `{"integer": "7"}` a 64-bit integer, `{"double":
 * "NaN"}` a double JSON cannot hold, and `{"date": "2026-01-10T13:45:00"}` a date, its time optional.
 */
export function caseValue(node: Json): unknown {
    if (node === null || typeof node === 'string' || typeof node === 'number' || typeof node === 'boolean') return node;
    const typed = object(node);
    const entries = Object.entries(typed);
    const [kind, written] = defined(entries[0], 'a typed value names its kind');
    if (entries.length !== 1) throw new Error(`${JSON.stringify(node)} is not a value a case writes.`);
    const given = text(written);
    switch (kind) {
        case 'decimal': return decimalFromText(given);
        case 'integer': return BigInt(given);
        case 'double': return Number(given);
        case 'date': return given.includes('T') ? Temporal.PlainDateTime.from(given) : Temporal.PlainDate.from(given);
        default: throw new Error(`'${kind}' is not a kind of value a case writes.`);
    }
}

// The Order Keys a case names, each the Consumer's function the C# tests declared, noting each value it is
// called with as .NET's invariant text writes it.
function orderKeyOf(name: string, keyCalls: string[]): PivotOrderKey {
    const key = orderKeyNamed(name);
    return value => {
        keyCalls.push(typeof value === 'number' ? formatDouble(value, undefined, invariantNumbers) : String(value));
        return key(value);
    };
}

function orderKeyNamed(name: string): PivotOrderKey {
    switch (name) {
        case 'tenor': return key => tenor(String(key));
        case 'tenorButOnIsText': return key => (key === 'ON' ? 'zero' : tenor(String(key)));
        case 'tenorThrowsOn7Y': return key => {
            if (key === '7Y') throw new Error('no 7Y bucket');
            return tenor(String(key));
        };
        case 'absolute': return key => Math.abs(Number(key));
        default: throw new Error(`'${name}' is not an Order Key the cases declare.`);
    }
}

/** A tenor's length in days, as a desk would key it: ON and TN first, then weeks, months and years, a
 * compound tenor summed. Anything else has no key. */
export function tenor(given: string): number | undefined {
    if (given === 'ON') return 0;
    if (given === 'TN') return 1;
    let days = 0;
    let number = 0;
    let any = false;
    for (const c of given) {
        if (c >= '0' && c <= '9') {
            number = number * 10 + (c.charCodeAt(0) - 48);
            continue;
        }
        const unit = c === 'D' ? 1 : c === 'W' ? 7 : c === 'M' ? 30 : c === 'Y' ? 360 : -1;
        if (unit < 0 || number === 0) return undefined;
        days += number * unit;
        number = 0;
        any = true;
    }
    return any && number === 0 ? days : undefined;
}

// ---- The layout and the options -----------------------------------------------------------------

/** A layout as the layout's JSON writes one, its version left out. */
export function layoutOf(written: Json): PivotLayout {
    return readPivotLayoutJson(JSON.stringify({ version: 1, ...object(written) }));
}

/** An Item's key as a layout's JSON writes one. */
export function itemKeyOf(written: Json): PivotItemKey {
    const [placement] = layoutOf({ rows: [{ field: 'f', hiddenItems: [written] }] }).rows;
    const key = placement?.hiddenItems[0];
    if (key === undefined) throw new Error(`${JSON.stringify(written)} is not an Item's key.`);
    return key;
}

/** The report's options: `locale`, a .NET culture name or `invariant`, and `words` ({@link wordsOf}). */
export function optionsOf(file: JsonObject, entry: JsonObject): PivotOptions {
    const locale = text(inherited(file, entry, 'locale'));
    const label = wordsOf(inherited(file, entry, 'words'));
    return label === undefined ? { locale } : { locale, label };
}

/** The words a case names: `english`, `japanese` or `unitedKingdom`, or a Consumer's own, an object of a word
 * by its id, every other id left to the English. */
export function wordsOf(words: Json): PivotLabel | undefined {
    if (typeof words !== 'string') {
        const replaced = new Map(Object.entries(object(words)).map(([id, word]) => [id, text(word)]));
        return id => replaced.get(id);
    }
    switch (words) {
        case 'english': return undefined;
        case 'japanese': return japanesePivotWords;
        case 'unitedKingdom': return unitedKingdomPivotWords;
        default: throw new Error(`'${words}' is not a set of words the cases name.`);
    }
}

// ---- Refusals -----------------------------------------------------------------------------------

/** What a refusal is named in a case, and its message. */
export function refusalOf(thrown: unknown): JsonObject {
    if (!(thrown instanceof Error)) throw new Error(`The engine threw ${String(thrown)}, which is not an Error.`, { cause: thrown });
    let refusal: string;
    if (thrown instanceof RangeError) refusal = 'outOfRange';
    else if (thrown instanceof TypeError) refusal = 'argument';
    else if (thrown instanceof PivotVersionError) refusal = 'version';
    else if (thrown instanceof PivotFormatError) refusal = 'format';
    else if (thrown instanceof SnapshotError) refusal = 'snapshot';
    else if (thrown instanceof PivotOrderKeyError || thrown.constructor === Error) refusal = 'invalidOperation';
    else throw thrown;
    return { refusal, saying: thrown.message };
}
