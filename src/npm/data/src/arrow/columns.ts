// Reads one Arrow column into one Snapshot column, a chunk of rows at a time, from Arrow's buffers, as
// ExGrid.Data.Arrow's ColumnReaders does (ADR-0065), with the same refusals in the same order: a value the
// column's kind cannot hold fails the load, naming its row and the column.

import { decimalLimit, maxDecimalScale, unscaledText } from '../decimal';
import { epochTicks, maxTicks, ticksPerDay } from '../dates';
import { SnapshotError, grouped } from '../snapshot-error';
import { BooleanBuilder, DecimalBuilder, DoubleBuilder, TextBuilder, WholeBuilder, type AnyColumnBuilder } from '../snapshot';
import { fieldTypeName, typeName, type ArrowField, type IntType, type TimeUnit } from './schema';
import { elementAt, int64Max, unreachable } from '../must';

/** The rows read between two checkpoints. */
export const chunkRows = 8192;

/** One column of a record batch, or a dictionary's entries: its rows, its nulls and its buffers. */
export interface ArrayData {
    readonly length: number;
    /** The nulls the batch says it holds; a negative count is unknown, and is read from the bitmap. */
    readonly nullCount: bigint;
    readonly buffers: readonly Uint8Array[];
}

/** A dictionary as the stream has sent it so far: its entries, in the parts a delta added, and a
 * generation that changes whenever it is replaced or extended. */
export interface DictionaryData {
    readonly generation: number;
    readonly parts: readonly ArrayData[];
    readonly length: number;
}

/** The dictionaries the stream has sent, by id, as a record batch is read. */
export type Dictionaries = ReadonlyMap<bigint, DictionaryData>;

/** UTC's names, any case: the IANA names of UTC, each also under `Etc/`, and the offsets and designator
 * that say UTC (ADR-0065). */
const utcZones = [
    'UTC', 'Etc/UTC', 'GMT', 'Etc/GMT', 'UCT', 'Etc/UCT', 'Universal', 'Etc/Universal', 'Zulu', 'Etc/Zulu',
    'Greenwich', 'Etc/Greenwich', 'GMT0', 'Etc/GMT0', 'GMT+0', 'Etc/GMT+0', 'GMT-0', 'Etc/GMT-0',
    '+00:00', '-00:00', 'Z',
].map(zone => zone.toUpperCase());

const strict = new TextDecoder('utf-8', { fatal: true, ignoreBOM: true });

function viewOf(bytes: Uint8Array): DataView {
    return new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
}

/** The text UTF-8 bytes hold, decoded strictly, or `refusal` when they are not valid UTF-8. */
function utf8(bytes: Uint8Array, refusal: () => SnapshotError): string {
    try {
        return strict.decode(bytes);
    } catch (error) {
        if (error instanceof TypeError) throw refusal();
        throw error;
    }
}

export abstract class ColumnReader {
    readonly builder: AnyColumnBuilder;
    /** Scratch the column reuses for every chunk: the chunk's Blanks, one byte a row. */
    readonly #blanks = new Uint8Array(chunkRows);

    constructor(builder: AnyColumnBuilder) {
        this.builder = builder;
    }

    get name(): string {
        return this.builder.name;
    }

    /** Appends rows [`start`, `start` + `length`) of this column's array in one record batch; `rowBase`
     * rows came before them. */
    abstract read(data: ArrayData, start: number, length: number, rowBase: number, dictionaries: Dictionaries): void;

    /** The buffers the column takes from a record batch, after its validity bitmap: its values, or its
     * offsets and data. A view's data buffers are counted apart (`variadic`). */
    abstract readonly buffers: number;

    /** Whether the column's buffers include data buffers whose count the batch says, as a view's do. */
    get variadic(): boolean {
        return false;
    }

    /** Fails the load, naming the row — the `index`th of a chunk after `rowBase` rows — and this column. */
    protected refuse(rowBase: number, index: number, reason: string): SnapshotError {
        return new SnapshotError(reason, { row: rowBase + index + 1, column: this.name });
    }

    /** Fails the load, naming this column, for an array whose buffers do not hold what its type and length say. */
    protected malformed(what: string): SnapshotError {
        return new SnapshotError(`the Arrow stream is malformed: ${what}.`, { column: this.name });
    }

    /** The chunk's Blanks from the array's validity bitmap, or `undefined` when it holds none. */
    protected blanksOf(data: ArrayData, start: number, length: number): Uint8Array | undefined {
        if (data.nullCount === 0n) return undefined;
        const validity = elementAt(data.buffers, 0);
        if (validity.byteLength === 0) {
            if (data.nullCount < 0n) return undefined;
            throw this.malformed('its nulls have no validity bitmap');
        }
        if (validity.byteLength * 8 < data.length) throw this.malformed('its validity bitmap is shorter than its rows');
        const bits = viewOf(validity);
        let any = false;
        for (let i = 0; i < length; i++) {
            const bit = start + i;
            const blank = (bits.getUint8(bit >> 3) & (1 << (bit & 7))) === 0;
            this.#blanks[i] = blank ? 1 : 0;
            any ||= blank;
        }
        return any ? this.#blanks : undefined;
    }

    /** The chunk's value buffer, checked to hold `width` bytes for each of its rows. */
    protected valuesOf(data: ArrayData, start: number, length: number, width: number): DataView {
        const values = viewOf(elementAt(data.buffers, 1));
        if ((start + length) * width > values.byteLength) throw this.malformed('its value buffer holds fewer values than its rows');
        return values;
    }

    /**
     * Declares the Snapshot column `field` maps to, by ADR-0065's table, and returns what reads it. A type
     * outside the table, or a timestamp in a time zone other than UTC, is refused, naming the column and the type.
     */
    static for(field: ArrowField, caption: string): ColumnReader {
        const name = field.name;
        const type = field.type;
        if (field.dictionary !== undefined) {
            if (type.id === 'utf8' || type.id === 'large_utf8' || type.id === 'utf8_view') {
                return new DictionaryReader(new TextBuilder(name, caption), field.dictionary.id, field.dictionary.indexType, type.id);
            }
            throw new SnapshotError(`the Arrow type ${fieldTypeName(field)} is not one a Snapshot reads.`, { column: name });
        }
        switch (type.id) {
            case 'utf8': return new Utf8Reader(new TextBuilder(name, caption), false);
            case 'large_utf8': return new Utf8Reader(new TextBuilder(name, caption), true);
            case 'utf8_view': return new Utf8ViewReader(new TextBuilder(name, caption));
            case 'decimal': return new DecimalReader(new DecimalBuilder(name, caption), type.bitWidth / 8, type.scale, typeName(type));
            case 'float':
                if (type.precision === 'half') break;
                return new DoubleReader(new DoubleBuilder(name, caption), type.precision === 'single');
            case 'int': return new IntegerReader(new WholeBuilder('integer', name, caption), type);
            case 'date': return new DateReader(new WholeBuilder('date', name, caption), type.unit === 'day' ? 'day' : 'ms', 4 * (type.unit === 'day' ? 1 : 2), typeName(type));
            case 'timestamp': {
                const zone = type.zone;
                if (zone !== undefined && zone.trim() !== '' && !utcZones.includes(zone.trim().toUpperCase())) {
                    throw new SnapshotError(`the Arrow type ${typeName(type)} is a timestamp in the time zone '${zone}', which a Snapshot does not read: `
                        + 'converting it would need a time zone database. Write it in UTC, or as clock values without a time zone.', { column: name });
                }
                return new DateReader(new WholeBuilder('date', name, caption), type.unit, 8, typeName(type));
            }
            case 'time': return new TimeReader(new WholeBuilder('date', name, caption), type.bitWidth === 64, type.unit, typeName(type));
            case 'bool': return new BooleanReader(new BooleanBuilder(name, caption));
            case 'other': break;
            default: return unreachable(type);
        }
        throw new SnapshotError(`the Arrow type ${typeName(type)} is not one a Snapshot reads.`, { column: name });
    }
}

/** `utf8` and `large_utf8`: each value's UTF-8 bytes, decoded strictly, go to the column. */
class Utf8Reader extends ColumnReader {
    readonly buffers = 2;
    readonly #text: TextBuilder;
    readonly #large: boolean;

    constructor(text: TextBuilder, large: boolean) {
        super(text);
        this.#text = text;
        this.#large = large;
    }

    read(data: ArrayData, start: number, length: number, rowBase: number): void {
        const blanks = this.blanksOf(data, start, length);
        const width = this.#large ? 8 : 4;
        const offsets = viewOf(elementAt(data.buffers, 1));
        if ((start + length + 1) * width > offsets.byteLength) throw this.malformed('its offsets are fewer than its rows');
        const bytes = elementAt(data.buffers, 2);
        for (let i = 0; i < length; i++) {
            if (blanks?.[i] === 1) {
                this.#text.appendBlank();
                continue;
            }
            const at = (start + i) * width;
            const from = this.#large ? offsets.getBigInt64(at, true) : BigInt(offsets.getInt32(at, true));
            const to = this.#large ? offsets.getBigInt64(at + width, true) : BigInt(offsets.getInt32(at + width, true));
            if (from < 0n || to < from || to > BigInt(bytes.byteLength)) {
                throw this.refuse(rowBase, i, 'the Arrow stream is malformed: the value\'s offsets lie outside its data.');
            }
            this.#text.append(utf8(bytes.subarray(Number(from), Number(to)), () => this.refuse(rowBase, i, 'the text is not valid UTF-8.')));
        }
    }
}

/**
 * Arrow's views of text, `utf8_view`: sixteen bytes a value, the first four its length in bytes. Text of
 * twelve bytes or fewer follows in the view itself. Longer text lies in one of the array's data buffers,
 * which follow its validity bitmap and its views; the view goes on with the text's first four bytes, which
 * data buffer holds it, and where in that buffer it starts.
 */
const viewSize = 16;
const inlineText = 12;

/** The UTF-8 bytes view `index` holds or points to, or `undefined` when the view's length is negative or
 * it points outside the array's data. */
function viewText(data: ArrayData, views: Uint8Array, index: number): Uint8Array | undefined {
    const view = viewOf(views.subarray(index * viewSize, (index + 1) * viewSize));
    const length = view.getInt32(0, true);
    if (length < 0) return undefined;
    if (length <= inlineText) return views.subarray(index * viewSize + 4, index * viewSize + 4 + length);
    const buffer = view.getInt32(8, true);
    const offset = view.getInt32(12, true);
    const bytes = buffer < 0 ? undefined : data.buffers[2 + buffer];
    if (bytes === undefined || offset < 0 || length > bytes.byteLength - offset) return undefined;
    return bytes.subarray(offset, offset + length);
}

/** `utf8_view`, as Polars writes text by default: each value's UTF-8 bytes, in its view or in the data
 * buffer the view points into, go to the column. */
class Utf8ViewReader extends ColumnReader {
    readonly buffers = 1;
    readonly #text: TextBuilder;

    constructor(text: TextBuilder) {
        super(text);
        this.#text = text;
    }

    override get variadic(): boolean {
        return true;
    }

    read(data: ArrayData, start: number, length: number, rowBase: number): void {
        const blanks = this.blanksOf(data, start, length);
        const views = elementAt(data.buffers, 1);
        if ((start + length) * viewSize > views.byteLength) throw this.malformed('its views are fewer than its rows');
        for (let i = 0; i < length; i++) {
            if (blanks?.[i] === 1) {
                this.#text.appendBlank();
                continue;
            }
            const bytes = viewText(data, views, start + i);
            if (bytes === undefined) throw this.refuse(rowBase, i, 'the Arrow stream is malformed: the value\'s view points outside its data.');
            this.#text.append(utf8(bytes, () => this.refuse(rowBase, i, 'the text is not valid UTF-8.')));
        }
    }
}

/**
 * A dictionary of `utf8`, `large_utf8` or `utf8_view`, with indices of any integer type, taken under the
 * Snapshot's rules whatever order, case or nulls the producer's dictionary has (ADR-0064/0065): the
 * Snapshot's dictionary is in the order values first appear in the rows, one entry per exact text, and a
 * null index or a null entry is a Blank. Each entry is decoded once for each dictionary the stream sends,
 * and only when a row uses it, so a dictionary replaced or extended between record batches costs what the
 * rows use of it.
 */
class DictionaryReader extends ColumnReader {
    readonly buffers = 1;
    readonly #text: TextBuilder;
    readonly #id: bigint;
    readonly #indexType: IntType;
    readonly #valueType: 'utf8' | 'large_utf8' | 'utf8_view';
    #generation = -1;
    #entries: DictionaryData | undefined;
    /** Each entry's code in the column once decoded, -1 for a null entry, under the current generation. */
    #codes = new Map<number, number>();

    constructor(text: TextBuilder, id: bigint, indexType: IntType, valueType: 'utf8' | 'large_utf8' | 'utf8_view') {
        super(text);
        this.#text = text;
        this.#id = id;
        this.#indexType = indexType;
        this.#valueType = valueType;
    }

    read(data: ArrayData, start: number, length: number, rowBase: number, dictionaries: Dictionaries): void {
        const dictionary = dictionaries.get(this.#id);
        if (dictionary === undefined) throw this.malformed('a dictionary-encoded column has no dictionary');
        if (dictionary.generation !== this.#generation) this.#adopt(dictionary);
        const blanks = this.blanksOf(data, start, length);
        const width = this.#indexType.bitWidth / 8;
        const indices = this.valuesOf(data, start, length, width);
        const count = dictionary.length;
        const codes = new Int32Array(length);
        for (let i = 0; i < length; i++) {
            if (blanks?.[i] === 1) {
                codes[i] = -1;
                continue;
            }
            const index = this.#index(indices, (start + i) * width);
            // A negative index lies below every entry, as an unsigned one past them does.
            if (index < 0n || index >= BigInt(count)) {
                throw this.refuse(rowBase, i, `the dictionary index ${index} lies outside the dictionary's ${grouped(count)} entries.`);
            }
            codes[i] = this.#code(Number(index), rowBase, i);
        }
        for (const code of codes) this.#text.appendCode(code);
    }

    #index(indices: DataView, at: number): bigint {
        const { bitWidth, signed } = this.#indexType;
        switch (bitWidth) {
            case 8: return BigInt(signed ? indices.getInt8(at) : indices.getUint8(at));
            case 16: return BigInt(signed ? indices.getInt16(at, true) : indices.getUint16(at, true));
            case 32: return BigInt(signed ? indices.getInt32(at, true) : indices.getUint32(at, true));
            case 64: return signed ? indices.getBigInt64(at, true) : indices.getBigUint64(at, true);
            default: return unreachable(bitWidth);
        }
    }

    #adopt(dictionary: DictionaryData): void {
        for (const part of dictionary.parts) {
            if (this.#valueType === 'utf8_view') {
                if (part.length * viewSize > elementAt(part.buffers, 1).byteLength) throw this.malformed('its dictionary\'s views are fewer than its entries');
            } else {
                const width = this.#valueType === 'large_utf8' ? 8 : 4;
                if (part.length > 0 && (part.length + 1) * width > elementAt(part.buffers, 1).byteLength) {
                    throw this.malformed('its dictionary\'s offsets are fewer than its entries');
                }
            }
        }
        this.#entries = dictionary;
        this.#generation = dictionary.generation;
        this.#codes = new Map();
    }

    /** Entry `k`'s code in the column, -1 for a null entry; decoded once per dictionary. */
    #code(k: number, rowBase: number, i: number): number {
        const known = this.#codes.get(k);
        if (known !== undefined) return known;
        const entries = this.#entries;
        if (entries === undefined) throw new Error('Internal: a dictionary is read before it is adopted.');
        let at = k;
        let part = elementAt(entries.parts, 0);
        for (const each of entries.parts) {
            part = each;
            if (at < each.length) break;
            at -= each.length;
        }
        const validity = elementAt(part.buffers, 0);
        if (part.nullCount > 0n && validity.byteLength === 0) throw this.malformed('its dictionary\'s nulls have no validity bitmap');
        if (part.nullCount !== 0n && validity.byteLength > 0 && validity.byteLength * 8 <= at) {
            throw this.malformed('its dictionary\'s validity bitmap is shorter than its entries');
        }
        const isNull = part.nullCount !== 0n && validity.byteLength > 0 && (viewOf(validity).getUint8(at >> 3) & (1 << (at & 7))) === 0;
        let code = -1;
        if (!isNull) {
            const bytes = this.#bytes(part, at);
            if (bytes === undefined) {
                const where = this.#valueType === 'utf8_view' ? 'view points' : 'offsets lie';
                throw this.refuse(rowBase, i, `the Arrow stream is malformed: the dictionary's entry ${k}'s ${where} outside its data.`);
            }
            code = this.#text.codeOf(utf8(bytes, () => this.refuse(rowBase, i, `the dictionary's entry ${k} is not valid UTF-8.`)));
        }
        this.#codes.set(k, code);
        return code;
    }

    /** The UTF-8 bytes of entry `at` of `part`, by its offsets or its view; `undefined` when they lie
     * outside its data. */
    #bytes(part: ArrayData, at: number): Uint8Array | undefined {
        if (this.#valueType === 'utf8_view') return viewText(part, elementAt(part.buffers, 1), at);
        const offsets = viewOf(elementAt(part.buffers, 1));
        const large = this.#valueType === 'large_utf8';
        const from = large ? offsets.getBigInt64(at * 8, true) : BigInt(offsets.getInt32(at * 4, true));
        const to = large ? offsets.getBigInt64(at * 8 + 8, true) : BigInt(offsets.getInt32(at * 4 + 4, true));
        const data = elementAt(part.buffers, 2);
        if (from < 0n || to < from || to > BigInt(data.byteLength)) return undefined;
        return data.subarray(Number(from), Number(to));
    }
}

/**
 * `decimal32`, `decimal64`, `decimal128` and `decimal256` — a value `width` bytes wide — held exactly
 * (ADR-0064): each value is made a Decimal exactly, or refused by row and column when no Decimal holds it.
 */
class DecimalReader extends ColumnReader {
    readonly buffers = 1;
    readonly #number: DecimalBuilder;
    readonly #width: number;
    readonly #scale: number;
    readonly #typeName: string;

    constructor(number: DecimalBuilder, width: number, scale: number, name: string) {
        super(number);
        this.#number = number;
        this.#width = width;
        this.#scale = scale;
        this.#typeName = name;
    }

    read(data: ArrayData, start: number, length: number, rowBase: number): void {
        const blanks = this.blanksOf(data, start, length);
        const values = this.valuesOf(data, start, length, this.#width);
        for (let i = 0; i < length; i++) {
            if (blanks?.[i] === 1) {
                this.#number.appendBlank();
                continue;
            }
            const unscaled = this.#unscaled(values, (start + i) * this.#width);
            const exact = toDecimal(unscaled < 0n ? -unscaled : unscaled, this.#scale);
            if (typeof exact === 'string') {
                const shown = unscaledText(unscaled.toString(), this.#scale);
                throw this.refuse(rowBase, i, exact === 'places'
                    ? `the ${this.#typeName} value ${shown} has more decimal places than the 28 a decimal holds.`
                    : `the ${this.#typeName} value ${shown} lies beyond the range of a decimal.`);
            }
            this.#number.append(unscaled < 0n ? -exact.magnitude : exact.magnitude, exact.places);
        }
    }

    /** The value at `at`, its little-endian two's complement words read whole. */
    #unscaled(values: DataView, at: number): bigint {
        if (this.#width === 4) return BigInt(values.getInt32(at, true));
        let whole = values.getBigInt64(at + this.#width - 8, true);
        for (let word = this.#width - 16; word >= 0; word -= 8) whole = (whole << 64n) | values.getBigUint64(at + word, true);
        return whole;
    }
}

/** `magnitude` × 10^-`places` as a Decimal's digits and places, exactly; or why no Decimal holds it. */
function toDecimal(magnitude: bigint, places: number): { magnitude: bigint; places: number } | 'places' | 'range' {
    let digits = magnitude;
    let scale = places;
    if (scale < 0) {
        digits *= 10n ** BigInt(-scale);
        scale = 0;
    }
    while (scale > maxDecimalScale || digits >= decimalLimit) {
        if (scale === 0 || digits % 10n !== 0n) return scale > maxDecimalScale && digits < decimalLimit ? 'places' : 'range';
        digits /= 10n;
        scale -= 1;
    }
    return { magnitude: digits, places: scale };
}

/** `float64`, its bits kept, and `float32`, widened exactly; non-finite values are kept. */
class DoubleReader extends ColumnReader {
    readonly buffers = 1;
    readonly #number: DoubleBuilder;
    readonly #single: boolean;
    readonly #widened = new DataView(new ArrayBuffer(8));
    readonly #narrow = new DataView(new ArrayBuffer(4));

    constructor(number: DoubleBuilder, single: boolean) {
        super(number);
        this.#number = number;
        this.#single = single;
    }

    read(data: ArrayData, start: number, length: number): void {
        const blanks = this.blanksOf(data, start, length);
        const values = this.valuesOf(data, start, length, this.#single ? 4 : 8);
        const bytes = elementAt(data.buffers, 1);
        const widened = new Uint8Array(this.#widened.buffer);
        for (let i = 0; i < length; i++) {
            if (blanks?.[i] === 1) {
                this.#number.appendBlank();
            } else if (!this.#single) {
                this.#number.appendBytes(bytes, (start + i) * 8);
            } else {
                this.#widen(values.getUint32((start + i) * 4, true));
                this.#number.appendBytes(widened, 0);
            }
        }
    }

    /** A float32's bits widened to a float64's, as a processor widens one: exactly, and a NaN's payload
     * carried into the wider mantissa, quieted, as C#'s widening does. */
    #widen(bits: number): void {
        const sign = bits >>> 31;
        const exponent = (bits >>> 23) & 0xff;
        const mantissa = bits & 0x7fffff;
        if (exponent === 0xff && mantissa !== 0) {
            const quiet = mantissa | 0x400000;
            this.#widened.setUint32(4, (sign << 31) | (0x7ff << 20) | (quiet >>> 3), true);
            this.#widened.setUint32(0, (quiet & 0x7) << 29, true);
            return;
        }
        this.#narrow.setUint32(0, bits, true);
        this.#widened.setFloat64(0, this.#narrow.getFloat32(0, true), true);
    }
}

/** `int8` to `int64` and `uint8` to `uint64`, as 64-bit integers; a `uint64` above the largest `int64`
 * is refused by row and column. */
class IntegerReader extends ColumnReader {
    readonly buffers = 1;
    readonly #number: WholeBuilder<'integer'>;
    readonly #type: IntType;

    constructor(number: WholeBuilder<'integer'>, type: IntType) {
        super(number);
        this.#number = number;
        this.#type = type;
    }

    read(data: ArrayData, start: number, length: number, rowBase: number): void {
        const blanks = this.blanksOf(data, start, length);
        const { bitWidth, signed } = this.#type;
        const width = bitWidth / 8;
        const values = this.valuesOf(data, start, length, width);
        for (let i = 0; i < length; i++) {
            const at = (start + i) * width;
            const blank = blanks?.[i] === 1;
            if (bitWidth === 64 && !signed) {
                const value = values.getBigUint64(at, true);
                if (value > int64Max && !blank) {
                    throw this.refuse(rowBase, i, `the uint64 value ${value} lies beyond the range of a 64-bit integer.`);
                }
            }
            if (blank) {
                this.#number.appendBlank();
                continue;
            }
            switch (bitWidth) {
                case 8: this.#number.append(BigInt(signed ? values.getInt8(at) : values.getUint8(at))); break;
                case 16: this.#number.append(BigInt(signed ? values.getInt16(at, true) : values.getUint16(at, true))); break;
                case 32: this.#number.append(BigInt(signed ? values.getInt32(at, true) : values.getUint32(at, true))); break;
                case 64: this.#number.append(values.getBigInt64(at, true)); break;
                default: unreachable(bitWidth);
            }
        }
    }
}

/** The ticks in one unit. A nanosecond is a hundredth of a tick, so 1 stands for it, and a reader divides
 * nanoseconds by 100 instead of multiplying. */
function ticksPer(unit: 'day' | TimeUnit): bigint {
    switch (unit) {
        case 'day': return ticksPerDay;
        case 's': return 10_000_000n;
        case 'ms': return 10_000n;
        case 'us': return 10n;
        case 'ns': return 1n;
        default: return unreachable(unit);
    }
}

/** The unit's name, counted. */
function counted(unit: 'day' | TimeUnit): string {
    switch (unit) {
        case 'day': return 'days';
        case 's': return 'seconds';
        case 'ms': return 'milliseconds';
        case 'us': return 'microseconds';
        case 'ns': return 'nanoseconds';
        default: return unreachable(unit);
    }
}

function finer(type: string, nanoseconds: bigint): string {
    return `the ${type} value ${nanoseconds} is finer than the 100 nanoseconds a date holds.`;
}

/**
 * `date32`, `date64`, and a `timestamp` without a time zone or in UTC, as the clock value written
 * (ADR-0065). A value outside a date's range, or a nanosecond timestamp finer than the 100 nanoseconds a
 * date holds, is refused by row and column rather than moved.
 */
class DateReader extends ColumnReader {
    readonly buffers = 1;
    readonly #date: WholeBuilder<'date'>;
    readonly #unit: 'day' | TimeUnit;
    readonly #width: number;
    readonly #typeName: string;
    readonly #ticksPerUnit: bigint;
    readonly #min: bigint;
    readonly #max: bigint;

    constructor(date: WholeBuilder<'date'>, unit: 'day' | TimeUnit, width: number, name: string) {
        super(date);
        this.#date = date;
        this.#unit = unit;
        this.#width = width;
        this.#typeName = name;
        this.#ticksPerUnit = ticksPer(unit);
        this.#min = -(epochTicks / this.#ticksPerUnit);
        this.#max = (maxTicks - epochTicks) / this.#ticksPerUnit;
    }

    read(data: ArrayData, start: number, length: number, rowBase: number): void {
        const blanks = this.blanksOf(data, start, length);
        const values = this.valuesOf(data, start, length, this.#width);
        for (let i = 0; i < length; i++) {
            if (blanks?.[i] === 1) {
                this.#date.appendBlank();
                continue;
            }
            const at = (start + i) * this.#width;
            const value = this.#width === 4 ? BigInt(values.getInt32(at, true)) : values.getBigInt64(at, true);
            if (this.#unit === 'ns') {
                if (value % 100n !== 0n) throw this.refuse(rowBase, i, finer(this.#typeName, value));
                this.#date.append(value / 100n + epochTicks);
                continue;
            }
            if (value < this.#min || value > this.#max) {
                throw this.refuse(rowBase, i,
                    `the ${this.#typeName} value ${value} (${counted(this.#unit)} since 1970-01-01) lies outside the range of a date, 0001-01-01 to 9999-12-31.`);
            }
            this.#date.append(value * this.#ticksPerUnit + epochTicks);
        }
    }
}

/**
 * `time32` and `time64`, a time of day, as the clock time on the first day a date holds, 0001-01-01 — as a
 * database's TimeOnly is read (ADR-0064/0065). A value outside a day, or a nanosecond value finer than the
 * 100 nanoseconds a date holds, is refused by row and column rather than moved.
 */
class TimeReader extends ColumnReader {
    readonly buffers = 1;
    readonly #date: WholeBuilder<'date'>;
    readonly #wide: boolean;
    readonly #unit: TimeUnit;
    readonly #typeName: string;
    readonly #ticksPerUnit: bigint;
    /** The units in a day: a time of day is at least 0 and fewer than these. */
    readonly #perDay: bigint;

    constructor(date: WholeBuilder<'date'>, wide: boolean, unit: TimeUnit, name: string) {
        super(date);
        this.#date = date;
        this.#wide = wide;
        this.#unit = unit;
        this.#typeName = name;
        this.#ticksPerUnit = ticksPer(unit);
        this.#perDay = unit === 'ns' ? ticksPerDay * 100n : ticksPerDay / this.#ticksPerUnit;
    }

    read(data: ArrayData, start: number, length: number, rowBase: number): void {
        const blanks = this.blanksOf(data, start, length);
        const width = this.#wide ? 8 : 4;
        const values = this.valuesOf(data, start, length, width);
        for (let i = 0; i < length; i++) {
            if (blanks?.[i] === 1) {
                this.#date.appendBlank();
                continue;
            }
            const at = (start + i) * width;
            const value = this.#wide ? values.getBigInt64(at, true) : BigInt(values.getInt32(at, true));
            if (value < 0n || value >= this.#perDay) {
                throw this.refuse(rowBase, i,
                    `the ${this.#typeName} value ${value} (${counted(this.#unit)} since midnight) lies outside a day, 00:00:00 to 23:59:59.9999999.`);
            }
            if (this.#unit !== 'ns') {
                this.#date.append(value * this.#ticksPerUnit);
                continue;
            }
            if (value % 100n !== 0n) throw this.refuse(rowBase, i, finer(this.#typeName, value));
            this.#date.append(value / 100n);
        }
    }
}

/** `bool`: Arrow's bits, unpacked. */
class BooleanReader extends ColumnReader {
    readonly buffers = 1;
    readonly #flag: BooleanBuilder;

    constructor(flag: BooleanBuilder) {
        super(flag);
        this.#flag = flag;
    }

    read(data: ArrayData, start: number, length: number): void {
        const blanks = this.blanksOf(data, start, length);
        const bits = elementAt(data.buffers, 1);
        if (bits.byteLength * 8 < start + length) throw this.malformed('its value bitmap is shorter than its rows');
        const view = viewOf(bits);
        for (let i = 0; i < length; i++) {
            if (blanks?.[i] === 1) {
                this.#flag.appendBlank();
                continue;
            }
            const bit = start + i;
            this.#flag.append((view.getUint8(bit >> 3) & (1 << (bit & 7))) !== 0);
        }
    }
}
