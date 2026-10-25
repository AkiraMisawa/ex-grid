// Layer 1: Grid Sources (tests/cases/source, kind "source"), run against the Script Core. A case makes a
// source over its rows and takes steps on it in turn: each step is one thing the grid does to the source,
// or what it then reads.
import { describe, expect, it } from 'vitest';
import { defined, elementAt } from '../src/must';
import type { QueryColumn } from '../src/query/columns';
import type { GridFilter } from '../src/query/filter';
import type { SortSpec } from '../src/query/sort';
import { dateIdentity } from '../src/query/dates';
import { GridSource } from '../src/source/grid-source';
import type { RowRange } from '../src/viewport/row-range';
import { caseFiles, expectRefused, type Refusal } from './cases';
import { filterOf, value } from './query-values';

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
    readonly replaceRow?: number | { readonly stranger: readonly unknown[] };
    readonly with?: readonly unknown[] | 'itself';
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
}

interface SourceCase {
    readonly rows: readonly (readonly unknown[])[];
    readonly columns?: readonly Declared[];
    readonly steps: readonly Step[];
}

type Declared = Pick<QueryColumn<Row>, 'name' | 'type' | 'dateType'>;

/** A value as the case compares it: a date by the date it names, anything else itself. */
function compared(read: unknown): unknown {
    return dateIdentity(read) ?? read;
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
                const columns: QueryColumn<Row>[] = declared.map((column, index) => ({ ...column, value: row => row.values[index] }));
                const held: Row[] = c.rows.map((written, position) => rowOf(position, written));
                const source = GridSource.from(held);
                let heard = 0;
                source.subscribe(() => { heard += 1; });

                for (const step of c.steps) {
                    if (step.expect !== undefined) {
                        check(step.expect, source.snapshot(), heard);
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
                        const target = typeof step.replaceRow === 'number' ? elementAt(held, step.replaceRow) : rowOf(-1, step.replaceRow.stranger);
                        const written = defined(step.with, `the replacement of "${c.name}"`);
                        const replacement = written === 'itself' ? target : rowOf(target.position, written);
                        source.replaceRow(target, replacement);
                        if (target.position >= 0) held[target.position] = replacement;
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
