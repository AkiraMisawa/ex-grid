// Layer 1: the View State's JSON (tests/cases/view-state, kind "viewStateJson"; ADR-0207, ADR-0370 section 5),
// run against the Script Core's GridViewStateJson, GridViewState and sameGridQuery. The C# core runs the same
// cases under three cultures; the Script Core has no current culture to run them under.
import { ticksFromText } from '@ex-grid/data';
import { describe, expect, it } from 'vitest';
import type { FilterClause, FilterSpec, GridFilter } from '../src/query/filter';
import type { SortSpec } from '../src/query/sort';
import type { GridQuery } from '../src/source/fetching-source';
import {
    GridViewState, sameGridQuery, type ColumnViewState, type ColumnViewStateWidth, type ViewStateOperand,
} from '../src/view-state/view-state';
import { GridViewStateJson, GridViewStateJsonError } from '../src/view-state/view-state-json';
import { caseFiles, expectRefusal, type Refusal } from './cases';

type Built = Readonly<Record<string, unknown>>;

interface ReadRefusal {
    readonly path: string | null;
    readonly says?: string;
    readonly saying?: string;
}

interface Refused {
    readonly kind: 'argument' | 'outOfRange' | 'notSupported';
    readonly saying?: string;
}

interface ViewStateCase {
    readonly read?: string;
    readonly readQuery?: string;
    readonly write?: Built;
    readonly writeQuery?: Built;
    readonly of?: { readonly columns: readonly { readonly name: string; readonly width?: number | 'Auto' }[]; readonly pinnedColumnCount: number; readonly sorts: readonly Built[] };
    readonly construct?: Built;
    readonly equal?: readonly [Built, Built];
    readonly equalQuery?: readonly [Built, Built];
    readonly writes?: string;
    readonly refused?: ReadRefusal & Refused;
    readonly is?: boolean;
}

function must<T>(value: T | undefined, key: string): T {
    if (value === undefined) throw new Error(`The case needs "${key}".`);
    return value;
}

function member(built: Built, key: string): unknown {
    if (!Object.hasOwn(built, key)) throw new Error(`${JSON.stringify(built)} needs "${key}".`);
    return built[key];
}

function list(built: Built, key: string): readonly unknown[] {
    const value = member(built, key);
    if (!Array.isArray(value)) throw new Error(`${JSON.stringify(built)} needs "${key}" as a list.`);
    return value;
}

function text(built: Built, key: string): string {
    const value = member(built, key);
    if (typeof value !== 'string') throw new Error(`${JSON.stringify(built)} needs "${key}" as a string.`);
    return value;
}

// ---- What a case builds, from the names the JSON writes ----------------------------------------------

const camel = (name: string): string => name.charAt(0).toLowerCase() + name.slice(1);

/** A name the JSON writes, as the model names it; a number, which no name is, as itself. */
function named(value: unknown): unknown {
    return typeof value === 'string' ? camel(value) : value;
}

function view(built: Built): GridViewState {
    const columns = list(built, 'columns').map(column => {
        if (column === null) return null as unknown as ColumnViewState;
        const c = column as Built;
        const width = member(c, 'width');
        return { column: text(c, 'column'), width: (width === 'Auto' ? { auto: true } : { px: width }) as ColumnViewStateWidth, pinned: member(c, 'pinned') as boolean };
    });
    const sorts = list(built, 'sorts').map(sort);
    return Object.hasOwn(built, 'filter') ? new GridViewState({ columns, sorts, filter: filter(member(built, 'filter') as Built) }) : new GridViewState({ columns, sorts });
}

function query(built: Built): GridQuery {
    const range = member(built, 'range');
    return {
        range: range === 'default' ? { start: 0, count: 0 } : { start: member(range as Built, 'start') as number, count: member(range as Built, 'count') as number },
        sorts: list(built, 'sorts').map(sort),
        filter: Object.hasOwn(built, 'filter') ? filter(member(built, 'filter') as Built) : undefined,
    };
}

function sort(built: unknown): SortSpec {
    if (built === null) return null as unknown as SortSpec;
    return { column: text(built as Built, 'column'), direction: named(member(built as Built, 'direction')) as SortSpec['direction'] };
}

function filter(built: Built): GridFilter {
    const columns = Object.fromEntries(Object.entries(member(built, 'columns') as Built).map(([name, spec]) => [name, spec === null ? null : specOf(spec as Built)]));
    return (Object.hasOwn(built, 'opaque') ? { columns, opaque: member(built, 'opaque') } : { columns }) as GridFilter;
}

function specOf(built: Built): FilterSpec {
    const clauses = member(built, 'clauses');
    const read = clauses === null ? null : (clauses as readonly unknown[]).map(clauseOf);
    return (Object.hasOwn(built, 'combinator') ? { clauses: read, combinator: named(member(built, 'combinator')) } : { clauses: read }) as FilterSpec;
}

function clauseOf(built: unknown): FilterClause {
    if (built === null) return null as unknown as FilterClause;
    const c = built as Built;
    return {
        operator: named(member(c, 'operator')) as FilterClause['operator'],
        ...(Object.hasOwn(c, 'value') ? { value: operand(member(c, 'value') as Built) } : {}),
        ...(Object.hasOwn(c, 'values') ? { values: list(c, 'values').map(v => (v === null ? null : operand(v as Built))) } : {}),
    };
}

/** An operand as its tag's Script Core value, read from its text by JavaScript's own parse, not the reader's. */
function operand(built: Built): ViewStateOperand {
    const value = text(built, 'value');
    const type = text(built, 'type');
    switch (type) {
        case 'String': return { type: 'string', value };
        case 'Boolean': return { type: 'boolean', value: value === 'true' };
        case 'Decimal': {
            const [whole = '', fraction = ''] = value.split('.');
            return { type: 'decimal', value: { mantissa: BigInt(whole + fraction), scale: fraction.length } };
        }
        case 'Double': return { type: 'double', value: Number(value) };
        case 'Single': return { type: 'single', value: Math.fround(Number(value)) };
        case 'Int64': case 'Int32': case 'Int16': case 'SByte': case 'UInt64': case 'UInt32': case 'UInt16': case 'Byte':
            return { type: camel(type) as 'int64', value: BigInt(value) };
        case 'DateTime': return { type: 'dateTime', ticks: ticksFromText(value), kind: camel(text(built, 'kind')) as 'utc' };
        case 'DateOnly': return { type: 'dateOnly', ticks: ticksFromText(value) };
        case 'DateTimeOffset': {
            const match = /^(.*)([+-])(\d{2}):(\d{2})$/u.exec(value);
            if (match === null) throw new Error(`'${value}' is not a DateTimeOffset a case builds.`);
            const [, clock = '', sign = '', hours = '', minutes = ''] = match;
            return { type: 'dateTimeOffset', ticks: ticksFromText(clock), offsetMinutes: (sign === '-' ? -1 : 1) * (Number(hours) * 60 + Number(minutes)) };
        }
        default: throw new Error(`'${type}' is not an operand type a case builds.`);
    }
}

// ---- The refusals -------------------------------------------------------------------------------------

function expectReadRefused(action: () => unknown, refused: ReadRefusal): void {
    let thrown: unknown;
    try {
        action();
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the refusal the case expects is held to it below
        thrown = error;
    }
    expect(thrown).toBeInstanceOf(GridViewStateJsonError);
    const error = thrown as GridViewStateJsonError;
    expect(error.path, 'the path refused at').toBe(refused.path === null ? undefined : refused.path);
    if (refused.says !== undefined) expect(error.message).toBe(refused.says);
    else expect(error.message).toContain(must(refused.saying, 'says or saying'));
}

/** A writing or a making refused by its kind; notSupported is the C# core's NotSupportedException, an
 * `Error` that is neither a `RangeError` nor a `TypeError`. */
function expectRefused(action: () => unknown, refused: Refused): void {
    const kinds: Readonly<Record<Refused['kind'], Refusal>> = { argument: 'argument', outOfRange: 'outOfRange', notSupported: 'invalidOperation' };
    expectRefusal(action, kinds[refused.kind], refused.saying);
}

// ---- The cases ----------------------------------------------------------------------------------------

for (const file of caseFiles<ViewStateCase>('view-state', 'viewStateJson')) {
    const columns = (file as unknown as { readonly columns: readonly string[] }).columns;
    describe(`view-state/${file.file}`, () => {
        for (const c of file.cases) {
            it(c.name, () => {
                expect.hasAssertions();
                if (c.read !== undefined) {
                    const json = c.read;
                    if (c.refused !== undefined) return expectReadRefused(() => GridViewStateJson.read(json, columns), c.refused);
                    const read = GridViewStateJson.read(json, columns);
                    const written = GridViewStateJson.write(read);
                    expect(written).toBe(must(c.writes, 'writes'));
                    expect(GridViewStateJson.read(written, columns).equals(read)).toBe(true);
                } else if (c.readQuery !== undefined) {
                    const json = c.readQuery;
                    if (c.refused !== undefined) return expectReadRefused(() => GridViewStateJson.readQuery(json), c.refused);
                    const read = GridViewStateJson.readQuery(json);
                    const written = GridViewStateJson.writeQuery(read);
                    expect(written).toBe(must(c.writes, 'writes'));
                    expect(sameGridQuery(GridViewStateJson.readQuery(written), read)).toBe(true);
                } else if (c.write !== undefined) {
                    const built = c.write;
                    if (c.refused !== undefined) return expectRefused(() => GridViewStateJson.write(view(built)), c.refused);
                    const state = view(built);
                    const written = GridViewStateJson.write(state);
                    expect(written).toBe(must(c.writes, 'writes'));
                    const back = GridViewStateJson.read(written, columns);
                    expect(back.equals(state), 'reads back as what was written').toBe(true);
                    // What equality does not see, and the form keeps: a Decimal's places, a DateTime's kind, an offset.
                    expect(GridViewStateJson.write(back)).toBe(written);
                } else if (c.writeQuery !== undefined) {
                    const built = c.writeQuery;
                    if (c.refused !== undefined) return expectRefused(() => GridViewStateJson.writeQuery(query(built)), c.refused);
                    const asked = query(built);
                    const written = GridViewStateJson.writeQuery(asked);
                    expect(written).toBe(must(c.writes, 'writes'));
                    const back = GridViewStateJson.readQuery(written);
                    expect(sameGridQuery(back, asked), 'reads back as what was written').toBe(true);
                    expect(GridViewStateJson.writeQuery(back)).toBe(written);
                } else if (c.of !== undefined) {
                    const { of } = c;
                    const make = (): GridViewState => GridViewState.of(
                        of.columns.map(column => (column.width === undefined ? { name: column.name }
                            : { name: column.name, width: column.width === 'Auto' ? { auto: true as const } : column.width })),
                        of.pinnedColumnCount, of.sorts.map(sort));
                    if (c.refused !== undefined) return expectRefused(make, c.refused);
                    expect(GridViewStateJson.write(make())).toBe(must(c.writes, 'writes'));
                } else if (c.construct !== undefined) {
                    const built = c.construct;
                    expectRefused(() => view(built), must(c.refused, 'refused'));
                } else if (c.equal !== undefined) {
                    const [a, b] = c.equal.map(view);
                    const equal = must(c.is, 'is');
                    expect(must(a, 'the first').equals(must(b, 'the second'))).toBe(equal);
                    expect(must(b, 'the second').equals(must(a, 'the first'))).toBe(equal);
                } else if (c.equalQuery !== undefined) {
                    const [a, b] = c.equalQuery.map(query);
                    const equal = must(c.is, 'is');
                    expect(sameGridQuery(must(a, 'the first'), must(b, 'the second'))).toBe(equal);
                    expect(sameGridQuery(must(b, 'the second'), must(a, 'the first'))).toBe(equal);
                } else {
                    throw new Error(`The case "${c.name}" names no action a viewStateJson case takes.`);
                }
                return undefined;
            });
        }
    });
}

const notional = (value: unknown): GridViewState => new GridViewState({
    columns: [], sorts: [], filter: { columns: { Notional: { clauses: [{ operator: 'equals', value }] } } },
});

function at(json: string): GridViewStateJsonError {
    try {
        GridViewStateJson.read(json, ['Book']);
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the refusal is what the test reads
        if (error instanceof GridViewStateJsonError) return error;
        throw error;
    }
    throw new Error('The JSON was read.');
}

describe('GridViewStateJson', () => {
    it('ADR-0207: an operand without its type has no JSON form, since a number could be a Decimal or a Double', () => {
        expectRefusal(() => GridViewStateJson.write(notional(1.5)), 'invalidOperation', "The filter on 'Notional', clause 1: an operand of type number has no JSON form");
        expectRefusal(() => GridViewStateJson.write(notional(new Date(0))), 'invalidOperation', 'an operand of type Date has no JSON form');
    });

    it('ADR-0207: an operand whose value its type does not hold is refused, never written as another value', () => {
        expectRefusal(() => GridViewStateJson.write(notional({ type: 'single', value: 0.1 })), 'outOfRange', 'is not a Single');
        expectRefusal(() => GridViewStateJson.write(notional({ type: 'int32', value: 2n ** 31n })), 'outOfRange', "outside a Int32's range");
        expectRefusal(() => GridViewStateJson.write(notional({ type: 'int32', value: 1 })), 'argument', 'holds a bigint');
        expectRefusal(() => GridViewStateJson.write(notional({ type: 'decimal', value: { mantissa: 1n, scale: 29 } })), 'outOfRange', 'places are 0 to 28');
        expectRefusal(() => GridViewStateJson.write(notional({ type: 'dateOnly', ticks: 1n })), 'argument', "a midnight's");
        expectRefusal(() => GridViewStateJson.write(notional({ type: 'dateTimeOffset', ticks: 0n, offsetMinutes: 60 })), 'outOfRange', 'outside a date');
    });

    it('ADR-0207: a refusal carries the path the C# core names, and none where it names none', () => {
        const misshapen = at('{"version":1,"columns":[{"column":"Book","width":"Auto","pinned":"yes"}],"sorts":[]}');
        expect(misshapen.path).toBe('$.columns[0].pinned');
        expect(misshapen.name).toBe('GridViewStateJsonError');
        expect(at('{"version":1,"columns":[{"column":"Gone","width":"Auto","pinned":false}],"sorts":[]}').path).toBeUndefined();
    });

    it('ADR-0207: the entry point and its functions are one', async () => {
        const core = await import('../src/index');
        expect(core.readGridViewStateJson).toBe(GridViewStateJson.read);
        expect(core.writeGridViewStateJson).toBe(GridViewStateJson.write);
        expect(GridViewStateJson.version).toBe(1);
    });
});
