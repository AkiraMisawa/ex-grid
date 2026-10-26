// Layer 1: the bundled sources' Level Aggregates (ADR-0370 section 3) against the check a server's are held to.
// Over data chosen to make rounding show — magnitudes twenty orders apart, exact and double numbers mixed,
// Blanks — every sibling set of every level answer must add up to its parent, the records too, and the answer
// must cross PivotJson as the same text. The shared cases (tests/cases/pivot/source-levels.json) hold what both
// engines answer; this holds that what they answer is what a server is held to.
import { decimalFromText } from '@ex-grid/data';
import { describe, expect, it } from 'vitest';
import {
    createPivotLayout, levelQueryForLayout, pivotCube, PivotJson, pivotQuery, pivotRecordField, pivotReport, queryForLayout, RecordPivotSource, pivotItemKey, type PivotAnswer, type PivotAnswerValues, type PivotLevelRuleInput,
} from '../src/index';
import { levelMismatch, type LevelParts } from '../src/levels/level-check';
import { everyReading, read } from './readings';
import type { DataSet } from './cases';
import { elementAt } from '../src/must';

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
    readonly desk: string | null;
    readonly book: string;
    readonly live: boolean;
    readonly amount: unknown;
}

// The readings taken need no data set.
const noData: DataSet = { fields: [], records: [], keyCalls: [] };

const fields = [
    pivotRecordField<Trade>({ name: 'Region', type: 'text', value: t => t.region }),
    pivotRecordField<Trade>({ name: 'Desk', type: 'text', value: t => t.desk }),
    pivotRecordField<Trade>({ name: 'Book', type: 'text', value: t => t.book }),
    pivotRecordField<Trade>({ name: 'Live', type: 'boolean', value: t => t.live }),
    pivotRecordField<Trade>({ name: 'Amount', type: 'number', value: t => t.amount }),
];

function pick<T extends string | number | null>(next: () => number, items: readonly T[]): T {
    return elementAt(items, Math.floor(next() * items.length));
}

function trades(seed: number, count: number, exact: boolean): Trade[] {
    const next = random(seed);
    return Array.from({ length: count }, () => {
        const scale = pick(next, [1e-6, 1, 1e6, 1e14]);
        const value = (next() - 0.5) * scale;
        const amount = next() < 0.05 ? null : exact ? decimalFromText(value.toFixed(6)) : value;
        return {
            region: pick(next, ['East', 'West', 'north', 'South']),
            desk: pick(next, ['Rates', 'Credit', 'FX', null]),
            book: pick(next, ['A', 'B', 'C', 'D', 'E']),
            live: next() < 0.5,
            amount,
        };
    });
}

// One cell's parts as a server answers them, which the check reads.
function partsAt(values: PivotAnswerValues, cell: number): LevelParts {
    const counts = { values: values.valuesAt(cell), numbers: values.numbersAt(cell), nonFinite: values.nonFiniteAt(cell) };
    if (counts.numbers === 0 || counts.nonFinite) return counts;
    const absoluteSum = values.absoluteSumAt(cell);
    return {
        ...counts,
        sum: values.sumAt(cell),
        ...(absoluteSum === undefined ? {} : { absoluteSum }),
        min: values.minAt(cell),
        max: values.maxAt(cell),
        product: values.productAt(cell),
        mean: values.meanAt(cell),
        m2: values.m2At(cell),
    };
}

// Each cell's Item at every level, rows then columns, −1 below its depth.
function pathsOf(answer: PivotAnswer): number[][] {
    const axes = [...answer.rows, ...answer.columns];
    return Array.from({ length: answer.leafCount }, (_, cell) => axes.map(axis => axis.itemAt(cell)));
}

// Every sibling set of a level answer checked against its parent: the cells one level below a cell along one
// axis, the other axis's group the same. How many sets were checked.
function checkSiblings(answer: PivotAnswer): number {
    const paths = pathsOf(answer);
    const rowCount = answer.rows.length;
    const axes: readonly (readonly [number, number])[] = [[0, rowCount], [rowCount, rowCount + answer.columns.length]];
    let checked = 0;
    paths.forEach((parent, cell) => {
        for (const [from, to] of axes) {
            let depth = from;
            while (depth < to && elementAt(parent, depth) >= 0) depth++;
            if (depth === to) continue;
            const children = paths.flatMap((path, child) => {
                const below = path.every((item, level) => (level === depth ? item >= 0 : item === elementAt(parent, level)));
                return below ? [child] : [];
            });
            if (children.length === 0) continue;
            checked++;
            const records = children.reduce((sum, child) => sum + answer.recordsAt(child), 0);
            expect(records, `records of cell ${cell}`).toBe(answer.recordsAt(cell));
            for (const values of answer.values) {
                expect(levelMismatch(partsAt(values, cell), children.map(child => partsAt(values, child))), `cell ${cell}, level ${depth}`).toBeUndefined();
            }
        }
    });
    return checked;
}

const rules: { readonly name: string; readonly rows: readonly PivotLevelRuleInput[]; readonly columns: readonly PivotLevelRuleInput[] }[] = [
    { name: 'Region collapsed', rows: [{ collapsed: true }, {}, {}], columns: [{}] },
    { name: 'Region collapsed but East', rows: [{ collapsed: true, toggledItems: [pivotItemKey('text', 'east')] }, {}, {}], columns: [{}] },
    { name: 'Desk collapsed but Rates', rows: [{}, { collapsed: true, toggledItems: [pivotItemKey('text', 'Rates')] }, {}], columns: [{}] },
];

describe('ADR-0370: the bundled sources\' Level Aggregates are what a server is held to', () => {
    for (const exact of [false, true]) {
        for (const rule of rules) {
            it(`ADR-0370: every sibling set adds up to its parent, ${rule.name}, ${exact ? 'exact' : 'double'} amounts`, async () => {
                for (const seed of [1, 2, 3]) {
                    const source = new RecordPivotSource(trades(seed, 2000, exact), fields);
                    const asked = {
                        rows: ['Region', 'Desk', 'Book'], columns: ['Live'],
                        values: [{ field: 'Amount', parts: ['sum', 'extremes', 'product', 'variance'] as const }],
                    };
                    const leaves = await source.aggregate(pivotQuery({ ...asked, maxLeaves: 1000 }));
                    // A cap the leaves pass and the cells do not: four regions by four desks by five books by two.
                    const query = pivotQuery({ ...asked, maxLeaves: leaves.leafCount - 1, levels: { rows: rule.rows, columns: rule.columns } });
                    const answer = await source.aggregate(query);

                    expect(answer.isLevels).toBe(true);
                    expect(answer.mismatch(query)).toBeUndefined();
                    expect(checkSiblings(answer)).toBeGreaterThan(0);
                    // The cube checks the same sets, and the report is the leaves' report, to the last bit.
                    const layout = createPivotLayout({
                        rows: rule.rows.map((r, i) => ({ field: elementAt(asked.rows, i), ...r })),
                        columns: [{ field: 'Live' }],
                        values: [
                            { field: 'Amount', aggregation: 'sum' }, { field: 'Amount', aggregation: 'max' },
                            { field: 'Amount', aggregation: 'product' }, { field: 'Amount', aggregation: 'stdDev' },
                        ],
                    });
                    const leafQuery = queryForLayout(layout, 1000);
                    const fromLeaves = pivotReport(pivotCube(leafQuery, await source.aggregate(leafQuery), fields), layout);
                    const levelQuery = levelQueryForLayout(layout, leaves.leafCount - 1);
                    const fromLevels = pivotReport(pivotCube(levelQuery, await source.aggregate(levelQuery), fields), layout);
                    expect(read(fromLevels, everyReading, noData)).toEqual(read(fromLeaves, everyReading, noData));
                    const written = PivotJson.writeAnswer(answer);
                    expect(PivotJson.writeAnswer(PivotJson.readAnswer(written))).toBe(written);
                }
            });
        }
    }
});
