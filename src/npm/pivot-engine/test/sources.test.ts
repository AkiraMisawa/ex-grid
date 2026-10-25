// Layer 1: the Pivot Source cases (tests/cases/pivot; ADR-0370 sections 2 and 5, ADR-0066), kind
// "pivotSource", run against the Script Core's engine (source-cases.ts).
// tests/ExPivot.Engine.Tests/Cases/PivotSourceCaseTests.cs is the C# runner, and the one that writes the
// answers.
import { describe, expect, it } from 'vitest';
import { FetchingPivotSource, pivotField, PivotItemCounts, PivotSourceFeatures } from '../src/index';
import { caseFiles, must, text } from './cases';
import { sourceAnswer } from './source-cases';

for (const file of caseFiles('pivotSource')) {
    describe(`pivot/${file.file}`, () => {
        for (const entry of file.cases) {
            it(text(must(entry, 'name')), async () => {
                expect(await sourceAnswer(file.document, entry)).toEqual(entry['scriptExpect'] ?? must(entry, 'expect'));
            });
        }
    });
}

function countedTwice(): Promise<PivotItemCounts> {
    return Promise.resolve(new PivotItemCounts('1', [['Desk', 2]]));
}

// ADR-0370 section 3: a fetching source declares that it counts Items exactly when a fetcher counts them, and
// a count is a whole number of at least 0.
describe('item counts', () => {
    const fetchers = {
        aggregate: () => Promise.reject(new Error('not asked')),
        items: () => Promise.reject(new Error('not asked')),
        details: () => Promise.reject(new Error('not asked')),
    };
    const fields = [pivotField({ name: 'Desk', type: 'text' })];

    it('ADR-0370: a source that declares counting needs a fetcher that counts, and one that counts declares it', () => {
        expect(() => new FetchingPivotSource(fields, new PivotSourceFeatures(['sum'], false, true), fetchers))
            .toThrow('The source declares that it counts its fields\' Items, and no fetcher counts them (ADR-0370 section 3).');
        expect(() => new FetchingPivotSource(fields, new PivotSourceFeatures(['sum']), { ...fetchers, itemCounts: countedTwice }))
            .toThrow('A fetcher counts the fields\' Items, and the source\'s features do not declare it (ADR-0370 section 3).');
    });

    it('ADR-0370: a count is a whole number of at least 0, of a field counted once', () => {
        expect(() => new PivotItemCounts('1', [['Desk', -1]])).toThrow(RangeError);
        expect(() => new PivotItemCounts('1', [['Desk', 1.5]])).toThrow(TypeError);
        expect(() => new PivotItemCounts('1', [['Desk', 1], ['Desk', 2]])).toThrow(TypeError);
        expect(new PivotItemCounts('1', [['Desk', 0]]).countOf('Desk')).toBe(0);
        expect(new PivotItemCounts('1', []).countOf('Desk')).toBeUndefined();
    });
});
