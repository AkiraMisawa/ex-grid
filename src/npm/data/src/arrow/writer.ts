// One write of a Snapshot as an uncompressed Arrow IPC stream (ADR-0065), as ExGrid.Data.Arrow's
// ArrowSnapshotWriter writes one: every column's Arrow type is settled, and every value checked to be
// writable in it, before the first byte is made, so a refused write yields nothing. Text is a dictionary of
// utf8 with int32 indices, sent once before the first record batch; Decimal decimal128(38, scale); Double
// float64; Integer int64; Date date32 or a timestamp without a time zone; Boolean bool; a Blank a null slot.

import { decimalText } from '../decimal';
import { epochTicks, ticksPerDay, ticksText } from '../dates';
import { SnapshotError } from '../snapshot-error';
import { isHeldSnapshot, type Snapshot, type SnapshotColumn } from '../snapshot';
import { FlatWriter, type FlatTableSpec } from './flat-writer';
import { snapshotArrowMetadata } from './reader';
import { elementAt, unreachable } from '../must';

/** The rows of a record batch. */
const batchRows = 65_536;

/** Arrow's metadata version 5, as Arrow 1.0 and later write it. */
const metadataVersion = 4;

/** How far from 1970 a nanosecond timestamp reaches, in ticks. */
const nanosecondReach = ((1n << 63n) - 1n) / 100n;

const largestDecimal128 = 10n ** 38n - 1n;

const encoder = new TextEncoder();

/** A lone surrogate: a high one not followed by a low one, or a low one not preceded by a high one. */
const loneSurrogate = /[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]/;

/** One buffer of a record batch's body, and the rows and nulls of the column it belongs to. */
interface ColumnBody {
    readonly length: number;
    readonly nulls: number;
    readonly buffers: readonly Uint8Array[];
}

/** A column's Arrow type, settled before a byte is written, and how it makes its buffers for a batch. */
interface ColumnPlan {
    readonly field: FlatTableSpec;
    /** The column's dictionary batch, its id and its entries, for Text. */
    readonly dictionary: { readonly id: bigint; readonly entries: ColumnBody } | undefined;
    batch(start: number, length: number): ColumnBody;
}

function field(name: string, caption: string, typeType: number, type: FlatTableSpec, dictionary?: FlatTableSpec): FlatTableSpec {
    // Field: name (0), nullable (1), type_type (2), type (3), dictionary (4), children (5), custom_metadata (6).
    const metadata = caption === name ? undefined : [keyValue(snapshotArrowMetadata.caption, caption)];
    return [
        { type: 'string', value: name },
        { type: 'bool', value: true },
        { type: 'uint8', value: typeType },
        { type: 'table', value: type },
        dictionary === undefined ? undefined : { type: 'table', value: dictionary },
        { type: 'tables', value: [] },
        metadata === undefined ? undefined : { type: 'tables', value: metadata },
    ];
}

function keyValue(key: string, value: string): FlatTableSpec {
    return [{ type: 'string', value: key }, { type: 'string', value }];
}

/** A bitmap of `length` bits, least significant bit first as Arrow numbers them, and how many are set. */
function bitmapOf(length: number, set: (index: number) => boolean): { bitmap: Uint8Array; count: number } {
    const bitmap = new Uint8Array(Math.ceil(length / 8));
    let count = 0;
    let byte = 0;
    for (let i = 0; i < length; i++) {
        if (set(i)) {
            byte |= 1 << (i & 7);
            count++;
        }
        if ((i & 7) === 7 || i === length - 1) {
            bitmap[i >> 3] = byte;
            byte = 0;
        }
    }
    return { bitmap, count };
}

/** A batch of `width`-byte values, each row that is not a Blank put by `put`, with its validity bitmap, or an
 * empty one when no row is a Blank. */
function fixedWidth(column: SnapshotColumn, start: number, length: number, width: number, put: (view: DataView, at: number, row: number) => void): ColumnBody {
    const values = new Uint8Array(length * width);
    const view = new DataView(values.buffer);
    const { bitmap, count } = bitmapOf(length, i => {
        const blank = column.isBlank(start + i);
        if (!blank) put(view, i * width, start + i);
        return !blank;
    });
    const nulls = length - count;
    return { length, nulls, buffers: [nulls === 0 ? new Uint8Array(0) : bitmap, values] };
}

/** Refuses a name or a caption UTF-8 cannot carry, which Arrow's metadata would otherwise carry changed. */
function checkUnicode(text: string, what: string, column: string | undefined): void {
    if (!loneSurrogate.test(text)) return;
    const reason = `the ${what} is not valid Unicode — it holds a lone surrogate — so UTF-8 cannot carry it.`;
    throw new SnapshotError(reason, column === undefined ? {} : { column });
}

function plan(column: SnapshotColumn, dictionaryId: number, rowCount: number): ColumnPlan {
    const refuse = (row: number, reason: string): SnapshotError => new SnapshotError(reason, { row: row + 1, column: column.name });
    switch (column.kind) {
        case 'text': {
            const entries = column.dictionary;
            const encoded = entries.map((text, code) => {
                if (loneSurrogate.test(text)) {
                    // Every entry is held by a row, as a Snapshot's dictionary is made from its rows.
                    const row = Array.from({ length: rowCount }, (_, each) => each).find(each => column.codeAt(each) === code);
                    if (row === undefined) throw new Error(`Internal: the dictionary's entry ${code} is held by no row.`);
                    throw refuse(row, 'the text is not valid Unicode — it holds a lone surrogate — so UTF-8 cannot carry it.');
                }
                return encoder.encode(text);
            });
            const total = encoded.reduce((sum, bytes) => sum + bytes.byteLength, 0);
            if (total > 0x7fffffff) throw new SnapshotError('the column\'s distinct texts come to more than the 2 GiB a utf8 dictionary holds.', { column: column.name });
            const offsets = new Uint8Array(4 * (entries.length + 1));
            const offsetView = new DataView(offsets.buffer);
            const data = new Uint8Array(total);
            let at = 0;
            encoded.forEach((bytes, code) => {
                data.set(bytes, at);
                at += bytes.byteLength;
                offsetView.setInt32(4 * (code + 1), at, true);
            });
            const int32: FlatTableSpec = [{ type: 'int32', value: 32 }, { type: 'bool', value: true }];
            // DictionaryEncoding: id (0), indexType (1), isOrdered (2), dictionaryKind (3).
            const encoding: FlatTableSpec = [
                { type: 'int64', value: BigInt(dictionaryId) },
                { type: 'table', value: int32 },
                { type: 'bool', value: false },
                { type: 'int16', value: 0 },
            ];
            return {
                field: field(column.name, column.caption, 5, [], encoding),
                dictionary: { id: BigInt(dictionaryId), entries: { length: entries.length, nulls: 0, buffers: [new Uint8Array(0), offsets, data] } },
                batch: (start, length) => fixedWidth(column, start, length, 4, (view, offset, row) => {
                    view.setInt32(offset, column.codeAt(row), true);
                }),
            };
        }
        case 'decimal': {
            const values = Array.from({ length: rowCount }, (_, row) => column.valueAt(row));
            const scale = values.reduce((most, value) => (value === null ? most : Math.max(most, value.scale)), 0);
            // Each value's digits at the column's scale, a Blank's 0.
            const unscaled = values.map((value, row) => {
                if (value === null) return 0n;
                const digits = value.mantissa * 10n ** BigInt(scale - value.scale);
                if (digits > largestDecimal128 || digits < -largestDecimal128) {
                    throw refuse(row, `the value ${decimalText(value)} cannot be written exactly as decimal128(38, ${scale}): at the column's ${scale} places it passes 38 digits.`);
                }
                return digits;
            });
            // Decimal: precision (0), scale (1), bitWidth (2).
            const type: FlatTableSpec = [{ type: 'int32', value: 38 }, { type: 'int32', value: scale }, { type: 'int32', value: 128 }];
            return {
                field: field(column.name, column.caption, 7, type),
                dictionary: undefined,
                batch: (start, length) => fixedWidth(column, start, length, 16, (view, offset, row) => {
                    const word = BigInt.asUintN(128, elementAt(unscaled, row));
                    view.setBigUint64(offset, BigInt.asUintN(64, word), true);
                    view.setBigUint64(offset + 8, word >> 64n, true);
                }),
            };
        }
        case 'double':
            return {
                // FloatingPoint: precision (0), DOUBLE 2.
                field: field(column.name, column.caption, 3, [{ type: 'int16', value: 2 }]),
                dictionary: undefined,
                batch: (start, length) => fixedWidth(column, start, length, 8, (view, offset, row) => {
                    view.setBigUint64(offset, column.bitsAt(row), true);
                }),
            };
        case 'integer':
            return {
                // Int: bitWidth (0), is_signed (1).
                field: field(column.name, column.caption, 2, [{ type: 'int32', value: 64 }, { type: 'bool', value: true }]),
                dictionary: undefined,
                batch: (start, length) => fixedWidth(column, start, length, 8, (view, offset, row) => {
                    const value = column.valueAt(row);
                    if (value === null) throw new Error('Internal: a Blank is written as a value.');
                    view.setBigInt64(offset, value, true);
                }),
            };
        case 'date': return datePlan(column, rowCount, refuse);
        case 'boolean':
            return {
                field: field(column.name, column.caption, 6, []),
                dictionary: undefined,
                batch: (start, length) => {
                    const { bitmap, count } = bitmapOf(length, i => !column.isBlank(start + i));
                    const bits = bitmapOf(length, i => column.valueAt(start + i) === true).bitmap;
                    const nulls = length - count;
                    return { length, nulls, buffers: [nulls === 0 ? new Uint8Array(0) : bitmap, bits] };
                },
            };
        default: return unreachable(column);
    }
}

/** The units a Date may be written in, coarsest first, with the ticks in one and the Timestamp unit. */
const dateUnits = [
    { name: 'day', ticks: ticksPerDay, timeUnit: -1 },
    { name: 's', ticks: 10_000_000n, timeUnit: 0 },
    { name: 'ms', ticks: 10_000n, timeUnit: 1 },
    { name: 'us', ticks: 10n, timeUnit: 2 },
    { name: 'ns', ticks: 1n, timeUnit: 3 },
] as const;

/**
 * Date as date32 when every value is a midnight, and otherwise as a timestamp without a time zone in the
 * coarsest unit that holds every value exactly (ADR-0065). Nanoseconds reach only from 1677 to 2262, so a
 * column that needs them and holds a date outside those years is refused by row and column.
 */
function datePlan(column: Extract<SnapshotColumn, { kind: 'date' }>, rowCount: number, refuse: (row: number, reason: string) => SnapshotError): ColumnPlan {
    let unit = 0;
    for (let row = 0; row < rowCount; row++) {
        const ticks = column.valueAt(row);
        if (ticks === null) continue;
        const needs = dateUnits.findIndex(each => ticks % each.ticks === 0n);
        unit = Math.max(unit, needs);
    }
    const chosen = dateUnits[unit];
    if (chosen === undefined) throw new Error(`Internal: the date unit ${unit} is not one.`);
    if (chosen.name === 'ns') {
        for (let row = 0; row < rowCount; row++) {
            const ticks = column.valueAt(row);
            if (ticks === null) continue;
            const from1970 = ticks - epochTicks;
            if (from1970 >= -nanosecondReach && from1970 <= nanosecondReach) continue;
            const fraction = ticks % 10_000_000n;
            const date = `${ticksText(ticks - fraction)}.${fraction.toString().padStart(7, '0')}`;
            throw refuse(row, `the date ${date} cannot be written: the column holds a time finer than a microsecond, `
                + 'so it is written in nanoseconds, which reach only from 1677-09-21 to 2262-04-11.');
        }
    }
    if (chosen.name === 'day') {
        return {
            // Date: unit (0), DAY 0.
            field: field(column.name, column.caption, 8, [{ type: 'int16', value: 0 }]),
            dictionary: undefined,
            batch: (start, length) => fixedWidth(column, start, length, 4, (view, offset, row) => {
                view.setInt32(offset, Number((ticksOf(column, row) - epochTicks) / ticksPerDay), true);
            }),
        };
    }
    return {
        // Timestamp: unit (0), timezone (1), left out for clock values without a time zone.
        field: field(column.name, column.caption, 10, [{ type: 'int16', value: chosen.timeUnit }]),
        dictionary: undefined,
        batch: (start, length) => fixedWidth(column, start, length, 8, (view, offset, row) => {
            const from1970 = ticksOf(column, row) - epochTicks;
            view.setBigInt64(offset, chosen.name === 'ns' ? from1970 * 100n : from1970 / chosen.ticks, true);
        }),
    };
}

function ticksOf(column: Extract<SnapshotColumn, { kind: 'date' }>, row: number): bigint {
    const ticks = column.valueAt(row);
    if (ticks === null) throw new Error('Internal: a Blank is written as a value.');
    return ticks;
}

/** A message: its framing, its metadata padded to eight bytes, and its body. */
function message(headerType: number, header: FlatTableSpec, body: readonly Uint8Array[], bodyLength: number): Uint8Array[] {
    // Message: version (0), header_type (1), header (2), bodyLength (3).
    const metadata = FlatWriter.write([
        { type: 'int16', value: metadataVersion },
        { type: 'uint8', value: headerType },
        { type: 'table', value: header },
        { type: 'int64', value: BigInt(bodyLength) },
    ]);
    const prefix = new Uint8Array(8);
    const view = new DataView(prefix.buffer);
    view.setInt32(0, -1, true);
    view.setInt32(4, metadata.byteLength, true);
    return [prefix, metadata, ...body];
}

/** A record batch's header and body: one node for each column, and its buffers padded to eight bytes. */
function batchMessage(headerType: number, length: number, columns: readonly ColumnBody[], wrap: (batch: FlatTableSpec) => FlatTableSpec): Uint8Array[] {
    const nodes = new Uint8Array(16 * columns.length);
    const nodeView = new DataView(nodes.buffer);
    const buffers = columns.flatMap(column => column.buffers);
    const bufferSpecs = new Uint8Array(16 * buffers.length);
    const bufferView = new DataView(bufferSpecs.buffer);
    const body: Uint8Array[] = [];
    let offset = 0;
    columns.forEach((column, c) => {
        nodeView.setBigInt64(16 * c, BigInt(column.length), true);
        nodeView.setBigInt64(16 * c + 8, BigInt(column.nulls), true);
    });
    buffers.forEach((buffer, b) => {
        bufferView.setBigInt64(16 * b, BigInt(offset), true);
        bufferView.setBigInt64(16 * b + 8, BigInt(buffer.byteLength), true);
        const padded = Math.ceil(buffer.byteLength / 8) * 8;
        body.push(buffer);
        if (padded > buffer.byteLength) body.push(new Uint8Array(padded - buffer.byteLength));
        offset += padded;
    });
    // RecordBatch: length (0), nodes (1), buffers (2).
    const batch: FlatTableSpec = [
        { type: 'int64', value: BigInt(length) },
        { type: 'structs', value: nodes, count: columns.length },
        { type: 'structs', value: bufferSpecs, count: buffers.length },
    ];
    return message(headerType, wrap(batch), body, offset);
}

/**
 * Writes `snapshot` as an uncompressed Arrow IPC stream (ADR-0065): its rows in order, in record batches of
 * 65,536 rows, with its captions, Record Key and version in the metadata, so ExGrid.Data.Arrow, pyarrow,
 * DuckDB and Polars read it, and it reads back as the Snapshot it was. HTTP's own compression does the rest.
 * Every value is checked before a byte is made, so a refused write yields nothing: a {@link SnapshotError}
 * names the row and the column of a Decimal past decimal128's 38 digits at the column's scale, a Date that
 * needs nanoseconds outside their range, or text that is not valid Unicode. A value that is not a Snapshot
 * this package made is refused with a `TypeError`.
 */
export function writeArrowSnapshot(snapshot: Snapshot): Uint8Array {
    if (!isHeldSnapshot(snapshot)) throw new TypeError('Only a Snapshot that createSnapshot or readArrowSnapshot made is written.');
    for (const column of snapshot.columns) {
        checkUnicode(column.name, 'column\'s name', column.name);
        checkUnicode(column.caption, 'column\'s caption', column.name);
    }
    let dictionaries = 0;
    const plans = snapshot.columns.map(column => plan(column, column.kind === 'text' ? dictionaries++ : -1, snapshot.rowCount));
    const metadata = [keyValue(snapshotArrowMetadata.version, snapshot.version.toString())];
    if (snapshot.recordKey !== undefined) metadata.push(keyValue(snapshotArrowMetadata.recordKey, snapshot.recordKey));
    // Schema: endianness (0), fields (1), custom_metadata (2).
    const schema: FlatTableSpec = [
        { type: 'int16', value: 0 },
        { type: 'tables', value: plans.map(each => each.field) },
        { type: 'tables', value: metadata },
    ];
    const parts: Uint8Array[] = message(1, schema, [], 0);
    // A dictionary is sent once, before the first record batch; a Snapshot of no rows sends none.
    for (const { dictionary } of snapshot.rowCount > 0 ? plans : []) {
        if (dictionary === undefined) continue;
        // DictionaryBatch: id (0), data (1), isDelta (2).
        parts.push(...batchMessage(2, dictionary.entries.length, [dictionary.entries], batch => [
            { type: 'int64', value: dictionary.id },
            { type: 'table', value: batch },
            { type: 'bool', value: false },
        ]));
    }
    for (let start = 0; start < snapshot.rowCount; start += batchRows) {
        const length = Math.min(batchRows, snapshot.rowCount - start);
        parts.push(...batchMessage(3, length, plans.map(each => each.batch(start, length)), batch => batch));
    }
    // The end-of-stream marker: a continuation and a length of 0.
    const end = new Uint8Array(8);
    new DataView(end.buffer).setInt32(0, -1, true);
    parts.push(end);
    const stream = new Uint8Array(parts.reduce((sum, part) => sum + part.byteLength, 0));
    let at = 0;
    for (const part of parts) {
        stream.set(part, at);
        at += part.byteLength;
    }
    return stream;
}
