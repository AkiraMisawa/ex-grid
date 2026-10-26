// Layer 1: the wire cases (tests/cases/pivot; ADR-0370 sections 2 and 3, ADR-0066), kind "pivotJson": a Pivot
// Source document read as the document it names and written again, as the same text the C# engine writes, or
// what refuses it. tests/ExPivot.Engine.Tests/Cases/PivotJsonCaseTests.cs is the C# runner, and the one that
// writes the answers.
import { describe, expect, it } from 'vitest';
import { datePartField, PivotJson, pivotDetailsQuery, pivotField, pivotQuery, type PivotField, type PivotFieldType } from '../src/index';
import { jsonDouble } from '../src/json/json-text';
import { doubleRaw } from '../src/text/invariant';
import { caseFiles, must, refusalOf, text, type Json } from './cases';

function written(document: string, json: string): string {
    switch (document) {
        case 'query': return PivotJson.writeQuery(PivotJson.readQuery(json));
        case 'answer': return PivotJson.writeAnswer(PivotJson.readAnswer(json));
        case 'itemsQuery': return PivotJson.writeItemsQuery(PivotJson.readItemsQuery(json));
        case 'itemPage': return PivotJson.writeItemPage(PivotJson.readItemPage(json));
        case 'detailsQuery': return PivotJson.writeDetailsQuery(PivotJson.readDetailsQuery(json));
        case 'detailPage': return PivotJson.writeDetailPage(PivotJson.readDetailPage(json));
        case 'sourceChanged': return PivotJson.writeSourceChanged(PivotJson.readSourceChanged(json));
        case 'itemCounts': return PivotJson.writeItemCounts(PivotJson.readItemCounts(json));
        case 'changeBatch': {
            // The Script Core reads a batch against the source's fields; here the source's are the batch's own.
            const fields = fieldsOf(json);
            const notice = PivotJson.readServerNotice(json, fields);
            if (notice.kind !== 'changeBatch') throw new Error('A changeBatch document reads as a Change Batch.');
            return PivotJson.writeChangeBatch(notice, fields);
        }
        default: throw new Error(`'${document}' is no document the cases read.`);
    }
}

// The fields a batch document names, where it names them as a reader takes them; none otherwise, so that the
// reader refuses the document in its own words.
function fieldsOf(json: string): PivotField[] {
    const root: unknown = JSON.parse(json);
    const named: unknown = typeof root === 'object' && root !== null ? Reflect.get(root, 'fields') : undefined;
    if (!Array.isArray(named)) return [];
    const types: readonly unknown[] = ['text', 'number', 'date', 'boolean'];
    const fields: PivotField[] = [];
    for (const field of named as unknown[]) {
        const name: unknown = typeof field === 'object' && field !== null ? Reflect.get(field, 'name') : undefined;
        const type: unknown = typeof field === 'object' && field !== null ? Reflect.get(field, 'type') : undefined;
        const part: unknown = typeof field === 'object' && field !== null ? Reflect.get(field, 'datePart') : undefined;
        if (typeof name !== 'string' || name.length === 0 || !types.includes(type)) return [];
        if (part === 'year' || part === 'quarter' || part === 'month') fields.push(datePartField(name, 'Date', part));
        else fields.push(pivotField({ name, type: type as PivotFieldType }));
    }
    return fields;
}

function answer(document: string, json: string): Json {
    try {
        return written(document, json);
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a refusal is the document's answer, compared below
        return refusalOf(error);
    }
}

for (const file of caseFiles('pivotJson')) {
    describe(`pivot/${file.file}`, () => {
        for (const entry of file.cases) {
            it(text(must(entry, 'name')), () => {
                expect(answer(text(must(entry, 'document')), text(must(entry, 'read')))).toEqual(must(entry, 'expect'));
            });
        }
    });
}

// ADR-0066: what the C# engine cannot hold is refused as it is written, not by the server that reads it.
describe('writing what a document cannot hold', () => {
    it('ADR-0066: a cap past 32 bits is refused by the writer, as the C# engine\'s int cannot hold it', () => {
        expect(() => PivotJson.writeQuery(pivotQuery({ maxLeaves: 2 ** 31 }))).toThrow(RangeError);
        expect(() => PivotJson.writeDetailsQuery(pivotDetailsQuery({ sourceVersion: '1', count: 2 ** 31 }))).toThrow(RangeError);
        expect(PivotJson.readDetailsQuery(PivotJson.writeDetailsQuery(pivotDetailsQuery({ sourceVersion: '1' }))).count).toBe(2 ** 31 - 1);
    });

    it('ADR-0066: text that is not UTF-16, a lone surrogate, is refused as Utf8JsonWriter refuses it', () => {
        expect(() => PivotJson.writeSourceChanged({ sourceVersion: 'a\uD800b' })).toThrow(TypeError);
        expect(PivotJson.writeSourceChanged({ sourceVersion: 'a😀' })).toBe('{"version":1,"type":"sourceChanged","sourceVersion":"a\\uD83D\\uDE00"}');
    });
});

// ADR-0066: a double is written as .NET's "R" writes it; between 10^-4 and 10^15 the writer takes JavaScript's
// own text, which must be that one.
it('ADR-0066: a double the writer spells by JavaScript\'s text is spelt as .NET\'s round trip spells it', () => {
    let state = 1;
    const next = (): number => {
        state = (state * 48_271) % 2_147_483_647;
        return state / 2_147_483_647;
    };
    for (let i = 0; i < 20_000; i++) {
        const value = (next() < 0.5 ? -1 : 1) * 10 ** (next() * 40 - 20) * (next() < 0.1 ? 1 : next());
        const spelt = next() < 0.2 ? Math.round(value) : value;
        expect(jsonDouble(spelt)).toBe(doubleRaw(spelt));
    }
    for (const edge of [1e-4, 9.999999999999999e-5, 1e15, 999_999_999_999_999.9, -0, 0, 0.30000000000000004]) expect(jsonDouble(edge)).toBe(doubleRaw(edge));
});
