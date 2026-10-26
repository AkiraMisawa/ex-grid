// A Board Input (ADR-0341), as ExGrid.Data's BoardInput reads, checks and binds one: the name a Board gives a
// piece of its data and each column's declared kind, checked against a source's shape before any row is drawn.
// The refusals are worded as C# words them, so the shared cases of tests/cases/data/ hold both to one text.

import { canonicalDecimal, decimalLimit, isSnapshotDecimal, maxDecimalScale, type SnapshotDecimal } from './decimal';
import { daysIn, ticksOf } from './dates';
import {
    JsonNumber, JsonTextError, isJsonArray, isJsonObject, jsonMember, jsonMemberNames, jsonRawText, readJsonText, type JsonValue,
} from './json';
import { createSnapshot, type Snapshot, type SnapshotColumnInput, type SnapshotKind } from './snapshot';
import type { DataShape, DataShapeColumn } from './arrow/reader';

/**
 * A Board, or the data bound to one of its inputs, that cannot be drawn (ADR-0340, ADR-0341): its message names
 * the reason in one sentence and `path` says where, which a host returns to the AI or the person that wrote it.
 */
export class BoardRefusedError extends Error {
    /** Where it was refused, such as `inputs.trades.columns[2].type` in a Board or `trades[41].notional` in an
     * input's data; empty for the whole. */
    readonly path: string;

    /** A refusal at `path`, said by `message`. */
    constructor(path: string, message: string, options?: ErrorOptions) {
        super(message, options);
        this.name = 'BoardRefusedError';
        this.path = path;
    }
}

const kindNames: readonly SnapshotKind[] = Object.freeze(['text', 'decimal', 'double', 'integer', 'date', 'boolean']);

/** The JSON name of each kind, as a declaration writes it, in the order C# lists them. */
export const boardKindNames: readonly SnapshotKind[] = kindNames;

const declarationKeys = ['columns'];
const columnKeys = ['name', 'type'];

/** A source that answers its shape before any row (ADR-0341), as `IShapedSource` does in C#: `undefined` where it
 * was given none to say, which a Board refuses to bind rather than guess from rows. */
export interface ShapedSource {
    /** The source's shape, or `undefined`. */
    shape(signal?: AbortSignal): Promise<DataShape | undefined>;
}

/** The column of `shape` named `name` (ordinal), or `undefined` where it has none. */
export function shapeColumn(shape: DataShape, name: string): DataShapeColumn | undefined {
    return shape.columns.find(column => column.name === name);
}

/**
 * A Board Input (ADR-0341): the name a Board gives a piece of its data, and each column's name and kind, written by
 * whoever composed the Board while they knew what the data means. A workflow binds it to a source at run time; the
 * source's shape is checked against it before any row is drawn. Kinds are never inferred from rows.
 */
export class BoardInput {
    readonly #name: string;
    readonly #shape: DataShape;
    readonly #byName: ReadonlyMap<string, DataShapeColumn>;

    /** An input named `name` declaring `columns`, in order. An empty name, no columns, a column with no name or named
     * twice, and a kind a Snapshot does not hold are refused with a `TypeError`. */
    constructor(name: string, columns: readonly DataShapeColumn[]) {
        if (name === '') throw new TypeError('An input has a name.');
        if (columns.length === 0) throw new TypeError(`Input '${name}' declares no column.`);
        const byName = new Map<string, DataShapeColumn>();
        for (const column of columns) {
            if (column.name === '') throw new TypeError(`A column of input '${name}' has no name.`);
            if (!kindNames.includes(column.kind)) throw new TypeError(`Column '${column.name}' has no kind a Snapshot holds.`);
            if (byName.has(column.name)) throw new TypeError(`Column '${column.name}' is named twice in the shape.`);
            byName.set(column.name, Object.freeze({ name: column.name, kind: column.kind }));
        }
        this.#name = name;
        this.#byName = byName;
        this.#shape = Object.freeze({ columns: Object.freeze([...byName.values()]) });
    }

    /** The input's name, as the Board's views name it in their `data`. */
    get name(): string {
        return this.#name;
    }

    /** The columns declared, in order. */
    get columns(): readonly DataShapeColumn[] {
        return this.#shape.columns;
    }

    /** The declaration as a shape. */
    get shape(): DataShape {
        return this.#shape;
    }

    /** The column declared under `name` (ordinal), or `undefined`. */
    column(name: string): DataShapeColumn | undefined {
        return this.#byName.get(name);
    }

    /**
     * Reads a declaration as a Board writes it, `{"columns": [{"name": "desk", "type": "text"}, …]}`. A member the
     * declaration does not have, a missing or empty name, a type that is none of {@link boardKindNames}, a column
     * named twice and an empty list are refused, naming `path`, where the declaration stands (`inputs.trades`).
     */
    static read(name: string, declaration: JsonValue, path: string): BoardInput {
        if (name === '') throw new BoardRefusedError(path, 'An input has an empty name.');
        if (!isJsonObject(declaration)) throw new BoardRefusedError(path, `Input '${name}' is not an object.`);
        for (const member of jsonMemberNames(declaration)) {
            if (!declarationKeys.includes(member)) throw new BoardRefusedError(`${path}.${member}`, `Input '${name}' has no member '${member}'.`);
        }
        const list = jsonMember(declaration, 'columns');
        if (!isJsonArray(list)) throw new BoardRefusedError(`${path}.columns`, `Input '${name}' declares its columns as a list.`);
        const columns: DataShapeColumn[] = [];
        const named = new Set<string>();
        list.forEach((column, i) => {
            const at = `${path}.columns[${i}]`;
            if (!isJsonObject(column)) throw new BoardRefusedError(at, `A column of input '${name}' is not an object.`);
            for (const member of jsonMemberNames(column)) {
                if (!columnKeys.includes(member)) throw new BoardRefusedError(`${at}.${member}`, `A column of input '${name}' has no member '${member}'.`);
            }
            const columnName = jsonMember(column, 'name');
            if (typeof columnName !== 'string' || columnName === '') throw new BoardRefusedError(`${at}.name`, `A column of input '${name}' has no name.`);
            if (named.has(columnName)) throw new BoardRefusedError(`${at}.name`, `Input '${name}' declares column '${columnName}' twice.`);
            named.add(columnName);
            const type = jsonMember(column, 'type');
            const kind = kindNames.find(k => k === type);
            if (kind === undefined) {
                throw new BoardRefusedError(`${at}.type`,
                    `Column '${columnName}' of input '${name}' declares no type; it is one of ${kindNames.join(', ')}, never guessed from the data.`);
            }
            columns.push({ name: columnName, kind });
        });
        if (columns.length === 0) throw new BoardRefusedError(`${path}.columns`, `Input '${name}' declares no column.`);
        return new BoardInput(name, columns);
    }

    /**
     * Checks a source's shape against the declaration: every declared column is there, of the kind declared. A
     * column the source has and the input does not declare is no mismatch; the views cannot name it. A mismatch is
     * refused naming the input and the column.
     */
    check(shape: DataShape): void {
        for (const declared of this.columns) {
            const held = shapeColumn(shape, declared.name);
            if (held === undefined) {
                throw new BoardRefusedError(this.#name, `Input '${this.#name}' declares column '${declared.name}', which its source does not have.`);
            }
            if (held.kind !== declared.kind) {
                throw new BoardRefusedError(this.#name,
                    `Input '${this.#name}', column '${declared.name}': declared ${declared.kind}, and its source holds ${held.kind}.`);
            }
        }
    }

    /**
     * Asks `source` its shape, before any row, and checks it against the declaration. A source that says no shape
     * is refused: its kinds are never inferred from rows, which a first page of Blanks would get wrong.
     */
    async checkSource(source: ShapedSource, signal?: AbortSignal): Promise<DataShape> {
        const shape = await source.shape(signal);
        if (shape === undefined) {
            throw new BoardRefusedError(this.#name,
                `Input '${this.#name}' is bound to a source that does not say its shape, and its kinds are not guessed from its rows.`);
        }
        this.check(shape);
        return shape;
    }

    /** Binds `snapshot` to this input: its shape checked, and the Snapshot itself returned, unchanged. */
    bind(snapshot: Snapshot): Snapshot {
        this.check({ columns: snapshot.columns.map(column => ({ name: column.name, kind: column.kind })) });
        return snapshot;
    }

    /**
     * Data that arrived as JSON, a list of objects, made a Snapshot by the declared kinds: one column per declared
     * column, in order. A member missing or `null` is a Blank; a member the input does not declare is not read. A
     * value that does not convert is refused by input, row and column, never dropped or read as zero: a `decimal` is
     * a JSON number or its digits as a string, read exactly; a `double` a number, or `"NaN"`, `"Infinity"` or
     * `"-Infinity"`; an `integer` a whole number, or its digits as a string; a `date` a string `yyyy-MM-dd`, or with
     * a time `yyyy-MM-ddTHH:mm[:ss[.fffffff]]`, never with an offset; `text` a string, and a `boolean` `true` or
     * `false`. The refusal's path is `input[row].column`, the row counted from 0.
     */
    fromJson(json: string | JsonValue): Snapshot {
        let rows: JsonValue;
        if (typeof json === 'string') {
            try {
                rows = readJsonText(json);
            } catch (error) {
                if (!(error instanceof JsonTextError)) throw error;
                throw new BoardRefusedError(this.#name, `The data of input '${this.#name}' is not JSON: ${error.message}`, { cause: error });
            }
        } else {
            rows = json;
        }
        if (!isJsonArray(rows)) throw new BoardRefusedError(this.#name, `The data of input '${this.#name}' is not a list of rows.`);
        const values = this.columns.map((): unknown[] => []);
        rows.forEach((element, row) => {
            if (!isJsonObject(element)) {
                throw new BoardRefusedError(`${this.#name}[${row}]`, `Input '${this.#name}', row ${grouped(row + 1)}: is not an object.`);
            }
            this.columns.forEach((column, c) => {
                const value = jsonMember(element, column.name);
                const into = values[c];
                if (into === undefined) throw new Error('Internal: a column without its values.');
                if (value === undefined || value === null) {
                    into.push(null);
                    return;
                }
                const read = convert(column.kind, value);
                if (read === undefined) {
                    throw new BoardRefusedError(`${this.#name}[${row}].${column.name}`,
                        `Input '${this.#name}', row ${grouped(row + 1)}, column '${column.name}': ${shown(value)} is not ${article(column.kind)}.`);
                }
                into.push(read);
            });
        });
        return createSnapshot({ columns: this.columns.map((column, c) => columnInput(column, values[c])) });
    }
}

// Each kind's values as createSnapshot takes them, which convert made of that kind.
function columnInput(column: DataShapeColumn, values: readonly unknown[] | undefined): SnapshotColumnInput {
    if (values === undefined) throw new Error('Internal: a column without its values.');
    const { name } = column;
    const of = <T>(is: (value: unknown) => value is T): (T | null)[] => values.map(value => {
        if (value === null || is(value)) return value;
        throw new Error(`Internal: a value of column '${name}' is not of its kind.`);
    });
    switch (column.kind) {
        case 'text': return { name, kind: 'text', values: of((v): v is string => typeof v === 'string') };
        case 'boolean': return { name, kind: 'boolean', values: of((v): v is boolean => typeof v === 'boolean') };
        case 'double': return { name, kind: 'double', values: of((v): v is number => typeof v === 'number') };
        case 'integer': return { name, kind: 'integer', values: of((v): v is bigint => typeof v === 'bigint') };
        case 'date': return { name, kind: 'date', values: of((v): v is bigint => typeof v === 'bigint') };
        case 'decimal': return { name, kind: 'decimal', values: of(isSnapshotDecimal) };
        default: throw new Error(`Internal: ${String(column.kind)} is not a kind.`);
    }
}

function grouped(count: number): string {
    return count.toString().replace(/\B(?=(\d{3})+(?!\d))/gu, ',');
}

function shown(value: JsonValue): string {
    const raw = jsonRawText(value);
    return raw.length <= 40 ? raw : `${raw.slice(0, 37)}...`;
}

function article(kind: SnapshotKind): string {
    switch (kind) {
        case 'integer': return 'an integer';
        case 'text': return 'text';
        case 'decimal': case 'double': case 'date': case 'boolean': return `a ${kind}`;
        default: throw new Error(`Internal: ${String(kind)} is not a kind.`);
    }
}

const int64Min = -(2n ** 63n);
const int64Max = 2n ** 63n - 1n;

/** A value of `kind` read from `value`, or `undefined` where it does not convert. */
function convert(kind: SnapshotKind, value: JsonValue): string | SnapshotDecimal | number | bigint | boolean | undefined {
    switch (kind) {
        case 'text': return typeof value === 'string' ? value : undefined;
        case 'boolean': return typeof value === 'boolean' ? value : undefined;
        case 'decimal':
            if (value instanceof JsonNumber) return decimalOfNumber(value.raw);
            return typeof value === 'string' ? decimalOfDigits(value) : undefined;
        case 'double':
            if (value instanceof JsonNumber) return Number.isFinite(value.double) ? value.double : undefined;
            if (value === 'NaN') return Number.NaN;
            if (value === 'Infinity') return Number.POSITIVE_INFINITY;
            if (value === '-Infinity') return Number.NEGATIVE_INFINITY;
            return undefined;
        case 'integer': {
            // TryGetInt64 reads a number's spelling with no fraction or exponent; long.TryParse a sign and digits.
            const digits = value instanceof JsonNumber ? (/^-?\d+$/u.test(value.raw) ? value.raw : undefined)
                : typeof value === 'string' && /^[+-]?\d+$/u.test(value) ? value : undefined;
            if (digits === undefined) return undefined;
            const whole = BigInt(digits);
            return whole >= int64Min && whole <= int64Max ? whole : undefined;
        }
        case 'date': return typeof value === 'string' ? ticksOfText(value) : undefined;
        default: throw new Error(`Internal: ${String(kind)} is not a kind.`);
    }
}

/** The decimal a JSON number's spelling writes exactly, exponent read, as `JsonElement.TryGetDecimal` reads it;
 * `undefined` where no decimal holds it exactly. */
function decimalOfNumber(raw: string): SnapshotDecimal | undefined {
    const match = /^(-?)(\d+)(?:\.(\d+))?(?:[eE]([+-]?\d+))?$/u.exec(raw);
    if (match === null) return undefined;
    const [, sign = '', whole = '', fraction = '', exponent = '0'] = match;
    let digits = (whole + fraction).replace(/^0+(?=\d)/u, '');
    let places = fraction.length - Number(exponent);
    while (places > 0 && digits.endsWith('0')) {
        digits = digits.slice(0, -1);
        places--;
    }
    if (places < 0) {
        digits += '0'.repeat(-places);
        places = 0;
    }
    if (places > maxDecimalScale) return undefined;
    const magnitude = BigInt(digits);
    if (magnitude >= decimalLimit) return undefined;
    return canonicalDecimal(sign === '-' ? -magnitude : magnitude, places);
}

/** The decimal digits as text write, a sign and a point allowed, as `decimal.TryParse` under
 * `AllowLeadingSign | AllowDecimalPoint` reads them; `undefined` where no decimal holds them exactly. */
function decimalOfDigits(text: string): SnapshotDecimal | undefined {
    const match = /^([+-]?)(\d*)(?:\.(\d*))?$/u.exec(text);
    if (match === null) return undefined;
    const [, sign = '', whole = '', fraction = ''] = match;
    if (whole === '' && fraction === '') return undefined;
    const places = fraction.replace(/0+$/u, '');
    if (places.length > maxDecimalScale) return undefined;
    const magnitude = BigInt(`${whole === '' ? '0' : whole}${places}`);
    if (magnitude >= decimalLimit) return undefined;
    return canonicalDecimal(sign === '-' ? -magnitude : magnitude, places.length);
}

/** The ticks of a date's text in one of the forms a declaration takes, or `undefined`. */
function ticksOfText(text: string): bigint | undefined {
    const match = /^(\d{4})-(\d{2})-(\d{2})(?:T(\d{2}):(\d{2})(?::(\d{2})(?:\.(\d{1,7}))?)?)?$/u.exec(text);
    if (match === null) return undefined;
    const [, year = '', month = '', day = '', hour = '0', minute = '0', second = '0', fraction = ''] = match;
    const y = Number(year), mo = Number(month), d = Number(day), h = Number(hour), mi = Number(minute), s = Number(second);
    if (y < 1 || mo < 1 || mo > 12 || d < 1 || d > daysIn(y, mo) || h > 23 || mi > 59 || s > 59) return undefined;
    return ticksOf(y, mo, d, h, mi, s, Number(fraction.padEnd(7, '0')));
}
