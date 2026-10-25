// Layer 1: the report cases (tests/cases/pivot, kind "pivotReport"; ADR-0370 sections 2 and 5), run against
// the Script Core's engine. A case is a layout over a data set under a locale and words, and the report read
// back as the C# runner reads one (./readings), or the refusal it meets. tests/ExPivot.Engine.Tests/Cases/
// PivotReportCaseTests.cs is the C# runner, and the one that writes the answers.
import { describe, expect, it } from 'vitest';
import {
    aggregatePivot, computePivot, itemsOfCube, pivotReport, recordsBehind, type PivotCube, type PivotItemInfo, type PivotLayout,
    type PivotReport,
} from '../src/index';
import { array, caseFiles, dataSet, layoutOf, must, object, optionsOf, refusalOf, text, type DataSet, type Json, type JsonObject } from './cases';
import { itemKey, read } from './readings';


// An Item as Filter… lists it: its key, its label and whether it is hidden.
function item(info: PivotItemInfo): JsonObject {
    return { key: itemKey(info.key), label: info.label, isHidden: info.isHidden };
}

// A reading, or the refusal it meets.
function attempt(reading: () => Json): Json {
    try {
        return reading();
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a refusal is the reading's answer, compared below
        return refusalOf(error);
    }
}

function answerOf(file: JsonObject, entry: JsonObject): JsonObject {
    const options = optionsOf(file, entry);
    let data: DataSet;
    let layout: PivotLayout;
    let cube: PivotCube;
    let report: PivotReport;
    try {
        data = dataSet(file, entry);
        layout = layoutOf(must(entry, 'layout'));
        const cubeOf = entry['cubeOf'];
        cube = aggregatePivot(data.records, data.fields, cubeOf === undefined ? layout : layoutOf(cubeOf));
        report = pivotReport(cube, layout, options);
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a refusal is the case's answer, compared below
        return refusalOf(error);
    }
    const also = entry['also'];
    const answer = read(report, also === undefined ? [] : array(also).map(text), data);
    const holds = entry['holds'];
    if (holds !== undefined) answer['holds'] = array(holds).map(other => cube.holds(layoutOf(other)));
    const itemsOf = entry['itemsOf'];
    if (itemsOf !== undefined) {
        answer['items'] = Object.fromEntries(array(itemsOf).map(text)
            .map(field => [field, attempt(() => itemsOfCube(cube, layout, field, options).map(item))]));
    }
    const behind = entry['behind'];
    if (behind !== undefined) {
        answer['behind'] = array(behind).map(cell => {
            const [row, column] = array(cell);
            if (typeof row !== 'number' || typeof column !== 'number') throw new TypeError(`${JSON.stringify(cell)} is not a row and a value column.`);
            const at = report.rows[row];
            if (at === undefined) throw new RangeError(`The report has no row ${row}.`);
            return recordsBehind(data.records, data.fields, report, at, column).map(record => data.records.indexOf(record));
        });
    }
    const sameRowsAs = entry['sameRowsAs'];
    if (sameRowsAs !== undefined) {
        answer['sameRows'] = array(sameRowsAs).map(written => {
            const other = object(written);
            const records = other['records'] === undefined ? data.records : dataSet(file, { ...entry, records: other['records'] }).records;
            const otherLayout = other['layout'] === undefined ? layout : layoutOf(other['layout']);
            return report.hasSameRowsAs(computePivot(records, data.fields, otherLayout, options));
        });
    }
    return answer;
}

for (const file of caseFiles('pivotReport')) {
    describe(`pivot/${file.file}`, () => {
        for (const entry of file.cases) {
            it(text(must(entry, 'name')), () => {
                const refusal = entry['throws'];
                // A refusal that names the platform's own API or types says the Script Core's in `scriptSaying`.
                const scriptSaying = entry['scriptSaying'];
                const expected = refusal === undefined ? must(entry, 'expect') : scriptSaying === undefined ? refusal : { ...object(refusal), saying: scriptSaying };
                expect(answerOf(file.document, entry)).toEqual(expected);
            });
        }
    });
}
