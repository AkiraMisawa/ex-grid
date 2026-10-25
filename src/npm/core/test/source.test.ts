// Layer 1: Grid Sources (tests/cases/source, kind "source"), run against the Script Core. A case makes a
// source over its rows and takes steps on it in turn: each step is one thing the grid does to the source,
// or what it then reads.
import { describe, expect, it } from 'vitest';
import { defined, elementAt } from '../src/must';
import type { QueryColumn } from '../src/query/columns';
import type { GridFilter } from '../src/query/filter';
import type { SortSpec } from '../src/query/sort';
import { dateIdentity } from '../src/query/dates';
import type { GridChangeBatch } from '../src/source/change-batch';
import { GridSource } from '../src/source/grid-source';
import type { InMemoryGridSource } from '../src/source/in-memory-source';
import type { RowKey } from '../src/source/row-key';
import type { RowRange } from '../src/viewport/row-range';
import { caseFiles, expectRefused, type Refusal } from './cases';
import type { CellChangedAt } from '../src/source/cell-change-times';
import { FakeClock } from './fake-clock';
import { filterOf, formatOf, value } from './query-values';

/** A row of a case: its values, and the place in the case's rows it stands for, which every version of
 * it keeps. A new object for every version, since identity is what says a row changed. */
interface Row {
    readonly position: number;
    readonly values: readonly unknown[];
}

interface Step {
    readonly setColumns?: true | 'rebuilt' | readonly string[];
    readonly setSorts?: readonly SortSpec[];
    readonly setFilter?: GridFilter | null;
    readonly replaceRow?: number | { readonly stranger: readonly unknown[] } | { readonly window: number } | Same;
    readonly with?: readonly unknown[] | 'itself';
    readonly apply?: { readonly added?: readonly Entry[]; readonly changed?: readonly Entry[]; readonly removedKeys?: readonly RowKey[] };
    readonly replaceAll?: readonly Entry[];
    readonly gatherInterval?: number;
    readonly advance?: number;
    readonly publishGathered?: true;
    readonly cellChangedAt?: true;
    readonly changeTimesKeptFor?: number;
    readonly needRange?: RowRange;
    readonly rows?: RowRange;
    readonly distinctValues?: string;
    readonly is?: readonly unknown[];
    readonly tooMany?: true;
    readonly expect?: Expectation;
    readonly throws?: Refusal;
    readonly saying?: string;
}

interface Expectation {
    readonly window?: readonly number[];
    readonly values?: readonly (readonly unknown[])[];
    readonly windowStart?: number;
    readonly totalCount?: number | null;
    readonly loading?: boolean;
    readonly version?: number;
    readonly sorts?: readonly SortSpec[];
    readonly filter?: GridFilter | null;
    readonly heard?: number;
    readonly keys?: readonly RowKey[];
    readonly windowKept?: boolean;
    readonly vouches?: boolean;
    readonly handed?: true;
    readonly gatherInterval?: number;
    readonly changeTimes?: boolean;
    readonly changeTimesKeptFor?: number;
    readonly changedAt?: Readonly<Record<string, Readonly<Record<string, number | null>>>>;
}

/** The object last handed to the source under a key. */
interface Same {
    readonly same: RowKey;
}

/** A row a step hands over: a new object with these values, or the one last handed under a key. */
type Entry = readonly unknown[] | Same;

interface SourceCase {
    readonly rows: readonly (readonly unknown[])[];
    readonly rowKey?: string;
    readonly fromThrows?: Refusal;
    readonly fromSaying?: string;
    readonly columns?: readonly Declared[];
    readonly steps: readonly Step[];
}

type Declared = Pick<QueryColumn<Row>, 'name' | 'type' | 'dateType'> & { readonly format?: string };

/** A value as the case compares it: a date by the date it names, anything else itself. */
function compared(read: unknown): unknown {
    return dateIdentity(read) ?? read;
}

function isWrittenRow(entry: Entry): entry is readonly unknown[] {
    return Array.isArray(entry);
}

function rowOf(position: number, written: readonly unknown[]): Row {
    return Object.freeze({ position, values: Object.freeze(written.map(value)) });
}

for (const file of caseFiles<SourceCase>('source', 'source')) {
    const { columns: fileColumns } = file as unknown as { readonly columns: readonly Declared[] };
    describe(`source/${file.file}`, () => {
        for (const c of file.cases) {
            it(c.name, async () => {
                expect.hasAssertions();
                const declared = c.columns ?? fileColumns;
                const columns: QueryColumn<Row>[] = declared.map(({ format, ...column }, index) => ({
                    ...column,
                    value: row => row.values[index],
                    ...(format === undefined ? {} : { format: formatOf(format) }),
                }));
                const held: Row[] = c.rows.map((written, position) => rowOf(position, written));
                const clock = new FakeClock();
                const start = clock.now();
                let changedAt: CellChangedAt<Row> | undefined;
                const keyAt = c.rowKey === undefined ? undefined : declared.findIndex(column => column.name === c.rowKey);
                const rowKey = keyAt === undefined ? undefined : (row: Row): RowKey => row.values[keyAt] as RowKey;
                const make = (): InMemoryGridSource<Row> => GridSource.from(held, rowKey === undefined ? {} : { rowKey, clock });
                if (c.fromThrows !== undefined) {
                    let refused: unknown;
                    try {
                        make();
                    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the refusal the case expects is held to it below
                        refused = error;
                    }
                    expectRefused(refused, c.fromThrows, c.fromSaying);
                    return;
                }
                const source = make();
                // The object last handed to the source under each key.
                const handed = new Map<RowKey, Row>(rowKey === undefined ? [] : held.map(row => [rowKey(row), row]));
                const hand = (row: Row): Row => {
                    if (rowKey !== undefined) handed.set(rowKey(row), row);
                    return row;
                };
                const entryOf = (entry: Entry): Row => isWrittenRow(entry) ? rowOf(-1, entry) : defined(handed.get(entry.same), `the row handed under ${String(entry.same)}`);
                let heard = 0;
                let lastWindow = source.snapshot().window;
                source.subscribe(() => { heard += 1; });

                for (const step of c.steps) {
                    if (step.expect !== undefined) {
                        const state = source.snapshot();
                        check(step.expect, state, heard);
                        if (step.expect.windowKept !== undefined) expect(state.window === lastWindow, 'windowKept').toBe(step.expect.windowKept);
                        if (step.expect.vouches !== undefined) expect(source.vouchesDistinctRows, 'vouches').toBe(step.expect.vouches);
                        if (step.expect.gatherInterval !== undefined) expect(source.gatherInterval, 'gatherInterval').toBe(step.expect.gatherInterval);
                        if (step.expect.keys !== undefined) {
                            const keyOf = defined(rowKey, `the Row Key of "${c.name}"`);
                            expect(state.window.map(keyOf), 'keys').toEqual(step.expect.keys);
                        }
                        if (step.expect.changeTimes !== undefined) expect(source.cellChangedAt !== undefined, 'changeTimes').toBe(step.expect.changeTimes);
                        if (step.expect.changeTimesKeptFor !== undefined) expect(source.changeTimesKeptFor, 'changeTimesKeptFor').toBe(step.expect.changeTimesKeptFor);
                        if (step.expect.changedAt !== undefined) {
                            const keyOf = defined(rowKey, `the Row Key of "${c.name}"`);
                            const lookup = defined(changedAt, `the cellChangedAt "${c.name}" read`);
                            for (const [key, byColumn] of Object.entries(step.expect.changedAt)) {
                                const row = defined(state.window.find(each => String(keyOf(each)) === key), `the row under ${key}`);
                                for (const [column, at] of Object.entries(byColumn)) {
                                    const read = lookup(row, column);
                                    expect(read === undefined ? null : read - start, `changedAt of ${key}, ${column}`).toBe(at);
                                }
                            }
                        }
                        if (step.expect.handed === true) {
                            const keyOf = defined(rowKey, `the Row Key of "${c.name}"`);
                            expect(state.window.every(row => handed.get(keyOf(row)) === row), 'every row the object last handed').toBe(true);
                        }
                        lastWindow = state.window;
                        heard = 0;
                        continue;
                    }
                    if (step.throws === undefined) {
                        await take(step);
                        continue;
                    }
                    let thrown: unknown;
                    try {
                        await take(step);
                    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the refusal the step expects is held to it below
                        thrown = error;
                    }
                    expectRefused(thrown, step.throws, step.saying);
                }

                async function take(step: Step): Promise<void> {
                    if (step.setColumns !== undefined) {
                        const names = step.setColumns;
                        source.setColumns(names === true ? columns
                            : names === 'rebuilt' ? columns.map(column => ({ ...column }))
                            : names.map(name => defined(columns.find(column => column.name === name), name)));
                    } else if (step.setSorts !== undefined) {
                        source.setSorts(step.setSorts);
                    } else if (step.setFilter !== undefined) {
                        source.setFilter(step.setFilter === null ? undefined : filterOf(step.setFilter));
                    } else if (step.replaceRow !== undefined) {
                        const place = step.replaceRow;
                        const target = typeof place === 'number' ? elementAt(held, place)
                            : 'stranger' in place ? rowOf(-1, place.stranger)
                            : 'window' in place ? elementAt(source.snapshot().window, place.window)
                            : entryOf(place);
                        const written = defined(step.with, `the replacement of "${c.name}"`);
                        const replacement = written === 'itself' ? target : rowOf(target.position, written);
                        source.replaceRow(target, replacement);
                        if (target.position >= 0) held[target.position] = replacement;
                        hand(replacement);
                    } else if (step.apply !== undefined) {
                        const { added = [], changed = [], removedKeys = [] } = step.apply;
                        const batch: GridChangeBatch<Row> = { added: added.map(entryOf), changed: changed.map(entryOf), removedKeys };
                        source.apply(batch);
                        for (const row of [...(batch.added ?? []), ...(batch.changed ?? [])]) hand(row); // oxlint-disable-line ex-grid/no-stand-in-default -- the batch was built just above with both lists
                    } else if (step.replaceAll !== undefined) {
                        const rows = step.replaceAll.map(entryOf);
                        source.replaceAll(rows);
                        for (const row of rows) hand(row);
                    } else if (step.gatherInterval !== undefined) {
                        source.gatherInterval = step.gatherInterval;
                    } else if (step.advance !== undefined) {
                        clock.advance(step.advance);
                    } else if (step.publishGathered === true) {
                        source.publishGathered();
                    } else if (step.cellChangedAt === true) {
                        changedAt = source.cellChangedAt;
                    } else if (step.changeTimesKeptFor !== undefined) {
                        source.changeTimesKeptFor = step.changeTimesKeptFor;
                    } else if (step.needRange !== undefined) {
                        await source.needRange(step.needRange);
                    } else if (step.rows !== undefined) {
                        const rows = await source.rows(step.rows, new AbortController().signal);
                        expect(rows.map(row => row.position)).toEqual(step.is);
                    } else if (step.distinctValues !== undefined) {
                        const answer = await source.distinctValues(step.distinctValues, new AbortController().signal);
                        if (step.tooMany === true) {
                            expect(answer.kind).toBe('tooMany');
                        } else {
                            expect(answer.kind === 'values' ? answer.values.map(compared) : answer.kind)
                                .toEqual(defined(step.is, `the values of "${c.name}"`).map(value).map(compared));
                        }
                    } else {
                        throw new Error(`A step of "${c.name}" takes nothing: ${JSON.stringify(step)}.`);
                    }
                }
            });
        }
    });
}

function check(expected: Expectation, state: ReturnType<ReturnType<typeof GridSource.from<Row>>['snapshot']>, heard: number): void {
    if (expected.window !== undefined) expect(state.window.map(row => row.position), 'window').toEqual(expected.window);
    if (expected.values !== undefined) {
        expect(state.window.map(row => row.values.map(compared)), 'values').toEqual(expected.values.map(row => row.map(value).map(compared)));
    }
    if (expected.windowStart !== undefined) expect(state.windowStart, 'windowStart').toBe(expected.windowStart);
    if (expected.totalCount !== undefined) {
        expect(state.totalCount === undefined ? null : state.totalCount, 'totalCount').toBe(expected.totalCount);
    }
    if (expected.loading !== undefined) expect(state.loading, 'loading').toBe(expected.loading);
    if (expected.version !== undefined) expect(state.rowSequenceVersion, 'version').toBe(expected.version);
    if (expected.sorts !== undefined) expect(state.sorts, 'sorts').toEqual(expected.sorts);
    if (expected.filter !== undefined) {
        expect(state.filter, 'filter').toEqual(expected.filter === null ? undefined : filterOf(expected.filter));
    }
    if (expected.heard !== undefined) expect(heard, 'heard').toBe(expected.heard);
}
