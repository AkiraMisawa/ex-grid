// LV-5 (ADR-0141/0023): the incremental requery of GridSource.from with a Row Key equals applyQuery over
// the same rows, Filter and Sorts exactly, position by position and by identity, the stable tie order
// included, and equals the same changes made one replaceRow at a time. The Script Core's half of
// dotnet/tests/ExGrid.Tests/LiveRequeryPropertyTests.cs, which a single case cannot state: random bases, Sorts
// of 0 to 3 levels over every column type, Filters over every operator each type allows, batches from none
// to every row, several gathered between two publications, and a quarter of them whole new lists, in the
// base order or in a new one (LV-4, D7). The values sit in the corners: Blanks, ties (1, 1.0, 1n), case
// pairs, the Turkish i, ß against ss, accents, the empty string. After every publication the Row Sequence
// Version moved exactly when the sequence of keys did (LV-7). The seed is fixed, and every failure names
// the case and the step.
import { Temporal } from 'temporal-polyfill';
import { describe, expect, it } from 'vitest';
import { defined, elementAt } from '../src/must';
import type { QueryColumn } from '../src/query/columns';
import { filterOperatorsFor, type FilterClause, type FilterSpec, type GridFilter } from '../src/query/filter';
import { applyQuery } from '../src/query/query-engine';
import type { SortSpec } from '../src/query/sort';
import type { GridChangeBatch } from '../src/source/change-batch';
import { GridSource } from '../src/source/grid-source';
import type { InMemoryGridSource } from '../src/source/in-memory-source';
import { FakeClock } from './fake-clock';
import { Random } from './random';

const seed = 20261025;
const cases = 600;

interface Row {
    readonly id: number;
    readonly book: string | null;
    readonly desk: string | null;
    readonly pnl: number | bigint | null;
    readonly notional: number | bigint | null;
    readonly tradeDate: Temporal.PlainDateTime | null;
    readonly confirmed: boolean | null;
}

const texts: readonly (string | null)[] =
    ['a', 'A', 'b', 'B', 'ab', 'AB', 'aB', 'ä', 'Ä', 'ss', 'SS', 'ß', 'i', 'I', 'ı', 'İ', '', ' ', 'z', 'Z', 'é', 'é', null];
const numbers: readonly (number | bigint | null)[] =
    [null, -1, 0, -0, 1, 1.0, 1n, 2.5, -2.5, 1_000_000, 0.01, -0.01, Number.MAX_SAFE_INTEGER, 2n ** 70n];
const dates: readonly (Temporal.PlainDateTime | null)[] = [
    null,
    Temporal.PlainDateTime.from('2026-01-01T00:00'),
    Temporal.PlainDateTime.from('2026-01-01T00:00:00.000'),
    Temporal.PlainDateTime.from('2026-01-02T00:00'),
    Temporal.PlainDateTime.from('2025-12-31T23:59:59'),
];
const bools: readonly (boolean | null)[] = [null, true, false];

const columns: readonly QueryColumn<Row>[] = [
    { name: 'Id', type: 'number', value: row => row.id },
    { name: 'Book', type: 'text', value: row => row.book },
    { name: 'Desk', type: 'text', value: row => row.desk },
    { name: 'Pnl', type: 'number', value: row => row.pnl },
    { name: 'Notional', type: 'number', value: row => row.notional },
    { name: 'TradeDate', type: 'date', value: row => row.tradeDate },
    { name: 'Confirmed', type: 'boolean', value: row => row.confirmed },
];

// The source's own share, restated: the test counts which batches were small enough for the incremental
// path, so that both paths are known to have been compared (live-requery.ts, wholeRequeryShare).
const liveRequeryShare = 6;

function rowWith(row: Row, field: number, random: Random): Row {
    switch (field) {
        case 0: return { ...row, book: random.pick(texts) };
        case 1: return { ...row, desk: random.pick(texts) };
        case 2: return { ...row, pnl: random.pick(numbers) };
        case 3: return { ...row, notional: random.pick(numbers) };
        case 4: return { ...row, tradeDate: random.pick(dates) };
        default: return { ...row, confirmed: random.pick(bools) };
    }
}

function randomSorts(random: Random): SortSpec[] {
    return random.shuffled(columns).slice(0, random.below(4))
        .map(column => ({ column: column.name, direction: random.below(2) === 0 ? 'ascending' : 'descending' }));
}

function operand(random: Random, column: QueryColumn<Row>): unknown {
    switch (column.name) {
        case 'Id': return random.below(50);
        case 'Book':
        case 'Desk': { const text = random.pick(texts); return text === null ? 'a' : text; }
        case 'Pnl':
        case 'Notional': { const number = random.pick(numbers); return number === null ? 0 : number; }
        case 'TradeDate': return random.pick(dates.slice(1));
        default: return random.below(2) === 0;
    }
}

function randomClause(random: Random, column: QueryColumn<Row>): FilterClause {
    const operator = random.pick(filterOperatorsFor(column.type));
    if (operator === 'isBlank' || operator === 'isNotBlank') return { operator };
    if (operator === 'in') return { operator, values: Array.from({ length: random.next(1, 5) }, () => random.below(5) === 0 ? null : operand(random, column)) };
    return { operator, value: operand(random, column) };
}

function randomFilter(random: Random): GridFilter | undefined {
    if (random.below(4) === 0) return undefined;
    const specs: Record<string, FilterSpec> = {};
    for (const column of random.shuffled(columns).slice(0, random.next(1, 3))) {
        specs[column.name] = {
            clauses: Array.from({ length: random.next(1, 3) }, () => randomClause(random, column)),
            combinator: random.below(2) === 0 ? 'and' : 'or',
        };
    }
    return { columns: specs };
}

interface Batch {
    readonly added: Row[];
    readonly changed: Row[];
    readonly removedKeys: number[];
}

/** Why two lists of rows differ, or `undefined` when they hold the same objects in the same order. */
function difference(actual: readonly Row[], expected: readonly Row[]): string | undefined {
    if (actual.length !== expected.length) return `${actual.length.toString()} rows, expected ${expected.length.toString()}`;
    const at = actual.findIndex((row, i) => row !== expected[i]);
    return at < 0 ? undefined : `position ${at.toString()}: #${elementAt(actual, at).id.toString()}, expected #${elementAt(expected, at).id.toString()}`;
}

function sameKeys(a: readonly Row[], b: readonly Row[]): boolean {
    return a.length === b.length && a.every((row, i) => row.id === elementAt(b, i).id);
}

interface Outcome {
    readonly publications: number;
    readonly replaceRowPublications: number;
    readonly failures: string[];
    readonly coverage: Map<string, number>;
}

function run(): Outcome {
    const random = new Random(seed);
    const failures: string[] = [];
    const coverage = new Map<string, number>();
    const count = (what: string): void => { coverage.set(what, (coverage.get(what) ?? 0) + 1); }; // oxlint-disable-line ex-grid/no-stand-in-default -- a corner not yet reached has been reached no times
    let publications = 0;
    let replaceRowPublications = 0;
    let nextId = 0;

    const newRow = (id = nextId++): Row => ({
        id, book: random.pick(texts), desk: random.pick(texts), pnl: random.pick(numbers), notional: random.pick(numbers),
        tradeDate: random.pick(dates), confirmed: random.pick(bools),
    });
    const changedRow = (row: Row): Row => {
        // Sometimes a new object with every value the same: a change the row cannot show.
        if (random.below(10) === 0) return { ...row };
        let changed = row;
        for (let f = random.next(1, 4); f > 0; f--) changed = rowWith(changed, random.below(6), random);
        return changed === row ? { ...row } : changed;
    };

    const randomBatch = (base: readonly Row[], replaceOnly: boolean, removedEarlier: number[]): Batch => {
        const rows = base.length;
        const choice = random.below(4);
        const k = rows === 0 ? 0
            : choice === 0 ? 0
            : choice === 1 ? random.next(1, Math.min(rows, 3) + 1)
            : choice === 2 ? random.next(1, Math.max(1, Math.floor(rows / 4)) + 1)
            : random.next(1, rows + 1);
        const changed: Row[] = [];
        const removedKeys: number[] = [];
        for (const at of random.shuffled(base.map((_, i) => i)).slice(0, k)) {
            const row = elementAt(base, at);
            if (!replaceOnly && random.below(5) === 0) removedKeys.push(row.id);
            else changed.push(changedRow(row));
        }
        const added: Row[] = [];
        if (!replaceOnly) {
            for (let i = random.below(4) === 0 ? random.below(6) : 0; i > 0; i--) added.push(newRow());
            // A key removed by an earlier batch of this gathering, added again under the old key.
            if (removedEarlier.length > 0 && random.below(2) === 0) {
                const [id] = removedEarlier.splice(random.below(removedEarlier.length), 1);
                added.push(newRow(defined(id, 'the key removed earlier')));
                count('key removed and added again');
            }
        }
        return { added, changed, removedKeys };
    };

    for (let c = 0; c < cases && failures.length < 10; c++) {
        const n = random.below(10) === 0 ? random.below(4) : random.next(1, 200);
        let base: Row[] = Array.from({ length: n }, () => newRow());
        const sorts = randomSorts(random);
        const filter = randomFilter(random);

        const clock = new FakeClock();
        const source = GridSource.from(base, { rowKey: row => row.id, clock });
        source.setColumns(columns);
        source.setFilter(filter);
        source.setSorts(sorts);
        let why = difference(source.snapshot().window, applyQuery(base, columns, filter, sorts));
        if (why !== undefined) {
            failures.push(`case ${c.toString()}: the first result differs: ${why}`);
            continue;
        }

        // Every publication, as the grid would see it: the version moved exactly when the sequence of keys
        // did, against the publication before it (LV-7).
        let published = source.snapshot().window;
        let publishedVersion = source.snapshot().rowSequenceVersion;
        source.subscribe(() => {
            publications++;
            const { window: now, rowSequenceVersion } = source.snapshot();
            const moved = !sameKeys(published, now);
            count(moved ? 'sequence moved' : 'sequence kept');
            if (rowSequenceVersion !== publishedVersion + (moved ? 1 : 0)) {
                failures.push(`case ${c.toString()}: a publication took the version from ${publishedVersion.toString()} to `
                    + `${rowSequenceVersion.toString()}, and the sequence ${moved ? 'moved' : 'did not move'}`);
            }
            published = now;
            publishedVersion = rowSequenceVersion;
        });

        // A third of the cases change rows only, and are also replayed on a source without a key, one
        // replaceRow at a time.
        const replaceOnly = random.below(3) === 0;
        let oneAtATime: InMemoryGridSource<Row> | undefined;
        if (replaceOnly) {
            oneAtATime = GridSource.from(base);
            oneAtATime.setColumns(columns);
            oneAtATime.setFilter(filter);
            oneAtATime.setSorts(sorts);
        }

        for (let step = random.next(1, 7); step > 0 && failures.length < 10; step--) {
            // The interval has passed since the last publication, so the first batch is published at once and
            // the others are gathered into one publication at the interval's end.
            const batches = random.below(4) === 0 ? random.next(2, 4) : 1;
            const touched = new Set<number>();
            const removedEarlier: number[] = [];
            const replaced: [Row, Row][] = [];
            let before: string | undefined;
            for (let b = 0; b < batches; b++) {
                const batch = randomBatch(base, replaceOnly, removedEarlier);
                // A quarter of the time, a whole new list instead (LV-4, D7): the reference base is then the
                // list itself, in its order.
                if (random.below(4) === 0) {
                    const changes = new Map(batch.changed.map(row => [row.id, row]));
                    const removed = new Set(batch.removedKeys);
                    const list: Row[] = [];
                    for (const row of base) {
                        if (removed.has(row.id)) continue;
                        const changed = changes.get(row.id);
                        if (changed === undefined) {
                            list.push(row);
                        } else {
                            replaced.push([row, changed]);
                            list.push(changed);
                        }
                    }
                    let kind = 'list in the base order';
                    if (replaceOnly || random.below(3) !== 0) {
                        list.push(...batch.added);
                    } else {
                        switch (random.below(4)) {
                            case 0:
                                list.push(...batch.added);
                                list.splice(0, list.length, ...random.shuffled(list));
                                kind = 'list shuffled';
                                break;
                            case 1:
                                list.reverse();
                                list.push(...batch.added);
                                kind = 'list reversed';
                                break;
                            case 2:
                                for (let moves = random.next(1, 4); moves > 0 && list.length > 1; moves--) {
                                    const [row] = list.splice(random.below(list.length), 1);
                                    list.splice(random.below(list.length + 1), 0, defined(row, 'the row moved'));
                                }
                                list.push(...batch.added);
                                kind = 'list with rows moved';
                                break;
                            default:
                                for (const row of batch.added) list.splice(random.below(list.length + 1), 0, row);
                                kind = 'list with rows added among the others';
                        }
                    }
                    for (const id of batch.removedKeys) removedEarlier.push(id);
                    for (const id of [...batch.removedKeys, ...batch.changed.map(row => row.id), ...batch.added.map(row => row.id)]) touched.add(id);
                    base = list;
                    count(kind);
                    if (before !== undefined) count(`${before} then a list in one gathering`);
                    before = 'a list';
                    source.replaceAll(list);
                    continue;
                }
                if (before !== undefined) count(`${before} then a batch in one gathering`);
                before = 'a batch';
                // The reference base: changed in place, removed, then added at the end.
                for (const row of batch.changed) {
                    const at = base.findIndex(held => held.id === row.id);
                    replaced.push([elementAt(base, at), row]);
                    base[at] = row;
                    touched.add(row.id);
                }
                for (const key of batch.removedKeys) {
                    base.splice(base.findIndex(held => held.id === key), 1);
                    removedEarlier.push(key);
                    touched.add(key);
                }
                base.push(...batch.added);
                for (const row of batch.added) touched.add(row.id);
                const handed: GridChangeBatch<Row> = batch;
                source.apply(handed);
            }
            if (batches > 1) count('gathered several batches');
            clock.advance(1000);

            count(touched.size * liveRequeryShare <= base.length ? 'incremental-sized' : 'whole-sized');
            const where = `case ${c.toString()} step ${step.toString()}`;
            why = difference(source.snapshot().window, applyQuery(base, columns, filter, sorts));
            if (why !== undefined) {
                failures.push(`${where}: the result differs from applyQuery: ${why}; sorts ${JSON.stringify(sorts)}; `
                    + `filtered: ${String(filter !== undefined)}; ${touched.size.toString()} keys touched of ${base.length.toString()}`);
                break;
            }
            // What the grid was last told is what it reads: a Window or a version that moved without a
            // notification would never reach the screen (ADR-0023's no-op principle).
            why = difference(source.snapshot().window, published);
            if (why !== undefined || source.snapshot().rowSequenceVersion !== publishedVersion) {
                failures.push(`${where}: the Window or the version moved unheard: ${String(why)}`);
                break;
            }
            if (oneAtATime !== undefined) {
                for (const [old, replacement] of replaced) oneAtATime.replaceRow(old, replacement);
                replaceRowPublications++;
                why = difference(source.snapshot().window, oneAtATime.snapshot().window);
                if (why !== undefined) {
                    failures.push(`${where}: the result differs from replaceRow one at a time: ${why}`);
                    break;
                }
            }
        }
    }
    return { publications, replaceRowPublications, failures, coverage };
}

describe('GridSource.from with a Row Key', () => {
    it('ADR-0141 / LV-5: the incremental requery equals applyQuery and replaceRow one at a time, over random batches', () => {
        const outcome = run();
        const described = `seed ${seed.toString()}: ${outcome.publications.toString()} publications, `
            + `${outcome.replaceRowPublications.toString()} against replaceRow; `
            + [...outcome.coverage].toSorted(([a], [b]) => a < b ? -1 : 1).map(([what, n]) => `${what} ${n.toString()}`).join(', ');
        expect(outcome.failures.slice(0, 5), described).toEqual([]);
        // The corners were reached, so the comparison was not vacuous.
        const reached = (what: string): number => outcome.coverage.get(what) ?? 0; // oxlint-disable-line ex-grid/no-stand-in-default -- a corner never reached was reached no times
        expect(reached('incremental-sized'), described).toBeGreaterThan(300);
        expect(reached('whole-sized'), described).toBeGreaterThan(30);
        expect(reached('sequence moved'), described).toBeGreaterThan(150);
        expect(reached('sequence kept'), described).toBeGreaterThan(150);
        expect(reached('key removed and added again'), described).toBeGreaterThan(5);
        expect(outcome.replaceRowPublications, described).toBeGreaterThan(60);
        expect(reached('list in the base order'), described).toBeGreaterThan(200);
        for (const kind of ['list shuffled', 'list reversed', 'list with rows moved', 'list with rows added among the others']) {
            expect(reached(kind), `${kind}; ${described}`).toBeGreaterThan(20);
        }
        expect(reached('a list then a batch in one gathering'), described).toBeGreaterThan(20);
        expect(reached('a batch then a list in one gathering'), described).toBeGreaterThan(20);
    });
});
