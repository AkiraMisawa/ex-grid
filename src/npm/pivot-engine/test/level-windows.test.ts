// Layer 1: the bundled sources' windows of sibling sets ordered by a value (ADR-0370 section 3). Over data made
// to tie at the edges, with Blanks and with sets nested in sets, every window must be where it was asked, its
// margins strictly beyond its edges, before, margins, window and after composing to the parent at every column
// group, and each sibling it answers the cell the whole answer has for it, to the last bit. The answer must
// cross PivotJson as the same text, and an answer of another shape is named for what it lacks.
import { decimalFromText } from '@ex-grid/data';
import { describe, expect, it } from 'vitest';
import {
    pivotCube, PivotAnswerBuilder, PivotJson, pivotQuery, pivotRecordField, RecordPivotSource, textItemKey, type PivotAnswer, type PivotItemKey, type PivotLevelOrder,
    type PivotLevelRuleInput, type PivotQuery, type PivotWindowStart,
} from '../src/index';
import { answerColumns } from '../src/cube/answer';
import { levelMismatch, levelPartsAt, type LevelParts } from '../src/levels/level-check';
import { defined, elementAt } from '../src/must';

/** A reproducible stream of numbers in [0, 1). */
function random(seed: number): () => number {
    let state = seed >>> 0;
    return () => {
        state = (state + 0x6d2b79f5) >>> 0;
        let t = state;
        t = Math.imul(t ^ (t >>> 15), t | 1);
        t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
        return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
}

interface Trade {
    readonly region: string;
    readonly desk: string;
    readonly book: string;
    readonly live: boolean;
    readonly trader: string;
    readonly amount: unknown;
}

const fields = [
    pivotRecordField<Trade>({ name: 'Region', type: 'text', value: t => t.region }),
    pivotRecordField<Trade>({ name: 'Desk', type: 'text', value: t => t.desk }),
    pivotRecordField<Trade>({ name: 'Book', type: 'text', value: t => t.book }),
    pivotRecordField<Trade>({ name: 'Live', type: 'boolean', value: t => t.live }),
    pivotRecordField<Trade>({ name: 'Trader', type: 'text', value: t => t.trader }),
    pivotRecordField<Trade>({ name: 'Amount', type: 'number', value: t => t.amount }),
];

// Few records a book and amounts from a short list, so sums tie; a book of Blanks now and then, whose value is
// no number. The traders, under a collapsed Live, make the leaves outnumber the cells shown, so a cap between the
// two has the whole set answered as cells.
function trades(seed: number, count: number, exact: boolean): Trade[] {
    const next = random(seed);
    const pick = <T extends string | number>(items: readonly T[]): T => elementAt(items, Math.floor(next() * items.length));
    return Array.from({ length: count }, () => {
        const book = `B${Math.floor(next() * 60)}`;
        const blank = book.endsWith('7');
        const value = pick([1, 2, 3, 0.1, 0.2, -1]);
        return {
            region: pick(['East', 'West', 'North']),
            desk: pick(['Rates', 'Credit', 'FX', 'Loans']),
            book,
            live: next() < 0.5,
            trader: pick(['Ann', 'Bo', 'Cy', 'Di', 'Ed']),
            amount: blank ? null : exact ? decimalFromText(String(value)) : value,
        };
    });
}

const amount = (aggregation: PivotLevelOrder['aggregation'], descending: boolean): PivotLevelOrder => ({ value: 0, aggregation, descending });

const book = (n: number): PivotItemKey => textItemKey(`B${n}`);

const liveCollapsed: readonly PivotLevelRuleInput[] = [{ collapsed: true }, {}];

function questionOf(rules: readonly PivotLevelRuleInput[], limit: number, starts: readonly PivotWindowStart[] = [], window = true): PivotQuery {
    const rows = rules.length === 3 ? ['Region', 'Desk', 'Book'] : ['Region', 'Book'];
    return pivotQuery({
        rows, columns: ['Live', 'Trader'],
        values: [{ field: 'Amount', parts: ['sum', 'extremes', 'variance'] }],
        maxLeaves: 100_000,
        levels: window ? { rows: rules, columns: liveCollapsed, window: { limit, starts } } : { rows: rules, columns: liveCollapsed },
    });
}

// A cell's row Items as indexes, and its column Items: −1 below its depth.
function rowPath(answer: PivotAnswer, cell: number): number[] {
    return answer.rows.map(axis => axis.itemAt(cell));
}

function columnKey(answer: PivotAnswer, cell: number): string {
    return answer.columns.map(axis => (axis.itemAt(cell) < 0 ? '-' : JSON.stringify(elementAt(axis.items, axis.itemAt(cell))))).join('/');
}

// A row group's Items as keys, for comparing two answers.
function rowKeys(answer: PivotAnswer, cell: number): string {
    return answer.rows.map(axis => (axis.itemAt(cell) < 0 ? '-' : JSON.stringify(elementAt(axis.items, axis.itemAt(cell))))).join('/');
}

function partsOf(answer: PivotAnswer, cell: number): LevelParts {
    return levelPartsAt(answerColumns(elementAt(answer.values, 0)), cell);
}

function keyOf(answer: PivotAnswer, cell: number, order: PivotLevelOrder): number | undefined {
    const value = answerColumns(elementAt(answer.values, order.value)).read(cell, order.aggregation);
    return value.kind === 'number' ? value.number : undefined;
}

// Sorts cells by their value as the order says, a cell of no number last, ties in the order given.
function ordered(answer: PivotAnswer, cells: readonly number[], order: PivotLevelOrder): number[] {
    return cells.toSorted((a, b) => {
        const x = keyOf(answer, a, order);
        const y = keyOf(answer, b, order);
        if (x === undefined || y === undefined) return x === undefined ? (y === undefined ? 0 : 1) : -1;
        const compared = x < y ? -1 : x > y ? 1 : 0;
        return order.descending ? -compared : compared;
    });
}

// Checks every window of `answer` against the whole answer to the same question without one; how many it checked.
function checkWindows(query: PivotQuery, answer: PivotAnswer, whole: PivotAnswer): number {
    const levels = query.levels;
    if (levels?.window === undefined) throw new Error('A question with a window.');
    const limit = levels.window.limit;
    const cells = Array.from({ length: answer.leafCount }, (_, cell) => cell);
    const groups = cells.filter(cell => answer.restAt(cell) === undefined);
    const wholeByKey = new Map(Array.from({ length: whole.leafCount }, (_, cell) => [`${rowKeys(whole, cell)}|${columnKey(whole, cell)}`, cell]));
    // Every group the window answers is the whole answer's cell, part for part.
    for (const cell of groups) {
        const held = wholeByKey.get(`${rowKeys(answer, cell)}|${columnKey(answer, cell)}`);
        expect(held, `cell ${cell} is a group of the whole answer`).toBeDefined();
        if (held === undefined) continue;
        expect(answer.recordsAt(cell)).toBe(whole.recordsAt(held));
        expect(partsOf(answer, cell)).toEqual(partsOf(whole, held));
    }
    answer.windows.forEach((window, w) => {
        const depth = window.parent.length;
        const order = elementAt(levels.rows, depth).order;
        if (order === undefined) throw new Error('A window is of a set ordered by a value.');
        const under = (cell: number): boolean => {
            const path = rowPath(answer, cell);
            return window.parent.every((item, level) => path[level] === item) && (path[depth] ?? -1) >= 0 && (path[depth + 1] ?? -1) < 0; // oxlint-disable-line ex-grid/no-stand-in-default -- past the row fields is below every depth
        };
        const atRoot = (cell: number): boolean => answer.columns.every(axis => axis.itemAt(cell) < 0);
        const siblings = ordered(answer, groups.filter(cell => under(cell) && atRoot(cell)), order);
        const margins = (window.before > 0 ? 1 : 0) + (window.after > 0 ? 1 : 0);
        const shown = siblings.length - margins;
        const total = window.before + shown + window.after;
        expect(total).toBeGreaterThan(limit);
        // Its margins strictly beyond its edges.
        if (window.before > 0) expect(keyOf(answer, elementAt(siblings, 0), order)).not.toBe(keyOf(answer, elementAt(siblings, 1), order));
        if (window.after > 0) expect(keyOf(answer, elementAt(siblings, siblings.length - 1), order)).not.toBe(keyOf(answer, elementAt(siblings, siblings.length - 2), order));
        // Where it was asked: the whole set's order, cut there.
        const parentKey = rowKeys(answer, defined(cells.find(cell => answer.restAt(cell) === undefined && atRoot(cell)
            && rowPath(answer, cell).every((item, level) => (level < depth ? item === elementAt(window.parent, level) : item < 0))), "a window's parent's cell"));
        const wholeSiblings = ordered(whole, Array.from({ length: whole.leafCount }, (_, cell) => cell).filter(cell => {
            const keys = rowKeys(whole, cell).split('/');
            return whole.columns.every(axis => axis.itemAt(cell) < 0) && keys[depth] !== '-' && (keys[depth + 1] ?? '-') === '-' // oxlint-disable-line ex-grid/no-stand-in-default -- past the row fields is below every depth
                && keys.slice(0, depth).join('/') === parentKey.split('/').slice(0, depth).join('/');
        }), order);
        expect(wholeSiblings.length).toBe(total);
        const answered = wholeSiblings.slice(Math.max(0, window.before - 1), total - window.after + (window.after > 0 ? 1 : 0)).map(cell => rowKeys(whole, cell)).toSorted();
        expect(siblings.map(cell => rowKeys(answer, cell)).toSorted()).toEqual(answered);
        // Before, margins, window and after compose to the parent at every column group.
        for (const parent of groups.filter(cell => {
            const path = rowPath(answer, cell);
            return path.every((item, level) => (level < depth ? item === elementAt(window.parent, level) : item < 0));
        })) {
            const column = columnKey(answer, parent);
            const children = [
                ...groups.filter(cell => under(cell) && columnKey(answer, cell) === column),
                ...cells.filter(cell => answer.restAt(cell)?.window === w && columnKey(answer, cell) === column),
            ];
            expect(children.reduce((sum, child) => sum + answer.recordsAt(child), 0), `records at ${column}`).toBe(answer.recordsAt(parent));
            expect(levelMismatch(partsOf(answer, parent), children.map(child => partsOf(answer, child))), `parts at ${column}`).toBeUndefined();
        }
    });
    return answer.windows.length;
}

describe('ADR-0370: the bundled sources answer a sibling set past the limit as a window', () => {
    const cases: { readonly name: string; readonly rules: readonly PivotLevelRuleInput[]; readonly limit: number; readonly starts?: (whole: PivotAnswer) => PivotWindowStart[] }[] = [
        { name: 'the books under each region, by sum descending', rules: [{}, { order: amount('sum', true) }], limit: 5 },
        { name: 'by average ascending, started part way', rules: [{}, { order: amount('average', false) }], limit: 4, starts: () => [{ rows: [textItemKey('East')], start: 7 }, { rows: [textItemKey('West')], start: 1000 }] },
        { name: 'by count, which ties often', rules: [{}, { order: amount('count', true) }], limit: 3, starts: () => [{ rows: [textItemKey('North')], start: 2 }] },
        { name: 'by max, a set nested under a windowed one', rules: [{ order: amount('max', true) }, { order: amount('sum', false) }], limit: 2 },
        { name: 'the regions under the grand total, by stdDev', rules: [{ order: amount('stdDev', true) }, {}], limit: 1, starts: () => [{ rows: [], start: 1 }] },
        { name: 'books under desks under regions, the desks collapsed but one', rules: [{}, { collapsed: true, toggledItems: [textItemKey('FX')] }, { order: amount('sum', true) }], limit: 6 },
    ];
    for (const exact of [true, false]) {
        for (const c of cases) {
            it(`ADR-0370: ${c.name}, ${exact ? 'exact' : 'double'} amounts`, async () => {
                for (const seed of [1, 2]) {
                    const data = trades(seed, 1500, exact);
                    const source = new RecordPivotSource(data, fields);
                    const plain = questionOf(c.rules, c.limit, [], false);
                    const leaves = await source.aggregate(pivotQuery({ rows: plain.rows, columns: plain.columns, values: plain.values, maxLeaves: 100_000 }));
                    const capped = (q: PivotQuery): PivotQuery => pivotQuery({ ...q, maxLeaves: leaves.leafCount - 1 });
                    const wholeCells = await source.aggregate(capped(plain));
                    expect(wholeCells.isLevels).toBe(true);
                    const query = capped(questionOf(c.rules, c.limit, c.starts?.(wholeCells) ?? [])); // oxlint-disable-line ex-grid/no-stand-in-default -- a case that names no start starts every window at its first sibling
                    const answer = await source.aggregate(query);
                    expect(answer.isLevels).toBe(true);
                    expect(answer.mismatch(query)).toBeUndefined();
                    expect(checkWindows(query, answer, wholeCells)).toBeGreaterThan(0);
                    const written = PivotJson.writeAnswer(answer);
                    const read = PivotJson.readAnswer(written);
                    expect(PivotJson.writeAnswer(read)).toBe(written);
                    expect(read.mismatch(PivotJson.readQuery(PivotJson.writeQuery(query)))).toBeUndefined();
                }
            });
        }
    }

    it('ADR-0370: a set that fits the limit, or a question without a window, is answered whole', async () => {
        const source = new RecordPivotSource(trades(3, 1500, true), fields);
        const order = amount('sum', true);
        const leaves = await source.aggregate(pivotQuery({ rows: ['Region', 'Book'], columns: ['Live', 'Trader'], values: [{ field: 'Amount', parts: ['sum'] }], maxLeaves: 100_000 }));
        const ask = (window: boolean, limit: number): PivotQuery => pivotQuery({
            rows: ['Region', 'Book'], columns: ['Live', 'Trader'], values: [{ field: 'Amount', parts: ['sum'] }], maxLeaves: leaves.leafCount - 1,
            levels: window ? { rows: [{}, { order }], columns: liveCollapsed, window: { limit } } : { rows: [{}, { order }], columns: liveCollapsed },
        });
        expect((await source.aggregate(ask(false, 1))).windows).toHaveLength(0);
        expect((await source.aggregate(ask(true, 60))).windows).toHaveLength(0);
        expect((await source.aggregate(ask(true, 59))).windows.length).toBeGreaterThan(0);
    });

    it('ADR-0370: a set ordered by label is answered whole under a window, and the cap still refuses it', async () => {
        const source = new RecordPivotSource(trades(4, 1500, true), fields);
        const query = pivotQuery({
            rows: ['Region', 'Book'], columns: ['Live'], values: [{ field: 'Amount', parts: ['sum'] }], maxLeaves: 100,
            levels: { rows: [{}, {}], columns: [{}], window: { limit: 3 } },
        });
        const answer = await source.aggregate(query);
        expect(answer.refusal?.kind).toBe('tooManyLeaves');
    });

    it('ADR-0370: the TypeScript cube refuses a window by name until it lays one out', async () => {
        const source = new RecordPivotSource(trades(5, 1500, true), fields);
        const query = pivotQuery({
            rows: ['Region', 'Book'], columns: ['Live', 'Trader'], values: [{ field: 'Amount', parts: ['sum'] }], maxLeaves: 200,
            levels: { rows: [{}, { order: amount('sum', true) }], columns: liveCollapsed, window: { limit: 3 } },
        });
        const answer = await source.aggregate(query);
        expect(answer.windows.length).toBeGreaterThan(0);
        expect(() => pivotCube(query, answer, fields)).toThrow('The answer holds windows of sibling sets, which this engine does not lay out yet');
    });
});

describe('ADR-0370: a question with a window, and an answer that is not its answer', () => {
    const order = amount('sum', true);
    const ask = (limit: number, starts: readonly PivotWindowStart[] = []): PivotQuery => pivotQuery({
        rows: ['Region', 'Book'], values: [{ field: 'Amount', parts: ['sum'] }], maxLeaves: 50,
        levels: { rows: [{}, { order }], columns: [], window: { limit, starts } },
    });
    const east = textItemKey('East');

    // East with books B0…B(n−1), the sums n−1 … 0: the window shows `from` to `to` − 1, the margins beside it.
    function windowed(query: PivotQuery, n: number, from: number, to: number, rests: { before?: boolean; after?: boolean } = { before: true, after: true }): PivotAnswer {
        const builder = new PivotAnswerBuilder(query, 's');
        const cell = (rows: readonly PivotItemKey[], records: number, sum: number): void => {
            const at = builder.addCell(rows, [], records);
            builder.setCounts(at, 0, records, records);
            builder.setSum(at, 0, { kind: 'exact', value: decimalFromText(String(sum)) });
        };
        const total = (n * (n - 1)) / 2;
        cell([], n, total);
        cell([east], n, total);
        const window = builder.addWindow([east], from, n - to);
        let before = 0;
        let after = 0;
        for (let i = 0; i < n; i++) {
            if (i >= from - 1 && i < to + 1) cell([east, book(i)], 1, n - 1 - i);
            else if (i < from) before += n - 1 - i;
            else after += n - 1 - i;
        }
        const rest = (side: 'before' | 'after', count: number, sum: number): void => {
            const at = builder.addRest(window, side, [], count);
            builder.setCounts(at, 0, count, count);
            builder.setSum(at, 0, { kind: 'exact', value: decimalFromText(String(sum)) });
        };
        if (from >= 2 && rests.before !== false) rest('before', from - 1, before);
        if (n - to >= 2 && rests.after !== false) rest('after', n - to - 1, after);
        return builder.build();
    }

    it('ADR-0370: an honest window answers its question', () => {
        expect(windowed(ask(3, [{ rows: [east], start: 4 }]), 10, 4, 7).mismatch(ask(3, [{ rows: [east], start: 4 }]))).toBeUndefined();
        // A start past the end is taken back to the last limit's worth.
        expect(windowed(ask(3, [{ rows: [east], start: 40 }]), 10, 7, 10).mismatch(ask(3, [{ rows: [east], start: 40 }]))).toBeUndefined();
    });

    it('ADR-0370: a window not where it was asked, or of a set the limit answers whole, is named', () => {
        expect(windowed(ask(3), 10, 4, 7).mismatch(ask(3))).toBe('its window 0 under [Region=Text:East] shows siblings 4 to 6, and the question asks for 0 to 2');
        expect(windowed(ask(12), 10, 4, 7).mismatch(ask(12))).toBe('its window 0 under [Region=Text:East] is of 10 siblings, which the limit of 12 answers whole');
        const plain = pivotQuery({ rows: ['Region', 'Book'], values: [{ field: 'Amount', parts: ['sum'] }], maxLeaves: 50, levels: { rows: [{}, { order }], columns: [] } });
        expect(windowed(plain, 10, 4, 7).mismatch(plain)).toBe('it answers a window, and the question asks for none');
    });

    it('ADR-0370: a side with siblings beyond its margin carries their rest, and only then', () => {
        expect(windowed(ask(3, [{ rows: [east], start: 4 }]), 10, 4, 7, { before: false }).mismatch(ask(3, [{ rows: [east], start: 4 }])))
            .toBe('its window 0 has 4 siblings before it and no rest cell for those beyond its margin');
    });

    it('ADR-0370: a set past the limit answered whole is named', () => {
        const builder = new PivotAnswerBuilder(ask(3), 's');
        for (const rows of [[], [east], ...[0, 1, 2, 3].map(n => [east, book(n)])]) {
            const at = builder.addCell(rows, [], 1);
            builder.setCounts(at, 0, 1, 1);
            builder.setSum(at, 0, { kind: 'exact', value: decimalFromText('1') });
        }
        expect(builder.build().mismatch(ask(3))).toBe('it answers the 4 siblings under [Region=Text:East] whole, past the window\'s limit of 3');
    });

    // East's books B0…B(n−1) summing to `sums`, the window showing `from` to `to` − 1 with its margins, each rest
    // summing its books plus `restOff`.
    function books(query: PivotQuery, sums: readonly number[], from: number, to: number, restOff = 0): PivotAnswer {
        const builder = new PivotAnswerBuilder(query, 's');
        const cell = (at: number, records: number, sum: number): void => {
            builder.setCounts(at, 0, records, records);
            builder.setSum(at, 0, { kind: 'exact', value: decimalFromText(String(sum)) });
        };
        const n = sums.length;
        const total = sums.reduce((a, b) => a + b, 0);
        cell(builder.addCell([], [], n), n, total);
        cell(builder.addCell([east], [], n), n, total);
        const window = builder.addWindow([east], from, n - to);
        sums.forEach((sum, i) => {
            if (i >= from - 1 && i < to + 1) cell(builder.addCell([east, book(i)], [], 1), 1, sum);
        });
        const beyond = (side: 'before' | 'after', sumsBeyond: readonly number[]): void => {
            if (sumsBeyond.length > 0) cell(builder.addRest(window, side, [], sumsBeyond.length), sumsBeyond.length, sumsBeyond.reduce((a, b) => a + b, 0) + restOff);
        };
        beyond('before', sums.slice(0, Math.max(0, from - 1)));
        beyond('after', sums.slice(to + 1));
        return builder.build();
    }

    it('ADR-0370: the cube checks a window against its parent, rests included, and refuses one that does not add up', () => {
        const asked = ask(3, [{ rows: [east], start: 4 }]);
        const sums = [9, 8, 7, 6, 5, 4, 3, 2, 1, 0];
        expect(() => pivotCube(asked, books(asked, sums, 4, 7), fields)).toThrow('which this engine does not lay out yet');
        expect(() => pivotCube(asked, books(asked, sums, 4, 7, 1), fields))
            .toThrow("The server's answer for [Region=Text:East] × [] does not add up: its sum of 'Amount' is not what its row groups below compose to");
    });

    it('ADR-0370: the cube refuses a window whose margin ties with the sibling at its edge', () => {
        const asked = ask(3, [{ rows: [east], start: 4 }]);
        expect(books(asked, [9, 8, 7, 5, 5, 4, 3, 2, 1, 0], 4, 7).mismatch(asked)).toBeUndefined();
        expect(() => pivotCube(asked, books(asked, [9, 8, 7, 5, 5, 4, 3, 2, 1, 0], 4, 7), fields))
            .toThrow("The server's window under [Region=Text:East] × [] is not where its order puts it: the margin before it, "
                + '[Region=Text:East, Book=Text:B3] × [], does not sort strictly before [Region=Text:East, Book=Text:B4] × []');
        expect(() => pivotCube(asked, books(asked, [9, 8, 7, 6, 5, 4, 3, 3, 1, 0], 4, 7), fields))
            .toThrow('the margin after it, [Region=Text:East, Book=Text:B7] × [], does not sort strictly after [Region=Text:East, Book=Text:B6] × []');
    });

    it('ADR-0370: an order a column field says, or one that reads a part not asked for, is refused', () => {
        expect(() => pivotQuery({ rows: ['Region'], columns: ['Live'], values: [{ field: 'Amount', parts: ['sum'] }], levels: { rows: [{}], columns: [{ order }] } }))
            .toThrow("A column field's rule says no order");
        expect(() => pivotQuery({ rows: ['Region'], values: [{ field: 'Amount' }], levels: { rows: [{ order }], columns: [] } }))
            .toThrow("An order reads the sum of 'Amount', and the question asks for none of its parts");
        expect(() => pivotQuery({ rows: ['Region'], values: [{ field: 'Amount', parts: ['sum'] }], levels: { rows: [{}], columns: [], window: { limit: 0 } } }))
            .toThrow('A window holds at least one sibling, not 0.');
    });
});
