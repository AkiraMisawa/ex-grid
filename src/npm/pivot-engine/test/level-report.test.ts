// Layer 1: a report laid out from Level Aggregates (ADR-0370 section 3) is the report laid out from the leaves.
// Excel's rules stay in the engine, so every report case is laid out twice — from the bundled source's leaves and
// from its level answer to the question with the layout's levels, under a cap the leaves pass — and every
// reading of the two must agree, to the last bit. A case is taken as written, and with its outermost row field,
// its outermost column field and both collapsed, so that a level answer has groups to stand in for.
import { describe, expect, it } from 'vitest';
import { levelQueryForLayout, pivotCube, pivotReport, queryForLayout, RecordPivotSource } from '../src/index';
import { array, caseFiles, dataSet, layoutOf, must, object, optionsOf, text, type Json, type JsonObject } from './cases';
import { everyReading, read } from './readings';

// The layout written, with the first field of each axis named collapsed.
function collapsing(written: JsonObject, axes: readonly string[]): JsonObject {
    const layout: Record<string, Json> = { ...written };
    for (const axis of axes) {
        const fields = written[axis];
        if (fields === undefined || array(fields).length < 2) continue;
        layout[axis] = array(fields).map((field, i) => (i === 0 ? { ...object(field), collapsed: true } : field));
    }
    return layout;
}

const variants: readonly (readonly string[])[] = [[], ['rows'], ['columns'], ['rows', 'columns']];

let compared = 0;
for (const file of caseFiles('pivotReport')) {
    describe(`pivot/${file.file}`, () => {
        for (const entry of file.cases) {
            if (entry['throws'] !== undefined || entry['cubeOf'] !== undefined) continue;
            it(`ADR-0370: from Level Aggregates, ${text(must(entry, 'name'))}`, async () => {
                const data = dataSet(file.document, entry);
                const options = optionsOf(file.document, entry);
                const source = new RecordPivotSource(data.records, data.fields);
                for (const axes of variants) {
                    const layout = layoutOf(collapsing(object(must(entry, 'layout')), axes));
                    const leafQuery = queryForLayout(layout, Number.MAX_SAFE_INTEGER);
                    const leaves = await source.aggregate(leafQuery);
                    if (leaves.leafCount < 2) continue;
                    const levelQuery = levelQueryForLayout(layout, leaves.leafCount - 1);
                    const levels = await source.aggregate(levelQuery);
                    // Every group expanded makes more cells than leaves: no cap stands between them.
                    if (levels.isRefused) continue;
                    expect(levels.isLevels).toBe(true);
                    const fromLeaves = pivotReport(pivotCube(leafQuery, leaves, data.fields), layout, options);
                    const fromLevels = pivotReport(pivotCube(levelQuery, levels, data.fields), layout, options);
                    expect(read(fromLevels, everyReading, data), axes.join(' and ')).toEqual(read(fromLeaves, everyReading, data));
                    compared++;
                }
            });
        }
    });
}

describe('ADR-0370: the report cases reach Level Aggregates', () => {
    it('ADR-0370: enough layouts collapse a group to make a level answer of them', () => {
        expect(compared).toBeGreaterThan(50);
    });
});
