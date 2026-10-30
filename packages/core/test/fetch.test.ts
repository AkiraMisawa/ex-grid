// The fetching source's shared cases (tests/cases/source, kind `fetch`; ADR-0350 section 5), run against
// `GridSource.fetch`: a source over a server the case answers by hand, one fetch at a time. A row is a text,
// `Row n` for the row at position n. Every step is read once the promises it set going have run, as the C#
// core's runner reads it once its continuations have. The C# core's runner is
// dotnet/tests/ExGrid.Tests/Cases/FetchCaseTests.cs.
import { describe, expect, it } from 'vitest';
import { defined, elementAt } from '../src/must';
import type { QueryColumn } from '../src/query/columns';
import type { GridFilter } from '../src/query/filter';
import type { SortSpec } from '../src/query/sort';
import type { DistinctValues } from '../src/source/grid-source';
import { GridSource } from '../src/source/grid-source';
import type { FetchingGridSource, GridPage, GridQuery } from '../src/source/fetching-source';
import { rowRange, type RowRange } from '../src/viewport/row-range';
import { caseFiles, expectRefusal, expectRefused, type Refusal } from './cases';
import { filterOf } from './query-values';

type Pair = readonly [number, number];

interface WrittenPage {
    readonly start: number;
    readonly count?: number;
    readonly rows?: readonly string[];
    readonly total: number;
    readonly orderToken?: string;
}

interface Expected {
    readonly calls?: readonly Pair[];
    readonly cancelled?: readonly boolean[];
    readonly windowStart?: number;
    readonly windowCount?: number;
    readonly first?: string;
    readonly totalCount?: number;
    readonly loading?: boolean;
    readonly version?: number;
    readonly sorts?: readonly SortSpec[];
    readonly filter?: GridFilter | null;
    readonly lastError?: string | null;
    readonly reported?: readonly string[];
    readonly vouches?: boolean;
    readonly heard?: number;
}

interface Step {
    readonly bind?: true;
    readonly setSorts?: readonly SortSpec[];
    readonly setFilter?: GridFilter | null;
    readonly requery?: true;
    readonly dispose?: true;
    readonly needRange?: Pair;
    readonly rows?: Pair;
    readonly as?: string;
    readonly throws?: Refusal;
    readonly saying?: string;
    readonly rejects?: string;
    readonly answer?: number;
    readonly fail?: number;
    readonly page?: WrittenPage;
    readonly error?: string;
    readonly settled?: string;
    readonly pending?: string;
    readonly same?: readonly [string, string];
    readonly read?: string;
    readonly is?: readonly string[];
    readonly asked?: number;
    readonly range?: Pair;
    readonly sorts?: readonly SortSpec[];
    readonly filtered?: readonly string[];
    readonly distinctValues?: string;
    readonly tooMany?: true;
    readonly handed?: readonly string[];
    readonly expect?: Expected;
}

interface FetchCase {
    readonly readAhead: number;
    readonly hears: boolean;
    readonly rowKey?: boolean;
    readonly distinct?: readonly string[];
    readonly steps: readonly Step[];
}

/** One fetch the server was asked, waiting for the case to answer it. */
interface Call {
    readonly query: GridQuery;
    readonly signal: AbortSignal;
    readonly resolve: (page: GridPage<string>) => void;
    readonly reject: (error: Error) => void;
}

/** A promise as the case reads it: whether it has settled, and how. */
interface Tracked<T> {
    readonly promise: Promise<T>;
    outcome: { readonly kind: 'pending' } | { readonly kind: 'fulfilled'; readonly value: T } | { readonly kind: 'rejected'; readonly error: unknown };
}

function track<T>(promise: Promise<T>): Tracked<T> {
    const tracked: Tracked<T> = { promise, outcome: { kind: 'pending' } };
    void (async () => {
        try {
            tracked.outcome = { kind: 'fulfilled', value: await promise };
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the case reads the rejection
            tracked.outcome = { kind: 'rejected', error };
        }
    })();
    return tracked;
}

/** Lets every promise the step set going run, as the C# core's continuations run when an answer is given. */
async function settle(): Promise<void> {
    await new Promise(resolve => { setTimeout(resolve, 0); });
}

function pageOf(written: WrittenPage): GridPage<string> {
    const rows = written.rows ?? Array.from({ length: defined(written.count, 'a page\'s count, or its rows') }, (_, at) => `Row ${written.start + at}`);
    return { rows, start: written.start, totalCount: written.total, ...(written.orderToken === undefined ? {} : { orderToken: written.orderToken }) };
}

function rangeOf(pair: Pair): RowRange {
    return rowRange(pair[0], pair[1]);
}

function messageOf(error: unknown): string {
    if (!(error instanceof Error)) throw new Error(`Expected an Error, got ${String(error)}.`);
    return error.message;
}

function named<T>(map: Map<string, Tracked<T>>, label: string): Tracked<T> {
    return defined(map.get(label), `the promise named ${label}`);
}

for (const file of caseFiles<FetchCase>('source', 'fetch')) {
    describe(file.file, () => {
        const columns = (file as unknown as { readonly columns: readonly string[] }).columns
            .map((name): QueryColumn<string> => ({ name, type: 'text', value: row => row }));
        for (const c of file.cases) {
            it(c.name, async () => {
                expect.hasAssertions();
                const calls: Call[] = [];
                const handed: (readonly string[])[] = [];
                const reported: string[] = [];
                const { distinct } = c;
                const source: FetchingGridSource<string> = GridSource.fetch<string>(
                    (query, signal) => new Promise<GridPage<string>>((resolve, reject) => {
                        calls.push({ query, signal, resolve, reject });
                    }),
                    {
                        readAheadRows: c.readAhead,
                        ...(distinct === undefined ? {} : {
                            distinctValues: async (_column: string, filter: GridFilter | undefined): Promise<DistinctValues> => {
                                handed.push(filter === undefined ? [] : Object.keys(filter.columns));
                                return { kind: 'values', values: distinct };
                            },
                        }),
                        ...(c.rowKey === true ? { rowKey: (row: string) => row } : {}),
                        ...(c.hears ? { onFetchFailed: ({ error }: { readonly error: unknown }) => { reported.push(messageOf(error)); } } : {}),
                    });
                let heard = 0;
                source.subscribe(() => { heard += 1; });
                const promises = new Map<string, Tracked<void>>();
                const copies = new Map<string, Tracked<readonly string[]>>();

                for (const step of c.steps) {
                    if (step.bind !== undefined) {
                        source.setColumns(columns);
                    } else if (step.setSorts !== undefined) {
                        source.setSorts(step.setSorts);
                    } else if (step.setFilter !== undefined) {
                        source.setFilter(step.setFilter === null ? undefined : filterOf(step.setFilter));
                    } else if (step.requery !== undefined) {
                        source.requery();
                    } else if (step.dispose !== undefined) {
                        source.dispose();
                    } else if (step.needRange !== undefined) {
                        const [start, count] = step.needRange;
                        if (step.throws !== undefined) {
                            expectRefusal(() => source.needRange({ start, count }), step.throws);
                            continue;
                        }
                        promises.set(defined(step.as, 'the name of a range asked for'), track(source.needRange(rangeOf(step.needRange))));
                    } else if (step.rows !== undefined) {
                        copies.set(defined(step.as, 'the name of rows read'), track(source.rows(rangeOf(step.rows), new AbortController().signal)));
                    } else if (step.answer !== undefined) {
                        elementAt(calls, step.answer).resolve(pageOf(defined(step.page, 'the page answered')));
                    } else if (step.fail !== undefined) {
                        elementAt(calls, step.fail).reject(new Error(defined(step.error, 'what the fetch failed with')));
                    } else if (step.settled !== undefined) {
                        const { outcome } = named(promises, step.settled);
                        expect(outcome.kind, `${step.settled} settled`).not.toBe('pending');
                        if (step.throws !== undefined) {
                            expectRefused(outcome.kind === 'rejected' ? outcome.error : undefined, step.throws, step.saying);
                        } else if (step.rejects !== undefined) {
                            expect(outcome.kind === 'rejected' ? messageOf(outcome.error) : outcome.kind, 'rejected with').toBe(step.rejects);
                        } else {
                            expect(outcome.kind, `${step.settled} fulfilled`).toBe('fulfilled');
                        }
                    } else if (step.pending !== undefined) {
                        expect(named(promises, step.pending).outcome.kind, `${step.pending} pending`).toBe('pending');
                    } else if (step.same !== undefined) {
                        expect(named(promises, step.same[0]).promise).toBe(named(promises, step.same[1]).promise);
                    } else if (step.read !== undefined) {
                        const { outcome } = named(copies, step.read);
                        expect(outcome.kind === 'fulfilled' ? outcome.value : outcome.kind, step.read).toEqual(step.is);
                    } else if (step.asked !== undefined) {
                        const { query } = elementAt(calls, step.asked);
                        expect([query.range.start, query.range.count], 'range').toEqual(step.range);
                        expect(query.sorts, 'sorts').toEqual(step.sorts);
                        expect(query.filter === undefined ? [] : Object.keys(query.filter.columns), 'filtered').toEqual(step.filtered);
                    } else if (step.distinctValues !== undefined) {
                        const answer = await source.distinctValues(step.distinctValues, new AbortController().signal);
                        if (step.tooMany !== undefined) {
                            expect(answer.kind).toBe('tooMany');
                            continue;
                        }
                        expect(answer.kind === 'values' ? answer.values : answer.kind, 'values').toEqual(step.is);
                        expect(handed.at(-1), 'the filter handed').toEqual(step.handed);
                    } else if (step.expect !== undefined) {
                        expectState(source, step.expect, calls, heard, reported);
                        heard = 0;
                        reported.length = 0;
                    } else {
                        throw new Error(`${JSON.stringify(step)} is no step a fetch case takes.`);
                    }
                    await settle();
                }
            });
        }
    });
}

function expectState(source: FetchingGridSource<string>, expected: Expected, calls: readonly Call[], heard: number, reported: readonly string[]): void {
    const state = source.snapshot();
    if (expected.calls !== undefined) expect(calls.map(({ query }) => [query.range.start, query.range.count]), 'calls').toEqual(expected.calls);
    if (expected.cancelled !== undefined) expect(calls.map(({ signal }) => signal.aborted), 'cancelled').toEqual(expected.cancelled);
    if (expected.windowStart !== undefined) expect(state.windowStart, 'windowStart').toBe(expected.windowStart);
    if (expected.windowCount !== undefined) expect(state.window.length, 'windowCount').toBe(expected.windowCount);
    if (expected.first !== undefined) expect(state.window[0], 'first').toBe(expected.first);
    if (expected.totalCount !== undefined) expect(state.totalCount, 'totalCount').toBe(expected.totalCount);
    if (expected.loading !== undefined) expect(state.loading, 'loading').toBe(expected.loading);
    if (expected.version !== undefined) expect(state.rowSequenceVersion, 'version').toBe(expected.version);
    if (expected.sorts !== undefined) expect(state.sorts, 'sorts').toEqual(expected.sorts);
    if (expected.filter !== undefined) {
        expect(state.filter, 'filter').toEqual(expected.filter === null ? undefined : filterOf(expected.filter));
    }
    if (expected.lastError !== undefined) {
        if (expected.lastError === null) expect(state.lastError, 'lastError').toBeUndefined();
        else expect(messageOf(state.lastError), 'lastError').toContain(expected.lastError);
    }
    if (expected.reported !== undefined) {
        expect(reported.length, 'reported').toBe(expected.reported.length);
        expected.reported.forEach((said, at) => { expect(elementAt(reported, at), 'reported').toContain(said); });
    }
    if (expected.vouches !== undefined) expect(source.vouchesDistinctRows, 'vouches').toBe(expected.vouches);
    if (expected.heard !== undefined) expect(heard, 'heard').toBe(expected.heard);
}
