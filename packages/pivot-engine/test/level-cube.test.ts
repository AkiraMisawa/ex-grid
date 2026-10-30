// Layer 1: a report laid out from a server's Level Aggregates (ADR-0370 section 3). Every sibling set is checked
// against its parent as the cube is made, and one that does not add up refuses the answer, naming the parent;
// a report source over a server that answers them asks with the groups its layout shows, and asks again when an
// expand shows others. That the report is the report of the leaves is held by level-report.test.ts.
import { decimalFromText } from '@ex-grid/data';
import { describe, expect, it } from 'vitest';
import {
    createPivotLayout, FetchingPivotSource, levelQueryForLayout, LocalPivotReportSource, PivotAnswerBuilder, pivotCube, PivotJson, pivotRecordField,
    pivotReport, PivotSourceFeatures, RecordPivotSource, textItemKey, type PivotAnswer, type PivotItemKey, type PivotLayout, type PivotQuery,
    type PivotReportUpdate,
} from '../src/index';

interface Sale {
    readonly region: string;
    readonly product: string;
    readonly amount: string;
}

const sales: readonly Sale[] = [
    { region: 'East', product: 'a', amount: '1' },
    { region: 'East', product: 'b', amount: '2' },
    { region: 'West', product: 'a', amount: '3' },
    { region: 'West', product: 'b', amount: '4' },
];

const fields = [
    pivotRecordField<Sale>({ name: 'Region', type: 'text', value: s => s.region }),
    pivotRecordField<Sale>({ name: 'Product', type: 'text', value: s => s.product }),
    pivotRecordField<Sale>({ name: 'Amount', type: 'number', value: s => decimalFromText(s.amount) }),
];

const east = textItemKey('East');
const west = textItemKey('West');

function layoutOf(expanded: readonly PivotItemKey[] = []): PivotLayout {
    return createPivotLayout({
        rows: [{ field: 'Region', collapsed: true, toggledItems: expanded }, { field: 'Product' }],
        values: [{ field: 'Amount', aggregation: 'sum', numberFormat: '0' }],
    });
}

type Cell = readonly [rows: readonly PivotItemKey[], records: number, sum: string];

// A server's GROUPING SETS answer, its cells as given: the row group's Items, the records and the exact sum.
function answerOf(query: PivotQuery, cells: readonly Cell[]): PivotAnswer {
    const builder = new PivotAnswerBuilder(query, 'server');
    for (const [items, records, sum] of cells) {
        const cell = builder.addCell(items, [], records);
        builder.setCounts(cell, 0, records, records);
        builder.setSum(cell, 0, { kind: 'exact', value: decimalFromText(sum) });
    }
    return builder.build();
}

describe('ADR-0370: a report from a server\'s Level Aggregates', () => {
    const query = levelQueryForLayout(layoutOf(), 3);
    const honest: readonly Cell[] = [[[], 4, '10'], [[east], 2, '3'], [[west], 2, '7']];

    it('ADR-0370: an answer that adds up is laid out, each collapsed group as one row', () => {
        const report = pivotReport(pivotCube(query, answerOf(query, honest), fields), layoutOf(), { locale: 'invariant' });
        expect(report.rows.map(row => `${row.labels.map(label => label.text).join('/')} ${report.valueAt(row, 0)?.text}`)).toEqual(['East 3', 'West 7', 'Grand Total 10']);
    });

    it('ADR-0370: a group whose children do not add up to it refuses the answer, naming the group', () => {
        const sum = answerOf(query, [[[], 4, '11'], [[east], 2, '3'], [[west], 2, '7']]);
        expect(() => pivotCube(query, sum, fields)).toThrow("The server's answer for [] × [] does not add up: its sum of 'Amount' is not what its row groups below compose to (ADR-0370 section 3).");
        const records = answerOf(query, [[[], 5, '10'], [[east], 2, '3'], [[west], 2, '7']]);
        expect(() => pivotCube(query, records, fields)).toThrow("its records (5) is not what its row groups below compose to");
        // A child left out leaves the parent's records unexplained.
        const missing = answerOf(query, [[[], 4, '10'], [[east], 2, '3']]);
        expect(() => pivotCube(query, missing, fields)).toThrow("The server's answer for [] × [] does not add up: its records (4)");
    });

    it('ADR-0370: an expanded group is checked against its children too', () => {
        const expanded = levelQueryForLayout(layoutOf([east]), 5);
        const children: readonly Cell[] = [...honest, [[east, textItemKey('a')], 1, '1'], [[east, textItemKey('b')], 1, '2']];
        expect(pivotCube(expanded, answerOf(expanded, children), fields).isLevels).toBe(true);
        const wrong: readonly Cell[] = [...honest, [[east, textItemKey('a')], 1, '1'], [[east, textItemKey('b')], 1, '1']];
        expect(() => pivotCube(expanded, answerOf(expanded, wrong), fields)).toThrow("The server's answer for [Region=Text:East] × [] does not add up: its sum of 'Amount'");
    });

    it('ADR-0370: a cube of Level Aggregates holds the groups it was asked for, and a collapse of them', () => {
        const cube = pivotCube(query, answerOf(query, honest), fields);
        expect(cube.holds(layoutOf())).toBe(true);
        expect(cube.holds(layoutOf([east]))).toBe(false);
        expect(() => pivotReport(cube, layoutOf([east]), { locale: 'invariant' })).toThrow(
            'The cube was made from Level Aggregates of the groups its question showed, and this layout shows groups below them it does not hold');
        const expanded = levelQueryForLayout(layoutOf([east]), 5);
        const wider = pivotCube(expanded, answerOf(expanded, [...honest, [[east, textItemKey('a')], 1, '1'], [[east, textItemKey('b')], 1, '2']]), fields);
        expect(wider.holds(layoutOf())).toBe(true);
        expect(wider.holds(layoutOf([east, west]))).toBe(false);
    });
});

// The texts of a Window's rows.
function texts(update: PivotReportUpdate): string[] {
    if (update.refusal !== undefined) throw new Error(`Refused: ${update.refusal.message}`);
    return update.rows.map(row => `${row.labels.map(label => label.text).join('/')} ${row.values.map(value => value?.text).join(' ')}`);
}

describe('ADR-0370: a report source over a server that answers Level Aggregates', () => {
    it('ADR-0370: asks with the groups shown, answers past the cap, and asks again when an expand shows others', async () => {
        const inner = new RecordPivotSource(sales, fields);
        const asked: PivotQuery[] = [];
        // The server: every question and answer crosses PivotJson, as a server's would.
        const server = new FetchingPivotSource(inner.fields, new PivotSourceFeatures(inner.features.aggregations, false, false, true), {
            aggregate: async (question, signal) => {
                const read = PivotJson.readQuery(PivotJson.writeQuery(question));
                asked.push(read);
                return PivotJson.readAnswer(PivotJson.writeAnswer(await inner.aggregate(read, signal)));
            },
            items: (question, signal) => inner.items(question, signal),
            details: (question, signal) => inner.details(question, signal),
        });
        const reports = new LocalPivotReportSource(server);
        const everything = new LocalPivotReportSource(inner);
        const window = { start: 0, count: 20 };
        const settings = { locale: 'invariant', words: {} };

        const collapsed = await reports.window({ requestId: 'r1', layout: layoutOf(), settings, window, maxLeaves: 3 });
        expect(asked.at(-1)?.levels?.rows[0]?.collapsed).toBe(true);
        // Four leaves pass the cap of three; the three groups shown do not.
        expect(texts(collapsed)).toEqual(texts(await everything.window({ requestId: 'e1', layout: layoutOf(), settings, window })));

        const expanded = await reports.window({ requestId: 'r2', layout: layoutOf([west]), settings, window, maxLeaves: 5 });
        expect(asked).toHaveLength(2);
        expect(texts(expanded)).toEqual(texts(await everything.window({ requestId: 'e2', layout: layoutOf([west]), settings, window })));

        // A collapse is laid out from the groups held: nothing is asked.
        const again = await reports.window({ requestId: 'r3', layout: layoutOf(), settings, window, maxLeaves: 5 });
        expect(asked).toHaveLength(2);
        expect(texts(again)).toEqual(texts(collapsed));
    });
});
