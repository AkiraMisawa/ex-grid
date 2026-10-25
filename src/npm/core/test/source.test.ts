// Layer 1: Grid Sources (tests/cases/source, kind "source"), run against the Script Core. A case makes a
// source over its rows and takes steps on it in turn: each step is one thing the grid does to the source,
// or what it then reads.
import { describe, expect, it } from 'vitest';
import { defined, elementAt, itemAt } from '../src/must';
import type { QueryColumn } from '../src/query/columns';
import type { GridFilter } from '../src/query/filter';
import type { SortSpec } from '../src/query/sort';
import { dateIdentity } from '../src/query/dates';
import type { GridChangeBatch } from '../src/source/change-batch';
import { GridSource } from '../src/source/grid-source';
import type { InMemoryGridSource } from '../src/source/in-memory-source';
import type { RowKey } from '../src/source/row-key';
import type { RowRange } from '../src/viewport/row-range';
import type { RowKind } from '../src/paint/row-classes';
import { rowMarkHeaderState, type RowMarkHeaderState } from '../src/rows/row-marks';
import { caseFiles, expectRefused, type Refusal } from './cases';
import type { CellChangedAt } from '../src/source/cell-change-times';
import { FakeClock } from './fake-clock';
import { filterOf, formatOf, value } from './query-values';
import type { GridReplaceWrite } from '../src/finding/replace';
import { expectAnswers, summaryRequestOf, type WrittenAnswer, type WrittenAsk } from './summary-answers';
import { findRequestOf, numberText, replaceRequestOf, type WrittenFind, type WrittenReplace } from './find-requests';

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
    readonly replaceRow?: Named;
    readonly markRow?: Named;
    readonly marked?: boolean;
    readonly markAll?: boolean;
    readonly markPositions?: readonly RowRange[];
    readonly version?: number;
    readonly rowKind?: { readonly column: string; readonly kinds: Readonly<Record<string, RowKind>> };
    readonly with?: readonly unknown[] | 'itself';
    readonly apply?: Batch;
    readonly replaceAll?: readonly Entry[];
    readonly gatherInterval?: number;
    readonly advance?: number;
    readonly publishGathered?: true;
    readonly cellChangedAt?: true;
    readonly changeTimesKeptFor?: number;
    readonly needRange?: RowRange;
    readonly rows?: RowRange;
    readonly distinctValues?: string;
    readonly is?: readonly unknown[] | null;
    readonly tooMany?: true;
    readonly find?: WrittenFind;
    readonly replace?: WrittenReplace;
    readonly entryPrefix?: Readonly<Record<string, string>>;
    readonly applyDuringWrite?: Batch;
    readonly answers?: Answer;
    readonly summarize?: WrittenAsk;
    readonly summary?: Readonly<Record<string, WrittenAnswer>>;
    readonly texts?: Readonly<Record<string, string>>;
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
    readonly marked?: readonly number[];
    readonly windowMarks?: readonly boolean[];
    readonly markCounts?: readonly [number, number, number];
    readonly markHeader?: RowMarkHeaderState;
    readonly markedRows?: readonly (readonly unknown[] | RowKey)[];
    readonly marksHeard?: number;
}

interface Batch {
    readonly added?: readonly Entry[];
    readonly changed?: readonly Entry[];
    readonly removedKeys?: readonly RowKey[];
}

/** What a Replace All through the source answers. */
interface Answer {
    readonly count?: number;
    readonly version?: number;
    readonly refused?: readonly (readonly [number, string, string | null])[];
    readonly changes?: number;
    readonly applied?: true;
}

/** A row a step names: by its place, a stranger, by its position in the Window, or under a key. */
type Named = number | { readonly stranger: readonly unknown[] } | { readonly window: number } | Same;

/** The object last handed to the source under a key. */
interface Same {
    readonly same: RowKey;
}

/** A row a step hands over: a new object with these values, or the one last handed under a key. */
type Entry = readonly unknown[] | Same;

interface SourceCase {
    /** A row written `{"sameAs": place}` is the object at that place again: one row by identity (ADR-0003). */
    readonly rows: readonly (readonly unknown[] | { readonly sameAs: number })[];
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
                const columnNames = declared.map(column => column.name);
                const held: Row[] = [];
                for (const written of c.rows) held.push('sameAs' in written ? elementAt(held, written.sameAs) : rowOf(held.length, written));
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
                const named = (place: Named): Row => typeof place === 'number' ? elementAt(held, place)
                    : 'stranger' in place ? rowOf(-1, place.stranger)
                    : 'window' in place ? elementAt(source.snapshot().window, place.window)
                    : entryOf(place);
                const applyBatch = (written: Batch): void => {
                    const { added = [], changed = [], removedKeys = [] } = written;
                    const batch: GridChangeBatch<Row> = { added: added.map(entryOf), changed: changed.map(entryOf), removedKeys };
                    source.apply(batch);
                    for (const row of [...(batch.added ?? []), ...(batch.changed ?? [])]) hand(row); // oxlint-disable-line ex-grid/no-stand-in-default -- the batch was built just above with both lists
                };
                const versionOf = (step: Step): number => step.version ?? source.snapshot().rowSequenceVersion;
                let heard = 0;
                let marksHeard = 0;
                let lastWindow = source.snapshot().window;
                source.subscribe(() => { heard += 1; });
                source.marks.subscribe(() => { marksHeard += 1; });

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
                        const { marks } = source;
                        if (step.expect.marked !== undefined) {
                            expect(held.flatMap((row, place) => marks.isMarked(row) ? [place] : []), 'marked').toEqual(step.expect.marked);
                        }
                        if (step.expect.windowMarks !== undefined) expect(state.window.map(row => marks.isMarked(row)), 'windowMarks').toEqual(step.expect.windowMarks);
                        if (step.expect.markCounts !== undefined) {
                            const { counts } = marks;
                            expect([counts.markedInResult, counts.rowsInResult, counts.markedOutsideResult], 'markCounts').toEqual(step.expect.markCounts);
                        }
                        if (step.expect.markHeader !== undefined) expect(rowMarkHeaderState(marks.counts), 'markHeader').toBe(step.expect.markHeader);
                        if (step.expect.markedRows !== undefined) {
                            expect(marks.markedRows.map(row => rowKey === undefined ? row.values.map(compared) : rowKey(row)), 'markedRows')
                                .toEqual(step.expect.markedRows.map(each => Array.isArray(each) ? each.map(value).map(compared) : each));
                        }
                        if (step.expect.marksHeard !== undefined) expect(marksHeard, 'marksHeard').toBe(step.expect.marksHeard);
                        lastWindow = state.window;
                        heard = 0;
                        marksHeard = 0;
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

                // A Replace All through the source (ADR-0332 §3). The write sets a text column's text, and a
                // number column's number where the text reads as one, refusing it otherwise; under
                // "entryPrefix", the entry is the value's text after that prefix, and the write takes the
                // prefix off again.
                function replace(written: WrittenReplace, step: Step): void {
                    const prefixes = step.entryPrefix;
                    let during = step.applyDuringWrite;
                    const write = (row: Row, column: string, text: string): GridReplaceWrite<Row> => {
                        if (during !== undefined) {
                            const batch = during;
                            during = undefined;
                            applyBatch(batch);
                        }
                        const at = declared.findIndex(each => each.name === column);
                        if (at < 0) return { refusal: 'no such column' };
                        let entry = text;
                        if (prefixes !== undefined && Object.hasOwn(prefixes, column)) {
                            const prefix = defined(prefixes[column], column);
                            if (!entry.startsWith(prefix)) throw new Error(`"${c.name}" wrote '${entry}' in ${column} without its prefix ${prefix}.`);
                            entry = entry.slice(prefix.length);
                        }
                        let cell: unknown = entry;
                        if (elementAt(declared, at).type === 'number') {
                            if (!numberText.test(entry)) return { refusal: 'not a number' };
                            cell = Number(entry);
                        }
                        return { row: Object.freeze({ position: row.position, values: Object.freeze(row.values.with(at, cell)) }) };
                    };
                    const editorEntry = prefixes === undefined ? undefined : (row: Row, column: string): string | undefined => {
                        if (!Object.hasOwn(prefixes, column)) return undefined;
                        return defined(prefixes[column], column) + String(itemAt(row.values, declared.findIndex(each => each.name === column)));
                    };
                    const answer = source.replace(replaceRequestOf(written, columnNames, source.snapshot().rowSequenceVersion), write, editorEntry);
                    for (const { after } of answer.changes) {
                        if (after.position >= 0) held[after.position] = after;
                        hand(after);
                    }
                    const expected = defined(step.answers, `what the Replace All of "${c.name}" answers`);
                    const { result } = answer;
                    if (expected.version !== undefined) expect(result.rowSequenceVersion, 'version').toBe(expected.version);
                    if (expected.count !== undefined) expect(result.kind === 'replaced' ? result.count : result.kind, 'count').toBe(expected.count);
                    if (expected.refused !== undefined) {
                        expect(result.kind === 'refused' ? result.cells.map(cell => [cell.row, cell.column, cell.message === undefined ? null : cell.message]) : result.kind, 'refused')
                            .toEqual(expected.refused);
                    }
                    if (expected.changes !== undefined) expect(answer.changes.length, 'changes').toBe(expected.changes);
                    if (expected.applied === true) {
                        const { window } = source.snapshot();
                        expect(answer.changes.every(({ after }) => window.includes(after)), 'every rewritten row in the Window').toBe(true);
                    }
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
                        const target = named(step.replaceRow);
                        const written = defined(step.with, `the replacement of "${c.name}"`);
                        const replacement = written === 'itself' ? target : rowOf(target.position, written);
                        source.replaceRow(target, replacement);
                        if (target.position >= 0) held[target.position] = replacement;
                        hand(replacement);
                    } else if (step.apply !== undefined) {
                        applyBatch(step.apply);
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
                    } else if (step.markRow !== undefined) {
                        await source.marks.onMarkIntent({ kind: 'oneRow', row: named(step.markRow), marked: defined(step.marked, `what "${c.name}" marks`) });
                    } else if (step.markAll !== undefined) {
                        await source.marks.onMarkIntent({ kind: 'allRows', marked: step.markAll, rowSequenceVersion: versionOf(step) });
                    } else if (step.markPositions !== undefined) {
                        await source.marks.onMarkIntent({ kind: 'positions', ranges: step.markPositions, rowSequenceVersion: versionOf(step) });
                    } else if (step.rowKind !== undefined) {
                        const { column, kinds } = step.rowKind;
                        const at = declared.findIndex(each => each.name === column);
                        if (at < 0) throw new Error(`"${c.name}" names no column ${column} for its Row Kind.`);
                        source.marks.setRowKind(row => {
                            const written = String(row.values[at]);
                            return Object.hasOwn(kinds, written) ? defined(kinds[written], written) : 'detail';
                        });
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
                                .toEqual(defined(step.is === null ? undefined : step.is, `the values of "${c.name}"`).map(value).map(compared));
                        }
                    } else if (step.find !== undefined) {
                        const request = { ...findRequestOf(step.find, columnNames), rowSequenceVersion: source.snapshot().rowSequenceVersion };
                        const found = await source.find(request, new AbortController().signal);
                        expect(found.found ? [found.row, found.column] : null, JSON.stringify(step.find)).toEqual(step.is);
                    } else if (step.summarize !== undefined) {
                        const result = await source.summarize(summaryRequestOf(step.summarize, columnNames, source.snapshot().rowSequenceVersion), new AbortController().signal);
                        expectAnswers(result, defined(step.summary, `what the summary of "${c.name}" shows`), step.texts);
                    } else if (step.replace !== undefined) {
                        replace(step.replace, step);
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
