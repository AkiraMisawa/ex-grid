// The View State's JSON (ADR-0207): a whole GridViewState, or a GridQuery alone, written as the C# core's
// GridViewStateJson writes it, byte for byte, and read back refusing what it refuses, in its words and at the
// path it names. Free of the DOM: a Board's grid view (ADR-0342) reads it as a grid does. The form, and what
// each side refuses, is the C# class's documentation; this file transcribes it.

import { JsonNumber, JsonTextError, isJsonArray, isJsonObject, jsonMember, jsonMemberNames, jsonRawText, readJsonText } from '@ex-grid/data';
import type { JsonObject, JsonValue } from '@ex-grid/data';
import type { FilterClause, FilterCombinator, FilterOperator, FilterSpec, GridFilter } from '../query/filter';
import type { SortDirection, SortSpec } from '../query/sort';
import type { GridQuery } from '../source/fetching-source';
import { roundTripText } from './float-text';
import { jsonArray, jsonObject, jsonString } from './json-text';
import {
    decimalOperandText, floatOperandText, integerRanges, kindNames, maxTicks, offsetText, operandTags, readDateOnlyText,
    readDateTimeOffsetText, readDateTimeText, readDecimalText, readFloatText, readIntegerText, ticksOperandText,
} from './operand-text';
import {
    combinatorOf, GridViewState, isTagged,
    type ColumnViewState, type ColumnViewStateWidth, type ViewStateDateTimeKind, type ViewStateIntegerType, type ViewStateOperand,
} from './view-state';

/** The version this writer writes and the only one this reader reads (ADR-0207). */
const version = 1;

/**
 * A View State's or a Query's JSON refused by the reader: text that is not JSON, a document not in the form of
 * version 1, a column the grid does not have, a value that is not its tag's type (ADR-0207). Its message is
 * the C# core's `JsonException` message, word for word, except where the text is not JSON at all, where the
 * words after "cannot be read:" are the platform's own.
 */
export class GridViewStateJsonError extends Error {
    /** Where in the document the refusal stands, as the C# core's `JsonException.Path` says it (`$`,
     * `$.columns[0].width`, `$.filter.columns['Notional'].clauses[0].value`); `undefined` where it has none:
     * a refusal of what a part means rather than of its shape, and text that is not JSON. */
    readonly path: string | undefined;

    /** A refusal saying `message`, at `path` where it stands at one. */
    constructor(message: string, path?: string, options?: ErrorOptions) {
        super(message, options);
        this.name = 'GridViewStateJsonError';
        this.path = path;
    }
}

// ---- Names --------------------------------------------------------------------------------------------
//
// Written out rather than derived, so that a name is the form's, and another case or a number is refused.

const operatorNames: Readonly<Record<FilterOperator, string>> = Object.freeze({
    equals: 'Equals', notEquals: 'NotEquals', greaterThan: 'GreaterThan', greaterThanOrEqual: 'GreaterThanOrEqual',
    lessThan: 'LessThan', lessThanOrEqual: 'LessThanOrEqual', contains: 'Contains', doesNotContain: 'DoesNotContain',
    startsWith: 'StartsWith', endsWith: 'EndsWith', in: 'In', isBlank: 'IsBlank', isNotBlank: 'IsNotBlank',
});
const directionNames: Readonly<Record<SortDirection, string>> = Object.freeze({ ascending: 'Ascending', descending: 'Descending' });
const combinatorNames: Readonly<Record<FilterCombinator, string>> = Object.freeze({ and: 'And', or: 'Or' });

/** The member of `names` whose name is `name`, or `undefined`. */
function named<T extends string>(names: Readonly<Record<T, string>>, name: string): T | undefined {
    return (Object.keys(names) as T[]).find(key => names[key] === name); // oxlint-disable-line typescript/no-unsafe-type-assertion -- the keys of a Record<T, string> are T's members
}

/** The name `names` gives `value`, or `undefined` where `value` is none of its members. */
function nameOf<T extends string>(names: Readonly<Record<T, string>>, value: unknown): string | undefined {
    return typeof value === 'string' && Object.hasOwn(names, value) ? names[value as T] : undefined; // oxlint-disable-line typescript/no-unsafe-type-assertion -- an own key of the Record is one of T's members
}

/** A value no name names, as the C# core shows an enum's: its number, or what an untyped caller passed. */
function shown(value: unknown): string {
    return typeof value === 'string' || typeof value === 'number' ? String(value) : typeof value;
}

function capital(text: string): string {
    return text.length === 0 ? text : text.charAt(0).toUpperCase() + text.slice(1);
}

// ---- Writing ------------------------------------------------------------------------------------------

/**
 * A whole View State as JSON, the C# core's text byte for byte. Refuses with an `Error` an operand that is not
 * a {@link ViewStateOperand}, a Double or Single that is not finite, and an Opaque Filter (the C# core's
 * `NotSupportedException`); with a `TypeError` what reading would refuse: a sort level, a column's filter, its
 * clauses or a clause that is missing, a sort level or a filter that names no column, and a direction, a
 * combinator or an operator that is none of the model's; and an operand whose value is not of its type, with a
 * `TypeError`, or outside its type's range, with a `RangeError`.
 */
export function writeGridViewStateJson(viewState: GridViewState): string {
    if (!(viewState instanceof GridViewState)) throw new TypeError('A View State is written from a GridViewState.');
    const columns = viewState.columns.map(column => jsonObject([
        ['column', jsonString(column.column)],
        ['width', 'px' in column.width ? roundTripText(column.width.px, 'double') : '"Auto"'],
        ['pinned', column.pinned ? 'true' : 'false'],
    ]));
    return documentText([['columns', jsonArray(columns)], ['sorts', writeSorts(viewState.sorts)]], writeFilter(viewState.filter));
}

/** A Query alone as JSON, the C# core's text byte for byte: for a Consumer's own server, or to keep the sort
 * and the filter without the columns. Refuses what {@link writeGridViewStateJson} refuses, and a range that
 * holds no rows, with a `TypeError`. */
export function writeGridQueryJson(query: GridQuery): string {
    const { range } = query;
    if (!(range.count >= 1)) {
        throw new TypeError(`A Query's range is a run of at least one row, and ${range.count} rows at ${range.start} is not: a RowRange left at its default (ADR-0001).`);
    }
    const sorts: unknown = query.sorts;
    if (!Array.isArray(sorts)) throw new TypeError("A Query's sorts are a list, empty for none.");
    const written = jsonObject([['start', String(range.start)], ['count', String(range.count)]]);
    return documentText([['range', written], ['sorts', writeSorts(query.sorts)]], writeFilter(query.filter));
}

function documentText(members: readonly (readonly [string, string])[], filter: string | undefined): string {
    return jsonObject([['version', String(version)], ...members, ...(filter === undefined ? [] : [['filter', filter] as const])]);
}

function writeSorts(sorts: readonly SortSpec[]): string {
    return jsonArray(sorts.map((sort: SortSpec | null | undefined, i) => {
        const where = `Sort level ${i + 1}`;
        if (sort === null || sort === undefined) throw new TypeError(`${where} is null.`);
        if (typeof sort.column !== 'string' || sort.column === '') throw new TypeError(`${where} names no column.`);
        const direction = nameOf(directionNames, sort.direction);
        if (direction === undefined) throw new TypeError(`${where}: ${shown(sort.direction)} is not a SortDirection this version names.`);
        return jsonObject([['column', jsonString(sort.column)], ['direction', jsonString(direction)]]);
    }));
}

function writeFilter(filter: GridFilter | null | undefined): string | undefined {
    if (filter === null || filter === undefined) return undefined;
    const columns: unknown = filter.columns;
    if (typeof columns !== 'object' || columns === null) throw new TypeError("The filter's columns are a dictionary, empty for none.");
    if (filter.opaque !== undefined && filter.opaque !== null) {
        throw new Error(
            "The filter's Opaque Filter has no JSON form here: its meaning is the Consumer's alone (ADR-0002, ADR-0023). "
            + 'Keep it beside the View State, and write the filter without it (filter with { Opaque = null }) (ADR-0207).');
    }
    const written = columnOrder(filter.columns).map((name): readonly [string, string] => {
        if (name === '') throw new TypeError('The filter names no column.');
        const on = `the filter on '${name}'`;
        const spec = filter.columns[name] as FilterSpec | null | undefined;
        if (spec === null || spec === undefined) throw new TypeError(`${capital(on)} is null.`);
        const clauses: unknown = spec.clauses;
        if (!Array.isArray(clauses)) throw new TypeError(`${capital(on)} has no list of clauses; its clauses are a list, empty for none.`);
        const combinator = combinatorOf(spec);
        const combinatorName = nameOf(combinatorNames, combinator);
        if (combinatorName === undefined) throw new TypeError(`${capital(on)}: ${shown(combinator)} is not a FilterCombinator this version names.`);
        const clauseTexts = spec.clauses.map((clause: FilterClause | null | undefined, i) => writeClause(clause, `${on}, clause ${i + 1}`));
        return [name, jsonObject([['combinator', jsonString(combinatorName)], ['clauses', jsonArray(clauseTexts)]])];
    });
    return jsonObject([['columns', jsonObject(written)]]);
}

function writeClause(clause: FilterClause | null | undefined, where: string): string {
    if (clause === null || clause === undefined) throw new TypeError(`${capital(where)} is null.`);
    const operator = nameOf(operatorNames, clause.operator);
    if (operator === undefined) throw new TypeError(`${capital(where)}: ${shown(clause.operator)} is not a FilterOperator this version names.`);
    const members: [string, string][] = [['operator', jsonString(operator)]];
    if (clause.value !== undefined && clause.value !== null) members.push(['value', writeOperand(clause.value, where)]);
    const values: unknown = clause.values;
    if (values !== undefined && values !== null) {
        if (!Array.isArray(values)) throw new TypeError(`${capital(where)}: its values are a list.`);
        members.push(['values', jsonArray(values.map((value: unknown, j) =>
            value === undefined || value === null ? 'null' : writeOperand(value, `${where}, value ${j + 1}`)))]);
    }
    return jsonObject(members);
}

function writeOperand(value: unknown, where: string): string {
    const at = capital(where);
    if (!isTagged(value)) {
        throw new Error(
            `${at}: an operand of type ${typeName(value)} has no JSON form; a filter takes text, a Boolean, `
            + 'a number, a DateTime, a DateOnly or a DateTimeOffset (ADR-0023, ADR-0207).');
    }
    const tag = operandTags[value.type];
    const operand = (text: string, kind?: string): string => jsonObject([
        ['type', jsonString(tag)], ['value', jsonString(text)], ...(kind === undefined ? [] : [['kind', jsonString(kind)] as const])]);
    const notOfType = (what: string): TypeError => new TypeError(`${at}: a ${tag} operand holds ${what}.`);
    switch (value.type) {
        case 'string':
            if (typeof value.value !== 'string') throw notOfType('a string');
            return operand(value.value);
        case 'boolean':
            if (typeof value.value !== 'boolean') throw notOfType('true or false');
            return operand(value.value ? 'true' : 'false');
        case 'decimal': {
            const { mantissa, scale }: { mantissa: unknown; scale: unknown } = value.value;
            if (typeof mantissa !== 'bigint' || typeof scale !== 'number' || !Number.isInteger(scale)) throw notOfType('a { mantissa, scale } of a bigint and a whole number');
            if (mantissa >= 1n << 96n || mantissa <= -(1n << 96n) || scale < 0 || scale > 28) {
                throw new RangeError(`${at}: a Decimal's mantissa lies below 2^96 in magnitude and its places are 0 to 28.`);
            }
            return operand(decimalOperandText({ mantissa, scale }));
        }
        case 'double':
        case 'single': {
            const number: unknown = value.value;
            if (typeof number !== 'number') throw notOfType('a number');
            if (!Number.isFinite(number)) throw new Error(`${at}: ${nonFiniteText(number)} is not a finite number, which no filter takes (ADR-0023).`);
            if (value.type === 'single' && Math.fround(number) !== number) throw new RangeError(`${at}: ${number} is not a Single; a Single holds a 32-bit float.`);
            return operand(floatOperandText(number, value.type));
        }
        case 'int64': case 'int32': case 'int16': case 'sByte':
        case 'uInt64': case 'uInt32': case 'uInt16': case 'byte':
            return operand(integerText(value.value, value.type, at));
        case 'dateTime': {
            const kind = nameOf(kindNames, value.kind);
            if (kind === undefined) throw new TypeError(`${at}: a DateTime's kind is unspecified, utc or local.`);
            return operand(ticksOperandText(ticksIn(value.ticks, at), true), kind);
        }
        case 'dateOnly': {
            const ticks = ticksIn(value.ticks, at);
            if (ticks % 864_000_000_000n !== 0n) throw new TypeError(`${at}: a DateOnly's ticks are a midnight's.`);
            return operand(ticksOperandText(ticks, false));
        }
        case 'dateTimeOffset': {
            const ticks = ticksIn(value.ticks, at);
            const offset: unknown = value.offsetMinutes;
            if (typeof offset !== 'number' || !Number.isInteger(offset) || Math.abs(offset) > 14 * 60) {
                throw new RangeError(`${at}: a DateTimeOffset's offset is a whole number of minutes within ±14:00.`);
            }
            const utc = ticks - BigInt(offset) * 600_000_000n;
            if (utc < 0n || utc > maxTicks) throw new RangeError(`${at}: the instant a DateTimeOffset names lies outside a date's range.`);
            return operand(ticksOperandText(ticks, true) + offsetText(offset));
        }
        default:
            throw new Error(`Internal: ${JSON.stringify(tag)} is a tag the writer does not take.`);
    }
}

function integerText(value: unknown, type: ViewStateIntegerType, at: string): string {
    if (typeof value !== 'bigint') throw new TypeError(`${at}: a ${operandTags[type]} operand holds a bigint.`);
    const [least, most] = integerRanges[type];
    if (value < least || value > most) throw new RangeError(`${at}: ${value} lies outside a ${operandTags[type]}'s range, ${least} to ${most}.`);
    return value.toString();
}

function ticksIn(ticks: unknown, at: string): bigint {
    if (typeof ticks !== 'bigint') throw new TypeError(`${at}: a date's ticks are a bigint.`);
    if (ticks < 0n || ticks > maxTicks) throw new RangeError(`${at}: ${ticks} ticks lie outside a date's, 0001-01-01 to 9999-12-31.`);
    return ticks;
}

/** A non-finite number as .NET writes it in the invariant culture; the C# core writes it in the current
 * culture, which the Script Core does not have. */
function nonFiniteText(value: number): string {
    if (Number.isNaN(value)) return 'NaN';
    return value > 0 ? 'Infinity' : '-Infinity';
}

function typeName(value: unknown): string {
    if (typeof value !== 'object' || value === null) return typeof value;
    const prototype: unknown = Object.getPrototypeOf(value);
    if (prototype === null || typeof prototype !== 'object') return 'Object';
    const constructor: unknown = Reflect.get(prototype, 'constructor');
    return typeof constructor === 'function' && constructor.name !== '' ? constructor.name : 'Object';
}

// The order a filter's columns were read in, which an object's own keys do not keep where a name is a whole
// number, so that a View State read and written again is the text it was.
const readColumnOrder = new WeakMap<object, readonly string[]>();

function columnOrder(columns: Readonly<Record<string, FilterSpec>>): readonly string[] {
    const order = readColumnOrder.get(columns);
    return order === undefined ? Object.keys(columns) : order;
}

// ---- The form's shape ---------------------------------------------------------------------------------
//
// Held on the parsed document before anything is read from it, as the C# core holds it against its
// documents' metadata: a member this version does not have, a member it requires that is missing or null,
// and a member of the wrong JSON type, refused in the form's words, naming the member and where it stands. A
// null inside a list or a dictionary is left to the reader, which refuses it by what it stands for, or takes
// it: a Blank among In's operands.

type Shape =
    | { readonly kind: 'object'; readonly members: readonly MemberShape[] }
    | { readonly kind: 'array'; readonly item: Shape }
    | { readonly kind: 'dictionary'; readonly entry: Shape }
    | { readonly kind: 'string' | 'boolean' | 'int' | 'any' };

interface MemberShape {
    readonly name: string;
    readonly shape: Shape;
    /** Whether a document leaves it out only to be refused. */
    readonly required: boolean;
    /** Whether `null` stands for it being absent. */
    readonly nullable: boolean;
}

const textShape: Shape = { kind: 'string' };
const required = (name: string, shape: Shape): MemberShape => ({ name, shape, required: true, nullable: false });
const optional = (name: string, shape: Shape): MemberShape => ({ name, shape, required: false, nullable: true });
const objectShape = (...members: MemberShape[]): Shape => ({ kind: 'object', members });

const operandShape = objectShape(required('type', textShape), required('value', textShape), optional('kind', textShape));
const clauseShape = objectShape(required('operator', textShape), optional('value', operandShape), optional('values', { kind: 'array', item: operandShape }));
const filterShape = objectShape(required('columns', {
    kind: 'dictionary',
    entry: objectShape(required('combinator', textShape), required('clauses', { kind: 'array', item: clauseShape })),
}));
const sortsShape: Shape = { kind: 'array', item: objectShape(required('column', textShape), required('direction', textShape)) };
const viewStateShape = objectShape(
    required('version', { kind: 'int' }),
    required('columns', { kind: 'array', item: objectShape(required('column', textShape), required('width', { kind: 'any' }), required('pinned', { kind: 'boolean' })) }),
    required('sorts', sortsShape),
    optional('filter', filterShape));
const queryShape = objectShape(
    required('version', { kind: 'int' }),
    required('range', objectShape(required('start', { kind: 'int' }), required('count', { kind: 'int' }))),
    required('sorts', sortsShape),
    optional('filter', filterShape));

function checkShape(element: JsonValue, shape: Shape, path: string, label: string, what: string): void {
    switch (shape.kind) {
        case 'object':
            if (!isJsonObject(element)) throw misshapen(what, path, `${label} is an object, and here it is ${described(element)}`);
            for (const name of jsonMemberNames(element)) {
                const member = shape.members.find(m => m.name === name);
                if (member === undefined) throw misshapen(what, path, `it has no member '${name}' in version ${version}`);
                checkMember(memberValue(element, name), member, `${path}.${name}`, what);
            }
            for (const member of shape.members) {
                if (member.required && jsonMember(element, member.name) === undefined) throw misshapen(what, path, `its member '${member.name}' is missing`);
            }
            return;
        case 'array':
            if (!isJsonArray(element)) throw misshapen(what, path, `${label} is an array, and here it is ${described(element)}`);
            element.forEach((item, i) => {
                if (item !== null) checkShape(item, shape.item, `${path}[${i}]`, `item ${i + 1} of ${label}`, what);
            });
            return;
        case 'dictionary':
            if (!isJsonObject(element)) throw misshapen(what, path, `${label} is an object, and here it is ${described(element)}`);
            for (const name of jsonMemberNames(element)) {
                const entry = memberValue(element, name);
                if (entry !== null) checkShape(entry, shape.entry, `${path}['${name}']`, `'${name}' in ${label}`, what);
            }
            return;
        case 'string':
        case 'boolean':
        case 'int':
        case 'any': {
            const fits = shape.kind === 'string' ? typeof element === 'string'
                : shape.kind === 'boolean' ? typeof element === 'boolean'
                    : shape.kind === 'int' ? element instanceof JsonNumber && element.int32 !== undefined
                        : true;
            if (!fits) throw misshapen(what, path, `${label} is ${expected(shape)}, and here it is ${described(element)}`);
            return;
        }
        default:
            throw new Error('Internal: a shape of no kind.');
    }
}

function checkMember(value: JsonValue, member: MemberShape, path: string, what: string): void {
    // A width is any JSON here: the reader refuses what is neither form, naming its column.
    if (member.shape.kind === 'any') return;
    if (value === null) {
        if (member.nullable) return;
        throw misshapen(what, path, `'${member.name}' is null, and it is ${expected(member.shape)}`);
    }
    checkShape(value, member.shape, path, `'${member.name}'`, what);
}

function expected(shape: Shape): string {
    switch (shape.kind) {
        case 'object':
        case 'dictionary': return 'an object';
        case 'array': return 'an array';
        case 'string': return 'a string';
        case 'boolean': return 'true or false';
        case 'int': return 'a whole number';
        case 'any': return 'a value of this form';
        default: throw new Error('Internal: a shape of no kind.');
    }
}

function described(element: JsonValue): string {
    if (element === null) return 'null';
    if (typeof element === 'string') return 'a string';
    if (typeof element === 'boolean') return element ? 'true' : 'false';
    if (element instanceof JsonNumber) return `the number ${element.raw}`;
    return isJsonArray(element) ? 'an array' : 'an object';
}

function misshapen(what: string, path: string, words: string): GridViewStateJsonError {
    return new GridViewStateJsonError(`${what} cannot be read at ${path}: ${words} (ADR-0207).`, path);
}

function refused(message: string): GridViewStateJsonError {
    return new GridViewStateJsonError(message);
}

// ---- Reading ------------------------------------------------------------------------------------------

function parse(json: string, what: string, shape: Shape): JsonObject {
    if (typeof json !== 'string') throw new TypeError(`${what} is read from JSON text.`);
    let root: JsonValue;
    try {
        root = readJsonText(json);
    } catch (error) {
        // One error type for every refusal: text that is not JSON, and a member named twice, which is never
        // settled by whichever came last.
        if (error instanceof JsonTextError) throw new GridViewStateJsonError(`${what} cannot be read: ${error.message}`, undefined, { cause: error });
        throw error;
    }
    if (!isJsonObject(root)) throw refused(`${what} is a JSON object.`);
    // The version is read first and alone: a later version's members are not this version's, and would be
    // refused as unknown under a message that hides the reason (ADR-0207).
    const written = jsonMember(root, 'version');
    if (!(written instanceof JsonNumber)) throw refused(`${what} names its version as a number; this reader reads version ${version} (ADR-0207).`);
    if (written.int32 !== version) throw refused(`${what} of version ${written.raw} cannot be read; this reader reads version ${version} (ADR-0207).`);
    checkShape(root, shape, '$', 'the document', what);
    return root;
}

// The readers of a document whose shape has been held: a miss here is a defect of the shape above.

function memberValue(object: JsonObject, name: string): JsonValue {
    const value = jsonMember(object, name);
    if (value === undefined) throw new Error(`Internal: member '${name}' is named and missing.`);
    return value;
}

function textOf(object: JsonObject, name: string): string {
    const value = memberValue(object, name);
    if (typeof value !== 'string') throw new Error(`Internal: member '${name}' passed the shape as other than a string.`);
    return value;
}

function listOf(object: JsonObject, name: string): readonly JsonValue[] {
    const value = memberValue(object, name);
    if (!isJsonArray(value)) throw new Error(`Internal: member '${name}' passed the shape as other than an array.`);
    return value;
}

function objectOf(value: JsonValue): JsonObject {
    if (!isJsonObject(value)) throw new Error('Internal: an item passed the shape as other than an object.');
    return value;
}

function wholeOf(object: JsonObject, name: string): number {
    const value = memberValue(object, name);
    const whole = value instanceof JsonNumber ? value.int32 : undefined;
    if (whole === undefined) throw new Error(`Internal: member '${name}' passed the shape as other than a whole number.`);
    return whole;
}

/** A member that `null` or its absence leaves out, or its value. */
function presentOf(object: JsonObject, name: string): JsonValue | undefined {
    const value = jsonMember(object, name);
    return value === null ? undefined : value;
}

/**
 * A View State read back from the JSON {@link writeGridViewStateJson} writes, every column it names checked
 * against `columns`: the names of every column the grid can show, hidden ones included. Refuses with a
 * {@link GridViewStateJsonError} what the C# core refuses, in its words: text that is not JSON; a document
 * that is not an object, or of another version; a member this version does not have, a member it requires
 * that is missing or null, and a member of the wrong JSON type, at the path where it stands; a column the grid
 * does not have, in the columns, the sort or the filter; a column named twice, or pinned after one that is
 * not; a width that is neither `"Auto"` nor a finite, positive number; a direction, a combinator or an
 * operator this version does not name; and an operand whose tag is not one of the sixteen, whose value does
 * not parse as its tag's type in the invariant culture, a DateTime with no kind, and a kind on another type.
 */
export function readGridViewStateJson(json: string, columns: Iterable<string>): GridViewState {
    const known = new Set(columns);
    const document = parse(json, 'A View State', viewStateShape);
    const read: ColumnViewState[] = [];
    const seen = new Set<string>();
    let leading = true;
    listOf(document, 'columns').forEach((item, i) => {
        if (item === null) throw refused(`The View State's column ${i + 1} is null.`);
        const column = objectOf(item);
        const name = knownColumn(textOf(column, 'column'), known, `The View State's column ${i + 1}`);
        if (seen.has(name)) throw refused(`Column '${name}' appears twice in the View State's columns; a column stands in one place.`);
        seen.add(name);
        const pinned = memberValue(column, 'pinned') === true;
        if (pinned && !leading) throw refused(`Column '${name}' is pinned after a column that is not; the pinned columns are the leading ones (ADR-0004).`);
        leading &&= pinned;
        read.push({ column: name, width: readWidth(memberValue(column, 'width'), name), pinned });
    });
    const sorts = readSorts(listOf(document, 'sorts'), known);
    const filter = readFilter(presentOf(document, 'filter'), known);
    return new GridViewState(filter === undefined ? { columns: read, sorts } : { columns: read, sorts, filter });
}

/**
 * A Query read back from the JSON {@link writeGridQueryJson} writes. Refuses what
 * {@link readGridViewStateJson} refuses, and a range no grid could ask for (ADR-0001). The column names it
 * carries are not checked here, since a server reading it has no grid, but where it is applied.
 */
export function readGridQueryJson(json: string): GridQuery {
    const document = parse(json, 'A Query', queryShape);
    const range = objectOf(memberValue(document, 'range'));
    const start = wholeOf(range, 'start');
    const count = wholeOf(range, 'count');
    if (start < 0 || count < 1) {
        throw refused(`A Query's range is a run of at least one row at a position of 0 or more, and ${count} rows at ${start} is not (ADR-0001).`);
    }
    const sorts = readSorts(listOf(document, 'sorts'), undefined);
    return Object.freeze({ range: Object.freeze({ start, count }), sorts, filter: readFilter(presentOf(document, 'filter'), undefined) });
}

function knownColumn(column: string, known: ReadonlySet<string> | undefined, where: string): string {
    if (column === '') throw refused(`${where} names no column.`);
    if (known !== undefined && !known.has(column)) throw refused(`${where} names column '${column}', which the grid does not have (ADR-0207).`);
    return column;
}

function readWidth(width: JsonValue, column: string): ColumnViewStateWidth {
    if (width === 'Auto') return Object.freeze({ auto: true });
    if (width instanceof JsonNumber && Number.isFinite(width.double) && width.double > 0) return Object.freeze({ px: width.double });
    throw refused(`Column '${column}': a width is "Auto" or a finite, positive number of pixels, and ${jsonRawText(width)} is neither (ADR-0016, ADR-0207).`);
}

function readSorts(sorts: readonly JsonValue[], known: ReadonlySet<string> | undefined): readonly SortSpec[] {
    return Object.freeze(sorts.map((item, i) => {
        const where = `Sort level ${i + 1}`;
        if (item === null) throw refused(`${where} is null.`);
        const sort = objectOf(item);
        const column = knownColumn(textOf(sort, 'column'), known, where);
        const name = textOf(sort, 'direction');
        const direction = named(directionNames, name);
        if (direction === undefined) throw refused(`${where}: '${name}' is not a sort direction; a direction is Ascending or Descending (ADR-0207).`);
        return Object.freeze({ column, direction });
    }));
}

function readFilter(filter: JsonValue | undefined, known: ReadonlySet<string> | undefined): GridFilter | undefined {
    if (filter === undefined) return undefined;
    const columns = objectOf(memberValue(objectOf(filter), 'columns'));
    const read: Record<string, FilterSpec> = {};
    const order = jsonMemberNames(columns);
    for (const column of order) {
        knownColumn(column, known, 'The filter');
        const where = `the filter on '${column}'`;
        const entry = memberValue(columns, column);
        if (entry === null) throw refused(`${capital(where)} is null.`);
        const spec = objectOf(entry);
        const clauses = listOf(spec, 'clauses').map((item, i): FilterClause => {
            const at = `${where}, clause ${i + 1}`;
            if (item === null) throw refused(`${capital(at)} is null.`);
            return readClause(objectOf(item), at);
        });
        const name = textOf(spec, 'combinator');
        const combinator = named(combinatorNames, name);
        if (combinator === undefined) throw refused(`${capital(where)}: '${name}' is not a combinator; a combinator is And or Or (ADR-0009, ADR-0207).`);
        // Defined rather than assigned: a column named __proto__ is a column like any other.
        Object.defineProperty(read, column, { value: Object.freeze({ clauses: Object.freeze(clauses), combinator }), enumerable: true });
    }
    readColumnOrder.set(read, order);
    return Object.freeze({ columns: Object.freeze(read) });
}

function readClause(clause: JsonObject, at: string): FilterClause {
    const name = textOf(clause, 'operator');
    const operator = named(operatorNames, name);
    if (operator === undefined) throw refused(`${capital(at)}: '${name}' is not an operator this reader knows (ADR-0023, ADR-0207).`);
    const value = presentOf(clause, 'value');
    const values = presentOf(clause, 'values');
    const operand = value === undefined ? undefined : readOperand(objectOf(value), at);
    const list = values === undefined || !isJsonArray(values)
        ? undefined
        : Object.freeze(values.map((item, j) => (item === null ? null : readOperand(objectOf(item), `${at}, value ${j + 1}`))));
    return Object.freeze({ operator, ...(operand === undefined ? {} : { value: operand }), ...(list === undefined ? {} : { values: list }) });
}

function readOperand(operand: JsonObject, where: string): ViewStateOperand {
    const at = capital(where);
    const tag = textOf(operand, 'type');
    const value = textOf(operand, 'value');
    const kind = presentOf(operand, 'kind');
    if (kind !== undefined && tag !== 'DateTime') throw refused(`${at}: a kind belongs to a DateTime, and this operand is a ${tag}.`);
    const type = named(operandTags, tag);
    if (type === undefined) {
        throw refused(
            `${at}: '${tag}' is not an operand type this reader knows; it reads String, Boolean, Decimal, `
            + 'Double, Single, Int64, Int32, Int16, SByte, UInt64, UInt32, UInt16, Byte, DateTime, DateOnly and DateTimeOffset (ADR-0207).');
    }
    const read = operandOf(type, value, kind, at);
    if (read === undefined) throw refused(`${at}: '${value}' is not a ${tag} written in the invariant culture (ADR-0207).`);
    return Object.freeze(read);
}

function operandOf(type: ViewStateOperand['type'], text: string, kind: JsonValue | undefined, at: string): ViewStateOperand | undefined {
    switch (type) {
        case 'string':
            return { type, value: text };
        case 'boolean':
            return text === 'true' || text === 'false' ? { type, value: text === 'true' } : undefined;
        case 'decimal': {
            const value = readDecimalText(text);
            return value === undefined ? undefined : { type, value };
        }
        case 'double':
        case 'single': {
            const value = readFloatText(text, type);
            return value === undefined ? undefined : { type, value };
        }
        case 'int64': case 'int32': case 'int16': case 'sByte':
        case 'uInt64': case 'uInt32': case 'uInt16': case 'byte': {
            const value = readIntegerText(text, type);
            return value === undefined ? undefined : { type, value };
        }
        case 'dateTime': {
            // The kind first: a DateTime with none, or another, is refused for that before its text is read.
            const readKind = readDateTimeKind(kind, at);
            const ticks = readDateTimeText(text);
            return ticks === undefined ? undefined : { type, ticks, kind: readKind };
        }
        case 'dateOnly': {
            const ticks = readDateOnlyText(text);
            return ticks === undefined ? undefined : { type, ticks };
        }
        case 'dateTimeOffset': {
            const read = readDateTimeOffsetText(text);
            return read === undefined ? undefined : { type, ...read };
        }
        default:
            throw new Error(`Internal: ${JSON.stringify(type)} is a type the reader does not take.`);
    }
}

function readDateTimeKind(kind: JsonValue | undefined, at: string): ViewStateDateTimeKind {
    if (kind === undefined) throw refused(`${at}: a DateTime names its kind — Unspecified, Utc or Local (ADR-0207).`);
    if (typeof kind !== 'string') throw new Error("Internal: an operand's kind passed the shape as other than a string.");
    const name = kind;
    const read = named(kindNames, name);
    if (read === undefined) throw refused(`${at}: '${name}' is not a DateTime kind; a kind is Unspecified, Utc or Local (ADR-0207).`);
    return read;
}

/**
 * ExGrid's entry point for the View State's JSON (ADR-0207), as the C# core's `GridViewStateJson` is: a whole
 * {@link GridViewState}, or a {@link GridQuery} alone, written as JSON byte for byte as the C# core writes it,
 * and read back as what was written, refusing with a {@link GridViewStateJsonError} what the C# core refuses.
 * Filter operands are {@link ViewStateOperand}s, each carrying its type.
 */
export const GridViewStateJson = Object.freeze({
    /** The version this writer writes and the only one this reader reads (ADR-0207). */
    version,
    /** A whole View State as JSON: {@link writeGridViewStateJson}. */
    write: writeGridViewStateJson,
    /** A View State read back, checked against the grid's columns: {@link readGridViewStateJson}. */
    read: readGridViewStateJson,
    /** A Query alone as JSON: {@link writeGridQueryJson}. */
    writeQuery: writeGridQueryJson,
    /** A Query read back: {@link readGridQueryJson}. */
    readQuery: readGridQueryJson,
});
