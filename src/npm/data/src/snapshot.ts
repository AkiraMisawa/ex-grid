// The Snapshot (ADR-0064) as TypeScript holds it: the family's immutable data at one version, column by
// column, each of one kind, a Blank kept apart from every value. It holds what ExGrid.Data's Snapshot
// holds and travels between them as Arrow (ADR-0065, ADR-0370 section 3); it is data, not a query engine.
// A Change Batch makes the next version (change-batch.ts), copying the rows it keeps in runs.

import { canonicalDecimal, isSnapshotDecimal, type SnapshotDecimal } from './decimal';
import { maxTicks, ticksOfTemporal } from './dates';
import { SnapshotError } from './snapshot-error';
import { elementAt, int64Max, int64Min, unreachable } from './must';

/** A column's kind (ADR-0064). */
export type SnapshotKind = 'text' | 'decimal' | 'double' | 'integer' | 'date' | 'boolean';

/** What every column of a Snapshot answers, whatever its kind. */
export interface SnapshotColumnBase<K extends SnapshotKind, V> {
    /** The column's name, unique within its Snapshot. */
    readonly name: string;
    /** The data's own label for the column; its name unless one was given. */
    readonly caption: string;
    /** The column's kind. */
    readonly kind: K;
    /** Whether the column is a Blank at `row`, counted from 0. A row outside the Snapshot throws a `RangeError`. */
    isBlank(row: number): boolean;
    /** The value at `row`, counted from 0, or `null` for a Blank. A row outside the Snapshot throws a `RangeError`. */
    valueAt(row: number): V | null;
}

/** A Text column: every distinct value once in its dictionary, in the order it first appears, and each
 * row a code into it. Two spellings are two values, and the empty string is a value, not a Blank. */
export interface TextColumn extends SnapshotColumnBase<'text', string> {
    /** The column's distinct values, in the order they first appear in its rows. */
    readonly dictionary: readonly string[];
    /** The row's code into the dictionary, or -1 for a Blank. */
    codeAt(row: number): number;
}

/** A Decimal column: C#'s decimal, held exactly. A value is handed out canonical, so 1.5 and 1.50 are one. */
export type DecimalColumn = SnapshotColumnBase<'decimal', SnapshotDecimal>;

/** A Double column: IEEE 754 doubles as they came, non-finite values and negative zero included. */
export interface DoubleColumn extends SnapshotColumnBase<'double', number> {
    /** The row's IEEE 754 bits as they are held, a NaN's payload included, which a number need not carry;
     * 0 for a Blank. */
    bitsAt(row: number): bigint;
}

/** An Integer column: 64-bit integers. */
export type IntegerColumn = SnapshotColumnBase<'integer', bigint>;

/** A Date column: clock values, without a zone, as C#'s DateTime ticks: 100 nanoseconds since
 * 0001-01-01T00:00, up to 9999-12-31T23:59:59.9999999. `ticksText` writes one as text. */
export type DateColumn = SnapshotColumnBase<'date', bigint>;

/** A Boolean column. */
export type BooleanColumn = SnapshotColumnBase<'boolean', boolean>;

/** A column of a Snapshot, told apart by its `kind`. */
export type SnapshotColumn = TextColumn | DecimalColumn | DoubleColumn | IntegerColumn | DateColumn | BooleanColumn;

/** The family's immutable data at one version, held in columns (ADR-0064). */
export interface Snapshot {
    /** The version, which ExGrid.Data's Change Batches move on. */
    readonly version: bigint;
    /** The Record Key's column name, or `undefined` for a Snapshot without one. */
    readonly recordKey: string | undefined;
    /** How many rows it holds. */
    readonly rowCount: number;
    /** Its columns, in order. */
    readonly columns: readonly SnapshotColumn[];
    /** The column of that name; a name it does not hold throws a `RangeError`. */
    column(name: string): SnapshotColumn;
}

/** A Temporal `PlainDate` or `PlainDateTime`, the browser's or a polyfill's, recognised by its tag. */
export interface TemporalPlainDateLike {
    /** The tag a Temporal value carries. */
    readonly [Symbol.toStringTag]: 'Temporal.PlainDate' | 'Temporal.PlainDateTime';
}

/** What every column a Snapshot is made from gives, whatever its kind. */
export interface SnapshotColumnInputBase<K extends SnapshotKind, V> {
    /** The column's name, unique within the Snapshot and not empty. */
    readonly name: string;
    /** The data's own label for the column; its name when left out. */
    readonly caption?: string;
    /** The column's kind. */
    readonly kind: K;
    /** One value for each row, `null` for a Blank. */
    readonly values: readonly (V | null)[];
}

/** A column a Snapshot is made from: Text from strings, Decimal from `SnapshotDecimal`s, Double from
 * numbers, Integer from bigints or safe integers, Date from ticks or Temporal plain dates and date-times,
 * Boolean from booleans. */
export type SnapshotColumnInput =
    | SnapshotColumnInputBase<'text', string>
    | SnapshotColumnInputBase<'decimal', SnapshotDecimal>
    | SnapshotColumnInputBase<'double', number>
    | SnapshotColumnInputBase<'integer', bigint | number>
    | SnapshotColumnInputBase<'date', bigint | TemporalPlainDateLike>
    | SnapshotColumnInputBase<'boolean', boolean>;

/** What a Snapshot is made from. */
export interface SnapshotInput {
    /** Its columns, in order, each holding the same number of rows. */
    readonly columns: readonly SnapshotColumnInput[];
    /** The name of the Text or Integer column whose value tells each record from every other; none when
     * left out. */
    readonly recordKey?: string;
    /** The version, a whole number from 0; 0 when left out. */
    readonly version?: bigint | number;
}

// ---- columns as they are held ------------------------------------------------------------------

/** Throws unless `row` is one of `rowCount` rows. */
function checkRow(row: number, rowCount: number): void {
    if (!Number.isInteger(row) || row < 0 || row >= rowCount) {
        throw new RangeError(`Row ${row} lies outside the Snapshot's ${rowCount} rows.`);
    }
}

/** A column's Blanks, one byte a row, 1 for a Blank; `undefined` when it holds none. */
type Blanks = Uint8Array | undefined;

abstract class HeldColumn {
    readonly name: string;
    readonly caption: string;
    protected readonly rowCount: number;
    readonly #blanks: Blanks;

    constructor(name: string, caption: string, rowCount: number, blanks: Blanks) {
        this.name = name;
        this.caption = caption;
        this.rowCount = rowCount;
        this.#blanks = blanks;
    }

    isBlank(row: number): boolean {
        checkRow(row, this.rowCount);
        return this.#blanks !== undefined && this.#blanks[row] === 1;
    }

    /** The Blanks as they are held, for a next version to copy. */
    get heldBlanks(): Blanks {
        return this.#blanks;
    }
}

/** Rows [`from`, `to`) of a column, which a next version takes as they are. */
export interface ColumnRun {
    readonly column: SnapshotColumn;
    readonly from: number;
    readonly to: number;
}

function runLength(runs: readonly ColumnRun[]): number {
    return runs.reduce((total, run) => total + run.to - run.from, 0);
}

function heldOf<T>(column: SnapshotColumn, held: abstract new (...args: never[]) => T): T {
    if (!(column instanceof held)) throw new TypeError(`The column '${column.name}' was not made by @ex-grid/data, so a next version cannot take its rows.`);
    return column;
}

function assembledBlanks(runs: readonly ColumnRun[], total: number): Blanks {
    const held = runs.map(run => heldOf(run.column, HeldColumn).heldBlanks);
    if (held.every(blanks => blanks === undefined)) return undefined;
    const blanks = new Uint8Array(total);
    let at = 0;
    runs.forEach((run, i) => {
        const source = held[i];
        if (source !== undefined) blanks.set(source.subarray(run.from, run.to), at);
        at += run.to - run.from;
    });
    return blanks;
}

/** What {@link assembledArray} needs of a typed array. */
interface Assemblable<T> {
    subarray(from: number, to: number): T;
    set(source: T, offset: number): void;
}

/** A typed array holding each run's part of `part(run)`, in order. */
function assembledArray<T extends Assemblable<T>>(
    runs: readonly ColumnRun[], total: number, make: (length: number) => T, part: (run: ColumnRun) => T,
): T {
    const assembled = make(total);
    let at = 0;
    for (const run of runs) {
        assembled.set(part(run).subarray(run.from, run.to), at);
        at += run.to - run.from;
    }
    return assembled;
}

class HeldText extends HeldColumn implements TextColumn {
    readonly kind = 'text';
    readonly dictionary: readonly string[];
    readonly #codes: Int32Array;

    constructor(name: string, caption: string, rowCount: number, dictionary: readonly string[], codes: Int32Array) {
        super(name, caption, rowCount, undefined);
        // A frozen dictionary is one a version before this one holds, or one made for this one: it is shared, not copied.
        this.dictionary = Object.isFrozen(dictionary) ? dictionary : Object.freeze(dictionary.slice());
        this.#codes = codes;
    }

    override isBlank(row: number): boolean {
        return this.codeAt(row) < 0;
    }

    codeAt(row: number): number {
        checkRow(row, this.rowCount);
        return elementAt(this.#codes, row);
    }

    valueAt(row: number): string | null {
        const code = this.codeAt(row);
        return code < 0 ? null : elementAt(this.dictionary, code);
    }

    /** The runs' rows as one column over `dictionary`, a run's codes taken through its column's entry in
     * `remaps` where it has one, and as they are where it has none. */
    static assembled(name: string, caption: string, runs: readonly ColumnRun[], dictionary: readonly string[],
        remaps: ReadonlyMap<SnapshotColumn, Int32Array>): HeldText {
        const total = runLength(runs);
        const codes = new Int32Array(total);
        let at = 0;
        for (const run of runs) {
            const source = heldOf(run.column, HeldText).#codes;
            const remap = remaps.get(run.column);
            if (remap === undefined) {
                codes.set(source.subarray(run.from, run.to), at);
            } else {
                for (let row = run.from; row < run.to; row++) {
                    const code = elementAt(source, row);
                    codes[at + row - run.from] = code < 0 ? -1 : elementAt(remap, code);
                }
            }
            at += run.to - run.from;
        }
        return new HeldText(name, caption, total, dictionary, codes);
    }
}

class HeldDecimal extends HeldColumn implements DecimalColumn {
    readonly kind = 'decimal';
    readonly #mantissas: readonly bigint[];
    readonly #scales: Uint8Array;

    constructor(name: string, caption: string, rowCount: number, blanks: Blanks, mantissas: readonly bigint[], scales: Uint8Array) {
        super(name, caption, rowCount, blanks);
        this.#mantissas = mantissas;
        this.#scales = scales;
    }

    valueAt(row: number): SnapshotDecimal | null {
        return this.isBlank(row) ? null : Object.freeze({ mantissa: elementAt(this.#mantissas, row), scale: elementAt(this.#scales, row) });
    }

    /** The runs' rows as one column. */
    static assembled(name: string, caption: string, runs: readonly ColumnRun[]): HeldDecimal {
        const total = runLength(runs);
        const mantissas: bigint[] = [];
        for (const run of runs) {
            const source = heldOf(run.column, HeldDecimal).#mantissas;
            for (let row = run.from; row < run.to; row++) mantissas.push(elementAt(source, row));
        }
        const scales = assembledArray(runs, total, length => new Uint8Array(length), run => heldOf(run.column, HeldDecimal).#scales);
        return new HeldDecimal(name, caption, total, assembledBlanks(runs, total), mantissas, scales);
    }
}

class HeldDouble extends HeldColumn implements DoubleColumn {
    readonly kind = 'double';
    readonly #values: Float64Array;
    readonly #bits: BigInt64Array;

    constructor(name: string, caption: string, rowCount: number, blanks: Blanks, values: Float64Array) {
        super(name, caption, rowCount, blanks);
        this.#values = values;
        this.#bits = new BigInt64Array(values.buffer, values.byteOffset, values.length);
    }

    valueAt(row: number): number | null {
        return this.isBlank(row) ? null : elementAt(this.#values, row);
    }

    bitsAt(row: number): bigint {
        return this.isBlank(row) ? 0n : BigInt.asUintN(64, elementAt(this.#bits, row));
    }

    /** The runs' rows as one column, each double's bits as they are held. */
    static assembled(name: string, caption: string, runs: readonly ColumnRun[]): HeldDouble {
        const total = runLength(runs);
        const bits = assembledArray(runs, total, length => new BigInt64Array(length), run => heldOf(run.column, HeldDouble).#bits);
        return new HeldDouble(name, caption, total, assembledBlanks(runs, total), new Float64Array(bits.buffer, bits.byteOffset, total));
    }
}

class HeldWhole<K extends 'integer' | 'date'> extends HeldColumn implements SnapshotColumnBase<K, bigint> {
    readonly kind: K;
    readonly #values: BigInt64Array;

    constructor(kind: K, name: string, caption: string, rowCount: number, blanks: Blanks, values: BigInt64Array) {
        super(name, caption, rowCount, blanks);
        this.kind = kind;
        this.#values = values;
    }

    valueAt(row: number): bigint | null {
        return this.isBlank(row) ? null : elementAt(this.#values, row);
    }

    /** The runs' rows as one column of `kind`. */
    static assembled<K extends 'integer' | 'date'>(kind: K, name: string, caption: string, runs: readonly ColumnRun[]): HeldWhole<K> {
        const total = runLength(runs);
        const values = assembledArray(runs, total, length => new BigInt64Array(length), run => heldOf(run.column, HeldWhole).#values);
        return new HeldWhole(kind, name, caption, total, assembledBlanks(runs, total), values);
    }
}

class HeldBoolean extends HeldColumn implements BooleanColumn {
    readonly kind = 'boolean';
    readonly #values: Uint8Array;

    constructor(name: string, caption: string, rowCount: number, blanks: Blanks, values: Uint8Array) {
        super(name, caption, rowCount, blanks);
        this.#values = values;
    }

    valueAt(row: number): boolean | null {
        return this.isBlank(row) ? null : elementAt(this.#values, row) === 1;
    }

    /** The runs' rows as one column. */
    static assembled(name: string, caption: string, runs: readonly ColumnRun[]): HeldBoolean {
        const total = runLength(runs);
        const values = assembledArray(runs, total, length => new Uint8Array(length), run => heldOf(run.column, HeldBoolean).#values);
        return new HeldBoolean(name, caption, total, assembledBlanks(runs, total), values);
    }
}

/**
 * The runs' rows as one column like `like`, of its name, caption and kind: the rows a next version keeps,
 * copied as they are held. A Text column's codes are taken through `remaps` into `dictionary`.
 */
export function assembledColumn(like: SnapshotColumn, runs: readonly ColumnRun[], dictionary: readonly string[],
    remaps: ReadonlyMap<SnapshotColumn, Int32Array>): SnapshotColumn {
    switch (like.kind) {
        case 'text': return HeldText.assembled(like.name, like.caption, runs, dictionary, remaps);
        case 'decimal': return HeldDecimal.assembled(like.name, like.caption, runs);
        case 'double': return HeldDouble.assembled(like.name, like.caption, runs);
        case 'integer': return HeldWhole.assembled('integer', like.name, like.caption, runs);
        case 'date': return HeldWhole.assembled('date', like.name, like.caption, runs);
        case 'boolean': return HeldBoolean.assembled(like.name, like.caption, runs);
        default: return unreachable(like);
    }
}

/**
 * Where a version's rows stand in the order of its lineage, which a Change Batch keeps (ADR-0064): each row's
 * position, ascending, `undefined` where the rows stand at 0, 1, 2 and so on; and the position the next
 * added record takes.
 */
export interface Lineage {
    readonly positions: Float64Array | undefined;
    readonly nextPosition: number;
}

class HeldSnapshot implements Snapshot {
    readonly version: bigint;
    readonly recordKey: string | undefined;
    readonly rowCount: number;
    readonly columns: readonly SnapshotColumn[];
    readonly #byName: ReadonlyMap<string, SnapshotColumn>;
    readonly #lineage: Lineage;

    constructor(columns: readonly SnapshotColumn[], rowCount: number, recordKey: string | undefined, version: bigint, lineage?: Lineage) {
        this.columns = Object.freeze(columns.slice());
        this.rowCount = rowCount;
        this.recordKey = recordKey;
        this.version = version;
        this.#byName = new Map(columns.map(column => [column.name, column]));
        this.#lineage = lineage ?? { positions: undefined, nextPosition: rowCount };
        Object.freeze(this);
    }

    /** Where its rows stand in its lineage's order. */
    get lineage(): Lineage {
        return this.#lineage;
    }

    column(name: string): SnapshotColumn {
        const column = this.#byName.get(name);
        if (column === undefined) throw new RangeError(`The Snapshot holds no column named '${name}'.`);
        return column;
    }
}

/** Whether `value` is a Snapshot this package made, by a read or from a Consumer's values: one whose every
 * value was checked against its kind, which a structural look-alike's need not be. */
export function isHeldSnapshot(value: unknown): value is Snapshot {
    return value instanceof HeldSnapshot;
}

/** The lineage of a Snapshot this package made; a look-alike is refused, naming what it is for. */
export function lineageOf(snapshot: Snapshot, what: string): Lineage {
    if (!(snapshot instanceof HeldSnapshot)) throw new TypeError(`${what} is a Snapshot @ex-grid/data made, by a read or createSnapshot.`);
    return snapshot.lineage;
}

/** A next version, of columns already made. */
export function nextSnapshot(columns: readonly SnapshotColumn[], rowCount: number, recordKey: string | undefined, version: bigint,
    lineage: Lineage): Snapshot {
    return new HeldSnapshot(columns, rowCount, recordKey, version, lineage);
}

// ---- building ------------------------------------------------------------------------------------

/** Room that grows as rows are appended, and is cut to the rows held when the column is sealed. */
function grown<T extends Uint8Array | Int32Array | Float64Array | BigInt64Array>(array: T, needed: number, make: (length: number) => T): T {
    if (needed <= array.length) return array;
    const room = make(Math.max(needed, array.length * 2, 64));
    new Uint8Array(room.buffer, room.byteOffset, array.byteLength).set(new Uint8Array(array.buffer, array.byteOffset, array.byteLength));
    return room;
}

/** A column being built, a row at a time or a run at a time: by a read, or from a Consumer's values. */
export abstract class ColumnBuilder {
    readonly name: string;
    readonly caption: string;
    abstract readonly kind: SnapshotKind;
    protected count = 0;
    #blanks: Uint8Array | undefined;

    constructor(name: string, caption: string) {
        this.name = name;
        this.caption = caption;
    }

    /** The rows appended so far. */
    get rowCount(): number {
        return this.count;
    }

    /** Marks row `row`, which has just been given room, a Blank. */
    protected markBlank(row: number): void {
        this.#blanks = grown(this.#blanks ?? new Uint8Array(0), row + 1, length => new Uint8Array(length));
        this.#blanks[row] = 1;
    }

    /** Whether row `row`, already appended, is a Blank. */
    blankAt(row: number): boolean {
        return this.#blanks !== undefined && row < this.#blanks.length && this.#blanks[row] === 1;
    }

    protected sealedBlanks(): Blanks {
        return this.#blanks?.slice(0, this.count);
    }

    abstract appendBlank(): void;

    abstract seal(): SnapshotColumn;
}

export class TextBuilder extends ColumnBuilder {
    readonly kind = 'text';
    readonly #dictionary: string[] = [];
    readonly #codeOf = new Map<string, number>();
    #codes = new Int32Array(0);

    /** The code `text` has in this column's dictionary, given it one when it has none yet. */
    codeOf(text: string): number {
        let code = this.#codeOf.get(text);
        if (code === undefined) {
            code = this.#dictionary.length;
            this.#dictionary.push(text);
            this.#codeOf.set(text, code);
        }
        return code;
    }

    /** Appends the row whose code is `code`, -1 for a Blank. */
    appendCode(code: number): void {
        this.#codes = grown(this.#codes, this.count + 1, length => new Int32Array(length));
        this.#codes[this.count++] = code;
    }

    append(text: string | null): void {
        this.appendCode(text === null ? -1 : this.codeOf(text));
    }

    appendBlank(): void {
        this.appendCode(-1);
    }

    /** The row's code, -1 for a Blank. */
    codeAt(row: number): number {
        return elementAt(this.#codes, row);
    }

    /** The text of a code. */
    textOf(code: number): string {
        return elementAt(this.#dictionary, code);
    }

    seal(): TextColumn {
        return new HeldText(this.name, this.caption, this.count, this.#dictionary, this.#codes.slice(0, this.count));
    }
}

export class DecimalBuilder extends ColumnBuilder {
    readonly kind = 'decimal';
    readonly #mantissas: bigint[] = [];
    #scales = new Uint8Array(0);

    /** Appends ± a Decimal's digits at its places, which the caller has checked a Decimal holds. */
    append(mantissa: bigint, scale: number): void {
        const value = canonicalDecimal(mantissa, scale);
        this.#mantissas.push(value.mantissa);
        this.#scales = grown(this.#scales, this.count + 1, length => new Uint8Array(length));
        this.#scales[this.count++] = value.scale;
    }

    appendBlank(): void {
        this.#mantissas.push(0n);
        this.#scales = grown(this.#scales, this.count + 1, length => new Uint8Array(length));
        this.markBlank(this.count++);
    }

    seal(): DecimalColumn {
        return new HeldDecimal(this.name, this.caption, this.count, this.sealedBlanks(), this.#mantissas.slice(), this.#scales.slice(0, this.count));
    }
}

export class DoubleBuilder extends ColumnBuilder {
    readonly kind = 'double';
    #values = new Float64Array(0);

    /** Appends a double from its eight little-endian bytes, as they are, a NaN's payload included. */
    appendBytes(bytes: Uint8Array, offset: number): void {
        this.#values = grown(this.#values, this.count + 1, length => new Float64Array(length));
        new Uint8Array(this.#values.buffer, this.count * 8, 8).set(bytes.subarray(offset, offset + 8));
        this.count++;
    }

    append(value: number): void {
        this.#values = grown(this.#values, this.count + 1, length => new Float64Array(length));
        this.#values[this.count++] = value;
    }

    appendBlank(): void {
        this.#values = grown(this.#values, this.count + 1, length => new Float64Array(length));
        this.markBlank(this.count++);
    }

    seal(): DoubleColumn {
        return new HeldDouble(this.name, this.caption, this.count, this.sealedBlanks(), this.#values.slice(0, this.count));
    }
}

export class WholeBuilder<K extends 'integer' | 'date'> extends ColumnBuilder {
    readonly kind: K;
    #values = new BigInt64Array(0);

    constructor(kind: K, name: string, caption: string) {
        super(name, caption);
        this.kind = kind;
    }

    append(value: bigint): void {
        this.#values = grown(this.#values, this.count + 1, length => new BigInt64Array(length));
        this.#values[this.count++] = value;
    }

    appendBlank(): void {
        this.#values = grown(this.#values, this.count + 1, length => new BigInt64Array(length));
        this.markBlank(this.count++);
    }

    /** The row's value, 0 for a Blank. */
    valueAt(row: number): bigint {
        return elementAt(this.#values, row);
    }

    seal(): IntegerColumn | DateColumn {
        const values = this.#values.slice(0, this.count);
        return this.kind === 'integer'
            ? new HeldWhole('integer', this.name, this.caption, this.count, this.sealedBlanks(), values)
            : new HeldWhole('date', this.name, this.caption, this.count, this.sealedBlanks(), values);
    }
}

export class BooleanBuilder extends ColumnBuilder {
    readonly kind = 'boolean';
    #values = new Uint8Array(0);

    append(value: boolean): void {
        this.#values = grown(this.#values, this.count + 1, length => new Uint8Array(length));
        this.#values[this.count++] = value ? 1 : 0;
    }

    appendBlank(): void {
        this.#values = grown(this.#values, this.count + 1, length => new Uint8Array(length));
        this.markBlank(this.count++);
    }

    seal(): BooleanColumn {
        return new HeldBoolean(this.name, this.caption, this.count, this.sealedBlanks(), this.#values.slice(0, this.count));
    }
}

/** A builder for each kind. */
export type AnyColumnBuilder = TextBuilder | DecimalBuilder | DoubleBuilder | WholeBuilder<'integer'> | WholeBuilder<'date'> | BooleanBuilder;

/** A new builder of the kind. */
export function builderOf(kind: SnapshotKind, name: string, caption: string): AnyColumnBuilder {
    switch (kind) {
        case 'text': return new TextBuilder(name, caption);
        case 'decimal': return new DecimalBuilder(name, caption);
        case 'double': return new DoubleBuilder(name, caption);
        case 'integer': return new WholeBuilder('integer', name, caption);
        case 'date': return new WholeBuilder('date', name, caption);
        case 'boolean': return new BooleanBuilder(name, caption);
        default: return unreachable(kind);
    }
}

/**
 * Seals the builders into a Snapshot, refusing a Record Key Blank or carried twice (ADR-0064) by the first
 * row that is either, as ExGrid.Data refuses it. The caller has checked the columns hold the same rows,
 * their names are unique, and the key names a Text or an Integer column.
 */
export function sealSnapshot(builders: readonly AnyColumnBuilder[], rowCount: number, recordKey: string | undefined, version: bigint): Snapshot {
    if (recordKey !== undefined) {
        const key = builders.find(builder => builder.name === recordKey);
        if (key === undefined) throw new Error(`Internal: the Record Key '${recordKey}' names no column.`);
        checkKeys(key, rowCount);
    }
    return new HeldSnapshot(builders.map(builder => builder.seal()), rowCount, recordKey, version);
}

function checkKeys(key: AnyColumnBuilder, rowCount: number): void {
    const seen = new Map<string | bigint, number>();
    for (let row = 0; row < rowCount; row++) {
        let value: string | bigint;
        if (key instanceof TextBuilder) {
            const code = key.codeAt(row);
            if (code < 0) throw new SnapshotError('the Record Key is Blank.', { row: row + 1, column: key.name });
            value = key.textOf(code);
        } else if (key instanceof WholeBuilder && key.kind === 'integer') {
            if (key.blankAt(row)) throw new SnapshotError('the Record Key is Blank.', { row: row + 1, column: key.name });
            value = key.valueAt(row);
        } else {
            throw new Error(`Internal: the Record Key '${key.name}' is a ${key.kind} column.`);
        }
        const first = seen.get(value);
        if (first !== undefined) {
            const shown = typeof value === 'string' ? `'${value}'` : value.toString();
            throw new SnapshotError(`the Record Key ${shown} is already carried by row ${first + 1}.`, { row: row + 1, column: key.name, key: value });
        }
        seen.set(value, row);
    }
}

/**
 * A Snapshot of the Consumer's values, column by column (ADR-0064): each value checked against its column's
 * kind, Text's dictionary made in the order values first appear, and the Record Key checked. A value of the
 * wrong kind, columns of different lengths, a name empty or twice, or a Record Key that names no Text or
 * Integer column is refused with a `TypeError`; a value outside its kind's range with a `RangeError`; a
 * Record Key Blank or carried twice with a {@link SnapshotError} naming the row and the key.
 */
export function createSnapshot(input: SnapshotInput): Snapshot {
    const version = input.version === undefined ? 0n : wholeVersion(input.version);
    const first = input.columns[0];
    const rowCount = first === undefined ? 0 : first.values.length;
    const names = new Set<string>();
    const builders = input.columns.map(column => {
        if (typeof column.name !== 'string' || column.name === '') throw new TypeError('A column of a Snapshot is named, and its name is not empty.');
        if (names.has(column.name)) throw new TypeError(`Two columns are named '${column.name}', and a column's name is unique within a Snapshot.`);
        names.add(column.name);
        if (column.values.length !== rowCount) {
            throw new TypeError(`The column '${column.name}' holds ${column.values.length} rows, and the first column ${rowCount}; every column holds the Snapshot's rows.`);
        }
        return built(column);
    });
    if (input.recordKey !== undefined) {
        const key = builders.find(builder => builder.name === input.recordKey);
        if (key === undefined) throw new TypeError(`No column is named '${input.recordKey}', so it cannot be the Record Key.`);
        if (key.kind !== 'text' && key.kind !== 'integer') {
            throw new TypeError(`Only a Text or an Integer column can be the Record Key; '${key.name}' is ${key.kind}.`);
        }
    }
    return sealSnapshot(builders, rowCount, input.recordKey, version);
}

function wholeVersion(version: bigint | number): bigint {
    const whole = typeof version === 'number' && Number.isSafeInteger(version) ? BigInt(version) : version;
    if (typeof whole !== 'bigint') throw new TypeError(`The version ${String(version)} is not a whole number.`);
    if (whole < 0n || whole > int64Max) throw new RangeError(`The version ${whole} lies outside a version's, 0 to ${int64Max}.`);
    return whole;
}

function built(column: SnapshotColumnInput): AnyColumnBuilder {
    const caption = column.caption ?? column.name;
    const wrong = (value: unknown, row: number): TypeError =>
        new TypeError(`Row ${row + 1} of the ${column.kind} column '${column.name}' holds ${describe(value)}, which is not a ${column.kind} value.`);
    switch (column.kind) {
        case 'text': {
            const builder = new TextBuilder(column.name, caption);
            column.values.forEach((value: unknown, row) => {
                if (value !== null && typeof value !== 'string') throw wrong(value, row);
                builder.append(value);
            });
            return builder;
        }
        case 'decimal': {
            const builder = new DecimalBuilder(column.name, caption);
            column.values.forEach((value: unknown, row) => {
                if (value === null) builder.appendBlank();
                else if (isSnapshotDecimal(value)) builder.append(value.mantissa, value.scale);
                else throw wrong(value, row);
            });
            return builder;
        }
        case 'double': {
            const builder = new DoubleBuilder(column.name, caption);
            column.values.forEach((value: unknown, row) => {
                if (value === null) builder.appendBlank();
                else if (typeof value === 'number') builder.append(value);
                else throw wrong(value, row);
            });
            return builder;
        }
        case 'integer': {
            const builder = new WholeBuilder('integer', column.name, caption);
            column.values.forEach((value: unknown, row) => {
                if (value === null) {
                    builder.appendBlank();
                    return;
                }
                const whole = typeof value === 'number' && Number.isSafeInteger(value) ? BigInt(value) : value;
                if (typeof whole !== 'bigint') throw wrong(value, row);
                if (whole < int64Min || whole > int64Max) {
                    throw new RangeError(`Row ${row + 1} of the integer column '${column.name}' holds ${whole}, beyond a 64-bit integer's range.`);
                }
                builder.append(whole);
            });
            return builder;
        }
        case 'date': {
            const builder = new WholeBuilder('date', column.name, caption);
            column.values.forEach((value: unknown, row) => {
                if (value === null) {
                    builder.appendBlank();
                    return;
                }
                const ticks = typeof value === 'object' ? ticksOfTemporal(value) : value;
                if (typeof ticks !== 'bigint') throw wrong(value, row);
                if (ticks < 0n || ticks > maxTicks) {
                    throw new RangeError(`Row ${row + 1} of the date column '${column.name}' holds ${ticks} ticks, outside a date's range, 0001-01-01 to 9999-12-31.`);
                }
                builder.append(ticks);
            });
            return builder;
        }
        case 'boolean': {
            const builder = new BooleanBuilder(column.name, caption);
            column.values.forEach((value: unknown, row) => {
                if (value === null) builder.appendBlank();
                else if (typeof value === 'boolean') builder.append(value);
                else throw wrong(value, row);
            });
            return builder;
        }
        default:
            return unreachable(column);
    }
}

function describe(value: unknown): string {
    switch (typeof value) {
        case 'string': return `the text '${value}'`;
        case 'number':
        case 'bigint':
        case 'boolean':
            return `the ${typeof value} ${String(value)}`;
        case 'undefined': return 'undefined';
        case 'symbol': return 'a symbol';
        case 'function': return 'a function';
        case 'object': return value === null ? 'null' : Object.prototype.toString.call(value);
        default: throw new Error(`Internal: typeof says ${typeof value}, which is none of its answers.`);
    }
}
